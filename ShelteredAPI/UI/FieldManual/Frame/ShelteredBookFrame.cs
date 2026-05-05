using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using ShelteredAPI.UI.Internal;
using UnityEngine;


using ShelteredAPI.UI.FieldManual.Tooltips;
using ShelteredAPI.UI.Internal.ModManager;
namespace ShelteredAPI.UI.FieldManual.Frame
{
    /// <summary>
    /// Builds the keybind panel on top of Sheltered's scenario-book visual language.
    /// The frame owns only chrome and anchors; keybind behavior stays in the panel/widgets.
    /// </summary>
    internal sealed class ShelteredBookFrame : IPanelFrame
    {
        private const float ScreenSpan = 4000f;
        private const int PageWidth = 560;
        private const int PageHeight = 660;
        private const int BookWidth = 1240;
        private const int BookHeight = 720;

        private readonly IThemePalette _palette;
        private readonly IThemeMetrics _metrics;
        private readonly ITextureLibrary _textures;
        private readonly UIPrimitiveFactory _ui;

        public ShelteredBookFrame(IThemePalette palette, IThemeMetrics metrics, ITextureLibrary textures, UIPrimitiveFactory ui)
        {
            _palette = palette;
            _metrics = metrics;
            _textures = textures;
            _ui = ui;
        }

        public PanelFrameRegions Build(GameObject parent, string title, string subtitle)
        {
            UITexture vignette = _ui.CreateQuad(parent, "BookBackdrop", _textures.Vignette(512, 512), Vector3.zero,
                (int)ScreenSpan, (int)ScreenSpan, Color.white, _ui.NextDepth());
            _ui.AddClickCollider(vignette.gameObject, (int)ScreenSpan, (int)ScreenSpan, null);

            if (!ModManagerPanelScaffolding.TryCloneScenarioBookVisuals(parent, _ui.NextDepth()))
                BuildFallbackBook(parent);

            GameObject header = _ui.CreateChild(parent, "BookHeader", Vector3.zero);
            GameObject content = _ui.CreateChild(parent, "BookContentRoot", new Vector3(0f, -10f, 0f));
            GameObject footer = _ui.CreateChild(parent, "BookFooterRoot", Vector3.zero);

            int labelDepth = _ui.NextDepth();
            UILabel titleLabel = _ui.CreateLabel(header, "Title", title ?? string.Empty,
                new Vector3(-280f, 286f, 0f), 34, _palette.Ink,
                420, 48, NGUIText.Alignment.Center, UIWidget.Pivot.Center, labelDepth);
            titleLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

            UILabel subtitleLabel = _ui.CreateLabel(header, "Subtitle", subtitle ?? string.Empty,
                new Vector3(300f, 286f, 0f), 20, _palette.InkFaded,
                420, 34, NGUIText.Alignment.Center, UIWidget.Pivot.Center, labelDepth);
            subtitleLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

            // This content rect intentionally leaves a wide middle band clear for the book crease.
            Rect contentRect = new Rect(-540f, -255f, 1080f, 490f);
            return new PanelFrameRegions(parent, header, content, footer, contentRect);
        }

        private void BuildFallbackBook(GameObject parent)
        {
            _ui.CreateQuad(parent, "BookShadow", _textures.White, new Vector3(8f, -10f, 0f),
                BookWidth, BookHeight, _palette.PaperShadow, _ui.NextDepth());

            _ui.CreateQuad(parent, "BookCover", _textures.Gunmetal(BookWidth, BookHeight),
                Vector3.zero, BookWidth, BookHeight, Color.white, _ui.NextDepth());

            _ui.CreateQuad(parent, "LeftPage", _textures.Paper(PageWidth, PageHeight),
                new Vector3(-300f, 0f, 0f), PageWidth, PageHeight, Color.white, _ui.NextDepth());
            _ui.CreateQuad(parent, "RightPage", _textures.Paper(PageWidth, PageHeight),
                new Vector3(300f, 0f, 0f), PageWidth, PageHeight, Color.white, _ui.NextDepth());

            _ui.CreateQuad(parent, "BookCreaseShadow", _textures.White, Vector3.zero,
                66, PageHeight + 20, new Color(0.08f, 0.04f, 0.03f, 0.36f), _ui.NextDepth());
            _ui.CreateQuad(parent, "BookCreaseHighlight", _textures.White, new Vector3(-18f, 0f, 0f),
                4, PageHeight, new Color(0.95f, 0.88f, 0.66f, 0.22f), _ui.NextDepth());

            Texture2D rivet = _textures.Rivet(18);
            int ringDepth = _ui.NextDepth();
            for (int i = 0; i < 8; i++)
            {
                float y = 252f - i * 72f;
                _ui.CreateQuad(parent, "BookRing" + i, rivet, new Vector3(0f, y, 0f),
                    18, 18, Color.white, ringDepth);
            }
        }
    }
}
