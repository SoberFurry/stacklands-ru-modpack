using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BetterSideBarNS
{
    /// <summary>
    /// Builds a search index from the Russian localization TSV.
    /// Built once on world load. Never scanned per-keystroke.
    /// </summary>
    public static class RuSearchIndex
    {
        // term_id → normalized Russian text (ё→е, lowercase, no spaces)
        private static Dictionary<string, string> _termToRu = new Dictionary<string, string>();

        // cardId → normalized search blob (ru name + description + ingredient names)
        private static Dictionary<string, string> _cardBlob = new Dictionary<string, string>();

        public static bool IsReady { get; private set; }
        private static ModLogger L;
        private static string _tsvPath;

        public static void SetLogger(ModLogger logger) { L = logger; }

        public static void SetTsvPath(string path) { _tsvPath = path; }

        public static void Build()
        {
            try
            {
                IsReady = false;
                _termToRu.Clear();
                _cardBlob.Clear();

                _tsvPath = FindRuTsv(); // always re-find to pick most complete TSV

                if (!string.IsNullOrEmpty(_tsvPath) && File.Exists(_tsvPath))
                {
                    LoadTsv(_tsvPath);
                }

                // Build card search blobs using WorldManager data if available
                if (WorldManager.instance?.GameDataLoader?.CardDataPrefabs != null)
                {
                    BuildCardBlobs(WorldManager.instance.GameDataLoader.CardDataPrefabs,
                                   WorldManager.instance.GameDataLoader.BlueprintPrefabs);
                }

                IsReady = true;
                if (L != null) L.Log($"RuSearchIndex built: {_termToRu.Count} terms, {_cardBlob.Count} cards.");
            }
            catch (Exception ex)
            {
                if (L != null) L.Log("RuSearchIndex.Build error: " + ex.Message);
            }
        }

        private static string FindRuTsv()
        {
            var candidates = new List<(string path, int rows)>();

            // Strategy 1: loaded mods
            try
            {
                if (ModManager.LoadedMods != null)
                {
                    foreach (Mod mod in ModManager.LoadedMods)
                    {
                        if (mod?.Path == null) continue;
                        string c = Path.Combine(mod.Path, "localization.tsv");
                        if (File.Exists(c) && HasRuColumn(c))
                            candidates.Add((c, CountRows(c)));
                    }
                }
            }
            catch { }

            // Strategy 2: all mod paths (workshop + local)
            try
            {
                foreach (string dir in ModManager.GetModPaths())
                {
                    if (!Directory.Exists(dir)) continue;
                    string c = Path.Combine(dir, "localization.tsv");
                    if (File.Exists(c) && HasRuColumn(c) && !candidates.Any(x => x.path == c))
                        candidates.Add((c, CountRows(c)));
                }
            }
            catch { }

            if (candidates.Count == 0) return null;

            // Return the TSV with the most rows (most complete translation)
            candidates.Sort((a, b) => b.rows.CompareTo(a.rows));
            if (L != null) L.Log($"RuTsv candidates: {string.Join(", ", candidates.Select(x => $"{x.rows}:{Path.GetFileName(Path.GetDirectoryName(x.path))}"))}");
            return candidates[0].path;
        }

        private static int CountRows(string path)
        {
            try { return File.ReadLines(path).Count(l => !string.IsNullOrWhiteSpace(l)); }
            catch { return 0; }
        }

        private static bool HasRuColumn(string path)
        {
            try { return (File.ReadLines(path).FirstOrDefault() ?? "").Contains("Russian"); }
            catch { return false; }
        }

        private static void LoadTsv(string path)
        {
            // Format: Term \t Notes \t Russian
            // Find which column is Russian
            int ruCol = -1;
            bool first = true;

            foreach (string line in File.ReadLines(path, System.Text.Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split('\t');

                if (first)
                {
                    first = false;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i].Trim().Equals("Russian", StringComparison.OrdinalIgnoreCase))
                        {
                            ruCol = i;
                            break;
                        }
                    }
                    if (ruCol < 0) return; // no Russian column
                    continue;
                }

                if (parts.Length <= ruCol) continue;
                string term = parts[0].Trim();
                string ruText = parts[ruCol].Trim();
                if (!string.IsNullOrEmpty(term) && !string.IsNullOrEmpty(ruText))
                    _termToRu[term] = Normalize(ruText);
            }
        }

        private static void BuildCardBlobs(
            System.Collections.Generic.List<CardData> cards,
            System.Collections.Generic.List<Blueprint> blueprints)
        {
            foreach (CardData cd in cards)
            {
                if (string.IsNullOrEmpty(cd.Id)) continue;

                var parts = new List<string>();

                // Russian name
                string ruName = GetRuText(cd.NameTerm);
                if (!string.IsNullOrEmpty(ruName)) parts.Add(ruName);

                // Russian description
                string ruDesc = GetRuText(cd.DescriptionTerm);
                if (!string.IsNullOrEmpty(ruDesc)) parts.Add(ruDesc);

                // English name (normalized for fallback)
                // English name added via term lookup (no SokLoc dependency)
                parts.Add(Normalize(cd.NameTerm));

                // Internal ID always searchable
                parts.Add(Normalize(cd.Id));

                _cardBlob[cd.Id] = string.Join(" ", parts);
            }

            // Add ingredient names to blueprint blobs
            if (blueprints != null)
            {
                foreach (Blueprint bp in blueprints)
                {
                    if (string.IsNullOrEmpty(bp.CardId)) continue;
                    if (bp.Subprints == null) continue;

                    foreach (Subprint sp in bp.Subprints)
                    {
                        if (sp == null) continue;
                        string bpBlob = _cardBlob.ContainsKey(bp.CardId) ? _cardBlob[bp.CardId] : "";

                        // Add ingredient names
                        if (sp.RequiredCards != null)
                        {
                            foreach (string ingId in sp.RequiredCards)
                            {
                                if (string.IsNullOrEmpty(ingId)) continue;
                                if (_cardBlob.ContainsKey(ingId))
                                    bpBlob += " " + _cardBlob[ingId];
                            }
                        }

                        // Add result name
                        if (!string.IsNullOrEmpty(sp.ResultCard) && _cardBlob.ContainsKey(sp.ResultCard))
                            bpBlob += " " + _cardBlob[sp.ResultCard];

                        _cardBlob[bp.CardId] = bpBlob;
                    }
                }
            }
        }

        // ─── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns Russian text for a localization term, normalized.
        /// </summary>
        public static string GetRuText(string term)
        {
            if (string.IsNullOrEmpty(term)) return null;
            _termToRu.TryGetValue(term, out string val);
            return val;
        }

        /// <summary>
        /// Search across Russian names, descriptions, ingredients, internal ID.
        /// Handles ё=е, case-insensitive, multi-word AND logic.
        /// </summary>
        public static bool MatchesRussian(string cardId, string searchTerm)
        {
            if (string.IsNullOrEmpty(cardId) || string.IsNullOrEmpty(searchTerm)) return false;

            string blob;
            if (!_cardBlob.TryGetValue(cardId, out blob)) return false;
            if (string.IsNullOrEmpty(blob)) return false;

            string normTerm = Normalize(searchTerm);

            // Multi-word AND: all words must match
            string[] words = normTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string word in words)
                if (!blob.Contains(word)) return false;

            return true;
        }

        /// <summary>
        /// True if search term contains Cyrillic characters.
        /// </summary>
        public static bool IsCyrillicSearch(string term)
        {
            if (string.IsNullOrEmpty(term)) return false;
            foreach (char c in term)
                if (c >= 'Ѐ' && c <= 'ӿ') return true;
            return false;
        }

        public static string Normalize(string s)
        {
            if (s == null) return "";
            return s.ToLowerInvariant()
                    .Replace('ё', 'е')
                    .Replace('Ё', 'е')
                    .Replace(" ", "");
        }
    }
}
