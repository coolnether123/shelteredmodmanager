using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Theme
{
    /// <summary>
    /// Color palette for a field-manual visual theme. Each color is a material role
    /// (paper, ink, brass, etc.) rather than a UI primitive (background/foreground),
    /// so consumers compose surfaces by combining roles.
    /// </summary>
    internal interface IThemePalette
    {
        Color Gunmetal { get; }
        Color GunmetalShadow { get; }
        Color GunmetalHighlight { get; }
        Color OliveBand { get; }
        Color Brass { get; }

        Color Paper { get; }
        Color PaperShadow { get; }
        Color PaperGrain { get; }

        Color Ink { get; }
        Color InkFaded { get; }

        Color StampRed { get; }
        Color GraphitePencil { get; }

        Color KeycapFace { get; }
        Color KeycapBevelLight { get; }
        Color KeycapBevelDark { get; }
        Color KeycapInk { get; }
        Color KeycapPulse { get; }

        Color MaskingTape { get; }
        Color Vignette { get; }
    }
}
