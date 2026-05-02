using UnityEngine;

namespace ShelteredAPI.Scenarios.UiKit.Theme
{
    /// <summary>
    /// Color palette for a scenario authoring window theme. Roles describe the
    /// material the surface represents (panel, panel-alt, accent, danger) rather
    /// than a UI primitive. Implementations supply the values; the style sheet
    /// composes them into <see cref="UnityEngine.GUIStyle"/> objects.
    /// </summary>
    internal interface IScenarioUiPalette
    {
        // Surfaces
        Color PanelBase { get; }
        Color PanelRaised { get; }
        Color PanelInset { get; }
        Color PanelOverlay { get; }
        Color Viewport { get; }

        // Borders / dividers
        Color BorderSubtle { get; }
        Color BorderStrong { get; }

        // Accents
        Color AccentPrimary { get; }
        Color AccentActive { get; }
        Color AccentDanger { get; }
        Color AccentMuted { get; }

        // Text roles
        Color TextTitle { get; }
        Color TextSubtitle { get; }
        Color TextBody { get; }
        Color TextMuted { get; }
        Color TextOnAccent { get; }
        Color TextDisabled { get; }
    }
}
