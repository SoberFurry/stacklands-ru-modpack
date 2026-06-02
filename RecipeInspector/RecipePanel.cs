using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RecipeInspectorNS
{
    public class RecipePanel : MonoBehaviour
    {
        public static RecipePanel Instance { get; private set; }
        private static string    _modPath;
        private static ModLogger _log;
        private static bool      _hasBSB;
        private static MethodInfo _bsbPin;

        // ── State ──────────────────────────────────────────────────────────────────
        private bool   _uiBuilt;
        private bool   _collapsed = true;
        private bool   _settingsOpen;
        private bool   _bindingKey;
        private string _bindTarget;
        private float  _savedSpeed;

        // Settings snapshot for cancel/revert
        private struct Snap
        {
            public bool ShowIcons,OnlyFound,ShowResultRow,DarkTheme,AutoHide,KeepOnCraft,ShowTime,DedupVariants;
            public int  FontSizeIdx,OpacityIdx,MaxVariants,AutoHideDelay;
            public KeyCode KeyOpen,KeyPin,KeyHide;
        }
        private Snap _snap;

        // ── Slide animation ────────────────────────────────────────────────────────
        private float _slideVel;
        private float _slideTarget;
        private float _panelW;
        private const float SlideTime = 0.18f;

        // ── Slots ─────────────────────────────────────────────────────────────────
        private class Slot { public string BpId,ResultId,Label,FullName; public int Vi; public bool Pinned; }
        private readonly List<Slot> _slots = new List<Slot>();
        private int _active;

        // ── UI roots ──────────────────────────────────────────────────────────────
        private Canvas        _canvas;
        private RectTransform _panelRt;
        private GameObject    _bodyGo;
        private Image         _panelBg, _titleBg;
        private Transform     _tabBar, _content;
        // _collapseArrow unused — collapse handled via IcoBtn arrow-right

        // Tab strip shown when collapsed
        private RectTransform _stripRt;
        private Image         _stripBg;

        // Overlays
        private GameObject      _settingsOv, _blocker;
        private GameObject      _toastGo;
        private TextMeshProUGUI _toastTmp;
        private Coroutine       _toastCo;
        private GameObject      _tipGo;
        private TextMeshProUGUI _tipTmp;
        private RectTransform   _tipRt;

        // ── Static API ────────────────────────────────────────────────────────────

        public static void EnsureCreated(string modPath, ModLogger log)
        {
            if (Instance != null) return;
            _modPath = modPath; _log = log;
            var go = new GameObject("RI_Root");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<RecipePanel>();

            _hasBSB = false; _bsbPin = null;
            if (ModManager.LoadedMods != null)
                foreach (Mod m in ModManager.LoadedMods)
                {
                    if (m?.Manifest?.Id != "better_sidebar") continue;
                    _hasBSB = true;
                    try { foreach (var t in m.GetType().Assembly.GetTypes()) { var mi = t.GetMethod("PinKnowledge", BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance); if (mi!=null){_bsbPin=mi;break;} } } catch {}
                    break;
                }
        }

        // Called from Mod.cs after cache is built — creates UI
        public static void Init()
        {
            if (Instance == null) return;
            Instance.EnsureUI();
        }

        public static void ToggleBlueprint(string bpId, string resultId)
        {
            if (Instance == null) return;
            Instance.EnsureUI();
            for (int i = 0; i < Instance._slots.Count; i++)
                if (Instance._slots[i].BpId == bpId) { Instance.CloseSlot(i); return; }
            Instance.AddSlot(bpId, resultId);
            Instance.SetCollapsed(false);
            Instance.Refresh();
        }

        public static void OnBlueprintCompleted(string bpId)
        {
            if (Instance == null) return;
            for (int i = Instance._slots.Count-1; i >= 0; i--)
                if (Instance._slots[i].BpId == bpId && !Instance._slots[i].Pinned)
                { Instance.CloseSlot(i); return; }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake() { Instance = this; }

        private void EnsureUI()
        {
            if (_uiBuilt) return;
            _uiBuilt = true;
            _canvas = GameCanvas.instance?.Canvas ?? FindObjectOfType<Canvas>();
            if (_canvas == null) { _uiBuilt = false; return; }
            Build();
        }

        private void Update()
        {
            if (!_uiBuilt) return;

            // Slide animation
            if (_panelRt != null)
            {
                float cur = _panelRt.anchoredPosition.x;
                if (Mathf.Abs(cur - _slideTarget) > 0.5f)
                {
                    float next = Mathf.SmoothDamp(cur, _slideTarget, ref _slideVel, SlideTime);
                    _panelRt.anchoredPosition = new Vector2(next, _panelRt.anchoredPosition.y);
                }
                // Update strip position to follow panel's right edge
                if (_stripRt != null)
                {
                    // Strip stays at right edge of screen always
                    _stripRt.anchoredPosition = new Vector2(0f, _panelRt.anchoredPosition.y);
                }
            }

            // Key bind capture
            if (_bindingKey && _settingsOpen)
            {
                foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
                {
                    if (kc == KeyCode.None) continue;
                    if (kc == KeyCode.Escape) { _bindingKey=false; _bindTarget=null; ReopenSettings(); return; }
                    if (!RecipeInspectorMod.IsKeyDown(kc)) continue;
                    ApplyBind(_bindTarget, kc);
                    _bindingKey=false; _bindTarget=null;
                    ReopenSettings(); return;
                }
                return;
            }

            // ESC closes settings (reverts changes)
            if (_settingsOpen)
            {
                try { if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) CloseSettings(false); } catch {}
                return;
            }

            // Hide/show hotkey
            try { if (RecipeInspectorMod.IsKeyDown(RecipeSettings.KeyHide)) SetCollapsed(!_collapsed); } catch {}

            // R on hovered tab = close that tab
            if (_slots.Count > 0 && _tabBar != null)
                try
                {
                    if (RecipeInspectorMod.IsKeyDown(RecipeSettings.KeyOpen))
                    {
                        var mp = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                        for (int i = 0; i < _tabBar.childCount; i++)
                        {
                            var rt = _tabBar.GetChild(i)?.GetComponent<RectTransform>();
                            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mp, null))
                            { CloseSlot(i); return; }
                        }
                    }
                } catch {}

            // Pin hotkey
            if (_hasBSB && _slots.Count > 0)
                try { if (RecipeInspectorMod.IsKeyDown(RecipeSettings.KeyPin)) PinActive(); } catch {}
        }

        // ── Build ─────────────────────────────────────────────────────────────────

        private void Build()
        {
            float cw = _canvas.GetComponent<RectTransform>().rect.width;
            _panelW = Mathf.Clamp(cw * 0.21f, 260f, 400f);

            // ── Tab strip (always at right edge, visible when collapsed) ──────────
            var stripGo = new GameObject("RI_Strip");
            stripGo.transform.SetParent(_canvas.transform, false);
            _stripRt = stripGo.AddComponent<RectTransform>();
            _stripRt.anchorMin = new Vector2(1f, 0.5f);
            _stripRt.anchorMax = new Vector2(1f, 0.5f);
            _stripRt.pivot     = new Vector2(1f, 0.5f);
            _stripRt.sizeDelta = new Vector2(20f, 52f);
            _stripRt.anchoredPosition = Vector2.zero;

            _stripBg = stripGo.AddComponent<Image>();
            _stripBg.color = RecipeSettings.TitleBgColor;
            var sOl = stripGo.AddComponent<Outline>();
            sOl.effectColor = RecipeSettings.OutlineColor; sOl.effectDistance = new Vector2(2,-2);

            var sBtn = stripGo.AddComponent<Button>();
            sBtn.targetGraphic = _stripBg;
            sBtn.onClick.AddListener(() => SetCollapsed(false));

            // Arrow label on strip
            var sLblGo = new GameObject("L"); sLblGo.transform.SetParent(stripGo.transform, false);
            var sLblRt = sLblGo.AddComponent<RectTransform>();
            sLblRt.anchorMin = Vector2.zero; sLblRt.anchorMax = Vector2.one; sLblRt.sizeDelta = Vector2.zero;
            var sLblTmp = sLblGo.AddComponent<TextMeshProUGUI>();
            sLblTmp.text = "<";
            sLblTmp.fontSize = 14f;
            sLblTmp.color = RecipeSettings.TextColor;
            sLblTmp.alignment = TextAlignmentOptions.Center;
            sLblTmp.raycastTarget = false;

            // ── Main panel ───────────────────────────────────────────────────────
            var panelGo = new GameObject("RI_Panel");
            panelGo.transform.SetParent(_canvas.transform, false);
            _panelRt = panelGo.AddComponent<RectTransform>();
            _panelRt.anchorMin = new Vector2(1f, 0.5f);
            _panelRt.anchorMax = new Vector2(1f, 0.5f);
            _panelRt.pivot     = new Vector2(1f, 0.5f);
            _panelRt.sizeDelta = new Vector2(_panelW, 0f);

            // Start off-screen
            float hideX = _panelW + 4f;
            _panelRt.anchoredPosition = new Vector2(hideX, 0f);
            _slideTarget = hideX;

            _panelBg = panelGo.AddComponent<Image>();
            _panelBg.color = RecipeSettings.BgColor;
            var pOl = panelGo.AddComponent<Outline>();
            pOl.effectColor = RecipeSettings.OutlineColor; pOl.effectDistance = new Vector2(2,-2);

            var pVL = panelGo.AddComponent<VerticalLayoutGroup>();
            pVL.padding = new RectOffset(0,0,0,6); pVL.spacing = 0;
            pVL.childForceExpandWidth=true; pVL.childForceExpandHeight=false;
            pVL.childControlWidth=true; pVL.childControlHeight=true;
            panelGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Title bar
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            _titleBg = titleGo.AddComponent<Image>();
            _titleBg.color = RecipeSettings.TitleBgColor;
            var tHL = titleGo.AddComponent<HorizontalLayoutGroup>();
            tHL.padding = new RectOffset(8,4,5,5); tHL.spacing = 3;
            tHL.childForceExpandHeight=false; tHL.childForceExpandWidth=false;
            tHL.childControlHeight=true; tHL.childControlWidth=true;
            titleGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var drag = titleGo.AddComponent<PanelDragHandler>();
            drag.Panel = _panelRt;
            drag.OnMoved = () =>
            {
                RecipeSettings.SavedPos = new Vector2(_panelRt.anchoredPosition.x, _panelRt.anchoredPosition.y);
                if (!_collapsed) _slideTarget = _panelRt.anchoredPosition.x;
            };

            var titleLbl = Lbl(titleGo.transform, "Рецепты", 14f, FontStyles.Bold);
            titleLbl.color = RecipeSettings.TextColor;
            titleLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Settings button
            IcoBtn(titleGo.transform, "cog", 26, 26, OpenSettings, "Настройки");

            // Collapse button
            IcoBtn(titleGo.transform, "arrow-right", 26, 26, () => SetCollapsed(true), "Свернуть панель");

            // Body
            _bodyGo = new GameObject("Body");
            _bodyGo.transform.SetParent(panelGo.transform, false);
            var bVL = _bodyGo.AddComponent<VerticalLayoutGroup>();
            bVL.padding = new RectOffset(6,6,4,0); bVL.spacing = 4;
            bVL.childForceExpandWidth=true; bVL.childForceExpandHeight=false;
            bVL.childControlWidth=true; bVL.childControlHeight=true;
            _bodyGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildTabRow(_bodyGo.transform);
            Div(_bodyGo.transform);
            BuildContentScroll(_bodyGo.transform);

            // Tooltip
            _tipGo = new GameObject("RI_Tip");
            _tipGo.transform.SetParent(_canvas.transform, false);
            _tipRt = _tipGo.AddComponent<RectTransform>();
            _tipRt.sizeDelta = new Vector2(190, 0); _tipRt.pivot = new Vector2(0f, 1f);
            _tipGo.AddComponent<Image>().color = new Color(0.10f,0.08f,0.06f,0.93f);
            var tvl = _tipGo.AddComponent<VerticalLayoutGroup>(); tvl.padding = new RectOffset(7,7,4,4);
            _tipGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _tipTmp = Lbl(_tipGo.transform, "", 11.5f);
            _tipTmp.color = new Color(0.95f, 0.92f, 0.85f); _tipTmp.enableWordWrapping = true;
            _tipGo.SetActive(false);

            // Toast
            _toastGo = new GameObject("RI_Toast");
            _toastGo.transform.SetParent(_canvas.transform, false);
            var tRt = _toastGo.AddComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.5f,0f); tRt.anchorMax = new Vector2(0.5f,0f);
            tRt.pivot = new Vector2(0.5f,0f); tRt.anchoredPosition = new Vector2(0f,50f);
            _toastGo.AddComponent<Image>().color = new Color(0.10f,0.08f,0.06f,0.92f);
            var toOl = _toastGo.AddComponent<Outline>(); toOl.effectColor = RecipeSettings.OutlineColor; toOl.effectDistance = new Vector2(2,-2);
            var toVL = _toastGo.AddComponent<VerticalLayoutGroup>(); toVL.padding = new RectOffset(14,14,7,7);
            var toCsf = _toastGo.AddComponent<ContentSizeFitter>(); toCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; toCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _toastTmp = Lbl(_toastGo.transform, "", 13f, FontStyles.Bold);
            _toastTmp.color = new Color(0.95f, 0.92f, 0.85f); _toastTmp.alignment = TextAlignmentOptions.Center;
            _toastGo.SetActive(false);

            panelGo.transform.SetAsLastSibling();

            // Show panel immediately in expanded state (even without recipes)
            _stripRt.gameObject.SetActive(false);
            _collapsed = false;
            _slideTarget = RecipeSettings.SavedPos.x;
            _panelRt.anchoredPosition = new Vector2(_slideTarget, 0f);
        }

        private void BuildTabRow(Transform parent)
        {
            var sg = new GameObject("TS"); sg.transform.SetParent(parent, false);
            var sr = sg.AddComponent<ScrollRect>(); sr.horizontal=true; sr.vertical=false; sr.scrollSensitivity=20f; sr.movementType=ScrollRect.MovementType.Clamped;
            var le = sg.AddComponent<LayoutElement>(); le.preferredHeight=28f; le.flexibleWidth=1;
            sg.AddComponent<Image>().color = Color.clear;

            var vp = new GameObject("V"); vp.transform.SetParent(sg.transform, false);
            var vpRt = vp.AddComponent<RectTransform>(); vpRt.anchorMin=Vector2.zero; vpRt.anchorMax=Vector2.one; vpRt.sizeDelta=Vector2.zero;
            vp.AddComponent<Mask>().showMaskGraphic = false; vp.AddComponent<Image>().color = Color.clear;
            sr.viewport = vpRt;

            var cnt = new GameObject("C"); cnt.transform.SetParent(vp.transform, false);
            var cRt = cnt.AddComponent<RectTransform>(); cRt.anchorMin=new Vector2(0,0); cRt.anchorMax=new Vector2(0,1); cRt.pivot=new Vector2(0,0.5f); cRt.sizeDelta=Vector2.zero;
            var cHL = cnt.AddComponent<HorizontalLayoutGroup>(); cHL.spacing=3; cHL.childForceExpandWidth=false; cHL.childForceExpandHeight=true; cHL.childControlWidth=true; cHL.childControlHeight=true;
            cnt.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = cRt;
            _tabBar = cnt.transform;
        }

        private void BuildContentScroll(Transform parent)
        {
            var sg = new GameObject("CS"); sg.transform.SetParent(parent, false);
            var sr = sg.AddComponent<ScrollRect>(); sr.horizontal=false; sr.vertical=true; sr.scrollSensitivity=30f; sr.movementType=ScrollRect.MovementType.Clamped;
            var le = sg.AddComponent<LayoutElement>(); le.preferredHeight=280f; le.flexibleHeight=0;
            sg.AddComponent<Image>().color = Color.clear;

            var vp = new GameObject("V"); vp.transform.SetParent(sg.transform, false);
            var vpRt = vp.AddComponent<RectTransform>(); vpRt.anchorMin=Vector2.zero; vpRt.anchorMax=Vector2.one; vpRt.sizeDelta=Vector2.zero;
            vp.AddComponent<Mask>().showMaskGraphic = false; vp.AddComponent<Image>().color = Color.clear;
            sr.viewport = vpRt;

            var cnt = new GameObject("C"); cnt.transform.SetParent(vp.transform, false);
            var cRt = cnt.AddComponent<RectTransform>(); cRt.anchorMin=new Vector2(0,1); cRt.anchorMax=new Vector2(1,1); cRt.pivot=new Vector2(0.5f,1f); cRt.sizeDelta=Vector2.zero;
            var cVL = cnt.AddComponent<VerticalLayoutGroup>(); cVL.spacing=5; cVL.padding=new RectOffset(2,2,2,4); cVL.childForceExpandWidth=true; cVL.childForceExpandHeight=false; cVL.childControlWidth=true; cVL.childControlHeight=true;
            cnt.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = cRt;
            _content = cnt.transform;
        }

        // ── Collapse / Expand ─────────────────────────────────────────────────────

        private void SetCollapsed(bool col)
        {
            _collapsed = col;
            float savedX = RecipeSettings.SavedPos.x;
            float hideX  = _panelW + 4f;

            _slideTarget = col ? hideX : savedX;

            // Body active only when expanded
            if (_bodyGo != null) _bodyGo.SetActive(!col);

            // Strip: show when collapsed AND there are slots
            if (_stripRt != null) _stripRt.gameObject.SetActive(col && _slots.Count > 0);

        }

        // ── Slots ─────────────────────────────────────────────────────────────────

        private void AddSlot(string bpId, string resultId)
        {
            if (_slots.Count >= 6) _slots.RemoveAt(0);
            Blueprint bp = GetBp(bpId);
            if (string.IsNullOrEmpty(resultId) && bp?.Subprints?.Count > 0)
                resultId = bp.Subprints.FirstOrDefault(s => s!=null && !string.IsNullOrEmpty(s.ResultCard))?.ResultCard;
            if (string.IsNullOrEmpty(resultId)) resultId = bpId;
            string full = ResolveName(bp, bpId, resultId);
            string lbl  = full.Length > 9 ? full.Substring(0,8)+"..." : full;
            _slots.Add(new Slot { BpId=bpId, ResultId=resultId, Label=lbl, FullName=full });
            _active = _slots.Count - 1;
        }

        private void CloseSlot(int idx)
        {
            if (idx < 0 || idx >= _slots.Count) return;
            _slots.RemoveAt(idx);
            _active = Mathf.Clamp(_active, 0, Mathf.Max(0, _slots.Count-1));
            if (_slots.Count == 0) SetCollapsed(true);
            else Refresh();
        }

        // ── Refresh ───────────────────────────────────────────────────────────────

        private void Refresh()
        {
            if (_panelBg != null) _panelBg.color = RecipeSettings.BgColor;
            if (_titleBg != null) _titleBg.color = RecipeSettings.TitleBgColor;
            if (_stripBg != null) _stripBg.color = RecipeSettings.TitleBgColor;
            RebuildTabs();
            RebuildContent();
            ForceLayout();
        }

        private void RebuildTabs()
        {
            foreach (Transform c in _tabBar) Destroy(c.gameObject);

            if (_slots.Count == 0)
            {
                var h = Lbl(_tabBar, "R - открыть рецепт", RecipeSettings.F_Small);
                h.color = RecipeSettings.SubTextColor;
                return;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                int idx = i; bool act = (i == _active);
                var s = _slots[i];

                var tGo = new GameObject("Tab"+i);
                tGo.transform.SetParent(_tabBar, false);
                var tImg = tGo.AddComponent<Image>();
                tImg.color = act ? RecipeSettings.BtnActiveColor : RecipeSettings.BtnColor;
                var tHL = tGo.AddComponent<HorizontalLayoutGroup>();
                tHL.padding = new RectOffset(5,3,2,2); tHL.spacing = 2;
                tHL.childForceExpandWidth=false; tHL.childForceExpandHeight=false;
                tHL.childControlWidth=true; tHL.childControlHeight=true;
                tGo.AddComponent<LayoutElement>().preferredWidth = 88;

                var lbl = Lbl(tGo.transform, s.Label, RecipeSettings.F_Small);
                lbl.color = RecipeSettings.BtnTextColor;
                lbl.overflowMode = TextOverflowModes.Ellipsis;
                lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

                var et = tGo.AddComponent<EventTrigger>();
                On(et, EventTriggerType.PointerClick, _ => { _active = idx; Refresh(); });
                On(et, EventTriggerType.PointerEnter, e => Tip(s.FullName + " (R=закрыть)", ((PointerEventData)e).position));
                On(et, EventTriggerType.PointerExit,  _ => HideTip());

                // x close button
                var xGo = new GameObject("X");
                xGo.transform.SetParent(tGo.transform, false);
                var xLE = xGo.AddComponent<LayoutElement>();
                xLE.minWidth=14; xLE.minHeight=14; xLE.preferredWidth=14; xLE.preferredHeight=14; xLE.flexibleWidth=0;
                var xTmp = xGo.AddComponent<TextMeshProUGUI>();
                xTmp.text = "x"; xTmp.fontSize = 11f;
                xTmp.color = RecipeSettings.BtnTextColor;
                xTmp.alignment = TextAlignmentOptions.Center;
                var xEt = xGo.AddComponent<EventTrigger>();
                On(xEt, EventTriggerType.PointerClick, _ => CloseSlot(idx));
                On(xEt, EventTriggerType.PointerEnter, e => Tip("Закрыть", ((PointerEventData)e).position));
                On(xEt, EventTriggerType.PointerExit,  _ => HideTip());
            }
        }

        private void RebuildContent()
        {
            foreach (Transform c in _content) Destroy(c.gameObject);

            if (_slots.Count == 0 || _active >= _slots.Count)
            {
                var h = Lbl(_content, "Наведись на рецепт в Ideas -> нажми " + RecipeSettings.KeyOpen, RecipeSettings.F_Small);
                h.color = RecipeSettings.SubTextColor;
                h.enableWordWrapping = true;
                return;
            }

            var slot = _slots[_active];
            Blueprint bp = null;
            try { bp = GetBp(slot.BpId); } catch {}

            // Header
            var hdr = Row(_content);
            if (RecipeSettings.ShowIcons)
            {
                string iconId = !string.IsNullOrEmpty(slot.ResultId) ? slot.ResultId : slot.BpId;
                try { CardIcon(hdr.transform, iconId, 40f); } catch {}
            }

            var ic = new GameObject("IC");
            ic.transform.SetParent(hdr.transform, false);
            var icVL = ic.AddComponent<VerticalLayoutGroup>();
            icVL.childForceExpandWidth=true; icVL.childControlHeight=true; icVL.childControlWidth=true;
            ic.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ic.AddComponent<LayoutElement>().flexibleWidth = 1;

            string dName = bp != null ? ResolveName(bp, slot.BpId, slot.ResultId) : slot.FullName;
            var nTmp = Lbl(ic.transform, dName, RecipeSettings.F_Title, FontStyles.Bold);
            nTmp.color = RecipeSettings.TextColor;
            nTmp.overflowMode = TextOverflowModes.Ellipsis;

            if (bp == null)
            {
                Div(_content);
                Lbl(_content, "Рецепт не найден: " + slot.BpId, RecipeSettings.F_Ingredient).color = new Color(0.75f,0.3f,0.2f);
                return;
            }

            var sps = new List<Subprint>();
            try
            {
                sps = bp.Subprints?.Where(s => s != null).ToList() ?? new List<Subprint>();
                if (RecipeSettings.DedupVariants)
                {
                    var seen = new HashSet<string>();
                    sps = sps.Where(s => seen.Add(s.RequiredCards == null ? "" : string.Join(",", s.RequiredCards.OrderBy(x => x)))).ToList();
                }
            }
            catch {}

            int total = Mathf.Min(sps.Count, RecipeSettings.MaxVariants);
            Lbl(ic.transform,
                total > 1 ? "Вариантов: " + total + (sps.Count > RecipeSettings.MaxVariants ? "+" : "") : "1 вариант",
                RecipeSettings.F_SubTitle).color = RecipeSettings.SubTextColor;

            Div(_content);

            // Navigation
            if (total > 1)
            {
                slot.Vi = Mathf.Clamp(slot.Vi, 0, total-1);
                var nav = Row(_content);
                IcoBtn(nav.transform, "arrow-left", 28, 24, () => { slot.Vi = (slot.Vi-1+total)%total; RebuildContent(); ForceLayout(); }, "Предыдущий вариант");
                var nl = Lbl(nav.transform, (slot.Vi+1) + "/" + total, RecipeSettings.F_NavBtn, FontStyles.Bold);
                nl.alignment = TextAlignmentOptions.Center; nl.color = RecipeSettings.TextColor;
                nl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                IcoBtn(nav.transform, "arrow-right", 28, 24, () => { slot.Vi = (slot.Vi+1)%total; RebuildContent(); ForceLayout(); }, "Следующий вариант");
            }

            // Subprint
            if (total > 0)
            {
                slot.Vi = Mathf.Clamp(slot.Vi, 0, total-1);
                try { BuildSub(sps[slot.Vi]); } catch (Exception ex) { if (_log!=null) _log.Log("BuildSub error: "+ex.Message); }
            }
            else Lbl(_content, "Нет вариантов крафта.", RecipeSettings.F_Ingredient).color = RecipeSettings.SubTextColor;

            Div(_content);

            // Actions
            var act = Row(_content);
            if (_hasBSB)
            {
                bool pinned = slot.Pinned;
                IcoTextBtn(act.transform, pinned ? "padlock" : "padlock-unlock", pinned ? "Откреп." : "Закреп.", 95, 26, PinActive, pinned ? "Открепить" : "Закрепить");
            }
            Spacer(act.transform);
            IcoTextBtn(act.transform, "cross", "Закрыть", 80, 26, () => CloseSlot(_active), "Закрыть вкладку");
        }

        private void BuildSub(Subprint sp)
        {
            var hr = Row(_content);
            SIcon(hr.transform, "hammer", RecipeSettings.IconColor);
            Lbl(hr.transform, " Нужно для крафта:", RecipeSettings.F_Header, FontStyles.Bold).color = RecipeSettings.TextColor;

            if (sp.RequiredCards != null && sp.RequiredCards.Length > 0)
            {
                var groups = sp.RequiredCards
                    .Where(c => !string.IsNullOrEmpty(c))
                    .GroupBy(c => c)
                    .Select(g => (id: g.Key, cnt: g.Count()))
                    .ToList();

                foreach (var (id, cnt) in groups)
                {
                    var r = Row(_content);
                    r.AddComponent<LayoutElement>().preferredHeight = 28;
                    if (RecipeSettings.ShowIcons) try { CardIcon(r.transform, id, 22f); } catch {}
                    var t = Lbl(r.transform, "  " + RecipeCache.GetName(id) + "  x" + cnt, RecipeSettings.F_Ingredient);
                    t.color = RecipeSettings.TextColor;
                    t.overflowMode = TextOverflowModes.Ellipsis;
                    t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                    var et = r.AddComponent<EventTrigger>();
                    string tipText = RecipeCache.GetName(id) + " x" + cnt;
                    On(et, EventTriggerType.PointerEnter, e => Tip(tipText, ((PointerEventData)e).position));
                    On(et, EventTriggerType.PointerExit,  _ => HideTip());
                }
            }
            else Lbl(_content, "  Ресурсы не нужны", RecipeSettings.F_Ingredient).color = RecipeSettings.SubTextColor;

            if (RecipeSettings.ShowResultRow && !string.IsNullOrEmpty(sp.ResultCard))
            {
                var r = Row(_content);
                r.AddComponent<LayoutElement>().preferredHeight = 26;
                SIcon(r.transform, "arrow-right", RecipeSettings.IconAccentColor);
                if (RecipeSettings.ShowIcons) try { CardIcon(r.transform, sp.ResultCard, 22f); } catch {}
                Lbl(r.transform, "  " + RecipeCache.GetName(sp.ResultCard), RecipeSettings.F_Ingredient, FontStyles.Bold).color = RecipeSettings.IconAccentColor;
            }

            if (RecipeSettings.ShowTime)
            {
                var r = Row(_content);
                SIcon(r.transform, "hourglass", RecipeSettings.SubTextColor);
                Lbl(r.transform, "  " + sp.Time.ToString("0") + " сек", RecipeSettings.F_Header).color = RecipeSettings.SubTextColor;
            }
        }

        private void PinActive()
        {
            if (!_hasBSB || _slots.Count == 0) return;
            var s = _active < _slots.Count ? _slots[_active] : null;
            if (s == null) return;
            s.Pinned = !s.Pinned;
            if (_bsbPin != null) try { var bp = GetBp(s.BpId); if (bp!=null) _bsbPin.Invoke(null, new object[]{bp}); } catch {}
            Toast(s.Pinned ? "Закреплено" : "Откреплено");
            RebuildContent(); ForceLayout();
        }

        // ── Settings ──────────────────────────────────────────────────────────────

        private void OpenSettings()
        {
            if (_settingsOpen) return;
            _settingsOpen = true;

            _snap = new Snap
            {
                ShowIcons=RecipeSettings.ShowIcons, OnlyFound=RecipeSettings.OnlyFound,
                ShowResultRow=RecipeSettings.ShowResultRow, DarkTheme=RecipeSettings.DarkTheme,
                AutoHide=RecipeSettings.AutoHide, KeepOnCraft=RecipeSettings.KeepOnCraft,
                ShowTime=RecipeSettings.ShowTime, DedupVariants=RecipeSettings.DedupVariants,
                FontSizeIdx=RecipeSettings.FontSizeIdx, OpacityIdx=RecipeSettings.OpacityIdx,
                MaxVariants=RecipeSettings.MaxVariants, AutoHideDelay=RecipeSettings.AutoHideDelay,
                KeyOpen=RecipeSettings.KeyOpen, KeyPin=RecipeSettings.KeyPin, KeyHide=RecipeSettings.KeyHide,
            };

            if (WorldManager.instance != null) { _savedSpeed = WorldManager.instance.SpeedUp; WorldManager.instance.SpeedUp = 0f; }

            _blocker = new GameObject("RI_Blocker");
            _blocker.transform.SetParent(_canvas.transform, false);
            var bRt = _blocker.AddComponent<RectTransform>(); bRt.anchorMin=Vector2.zero; bRt.anchorMax=Vector2.one; bRt.sizeDelta=Vector2.zero;
            _blocker.AddComponent<Image>().color = new Color(0,0,0,0.28f);
            _blocker.AddComponent<EventTrigger>();
            _blocker.transform.SetAsLastSibling();

            _settingsOv = new GameObject("RI_Sett");
            _settingsOv.transform.SetParent(_canvas.transform, false);
            var oRt = _settingsOv.AddComponent<RectTransform>();
            oRt.anchorMin=new Vector2(0.5f,0.5f); oRt.anchorMax=new Vector2(0.5f,0.5f);
            oRt.pivot=new Vector2(0.5f,0.5f); oRt.anchoredPosition=Vector2.zero; oRt.sizeDelta=new Vector2(400f,0f);
            _settingsOv.AddComponent<Image>().color = RecipeSettings.BgColor;
            var oOl = _settingsOv.AddComponent<Outline>(); oOl.effectColor=RecipeSettings.OutlineColor; oOl.effectDistance=new Vector2(3,-3);
            var oVL = _settingsOv.AddComponent<VerticalLayoutGroup>(); oVL.padding=new RectOffset(14,14,12,14); oVL.spacing=7; oVL.childForceExpandWidth=true; oVL.childForceExpandHeight=false; oVL.childControlWidth=true; oVL.childControlHeight=true;
            _settingsOv.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _settingsOv.transform.SetAsLastSibling();

            Lbl(_settingsOv.transform, "Настройки - Рецепты", 15f, FontStyles.Bold).color = RecipeSettings.TextColor;
            Div(_settingsOv.transform);

            SRow(_settingsOv.transform, "eye-yes",       "Показывать иконки карт",         RecipeSettings.ShowIcons,     v => RecipeSettings.ShowIcons     = v);
            SRow(_settingsOv.transform, "padlock-unlock","Только открытые рецепты",        RecipeSettings.OnlyFound,     v => RecipeSettings.OnlyFound     = v);
            SRow(_settingsOv.transform, "arrow-right",   "Показывать строку результата",   RecipeSettings.ShowResultRow, v => RecipeSettings.ShowResultRow = v);
            SRow(_settingsOv.transform, "moon",          "Тёмная тема",                    RecipeSettings.DarkTheme,     v => RecipeSettings.DarkTheme     = v);
            SRow(_settingsOv.transform, "timer",         "Авто-скрытие (без мыши)",        RecipeSettings.AutoHide,      v => RecipeSettings.AutoHide      = v);
            SRow(_settingsOv.transform, "padlock",       "Не закрывать при крафте",        RecipeSettings.KeepOnCraft,   v => RecipeSettings.KeepOnCraft   = v);
            SRow(_settingsOv.transform, "hourglass",     "Показывать время крафта",        RecipeSettings.ShowTime,      v => RecipeSettings.ShowTime      = v);
            SRow(_settingsOv.transform, "funnel",        "Дедупликация вариантов",         RecipeSettings.DedupVariants, v => RecipeSettings.DedupVariants = v);

            if (RecipeSettings.AutoHide)
            {
                SecLbl(_settingsOv.transform, "Задержка авто-скрытия:");
                var dr = Row(_settingsOv.transform);
                foreach (int d in new[]{1,3,5}) { int dc=d; bool sel=RecipeSettings.AutoHideDelay==d; TxtBtn(dr.transform, d+"с", 55, 24, () => { RecipeSettings.AutoHideDelay=dc; ReopenSettings(); }, sel); }
            }

            Div(_settingsOv.transform);
            SecLbl(_settingsOv.transform, "Размер шрифта:");
            var fsR = Row(_settingsOv.transform);
            string[] fsL = {"Малый","Нормальный","Крупный"};
            for (int i=0;i<3;i++) { int fi=i; bool sel=RecipeSettings.FontSizeIdx==i; TxtBtn(fsR.transform, fsL[i], -1, 24, () => { RecipeSettings.FontSizeIdx=fi; ReopenSettings(); }, sel, 1f); }

            SecLbl(_settingsOv.transform, "Прозрачность:");
            var opR = Row(_settingsOv.transform);
            string[] opL = {"70%","88%","100%"};
            for (int i=0;i<3;i++) { int oi=i; bool sel=RecipeSettings.OpacityIdx==i; TxtBtn(opR.transform, opL[i], -1, 24, () => { RecipeSettings.OpacityIdx=oi; ReopenSettings(); }, sel, 1f); }

            SecLbl(_settingsOv.transform, "Макс. вариантов:");
            var mvR = Row(_settingsOv.transform);
            foreach (int mv in new[]{10,20,50}) { int mvc=mv; bool sel=RecipeSettings.MaxVariants==mv; TxtBtn(mvR.transform, mv.ToString(), 65, 24, () => { RecipeSettings.MaxVariants=mvc; ReopenSettings(); }, sel); }

            Div(_settingsOv.transform);
            SecLbl(_settingsOv.transform, "Горячие клавиши:");
            KeyRow(_settingsOv.transform, "Открыть рецепт:", RecipeSettings.KeyOpen, "open");
            KeyRow(_settingsOv.transform, "Скрыть панель:",  RecipeSettings.KeyHide, "hide");
            if (_hasBSB) KeyRow(_settingsOv.transform, "Закрепить:", RecipeSettings.KeyPin, "pin");

            Div(_settingsOv.transform);
            SecLbl(_settingsOv.transform, "Положение панели:");
            TxtBtn(Row(_settingsOv.transform).transform, "Сбросить положение", 160, 24,
                () => { if (_panelRt!=null) { float x=-8f; _panelRt.anchoredPosition=new Vector2(x,0f); _slideTarget=x; RecipeSettings.SavedPos=new Vector2(x,0f); } }, false);

            Div(_settingsOv.transform);
            var bR = Row(_settingsOv.transform);
            IcoTextBtn(bR.transform, "rotate-right", "По умолчанию", 120, 28, () => { RecipeSettings.ResetDefaults(); ReopenSettings(); }, "Сбросить всё");
            Spacer(bR.transform);
            IcoTextBtn(bR.transform, "cross", "Отмена", 90, 28, () => CloseSettings(false), "Отмена (ESC)");
            IcoTextBtn(bR.transform, "check", "Сохранить", 90, 28, () => CloseSettings(true), "Сохранить настройки");

            LayoutRebuilder.ForceRebuildLayoutImmediate(oRt);
        }

        private void SRow(Transform p, string icon, string label, bool cur, Action<bool> onChange)
        {
            var r = Row(p);
            SIcon(r.transform, cur ? "eye-yes" : "eye-no", cur ? RecipeSettings.IconAccentColor : RecipeSettings.SubTextColor, 14f);
            var l = Lbl(r.transform, "  " + label, 12.5f);
            l.color = RecipeSettings.TextColor;
            l.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            TxtBtn(r.transform, cur ? "ВКЛ" : "ВЫКЛ", 55, 22, () => { onChange(!cur); ReopenSettings(); }, cur);
        }

        private void SecLbl(Transform p, string text)
        {
            Lbl(p, text, 12f).color = RecipeSettings.SubTextColor;
        }

        private void KeyRow(Transform p, string label, KeyCode cur, string target)
        {
            var r = Row(p);
            var l = Lbl(r.transform, label, 12f); l.color = RecipeSettings.TextColor;
            l.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
            bool waiting = _bindingKey && _bindTarget == target;
            TxtBtn(r.transform, waiting ? "Нажмите клавишу..." : cur.ToString(), 110, 22,
                () => { _bindingKey=true; _bindTarget=target; ReopenSettings(); }, waiting);
        }

        private void ApplyBind(string t, KeyCode kc)
        {
            if (t=="open") RecipeSettings.KeyOpen=kc;
            else if (t=="pin") RecipeSettings.KeyPin=kc;
            else if (t=="hide") RecipeSettings.KeyHide=kc;
            RecipeSettings.Save(); Toast("Клавиша: " + kc);
        }

        // Reopen without reverting (just refresh overlay UI)
        private void ReopenSettings()
        {
            if (!_settingsOpen) return;
            var savedSnap = _snap;
            if (_settingsOv!=null) { Destroy(_settingsOv); _settingsOv=null; }
            if (_blocker!=null)    { Destroy(_blocker);    _blocker=null; }
            _settingsOpen = false;
            OpenSettings();
            _snap = savedSnap; // restore original snapshot
        }

        private void CloseSettings(bool save)
        {
            if (!_settingsOpen) return;
            _settingsOpen = false;
            bool themeChange;

            if (save)
            {
                RecipeSettings.Save(); Toast("Настройки сохранены");
                themeChange = RecipeSettings.DarkTheme!=_snap.DarkTheme || RecipeSettings.OpacityIdx!=_snap.OpacityIdx;
            }
            else
            {
                themeChange = RecipeSettings.DarkTheme!=_snap.DarkTheme || RecipeSettings.OpacityIdx!=_snap.OpacityIdx;
                RecipeSettings.ShowIcons=_snap.ShowIcons; RecipeSettings.OnlyFound=_snap.OnlyFound;
                RecipeSettings.ShowResultRow=_snap.ShowResultRow; RecipeSettings.DarkTheme=_snap.DarkTheme;
                RecipeSettings.AutoHide=_snap.AutoHide; RecipeSettings.KeepOnCraft=_snap.KeepOnCraft;
                RecipeSettings.ShowTime=_snap.ShowTime; RecipeSettings.DedupVariants=_snap.DedupVariants;
                RecipeSettings.FontSizeIdx=_snap.FontSizeIdx; RecipeSettings.OpacityIdx=_snap.OpacityIdx;
                RecipeSettings.MaxVariants=_snap.MaxVariants; RecipeSettings.AutoHideDelay=_snap.AutoHideDelay;
                RecipeSettings.KeyOpen=_snap.KeyOpen; RecipeSettings.KeyPin=_snap.KeyPin; RecipeSettings.KeyHide=_snap.KeyHide;
            }

            if (WorldManager.instance != null) WorldManager.instance.SpeedUp = _savedSpeed;
            if (_settingsOv!=null) { Destroy(_settingsOv); _settingsOv=null; }
            if (_blocker!=null)    { Destroy(_blocker);    _blocker=null; }

            if (themeChange) RebuildAll();
            else Refresh();
        }

        private void RebuildAll()
        {
            if (_panelRt  != null) { Destroy(_panelRt.gameObject);  _panelRt  = null; }
            if (_stripRt  != null) { Destroy(_stripRt.gameObject);  _stripRt  = null; }
            if (_tipGo    != null) { Destroy(_tipGo);    _tipGo    = null; }
            if (_toastGo  != null) { Destroy(_toastGo);  _toastGo  = null; }
            _uiBuilt = false;
            _canvas = GameCanvas.instance?.Canvas ?? FindObjectOfType<Canvas>();
            EnsureUI();
            if (_slots.Count > 0) { SetCollapsed(false); Refresh(); }
        }

        // ── Utility ───────────────────────────────────────────────────────────────

        private string ResolveName(Blueprint bp, string bpId, string resultId)
        {
            if (!string.IsNullOrEmpty(resultId))
            {
                var n = RecipeCache.GetName(resultId);
                if (!n.Equals(resultId) && !n.Equals(resultId.Replace("_"," "))) return n;
            }
            if (bp != null)
            {
                try { var loc = SokLoc.Translate(bp.NameTerm); if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("---")) return loc; } catch {}
                var bn = RecipeCache.GetName(bp.CardId);
                if (!bn.Equals(bp.CardId) && !bn.Equals(bp.CardId.Replace("_"," "))) return bn;
            }
            string raw = !string.IsNullOrEmpty(resultId) ? resultId : bpId;
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(raw.Replace("_"," "));
        }

        private Blueprint GetBp(string id) =>
            WorldManager.instance?.GameDataLoader?.BlueprintPrefabs?.FirstOrDefault(b => b?.CardId == id);

        private void ForceLayout()
        {
            if (_panelRt != null && _panelRt.gameObject.activeSelf)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRt);
        }

        private void Tip(string text, Vector2 pos)
        {
            if (_tipGo==null || string.IsNullOrEmpty(text)) return;
            _tipTmp.text = text; _tipGo.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas.GetComponent<RectTransform>(), pos, null, out var local);
            _tipRt.anchoredPosition = local + new Vector2(8f,-8f);
            _tipGo.transform.SetAsLastSibling();
        }
        private void HideTip() { if (_tipGo!=null) _tipGo.SetActive(false); }

        private void Toast(string msg, float dur=2f)
        {
            if (_toastTmp==null) return;
            if (_toastCo!=null) StopCoroutine(_toastCo);
            _toastTmp.text = msg; _toastGo.SetActive(true); _toastGo.transform.SetAsLastSibling();
            _toastCo = StartCoroutine(HideToast(dur));
        }
        private IEnumerator HideToast(float t) { yield return new WaitForSecondsRealtime(t); if (_toastGo) _toastGo.SetActive(false); }

        // ── UI factory ────────────────────────────────────────────────────────────

        private static GameObject Row(Transform p)
        {
            var g = new GameObject("R"); g.transform.SetParent(p, false);
            var h = g.AddComponent<HorizontalLayoutGroup>(); h.spacing=4; h.childForceExpandHeight=false; h.childForceExpandWidth=false; h.childControlHeight=true; h.childControlWidth=true;
            g.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return g;
        }

        private static void Div(Transform p)
        {
            var g = new GameObject("D"); g.transform.SetParent(p, false);
            g.AddComponent<Image>().color = RecipeSettings.DividerColor;
            var le = g.AddComponent<LayoutElement>(); le.preferredHeight=1; le.flexibleWidth=1;
        }

        private static void Spacer(Transform p)
        {
            var g = new GameObject("Sp"); g.transform.SetParent(p, false);
            g.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        private static TextMeshProUGUI Lbl(Transform p, string text, float size, FontStyles style=FontStyles.Normal)
        {
            var g = new GameObject("L"); g.transform.SetParent(p, false);
            var t = g.AddComponent<TextMeshProUGUI>();
            t.text=text; t.fontSize=size; t.fontStyle=style;
            t.color=RecipeSettings.TextColor; t.overflowMode=TextOverflowModes.Ellipsis; t.enableWordWrapping=false;
            g.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return t;
        }

        // Fixed-size inline icon (layout-friendly)
        private static void SIcon(Transform p, string name, Color color, float size=15f)
        {
            var g = new GameObject("SI"); g.transform.SetParent(p, false);
            var le = g.AddComponent<LayoutElement>();
            le.minWidth=size; le.minHeight=size; le.preferredWidth=size; le.preferredHeight=size; le.flexibleWidth=0; le.flexibleHeight=0;
            var img = g.AddComponent<Image>();
            img.sprite = IconLoader.Get(name); img.color = color; img.preserveAspect = true; img.raycastTarget = false;
        }

        // Icon-only button (PNG icon fills interior)
        private static void IcoBtn(Transform p, string iconName, float w, float h, Action onClick, string tip=null)
        {
            var g = new GameObject("IB"); g.transform.SetParent(p, false);
            var le = g.AddComponent<LayoutElement>(); le.preferredWidth=w; le.preferredHeight=h; le.flexibleWidth=0;
            var bg = g.AddComponent<Image>(); bg.color = RecipeSettings.BtnColor;
            var btn = g.AddComponent<Button>(); btn.targetGraphic=bg; btn.onClick.AddListener(()=>onClick?.Invoke());
            // Icon inside: anchored to fill with padding
            var ig = new GameObject("I"); ig.transform.SetParent(g.transform, false);
            var iRt = ig.AddComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0.12f,0.12f); iRt.anchorMax = new Vector2(0.88f,0.88f); iRt.sizeDelta = Vector2.zero;
            var img = ig.AddComponent<Image>();
            img.sprite = IconLoader.Get(iconName); img.color = RecipeSettings.IconColor; img.preserveAspect = true; img.raycastTarget = false;
            if (tip!=null && Instance!=null)
            {
                var et = g.AddComponent<EventTrigger>();
                On(et, EventTriggerType.PointerEnter, e => Instance.Tip(tip, ((PointerEventData)e).position));
                On(et, EventTriggerType.PointerExit,  _ => Instance.HideTip());
            }
        }

        // Symbol button (text label, no icon PNG)
        private static GameObject SymBtn(Transform p, string sym, float w, float h, Action onClick, string tip=null)
        {
            var g = new GameObject("SBtn"); g.transform.SetParent(p, false);
            var le = g.AddComponent<LayoutElement>(); le.preferredWidth=w; le.preferredHeight=h; le.flexibleWidth=0;
            var bg = g.AddComponent<Image>(); bg.color = RecipeSettings.BtnColor;
            var btn = g.AddComponent<Button>(); btn.targetGraphic=bg; btn.onClick.AddListener(()=>onClick?.Invoke());

            var tg = new GameObject("T"); tg.transform.SetParent(g.transform, false);
            var tRt = tg.AddComponent<RectTransform>(); tRt.anchorMin=Vector2.zero; tRt.anchorMax=Vector2.one; tRt.sizeDelta=Vector2.zero;
            var tmp = tg.AddComponent<TextMeshProUGUI>();
            tmp.text=sym; tmp.fontSize=13f; tmp.color=RecipeSettings.BtnTextColor; tmp.alignment=TextAlignmentOptions.Center; tmp.enableWordWrapping=false;

            if (tip!=null && Instance!=null)
            {
                var et = g.AddComponent<EventTrigger>();
                On(et, EventTriggerType.PointerEnter, e => Instance.Tip(tip, ((PointerEventData)e).position));
                On(et, EventTriggerType.PointerExit,  _ => Instance.HideTip());
            }
            return g;
        }

        // Icon + text button (PNG icon)
        private static void IcoTextBtn(Transform p, string iconName, string label, float w, float h, Action onClick, string tip=null)
        {
            var g = new GameObject("ITB"); g.transform.SetParent(p, false);
            var le = g.AddComponent<LayoutElement>(); if(w>0) le.preferredWidth=w; le.preferredHeight=h; le.flexibleWidth=0;
            var bg = g.AddComponent<Image>(); bg.color = RecipeSettings.BtnColor;
            var btn = g.AddComponent<Button>(); btn.targetGraphic=bg; btn.onClick.AddListener(()=>onClick?.Invoke());
            var hl = g.AddComponent<HorizontalLayoutGroup>(); hl.padding=new RectOffset(5,5,2,2); hl.spacing=3; hl.childForceExpandWidth=false; hl.childForceExpandHeight=false; hl.childControlWidth=true; hl.childControlHeight=true;

            var ig = new GameObject("I"); ig.transform.SetParent(g.transform, false);
            var ile = ig.AddComponent<LayoutElement>(); ile.minWidth=13; ile.minHeight=13; ile.preferredWidth=13; ile.preferredHeight=13; ile.flexibleWidth=0; ile.flexibleHeight=0;
            var img = ig.AddComponent<Image>(); img.sprite=IconLoader.Get(iconName); img.color=RecipeSettings.IconColor; img.preserveAspect=true; img.raycastTarget=false;

            var tg = new GameObject("T"); tg.transform.SetParent(g.transform, false);
            var tmp = tg.AddComponent<TextMeshProUGUI>(); tmp.text=label; tmp.fontSize=RecipeSettings.F_Small; tmp.color=RecipeSettings.BtnTextColor; tmp.alignment=TextAlignmentOptions.Center; tmp.overflowMode=TextOverflowModes.Ellipsis; tmp.enableWordWrapping=false;
            tg.AddComponent<LayoutElement>().flexibleWidth=1;

            if (tip!=null && Instance!=null)
            {
                var et = g.AddComponent<EventTrigger>();
                On(et, EventTriggerType.PointerEnter, e => Instance.Tip(tip, ((PointerEventData)e).position));
                On(et, EventTriggerType.PointerExit,  _ => Instance.HideTip());
            }
        }

        // Symbol + text button
        private static void SymTextBtn(Transform p, string sym, string label, float w, float h, Action onClick, string tip=null)
        {
            var g = new GameObject("STBtn"); g.transform.SetParent(p, false);
            var le = g.AddComponent<LayoutElement>(); if(w>0) le.preferredWidth=w; le.preferredHeight=h; le.flexibleWidth=0;
            var bg = g.AddComponent<Image>(); bg.color = RecipeSettings.BtnColor;
            var btn = g.AddComponent<Button>(); btn.targetGraphic=bg; btn.onClick.AddListener(()=>onClick?.Invoke());
            var hl = g.AddComponent<HorizontalLayoutGroup>(); hl.padding=new RectOffset(5,5,2,2); hl.spacing=3; hl.childForceExpandWidth=false; hl.childForceExpandHeight=false; hl.childControlWidth=true; hl.childControlHeight=true;

            var sg = new GameObject("S"); sg.transform.SetParent(g.transform, false);
            var sLE = sg.AddComponent<LayoutElement>(); sLE.preferredWidth=16; sLE.preferredHeight=16; sLE.flexibleWidth=0;
            var sTmp = sg.AddComponent<TextMeshProUGUI>(); sTmp.text=sym; sTmp.fontSize=11f; sTmp.color=RecipeSettings.BtnTextColor; sTmp.alignment=TextAlignmentOptions.Center; sTmp.enableWordWrapping=false;

            var tg = new GameObject("T"); tg.transform.SetParent(g.transform, false);
            var tmp = tg.AddComponent<TextMeshProUGUI>(); tmp.text=label; tmp.fontSize=RecipeSettings.F_Small; tmp.color=RecipeSettings.BtnTextColor; tmp.alignment=TextAlignmentOptions.Center; tmp.overflowMode=TextOverflowModes.Ellipsis; tmp.enableWordWrapping=false;
            tg.AddComponent<LayoutElement>().flexibleWidth=1;

            if (tip!=null && Instance!=null)
            {
                var et = g.AddComponent<EventTrigger>();
                On(et, EventTriggerType.PointerEnter, e => Instance.Tip(tip, ((PointerEventData)e).position));
                On(et, EventTriggerType.PointerExit,  _ => Instance.HideTip());
            }
        }

        // Text-only button
        private static void TxtBtn(Transform p, string label, float w, float h, Action onClick, bool active, float flex=0f)
        {
            var g = new GameObject("TBtn"); g.transform.SetParent(p, false);
            var le = g.AddComponent<LayoutElement>(); if(w>0) le.preferredWidth=w; le.preferredHeight=h; if(flex>0) le.flexibleWidth=flex;
            var bg = g.AddComponent<Image>(); bg.color = active ? RecipeSettings.BtnActiveColor : RecipeSettings.BtnColor;
            var btn = g.AddComponent<Button>(); btn.targetGraphic=bg; btn.onClick.AddListener(()=>onClick?.Invoke());
            var tg = new GameObject("T"); tg.transform.SetParent(g.transform, false);
            var tRt = tg.AddComponent<RectTransform>(); tRt.anchorMin=Vector2.zero; tRt.anchorMax=Vector2.one; tRt.sizeDelta=Vector2.zero;
            var tmp = tg.AddComponent<TextMeshProUGUI>(); tmp.text=label; tmp.fontSize=RecipeSettings.F_Small; tmp.color=RecipeSettings.BtnTextColor; tmp.alignment=TextAlignmentOptions.Center; tmp.overflowMode=TextOverflowModes.Ellipsis; tmp.enableWordWrapping=false;
        }

        private static void CardIcon(Transform p, string cardId, float size)
        {
            var g = new GameObject("CI"); g.transform.SetParent(p, false);
            var le = g.AddComponent<LayoutElement>(); le.minWidth=size; le.minHeight=size; le.preferredWidth=size; le.preferredHeight=size; le.flexibleWidth=0; le.flexibleHeight=0;
            if (string.IsNullOrEmpty(cardId)) return;
            Sprite s = RecipeCache.GetIcon(cardId); if (s==null) return;
            var img = g.AddComponent<Image>(); img.sprite=s; img.preserveAspect=true; img.type=Image.Type.Simple;
        }

        private static void On(EventTrigger et, EventTriggerType type, Action<BaseEventData> cb)
        {
            var e = new EventTrigger.Entry { eventID=type }; e.callback.AddListener(d=>cb(d)); et.triggers.Add(e);
        }
    }

    // ── Drag handler ──────────────────────────────────────────────────────────────

    public class PanelDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform Panel;
        public Action OnMoved;
        private Vector2 _off;
        public void OnBeginDrag(PointerEventData e) { RectTransformUtility.ScreenPointToLocalPointInRectangle(Panel.parent as RectTransform,e.position,e.pressEventCamera,out var local); _off=Panel.anchoredPosition-local; }
        public void OnDrag(PointerEventData e) { if(Panel==null)return; RectTransformUtility.ScreenPointToLocalPointInRectangle(Panel.parent as RectTransform,e.position,e.pressEventCamera,out var local); Panel.anchoredPosition=local+_off; }
        public void OnEndDrag(PointerEventData e) { OnMoved?.Invoke(); }
    }
}
