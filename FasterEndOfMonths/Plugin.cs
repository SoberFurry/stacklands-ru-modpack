using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace FasterEndOfMonths
{
    public class Plugin : Mod
    {
        static ModLogger L;
        static ConfigEntry<int> autosaveFrequency;
        static ConfigEntry<bool> disableDebugAutosave;

        private ConfigEntry<T> CreateConfig<T>(string name, T defaultValue, string description)
        {
            return Config.GetEntry<T>(name, defaultValue, new ConfigUI { Tooltip = description });
        }

        private void Awake()
        {
            L = Logger;
            autosaveFrequency = CreateConfig("AutosaveFrequency", 1,
                "How often to save at the end of the moon (every x moons). Set to a large number to never save automatically.");
            disableDebugAutosave = CreateConfig("DisableDebugAutosave", true,
                "Disabling versioned backup saves reduces lag-spike at the end of each moon.");
            Harmony.PatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EndOfMonthCutscenes), "FeedVillagers")]
        public static void FeedVillagersPatch(out IEnumerator __result, out bool __runOriginal)
        {
            __runOriginal = false;
            __result = FeedVillagers();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldManager), "EndOfMonthRoutine")]
        public static void EndOfMonthRoutinePrefix(ref EndOfMonthParameters param)
        {
            param.SkipEndConfirmation = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldManager), "EndOfMonth")]
        public static void RememberSpeedUp(WorldManager __instance, out float __state)
        {
            __state = __instance.SpeedUp;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldManager), "EndOfMonth")]
        public static void RestoreSpeedUp(WorldManager __instance, float __state)
        {
            __instance.SpeedUp = __state;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DebugScreen), "AutoSave")]
        public static void DisableDebugAutoSave(out bool __runOriginal)
        {
            __runOriginal = !disableDebugAutosave.Value;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EndOfMonthCutscenes), "SpecialEvents", MethodType.Enumerator)]
        public static IEnumerable<CodeInstruction> NoWaitForSecondsInSpecialEvents(
            IEnumerable<CodeInstruction> instructions)
        {
            var ctor = typeof(WaitForSeconds).GetConstructor(new[] { typeof(float) });
            return new CodeMatcher(instructions)
                .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4), new CodeMatch(OpCodes.Newobj, ctor))
                .Repeat(m => m.SetOperandAndAdvance(0f))
                .InstructionEnumeration();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(WorldManager), "EndOfMonthRoutine", MethodType.Enumerator)]
        public static IEnumerable<CodeInstruction> NoWaitForSecondsInEndOfMonth(
            IEnumerable<CodeInstruction> instructions)
        {
            var ctor = typeof(WaitForSeconds).GetConstructor(new[] { typeof(float) });
            var matcher = new CodeMatcher(instructions)
                .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4), new CodeMatch(OpCodes.Newobj, ctor));
            if (matcher.IsValid)
                matcher.SetOperandAndAdvance(0f);
            else
                L.LogWarning("Didn't find WaitForSeconds in WorldManager.EndOfMonthRoutine");
            return matcher.InstructionEnumeration();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(WorldManager), "EndOfMonthRoutine", MethodType.Enumerator)]
        public static IEnumerable<CodeInstruction> ReduceAutosaves(
            IEnumerable<CodeInstruction> instructions)
        {
            var matcher = new CodeMatcher(instructions).MatchForward(false,
                new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(SaveManager), "instance")),
                new CodeMatch(OpCodes.Ldc_I4_1),
                new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(SaveManager), "Save", new[] { typeof(bool) })));
            if (matcher.IsValid)
            {
                matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(Plugin), nameof(EndOfMonthAutosave))));
                matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Nop));
                matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Nop));
            }
            else
                L.LogWarning("Didn't find SaveManager.Save in WorldManager.EndOfMonthRoutine");
            return matcher.InstructionEnumeration();
        }

        public static void EndOfMonthAutosave()
        {
            if (WorldManager.instance.CurrentMonth % autosaveFrequency.Value == 0)
                SaveManager.instance.Save(true);
        }

        // Reflection wrappers for private EndOfMonthCutscenes methods
        static readonly MethodInfo _getFoodToUseUp    = AccessTools.Method(typeof(EndOfMonthCutscenes), "GetFoodToUseUp");
        static readonly MethodInfo _tryCreatePoop     = AccessTools.Method(typeof(EndOfMonthCutscenes), "TryCreatePoop");
        static readonly MethodInfo _setStarvingStatus = AccessTools.Method(typeof(EndOfMonthCutscenes), "SetStarvingHumanStatus");

        static Food GetFoodToUseUp() => (Food)_getFoodToUseUp?.Invoke(null, null);
        static void TryCreatePoop(CardData cd) => _tryCreatePoop?.Invoke(null, new object[] { cd });
        static void SetStarvingHumanStatus(int n) => _setStarvingStatus?.Invoke(null, new object[] { n });

        public static IEnumerator FeedVillagers()
        {
            AudioManager.me.PlaySound2D(AudioManager.me.Eat, UnityEngine.Random.Range(0.8f, 1.2f), 0.3f);

            int requiredFoodCount = WorldManager.instance.GetRequiredFoodCount();
            var cardsToFeed = EndOfMonthCutscenes.GetCardsToFeed();
            var fedCards = new List<CardData>();

            for (int i = 0; i < cardsToFeed.Count; i++)
            {
                CardData cardToFeed = cardsToFeed[i];
                if (cardToFeed is BaseVillager bv) bv.AteUncookedFood = false;

                int foodForVillager = WorldManager.instance.GetCardRequiredFoodCount(cardToFeed.MyGameCard);
                for (int j = 0; j < foodForVillager; j++)
                {
                    Food food = GetFoodToUseUp();
                    if (food == null) break;

                    GameCard foodCard = food.MyGameCard;
                    food.FoodValue--;
                    requiredFoodCount--;

                    if (cardToFeed is BaseVillager bv2)
                    {
                        bv2.HealthPoints = Mathf.Min(bv2.HealthPoints + 3, bv2.ProcessedCombatStats.MaxHealth);
                        food.ConsumedBy(bv2);
                        TryCreatePoop(bv2);
                        if (!food.IsCookedFood) bv2.AteUncookedFood = true;
                    }

                    if (food.FoodValue <= 0 && food.Id != "compactstorage.food_warehouse" && food is not Hotpot)
                    {
                        var originalStack = foodCard.GetAllCardsInStack();
                        foodCard.RemoveFromStack();
                        food.FullyConsumed(cardToFeed);
                        originalStack.Remove(foodCard);
                        WorldManager.instance.Restack(originalStack);
                        foodCard.DestroyCard(true, true);
                    }

                    if (j == foodForVillager - 1) fedCards.Add(cardToFeed);
                }
            }

            if (requiredFoodCount > 0)
            {
                var unfedVillagers = new List<CardData>();
                foreach (CardData cd in cardsToFeed)
                    if (!fedCards.Contains(cd) && cd is not Kid)
                        unfedVillagers.Add(cd);

                int humansToDie = unfedVillagers.Count;
                SetStarvingHumanStatus(humansToDie);
                yield return Cutscenes.WaitForContinueClicked(SokLoc.Translate("label_uh_oh"));

                for (int i = 0; i < unfedVillagers.Count; i++)
                {
                    CardData cd2 = unfedVillagers[i];
                    if (cd2 is not Kid)
                    {
                        // FIX: added 4th parameter resetTargetOnDeath=true (new in current game version)
                        yield return WorldManager.instance.KillVillagerCoroutine(
                            cd2 as Villager, null, null, true);
                        SetStarvingHumanStatus(humansToDie - i);
                    }
                }

                if (WorldManager.instance.CheckAllVillagersDead())
                {
                    WorldManager.instance.VillagersStarvedAtEndOfMoon = true;
                    string boardId = WorldManager.instance.CurrentBoard.Id;
                    if (boardId == "main")
                    {
                        EndOfMonthCutscenes.CutsceneText = SokLoc.Translate("label_everyone_starved");
                        yield return Cutscenes.WaitForContinueClicked(SokLoc.Translate("label_game_over"));
                        GameCanvas.instance.SetScreen<GameOverScreen>();
                        WorldManager.instance.currentAnimationRoutine = null;
                    }
                    else if (boardId == "island")
                        yield return Cutscenes.EveryoneOnIslandDead();
                    else if (boardId == "forest")
                        yield return Cutscenes.EveryoneInForestDead();
                    else if (WorldManager.instance.CurrentBoard.BoardOptions.IsSpiritWorld)
                        yield return Cutscenes.EveryoneInSpiritWorldDead(boardId);
                    else
                        yield return Cutscenes.EveryoneOnIslandDead();
                }
            }
        }
    }
}


