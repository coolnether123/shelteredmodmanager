using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ModAPI.Core;
using ModAPI.Util;

namespace ModAPI.UI.ColorPicker
{
    /// <summary>
    /// Persists reusable color picker recent and pinned colors under the ModAPI user root.
    /// </summary>
    public static class ColorPickerPaletteStore
    {
        public static string DefaultPalettePath
        {
            get
            {
                return Path.Combine(Path.Combine(ModApiPaths.UserRoot, "ColorPicker"), "palette.json");
            }
        }

        public static ColorPickerPalette LoadDefault()
        {
            return Load(DefaultPalettePath);
        }

        public static ColorPickerPalette Load(string path)
        {
            ColorPickerPalette palette = new ColorPickerPalette();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return palette;

            try
            {
                ManualJsonObject root;
                string error;
                if (!ManualJson.TryParseObject(File.ReadAllText(path), out root, out error))
                {
                    MMLog.WriteWarning("[ColorPicker] Failed to parse palette: " + error);
                    return palette;
                }

                palette.Load(ReadColors(root.GetArray("recent")), ReadColors(root.GetArray("pinned")));
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ColorPicker] Failed to load palette: " + ex.Message);
            }

            return palette;
        }

        public static bool SaveDefault(ColorPickerPalette palette)
        {
            return Save(DefaultPalettePath, palette);
        }

        public static bool Save(string path, ColorPickerPalette palette)
        {
            if (palette == null || string.IsNullOrEmpty(path))
                return false;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                ManualJsonObject root = new ManualJsonObject();
                root.Set("recent", WriteColors(palette.RecentColors));
                root.Set("pinned", WriteColors(palette.PinnedColors));

                string tempPath = path + ".tmp";
                File.WriteAllText(tempPath, ManualJson.Serialize(root, true));
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tempPath, path);
                palette.MarkClean();
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ColorPicker] Failed to save palette: " + ex.Message);
                return false;
            }
        }

        private static ManualJsonValue WriteColors(ModColor[] colors)
        {
            ManualJsonArray array = new ManualJsonArray();
            if (colors != null)
            {
                for (int i = 0; i < colors.Length; i++)
                    array.Add(WriteColor(colors[i]));
            }

            return ManualJsonValue.Array(array);
        }

        private static ManualJsonValue WriteColor(ModColor color)
        {
            ManualJsonObject obj = new ManualJsonObject();
            obj.Set("r", ManualJsonValue.Number(color.R.ToString("R", CultureInfo.InvariantCulture)));
            obj.Set("g", ManualJsonValue.Number(color.G.ToString("R", CultureInfo.InvariantCulture)));
            obj.Set("b", ManualJsonValue.Number(color.B.ToString("R", CultureInfo.InvariantCulture)));
            obj.Set("a", ManualJsonValue.Number(color.A.ToString("R", CultureInfo.InvariantCulture)));
            return ManualJsonValue.Object(obj);
        }

        private static ModColor[] ReadColors(ManualJsonArray array)
        {
            List<ModColor> colors = new List<ModColor>();
            if (array == null)
                return colors.ToArray();

            for (int i = 0; i < array.Items.Count; i++)
            {
                ManualJsonValue value = array.Items[i];
                ManualJsonObject obj = value != null && value.Type == ManualJsonValueType.Object ? value.ObjectValue : null;
                if (obj == null)
                    continue;

                colors.Add(new ModColor(
                    ReadFloat(obj, "r", 1f),
                    ReadFloat(obj, "g", 1f),
                    ReadFloat(obj, "b", 1f),
                    ReadFloat(obj, "a", 1f)));
            }

            return colors.ToArray();
        }

        private static float ReadFloat(ManualJsonObject obj, string name, float fallback)
        {
            if (obj == null)
                return fallback;

            ManualJsonValue value = obj.Get(name);
            if (value == null)
                return fallback;

            string text = value.Type == ManualJsonValueType.Number ? value.NumberText : value.StringValue;
            float parsed;
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? ColorPickerMath.Clamp01(parsed)
                : fallback;
        }
    }
}
