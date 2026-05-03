using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ModAPI.Spine;
using ShelteredAPI.UI.Compatibility;
using ShelteredAPI.UI.FieldManual.Panels;
using ShelteredAPI.UI.Internal.Spine;
using ShelteredAPI.UI.Spine;
using UnityEngine;

namespace ShelteredAPI.UI.Compatibility.Settings
{
    /// <summary>
    /// Builds Sheltered book-style widgets for ordinary Spine settings.
    /// Keeps theme/layout concerns out of ModSettingsPanel orchestration.
    /// </summary>
    internal sealed class ModSettingsBookWidgetRenderer
    {
        private static readonly Color ColorHeader = new Color(0.17f, 0.13f, 0.09f, 1f);
        private static readonly Color ColorText = new Color(0.12f, 0.09f, 0.06f, 1f);
        private static readonly Color ColorRowRule = new Color(0.35f, 0.25f, 0.16f, 0.28f);
        private static readonly Color ColorTrack = new Color(0.25f, 0.18f, 0.12f, 0.72f);
        private static readonly Color ColorTrackFill = new Color(0.56f, 0.38f, 0.22f, 0.96f);
        private static readonly Color ColorValue = new Color(0.20f, 0.14f, 0.09f, 1f);
        private static readonly Color ColorInputBackground = new Color(0.18f, 0.13f, 0.09f, 0.52f);
        private static readonly Color ColorSwatchBorder = new Color(0.18f, 0.11f, 0.06f, 0.82f);

        private const int ColumnWidth = 500;
        private const int LabelWidth = 255;
        private const int ValueWidth = 86;
        private const int TrackWidth = 150;
        private const int TrackHeight = 18;
        private const int SmallButtonWidth = 38;
        private const int SmallButtonHeight = 34;
        private const int HeaderFontSize = 24;
        private const int LabelFontSize = 18;
        private const int ValueFontSize = 18;
        private const int ControlFontSize = 16;
        private const float LabelY = 10f;
        private const float ControlY = -17f;
        private const float RuleY = -38f;
        private const float ValueX = 270f;
        private const float TrackCenterX = 385f;
        private const float DecreaseX = 291f;
        private const float IncreaseX = 479f;

        private readonly FieldManualWindowChrome _chrome;
        private readonly Texture2D _whiteTexture;
        private readonly UIFont _bitmapFont;
        private readonly Font _ttfFont;
        private readonly ModSettingsPanel _panel;

        public ModSettingsBookWidgetRenderer(
            FieldManualWindowChrome chrome,
            Texture2D whiteTexture,
            UIFont bitmapFont,
            Font ttfFont,
            ModSettingsPanel panel)
        {
            _chrome = chrome;
            _whiteTexture = whiteTexture;
            _bitmapFont = bitmapFont;
            _ttfFont = ttfFont;
            _panel = panel;
        }

        public GameObject CreateWidget(GameObject contentRoot, SettingDefinition def, object settingsObject, bool isSectionHeader)
        {
            if (contentRoot == null || def == null)
                return null;

            if (isSectionHeader || def.Type == SettingType.Header)
                return CreateHeaderWidget(contentRoot, def);

            GameObject row = _chrome.Ui.CreateChild(contentRoot, "BookSetting_" + def.Id, Vector3.zero);
            CreateRowRule(row);
            CreateSettingLabel(row, def);

            switch (def.Type)
            {
                case SettingType.Bool:
                    BuildBoolControl(row, def, settingsObject);
                    break;
                case SettingType.Float:
                    BuildNumericControl(row, def, settingsObject, false);
                    break;
                case SettingType.Int:
                case SettingType.NumericInt:
                    BuildNumericControl(row, def, settingsObject, true);
                    break;
                case SettingType.Enum:
                    BuildEnumControl(row, def, settingsObject);
                    break;
                case SettingType.Choice:
                    BuildChoiceControl(row, def, settingsObject);
                    break;
                case SettingType.String:
                    BuildStringControl(row, def, settingsObject);
                    break;
                case SettingType.Color:
                    BuildColorControl(row, def, settingsObject);
                    break;
                case SettingType.Button:
                    BuildActionControl(row, def, settingsObject);
                    break;
                case SettingType.Keybind:
                    BuildSingleKeybindControl(row, def, settingsObject);
                    break;
                case SettingType.Spacer:
                    break;
                default:
                    BuildReadOnlyValue(row, def.Type.ToString());
                    break;
            }

            return row;
        }

