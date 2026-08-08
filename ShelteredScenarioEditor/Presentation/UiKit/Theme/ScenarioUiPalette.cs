using UnityEngine;

namespace ShelteredScenarioEditor.Presentation.UiKit.Theme
{
    /// <summary>
    /// Opaque smoked-steel material tokens with a restrained brass accent.
    /// Color32 keeps the token seam byte-exact and makes opacity auditable.
    /// </summary>
    internal sealed class ScenarioUiPalette : IScenarioUiPalette
    {
        private static Color Token(byte red, byte green, byte blue)
        {
            return new Color32(red, green, blue, 255);
        }

        public Color SurfacePage { get { return Token(23, 26, 28); } }
        public Color SurfaceCard { get { return Token(36, 40, 43); } }
        public Color SurfaceCardHover { get { return Token(45, 51, 55); } }
        public Color SurfaceCardSelected { get { return Token(58, 52, 40); } }
        public Color SurfaceInset { get { return Token(17, 20, 22); } }
        public Color SurfaceChrome { get { return Token(30, 35, 38); } }
        public Color SurfaceDisabled { get { return Token(42, 46, 48); } }
        public Color SurfaceViewport { get { return Token(13, 15, 16); } }
        public Color SurfaceScrim { get { return new Color32(0, 0, 0, 184); } }
        public Color DepthShadow { get { return Token(7, 8, 9); } }

        public Color BorderDefault { get { return Token(75, 83, 88); } }
        public Color BorderStrong { get { return Token(113, 123, 128); } }
        public Color BorderHighlight { get { return Token(170, 178, 181); } }
        public Color BorderFocus { get { return Token(214, 168, 75); } }

        public Color TextPrimary { get { return Token(241, 238, 230); } }
        public Color TextSecondary { get { return Token(195, 199, 200); } }
        public Color TextMuted { get { return Token(150, 157, 159); } }
        public Color TextInverse { get { return Token(241, 238, 230); } }
        public Color TextInverseMuted { get { return Token(174, 181, 183); } }
        public Color TextDisabled { get { return Token(105, 113, 117); } }
        public Color TextOnAccent { get { return Token(23, 19, 10); } }

        public Color AccentGold { get { return Token(214, 168, 75); } }
        public Color SemanticReady { get { return Token(37, 76, 56); } }
        public Color SemanticReadyStrong { get { return Token(63, 140, 98); } }
        public Color SemanticWarning { get { return Token(91, 67, 30); } }
        public Color SemanticWarningStrong { get { return Token(154, 107, 36); } }
        public Color SemanticError { get { return Token(90, 41, 39); } }
        public Color SemanticErrorStrong { get { return Token(168, 71, 64); } }
        public Color SemanticInfo { get { return Token(38, 69, 85); } }
        public Color SemanticInfoStrong { get { return Token(57, 123, 153); } }
        public Color ControlPressed { get { return Token(20, 23, 25); } }

        public Color WorkspaceStory { get { return Token(113, 62, 52); } }
        public Color WorkspaceCast { get { return Token(94, 70, 103); } }
        public Color WorkspaceSupplies { get { return Token(72, 90, 53); } }
        public Color WorkspaceMap { get { return Token(61, 88, 112); } }
        public Color WorkspaceTest { get { return Token(62, 98, 97); } }
        public Color WorkspacePublish { get { return Token(115, 90, 39); } }

    }
}
