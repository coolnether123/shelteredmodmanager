using System;
using System.Collections.Generic;
using System.Linq;
using ModAPI.Spine;
using ModAPI.Spine.UI;
using ModAPI.UI;
using UnityEngine;

namespace ModAPI.Internal.SpineUI
{
    internal static class SpineSelectionWidgetBuilder
    {
        public static GameObject CreateEnumWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, Vector3.zero);
            label.pivot = UIWidget.Pivot.Left;
            SpineWidgetRuntime.SetTooltip(label.gameObject, def.Tooltip);

            var valueLabel = UIUtil.CreateLabelQuick(container, string.Empty, 16, new Vector3(210, 0, 0));
            valueLabel.alignment = NGUIText.Alignment.Center;
            valueLabel.width = 100;

            Action refresh = () =>
            {
                var current = SpineWidgetRuntime.GetValue<object>(def, settingsObject);
                valueLabel.text = current != null ? current.ToString() : string.Empty;
            };
            refresh();

            UIUtil.CreateButton(container, SpineWidgetFactory.ButtonTemplate, "CYCLE", 100, 40, new Vector3(330, 0, 0), () =>
            {
                var values = Enum.GetValues(def.EnumType);
                var currentValue = SpineWidgetRuntime.GetValue<object>(def, settingsObject);
                var index = Array.IndexOf(values, currentValue);
                if (index == -1)
                {
                    var currentText = currentValue != null ? currentValue.ToString() : null;
                    for (var i = 0; i < values.Length; i++)
                    {
                        var candidate = values.GetValue(i);
                        if (candidate != null && candidate.ToString() == currentText)
                        {
                            index = i;
                            break;
                        }
                    }
                }

                if (index == -1) index = 0;
                index = (index + 1) % values.Length;

                var nextValue = values.GetValue(index);
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, nextValue))
                {
                    refresh();
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, panel);
                }
            });

            return container;
        }

        public static GameObject CreateChoiceWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, Vector3.zero);
            label.pivot = UIWidget.Pivot.Left;
            SpineWidgetRuntime.SetTooltip(label.gameObject, def.Tooltip);

            var valueLabel = UIUtil.CreateLabelQuick(container, string.Empty, 16, new Vector3(210, 0, 0));
            valueLabel.alignment = NGUIText.Alignment.Center;
            valueLabel.width = 100;

            Action refresh = () => valueLabel.text = SpineWidgetRuntime.GetValue<string>(def, settingsObject) ?? "None";
            refresh();

            UIUtil.CreateButton(container, SpineWidgetFactory.ButtonTemplate, "CYCLE", 100, 40, new Vector3(330, 0, 0), () =>
            {
                var options = def.GetOptions != null
                    ? def.GetOptions(settingsObject).ToList()
                    : new List<string>();
                if (options.Count == 0) return;

                var currentValue = SpineWidgetRuntime.GetValue<string>(def, settingsObject);
                var index = options.FindIndex(o => o.Equals(currentValue, StringComparison.OrdinalIgnoreCase));
                if (index == -1) index = 0;
                index = (index + 1) % options.Count;

                var nextValue = options[index];
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, nextValue))
                {
                    refresh();
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, panel);
                }
            });

            return container;
        }

        public static GameObject CreateKeybindWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            container.name = "Keybind_" + def.Id;
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, Vector3.zero);
            label.pivot = UIWidget.Pivot.Left;
            SpineWidgetRuntime.SetTooltip(label.gameObject, def.Tooltip);

            var valueLabel = UIUtil.CreateLabelQuick(container, string.Empty, 16, new Vector3(210, 0, 0));
            valueLabel.alignment = NGUIText.Alignment.Center;
            valueLabel.width = 130;

            Action refresh = () =>
            {
                var currentValue = SpineWidgetRuntime.GetValue<KeyCode>(def, settingsObject);
                valueLabel.text = SpineWidgetRuntime.FormatKeyCode(currentValue);
            };
            refresh();

            var capture = container.AddComponent<KeybindCaptureListener>();
            capture.ValueLabel = valueLabel;
            capture.DisplayTextProvider = () =>
            {
                var currentValue = SpineWidgetRuntime.GetValue<KeyCode>(def, settingsObject);
                return SpineWidgetRuntime.FormatKeyCode(currentValue);
            };
            capture.OnCanceled = refresh;
            capture.OnCaptured = key =>
            {
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, key))
                {
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, panel);
                }

                refresh();
            };

            UIUtil.CreateButton(container, SpineWidgetFactory.ButtonTemplate, "REBIND", 95, 40, new Vector3(340, 0, 0), capture.StartCapture);
            UIUtil.CreateButton(container, SpineWidgetFactory.ButtonTemplate, "CLEAR", 70, 40, new Vector3(430, 0, 0), () =>
            {
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, KeyCode.None))
                {
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, panel);
                }

                refresh();
            });

            return container;
        }
    }
}
