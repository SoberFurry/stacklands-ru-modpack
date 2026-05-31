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

                // this.Path is set by the mod loader — works on any PC
                _modPath = this.Path ?? "";

                Harmony harmony = new Harmony("recipe_inspector");
                foreach (var type in System.Reflection.Assembly.GetExecutingAssembly().GetTypes())
                {
                    try { harmony.CreateClassProcessor(type).Patch(); }
                    catch (Exception ex) { Logger.Log($"Patch skip [{type.Name}]: {ex.Message}"); }
                }

                Logger.Log("RecipeInspector loaded.");
            }
            catch (Exception ex)
            {
                Logger.Log("RecipeInspector load error: " + ex.Message);
            }
        }

        public override void Ready() { }

        // ── Build cache AFTER SokLoc + WorldManager are both ready ─────────────
        [HarmonyPatch(typeof(GameScreen), "InitIdeaElements")]
        public class InitIdeaElementsPatch
        {
            public static void Postfix()
            {
                try
                {
                    RecipeCache.Build();
                    // Init panel now that we have valid data
                    RecipePanel.EnsureCreated(_modPath, L);
                }
                catch (Exception ex)
                {
                    if (L != null) L.Log("InitIdeaElements patch error: " + ex.Message);
                }
            }
        }

        // ── Invalidate on world manager awake (before data is ready) ───────────
        [HarmonyPatch(typeof(WorldManager), "Awake")]
        public class WorldAwakePatch
        {
            public static void Postfix() { RecipeCache.InvalidateCache(); }
        }

        // ── Rebuild on language change ──────────────────────────────────────────
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

        // ── Remove tab on blueprint complete (if KeepOnCraft = false) ─────────────
        [HarmonyPatch(typeof(Blueprint), "CompleteBlueprint")]
        public class BlueprintCompletePatch
        {
            public static void Postfix(Blueprint __instance)
            {
                try
                {
                    if (RecipeSettings.KeepOnCraft) return;
                    if (RecipePanel.Instance == null) return;
                    RecipePanel.OnBlueprintCompleted(__instance.CardId);
                }
                catch { }
            }
        }

        // ── K key while hovering an IdeaElement → show Recipe Inspector ─────────
        // Checks every frame in IdeaElement.Update if K is pressed while hovered.
        [HarmonyPatch(typeof(IdeaElement), "Update")]
        public class IdeaElementUpdatePatch
        {
            public static void Postfix(IdeaElement __instance)
            {
                try
                {
                    if (Keyboard.current == null) return;
                    if (!__instance.MyButton.IsHovered && !__instance.MyButton.IsSelected) return;
                    if (!Keyboard.current.rKey.wasPressedThisFrame) return;

                    RecipePanel.EnsureCreated(_modPath, L);
                    if (!RecipeCache.IsReady) RecipeCache.Build();

                    // Get the result card for this blueprint/rumor
                    Blueprint bp = __instance.MyKnowledge as Blueprint;
                    if (bp != null)
                    {
                        // Show all recipes for the result card of this blueprint
                        string resultId = bp.Subprints?.Count > 0
                            ? bp.Subprints[0].ResultCard
                            : bp.CardId;
                        RecipePanel.ToggleBlueprint(bp.CardId, resultId);
                    }
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
    }
}




