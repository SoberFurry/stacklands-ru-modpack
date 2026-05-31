using System;
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
        private static string _modPath;
        private static ModLogger L;

        private bool _uiBuilt;
        private bool _collapsed;
        private bool _settingsOpen;
        private float _savedSpeedUp = 1f;
        private float _autoHideTimer = 0f;
        private bool  _mouseWasInPanel = false;

        private static bool _hasBetterSideBar;

        private bool _lastDarkTheme;
        private int  _lastOpacityIdx;

        // ── Slots ─────────────────────────────────────────────────────────────────
        private class Slot
        {
            public string BpId, ResultId, Label, FullName;
            public int  SprintIdx = 0;
            public bool Pinned    = false;
        }
        private readonly List<Slot> _slots = new List<Slot>();
        private int _active = 0;

        // ── UI references ─────────────────────────────────────────────────────────
        private RectTransform _panel;
        private GameObject    _contentRoot;
        private Transform     _tabBar;
        private Transform     _content;
        private TextMeshProUGUI _hoverLabel;
        private GameObject    _settingsOverlay;
        private GameObject    _modalBlocker;
        private Image         _panelBg;
        private Image         _titleBg;
        private bool          _visible;

        // ── Static API ────────────────────────────────────────────────────────────

        public static void EnsureCreated(string modPath, ModLogger logger)
        {
            if (Instance != null) return;
            _modPath = modPath;
            L = logger;
            var go = new GameObject("RI_Root");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<RecipePanel>();
            _hasBetterSideBar = ModManager.LoadedMods?.Any(m => m?.Manifest?.Id == "better_sidebar") ?? false;
        }

        public static void ToggleBlueprint(string bpId, string resultId)
        {
            if (Instance == null) return;
            Instance.EnsureUI();

            for (int i = 0; i < Instance._slots.Count; i++)
            {
                if (Instance._slots[i].BpId == bpId)
                {
                    Instance.CloseSlot(i);
                    return;
                }
            }
            Instance.AddSlot(bpId, resultId);
            Instance.SetVisible(true);
            Instance.Refresh();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake() { Instance = this; }

        private void EnsureUI()
        {
            if (_uiBuilt) return;
            _uiBuilt = true;
            BuildUI();
        }

        private void Update()
        {
            if (!_visible) return;

            if (RecipeSettings.AutoHide && _panel != null)
            {
                bool inPanel = false;
                try
                {
                    Vector2 mp = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                    inPanel = RectTransformUtility.RectangleContainsScreenPoint(_panel, mp, null);
                }
                catch { }

                if (inPanel) { _autoHideTimer = 0f; _mouseWasInPanel = true; }
                else if (_mouseWasInPanel)
                {
                    _autoHideTimer += Time.unscaledDeltaTime;
                    if (_autoHideTimer > RecipeSettings.AutoHideDelay) { SetVisible(false); _mouseWasInPanel = false; }
                }
            }

            try
            {
                if (_settingsOpen && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                    CloseSettings(false);
            }
            catch { }

            try
            {
                if (!_settingsOpen && _slots.Count > 0 &&
                    UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
                {
                    for (int i = 0; i < _tabBar.childCount; i++)
                    {
                        var child = _tabBar.GetChild(i);
                        if (child == null) continue;
                        var rt = child.GetComponent<RectTransform>();
                        if (rt == null) continue;
                        Vector2 mp = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                        if (RectTransformUtility.RectangleContainsScreenPoint(rt, mp, null))
                        { CloseSlot(i); return; }
                    }
                }
            }
            catch { }
        }

        // ── Build skeleton UI ─────────────────────────────────────────────────────

        private void BuildUI()
        {
            Canvas canvas = GameCanvas.instance?.Canvas ?? FindObjectOfType<Canvas>();
            if (canvas == null) { _uiBuilt = false; return; }

            var root = new GameObject("RI_Panel");
            root.transform.SetParent(canvas.transform, false);
            _panel = root.AddComponent<RectTransform>();

            _panel.anchorMin = new Vector2(1f, 0.5f);
            _panel.anchorMax = new Vector2(1f, 0.5f);
            _panel.pivot     = new Vector2(1f, 0.5f);
            _panel.anchoredPosition = RecipeSettings.SavedPos;

            float cw = canvas.GetComponent<RectTransform>()?.rect.width ?? 1920f;
            _panel.sizeDelta = new Vector2(Mathf.Clamp(cw * 0.22f, 270f, 420f), 0f);

            _panelBg = root.AddComponent<Image>();
            _panelBg.color = RecipeSettings.BgColor;

            var ol = root.AddComponent<Outline>();
            ol.effectColor    = RecipeSettings.OutlineColor;
            ol.effectDistance = new Vector2(2, -2);

            var vl = root.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(0, 0, 0, 8);
            vl.spacing = 0;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            vl.childControlWidth = true;     vl.childControlHeight = true;
            root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Title bar ────────────────────────────────────────────────────────
            var titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(root.transform, false);
            _titleBg = titleBar.AddComponent<Image>();
            _titleBg.color = RecipeSettings.TitleBgColor;
            var tbVL = titleBar.AddComponent<HorizontalLayoutGroup>();
            tbVL.padding = new RectOffset(10, 6, 6, 6);
            tbVL.spacing = 4;
            tbVL.childForceExpandHeight = false; tbVL.childForceExpandWidth = false;
            tbVL.childControlHeight = true;      tbVL.childControlWidth = true;
            titleBar.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var drag = titleBar.AddComponent<PanelDragHandler>();
            drag.Panel = _panel;

            var titleTmp = MkTmp(titleBar.transform, "Рецепты", 15f, FontStyles.Bold);
            titleTmp.color = RecipeSettings.TextColor;
            titleTmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            TitleBtn(titleBar.transform, "▼", ToggleCollapse);
            TitleBtn(titleBar.transform, "S", OpenSettings);
            TitleBtn(titleBar.transform, "×", () => SetVisible(false));

            // ── Content root (hidden when collapsed) ─────────────────────────────
            _contentRoot = new GameObject("ContentRoot");
            _contentRoot.transform.SetParent(root.transform, false);
            var crVL = _contentRoot.AddComponent<VerticalLayoutGroup>();
            crVL.spacing = 4;
            crVL.padding = new RectOffset(8, 8, 6, 0);
            crVL.childForceExpandWidth = true; crVL.childForceExpandHeight = false;
            crVL.childControlWidth = true;     crVL.childControlHeight = true;
            _contentRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Tab bar
            var tabGo = new GameObject("TabBar");
            tabGo.transform.SetParent(_contentRoot.transform, false);
            var tbl = tabGo.AddComponent<HorizontalLayoutGroup>();
            tbl.spacing = 3; tbl.childForceExpandWidth = false; tbl.childForceExpandHeight = false;
            tbl.childControlWidth = true; tbl.childControlHeight = true;
            tabGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _tabBar = tabGo.transform;

            Divider(_contentRoot.transform);

            // ── Content list ──────────────────────────────────────────────────────
            var contentGo = new GameObject("ContentList");
            contentGo.transform.SetParent(_contentRoot.transform, false);
            var cvl = contentGo.AddComponent<VerticalLayoutGroup>();
            cvl.spacing = 5; cvl.padding = new RectOffset(0, 0, 2, 2);
            cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;
            cvl.childControlWidth = true;     cvl.childControlHeight = true;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _content = contentGo.transform;

            // Hover label — поверх всего контента, но внутри _panel
            var hlGo = new GameObject("HoverLabel");
            hlGo.transform.SetParent(_panel, false);
            var hlRt = hlGo.AddComponent<RectTransform>();
            hlRt.anchorMin = new Vector2(0, 1); hlRt.anchorMax = new Vector2(1, 1);
            hlRt.pivot = new Vector2(0.5f, 1f);
            hlRt.anchoredPosition = new Vector2(0, -60f);
            hlRt.sizeDelta = new Vector2(0, 0);
            _hoverLabel = hlGo.AddComponent<TextMeshProUGUI>();
            _hoverLabel.fontSize = RecipeSettings.F_Small;
            _hoverLabel.color = RecipeSettings.SubTextColor;
            _hoverLabel.alignment = TextAlignmentOptions.Center;
            _hoverLabel.enableWordWrapping = true;
            var hlBg = hlGo.AddComponent<Image>();
            hlBg.color = new Color(0.1f, 0.08f, 0.05f, 0.85f);
            _hoverLabel.gameObject.SetActive(false);
            hlGo.transform.SetAsLastSibling();

            _panel.SetAsLastSibling();

            SetVisible(false);
        }

        // ── Slot management ───────────────────────────────────────────────────────

        private void AddSlot(string bpId, string resultId)
        {
            if (_slots.Count >= 6) _slots.RemoveAt(0);

            Blueprint bp = GetBp(bpId);

            if (string.IsNullOrEmpty(resultId) && bp?.Subprints?.Count > 0)
                resultId = bp.Subprints.FirstOrDefault(s => s != null && !string.IsNullOrEmpty(s.ResultCard))?.ResultCard;
            if (string.IsNullOrEmpty(resultId)) resultId = bpId;

            string full  = ResolveDisplayName(bp, bpId, resultId);
            string label = full.Length > 9 ? full.Substring(0, 8) + "…" : full;

            _slots.Add(new Slot { BpId = bpId, ResultId = resultId, Label = label, FullName = full });
            _active = _slots.Count - 1;
        }

        private void CloseSlot(int idx)
        {
            if (idx < 0 || idx >= _slots.Count) return;
            _slots.RemoveAt(idx);
            _active = Mathf.Clamp(_active, 0, Mathf.Max(0, _slots.Count - 1));
            if (_slots.Count == 0) SetVisible(false);
            else Refresh();
        }

        // ── Refresh ───────────────────────────────────────────────────────────────

        private void Refresh()
        {
            if (_panel == null) return;

            if (_panelBg  != null) _panelBg.color  = RecipeSettings.BgColor;
            if (_titleBg  != null) _titleBg.color  = RecipeSettings.TitleBgColor;

            RebuildTabs();
            RebuildContent();

            if (_contentRoot != null) _contentRoot.SetActive(!_collapsed);

            if (_panel.gameObject.activeSelf)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
        }

        private void RebuildTabs()
        {
            foreach (Transform c in _tabBar) Destroy(c.gameObject);
            if (_hoverLabel != null) { _hoverLabel.text = ""; _hoverLabel.gameObject.SetActive(false); }

            if (_slots.Count == 0)
            {
                var hint = MkTmp(_tabBar, "Наведись на рецепт в Ideas → R", RecipeSettings.F_Small);
                hint.color = RecipeSettings.SubTextColor;
                return;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                int idx = i; bool active = (i == _active);
                var slot = _slots[i];

                var tabGo = new GameObject("Tab_" + i);
                tabGo.transform.SetParent(_tabBar, false);

                var tabImg = tabGo.AddComponent<Image>();
                tabImg.color = active
                    ? ColorManager.instance.HoverButtonColor
                    : ColorManager.instance.ButtonColor;

                var thl = tabGo.AddComponent<HorizontalLayoutGroup>();
                thl.padding = new RectOffset(5, 3, 3, 3); thl.spacing = 2;
                thl.childForceExpandWidth = false; thl.childForceExpandHeight = false;
                thl.childControlWidth = true; thl.childControlHeight = true;
                tabGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                tabGo.AddComponent<LayoutElement>().preferredWidth = 88;

                var lbl = MkTmp(tabGo.transform, slot.Label, RecipeSettings.F_Small);
                lbl.color = ColorManager.instance.ButtonTextColor;
                lbl.overflowMode = TextOverflowModes.Ellipsis;
                lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

                var et = tabGo.AddComponent<EventTrigger>();
                var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                click.callback.AddListener((_) => { _active = idx; Refresh(); });
                et.triggers.Add(click);

                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                string fullName = slot.FullName;
                enter.callback.AddListener((_) =>
                {
                    if (_hoverLabel != null)
                    {
                        _hoverLabel.text = fullName + "  (R — закрыть)";
                        _hoverLabel.gameObject.SetActive(true);
                    }
                });
                et.triggers.Add(enter);

                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener((_) => { if (_hoverLabel != null) _hoverLabel.gameObject.SetActive(false); });
                et.triggers.Add(exit);

                var xGo = new GameObject("X"); xGo.transform.SetParent(tabGo.transform, false);
                var xT = xGo.AddComponent<TextMeshProUGUI>();
                xT.text = "×"; xT.fontSize = 13; xT.color = ColorManager.instance.ButtonTextColor;
                xT.alignment = TextAlignmentOptions.Center;
                xGo.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                xGo.AddComponent<LayoutElement>().preferredWidth = 16;
                var xEt = xGo.AddComponent<EventTrigger>();
                var xC = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                xC.callback.AddListener((_) => CloseSlot(idx));
                xEt.triggers.Add(xC);
            }
        }

        private void RebuildContent()
        {
            foreach (Transform c in _content) Destroy(c.gameObject);

            var slot = _active < _slots.Count ? _slots[_active] : null;
            if (L != null) L.Log($"RebuildContent: _active={_active}, slots={_slots.Count}, bp={slot?.BpId}");

            if (_slots.Count == 0 || _active >= _slots.Count) return;

            Blueprint bp = GetBp(slot.BpId);

            // ── Card header ──────────────────────────────────────────────────
            var hdr = HRow(_content);

            if (RecipeSettings.ShowIcons)
            {
                string iconId = !string.IsNullOrEmpty(slot.ResultId) ? slot.ResultId : slot.BpId;
                try { CardIconGo(hdr.transform, iconId, 44f); } catch (Exception ex) { if (L != null) L.Log("CardIconGo hdr error: " + ex); }
            }

            var infoCol = new GameObject("InfoCol");
            infoCol.transform.SetParent(hdr.transform, false);
            var icVL = infoCol.AddComponent<VerticalLayoutGroup>();
            icVL.childForceExpandWidth = true; icVL.childControlHeight = true; icVL.childControlWidth = true;
            infoCol.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            infoCol.AddComponent<LayoutElement>().flexibleWidth = 1;

            string displayName = bp != null ? ResolveDisplayName(bp, slot.BpId, slot.ResultId) : slot.FullName;
            var nameTmp = MkTmp(infoCol.transform, displayName, RecipeSettings.F_Title, FontStyles.Bold);
            nameTmp.color = RecipeSettings.TextColor;
            nameTmp.overflowMode = TextOverflowModes.Ellipsis;

            if (bp == null)
            {
                Divider(_content);
                var notFound = MkTmp(_content, $"Рецепт не найден: {slot.BpId}", RecipeSettings.F_Ingredient);
                notFound.color = new Color(0.75f, 0.35f, 0.25f);
                if (L != null) L.Log($"RecipePanel: blueprint '{slot.BpId}' not found");
                return;
            }

            var sprints = bp.Subprints?.Where(s => s != null).ToList() ?? new List<Subprint>();

            if (RecipeSettings.DedupVariants)
            {
                var seen = new HashSet<string>();
                sprints = sprints.Where(s => {
                    string key = s.RequiredCards == null ? "" :
                        string.Join(",", s.RequiredCards.OrderBy(x => x));
                    return seen.Add(key);
                }).ToList();
            }

            int total = Mathf.Min(sprints.Count, RecipeSettings.MaxVariants);

            var varTmp = MkTmp(infoCol.transform,
                total > 1 ? $"Вариантов: {total}{(sprints.Count > RecipeSettings.MaxVariants ? "+" : "")}" : "1 вариант",
                RecipeSettings.F_SubTitle);
            varTmp.color = RecipeSettings.SubTextColor;

            Divider(_content);

            // ── Navigation (only if multiple variants) ───────────────────────
            if (total > 1)
            {
                slot.SprintIdx = Mathf.Clamp(slot.SprintIdx, 0, total - 1);
                var nav = HRow(_content);

                try
                {
                    GameBtn(nav.transform, "<", 36, 28,
                        () => { slot.SprintIdx = (slot.SprintIdx - 1 + total) % total; RebuildContent(); ForceLayout(); });
                }
                catch (Exception ex) { if (L != null) L.Log("GameBtn nav prev error: " + ex); }

                var navLbl = MkTmp(nav.transform, $"{slot.SprintIdx + 1} / {total}", RecipeSettings.F_NavBtn, FontStyles.Bold);
                navLbl.alignment = TextAlignmentOptions.Center;
                navLbl.color = RecipeSettings.TextColor;
                navLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

                try
                {
                    GameBtn(nav.transform, ">", 36, 28,
                        () => { slot.SprintIdx = (slot.SprintIdx + 1) % total; RebuildContent(); ForceLayout(); });
                }
                catch (Exception ex) { if (L != null) L.Log("GameBtn nav next error: " + ex); }
            }

            // ── Current subprint ─────────────────────────────────────────────
            if (total > 0)
            {
                slot.SprintIdx = Mathf.Clamp(slot.SprintIdx, 0, total - 1);
                BuildSubprint(sprints[slot.SprintIdx]);
            }
            else
            {
                MkTmp(_content, "Нет вариантов крафта.", RecipeSettings.F_Ingredient).color = RecipeSettings.SubTextColor;
            }

            Divider(_content);

            // ── Actions ──────────────────────────────────────────────────────
            var act = HRow(_content);

            if (_hasBetterSideBar)
            {
                try
                {
                    GameBtn(act.transform, slot.Pinned ? "Откреп." : "Закреп.", 88, 28,
                        () => { slot.Pinned = !slot.Pinned; RebuildContent(); ForceLayout(); });
                }
                catch (Exception ex) { if (L != null) L.Log("GameBtn pin error: " + ex); }
            }

            var spLE = new GameObject("Sp").AddComponent<LayoutElement>();
            spLE.transform.SetParent(act.transform, false);
            spLE.flexibleWidth = 1;

            try
            {
                GameBtn(act.transform, "Закрыть", 88, 28, () => CloseSlot(_active));
            }
            catch (Exception ex) { if (L != null) L.Log("GameBtn close error: " + ex); }

            if (L != null) L.Log("RebuildContent OK: built " + _content.childCount + " children");
        }

        private void BuildSubprint(Subprint sp)
        {
            var ingHdr = MkTmp(_content, "Нужно для крафта:", RecipeSettings.F_Header, FontStyles.Bold);
            ingHdr.color = RecipeSettings.TextColor;

            if (sp.RequiredCards != null && sp.RequiredCards.Length > 0)
            {
                var groups = sp.RequiredCards
                    .Where(c => !string.IsNullOrEmpty(c))
                    .GroupBy(c => c)
                    .Select(g => (id: g.Key, cnt: g.Count()))
                    .ToList();

                foreach (var (id, cnt) in groups)
                {
                    var row = HRow(_content);
                    row.AddComponent<LayoutElement>().preferredHeight = 32;

                    if (RecipeSettings.ShowIcons)
                    {
                        try { CardIconGo(row.transform, id, 26f); } catch (Exception ex) { if (L != null) L.Log("CardIconGo ing error: " + ex); }
                    }

                    string label = cnt > 1 ? $"  {RecipeCache.GetName(id)}  ×{cnt}" : $"  {RecipeCache.GetName(id)}";
                    var t = MkTmp(row.transform, label, RecipeSettings.F_Ingredient);
                    t.color = RecipeSettings.TextColor;
                    t.overflowMode = TextOverflowModes.Ellipsis;
                    t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                }
            }
            else
            {
                MkTmp(_content, "  Ресурсы не нужны", RecipeSettings.F_Ingredient).color = RecipeSettings.SubTextColor;
            }

            if (RecipeSettings.ShowResultRow && !string.IsNullOrEmpty(sp.ResultCard))
            {
                var resRow = HRow(_content);
                resRow.AddComponent<LayoutElement>().preferredHeight = 30;

                var arr = MkTmp(resRow.transform, "→", 16f, FontStyles.Bold);
                arr.color = new Color(0.25f, 0.55f, 0.25f);

                if (RecipeSettings.ShowIcons)
                {
                    try { CardIconGo(resRow.transform, sp.ResultCard, 26f); } catch (Exception ex) { if (L != null) L.Log("CardIconGo result error: " + ex); }
                }

                var rn = MkTmp(resRow.transform, "  " + RecipeCache.GetName(sp.ResultCard),
                    RecipeSettings.F_Ingredient, FontStyles.Bold);
                rn.color = new Color(0.15f, 0.5f, 0.2f);
                rn.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            }

            if (RecipeSettings.ShowTime)
            {
                var timeRow = HRow(_content);
                MkTmp(timeRow.transform, $"Время: {sp.Time:0} сек", RecipeSettings.F_Header).color = RecipeSettings.SubTextColor;
            }
        }

        private void ForceLayout()
        {
            if (_panel != null && _panel.gameObject.activeSelf)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
        }

        // ── Settings overlay ──────────────────────────────────────────────────────

        private void OpenSettings()
        {
            if (_settingsOpen) return;
            _settingsOpen = true;

            if (WorldManager.instance != null) { _savedSpeedUp = WorldManager.instance.SpeedUp; WorldManager.instance.SpeedUp = 0f; }

            Canvas canvas = GameCanvas.instance?.Canvas ?? FindObjectOfType<Canvas>();
            if (canvas == null) return;

            _modalBlocker = new GameObject("RI_Blocker");
            _modalBlocker.transform.SetParent(canvas.transform, false);
            var br = _modalBlocker.AddComponent<RectTransform>();
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.sizeDelta = Vector2.zero;
            var bi = _modalBlocker.AddComponent<Image>();
            bi.color = new Color(0, 0, 0, 0.25f);
            _modalBlocker.AddComponent<EventTrigger>();
            _modalBlocker.transform.SetAsLastSibling();

            if (_panel != null)
                _panel.transform.SetSiblingIndex(_modalBlocker.transform.GetSiblingIndex() - 1);

            _settingsOverlay = new GameObject("RI_Settings");
            _settingsOverlay.transform.SetParent(canvas.transform, false);
            var or = _settingsOverlay.AddComponent<RectTransform>();
            or.anchorMin = new Vector2(0.5f, 0.5f); or.anchorMax = new Vector2(0.5f, 0.5f);
            or.pivot = new Vector2(0.5f, 0.5f); or.anchoredPosition = Vector2.zero;
            or.sizeDelta = new Vector2(400f, 0f);

            _settingsOverlay.AddComponent<Image>().color = RecipeSettings.BgColor;
            var so = _settingsOverlay.AddComponent<Outline>();
            so.effectColor = RecipeSettings.OutlineColor; so.effectDistance = new Vector2(3, -3);
            var svl = _settingsOverlay.AddComponent<VerticalLayoutGroup>();
            svl.padding = new RectOffset(14, 14, 12, 14); svl.spacing = 9;
            svl.childForceExpandWidth = true; svl.childForceExpandHeight = false;
            svl.childControlWidth = true; svl.childControlHeight = true;
            _settingsOverlay.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _settingsOverlay.transform.SetAsLastSibling();

            var st = MkTmp(_settingsOverlay.transform, "Настройки — Рецепты", 16f, FontStyles.Bold);
            st.color = RecipeSettings.TextColor;

            Divider(_settingsOverlay.transform);

            SRow(_settingsOverlay.transform, "Показывать иконки карт", RecipeSettings.ShowIcons, v => RecipeSettings.ShowIcons = v);
            SRow(_settingsOverlay.transform, "Только открытые рецепты", RecipeSettings.OnlyFound, v => RecipeSettings.OnlyFound = v);
            SRow(_settingsOverlay.transform, "Показывать строку результата", RecipeSettings.ShowResultRow, v => RecipeSettings.ShowResultRow = v);
            SRow(_settingsOverlay.transform, "Тёмная тема", RecipeSettings.DarkTheme, v => RecipeSettings.DarkTheme = v);
            SRow(_settingsOverlay.transform, "Авто-скрытие (3 сек без мыши)", RecipeSettings.AutoHide, v => RecipeSettings.AutoHide = v);
            SRow(_settingsOverlay.transform, "Не закрывать вкладку при крафте", RecipeSettings.KeepOnCraft, v => RecipeSettings.KeepOnCraft = v);
            SRow(_settingsOverlay.transform, "Показывать время крафта", RecipeSettings.ShowTime, v => RecipeSettings.ShowTime = v);
            SRow(_settingsOverlay.transform, "Дедупликация вариантов", RecipeSettings.DedupVariants, v => RecipeSettings.DedupVariants = v);

            if (RecipeSettings.AutoHide)
            {
                MkTmp(_settingsOverlay.transform, "Задержка авто-скрытия (сек):", 13f).color = RecipeSettings.SubTextColor;
                var adRow = HRow(_settingsOverlay.transform);
                int[] adOpts = { 1, 3, 5 };
                foreach (int ad in adOpts)
                {
                    int adC = ad; bool sel = RecipeSettings.AutoHideDelay == ad;
                    GameBtn(adRow.transform, ad + "с", 60, 30,
                        () => { RecipeSettings.AutoHideDelay = adC; ReopenSettings(); },
                        sel ? ColorManager.instance.HoverButtonColor : ColorManager.instance.ButtonColor);
                }
            }

            MkTmp(_settingsOverlay.transform, "Размер шрифта:", 13f).color = RecipeSettings.SubTextColor;
            var fsRow = HRow(_settingsOverlay.transform);
            string[] fsLbl = { "Малый", "Нормальный", "Крупный" };
            for (int i = 0; i < 3; i++)
            {
                int fi = i; bool sel = RecipeSettings.FontSizeIdx == i;
                GameBtn(fsRow.transform, fsLbl[i], -1, 30,
                    () => { RecipeSettings.FontSizeIdx = fi; ReopenSettings(); },
                    sel ? ColorManager.instance.HoverButtonColor : ColorManager.instance.ButtonColor, 1f);
            }

            MkTmp(_settingsOverlay.transform, "Прозрачность:", 13f).color = RecipeSettings.SubTextColor;
            var opRow = HRow(_settingsOverlay.transform);
            string[] opLbl = { "70%", "88%", "100%" };
            for (int i = 0; i < 3; i++)
            {
                int oi = i; bool sel = RecipeSettings.OpacityIdx == i;
                GameBtn(opRow.transform, opLbl[i], -1, 30,
                    () => { RecipeSettings.OpacityIdx = oi; ReopenSettings(); },
                    sel ? ColorManager.instance.HoverButtonColor : ColorManager.instance.ButtonColor, 1f);
            }

            MkTmp(_settingsOverlay.transform, "Макс. вариантов рецепта:", 13f).color = RecipeSettings.SubTextColor;
            var mvRow = HRow(_settingsOverlay.transform);
            int[] mvOpts = { 10, 20, 50 };
            foreach (int mv in mvOpts)
            {
                int mvC = mv; bool sel = RecipeSettings.MaxVariants == mv;
                GameBtn(mvRow.transform, mv.ToString(), 75, 30,
                    () => { RecipeSettings.MaxVariants = mvC; ReopenSettings(); },
                    sel ? ColorManager.instance.HoverButtonColor : ColorManager.instance.ButtonColor);
            }

            MkTmp(_settingsOverlay.transform, "Положение панели:", 13f).color = RecipeSettings.SubTextColor;
            var posRow = HRow(_settingsOverlay.transform);
            GameBtn(posRow.transform, "Сбросить положение", 160, 30,
                () => { if (_panel != null) { _panel.anchoredPosition = new Vector2(-8f, 0f); RecipeSettings.SavedPos = _panel.anchoredPosition; } });

            Divider(_settingsOverlay.transform);

            var btnRow = HRow(_settingsOverlay.transform);
            GameBtn(btnRow.transform, "По умолчанию", 130, 32, () => { RecipeSettings.ResetDefaults(); ReopenSettings(); });
            var sp = new GameObject("Sp").AddComponent<LayoutElement>();
            sp.transform.SetParent(btnRow.transform, false); sp.flexibleWidth = 1;
            GameBtn(btnRow.transform, "ESC / Отмена", 120, 32, () => CloseSettings(false));
            GameBtn(btnRow.transform, "Сохранить", 100, 32, () => CloseSettings(true));

            LayoutRebuilder.ForceRebuildLayoutImmediate(_settingsOverlay.GetComponent<RectTransform>());

            _lastDarkTheme  = RecipeSettings.DarkTheme;
            _lastOpacityIdx = RecipeSettings.OpacityIdx;
        }

        private void ReopenSettings() { CloseSettings(false); OpenSettings(); }

        private void CloseSettings(bool save)
        {
            if (!_settingsOpen) return;
            _settingsOpen = false;
            if (save) RecipeSettings.Save();

            bool themeChanged = RecipeSettings.DarkTheme  != _lastDarkTheme ||
                                RecipeSettings.OpacityIdx != _lastOpacityIdx;

            if (WorldManager.instance != null) WorldManager.instance.SpeedUp = _savedSpeedUp;
            if (_settingsOverlay != null) { Destroy(_settingsOverlay); _settingsOverlay = null; }
            if (_modalBlocker    != null) { Destroy(_modalBlocker);    _modalBlocker    = null; }

            if (themeChanged)
                RebuildPanelUI();
            else
                Refresh();
        }

        private void RebuildPanelUI()
        {
            if (_panel != null) { Destroy(_panel.gameObject); _panel = null; }
            _uiBuilt = false;
            _settingsOverlay = null;
            _modalBlocker = null;
            EnsureUI();
            Refresh();
        }

        private void SRow(Transform parent, string label, bool current, Action<bool> onChange)
        {
            var row = HRow(parent);
            var lbl = MkTmp(row.transform, label, 13f);
            lbl.color = RecipeSettings.TextColor;
            lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            GameBtn(row.transform, current ? "ВКЛ" : "ВЫКЛ", 65, 26,
                () => { onChange(!current); ReopenSettings(); },
                current ? ColorManager.instance.HoverButtonColor : ColorManager.instance.ButtonColor);
        }

        // ── Collapse ──────────────────────────────────────────────────────────────

        private void ToggleCollapse() { _collapsed = !_collapsed; Refresh(); }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string ResolveDisplayName(Blueprint bp, string bpId, string resultId)
        {
            if (!string.IsNullOrEmpty(resultId))
            {
                string n = RecipeCache.GetName(resultId);
                if (!n.Equals(resultId) && !n.Equals(resultId.Replace("_", " "))) return n;
            }
            if (bp != null)
            {
                try
                {
                    string loc = SokLoc.Translate(bp.NameTerm);
                    if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("---")) return loc;
                }
                catch { }
                string bn = RecipeCache.GetName(bp.CardId);
                if (!bn.Equals(bp.CardId) && !bn.Equals(bp.CardId.Replace("_", " "))) return bn;
            }
            string raw = !string.IsNullOrEmpty(resultId) ? resultId : bpId;
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(raw.Replace("_", " "));
        }

        private Blueprint GetBp(string id) =>
            WorldManager.instance?.GameDataLoader?.BlueprintPrefabs?.FirstOrDefault(b => b?.CardId == id);

        // ── UI factory helpers ────────────────────────────────────────────────────

        private static GameObject HRow(Transform parent)
        {
            var go = new GameObject("HR"); go.transform.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 5; h.childForceExpandHeight = false; h.childForceExpandWidth = false;
            h.childControlHeight = true; h.childControlWidth = true;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        private static void Divider(Transform parent)
        {
            var go = new GameObject("Div"); go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = RecipeSettings.DividerColor;
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 1; le.flexibleWidth = 1;
        }

        private static TextMeshProUGUI MkTmp(Transform parent, string text, float size,
            FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("T"); go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style;
            t.color = RecipeSettings.TextColor;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableWordWrapping = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return t;
        }

        private static CustomButton GameBtn(Transform parent, string label,
            float w, float h, Action onClick,
            Color? bg = null, float flexW = 0f)
        {
            GameObject btnObj;
            if (GameScreen.instance?.IdeasButton != null)
                btnObj = Instantiate(GameScreen.instance.IdeasButton.gameObject);
            else
            {
                btnObj = new GameObject("Btn");
                btnObj.AddComponent<Image>().color = bg ?? ColorManager.instance?.ButtonColor ?? new Color(0.85f, 0.80f, 0.70f);
                var cb = btnObj.AddComponent<CustomButton>();
                var tGo = new GameObject("T"); tGo.transform.SetParent(btnObj.transform, false);
                var tRt = tGo.AddComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.sizeDelta = Vector2.zero;
                var tTmp = tGo.AddComponent<TextMeshProUGUI>();
                tTmp.alignment = TextAlignmentOptions.Center;
                tTmp.enableWordWrapping = false;
                var field = typeof(CustomButton).GetField("textMeshPro",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(cb, tTmp);
            }

            foreach (var comp in btnObj.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                string tn = comp.GetType().Name;
                if (tn == "SokTermText" || tn.Contains("LocalizeTerm"))
                    Destroy(comp);
            }

            btnObj.transform.SetParent(parent, false);
            var le = btnObj.GetComponent<LayoutElement>() ?? btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            if (w > 0)   le.preferredWidth = w;
            if (flexW > 0) le.flexibleWidth = flexW;

            if (bg.HasValue)
            {
                var img = btnObj.GetComponent<Image>();
                if (img != null) img.color = bg.Value;
            }

            var btn = btnObj.GetComponent<CustomButton>();
            if (btn?.TextMeshPro != null)
            {
                btn.TextMeshPro.text = label;
                btn.TextMeshPro.fontSize = RecipeSettings.F_Small;
                btn.TextMeshPro.overflowMode = TextOverflowModes.Ellipsis;
            }
            if (onClick != null) btn.Clicked += delegate { onClick(); };
            return btn;
        }

        private static void TitleBtn(Transform parent, string label, Action onClick)
        {
            GameBtn(parent, label, 28, 26, onClick);
        }

        private static void CardIconGo(Transform parent, string cardId, float size)
        {
            var go = new GameObject("CI_" + cardId);
            go.transform.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.minWidth = size; le.minHeight = size;
            le.preferredWidth = size; le.preferredHeight = size;
            le.flexibleWidth = 0; le.flexibleHeight = 0;

            if (string.IsNullOrEmpty(cardId)) return;
            Sprite s = RecipeCache.GetIcon(cardId);
            if (s == null) return;

            var img = go.AddComponent<Image>();
            img.sprite = s;
            img.preserveAspect = true;
            img.type = Image.Type.Simple;
        }

        public void SetVisible(bool v)
        {
            _visible = v;
            if (_panel != null)
            {
                _panel.gameObject.SetActive(v);
                if (v && !_settingsOpen) _panel.SetAsLastSibling();
            }
            _autoHideTimer = 0f;
        }

        public static void OnBlueprintCompleted(string bpId)
        {
            if (Instance == null) return;
            for (int i = Instance._slots.Count - 1; i >= 0; i--)
            {
                if (Instance._slots[i].BpId == bpId)
                {
                    Instance.CloseSlot(i);
                    return;
                }
            }
        }
    }

    // ── Drag handler ──────────────────────────────────────────────────────────────

    public class PanelDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform Panel;
        private Vector2 _offset;

        public void OnBeginDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Panel.parent as RectTransform, e.position, e.pressEventCamera, out var local);
            _offset = Panel.anchoredPosition - local;
        }

        public void OnDrag(PointerEventData e)
        {
            if (Panel == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Panel.parent as RectTransform, e.position, e.pressEventCamera, out var local);
            Panel.anchoredPosition = local + _offset;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (Panel != null) RecipeSettings.SavedPos = Panel.anchoredPosition;
        }
    }
}
