# CHANGELOG

## v1.5.0 (current)
### RecipeSettings (expanded)
- Dark theme support (BgColor, TitleBgColor, TextColor etc from settings)
- Opacity control (3 levels: 70% / 86% / 99%)
- DedupVariants toggle
- ShowResultRow toggle
- ShowTime toggle
- AutoHide + AutoHideDelay settings
- KeepOnCraft toggle

### RecipePanel
- Settings overlay with game pause (WorldManager.SpeedUp = 0)
- Settings: Save + Reset to defaults buttons
- Conditional pin button (only shown if BetterSideBar mod loaded)
- AspectRatioFitter on card icons (fixes stretched/crooked display)
- ModLogger parameter added to EnsureCreated

### RecipeCache
- Special ID table: any_villager, any_worker, stone, fish, cotton etc
- ResolveDisplayName: 4-level fallback (resultId -> bpId -> SokLoc.Translate -> id.Replace)
- GetName last resort: readable ID (underscores to spaces)

## v1.4.0
### RecipePanel
- Settings button (opens overlay, pauses game)
- Drag-to-move header (PanelDragHandler component)
- Collapse/expand toggle
- R key from panel: hover tab + R = close slot
- Tab tooltip: GameScreen.InfoBoxTitle shows full name + R hint
- Conditional pin button (checks ModManager.LoadedMods for better_sidebar)
- Adaptive panel width: 22% of canvas width (min 260, max 420)
- Restored saved position from PlayerPrefs on panel creation

## v1.3.0
### Icons
- New 40x40px bold/solid PNG icons (replaced 24px)
- icon-all, icon-new, icon-reset, icon-make, icon-usedin, icon-open, icon-pin, icon-quick
- Generated via System.Drawing with AntiAlias + SmoothingMode

### RecipePanel
- Panel anchor: right edge, vertically centered (anchorMin/Max = 0.5)
- Panel width: adaptive (22% canvas width)
- Icon display size: 28px in buttons (was 16px)

## v1.2.0
### RecipePanel (rewrite)
- Multi-tab system: up to 6 open recipe tabs
- R toggle: R on already-open blueprint = remove tab
- Subprint navigation: prev/next buttons, shows "N/M"
- MaxVariants cap with user warning
- Collapse/expand button
- Drag-to-move (PanelDragHandler saves position to PlayerPrefs)
- R from panel: hover tab + R = close

### RecipeCache
- Special wildcard IDs: any_villager -> readable name
- ResolveDisplayName helper with 4 fallback levels

### RecipeSettings
- PlayerPrefs-based (platform-independent)
- Font size, max variants, show icons, only found

## v1.1.0
- Trigger changed: Ctrl+click on world card -> R key on IdeaElement in sidebar
- Fixed NullReferenceException: cache builds on InitIdeaElements (SokLoc ready)
- Language change detection: cache rebuilds when language switches

## v1.0.0
- Initial release
- RecipeCache: blueprints + cards indexed on InitIdeaElements
- RecipePanel: right-anchored panel, single slot
- Trigger: Ctrl+click on any card in world
- Shows: ingredients, result card, craft time
- Basic localization support (SokLoc + Russian TSV fallback)