        public static void ApplyStyle(GameObject widget, bool isSectionHeader)
        {
            if (widget == null)
                return;

            UILabel[] labels = widget.GetComponentsInChildren<UILabel>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                UILabel label = labels[i];
                if (label == null)
                    continue;

                label.color = isSectionHeader
                    ? ColorHeader
                    : (label.name == "Value" ? ColorValue : ColorText);
                if (label.fontSize <= 0)
                    continue;

                label.overflowMethod = UILabel.Overflow.ShrinkContent;
            }
        }

        private GameObject CreateHeaderWidget(GameObject contentRoot, SettingDefinition def)
        {
            GameObject row = _chrome.Ui.CreateChild(contentRoot, "BookHeader_" + (def != null ? def.Id : "Settings"), Vector3.zero);
            string label = def != null && !string.IsNullOrEmpty(def.Label) ? def.Label.ToUpperInvariant() : "SETTINGS";
            Color color = def != null && def.HeaderColor.HasValue ? def.HeaderColor.Value : ColorHeader;
            UILabel header = _chrome.Ui.CreateLabel(row, "HeaderLabel", label, new Vector3(0f, 6f, 0f), HeaderFontSize, color, ColumnWidth, 34, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _chrome.Ui.NextDepth());
            header.overflowMethod = UILabel.Overflow.ShrinkContent;
            _chrome.Ui.CreateQuad(row, "HeaderRule", _whiteTexture, new Vector3(ColumnWidth * 0.5f, -22f, 0f), ColumnWidth, 3, ColorRowRule, _chrome.Ui.NextDepth());
            return row;
        }

        private void CreateRowRule(GameObject row)
        {
            _chrome.Ui.CreateQuad(row, "RowRule", _whiteTexture, new Vector3(ColumnWidth * 0.5f, RuleY, 0f), ColumnWidth, 2, ColorRowRule, _chrome.Ui.NextDepth());
        }

        private UILabel CreateSettingLabel(GameObject row, SettingDefinition def)
        {
            string text = def != null && !string.IsNullOrEmpty(def.Label) ? def.Label : (def != null ? def.Id : string.Empty);
            UILabel label = _chrome.Ui.CreateLabel(row, "Label", text, new Vector3(0f, LabelY, 0f), LabelFontSize, ColorText, LabelWidth, 34, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _chrome.Ui.NextDepth());
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
            if (def != null)
                SpineWidgetRuntime.SetTooltip(label.gameObject, def.Tooltip);
            return label;
        }

