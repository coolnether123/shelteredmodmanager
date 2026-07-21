using UnityEngine;

namespace ShelteredAPI.Scenarios.Presentation.UiKit.Theme
{
    /// <summary>
    /// Exact sampled vanilla material tokens from the Phase 9 design contract.
    /// Color32 keeps the token seam byte-exact and makes opacity auditable.
    /// </summary>
    internal sealed class ScenarioUiPalette : IScenarioUiPalette
    {
        private static Color Token(byte red, byte green, byte blue)
        {
            return new Color32(red, green, blue, 255);
        }

        public Color SurfacePage { get { return Token(35, 23, 45); } }
        public Color SurfaceCard { get { return Token(209, 198, 165); } }
        public Color SurfaceCardHover { get { return Token(229, 217, 184); } }
        public Color SurfaceCardSelected { get { return Token(169, 162, 115); } }
        public Color SurfaceInset { get { return Token(190, 180, 155); } }
        public Color SurfaceChrome { get { return Token(90, 55, 28); } }
        public Color SurfaceDisabled { get { return Token(198, 190, 167); } }
        public Color SurfaceViewport { get { return Token(22, 18, 15); } }
        public Color SurfaceScrim { get { return new Color32(0, 0, 0, 184); } }
        public Color DepthShadow { get { return Token(22, 18, 15); } }

        public Color BorderDefault { get { return Token(89, 88, 88); } }
        public Color BorderStrong { get { return Token(74, 41, 33); } }
        public Color BorderHighlight { get { return Token(234, 224, 195); } }
        public Color BorderFocus { get { return Token(209, 102, 202); } }

        public Color TextPrimary { get { return Token(32, 30, 30); } }
        public Color TextSecondary { get { return Token(89, 88, 88); } }
        public Color TextMuted { get { return Token(122, 95, 72); } }
        public Color TextInverse { get { return Token(234, 224, 195); } }
        public Color TextInverseMuted { get { return Token(194, 194, 194); } }
        public Color TextDisabled { get { return Token(122, 95, 72); } }

        public Color AccentGold { get { return Token(156, 120, 52); } }
        public Color SemanticReady { get { return Token(137, 245, 116); } }
        public Color SemanticReadyStrong { get { return Token(75, 180, 60); } }
        public Color SemanticWarning { get { return Token(219, 192, 134); } }
        public Color SemanticWarningStrong { get { return Token(156, 120, 52); } }
        public Color SemanticError { get { return Token(250, 148, 143); } }
        public Color SemanticErrorStrong { get { return Token(197, 54, 46); } }
        public Color SemanticInfo { get { return Token(144, 153, 161); } }
        public Color SemanticInfoStrong { get { return Token(102, 140, 163); } }
        public Color ControlPressed { get { return Token(122, 95, 72); } }

        public Color WorkspaceStory { get { return Token(145, 73, 70); } }
        public Color WorkspaceCast { get { return Token(147, 96, 124); } }
        public Color WorkspaceSupplies { get { return Token(88, 123, 66); } }
        public Color WorkspaceMap { get { return Token(66, 102, 136); } }
        public Color WorkspaceTest { get { return Token(127, 145, 145); } }
        public Color WorkspacePublish { get { return Token(156, 120, 52); } }

        public Color PanelBase { get { return SurfacePage; } }
        public Color PanelRaised { get { return SurfaceCard; } }
        public Color PanelInset { get { return SurfaceInset; } }
        public Color Viewport { get { return SurfaceViewport; } }
        public Color BorderSubtle { get { return BorderDefault; } }
        public Color AccentActive { get { return AccentGold; } }
        public Color AccentHover { get { return SurfaceCardHover; } }
        public Color AccentDanger { get { return SemanticErrorStrong; } }
        public Color AccentMuted { get { return TextMuted; } }
        public Color AccentSuccess { get { return SemanticReadyStrong; } }
        public Color AccentWarning { get { return SemanticWarningStrong; } }
        public Color AccentNeutral { get { return SurfaceCard; } }
        public Color DisabledSurface { get { return SurfaceDisabled; } }
        public Color TextTitle { get { return AccentGold; } }
        public Color TextSubtitle { get { return TextInverseMuted; } }
        public Color TextBody { get { return TextInverse; } }
        public Color TextOnAccent { get { return TextInverse; } }
        public Color TextOnLight { get { return TextPrimary; } }
    }
}
