using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSideBarNS
{
    public static class PinIdeaMod
    {
        private static ModLogger L;
        private static ConfigFile C;

        private const string FileSeparator = ",";

        private static ConfigEntry<string> __fideas;
        private static List<string> Fideas;

        private static bool[] isFidea;
        private static Dictionary<BlueprintGroup, int> groupIdxMap;
        private static Dictionary<BlueprintGroup, int> groupFNumMap;
        private static Dictionary<BlueprintGroup, int> groupUIIdxMap;
        private static Dictionary<string, int> elementIdToIdx;

        private static Sprite FavorIcon;

        public static event Action ResetFavor;

        public static bool IsFidea(string cardId)
        {
            if (elementIdToIdx != null && elementIdToIdx.ContainsKey(cardId))
                return isFidea[elementIdToIdx[cardId]];
            return false;
        }

        public static void Initialize(ModLogger logger, ConfigFile config)
        {
            L = logger;
            C = config;

            __fideas = C.GetEntry<string>("favorite_ideas", "");
            __fideas.UI.Hidden = true;
            Fideas = __fideas.Value.Split(new[] { FileSeparator }, StringSplitOptions.RemoveEmptyEntries).ToList();

            groupIdxMap = new Dictionary<BlueprintGroup, int>();
            groupFNumMap = new Dictionary<BlueprintGroup, int>();
            groupUIIdxMap = new Dictionary<BlueprintGroup, int>();
            elementIdToIdx = new Dictionary<string, int>();

            Mod m = new Mod();
            ModManager.TryGetMod("better_sidebar", out m);
            FavorIcon = ResourceHelper.LoadSpriteFromPath(m.Path + "/Icons/icon-pin.png");
        }

        public static void InitIdeaElements(GameScreen __instance, List<IdeaElement> ___ideaElements,
            List<BlueprintGroup> ___groups, List<ExpandableLabel> ___ideaLabels)
        {
            try
            {
                // Reset state for re-init
                groupIdxMap.Clear();
                groupFNumMap.Clear();
                groupUIIdxMap.Clear();
                elementIdToIdx.Clear();
                ResetFavor = null;

                BlueprintDB.BlueprintGroups = ___groups;
                for (int i = 0; i < ___ideaLabels.Count; i++)
                {
                    BlueprintGroup group = BlueprintDB.BlueprintGroups[i];
                    if (!groupIdxMap.ContainsKey(group)) groupIdxMap[group] = 0;
                    if (!groupFNumMap.ContainsKey(group)) groupFNumMap[group] = 0;
                    groupUIIdxMap[group] = ___ideaLabels[i].transform.GetSiblingIndex() + 1;
                }

                BlueprintDB.IdeaElements = ___ideaElements;
                isFidea = new bool[___ideaElements.Count];
                int[] initFideasIdx = new int[Fideas.Count];

                for (int i = 0; i < ___ideaElements.Count; i++)
                {
                    IdeaElement ideaElement = ___ideaElements[i];
                    string cardId = ideaElement.MyKnowledge.CardId;
                    int idx_fi = Fideas.IndexOf(cardId);
                    int idx_ie = i;

                    if (!elementIdToIdx.ContainsKey(cardId))
                        elementIdToIdx[cardId] = idx_ie;

                    isFidea[idx_ie] = Fideas.Contains(cardId);
                    if (groupIdxMap.ContainsKey(ideaElement.MyKnowledge.Group))
                        groupIdxMap[ideaElement.MyKnowledge.Group] += 1;

                    // Favorite icon
                    GameObject favorLabel = UnityEngine.Object.Instantiate(ideaElement.NewLabel);
                    favorLabel.transform.SetParent(ideaElement.transform);
                    favorLabel.transform.SetSiblingIndex(favorLabel.transform.GetSiblingIndex() - 1);
                    favorLabel.GetComponent<Image>().sprite = FavorIcon;
                    favorLabel.SetActive(isFidea[idx_ie]);

                    ideaElement.gameObject.GetComponent<CustomButton>().Clicked += delegate
                    {
                        if (isFidea[idx_ie])
                        {
                            isFidea[idx_ie] = false;
                            Fideas.Remove(cardId);
                            if (groupFNumMap.ContainsKey(ideaElement.MyKnowledge.Group))
                                groupFNumMap[ideaElement.MyKnowledge.Group] -= 1;
                            HideUnhoveredCoroutine.StartCoroutine(ideaElement, delegate
                            {
                                UnpinIdea(idx_ie, ideaElement);
                                GameScreen.instance.UpdateIdeasLog();
                            });
                            favorLabel.SetActive(false);
                        }
                        else
                        {
                            HideUnhoveredCoroutine.InterruptCoroutine();
                            isFidea[idx_ie] = true;
                            Fideas.Add(cardId);
                            if (groupFNumMap.ContainsKey(ideaElement.MyKnowledge.Group))
                                groupFNumMap[ideaElement.MyKnowledge.Group] += 1;
                            PinIdea(idx_ie, ideaElement);
                            favorLabel.SetActive(true);
                        }
                        SaveConfig();
                        GameScreen.instance.UpdateIdeasLog();
                    };

                    ResetFavor += delegate
                    {
                        if (isFidea[idx_ie])
                        {
                            isFidea[idx_ie] = false;
                            Fideas.Remove(cardId);
                            if (groupFNumMap.ContainsKey(ideaElement.MyKnowledge.Group))
                                groupFNumMap[ideaElement.MyKnowledge.Group] -= 1;
                            UnpinIdea(idx_ie, ideaElement);
                            favorLabel.SetActive(false);
                        }
                    };

                    if (idx_fi != -1)
                        initFideasIdx[idx_fi] = idx_ie;
                }

                // Calculate first-element index per group
                int total = ___ideaElements.Count;
                for (int i = ___ideaLabels.Count - 1; i >= 0; i--)
                {
                    BlueprintGroup g = BlueprintDB.BlueprintGroups[i];
                    if (groupIdxMap.ContainsKey(g))
                    {
                        total -= groupIdxMap[g];
                        groupIdxMap[g] = total;
                    }
                }

                foreach (int idx_ie in initFideasIdx)
                {
                    if (idx_ie < ___ideaElements.Count)
                    {
                        BlueprintGroup g = ___ideaElements[idx_ie].MyKnowledge.Group;
                        if (groupFNumMap.ContainsKey(g)) groupFNumMap[g] += 1;
                        PinIdea(idx_ie, ___ideaElements[idx_ie]);
                    }
                }
            }
            catch (Exception ex)
            {
                if (L != null) L.Log("PinIdeaMod.InitIdeaElements error: " + ex.Message);
            }
        }

        static void PinIdea(int idx_ie, IdeaElement ie)
        {
            BlueprintGroup group = ie.MyKnowledge.Group;
            if (groupUIIdxMap.ContainsKey(group) && groupFNumMap.ContainsKey(group))
                ie.transform.SetSiblingIndex(groupUIIdxMap[group] + groupFNumMap[group] - 1);
        }

        static void UnpinIdea(int idx_ie, IdeaElement ie)
        {
            BlueprintGroup group = ie.MyKnowledge.Group;
            if (!groupUIIdxMap.ContainsKey(group) || !groupFNumMap.ContainsKey(group) || !groupIdxMap.ContainsKey(group))
                return;
            int offset = 0;
            int start = groupIdxMap.ContainsKey(group) ? groupIdxMap[group] : 0;
            for (int i = start; i < idx_ie && i < (isFidea?.Length ?? 0); i++)
                offset += (isFidea[i] ? 0 : 1);
            ie.transform.SetSiblingIndex(groupUIIdxMap[group] + groupFNumMap[group] + offset);
        }

        public static void ResetPin() => ResetFavor?.Invoke();

        public static void SaveConfig()
        {
            __fideas.Value = string.Join(FileSeparator, Fideas);
            C.Save();
        }
    }
}
