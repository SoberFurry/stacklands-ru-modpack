using HarmonyLib;
using System;
using UnityEngine;

namespace BetterSideBarNS
{
    public static class HideUnhoveredCoroutine
    {
        public static IdeaElement hidingUnhoveredIdea;
        public static event Action hidingUnhoveredIdeaCallback;

        public static void InterruptCoroutine()
        {
            hidingUnhoveredIdea = null;
        }

        public static void StartCoroutine(IdeaElement element, Action callback)
        {
            if (hidingUnhoveredIdea != null) return;
            hidingUnhoveredIdea = element;
            hidingUnhoveredIdeaCallback = callback;
        }

        public static void InvokeCallback()
        {
            hidingUnhoveredIdeaCallback?.Invoke();
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
                    InvokeCallback();
                }
            }
        }
    }
}
