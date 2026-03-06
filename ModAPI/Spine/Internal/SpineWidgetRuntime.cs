using System;
using ModAPI.Core;
using ModAPI.Spine;
using ModAPI.UI;
using UnityEngine;

namespace ModAPI.Internal.SpineUI
{
    internal static class SpineWidgetRuntime
    {
        public const int WidgetDepth = 10020;

        public static void NotifyChange(SettingDefinition def, object settingsObject, ModSettingsPanel panel)
        {
            MMLog.WriteDebug($"NotifyChange for {def.Id}. Signal to panel: {panel != null}");
            if (def.OnChanged != null)
            {
                def.OnChanged(settingsObject);
            }

            if (def.RequiresRestart)
            {
                MMLog.WriteInfo($"[Settings] {def.Label} requires restart.");
            }

            if (panel == null)
            {
                return;
            }

            panel.OnSettingChanged();
            if (def.ControlsChildVisibility)
            {
                panel.RefreshDependents(def.Id);
            }
        }

        public static void SetTooltip(GameObject go, string text)
        {
            if (go == null || string.IsNullOrEmpty(text)) return;

            var box = go.GetComponent<BoxCollider>();
            if (box == null)
            {
                NGUITools.AddWidgetCollider(go);
                box = go.GetComponent<BoxCollider>();
            }

            if (box != null)
            {
                var widget = go.GetComponent<UIWidget>();
                if (widget != null && (box.size.x < 1f || box.size.y < 1f))
                {
                    box.size = new Vector3(Mathf.Max(widget.width, 200), Mathf.Max(widget.height, 24), 1);
                    box.center = new Vector3(box.size.x / 2, 0, 0);
                }
            }

            var tooltipRoot = ResolveTooltipRoot(go);
            if (tooltipRoot == null) return;

            var label = go.GetComponent<UILabel>();
            UIHelper.AddTooltip(go, tooltipRoot, text, label != null ? label.bitmapFont : null, label != null ? label.trueTypeFont : null);
        }

        public static T GetValue<T>(SettingDefinition def, object obj)
        {
            if (def.Getter != null)
            {
                var value = def.Getter(obj);
                if (value is T)
                {
                    return (T)value;
                }

                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                }
            }

            object fallback = def.DefaultValue;
            if (fallback == null && typeof(T) == typeof(Color))
            {
                fallback = Color.white;
            }

            return (T)fallback;
        }

        public static bool TryApplyValue(SettingDefinition def, object settingsObject, object newValue)
        {
            if (def.Validate != null && !def.Validate(newValue, settingsObject))
            {
                return false;
            }

            SetValue(def, settingsObject, newValue);
            return true;
        }

        public static string FormatKeyCode(KeyCode key)
        {
            if (key == KeyCode.None) return "UNBOUND";

            var raw = key.ToString();
            if (raw.StartsWith("Alpha", StringComparison.Ordinal) && raw.Length == 6) return raw.Substring(5);
            if (raw.StartsWith("Keypad", StringComparison.Ordinal)) return "KP " + HumanizeKeyName(raw.Substring(6)).ToUpperInvariant();
            if (raw.EndsWith("Arrow", StringComparison.Ordinal)) return raw.Replace("Arrow", string.Empty).ToUpperInvariant();
            if (raw == "Mouse0") return "MOUSE LEFT";
            if (raw == "Mouse1") return "MOUSE RIGHT";
            if (raw == "Mouse2") return "MOUSE MIDDLE";
            return HumanizeKeyName(raw).ToUpperInvariant();
        }

        private static Transform ResolveTooltipRoot(GameObject go)
        {
            var panel = NGUITools.FindInParents<UIPanel>(go);
            if (panel != null) return panel.transform;

            var uiRoot = NGUITools.FindInParents<UIRoot>(go);
            if (uiRoot != null) return uiRoot.transform;

            return go.transform != null ? go.transform.root : null;
        }

        private static void SetValue(SettingDefinition def, object obj, object value)
        {
            if (def.Setter == null)
            {
                return;
            }

            try
            {
                var targetType = def.EnumType ?? (def.DefaultValue != null ? def.DefaultValue.GetType() : typeof(object));
                var converted = value;

                if (value != null && targetType.IsEnum && value.GetType() != targetType)
                {
                    if (value is string)
                    {
                        converted = Enum.Parse(targetType, (string)value);
                    }
                    else
                    {
                        converted = Enum.ToObject(targetType, value);
                    }
                }
                else if (value != null && !targetType.IsAssignableFrom(value.GetType()))
                {
                    converted = Convert.ChangeType(value, targetType);
                }

                def.Setter(obj, converted);
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"Setter failed for {def.Id}: {ex.Message}");
            }
        }

        private static string HumanizeKeyName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var chars = new System.Text.StringBuilder(value.Length + 8);
            var previous = '\0';
            for (var i = 0; i < value.Length; i++)
            {
                var current = value[i];
                if (current == '_' || current == '-')
                {
                    if (chars.Length > 0 && chars[chars.Length - 1] != ' ')
                    {
                        chars.Append(' ');
                    }

                    previous = current;
                    continue;
                }

                var addSpace =
                    i > 0 &&
                    ((char.IsUpper(current) && (char.IsLower(previous) || char.IsDigit(previous))) ||
                     (char.IsDigit(current) && char.IsLetter(previous)) ||
                     (char.IsLetter(current) && char.IsDigit(previous)));

                if (addSpace && chars.Length > 0 && chars[chars.Length - 1] != ' ')
                {
                    chars.Append(' ');
                }

                chars.Append(current);
                previous = current;
            }

            return chars.ToString().Trim();
        }
    }
}
