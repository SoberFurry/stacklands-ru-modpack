using UnityEngine;

namespace RecipeInspectorNS
{
    public static class RecipeSettings
    {
        // Keys
        private const string K_ICONS     = "RI_ShowIcons";
        private const string K_FONT      = "RI_FontSize";
        private const string K_MAXVAR    = "RI_MaxVariants";
        private const string K_FOUND     = "RI_OnlyFound";
        private const string K_DARK      = "RI_DarkTheme";
        private const string K_ALPHA     = "RI_Opacity";
        private const string K_AUTOHIDE  = "RI_AutoHide";
        private const string K_KEEPCRAFT = "RI_KeepOnCraft";
        private const string K_SHOWRES   = "RI_ShowResult";
        private const string K_SHOWTIME  = "RI_ShowTime";
        private const string K_DEDUP     = "RI_DedupVariants";
        private const string K_AUTODELAY = "RI_AutoHideDelay";
        private const string K_POSX      = "RI_PosX";
        private const string K_POSY      = "RI_PosY";

        // Keybinding keys
        private const string K_KEY_OPEN  = "RI_Key_Open";
        private const string K_KEY_PIN   = "RI_Key_Pin";
        private const string K_KEY_HIDE  = "RI_Key_Hide";

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

        // Горячие клавиши (хранятся как KeyCode int)
        public static KeyCode KeyOpen
        {
            get => (KeyCode)PlayerPrefs.GetInt(K_KEY_OPEN, (int)KeyCode.R);
            set => PlayerPrefs.SetInt(K_KEY_OPEN, (int)value);
        }

        public static KeyCode KeyPin
        {
            get => (KeyCode)PlayerPrefs.GetInt(K_KEY_PIN, (int)KeyCode.P);
            set => PlayerPrefs.SetInt(K_KEY_PIN, (int)value);
        }

        public static KeyCode KeyHide
        {
            get => (KeyCode)PlayerPrefs.GetInt(K_KEY_HIDE, (int)KeyCode.H);
            set => PlayerPrefs.SetInt(K_KEY_HIDE, (int)value);
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

        // Цвет иконок — автотинтинг под тему
        public static Color IconColor => TextColor;

        // Цвет иконки для кнопки «активна» (зелёный акцент)
        public static Color IconAccentColor => DarkTheme
            ? new Color(0.45f, 0.75f, 0.45f)
            : new Color(0.15f, 0.50f, 0.20f);

        // Цвет кнопок
        public static Color BtnColor => DarkTheme
            ? new Color(0.28f, 0.24f, 0.20f)
            : ColorManager.instance != null ? ColorManager.instance.ButtonColor : new Color(0.85f, 0.80f, 0.70f);

        public static Color BtnActiveColor => DarkTheme
            ? new Color(0.35f, 0.55f, 0.35f)
            : ColorManager.instance != null ? ColorManager.instance.HoverButtonColor : new Color(0.65f, 0.80f, 0.60f);

        public static Color BtnTextColor => DarkTheme
            ? new Color(0.92f, 0.88f, 0.82f)
            : ColorManager.instance != null ? ColorManager.instance.ButtonTextColor : new Color(0.12f, 0.08f, 0.04f);

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
            ShowIcons     = true;
            FontSizeIdx   = 1;
            MaxVariants   = 20;
            OnlyFound     = false;
            DarkTheme     = false;
            OpacityIdx    = 2;
            AutoHide      = false;
            KeepOnCraft   = false;
            ShowResultRow = true;
            ShowTime      = true;
            DedupVariants = true;
            AutoHideDelay = 3;
            SavedPos      = new Vector2(-8f, 0f);
            KeyOpen       = KeyCode.R;
            KeyPin        = KeyCode.P;
            KeyHide       = KeyCode.H;
            Save();
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
