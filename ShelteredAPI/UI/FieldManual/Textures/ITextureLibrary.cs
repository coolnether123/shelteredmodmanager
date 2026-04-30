using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Textures
{
    internal enum KeycapState
    {
        Rest = 0,
        Hover = 1,
        Pulse = 2,
        Empty = 3
    }

    /// <summary>
    /// Provides cached procedural textures keyed by kind and size.
    /// Implementations must be safe to call repeatedly: identical (kind, size) requests
    /// must return the same Texture2D instance.
    /// </summary>
    internal interface ITextureLibrary
    {
        Texture2D White { get; }
        Texture2D Gunmetal(int width, int height);
        Texture2D Paper(int width, int height);
        Texture2D Rivet(int diameter);
        Texture2D Keycap(int width, int height, KeycapState state);
        Texture2D MaskingTape(int width, int height);
        Texture2D OliveBand(int width, int height);
        Texture2D Vignette(int width, int height);
    }
}
