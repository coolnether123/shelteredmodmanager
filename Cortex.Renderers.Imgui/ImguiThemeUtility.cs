using System.Text;
using Cortex.Rendering.Models;
using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    internal static class ImguiThemeUtility
    {
        public static string BuildKey(params RenderColor[] colors)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < colors.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('|');
                }

                AppendColor(builder, colors[i]);
            }

            return builder.ToString();
        }

        public static string BuildKey(params Color[] colors)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < colors.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('|');
                }

                AppendColor(builder, colors[i]);
            }

            return builder.ToString();
        }

        public static Color ResolveColor(RenderColor color, Color fallback)
        {
            if (color.A == 0f && color.R == 0f && color.G == 0f && color.B == 0f)
            {
                return fallback;
            }

            return new Color(color.R, color.G, color.B, color.A);
        }

        public static Color ToColor(RenderColor color)
        {
            return new Color(color.R, color.G, color.B, color.A);
        }

        private static void AppendColor(StringBuilder builder, RenderColor color)
        {
            builder
                .Append(color.R.ToString("F3"))
                .Append(',')
                .Append(color.G.ToString("F3"))
                .Append(',')
                .Append(color.B.ToString("F3"))
                .Append(',')
                .Append(color.A.ToString("F3"));
        }

        private static void AppendColor(StringBuilder builder, Color color)
        {
            builder
                .Append(color.r.ToString("F3"))
                .Append(',')
                .Append(color.g.ToString("F3"))
                .Append(',')
                .Append(color.b.ToString("F3"))
                .Append(',')
                .Append(color.a.ToString("F3"));
        }
    }
}
