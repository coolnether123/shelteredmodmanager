using UnityEngine;

namespace ShelteredScenarioEditor.Presentation.UiKit.Theme
{
    /// <summary>
    /// Phase 9 material and semantic design tokens for scenario authoring.
    /// Material tokens are opaque; SurfaceScrim is the only translucent role.
    /// </summary>
    internal interface IScenarioUiPalette
    {
        // Surfaces and depth
        Color SurfacePage { get; }
        Color SurfaceCard { get; }
        Color SurfaceCardHover { get; }
        Color SurfaceCardSelected { get; }
        Color SurfaceInset { get; }
        Color SurfaceChrome { get; }
        Color SurfaceDisabled { get; }
        Color SurfaceViewport { get; }
        Color SurfaceScrim { get; }
        Color DepthShadow { get; }

        // Borders
        Color BorderDefault { get; }
        Color BorderStrong { get; }
        Color BorderHighlight { get; }
        Color BorderFocus { get; }

        // Text
        Color TextPrimary { get; }
        Color TextSecondary { get; }
        Color TextMuted { get; }
        Color TextInverse { get; }
        Color TextInverseMuted { get; }
        Color TextDisabled { get; }
        Color TextOnAccent { get; }

        // Accent and semantic states
        Color AccentGold { get; }
        Color SemanticReady { get; }
        Color SemanticReadyStrong { get; }
        Color SemanticWarning { get; }
        Color SemanticWarningStrong { get; }
        Color SemanticError { get; }
        Color SemanticErrorStrong { get; }
        Color SemanticInfo { get; }
        Color SemanticInfoStrong { get; }
        Color ControlPressed { get; }

        // Workspace accents
        Color WorkspaceStory { get; }
        Color WorkspaceCast { get; }
        Color WorkspaceSupplies { get; }
        Color WorkspaceMap { get; }
        Color WorkspaceTest { get; }
        Color WorkspacePublish { get; }

    }
}
