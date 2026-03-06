using ModAPI.Spine;
using ModAPI.Spine.UI;
using ModAPI.UI;
using UnityEngine;

namespace ModAPI.Internal.SpineUI
{
    internal static class SpineActionWidgetBuilder
    {
        public static GameObject CreateButtonWidget(SettingDefinition def, Transform parent, object settingsObject)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var isFullWidth = def.Label.StartsWith("=") || def.Label.Length > 25;
            if (isFullWidth)
            {
                UIUtil.CreateButton(container, SpineWidgetFactory.ButtonTemplate, def.Label, 380, 45, new Vector3(190, 0, 0), () =>
                {
                    if (def.OnChanged != null) def.OnChanged(settingsObject);
                });
            }
            else
            {
                var label = UIUtil.CreateLabelQuick(container, def.Label, 16, Vector3.zero);
                label.pivot = UIWidget.Pivot.Left;
                UIUtil.CreateButton(container, SpineWidgetFactory.ButtonTemplate, "EXECUTE", 100, 40, new Vector3(330, 0, 0), () =>
                {
                    if (def.OnChanged != null) def.OnChanged(settingsObject);
                });
            }

            SpineWidgetRuntime.SetTooltip(container, def.Tooltip);
            return container;
        }

        public static GameObject CreateHeaderWidget(SettingDefinition def, Transform parent)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label.ToUpper(), 17, Vector3.zero);
            label.pivot = UIWidget.Pivot.Left;
            label.alignment = NGUIText.Alignment.Left;
            label.transform.localPosition = Vector3.zero;
            label.width = 260;
            label.overflowMethod = UILabel.Overflow.ClampContent;
            label.multiLine = false;
            label.color = def.HeaderColor ?? new Color(1f, 0.8f, 0.2f);
            return container;
        }
    }
}