        private void BuildBoolControl(GameObject row, SettingDefinition def, object settingsObject)
        {
            Func<bool> read = delegate { return SpineWidgetRuntime.GetValue<bool>(def, settingsObject); };
            GameObject toggle = null;
            toggle = CreateButton(row, "Toggle", FormatBool(def, read()), new Vector3(TrackCenterX, LabelY, 0f), ControlFontSize, TrackWidth, 36, delegate
            {
                bool next = !read();
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, next))
                {
                    UpdateButtonLabel(toggle, FormatBool(def, next));
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, _panel);
                }
            });
            SpineWidgetRuntime.SetTooltip(toggle, def.Tooltip);
        }

        private void BuildNumericControl(GameObject row, SettingDefinition def, object settingsObject, bool snapToInt)
        {
            float min = def.MinValue.HasValue ? def.MinValue.Value : 0f;
            float max = def.MaxValue.HasValue ? def.MaxValue.Value : (snapToInt ? 100f : 1f);
            if (Mathf.Abs(max - min) < 0.0001f)
                max = min + 1f;

            Func<float> read = delegate
            {
                return snapToInt
                    ? (float)SpineWidgetRuntime.GetValue<int>(def, settingsObject)
                    : SpineWidgetRuntime.GetValue<float>(def, settingsObject);
            };

            UILabel value = CreateValueLabel(row, FormatNumeric(def, read(), snapToInt), new Vector3(ValueX, LabelY, 0f), ValueWidth);
            UITexture fill;
            GameObject track = CreateSliderTrack(row, min, max, read(), out fill);

            Action<float> apply = delegate(float raw)
            {
                float clamped = Mathf.Clamp(raw, min, max);
                object next;
                if (snapToInt)
                {
                    float step = ResolveSliderStep(def, true);
                    int stepped = Mathf.RoundToInt(ShouldSnapSliderToStep(def) ? Mathf.Round(clamped / step) * step : clamped);
                    stepped = Mathf.Clamp(stepped, Mathf.RoundToInt(min), Mathf.RoundToInt(max));
                    next = stepped;
                    clamped = stepped;
                }
                else
                {
                    if (ShouldSnapSliderToStep(def))
                    {
                        float step = ResolveSliderStep(def, false);
                        clamped = Mathf.Round(clamped / step) * step;
                    }
                    next = clamped;
                }

                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, next))
                {
                    value.text = FormatNumeric(def, clamped, snapToInt);
                    UpdateSliderFill(fill, min, max, clamped);
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, _panel);
                }
            };

            AddSliderInput(track, min, max, apply);
            AttachTooltip(track, BuildNumericTooltip(def, min, max, snapToInt));

            if (def.ShowValueInput)
                AddNumericValueInput(value, def, settingsObject, snapToInt, min, max, apply);

            if (def.ShowStepperButtons)
            {
                GameObject dec = CreateButton(row, "Dec", "-", new Vector3(DecreaseX, ControlY, 0f), 18, SmallButtonWidth, SmallButtonHeight, delegate { apply(read() - ResolveButtonStep(def, min, max, snapToInt)); });
                GameObject inc = CreateButton(row, "Inc", "+", new Vector3(IncreaseX, ControlY, 0f), 18, SmallButtonWidth, SmallButtonHeight, delegate { apply(read() + ResolveButtonStep(def, min, max, snapToInt)); });
                AttachTooltip(dec, BuildStepTooltip(def, min, max, snapToInt, false));
                AttachTooltip(inc, BuildStepTooltip(def, min, max, snapToInt, true));
            }
        }

        private void BuildEnumControl(GameObject row, SettingDefinition def, object settingsObject)
        {
            Array values = def.EnumType != null ? Enum.GetValues(def.EnumType) : null;
            Func<object> read = delegate { return SpineWidgetRuntime.GetValue<object>(def, settingsObject); };
            UILabel value = CreateValueLabel(row, FormatObjectValue(read()), new Vector3(TrackCenterX, LabelY, 0f), 140);

            Action<int> cycle = delegate(int delta)
            {
                if (values == null || values.Length == 0)
                    return;

                object current = read();
                int index = Array.IndexOf(values, current);
                if (index < 0) index = 0;
                else index = (index + delta) % values.Length;
                if (index < 0) index += values.Length;

                object selectedValue = values.GetValue(index);
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, selectedValue))
                {
                    value.text = FormatObjectValue(selectedValue);
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, _panel);
                }
            };

            GameObject previousButton = CreateButton(row, "PrevEnum", "<", new Vector3(DecreaseX, ControlY, 0f), 18, SmallButtonWidth, SmallButtonHeight, delegate { cycle(-1); });
            GameObject nextButton = CreateButton(row, "NextEnum", ">", new Vector3(IncreaseX, ControlY, 0f), 18, SmallButtonWidth, SmallButtonHeight, delegate { cycle(1); });
            AttachTooltip(previousButton, "Show the previous option for " + SafeLabel(def) + ".");
            AttachTooltip(nextButton, "Show the next option for " + SafeLabel(def) + ".");
        }

        private void BuildChoiceControl(GameObject row, SettingDefinition def, object settingsObject)
        {
            Func<string> read = delegate { return SpineWidgetRuntime.GetValue<string>(def, settingsObject) ?? "None"; };
            UILabel value = CreateValueLabel(row, read(), new Vector3(TrackCenterX, LabelY, 0f), 140);

            Action<int> cycle = delegate(int delta)
            {
                List<string> options = def.GetOptions != null ? def.GetOptions(settingsObject).ToList() : new List<string>();
                if (options.Count == 0)
                    return;

                string current = read();
                int index = options.FindIndex(delegate(string option) { return string.Equals(option, current, StringComparison.OrdinalIgnoreCase); });
                if (index < 0) index = 0;
                else index = (index + delta) % options.Count;
                if (index < 0) index += options.Count;

                string selectedValue = options[index];
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, selectedValue))
                {
                    value.text = selectedValue;
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, _panel);
                }
            };

            GameObject previousButton = CreateButton(row, "PrevChoice", "<", new Vector3(DecreaseX, ControlY, 0f), 18, SmallButtonWidth, SmallButtonHeight, delegate { cycle(-1); });
            GameObject nextButton = CreateButton(row, "NextChoice", ">", new Vector3(IncreaseX, ControlY, 0f), 18, SmallButtonWidth, SmallButtonHeight, delegate { cycle(1); });
            AttachTooltip(previousButton, "Show the previous option for " + SafeLabel(def) + ".");
            AttachTooltip(nextButton, "Show the next option for " + SafeLabel(def) + ".");
        }

        private void BuildStringControl(GameObject row, SettingDefinition def, object settingsObject)
        {
            string current = SpineWidgetRuntime.GetValue<string>(def, settingsObject) ?? string.Empty;
            CreateTextInput(row, def, settingsObject, current);
        }

        private void BuildActionControl(GameObject row, SettingDefinition def, object settingsObject)
        {
            string label = string.IsNullOrEmpty(def.ActionLabel) ? "Execute" : def.ActionLabel;
            GameObject button = CreateButton(row, "Execute", label, new Vector3(TrackCenterX, LabelY, 0f), 15, 150, 36, delegate
            {
                if (def.OnChanged != null)
                    def.OnChanged(settingsObject);
                _panel.OnSettingChanged();
            });
            AttachTooltip(button, def.Tooltip);
        }

        private void BuildSingleKeybindControl(GameObject row, SettingDefinition def, object settingsObject)
        {
            Func<string> display = delegate { return SpineWidgetRuntime.FormatKeyCode(SpineWidgetRuntime.GetValue<KeyCode>(def, settingsObject)); };
            UILabel value = CreateValueLabel(row, display(), new Vector3(TrackCenterX - 52f, LabelY, 0f), 112);

            KeybindCaptureListener capture = row.AddComponent<KeybindCaptureListener>();
            capture.ValueLabel = value;
            capture.DisplayTextProvider = display;
            capture.OnCanceled = delegate { value.text = display(); };
            capture.OnCaptured = delegate(KeyCode key)
            {
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, key))
                {
                    value.text = display();
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, _panel);
                }
            };

            GameObject button = CreateButton(row, "Rebind", "Rebind", new Vector3(ColumnWidth - 48f, ControlY, 0f), 14, 96, SmallButtonHeight, capture.StartCapture);
            AttachTooltip(button, "Change " + SafeLabel(def) + ".");
        }

        private void BuildColorControl(GameObject row, SettingDefinition def, object settingsObject)
        {
            Color current = SpineWidgetRuntime.GetValue<Color>(def, settingsObject);
            GameObject swatchRoot = _chrome.Ui.CreateChild(row, "ColorSwatch", new Vector3(TrackCenterX - 35f, LabelY, 0f));
            _chrome.Ui.CreateQuad(swatchRoot, "Border", _whiteTexture, Vector3.zero, 76, 34, ColorSwatchBorder, _chrome.Ui.NextDepth());
            UITexture swatch = _chrome.Ui.CreateQuad(swatchRoot, "Color", _whiteTexture, Vector3.zero, 66, 24, current, _chrome.Ui.NextDepth());
            _chrome.Ui.AddClickCollider(swatchRoot, 76, 34, null);

            UILabel value = CreateValueLabel(row, FormatColor(current), new Vector3(TrackCenterX + 64f, LabelY, 0f), 130);
            GameObject edit = CreateButton(row, "EditColor", "Pick", new Vector3(ColumnWidth - 48f, ControlY, 0f), 14, 96, SmallButtonHeight, delegate
            {
                ModColorPickerPanel.Show(current, delegate(Color selected)
                {
                    if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, selected))
                    {
                        current = selected;
                        swatch.color = selected;
                        value.text = FormatColor(selected);
                        SpineWidgetRuntime.NotifyChange(def, settingsObject, _panel);
                    }
                });
            });

            AttachTooltip(swatchRoot, def.Tooltip);
            AttachTooltip(edit, "Choose a color for " + SafeLabel(def) + ".");
        }

        private void BuildReadOnlyValue(GameObject row, string text)
        {
            CreateValueLabel(row, text ?? string.Empty, new Vector3(TrackCenterX, LabelY, 0f), 220);
        }

        private void CreateTextInput(GameObject row, SettingDefinition def, object settingsObject, string current)
        {
            int inputWidth = 220;
            _chrome.Ui.CreateQuad(row, "StringInputBackground", _whiteTexture, new Vector3(TrackCenterX, LabelY, 0f),
                inputWidth, 32, ColorInputBackground, _chrome.Ui.NextDepth());

            UILabel value = CreateValueLabel(row, string.IsNullOrEmpty(current) ? ResolvePlaceholder(def) : current,
                new Vector3(TrackCenterX, LabelY, 0f), inputWidth - 16);
            value.color = string.IsNullOrEmpty(current) ? _chrome.Palette.InkFaded : ColorValue;

            UIInput input = value.gameObject.AddComponent<UIInput>();
            input.label = value;
            input.value = current ?? string.Empty;
            input.activeTextColor = ColorValue;
            input.caretColor = ColorValue;
            input.selectionColor = new Color(0.35f, 0.25f, 0.16f, 0.35f);

            BoxCollider collider = value.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(inputWidth, 32, 1);
            collider.center = Vector3.zero;

            Action apply = delegate
            {
                string next = input.value ?? string.Empty;
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, next))
                {
                    value.text = string.IsNullOrEmpty(next) ? ResolvePlaceholder(def) : next;
                    value.color = string.IsNullOrEmpty(next) ? _chrome.Palette.InkFaded : ColorValue;
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, _panel);
                }
                else
                {
                    input.value = SpineWidgetRuntime.GetValue<string>(def, settingsObject) ?? string.Empty;
                }
            };

            EventDelegate.Add(input.onSubmit, delegate { apply(); });
            GameObject ok = CreateButton(row, "ApplyText", "OK", new Vector3(ColumnWidth - 48f, ControlY, 0f), 14, 96, SmallButtonHeight, delegate
            {
                apply();
                input.RemoveFocus();
            });

            AttachTooltip(value.gameObject, def.Tooltip);
            AttachTooltip(ok, "Apply text for " + SafeLabel(def) + ".");
        }

        private UILabel CreateValueLabel(GameObject parent, string text, Vector3 position, int width)
        {
            UILabel value = _chrome.Ui.CreateLabel(parent, "Value", text ?? string.Empty, position, ValueFontSize, ColorValue, width, 30, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _chrome.Ui.NextDepth());
            value.overflowMethod = UILabel.Overflow.ShrinkContent;
            return value;
        }

        private GameObject CreateSliderTrack(GameObject row, float min, float max, float value, out UITexture fill)
        {
            _chrome.Ui.CreateQuad(row, "SliderTrackShadow", _whiteTexture, new Vector3(TrackCenterX + 1f, ControlY - 1f, 0f), TrackWidth + 6, TrackHeight + 6, new Color(0.08f, 0.04f, 0.02f, 0.28f), _chrome.Ui.NextDepth());
            _chrome.Ui.CreateQuad(row, "SliderTrack", _whiteTexture, new Vector3(TrackCenterX, ControlY, 0f), TrackWidth, TrackHeight, ColorTrack, _chrome.Ui.NextDepth());
            fill = _chrome.Ui.CreateQuad(row, "SliderFill", _whiteTexture, new Vector3(TrackCenterX - TrackWidth * 0.5f, ControlY, 0f), 1, TrackHeight - 4, ColorTrackFill, _chrome.Ui.NextDepth());
            fill.pivot = UIWidget.Pivot.Left;
            UpdateSliderFill(fill, min, max, value);

            GameObject hit = _chrome.Ui.CreateChild(row, "SliderHit", new Vector3(TrackCenterX, ControlY, 0f));
            BoxCollider collider = hit.AddComponent<BoxCollider>();
            collider.size = new Vector3(TrackWidth, 38f, 1f);
            collider.center = Vector3.zero;
            return hit;
        }

        private void AddNumericValueInput(UILabel label, SettingDefinition def, object settingsObject, bool snapToInt, float min, float max, Action<float> apply)
        {
            if (label == null || apply == null)
                return;

            UITexture background = _chrome.Ui.CreateQuad(label.transform.parent.gameObject, "NumericInputBackground", _whiteTexture,
                label.transform.localPosition, ValueWidth + 8, 28, ColorInputBackground, _chrome.Ui.NextDepth());
            background.depth = label.depth - 1;

            UIInput input = label.gameObject.AddComponent<UIInput>();
            input.label = label;
            input.validation = def != null && !string.IsNullOrEmpty(def.UnitSuffix)
                ? UIInput.Validation.None
                : (snapToInt ? UIInput.Validation.Integer : UIInput.Validation.Float);
            input.activeTextColor = ColorValue;
            input.caretColor = ColorValue;
            input.selectionColor = new Color(0.35f, 0.25f, 0.16f, 0.35f);

            BoxCollider collider = label.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(ValueWidth + 8, 28, 1);
            collider.center = Vector3.zero;

            EventDelegate.Add(input.onSubmit, delegate
            {
                float parsed;
                if (TryParseNumericInput(input.value, def, out parsed))
                    apply(Mathf.Clamp(parsed, min, max));

                input.RemoveFocus();
            });

            AttachTooltip(label.gameObject, BuildValueInputTooltip(def, min, max, snapToInt));
        }

        private static bool TryParseNumericInput(string raw, SettingDefinition def, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(raw))
                return false;

            string text = raw.Trim();
            if (def != null && !string.IsNullOrEmpty(def.UnitSuffix) && text.EndsWith(def.UnitSuffix, StringComparison.OrdinalIgnoreCase))
                text = text.Substring(0, text.Length - def.UnitSuffix.Length).Trim();

            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private GameObject CreateButton(GameObject parent, string name, string text, Vector3 position, int fontSize, int width, int height, Action onClick)
        {
            if (_chrome == null || _chrome.Buttons == null || parent == null)
                return null;

            return _chrome.Buttons.Build(parent, name, text, position, width, height, fontSize, onClick);
        }

        private static void UpdateSliderFill(UITexture fill, float min, float max, float value)
        {
            if (fill == null)
                return;

            float normalized = Mathf.Clamp01(Mathf.InverseLerp(min, max, value));
            fill.width = Mathf.Max(1, Mathf.RoundToInt(TrackWidth * normalized));
        }

        private static void AddSliderInput(GameObject hit, float min, float max, Action<float> apply)
        {
            if (hit == null || apply == null)
                return;

            UIEventListener listener = UIEventListener.Get(hit);
            listener.onClick = delegate(GameObject go)
            {
                ApplySliderPointer(hit, min, max, apply);
            };
            listener.onDrag = delegate(GameObject go, Vector2 delta)
            {
                ApplySliderPointer(hit, min, max, apply);
            };
            listener.onPress = delegate(GameObject go, bool pressed)
            {
                if (pressed)
                    ApplySliderPointer(hit, min, max, apply);
            };
        }

        private static void ApplySliderPointer(GameObject hit, float min, float max, Action<float> apply)
        {
            Vector3 world = UICamera.lastHit.point;
            Vector3 local = hit.transform.InverseTransformPoint(world);
            float normalized = Mathf.Clamp01((local.x + TrackWidth * 0.5f) / TrackWidth);
            apply(Mathf.Lerp(min, max, normalized));
        }

        private static bool ShouldSnapSliderToStep(SettingDefinition def)
        {
            return def != null && def.SliderStepMode == SliderStepMode.Stepped;
        }

        private static float ResolveSliderStep(SettingDefinition def, bool snapToInt)
        {
            if (def != null && def.StepSize.HasValue && def.StepSize.Value > 0f)
                return def.StepSize.Value;

            return snapToInt ? 1f : 0.01f;
        }

        private static float ResolveButtonStep(SettingDefinition def, float min, float max, bool snapToInt)
        {
            if (IsShiftHeld() && def != null && def.LargeStepSize.HasValue && def.LargeStepSize.Value > 0f)
                return def.LargeStepSize.Value;
            if (def != null && def.FineStepSize.HasValue && def.FineStepSize.Value > 0f)
                return def.FineStepSize.Value;
            if (def != null && def.StepSize.HasValue && def.StepSize.Value > 0f)
                return def.StepSize.Value;

            return snapToInt ? 1f : Mathf.Max(0.01f, Mathf.Abs(max - min) / 20f);
        }

        private static bool IsShiftHeld()
        {
            return UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
        }

        private static string FormatNumeric(SettingDefinition def, float value, bool snapToInt)
        {
            string format = def != null ? def.ValueFormat : null;
            string formatted;
            if (snapToInt)
                formatted = string.IsNullOrEmpty(format)
                    ? Mathf.RoundToInt(value).ToString()
                    : value.ToString(format, CultureInfo.InvariantCulture);
            else
                formatted = string.IsNullOrEmpty(format)
                    ? value.ToString("0.00", CultureInfo.InvariantCulture)
                    : value.ToString(format, CultureInfo.InvariantCulture);

            if (def != null && !string.IsNullOrEmpty(def.UnitSuffix))
                formatted += def.UnitSuffix;

            return formatted;
        }

        private static string FormatBool(SettingDefinition def, bool value)
        {
            if (value)
                return def != null && !string.IsNullOrEmpty(def.TrueLabel) ? def.TrueLabel : "ON";

            return def != null && !string.IsNullOrEmpty(def.FalseLabel) ? def.FalseLabel : "OFF";
        }

        private static string FormatObjectValue(object value)
        {
            return value != null ? value.ToString() : string.Empty;
        }

        private static string FormatColor(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(color);
        }

        private static string ResolvePlaceholder(SettingDefinition def)
        {
            return def != null && !string.IsNullOrEmpty(def.Placeholder) ? def.Placeholder : "Enter text";
        }

        private static string SafeLabel(SettingDefinition def)
        {
            if (def == null)
                return "this setting";
            if (!string.IsNullOrEmpty(def.Label))
                return def.Label;
            return string.IsNullOrEmpty(def.Id) ? "this setting" : def.Id;
        }

        private static string BuildNumericTooltip(SettingDefinition def, float min, float max, bool snapToInt)
        {
            string text = def != null ? def.Tooltip : null;
            string range = "Range: " + FormatNumeric(def, min, snapToInt) + " to " + FormatNumeric(def, max, snapToInt) + ".";
            string step = def != null && def.SliderStepMode == SliderStepMode.Stepped
                ? " Slider snaps to " + FormatNumeric(def, ResolveSliderStep(def, snapToInt), snapToInt) + " increments."
                : " Drag for fine adjustment.";

            return string.IsNullOrEmpty(text) ? range + step : text + "\n" + range + step;
        }

        private static string BuildValueInputTooltip(SettingDefinition def, float min, float max, bool snapToInt)
        {
            return BuildNumericTooltip(def, min, max, snapToInt) + "\nClick the value to type an exact number.";
        }

        private static string BuildStepTooltip(SettingDefinition def, float min, float max, bool snapToInt, bool increase)
        {
            float step = ResolveButtonStep(def, min, max, snapToInt);
            string direction = increase ? "Increase" : "Decrease";
            string text = direction + " by " + FormatNumeric(def, step, snapToInt) + ".";
            if (def != null && def.LargeStepSize.HasValue && def.LargeStepSize.Value > 0f)
                text += " Hold Shift for " + FormatNumeric(def, def.LargeStepSize.Value, snapToInt) + ".";
            return text;
        }

        private static void AttachTooltip(GameObject target, string text)
        {
            SpineWidgetRuntime.SetTooltip(target, text);
        }

        private static void UpdateButtonLabel(GameObject button, string text)
        {
            if (button == null)
                return;

            UILabel[] labels = button.GetComponentsInChildren<UILabel>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].enabled)
                    labels[i].text = text ?? string.Empty;
            }
        }
    }
}
