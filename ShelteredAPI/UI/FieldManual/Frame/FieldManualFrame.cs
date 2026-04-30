using UnityEngine;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;

namespace ShelteredAPI.UI.FieldManual.Frame
{
    /// <summary>
    /// Assembles the "Operator's Field Manual" visual chrome:
    ///   - Vignette overlay over the whole screen
    ///   - Gunmetal panel with corner rivets
    ///   - Olive title strip with stencil text
    ///   - Cream paper sheet inset inside the gunmetal, with masking-tape strips at top corners
    /// All sizing/positions come from <see cref="IThemeMetrics"/>; all colors from <see cref="IThemePalette"/>.
    /// No interaction logic lives here.
    /// </summary>
    internal sealed class FieldManualFrame : IPanelFrame
    {
        private const float ScreenSpan = 4000f;

        private readonly IThemePalette _palette;
        private readonly IThemeMetrics _metrics;
        private readonly ITextureLibrary _textures;
        private readonly UIPrimitiveFactory _ui;

        public FieldManualFrame(IThemePalette palette, IThemeMetrics metrics, ITextureLibrary textures, UIPrimitiveFactory ui)
        {
            _palette = palette;
            _metrics = metrics;
            _textures = textures;
            _ui = ui;
        }

        public PanelFrameRegions Build(GameObject parent, string title, string subtitle)
        {
            int W = _metrics.PanelWidth;
            int H = _metrics.PanelHeight;
            int inset = _metrics.FrameInset;

            // ---------- 1. Vignette ----------
            _ui.CreateQuad(parent, "Vignette", _textures.Vignette(512, 512), Vector3.zero,
                (int)ScreenSpan, (int)ScreenSpan, Color.white, _ui.NextDepth());

            // ---------- 2. Gunmetal panel ----------
            int gunDepth = _ui.NextDepth();
            _ui.CreateQuad(parent, "Gunmetal", _textures.Gunmetal(W, H), Vector3.zero,
                W, H, Color.white, gunDepth);

            // Corner rivets
            int rivetSize = _metrics.RivetSize;
            int rivetMargin = _metrics.RivetMargin;
            float halfW = W * 0.5f - rivetMargin;
            float halfH = H * 0.5f - rivetMargin;
            int rivetDepth = _ui.NextDepth();
            Texture2D rivet = _textures.Rivet(rivetSize);
            _ui.CreateQuad(parent, "RivetTL", rivet, new Vector3(-halfW,  halfH, 0), rivetSize, rivetSize, Color.white, rivetDepth);
            _ui.CreateQuad(parent, "RivetTR", rivet, new Vector3( halfW,  halfH, 0), rivetSize, rivetSize, Color.white, rivetDepth);
            _ui.CreateQuad(parent, "RivetBL", rivet, new Vector3(-halfW, -halfH, 0), rivetSize, rivetSize, Color.white, rivetDepth);
            _ui.CreateQuad(parent, "RivetBR", rivet, new Vector3( halfW, -halfH, 0), rivetSize, rivetSize, Color.white, rivetDepth);

            // ---------- 3. Title strip (olive band) ----------
            int stripW = W - 2 * (_metrics.TitleStripInset + 4);
            int stripH = _metrics.TitleStripHeight;
            float stripY = H * 0.5f - inset - stripH * 0.5f;
            int stripDepth = _ui.NextDepth();

            GameObject header = _ui.CreateChild(parent, "Header", new Vector3(0, stripY, 0));
            _ui.CreateQuad(header, "OliveBand", _textures.OliveBand(stripW, stripH), Vector3.zero,
                stripW, stripH, Color.white, stripDepth);

            int titleDepth = _ui.NextDepth();
            UILabel titleLabel = _ui.CreateLabel(header, "Title", title ?? string.Empty,
                new Vector3(-stripW * 0.5f + 24, 8, 0), 26, _palette.Paper,
                stripW - 280, 32,
                NGUIText.Alignment.Left, UIWidget.Pivot.Left, titleDepth);
            // Bitmap fonts don't always re-render on size changes if the label was created
            // with no text earlier; nudge:
            titleLabel.MakePixelPerfect();

            UILabel subtitleLabel = _ui.CreateLabel(header, "Subtitle", subtitle ?? string.Empty,
                new Vector3(-stripW * 0.5f + 24, -16, 0), 14, new Color(_palette.Paper.r, _palette.Paper.g, _palette.Paper.b, 0.78f),
                stripW - 280, 20,
                NGUIText.Alignment.Left, UIWidget.Pivot.Left, titleDepth);
            subtitleLabel.MakePixelPerfect();

            UILabel fileNumber = _ui.CreateLabel(header, "FileNumber", "FILE № SHL-INPUT",
                new Vector3(stripW * 0.5f - 24, 0, 0), 14, new Color(_palette.Brass.r, _palette.Brass.g, _palette.Brass.b, 0.95f),
                240, 24,
                NGUIText.Alignment.Right, UIWidget.Pivot.Right, titleDepth);
            fileNumber.MakePixelPerfect();

            // ---------- 4. Paper sheet ----------
            int paperX = inset + 8;
            int paperTop = inset + stripH + 12;
            int paperBottom = inset + _metrics.FooterHeight + 8;
            int paperW = W - 2 * paperX;
            int paperH = H - paperTop - paperBottom;
            float paperCenterY = -(paperBottom * 0.5f) + (paperH - paperTop + paperBottom) * 0.0f;
            paperCenterY = (-(H * 0.5f) + paperBottom) + paperH * 0.5f;

            int paperShadowDepth = _ui.NextDepth();
            _ui.CreateQuad(parent, "PaperShadow", _textures.White, new Vector3(4, paperCenterY - 4, 0),
                paperW, paperH, _palette.PaperShadow, paperShadowDepth);

            int paperDepth = _ui.NextDepth();
            _ui.CreateQuad(parent, "Paper", _textures.Paper(paperW, paperH),
                new Vector3(0, paperCenterY, 0), paperW, paperH, Color.white, paperDepth);

            // Masking tape on top corners of the paper
            int tapeW = _metrics.TapeWidth;
            int tapeH = _metrics.TapeHeight;
            Texture2D tape = _textures.MaskingTape(tapeW, tapeH);
            int tapeDepth = _ui.NextDepth();
            float tapeY = paperCenterY + paperH * 0.5f - 6;
            float tapeOffsetX = paperW * 0.5f - 30;
            UITexture tapeL = _ui.CreateQuad(parent, "TapeLeft", tape,
                new Vector3(-tapeOffsetX, tapeY, 0), tapeW, tapeH, Color.white, tapeDepth);
            tapeL.transform.localRotation = Quaternion.Euler(0, 0, -7f);
            UITexture tapeR = _ui.CreateQuad(parent, "TapeRight", tape,
                new Vector3(tapeOffsetX, tapeY, 0), tapeW, tapeH, Color.white, tapeDepth);
            tapeR.transform.localRotation = Quaternion.Euler(0, 0, 8f);

            // ---------- 5. Region anchors ----------
            GameObject content = _ui.CreateChild(parent, "ContentRoot", new Vector3(0, paperCenterY, 0));
            GameObject footer = _ui.CreateChild(parent, "FooterRoot",
                new Vector3(0, -H * 0.5f + _metrics.FooterHeight * 0.5f + inset, 0));

            Rect contentRect = new Rect(
                -paperW * 0.5f + _metrics.ContentSidePadding,
                paperCenterY - paperH * 0.5f + _metrics.ContentBottomPadding,
                paperW - 2 * _metrics.ContentSidePadding,
                paperH - _metrics.ContentTopPadding - _metrics.ContentBottomPadding);

            return new PanelFrameRegions(parent, header, content, footer, contentRect);
        }
    }
}
