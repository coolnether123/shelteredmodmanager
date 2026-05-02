using UnityEngine;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Theme;

namespace ShelteredAPI.UI.FieldManual.Widgets
{
    /// <summary>
    /// A section header aligned to the left page of the Sheltered book.
    /// </summary>
    internal sealed class SectionStampWidget
    {
        private readonly IThemePalette _palette;
        private readonly IThemeMetrics _metrics;
        private readonly UIPrimitiveFactory _ui;

        public SectionStampWidget(IThemePalette palette, IThemeMetrics metrics, UIPrimitiveFactory ui)
        {
            _palette = palette;
            _metrics = metrics;
            _ui = ui;
        }

        public GameObject Build(GameObject parent, string text)
        {
            string stamped = (text ?? string.Empty).ToUpperInvariant();
            KeybindRowLayout layout = KeybindRowLayout.Create(_metrics);

            GameObject host = _ui.CreateChild(parent, "SectionStamp", Vector3.zero);
            host.transform.localRotation = Quaternion.Euler(0, 0, _metrics.SectionStampRotationDegrees);

            int depth = _ui.NextDepth();
            UILabel label = _ui.CreateLabel(host, "Stamp", stamped,
                new Vector3(layout.ActionLabelX, 0, 0),
                20,
                _palette.InkFaded,
                _metrics.ActionLabelWidth, _metrics.SectionStampHeight,
                NGUIText.Alignment.Left, UIWidget.Pivot.Left, depth);
            label.overflowMethod = UILabel.Overflow.ShrinkContent;

            return host;
        }
    }
}
