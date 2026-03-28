using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    internal sealed class ImguiTextureCache : IDisposable
    {
        private readonly Dictionary<TextureDescriptor, Texture2D> _textures = new Dictionary<TextureDescriptor, Texture2D>();
        private bool _disposed;

        public Texture2D GetFill(Color color)
        {
            return GetTexture(TextureDescriptor.CreateFill(color));
        }

        public Texture2D GetBordered(int width, int height, Color fillColor, Color borderColor)
        {
            return GetTexture(TextureDescriptor.CreateBordered(width, height, fillColor, borderColor));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            var textures = new List<Texture2D>(_textures.Values);
            _textures.Clear();
            _disposed = true;

            for (var i = 0; i < textures.Count; i++)
            {
                if (textures[i] != null)
                {
                    UnityEngine.Object.Destroy(textures[i]);
                }
            }
        }

        private Texture2D GetTexture(TextureDescriptor descriptor)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("ImguiTextureCache");
            }

            Texture2D texture;
            if (_textures.TryGetValue(descriptor, out texture) && texture != null)
            {
                return texture;
            }

            texture = descriptor.HasBorder
                ? CreateBorderedTexture(descriptor.Width, descriptor.Height, descriptor.FillColor, descriptor.BorderColor)
                : CreateFillTexture(descriptor.FillColor);
            _textures[descriptor] = texture;
            return texture;
        }

        private static Texture2D CreateFillTexture(Color32 color)
        {
            var texture = CreateTexture(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateBorderedTexture(int width, int height, Color32 fillColor, Color32 borderColor)
        {
            var texture = CreateTexture(width, height);
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    texture.SetPixel(x, y, isBorder ? borderColor : fillColor);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D CreateTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            return texture;
        }

        private struct TextureDescriptor : IEquatable<TextureDescriptor>
        {
            public int Width;
            public int Height;
            public Color32 FillColor;
            public Color32 BorderColor;
            public bool HasBorder;

            public static TextureDescriptor CreateFill(Color color)
            {
                return new TextureDescriptor
                {
                    Width = 1,
                    Height = 1,
                    FillColor = ToColor32(color),
                    BorderColor = default(Color32),
                    HasBorder = false
                };
            }

            public static TextureDescriptor CreateBordered(int width, int height, Color fillColor, Color borderColor)
            {
                return new TextureDescriptor
                {
                    Width = Math.Max(1, width),
                    Height = Math.Max(1, height),
                    FillColor = ToColor32(fillColor),
                    BorderColor = ToColor32(borderColor),
                    HasBorder = true
                };
            }

            public bool Equals(TextureDescriptor other)
            {
                return Width == other.Width &&
                    Height == other.Height &&
                    FillColor.Equals(other.FillColor) &&
                    BorderColor.Equals(other.BorderColor) &&
                    HasBorder == other.HasBorder;
            }

            public override bool Equals(object obj)
            {
                return obj is TextureDescriptor && Equals((TextureDescriptor)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Width;
                    hashCode = (hashCode * 397) ^ Height;
                    hashCode = (hashCode * 397) ^ FillColor.GetHashCode();
                    hashCode = (hashCode * 397) ^ BorderColor.GetHashCode();
                    hashCode = (hashCode * 397) ^ HasBorder.GetHashCode();
                    return hashCode;
                }
            }

            private static Color32 ToColor32(Color color)
            {
                return new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255));
            }
        }
    }
}
