using System;
using UnityEngine;

namespace Cortex.Modules.Shared
{
    internal enum RuntimeLogSeverity
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal,
        Unknown
    }

    internal static class RuntimeLogVisuals
    {
        private static readonly Color DebugAccent = new Color(0.72f, 0.72f, 0.78f, 1f);
        private static readonly Color InfoAccent = new Color(0.55f, 0.82f, 1f, 1f);
        private static readonly Color WarningAccent = new Color(1f, 0.88f, 0.56f, 1f);
        private static readonly Color ErrorAccent = new Color(1f, 0.62f, 0.62f, 1f);
        private static readonly Color FatalAccent = new Color(1f, 0.38f, 0.38f, 1f);
        private static readonly Color UnknownAccent = new Color(0.78f, 0.82f, 0.9f, 1f);

        private static readonly Color NeutralRowBackground = new Color(0.12f, 0.13f, 0.16f, 1f);
        private static readonly Color NeutralText = new Color(0.9f, 0.93f, 0.97f, 1f);

        public static RuntimeLogSeverity GetSeverity(string level)
        {
            if (string.IsNullOrEmpty(level))
            {
                return RuntimeLogSeverity.Unknown;
            }

            if (string.Equals(level, "Fatal", StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeLogSeverity.Fatal;
            }

            if (string.Equals(level, "Error", StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeLogSeverity.Error;
            }

            if (string.Equals(level, "Warning", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(level, "Warn", StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeLogSeverity.Warning;
            }

            if (string.Equals(level, "Debug", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(level, "Trace", StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeLogSeverity.Debug;
            }

            if (string.Equals(level, "Info", StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeLogSeverity.Info;
            }

            return RuntimeLogSeverity.Unknown;
        }

        public static Color GetAccentColor(string level)
        {
            switch (GetSeverity(level))
            {
                case RuntimeLogSeverity.Fatal:
                    return FatalAccent;
                case RuntimeLogSeverity.Error:
                    return ErrorAccent;
                case RuntimeLogSeverity.Warning:
                    return WarningAccent;
                case RuntimeLogSeverity.Debug:
                    return DebugAccent;
                case RuntimeLogSeverity.Info:
                    return InfoAccent;
                default:
                    return UnknownAccent;
            }
        }

        public static Color GetEntryBackgroundColor(string level, bool isSelected)
        {
            if (isSelected)
            {
                return MultiplyRgb(GetAccentColor(level), 0.34f, 1f);
            }

            return Lerp(NeutralRowBackground, GetAccentColor(level), 0.18f, 1f);
        }

        public static Color GetEntryTextColor(string level, bool isSelected)
        {
            if (isSelected)
            {
                return Color.white;
            }

            return Lerp(NeutralText, GetAccentColor(level), 0.35f, 1f);
        }

        private static Color MultiplyRgb(Color color, float factor, float alpha)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, alpha);
        }

        private static Color Lerp(Color from, Color to, float amount, float alpha)
        {
            amount = Mathf.Clamp01(amount);
            return new Color(
                from.r + ((to.r - from.r) * amount),
                from.g + ((to.g - from.g) * amount),
                from.b + ((to.b - from.b) * amount),
                alpha);
        }
    }
}
