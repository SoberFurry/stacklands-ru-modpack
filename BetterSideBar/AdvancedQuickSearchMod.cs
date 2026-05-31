using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace BetterSideBarNS
{
    public enum AdvancedSearchMode { Result, Ingred }

    public static class AdvancedQuickSearchMod
    {
        private static ModLogger L;
        private static ConfigFile C;

        private static string targetId;
        private static List<IdeaElement> searchResults;
        private static int currentFocusIdx;
        private static bool mouseUnfocused;
        private static Dictionary<string, bool> isQuickSearchResult;

        private static AdvancedSearchMode mode;
        private static ConfigEntry<bool> defaultResultMode;
        private static ConfigEntry<bool> clearOnLeave;
        private static ConfigEntry<bool> enableQuickSearch;

        private static Sprite QuickSearchIcon;
        private static Dictionary<string, GameObject> quickLabelMap;

        static IdeaElement hidingUnhoveredIdea;

        public static bool IsQuickSearchResult(string cardId)
        {
            if (isQuickSearchResult != null && isQuickSearchResult.ContainsKey(cardId))
                return isQuickSearchResult[cardId];
            return false;
        }

        public static void Initialize(ModLogger logger, ConfigFile config)
        {
            L = logger;
            C = config;

            searchResults = new List<IdeaElement>();
            mouseUnfocused = true;

            defaultResultMode = C.GetEntry<bool>("default_search_result", true);
            defaultResultMode.UI.Name = "Default Search for Recipe";
            defaultResultMode.UI.Tooltip = "On: search for recipes making the card.\nOff: search for recipes using the card as ingredient.\nHold Alt to switch mode.";

            clearOnLeave = C.GetEntry<bool>("clear_on_leave", true);
            clearOnLeave.UI.Name = "Reset Focused Result";
            clearOnLeave.UI.Tooltip = "Reset the focused search result to the first hit when moving mouse away.";

            enableQuickSearch = C.GetEntry<bool>("disable_quick_search", true);
            enableQuickSearch.UI.Name = "Enable Quick Search";
            enableQuickSearch.UI.Tooltip = "Enables the middle-mouse-button quick search.";

            Mod m = new Mod();
            ModManager.TryGetMod("better_sidebar", out m);
            QuickSearchIcon = ResourceHelper.LoadSpriteFromPath(m.Path + "/Icons/icon-quick.png");

            isQuickSearchResult = new Dictionary<string, bool>();
            quickLabelMap = new Dictionary<string, GameObject>();
        }

        public static void InitIdeaElements()
        {
            try
            {
                isQuickSearchResult.Clear();
                quickLabelMap.Clear();

                foreach (IdeaElement element in BlueprintDB.IdeaElements)
                {
                    string cardId = element.MyKnowledge.CardId;
                    if (!isQuickSearchResult.ContainsKey(cardId))
                        isQuickSearchResult[cardId] = false;

                    if (!quickLabelMap.ContainsKey(cardId))
                    {
                        GameObject quickLabel = UnityEngine.Object.Instantiate(element.NewLabel);
                        quickLabel.transform.SetParent(element.transform);
                        quickLabel.GetComponent<Image>().sprite = QuickSearchIcon;
                        quickLabel.SetActive(false);
                        quickLabelMap[cardId] = quickLabel;
                    }
                }
            }
            catch (Exception ex)
            {
                if (L != null) L.Log("AdvancedQuickSearchMod.InitIdeaElements error: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(WorldManager), "Update")]
        public class QuickSearchTriggerHarmonyPatches
        {
            public static void Postfix()
            {
                try
                {
                    if (!enableQuickSearch.Value) return;

                    if (!mouseUnfocused && WorldManager.instance.HoveredCard == null)
                    {
                        if (clearOnLeave.Value)
                        {
                            targetId = "";
                            searchResults.Clear();
                            currentFocusIdx = 0;
                        }
                        mouseUnfocused = true;
                    }

                    if (Mouse.current.middleButton.wasPressedThisFrame &&
                        WorldManager.instance.HoveredCard != null)
                    {
                        bool altHeld = Keyboard.current.leftAltKey.isPressed;
                        AdvancedSearchMode newMode = ((altHeld != defaultResultMode.Value)
                            ? AdvancedSearchMode.Result
                            : AdvancedSearchMode.Ingred);

                        string newTargetId = WorldManager.instance.HoveredCard.CardData.Id;

                        if (newTargetId == targetId && newMode == mode && searchResults.Count > 0)
                        {
                            currentFocusIdx = (currentFocusIdx + 1) % searchResults.Count;
                            mouseUnfocused = false;
                        }
                        else
                        {
                            mode = newMode;
                            Dictionary<string, List<string>> dict = (mode == AdvancedSearchMode.Result)
                                ? BlueprintDB.ResultBPMap
                                : BlueprintDB.IngredBPMap;

                            if (dict != null && dict.ContainsKey(newTargetId))
                            {
                                targetId = newTargetId;
                                searchResults.Clear();
                                currentFocusIdx = 0;
                                mouseUnfocused = false;

                                foreach (IdeaElement element in BlueprintDB.IdeaElements)
                                {
                                    string cardId = element.MyKnowledge.CardId;
                                    if (KnowledgeWasFound(element.MyKnowledge) &&
                                        dict[targetId].Contains(cardId))
                                    {
                                        if (isQuickSearchResult.ContainsKey(cardId))
                                            isQuickSearchResult[cardId] = true;
                                        if (quickLabelMap.ContainsKey(cardId))
                                            quickLabelMap[cardId].SetActive(true);
                                        searchResults.Add(element);
                                    }
                                }
                                GameScreen.instance.UpdateIdeasLog();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (L != null) L.Log("QuickSearchTrigger error (once): " + ex.Message);
                }
            }
        }

        [HarmonyPatch(typeof(IdeaElement), "Update")]
        public class UpdateElementQuickSearchStatusHarmonyPatches
        {
            public static void Postfix(IdeaElement __instance)
            {
                string cardId = __instance.MyKnowledge?.CardId;
                if (cardId == null) return;
                if (IsQuickSearchResult(cardId) &&
                    (__instance.MyButton.IsHovered || __instance.MyButton.IsSelected))
                {
                    isQuickSearchResult[cardId] = false;
                    if (quickLabelMap.ContainsKey(cardId))
                        quickLabelMap[cardId].SetActive(false);
                    hidingUnhoveredIdea = __instance;
                }
            }
        }

        [HarmonyPatch(typeof(GameScreen), "Update")]
        public class HideUnhoveredCoroutineHarmonyPatches
        {
            public static void Postfix()
            {
                if (hidingUnhoveredIdea != null &&
                    !hidingUnhoveredIdea.MyButton.IsHovered &&
                    !hidingUnhoveredIdea.MyButton.IsSelected)
                {
                    hidingUnhoveredIdea = null;
                    GameScreen.instance.UpdateIdeasLog();
                }
            }
        }

        [HarmonyPatch(typeof(GameScreen), "LateUpdate")]
        public class RenderCurrentFocusInfoHarmonyPatches
        {
            public static void Prefix()
            {
                if (enableQuickSearch.Value && !mouseUnfocused && searchResults.Count > 0)
                {
                    GameScreen.instance.InfoTitle.text = "";
                    GameScreen.InfoBoxTitle = searchResults[currentFocusIdx].MyKnowledge.KnowledgeName;
                    GameScreen.InfoBoxText = searchResults[currentFocusIdx].MyKnowledge.KnowledgeText;
                }
            }
        }

        private static bool KnowledgeWasFound(IKnowledge knowledge)
        {
            return WorldManager.instance?.CurrentSave?.FoundCardIds?.Contains(knowledge.CardId) ?? false;
        }
    }
}
