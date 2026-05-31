using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSideBarNS
{
    public enum SidebarTab { All, Pinned, Quick, New }

    public static class SidebarDisplayControl
    {
        private static ModLogger L;
        private static ConfigFile C;

        public static SidebarTab ActiveTab = SidebarTab.All;

        private static CustomButton _btnAll, _btnPinned, _btnQuick, _btnNew, _btnResetPin;
        private static bool _buttonsBuilt;

        private static Sprite _sprAll, _sprPin, _sprQuick, _sprNew, _sprReset;

        private static MethodInfo _kmSearch;
        private static bool _reflInit;
        private static bool _errorLogged;

        private const string LBL_ALL   = "Все";
        private const string LBL_PIN   = "Закреп";
        private const string LBL_QUICK = "Быстро";
        private const string LBL_NEW   = "Новые";
        private const string LBL_RESET = "Сброс";

        public static void Initialize(ModLogger logger, ConfigFile config) { L = logger; C = config; }

        public static void LoadIcons(string modPath)
        {
            _sprAll   = Safe(modPath + "/Icons/icon-all.png");
            _sprPin   = Safe(modPath + "/Icons/icon-pin.png");
            _sprQuick = Safe(modPath + "/Icons/icon-quick.png");
            _sprNew   = Safe(modPath + "/Icons/icon-new.png");
            _sprReset = Safe(modPath + "/Icons/icon-reset.png");
        }

        private static Sprite Safe(string p) { try { return ResourceHelper.LoadSpriteFromPath(p); } catch { return null; } }

        private static void EnsureReflection()
        {
            if (_reflInit) return;
            _reflInit = true;
            _kmSearch = AccessTools.Method(typeof(GameScreen), "KnowledgeMatchesSearch", new[] { typeof(IKnowledge), typeof(string) });
        }

        private static bool KnowledgeMatchesSearch(GameScreen gs, IKnowledge knowledge, string term)
        {
            EnsureReflection();
            if (RuSearchIndex.IsCyrillicSearch(term)) return RuSearchIndex.MatchesRussian(knowledge.CardId, term);
            if (_kmSearch == null) return true;
            try { return (bool)_kmSearch.Invoke(gs, new object[] { knowledge, term }); }
            catch { return true; }
        }

        private static bool KnowledgeFound(IKnowledge k)
            => WorldManager.instance?.CurrentSave?.FoundCardIds?.Contains(k.CardId) ?? false;

        private static Dictionary<object, bool> GetExpandedState(ExpandableLabel[] labels)
        {
            var d = new Dictionary<object, bool>();
            foreach (var l in labels) d[l.Tag] = l.IsExpanded;
            return d;
        }

        [HarmonyPatch(typeof(GameScreen), "Awake")]
        public class AwakePatch
        {
            public static void Postfix()
            {
                if (_buttonsBuilt) return;
                try { BuildTabButtons(); _buttonsBuilt = true; }
                catch (Exception ex) { if (L != null) L.Log("AwakePatch: " + ex.Message); }
            }
        }

        private static void BuildTabButtons()
        {
            Transform parent = GameScreen.instance.IdeaSearchField.transform.parent.parent;
            var container = new GameObject("BSB_Tabs");
            container.transform.SetParent(parent, false);
            container.transform.SetSiblingIndex(1);

            var vl = container.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 4; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.padding = new RectOffset(0, 0, 2, 2);

            var row1 = MakeHRow(container.transform);
            _btnAll    = MakeTabBtn(row1.transform, LBL_ALL,   _sprAll,   () => SetTab(SidebarTab.All));
            _btnPinned = MakeTabBtn(row1.transform, LBL_PIN,   _sprPin,   () => SetTab(SidebarTab.Pinned));
            _btnQuick  = MakeTabBtn(row1.transform, LBL_QUICK, _sprQuick, () => SetTab(SidebarTab.Quick));
            _btnNew    = MakeTabBtn(row1.transform, LBL_NEW,   _sprNew,   () => SetTab(SidebarTab.New));

            var row2 = MakeHRow(container.transform);
            _btnResetPin = MakeTabBtn(row2.transform, LBL_RESET, _sprReset, () =>
            {
                PinIdeaMod.ResetPin();
                if (ActiveTab == SidebarTab.Pinned) ActiveTab = SidebarTab.All;
                GameScreen.instance.UpdateIdeasLog();
                PinIdeaMod.SaveConfig();
            });
            var sp = new GameObject("Sp").AddComponent<LayoutElement>();
            sp.transform.SetParent(row2.transform, false);
            sp.flexibleWidth = 1;
        }

        private static void SetTab(SidebarTab tab) { ActiveTab = tab; GameScreen.instance.UpdateIdeasLog(); }

        private static HorizontalLayoutGroup MakeHRow(Transform parent)
        {
            var go = new GameObject("HRow"); go.transform.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4; h.childForceExpandHeight = false; h.childForceExpandWidth = false;
            h.childControlHeight = true; h.childControlWidth = true;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return h;
        }

        private static CustomButton MakeTabBtn(Transform parent, string label, Sprite icon, Action onClick)
        {
            var btnObj = UnityEngine.Object.Instantiate(GameScreen.instance.IdeasButton.gameObject);
            btnObj.name = "BSB_" + label;
            btnObj.transform.SetParent(parent, false);

            // Destroy any localization component that overrides text each frame
            foreach (var comp in btnObj.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                string tn = comp.GetType().Name;
                if (tn == "SokTermText" || tn.Contains("LocalizeTerm") || tn.Contains("SetTerm"))
                    UnityEngine.Object.Destroy(comp);
            }

            var le = btnObj.GetComponent<LayoutElement>() ?? btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 34; le.flexibleWidth = 1; le.minWidth = 0;

            if (icon != null)
            {
                var ig = new GameObject("BtnIcon");
                ig.transform.SetParent(btnObj.transform, false);
                ig.transform.SetSiblingIndex(0);
                var img = ig.AddComponent<Image>(); img.sprite = icon; img.preserveAspect = true;
                var ile = ig.AddComponent<LayoutElement>(); ile.preferredWidth = 28; ile.preferredHeight = 28;
            }

            var btn = btnObj.GetComponent<CustomButton>();
            if (btn?.TextMeshPro != null)
            {
                btn.TextMeshPro.text = label;
                btn.TextMeshPro.fontSize = 13;
                btn.TextMeshPro.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            }
            btn.Clicked += delegate { onClick?.Invoke(); };
            return btn;
        }

        [HarmonyPatch(typeof(GameScreen), "Update")]
        public class UpdatePatch
        {
            public static void Prefix()
            {
                if (!_buttonsBuilt || _btnAll == null) return;
                try
                {
                    // Force text EVERY frame to override any residual localization
                    SetBtn(_btnAll,     ActiveTab == SidebarTab.All,    LBL_ALL);
                    SetBtn(_btnPinned,  ActiveTab == SidebarTab.Pinned, LBL_PIN);
                    SetBtn(_btnQuick,   ActiveTab == SidebarTab.Quick,  LBL_QUICK);
                    SetBtn(_btnNew,     ActiveTab == SidebarTab.New,    LBL_NEW);
                    SetBtn(_btnResetPin, false,                         LBL_RESET);
                }
                catch { }
            }

            private static void SetBtn(CustomButton btn, bool active, string label)
            {
                if (btn == null) return;
                btn.Image.color = active ? ColorManager.instance.HoverButtonColor : ColorManager.instance.ButtonColor;
                if (btn.TextMeshPro != null)
                { btn.TextMeshPro.text = label; btn.TextMeshPro.color = ColorManager.instance.ButtonTextColor; }
            }
        }

        [HarmonyPatch(typeof(GameScreen), "UpdateIdeasLog")]
        public class UpdateIdeasLogPatch
        {
            public static void Postfix(GameScreen __instance, List<ExpandableLabel> ___ideaLabels, List<IdeaElement> ___ideaElements)
            {
                string searchTerm  = __instance.IdeaSearchField?.text ?? "";
                bool hasSearch     = !string.IsNullOrEmpty(searchTerm);
                bool isRuSearch    = hasSearch && RuSearchIndex.IsCyrillicSearch(searchTerm);
                bool filterActive  = ActiveTab != SidebarTab.All;
                if (!filterActive && !isRuSearch) return;

                try
                {
                    var expanded = GetExpandedState(__instance.IdeaElementsParent.GetComponentsInChildren<ExpandableLabel>());

                    foreach (IdeaElement el in ___ideaElements)
                    {
                        if (!KnowledgeFound(el.MyKnowledge)) { el.gameObject.SetActive(false); continue; }
                        bool pf = PassesTab(el);
                        bool ps = !hasSearch || KnowledgeMatchesSearch(__instance, el.MyKnowledge, searchTerm);
                        bool hov = el.MyButton.IsHovered || el.MyButton.IsSelected;

                        if (hov) el.gameObject.SetActive(true);
                        else if (pf && ps)
                            el.gameObject.SetActive(hasSearch || (expanded.ContainsKey(el.MyKnowledge.Group) && expanded[el.MyKnowledge.Group]));
                        else
                            el.gameObject.SetActive(false);
                    }

                    foreach (ExpandableLabel lbl in ___ideaLabels)
                    {
                        bool any = lbl.Children.Any(go => {
                            var el = go.GetComponent<IdeaElement>();
                            return el != null && KnowledgeFound(el.MyKnowledge) && PassesTab(el)
                                   && (!hasSearch || KnowledgeMatchesSearch(__instance, el.MyKnowledge, searchTerm));
                        });
                        lbl.gameObject.SetActive(any);
                        if (any && hasSearch) lbl.IsExpanded = true;
                        else if (any) lbl.IsExpanded = expanded.ContainsKey(lbl.Tag) && expanded[lbl.Tag];
                    }
                    _errorLogged = false;
                }
                catch (Exception ex)
                {
                    if (!_errorLogged) { _errorLogged = true; if (L != null) L.Log("Filter error (once): " + ex.Message); }
                }
            }

            private static bool PassesTab(IdeaElement el)
            {
                switch (ActiveTab)
                {
                    case SidebarTab.Pinned: return PinIdeaMod.IsFidea(el.MyKnowledge.CardId);
                    case SidebarTab.Quick:  return AdvancedQuickSearchMod.IsQuickSearchResult(el.MyKnowledge.CardId);
                    case SidebarTab.New:    return el.IsNew;
                    default: return true;
                }
            }
        }

        [HarmonyPatch(typeof(GameScreen), "InitIdeaElements")]
        public class InitPatch
        {
            public static void Postfix()
            {
                try { RuSearchIndex.Build(); }
                catch (Exception ex) { if (L != null) L.Log("RuSearch rebuild: " + ex.Message); }
            }
        }
    }
}
