using UnityEngine;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Theme;

namespace ShelteredAPI.UI.FieldManual.Widgets
{
    /// <summary>
    /// A section header rendered as a slightly rotated red rubber stamp.
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
            string stamped = "— " + (text ?? string.Empty).ToUpperInvariant() + " —";
            int rowWidth = (int)(_metrics.PanelWidth * 0.78f);

            GameObject host = _ui.CreateChild(parent, "SectionStamp", Vector3.zero);
            host.transform.localRotation = Quaternion.Euler(0, 0, _metrics.SectionStampRotationDegrees);

            int depth = _ui.NextDepth();
            UILabel label = _ui.CreateLabel(host, "Stamp", stamped,
                new Vector3(-rowWidth * 0.5f + 12, 0, 0),
                22,
                new Color(_palette.StampRed.r, _palette.StampRed.g, _palette.StampRed.b, 0.85f),
                rowWidth - 24, _metrics.SectionStampHeight,
                NGUIText.Alignment.Left, UIWidget.Pivot.Left, depth);
            label.spacingX = 2;

            return host;
        }
    }
}
