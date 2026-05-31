using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterSideBarNS
{
    public static class BlueprintDB
    {
        private static ModLogger L;
        private static ConfigFile C;

        public static Dictionary<string, List<string>> ResultBPMap;
        public static Dictionary<string, List<string>> IngredBPMap;

        public static List<BlueprintGroup> BlueprintGroups;
        public static List<IdeaElement> IdeaElements;

        public static bool BPReady = false;

        public static Action OnLoadSideBarData;

        public static void Initialize(ModLogger logger, ConfigFile config)
        {
            L = logger;
            C = config;

            ResultBPMap = new Dictionary<string, List<string>>();
            IngredBPMap = new Dictionary<string, List<string>>();

            BuildBlueprintMaps();
        }

        public static void BuildBlueprintMaps()
        {
            try
            {
                ResultBPMap.Clear();
                IngredBPMap.Clear();
                BPReady = false;

                // Use already-loaded data if available, otherwise create a loader
                IEnumerable<Blueprint> blueprints = null;
                if (WorldManager.instance?.GameDataLoader?.BlueprintPrefabs != null)
                {
                    blueprints = WorldManager.instance.GameDataLoader.BlueprintPrefabs;
                }
                else
                {
                    GameDataLoader data = new GameDataLoader(true, true);
                    blueprints = data.BlueprintPrefabs;
                }

                foreach (Blueprint blueprint in blueprints)
                {
                    if (blueprint == null || string.IsNullOrWhiteSpace(blueprint.CardId)) continue;
                    if (blueprint.Subprints == null) continue;
                    foreach (Subprint subprint in blueprint.Subprints)
                    {
                        if (subprint == null) continue;
                        if (!string.IsNullOrWhiteSpace(subprint.ResultCard))
                            AddUniqueEntry(ref ResultBPMap, subprint.ResultCard, blueprint.CardId);

                        if (subprint.ExtraResultCards != null)
                            foreach (string card in subprint.ExtraResultCards)
                                if (!string.IsNullOrWhiteSpace(card))
                                    AddUniqueEntry(ref ResultBPMap, card, blueprint.CardId);

                        if (subprint.RequiredCards != null)
                            foreach (string card in subprint.RequiredCards)
                                if (!string.IsNullOrWhiteSpace(card))
                                    AddUniqueEntry(ref IngredBPMap, card, blueprint.CardId);
                    }
                }

                BPReady = true;
                L.Log("BlueprintDB built: " + ResultBPMap.Count + " result entries, " + IngredBPMap.Count + " ingredient entries.");
            }
            catch (Exception ex)
            {
                L.Log("BlueprintDB.BuildBlueprintMaps failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(GameScreen), "InitIdeaElements")]
        public class LoadSideBarDataHarmonyPatches
        {
            public static void Postfix(GameScreen __instance, List<IdeaElement> ___ideaElements,
                List<BlueprintGroup> ___groups, List<ExpandableLabel> ___ideaLabels)
            {
                try
                {
                    BlueprintGroups = ___groups;
                    IdeaElements = ___ideaElements;

                    // Rebuild maps using now-available WorldManager data
                    BuildBlueprintMaps();

                    OnLoadSideBarData?.Invoke();
                    PinIdeaMod.InitIdeaElements(__instance, ___ideaElements, ___groups, ___ideaLabels);
                    AdvancedQuickSearchMod.InitIdeaElements();
                }
                catch (Exception ex)
                {
                    if (L != null) L.Log("LoadSideBarDataHarmonyPatches.Postfix error: " + ex.Message);
                }
            }
        }

        static bool AddUniqueEntry(ref Dictionary<string, List<string>> dict, string key, string value)
        {
            if (!dict.ContainsKey(key))
                dict[key] = new List<string>();
            if (dict[key].Contains(value))
                return false;
            dict[key].Add(value);
            return true;
        }
    }
}
