using System;
using ModAPI.Core;
using ModAPI.Internal.SpineUI;
using ModAPI.UI;
using UnityEngine;

namespace ModAPI.Spine.UI
{
    public static class SpineWidgetFactory
    {
        public static GameObject ButtonTemplate;
        public static GameObject SliderTemplate;

        public static GameObject CreateWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel = null)
        {
            MMLog.WriteDebug($"CreateWidget() - Id={def.Id}, Type={def.Type}, Label='{def.Label}'");

            try
            {
                GameObject result;
                switch (def.Type)
                {
                    case SettingType.Bool:
                        result = SpineScalarWidgetBuilder.CreateBoolWidget(def, parent, settingsObject, panel);
                        break;
                    case SettingType.Float:
                        result = SpineScalarWidgetBuilder.CreateSliderWidget(def, parent, settingsObject, panel, false);
                        break;
                    case SettingType.Int:
                        result = SpineScalarWidgetBuilder.CreateSliderWidget(def, parent, settingsObject, panel, true);
                        break;
                    case SettingType.String:
                        result = SpineScalarWidgetBuilder.CreateStringWidget(def, parent, settingsObject, panel);
                        break;
                    case SettingType.Enum:
                        result = SpineSelectionWidgetBuilder.CreateEnumWidget(def, parent, settingsObject, panel);
                        break;
                    case SettingType.Color:
                        result = UIUtil.CreateLabelQuick(parent.gameObject, $"{def.Label}: Color Picker Disabled", 14, Vector3.zero).gameObject;
                        break;
                    case SettingType.Button:
                        result = SpineActionWidgetBuilder.CreateButtonWidget(def, parent, settingsObject);
                        break;
                    case SettingType.Header:
                        result = SpineActionWidgetBuilder.CreateHeaderWidget(def, parent);
                        break;
                    case SettingType.Spacer:
                        result = new GameObject("Spacer_" + def.Id) { transform = { parent = parent } };
                        break;
                    case SettingType.NumericInt:
                        result = SpineScalarWidgetBuilder.CreateNumericIntWidget(def, parent, settingsObject, panel);
                        break;
                    case SettingType.Keybind:
                        result = SpineSelectionWidgetBuilder.CreateKeybindWidget(def, parent, settingsObject, panel);
                        break;
                    case SettingType.Choice:
                        result = SpineSelectionWidgetBuilder.CreateChoiceWidget(def, parent, settingsObject, panel);
                        break;
                    default:
                        result = UIUtil.CreateLabelQuick(parent.gameObject, $"Unknown: {def.Type}", 14, Vector3.zero).gameObject;
                        break;
                }

                if (result != null &&
                    (string.IsNullOrEmpty(result.name) || result.name.StartsWith("New Game Object", StringComparison.Ordinal)))
                {
                    result.name = def.Type + "_" + def.Id;
                }

                return result;
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"Error creating {def.Id}: {ex.Message}\n{ex.StackTrace}");
                return UIUtil.CreateLabelQuick(parent.gameObject, $"Error: {def.Id}", 14, Vector3.zero).gameObject;
            }
        }
    }
}
