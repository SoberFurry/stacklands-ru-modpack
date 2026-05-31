using UnityEngine;

namespace RecipeInspectorNS
{
    public static class RecipeSettings
    {
        // Keys
        private const string K_ICONS    = "RI_ShowIcons";
        private const string K_FONT     = "RI_FontSize";    // 0=small 1=normal 2=large
        private const string K_MAXVAR   = "RI_MaxVariants";
        private const string K_FOUND    = "RI_OnlyFound";
        private const string K_DARK     = "RI_DarkTheme";
        private const string K_ALPHA    = "RI_Opacity";     // 0=70% 1=88% 2=99%
        private const string K_AUTOHIDE = "RI_AutoHide";
        private const string K_KEEPCRAFT= "RI_KeepOnCraft"; // don't remove tab on craft
        private const string K_SHOWRES  = "RI_ShowResult";  // show result arrow row
        private const string K_SHOWTIME  = "RI_ShowTime";
        private const string K_DEDUP     = "RI_DedupVariants";
        private const string K_AUTODELAY = "RI_AutoHideDelay";  // 1, 3 или 5 секунд
        private const string K_POSX     = "RI_PosX";
        private const string K_POSY     = "RI_PosY";

        // ── Properties ─────────────────────────────────────────────────────────────

        public static bool ShowIcons
        {
            get => PlayerPrefs.GetInt(K_ICONS, 1) == 1;
            set => PlayerPrefs.SetInt(K_ICONS, value ? 1 : 0);
        }

        public static int FontSizeIdx
        {
            get => PlayerPrefs.GetInt(K_FONT, 1);
            set => PlayerPrefs.SetInt(K_FONT, Mathf.Clamp(value, 0, 2));
        }

        public static int MaxVariants
        {
            get => PlayerPrefs.GetInt(K_MAXVAR, 20);
            set => PlayerPrefs.SetInt(K_MAXVAR, Mathf.Clamp(value, 5, 100));
        }

        public static bool OnlyFound
        {
            get => PlayerPrefs.GetInt(K_FOUND, 0) == 1;
            set => PlayerPrefs.SetInt(K_FOUND, value ? 1 : 0);
        }

        public static bool DarkTheme
        {
            get => PlayerPrefs.GetInt(K_DARK, 0) == 1;
            set => PlayerPrefs.SetInt(K_DARK, value ? 1 : 0);
        }

        public static int OpacityIdx
        {
            get => PlayerPrefs.GetInt(K_ALPHA, 2);
            set => PlayerPrefs.SetInt(K_ALPHA, Mathf.Clamp(value, 0, 2));
        }

        public static bool AutoHide
        {
            get => PlayerPrefs.GetInt(K_AUTOHIDE, 0) == 1;
            set => PlayerPrefs.SetInt(K_AUTOHIDE, value ? 1 : 0);
        }

        public static bool KeepOnCraft
        {
            get => PlayerPrefs.GetInt(K_KEEPCRAFT, 0) == 1;
            set => PlayerPrefs.SetInt(K_KEEPCRAFT, value ? 1 : 0);
        }

        public static bool ShowResultRow
        {
            get => PlayerPrefs.GetInt(K_SHOWRES, 1) == 1;
            set => PlayerPrefs.SetInt(K_SHOWRES, value ? 1 : 0);
        }

        public static bool ShowTime
        {
            get => PlayerPrefs.GetInt(K_SHOWTIME, 1) == 1;
            set => PlayerPrefs.SetInt(K_SHOWTIME, value ? 1 : 0);
        }

        public static bool DedupVariants
        {
            get => PlayerPrefs.GetInt(K_DEDUP, 1) == 1;
            set => PlayerPrefs.SetInt(K_DEDUP, value ? 1 : 0);
        }

        public static int AutoHideDelay
        {
            get => PlayerPrefs.GetInt(K_AUTODELAY, 3);
            set => PlayerPrefs.SetInt(K_AUTODELAY, value);
        }

        public static Vector2 SavedPos
        {
            get => new Vector2(PlayerPrefs.GetFloat(K_POSX, -8f), PlayerPrefs.GetFloat(K_POSY, 0f));
            set { PlayerPrefs.SetFloat(K_POSX, value.x); PlayerPrefs.SetFloat(K_POSY, value.y); }
        }

        // ── Derived values ─────────────────────────────────────────────────────────

        public static float Opacity => OpacityIdx == 0 ? 0.68f : OpacityIdx == 1 ? 0.86f : 0.98f;
        public static float FontMult => FontSizeIdx == 0 ? 0.82f : FontSizeIdx == 2 ? 1.22f : 1.0f;

        public static Color BgColor => DarkTheme
            ? new Color(0.14f, 0.12f, 0.10f, Opacity)
            : new Color(0.97f, 0.95f, 0.89f, Opacity);

        public static Color TitleBgColor => DarkTheme
            ? new Color(0.20f, 0.17f, 0.14f, Opacity)
            : new Color(0.88f, 0.84f, 0.74f, Opacity);

        public static Color TextColor => DarkTheme
            ? new Color(0.92f, 0.88f, 0.82f)
            : new Color(0.12f, 0.08f, 0.04f);

        public static Color SubTextColor => DarkTheme
            ? new Color(0.65f, 0.62f, 0.55f)
            : new Color(0.45f, 0.42f, 0.38f);

        public static Color DividerColor => DarkTheme
            ? new Color(0.35f, 0.30f, 0.25f, 0.6f)
            : new Color(0.55f, 0.5f, 0.4f, 0.3f);

        public static Color OutlineColor => DarkTheme
            ? new Color(0.55f, 0.48f, 0.35f, 0.8f)
            : new Color(0.40f, 0.33f, 0.22f, 0.75f);

        // Font sizes
        public static float F_Title      => 18f * FontMult;
        public static float F_SubTitle   => 12f * FontMult;
        public static float F_Ingredient => 15f * FontMult;
        public static float F_Header     => 14f * FontMult;
        public static float F_Small      => 12f * FontMult;
        public static float F_NavBtn     => 14f * FontMult;

        // ── Persistence ────────────────────────────────────────────────────────────

        public static void ResetDefaults()
        {
            ShowIcons   = true;
            FontSizeIdx = 1;
            MaxVariants = 20;
            OnlyFound   = false;
            DarkTheme   = false;
            OpacityIdx  = 2;
            AutoHide    = false;
            KeepOnCraft = false;
            ShowResultRow = true;
            ShowTime      = true;
            DedupVariants = true;
            AutoHideDelay = 3;
            SavedPos    = new Vector2(-8f, 0f);
            Save();
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
