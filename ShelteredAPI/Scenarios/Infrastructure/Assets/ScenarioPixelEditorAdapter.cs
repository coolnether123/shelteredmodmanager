using System;
using ShelteredModManager.Shared.PixelEditing;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Assets
{
    /// <summary>
    /// Adapts Unity textures to the host-neutral pixel editor used by both the
    /// scenario authoring UI and the manager Content Workshop.
    /// </summary>
    internal static class ScenarioPixelEditorAdapter
    {
        public static bool PaintPixel(Texture2D texture, int x, int y, Color color)
        {
            if (texture == null)
                return false;

            PixelEditorSession session = new PixelEditorSession(ToDocument(texture), 1);
            session.ActiveColor = ToRgba32(color);
            if (!session.SetPixel(x, y, session.ActiveColor))
                return false;

            ApplyDocument(session.Document, texture);
            return true;
        }

        public static bool TryPickColor(Texture2D texture, int x, int y, out Color color)
        {
            color = Color.clear;
            if (texture == null)
                return false;

            PixelEditorSession session = new PixelEditorSession(ToDocument(texture), 1);
            if (!session.PickColor(x, y))
                return false;

            color = ToUnityColor(session.ActiveColor);
            return true;
        }

        public static PixelDocument ToDocument(Texture2D texture)
        {
            if (texture == null)
                throw new ArgumentNullException("texture");

            Color32[] pixels = texture.GetPixels32();
            byte[] rgba = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                int offset = i * 4;
                rgba[offset] = pixels[i].r;
                rgba[offset + 1] = pixels[i].g;
                rgba[offset + 2] = pixels[i].b;
                rgba[offset + 3] = pixels[i].a;
            }

            return new PixelDocument(texture.width, texture.height, rgba);
        }

        public static void ApplyDocument(PixelDocument document, Texture2D texture)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (texture == null)
                throw new ArgumentNullException("texture");
            if (document.Width != texture.width || document.Height != texture.height)
                throw new ArgumentException("Pixel document dimensions must match the Unity texture.", "document");

            byte[] rgba = document.CopyRgbaBytes();
            Color32[] pixels = new Color32[document.Width * document.Height];
            for (int i = 0; i < pixels.Length; i++)
            {
                int offset = i * 4;
                pixels[i] = new Color32(
                    rgba[offset],
                    rgba[offset + 1],
                    rgba[offset + 2],
                    rgba[offset + 3]);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
        }

        private static Rgba32 ToRgba32(Color color)
        {
            Color32 value = color;
            return new Rgba32(value.r, value.g, value.b, value.a);
        }

        private static Color ToUnityColor(Rgba32 color)
        {
            return new Color32(color.R, color.G, color.B, color.A);
        }
    }
}
