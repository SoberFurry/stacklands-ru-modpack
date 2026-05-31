using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BetterSideBarNS
{
    public class BetterSideBar : Mod
    {
        private void Awake()
        {
            try
            {
                Harmony harmony = new Harmony("better_sidebar");
                harmony.PatchAll();

                // Find mod path for icon loading
                // Path comes from the inherited Mod.Path property
                string modPath = this.Path ?? "";

                // Find Russian TSV path
                string ruTsv = FindRuTsv();

                // Initialize subsystems
                RuSearchIndex.SetLogger(Logger);
                RuSearchIndex.SetTsvPath(ruTsv);

                BlueprintDB.Initialize(Logger, Config);
                PinIdeaMod.Initialize(Logger, Config);
                AdvancedQuickSearchMod.Initialize(Logger, Config);
                SidebarDisplayControl.Initialize(Logger, Config);
                SidebarDisplayControl.LoadIcons(modPath);

                // Subscribe to language changes (if SokLoc is ready)
                try { SokLoc.instance.LanguageChanged += OnLanguageChanged; } catch { }

                Logger.Log("BetterSideBar v1.1.0 loaded.");
            }
            catch (Exception ex)
            {
                Logger.Log("BetterSideBar load error: " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        private static void OnLanguageChanged()
        {
            try { RuSearchIndex.Build(); }
            catch { }
        }

        private static string FindRuTsv()
        {
            // Use ModManager to find Russian TSV on any PC
            try
            {
                if (ModManager.LoadedMods != null)
                    foreach (Mod m in ModManager.LoadedMods)
                    {
                        if (m?.Path == null) continue;
                        string c = System.IO.Path.Combine(m.Path, "localization.tsv");
                        if (File.Exists(c) && HasRu(c)) return c;
                    }
            }
            catch { }

            try
            {
                foreach (string dir in ModManager.GetModPaths())
                {
                    string c = System.IO.Path.Combine(dir, "localization.tsv");
                    if (File.Exists(c) && HasRu(c)) return c;
                }
            }
            catch { }

            return null;
        }

        private static bool HasRu(string p)
        {
            try { return (File.ReadLines(p).FirstOrDefault() ?? "").Contains("Russian"); }
            catch { return false; }
        }

        public override void Ready() { }
    }
}
