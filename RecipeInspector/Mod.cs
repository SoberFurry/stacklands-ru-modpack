using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RecipeInspectorNS
{
    public class RecipeInspectorMod : Mod
    {
        private static ModLogger L;
        private static string _modPath;
        private static bool _patchError;

        private void Awake()
        {
            try
            {
                L = Logger;
                RecipeCache.SetLogger(Logger);
                _modPath = this.Path ?? "";

                IconLoader.Init(_modPath, Logger);

                new Harmony("recipe_inspector").PatchAll();

                Logger.Log("RecipeInspector v1.6.0 loaded. ModPath: " + _modPath);
            }
            catch (Exception ex)
            {
                Logger.Log("RecipeInspector load error: " + ex.Message);
            }
        }

        public override void Ready() { }

        // ── Build cache AFTER SokLoc + WorldManager are both ready ──────────────
        [HarmonyPatch(typeof(GameScreen), "InitIdeaElements")]
        public class InitIdeaElementsPatch
        {
            public static void Postfix()
            {
                try
                {
                    RecipeCache.Build();
                    RecipePanel.EnsureCreated(_modPath, L);
                    RecipePanel.Init(); // Build UI immediately when game screen is ready
                }
                catch (Exception ex)
                {
                    if (L != null) L.Log("InitIdeaElements patch error: " + ex.Message);
                }
            }
        }

        // ── Invalidate cache when world reloads ─────────────────────────────────
        [HarmonyPatch(typeof(WorldManager), "Awake")]
        public class WorldAwakePatch
        {
            public static void Postfix() { RecipeCache.InvalidateCache(); }
        }

        // ── Rebuild cache on language change ────────────────────────────────────
        [HarmonyPatch(typeof(GameScreen), "UpdateIdeasLog")]
        public class LangChangePatch
        {
            private static string _lastLang;
            public static void Prefix()
            {
                try
                {
                    string lang = SokLoc.instance?.CurrentLanguage;
                    if (lang != null && lang != _lastLang)
                    {
                        _lastLang = lang;
                        RecipeCache.Build();
                    }
                }
                catch { }
            }
        }

        // ── Configurable key while hovering IdeaElement → toggle recipe tab ─────
        [HarmonyPatch(typeof(IdeaElement), "Update")]
        public class IdeaElementUpdatePatch
        {
            public static void Postfix(IdeaElement __instance)
            {
                try
                {
                    if (Keyboard.current == null) return;
                    if (!__instance.MyButton.IsHovered && !__instance.MyButton.IsSelected) return;

                    if (!IsKeyDown(RecipeSettings.KeyOpen)) return;

                    RecipePanel.EnsureCreated(_modPath, L);
                    if (!RecipeCache.IsReady) RecipeCache.Build();

                    Blueprint bp = __instance.MyKnowledge as Blueprint;
                    if (bp == null) return;

                    string resultId = bp.Subprints?.Count > 0
                        ? bp.Subprints[0]?.ResultCard ?? bp.CardId
                        : bp.CardId;

                    RecipePanel.ToggleBlueprint(bp.CardId, resultId);
                }
                catch (Exception ex)
                {
                    if (!_patchError)
                    {
                        _patchError = true;
                        if (L != null) L.Log("IdeaElementUpdatePatch error (once): " + ex.Message);
                    }
                }
            }
        }

        // ── KeepOnCraft: remove tab when blueprint completes (unless locked) ─────
        [HarmonyPatch(typeof(Blueprint), "CardCompleted")]
        public class BlueprintCompletedPatch
        {
            public static void Postfix(Blueprint __instance)
            {
                try
                {
                    if (!RecipeSettings.KeepOnCraft)
                        RecipePanel.OnBlueprintCompleted(__instance.CardId);
                }
                catch { }
            }
        }

        // ── Helper: check configurable key pressed this frame ───────────────────
        public static bool IsKeyDown(KeyCode kc)
        {
            try
            {
                if (Keyboard.current == null) return false;
                string name = kc.ToString().ToLowerInvariant();
                var key = Keyboard.current.FindKeyOnCurrentKeyboardLayout(name);
                return key != null && key.wasPressedThisFrame;
            }
            catch { return false; }
        }
    }
}
