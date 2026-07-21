using System;
using System.Globalization;

namespace ModAPI.UI.ColorPicker
{
    /// <summary>
    /// Presentation-neutral RGBA color used by ModAPI color picker contracts.
    /// Channels are normalized floats in the inclusive 0..1 range.
    /// </summary>
    public struct ModColor
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public ModColor(float r, float g, float b)
            : this(r, g, b, 1f)
        {
        }

        public ModColor(float r, float g, float b, float a)
        {
            R = ColorPickerMath.Clamp01(r);
            G = ColorPickerMath.Clamp01(g);
            B = ColorPickerMath.Clamp01(b);
            A = ColorPickerMath.Clamp01(a);
        }

        public string ToHexRgba()
        {
            return "#"
                + ChannelToHex(R)
                + ChannelToHex(G)
                + ChannelToHex(B)
                + ChannelToHex(A);
        }

        public string ToHexRgb()
        {
            return "#"
                + ChannelToHex(R)
                + ChannelToHex(G)
                + ChannelToHex(B);
        }

        public ModHsvColor ToHsv()
        {
            return ColorPickerMath.RgbToHsv(this);
        }

        public bool NearlyEquals(ModColor other)
        {
            const float tolerance = 0.001f;
            return Math.Abs(R - other.R) < tolerance
                && Math.Abs(G - other.G) < tolerance
                && Math.Abs(B - other.B) < tolerance
                && Math.Abs(A - other.A) < tolerance;
        }

        public static bool TryParseHex(string value, out ModColor color)
        {
            color = new ModColor();

            if (string.IsNullOrEmpty(value))
                return false;

            string hex = value.Trim();
            if (hex.StartsWith("#", StringComparison.Ordinal))
                hex = hex.Substring(1);

            if (hex.Length == 3 || hex.Length == 4)
            {
                string expanded = string.Empty;
                for (int i = 0; i < hex.Length; i++)
                    expanded += new string(hex[i], 2);
                hex = expanded;
            }

            if (hex.Length != 6 && hex.Length != 8)
                return false;

            int r;
            int g;
            int b;
            int a = 255;
            if (!TryReadByte(hex, 0, out r)
                || !TryReadByte(hex, 2, out g)
                || !TryReadByte(hex, 4, out b))
                return false;

            if (hex.Length == 8 && !TryReadByte(hex, 6, out a))
                return false;

            color = new ModColor(r / 255f, g / 255f, b / 255f, a / 255f);
            return true;
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "rgba({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})",
                R,
                G,
                B,
                A);
        }

        private static bool TryReadByte(string value, int start, out int result)
        {
            result = 0;
            if (value == null || value.Length < start + 2)
                return false;

            return int.TryParse(
                value.Substring(start, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static string ChannelToHex(float value)
        {
            int channel = (int)Math.Round(ColorPickerMath.Clamp01(value) * 255f);
            if (channel < 0) channel = 0;
            if (channel > 255) channel = 255;
            return channel.ToString("X2", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Presentation-neutral HSVA color used by the picker core.
    /// Hue, saturation, value, and alpha are normalized floats in the inclusive 0..1 range.
    /// </summary>
    public struct ModHsvColor
    {
        public float H;
        public float S;
        public float V;
        public float A;

        public ModHsvColor(float h, float s, float v)
            : this(h, s, v, 1f)
        {
        }

        public ModHsvColor(float h, float s, float v, float a)
        {
            H = ColorPickerMath.Clamp01(h);
            S = ColorPickerMath.Clamp01(s);
            V = ColorPickerMath.Clamp01(v);
            A = ColorPickerMath.Clamp01(a);
        }

        public ModColor ToRgb()
        {
            return ColorPickerMath.HsvToRgb(H, S, V, A);
        }
    }

    /// <summary>
    /// Color conversion and parsing helpers shared by the color picker core.
    /// </summary>
    public static class ColorPickerMath
    {
        public static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        public static ModColor HsvToRgb(float h, float s, float v, float a)
        {
            h = Clamp01(h);
            s = Clamp01(s);
            v = Clamp01(v);

            if (s <= 0f)
                return new ModColor(v, v, v, a);

            float scaled = h * 6f;
            if (scaled >= 6f)
                scaled = 0f;

            int sector = (int)Math.Floor(scaled);
            float fraction = scaled - sector;
            float p = v * (1f - s);
            float q = v * (1f - s * fraction);
            float t = v * (1f - s * (1f - fraction));

            switch (sector)
            {
                case 0: return new ModColor(v, t, p, a);
                case 1: return new ModColor(q, v, p, a);
                case 2: return new ModColor(p, v, t, a);
                case 3: return new ModColor(p, q, v, a);
                case 4: return new ModColor(t, p, v, a);
                default: return new ModColor(v, p, q, a);
            }
        }

        public static ModHsvColor RgbToHsv(ModColor color)
        {
            float r = Clamp01(color.R);
            float g = Clamp01(color.G);
            float b = Clamp01(color.B);
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            float h = 0f;
            if (delta > 0f)
            {
                if (Math.Abs(max - r) < 0.0001f)
                {
                    h = (g - b) / delta;
                    if (h < 0f) h += 6f;
                }
                else if (Math.Abs(max - g) < 0.0001f)
                {
                    h = ((b - r) / delta) + 2f;
                }
                else
                {
                    h = ((r - g) / delta) + 4f;
                }

                h /= 6f;
            }

            float s = max <= 0f ? 0f : delta / max;
            return new ModHsvColor(h, s, max, color.A);
        }

        public static bool TryParseUnitFloat(string text, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(text))
                return false;

            if (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;

            return value >= 0f && value <= 1f;
        }

        public static string FormatUnitFloat(float value)
        {
            return Clamp01(value).ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
