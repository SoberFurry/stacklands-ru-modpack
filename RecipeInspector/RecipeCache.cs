using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RecipeInspectorNS
{
    /// <summary>
    /// Built once per world load on InitIdeaElements (when SokLoc is ready).
    /// Never rebuilt per-frame or per-keystroke.
    /// </summary>
    public static class RecipeCache
    {
        public static Dictionary<string, List<Blueprint>> RecipesByResult     = new Dictionary<string, List<Blueprint>>();
        public static Dictionary<string, List<Blueprint>> RecipesByIngredient = new Dictionary<string, List<Blueprint>>();
        public static Dictionary<string, string>          LocalizedNames      = new Dictionary<string, string>();
        public static Dictionary<string, string>          RuNames             = new Dictionary<string, string>();
        public static Dictionary<string, Sprite>          CardIcons           = new Dictionary<string, Sprite>();

        public static bool IsReady { get; private set; }
        private static bool _buildErrorLogged;
        private static ModLogger L;

        // Russian localization term→text (loaded once from TSV)
        private static Dictionary<string, string> _ruTermMap = new Dictionary<string, string>();
        private static bool _ruLoaded;

        public static void SetLogger(ModLogger logger) { L = logger; }

        /// <summary>
        /// Build the cache. Call from InitIdeaElements postfix (SokLoc and WorldManager are ready by then).
        /// </summary>
        public static void Build()
        {
            try
            {
                RecipesByResult.Clear();
                RecipesByIngredient.Clear();
                LocalizedNames.Clear();
                RuNames.Clear();
                CardIcons.Clear();
                IsReady = false;
                _buildErrorLogged = false;

                if (WorldManager.instance?.GameDataLoader == null)
                {
                    if (L != null) L.Log("RecipeCache: WorldManager not ready, deferring.");
                    return;
                }

                // Load Russian TSV once
                if (!_ruLoaded) LoadRuTsv();

                var loader = WorldManager.instance.GameDataLoader;

                // Index names and icons for all cards
                foreach (CardData cd in loader.CardDataPrefabs)
                {
                    if (cd == null || string.IsNullOrEmpty(cd.Id)) continue;
                    LocalizedNames[cd.Id] = GetLocalizedName(cd);
                    RuNames[cd.Id]        = GetRuName(cd);
                    if (cd.Icon != null) CardIcons[cd.Id] = cd.Icon;
                }

                // Index blueprints
                if (loader.BlueprintPrefabs != null)
                {
                    foreach (Blueprint bp in loader.BlueprintPrefabs)
                    {
                        if (bp == null || string.IsNullOrWhiteSpace(bp.CardId)) continue;
                        if (bp.HideFromIdeasTab) continue;
                        if (bp.Subprints == null) continue;

                        foreach (Subprint sp in bp.Subprints)
                        {
                            if (sp == null) continue;

                            if (!string.IsNullOrWhiteSpace(sp.ResultCard))
                                AddEntry(RecipesByResult, sp.ResultCard, bp);

                            if (sp.ExtraResultCards != null)
                                foreach (string c in sp.ExtraResultCards)
                                    if (!string.IsNullOrWhiteSpace(c))
                                        AddEntry(RecipesByResult, c, bp);

                            if (sp.RequiredCards != null)
                                foreach (string c in sp.RequiredCards)
                                    if (!string.IsNullOrWhiteSpace(c))
                                        AddEntry(RecipesByIngredient, c, bp);
                        }
                    }
                }

                IsReady = true;
                if (L != null) L.Log($"RecipeCache built: {RecipesByResult.Count} result keys, " +
                                      $"{RecipesByIngredient.Count} ingredient keys, " +
                                      $"{LocalizedNames.Count} names.");
            }
            catch (Exception ex)
            {
                if (!_buildErrorLogged)
                {
                    _buildErrorLogged = true;
                    if (L != null) L.Log("RecipeCache.Build error (once): " + ex.Message);
                }
            }
        }

        public static void InvalidateCache()
        {
            IsReady = false;
        }

        // ─── Name resolution ─────────────────────────────────────────────────────

        private static void LoadRuTsv()
        {
            _ruLoaded = true;
            try
            {
                string tsvPath = FindRuTsvPath();
                if (string.IsNullOrEmpty(tsvPath)) return;

                ParseTsv(tsvPath);
                if (L != null) L.Log($"Russian TSV loaded: {_ruTermMap.Count} terms from {tsvPath}");
            }
            catch (Exception ex)
            {
                if (L != null) L.Log("LoadRuTsv error: " + ex.Message);
            }
        }

        /// <summary>
        /// Find Russian localization TSV on any PC without hardcoded paths.
        /// Strategy: search loaded mods first, then all mod paths.
        /// </summary>
        private static string FindRuTsvPath()
        {
            var candidates = new List<(string path, int rows)>();

            // 1. Check loaded mods for ones that have a Russian column
            if (ModManager.LoadedMods != null)
            {
                foreach (Mod mod in ModManager.LoadedMods)
                {
                    if (mod?.Path == null) continue;
                    string candidate = System.IO.Path.Combine(mod.Path, "localization.tsv");
                    if (File.Exists(candidate) && HasRussianColumn(candidate))
                        candidates.Add((candidate, CountTsvRows(candidate)));
                }
            }

            // 2. Search all mod paths (includes workshop) without hardcoding Steam location
            try
            {
                foreach (string modDir in ModManager.GetModPaths())
                {
                    if (!Directory.Exists(modDir)) continue;
                    string candidate = System.IO.Path.Combine(modDir, "localization.tsv");
                    if (File.Exists(candidate) && HasRussianColumn(candidate) && !candidates.Any(x => x.path == candidate))
                        candidates.Add((candidate, CountTsvRows(candidate)));
                }
            }
            catch { }

            if (candidates.Count == 0) return null;

            // Return the TSV with the most rows (most complete translation)
            candidates.Sort((a, b) => b.rows.CompareTo(a.rows));
            if (L != null) L.Log($"RuTsv candidates: {string.Join(", ", candidates.Select(x => $"{x.rows}:{System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(x.path))}"))}");
            return candidates[0].path;
        }

        private static int CountTsvRows(string path)
        {
            try { return File.ReadLines(path).Count(l => !string.IsNullOrWhiteSpace(l)); }
            catch { return 0; }
        }

        private static bool HasRussianColumn(string tsvPath)
        {
            try
            {
                string first = File.ReadLines(tsvPath).FirstOrDefault() ?? "";
                return first.Contains("Russian");
            }
            catch { return false; }
        }

        private static void ParseTsv(string tsvPath)
        {
            string[] cols = (File.ReadLines(tsvPath).FirstOrDefault() ?? "").Split('\t');
            int ruCol = -1;
            for (int i = 0; i < cols.Length; i++)
                if (cols[i].Trim().Equals("Russian", StringComparison.OrdinalIgnoreCase)) { ruCol = i; break; }
            if (ruCol < 0) return;

            foreach (string line in File.ReadLines(tsvPath, System.Text.Encoding.UTF8).Skip(1))
            {
                string[] parts = line.Split('\t');
                if (parts.Length <= ruCol) continue;
                string term   = parts[0].Trim();
                string ruText = parts[ruCol].Trim();
                if (!string.IsNullOrEmpty(term) && !string.IsNullOrEmpty(ruText))
                    _ruTermMap[term] = ruText;
            }
        }

        private static string GetLocalizedName(CardData cd)
        {
            if (cd == null) return "[null]";
            try
            {
                if (SokLoc.instance != null && !string.IsNullOrEmpty(cd.NameTerm))
                {
                    string loc = SokLoc.Translate(cd.NameTerm);
                    if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("---")) return loc;
                }
                if (!string.IsNullOrEmpty(cd.nameOverride)) return cd.nameOverride;
            }
            catch { }
            return string.IsNullOrEmpty(cd.Id) ? "[unknown]" : "[" + cd.Id + "]";
        }

        private static string GetRuName(CardData cd)
        {
            if (cd == null || string.IsNullOrEmpty(cd.NameTerm)) return null;
            _ruTermMap.TryGetValue(cd.NameTerm, out string ru);
            return ru;
        }

        // ─── Public API ──────────────────────────────────────────────────────────

        // Special wildcard card IDs used in blueprints → human-readable name
        private static readonly Dictionary<string, string[]> _specialIds = new Dictionary<string, string[]>
        {
            // key → [Russian, English]
            ["any_villager"]        = new[] { "Любой житель",         "Any Villager" },
            ["any_villager_young"]  = new[] { "Молодой житель",       "Young Villager" },
            ["any_villager_old"]    = new[] { "Пожилой житель",       "Elder Villager" },
            ["breedable_villager"]  = new[] { "Житель (размножение)", "Villager (breedable)" },
            ["any_worker"]          = new[] { "Любой рабочий",        "Any Worker" },
            ["any_educated_worker"] = new[] { "Образованный рабочий", "Educated Worker" },
            ["stone"]               = new[] { "Камень (любой)",       "Stone (any)" },
            ["cotton"]              = new[] { "Хлопок",               "Cotton" },
            ["fish"]                = new[] { "Рыба (любая)",         "Fish (any)" },
        };

        public static string GetName(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return "—";

            // Check special wildcard IDs first
            if (_specialIds.TryGetValue(cardId, out string[] names))
            {
                bool ru = SokLoc.instance?.CurrentLanguage == "Russian";
                return ru ? names[0] : names[1];
            }

            // Prefer current language localized name
            bool usingRu = SokLoc.instance?.CurrentLanguage == "Russian";
            if (usingRu && RuNames.TryGetValue(cardId, out string ruName) && !string.IsNullOrEmpty(ruName))
                return ruName;

            if (LocalizedNames.TryGetValue(cardId, out string loc) && !string.IsNullOrEmpty(loc))
                return loc;

            // Fallback: show readable version of the ID
            return cardId.Replace("_", " ");
        }

        public static Sprite GetIcon(string cardId)
        {
            if (cardId == null) return null;
            CardIcons.TryGetValue(cardId, out Sprite s);
            return s;
        }

        public static bool IsFoundInSave(string cardId)
        {
            return WorldManager.instance?.CurrentSave?.FoundCardIds?.Contains(cardId) ?? false;
        }

        public static List<Blueprint> GetByResult(string cardId)
        {
            if (!IsReady || string.IsNullOrEmpty(cardId)) return new List<Blueprint>();
            RecipesByResult.TryGetValue(cardId, out var list);
            return list ?? new List<Blueprint>();
        }

        public static List<Blueprint> GetByIngredient(string cardId)
        {
            if (!IsReady || string.IsNullOrEmpty(cardId)) return new List<Blueprint>();
            RecipesByIngredient.TryGetValue(cardId, out var list);
            return list ?? new List<Blueprint>();
        }

        private static void AddEntry(Dictionary<string, List<Blueprint>> dict, string key, Blueprint bp)
        {
            if (!dict.ContainsKey(key)) dict[key] = new List<Blueprint>();
            if (!dict[key].Contains(bp)) dict[key].Add(bp);
        }
    }
}
