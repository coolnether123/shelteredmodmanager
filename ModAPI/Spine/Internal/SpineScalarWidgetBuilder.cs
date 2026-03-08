using System;
using ModAPI.Core;
using ModAPI.Spine;
using ModAPI.Spine.UI;
using ModAPI.UI;
using UnityEngine;

namespace ModAPI.Internal.SpineUI
{
    internal static class SpineScalarWidgetBuilder
    {
        public static GameObject CreateBoolWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            container.name = "Bool_" + def.Id;
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, Vector3.zero);
            label.pivot = UIWidget.Pivot.Left;
            SpineWidgetRuntime.SetTooltip(label.gameObject, def.Tooltip);

            var valueLabel = UIUtil.CreateLabelQuick(container, string.Empty, 16, new Vector3(210, 0, 0));
            valueLabel.alignment = NGUIText.Alignment.Center;
            valueLabel.width = 100;

            UIUtil.CreateButton(container, SpineWidgetFactory.ButtonTemplate, "TOGGLE", 100, 40, new Vector3(330, 0, 0), () =>
            {
                var value = !SpineWidgetRuntime.GetValue<bool>(def, settingsObject);
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, value))
                {
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, panel);
                    UpdateBoolText(valueLabel, value);
                }
            });

            UpdateBoolText(valueLabel, SpineWidgetRuntime.GetValue<bool>(def, settingsObject));
            return container;
        }

        public static GameObject CreateStringWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            container.name = "String_" + def.Id;
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, Vector3.zero);
            label.pivot = UIWidget.Pivot.Left;
            SpineWidgetRuntime.SetTooltip(label.gameObject, def.Tooltip);

            var inputObject = NGUITools.AddChild(container);
            inputObject.transform.localPosition = new Vector3(200, 0, 0);

            var inputLabel = inputObject.AddComponent<UILabel>();
            inputLabel.fontSize = 16;
            inputLabel.overflowMethod = UILabel.Overflow.ResizeFreely;
            inputLabel.depth = SpineWidgetRuntime.WidgetDepth;

            if (label.bitmapFont != null) inputLabel.bitmapFont = label.bitmapFont;
            else if (label.trueTypeFont != null) inputLabel.trueTypeFont = label.trueTypeFont;

            if (inputLabel.bitmapFont == null)
            {
                inputLabel.text = SpineWidgetRuntime.GetValue<string>(def, settingsObject) ?? string.Empty;
                inputLabel.color = new Color(0.75f, 0.75f, 0.75f);
                MMLog.WarnOnce("SpineWidgetFactory.StringInput.BitmapMissing",
                    "[Settings] Bitmap font unavailable; string text input disabled for compatibility.");
                return container;
            }

            var input = inputObject.AddComponent<UIInput>();
            input.label = inputLabel;
            input.value = SpineWidgetRuntime.GetValue<string>(def, settingsObject) ?? string.Empty;

            var box = inputObject.AddComponent<BoxCollider>();
            box.size = new Vector3(180, 30, 1);
            box.center = new Vector3(90, 0, 0);

            var background = inputObject.AddComponent<UISprite>();
            background.spriteName = "Blank";
            background.width = 180;
            background.height = 30;
            background.depth = SpineWidgetRuntime.WidgetDepth - 1;
            background.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            Action handleSubmit = () =>
            {
                var value = input.value;
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, value))
                {
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, panel);
                }
                else
                {
                    input.value = SpineWidgetRuntime.GetValue<string>(def, settingsObject);
                }
            };

            EventDelegate.Add(input.onSubmit, () =>
            {
                handleSubmit();
                if (UICamera.selectedObject == input.gameObject)
                {
                    UICamera.selectedObject = null;
                }
            });

            var okButton = UIUtil.CreateButton(container, SpineWidgetFactory.ButtonTemplate, "OK", 40, 30, new Vector3(390, 0, 0), () =>
            {
                handleSubmit();
                input.RemoveFocus();
                if (UICamera.selectedObject == input.gameObject)
                {
                    UICamera.selectedObject = null;
                }
            });

            if (okButton != null)
            {
                okButton.gameObject.SetActive(false);
                UIEventListener.Get(input.gameObject).onSelect += (go, selected) =>
                {
                    if (selected) okButton.gameObject.SetActive(true);
                    else if (UICamera.hoveredObject != okButton.gameObject) okButton.gameObject.SetActive(false);
                };
            }

            return container;
        }

        public static GameObject CreateNumericIntWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            container.name = "Numeric_" + def.Id;
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, Vector3.zero);
            label.pivot = UIWidget.Pivot.Left;
            SpineWidgetRuntime.SetTooltip(label.gameObject, def.Tooltip);

            var valueLabel = UIUtil.CreateLabelQuick(container, "0", 16, new Vector3(200, 0, 0));
            valueLabel.alignment = NGUIText.Alignment.Center;

            Action refresh = () => valueLabel.text = SpineWidgetRuntime.GetValue<int>(def, settingsObject).ToString();
            refresh();

            UIUtil.CreateButton(container, SpineWidgetFactory.ButtonTemplate, "-", 40, 40, new Vector3(150, 0, 0), () =>
            {
                var step = def.StepSize.HasValue && def.StepSize.Value > 0 ? (int)def.StepSize.Value : 1;
                var value = SpineWidgetRuntime.GetValue<int>(def, settingsObject) - step;
                if (def.MinValue.HasValue) value = Mathf.Max(value, (int)def.MinValue.Value);
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, value))
                {
                    refresh();
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, panel);
                }
            });

            UIUtil.CreateButton(container, SpineWidgetFactory.ButtonTemplate, "+", 40, 40, new Vector3(250, 0, 0), () =>
            {
                var step = def.StepSize.HasValue && def.StepSize.Value > 0 ? (int)def.StepSize.Value : 1;
                var value = SpineWidgetRuntime.GetValue<int>(def, settingsObject) + step;
                if (def.MaxValue.HasValue) value = Mathf.Min(value, (int)def.MaxValue.Value);
                if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, value))
                {
                    refresh();
                    SpineWidgetRuntime.NotifyChange(def, settingsObject, panel);
                }
            });

            return container;
        }

        public static GameObject CreateSliderWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel, bool snapToInt)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            container.name = (snapToInt ? "Int_" : "Float_") + def.Id;
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, new Vector3(0, 5, 0));
            label.pivot = UIWidget.Pivot.Left;
            SpineWidgetRuntime.SetTooltip(label.gameObject, def.Tooltip);

            var valueLabel = UIUtil.CreateLabelQuick(container, string.Empty, 16, new Vector3(210, -20, 0));
            valueLabel.alignment = NGUIText.Alignment.Right;
            valueLabel.width = 60;

            var canUseInput = valueLabel.bitmapFont != null;
            UIInput valueInput = null;

            var min = def.MinValue ?? 0f;
            var max = def.MaxValue ?? (snapToInt ? 100f : 1f);
            UISlider slider = null;

            Action<float> refreshDisplay = value =>
            {
                string display;
                if (snapToInt)
                {
                    var intValue = Mathf.RoundToInt(value);
                    display = intValue.ToString();
                    var normalized = Mathf.InverseLerp(min, max, intValue);
                    if (slider != null) slider.value = normalized;
                }
                else
                {
                    display = value.ToString("F2");
                    var normalized = Mathf.InverseLerp(min, max, value);
                    if (slider != null) slider.value = normalized;
                }

                valueLabel.text = display;
                if (canUseInput && valueInput != null)
                {
                    valueInput.value = display;
                }
            };

            if (canUseInput)
            {
                var inputBackground = NGUITools.AddChild<UISprite>(container);
                inputBackground.name = "InputBG";
                inputBackground.spriteName = "Blank";
                inputBackground.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
                inputBackground.width = 65;
                inputBackground.height = 24;
                inputBackground.depth = SpineWidgetRuntime.WidgetDepth - 5;
                inputBackground.transform.localPosition = new Vector3(210, -20, 0);

                var collider = valueLabel.gameObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(65, 24, 1);
                collider.center = Vector3.zero;

                valueInput = valueLabel.gameObject.AddComponent<UIInput>();
                valueInput.label = valueLabel;
                valueInput.activeTextColor = new Color(0.7f, 1f, 0.7f);
                valueInput.validation = snapToInt ? UIInput.Validation.Integer : UIInput.Validation.Float;

                Action handleSubmit = () =>
                {
                    float parsed;
                    if (float.TryParse(valueInput.value, out parsed))
                    {
                        parsed = Mathf.Clamp(parsed, min, max);
                        if (snapToInt)
                        {
                            var intValue = Mathf.RoundToInt(parsed);
                            if (def.StepSize.HasValue && def.StepSize.Value > 0)
                            {
                                var step = def.StepSize.Value;
                                intValue = Mathf.RoundToInt(Mathf.Round(parsed / step) * step);
                            }

                            if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, intValue))
                            {
                                SpineWidgetRuntime.NotifyChange(def, settingsObject, panel);
                            }
                        }
                        else if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, parsed))
                        {
                            SpineWidgetRuntime.NotifyChange(def, settingsObject, panel);
                        }
                    }

                    valueInput.isSelected = false;
                    var currentValue = snapToInt
                        ? (float)SpineWidgetRuntime.GetValue<int>(def, settingsObject)
                        : SpineWidgetRuntime.GetValue<float>(def, settingsObject);
                    refreshDisplay(currentValue);
                };

                var okButton = UIUtil.CreateButton(container, SpineWidgetFactory.ButtonTemplate, "OK", 30, 24, new Vector3(260, -20, 0), () =>
                {
                    handleSubmit();
                    valueInput.RemoveFocus();
                    if (UICamera.selectedObject == valueInput.gameObject)
                    {
                        UICamera.selectedObject = null;
                    }
                });

                if (okButton != null)
                {
                    okButton.gameObject.SetActive(false);
                    UIEventListener.Get(valueInput.gameObject).onSelect += (go, selected) =>
                    {
                        if (selected) okButton.gameObject.SetActive(true);
                        else if (UICamera.hoveredObject != okButton.gameObject) okButton.gameObject.SetActive(false);
                    };
                }

                EventDelegate.Add(valueInput.onSubmit, () =>
                {
                    handleSubmit();
                    if (UICamera.selectedObject == valueInput.gameObject)
                    {
                        UICamera.selectedObject = null;
                    }
                });
            }
            else
            {
                MMLog.WarnOnce("SpineWidgetFactory.SliderInput.BitmapMissing",
                    "[Settings] Bitmap font unavailable; numeric text entry disabled for compatibility.");
            }

            if (SpineWidgetFactory.SliderTemplate != null)
            {
                var sliderObject = UnityEngine.Object.Instantiate(SpineWidgetFactory.SliderTemplate);
                sliderObject.transform.parent = container.transform;
                NGUITools.SetLayer(sliderObject, container.layer);
                sliderObject.transform.localScale = Vector3.one;
                sliderObject.transform.localPosition = new Vector3(10, -20, 0);

                slider = sliderObject.GetComponentInChildren<UISlider>();
                if (slider != null) slider.numberOfSteps = 0;

                var sliderWidget = sliderObject.GetComponent<UIWidget>();
                if (sliderWidget != null)
                {
                    sliderWidget.width = 200;
                    sliderWidget.depth = SpineWidgetRuntime.WidgetDepth;
                }
            }

            var current = snapToInt
                ? SpineWidgetRuntime.GetValue<int>(def, settingsObject)
                : SpineWidgetRuntime.GetValue<float>(def, settingsObject);
            refreshDisplay(current);

            if (slider != null)
            {
                EventDelegate.Add(slider.onChange, () =>
                {
                    var value = Mathf.Lerp(min, max, slider.value);
                    if (snapToInt)
                    {
                        var step = def.StepSize.HasValue && def.StepSize.Value > 0 ? (int)def.StepSize.Value : 1;
                        var raw = Mathf.RoundToInt(value);
                        if (step > 1)
                        {
                            var offset = raw - (int)min;
                            var remainder = offset % step;
                            raw -= remainder;
                            if (remainder > step / 2) raw += step;
                        }

                        if (raw < (int)min) raw = (int)min;
                        if (raw > (int)max) raw = (int)max;
                        valueLabel.text = raw.ToString();
                        if (canUseInput && valueInput != null) valueInput.value = raw.ToString();

                        if (SpineWidgetRuntime.GetValue<int>(def, settingsObject) != raw)
                        {
                            if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, raw))
                            {
                                SpineWidgetRuntime.NotifyChange(def, settingsObject, panel);
                            }
                            else
                            {
                                refreshDisplay(SpineWidgetRuntime.GetValue<int>(def, settingsObject));
                            }
                        }
                    }
                    else
                    {
                        var step = def.StepSize.HasValue && def.StepSize.Value > 0 ? def.StepSize.Value : 0.01f;
                        if (step > 0) value = Mathf.Round(value / step) * step;
                        if (value < min) value = min;
                        if (value > max) value = max;
                        valueLabel.text = value.ToString("F2");
                        if (canUseInput && valueInput != null) valueInput.value = value.ToString("F2");

                        if (Mathf.Abs(SpineWidgetRuntime.GetValue<float>(def, settingsObject) - value) > 0.001f)
                        {
                            if (SpineWidgetRuntime.TryApplyValue(def, settingsObject, value))
                            {
                                SpineWidgetRuntime.NotifyChange(def, settingsObject, panel);
                            }
                            else
                            {
                                refreshDisplay(SpineWidgetRuntime.GetValue<float>(def, settingsObject));
                            }
                        }
                    }
                });
            }

            return container;
        }

        private static void UpdateBoolText(UILabel label, bool value)
        {
            label.text = value ? "[C0FFC0]ON[-]" : "[FFC0C0]OFF[-]";
        }
    }
}
