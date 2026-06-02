using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RecipeInspectorNS
{
    /// <summary>
    /// Loads PNG icons from the mod's Icons/ folder.
    /// Usage: IconLoader.Get("cog") → Sprite (null if not found)
    /// Tinting is done at the Image level using RecipeSettings.IconColor.
    /// </summary>
    public static class IconLoader
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private static string _iconsPath;
        private static ModLogger _log;

        public static void Init(string modPath, ModLogger log)
        {
            _log = log;
            _iconsPath = Path.Combine(modPath, "Icons");
        }

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_cache.TryGetValue(name, out Sprite cached)) return cached;

            string path = Path.Combine(_iconsPath, "icon-" + name + ".png");
            if (!File.Exists(path))
            {
                if (_log != null) _log.Log("IconLoader: not found: " + path);
                _cache[name] = null;
                return null;
            }

            try
            {
                byte[] data = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                ImageConversion.LoadImage(tex, data);
                var sprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
                sprite.name = name;
                _cache[name] = sprite;
                return sprite;
            }
            catch (Exception ex)
            {
                if (_log != null) _log.Log("IconLoader error " + name + ": " + ex.Message);
                _cache[name] = null;
                return null;
            }
        }

        public static void Clear() => _cache.Clear();
    }
}
