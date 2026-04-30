using UnityEngine;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Theme;

namespace ShelteredAPI.UI.FieldManual.Tooltips
{
    /// <summary>
    /// A two-line text strip that renders the bus's <see cref="ITooltipBus.Current"/>.
    /// Position, width, and theme are caller-supplied; the widget owns its labels and
    /// subscription only.
    ///
    /// Severity drives color: Info = ink, Hint = faded ink (italic feel), Warning = stamp red.
    /// </summary>
    internal sealed class TooltipDisplayWidget
    {
        private readonly IThemePalette _palette;
        private readonly UIPrimitiveFactory _ui;
        private ITooltipBus _bus;
        private UILabel _titleLabel;
        private UILabel _bodyLabel;

        public TooltipDisplayWidget(IThemePalette palette, UIPrimitiveFactory ui)
        {
            _palette = palette;
            _ui = ui;
        }

        public void Build(GameObject parent, Vector3 localPosition, int width, ITooltipBus bus)
        {
            _bus = bus;

            int titleDepth = _ui.NextDepth();
            _titleLabel = _ui.CreateLabel(parent, "TooltipTitle", string.Empty,
                localPosition + new Vector3(0, 12, 0),
                15, _palette.Ink,
                width, 22,
                NGUIText.Alignment.Center, UIWidget.Pivot.Center, titleDepth);

            int bodyDepth = _ui.NextDepth();
            _bodyLabel = _ui.CreateLabel(parent, "TooltipBody", string.Empty,
                localPosition + new Vector3(0, -10, 0),
                13, _palette.InkFaded,
                width, 40,
                NGUIText.Alignment.Center, UIWidget.Pivot.Center, bodyDepth);
            _bodyLabel.multiLine = true;
            _bodyLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
            _bodyLabel.maxLineCount = 2;

            if (_bus != null)
            {
                _bus.Changed += Refresh;
                Refresh(_bus.Current);
            }
        }

        public void Detach()
        {
            if (_bus != null) _bus.Changed -= Refresh;
            _bus = null;
        }

        private void Refresh(TooltipMessage msg)
        {
            if (_titleLabel == null || _bodyLabel == null) return;

            _titleLabel.text = msg.Title ?? string.Empty;
            _bodyLabel.text = msg.Body ?? string.Empty;

            Color titleColor;
            Color bodyColor;
            switch (msg.Severity)
            {
                case TooltipSeverity.Warning:
                    titleColor = _palette.StampRed;
                    bodyColor = _palette.StampRed;
                    break;
                case TooltipSeverity.Hint:
                    titleColor = _palette.InkFaded;
                    bodyColor = _palette.InkFaded;
                    break;
                default:
                    titleColor = _palette.Ink;
                    bodyColor = _palette.InkFaded;
                    break;
            }

            _titleLabel.color = titleColor;
            _bodyLabel.color = bodyColor;
        }
    }
}
