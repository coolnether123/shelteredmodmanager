using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Widgets
{
    /// <summary>
    /// Builds the fixed column header that mirrors <see cref="KeybindRowLayout"/>.
    /// </summary>
    internal sealed class KeybindColumnHeaderWidget
    {
        private readonly IThemePalette _palette;
        private readonly IThemeMetrics _metrics;
        private readonly ITextureLibrary _textures;
        private readonly UIPrimitiveFactory _ui;

        public KeybindColumnHeaderWidget(IThemePalette palette, IThemeMetrics metrics, ITextureLibrary textures, UIPrimitiveFactory ui)
        {
            _palette = palette;
            _metrics = metrics;
            _textures = textures;
            _ui = ui;
        }

        public GameObject Build(GameObject parent)
        {
            KeybindRowLayout layout = KeybindRowLayout.Create(_metrics);
            GameObject header = _ui.CreateChild(parent, "KeybindColumnHeader", Vector3.zero);
            Color headerColor = new Color(_palette.InkFaded.r, _palette.InkFaded.g, _palette.InkFaded.b, 0.95f);

            CreateHeaderLabel(header, "ActionHeader", "ACTION",
                new Vector3(layout.ActionLabelX, 2f, 0f), _metrics.ActionLabelWidth,
                NGUIText.Alignment.Left, UIWidget.Pivot.Left, headerColor);
            CreateHeaderLabel(header, "PrimaryHeader", "PRIMARY",
                new Vector3(layout.PrimaryCenterX, 2f, 0f), layout.KeySlotWidth,
                NGUIText.Alignment.Center, UIWidget.Pivot.Center, headerColor);
            CreateHeaderLabel(header, "AltHeader", "ALT",
                new Vector3(layout.SecondaryCenterX, 2f, 0f), layout.KeySlotWidth,
                NGUIText.Alignment.Center, UIWidget.Pivot.Center, headerColor);
            CreateHeaderLabel(header, "ClearHeader", "CLR",
                new Vector3(layout.ClearCenterX, 2f, 0f), layout.SmallButtonWidth,
                NGUIText.Alignment.Center, UIWidget.Pivot.Center, headerColor);
            CreateHeaderLabel(header, "ResetHeader", "RST",
                new Vector3(layout.ResetCenterX, 2f, 0f), layout.SmallButtonWidth,
                NGUIText.Alignment.Center, UIWidget.Pivot.Center, headerColor);

            _ui.CreateQuad(header, "Rule", _textures.White, new Vector3(0f, -15f, 0f),
                layout.RowWidth, 2, new Color(_palette.PaperGrain.r, _palette.PaperGrain.g, _palette.PaperGrain.b, 0.45f), _ui.NextDepth());
            return header;
        }

        private void CreateHeaderLabel(GameObject parent, string name, string text, Vector3 position, int width, NGUIText.Alignment alignment, UIWidget.Pivot pivot, Color color)
        {
            UILabel label = _ui.CreateLabel(parent, name, text, position,
                12, color, width, 20, alignment, pivot, _ui.NextDepth());
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
        }
    }
}
