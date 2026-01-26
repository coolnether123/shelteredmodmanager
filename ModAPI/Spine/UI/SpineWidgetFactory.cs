using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ModAPI.Spine;
using ModAPI.UI;
using ModAPI.Core;

namespace ModAPI.Spine.UI
{
    public static class SpineWidgetFactory
    {
        public static GameObject ButtonTemplate;
        public static GameObject SliderTemplate;

        private const int WIDGET_DEPTH = 10020;

        public static GameObject CreateWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel = null)
        {
            MMLog.WriteDebug($"[SpineWidgetFactory] CreateWidget() - Id={def.Id}, Type={def.Type}, Label='{def.Label}'");

            try
            {
                GameObject result = null;
                switch (def.Type)
                {
                    case SettingType.Bool: result = CreateBoolWidget(def, parent, settingsObject, panel); break;
                    case SettingType.Float: result = CreateSliderWidget(def, parent, settingsObject, panel, false); break;
                    case SettingType.Int: result = CreateSliderWidget(def, parent, settingsObject, panel, true); break;
                    case SettingType.String: result = CreateStringWidget(def, parent, settingsObject, panel); break;
                    case SettingType.Enum: result = CreateEnumWidget(def, parent, settingsObject, panel); break;
                    case SettingType.Color: result = UIUtil.CreateLabelQuick(parent.gameObject, $"{def.Label}: Color Picker Disabled", 14, Vector3.zero).gameObject; break;
                    case SettingType.Button: result = CreateButtonWidget(def, parent, settingsObject); break;
                    case SettingType.Header: result = CreateHeaderWidget(def, parent); break;
                    case SettingType.Spacer: result = new GameObject("Spacer_" + def.Id) { transform = { parent = parent } }; break;
                    case SettingType.NumericInt: result = CreateNumericIntWidget(def, parent, settingsObject, panel); break;
                    case SettingType.Choice: result = CreateChoiceWidget(def, parent, settingsObject, panel); break;
                    default: result = UIUtil.CreateLabelQuick(parent.gameObject, $"Unknown: {def.Type}", 14, Vector3.zero).gameObject; break;
                }

                if (result != null)
                {
                    if (string.IsNullOrEmpty(result.name) || result.name.StartsWith("New Game Object"))
                        result.name = def.Type.ToString() + "_" + def.Id;
                }
                return result;
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[SpineWidgetFactory] Error creating {def.Id}: {ex.Message}\n{ex.StackTrace}");
                return UIUtil.CreateLabelQuick(parent.gameObject, $"Error: {def.Id}", 14, Vector3.zero).gameObject;
            }
        }

        private static void NotifyChange(SettingDefinition def, object settingsObject, ModSettingsPanel panel)
        {
            MMLog.WriteDebug($"[Spine] NotifyChange for {def.Id}. Signal to panel: {panel != null}");
            def.OnChanged?.Invoke(settingsObject);
            if (def.RequiresRestart) MMLog.WriteInfo($"[Settings] {def.Label} requires restart.");
            if (panel != null) 
            {
                panel.OnSettingChanged();
                if (def.ControlsChildVisibility) panel.RefreshDependents(def.Id);
            }
        }

        private static void SetTooltip(GameObject go, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (go.GetComponent<Collider>() == null) NGUITools.AddWidgetCollider(go);

            UIEventListener.Get(go).onHover += (obj, isOver) => {
                if (isOver) ModTooltip.Show(text);
                else ModTooltip.Hide();
            };
        }

        private static GameObject CreateBoolWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            container.name = "Bool_" + def.Id;
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, Vector3.zero);
            label.pivot = UIWidget.Pivot.Left;
            SetTooltip(label.gameObject, def.Tooltip);

            var valLabel = UIUtil.CreateLabelQuick(container, "", 16, new Vector3(200, 0, 0));
            valLabel.alignment = NGUIText.Alignment.Center;

            UIUtil.CreateButton(container, ButtonTemplate, "TOGGLE", 100, 40, new Vector3(300, 0, 0), () => {
                bool v = !GetValue<bool>(def, settingsObject);
                if (TryApplyValue(def, settingsObject, v)) {
                    NotifyChange(def, settingsObject, panel);
                    UpdateBoolText(valLabel, v);
                }
            });

            UpdateBoolText(valLabel, GetValue<bool>(def, settingsObject));
            return container;
        }

        private static void UpdateBoolText(UILabel label, bool val)
        {
            label.text = val ? "[C0FFC0]ON[-]" : "[FFC0C0]OFF[-]";
        }

        private static GameObject CreateStringWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            container.name = "String_" + def.Id;
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, Vector3.zero);
            label.pivot = UIWidget.Pivot.Left;
            SetTooltip(label.gameObject, def.Tooltip);

            var inputGO = NGUITools.AddChild(container);
            inputGO.transform.localPosition = new Vector3(200, 0, 0);

            var inputLabel = inputGO.AddComponent<UILabel>();
            inputLabel.fontSize = 16;
            inputLabel.overflowMethod = UILabel.Overflow.ResizeFreely;
            inputLabel.depth = WIDGET_DEPTH;

            if (label.bitmapFont != null) inputLabel.bitmapFont = label.bitmapFont;
            else if (label.trueTypeFont != null) inputLabel.trueTypeFont = label.trueTypeFont;

            var input = inputGO.AddComponent<UIInput>();
            input.label = inputLabel;
            input.value = GetValue<string>(def, settingsObject) ?? "";

            var box = inputGO.AddComponent<BoxCollider>();
            box.size = new Vector3(180, 30, 1);
            box.center = new Vector3(90, 0, 0);

            var bg = inputGO.AddComponent<UISprite>();
            bg.spriteName = "Blank";
            bg.width = 180;
            bg.height = 30;
            bg.depth = WIDGET_DEPTH - 1;
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            Action handleStringSubmit = () => {
                string val = input.value;
                if (TryApplyValue(def, settingsObject, val)) {
                    NotifyChange(def, settingsObject, panel);
                } else {
                    input.value = GetValue<string>(def, settingsObject);
                }
            };

            EventDelegate.Add(input.onSubmit, () => {
                handleStringSubmit();
                if (UICamera.selectedObject == input.gameObject) UICamera.selectedObject = null;
            });

            var okBtn = UIUtil.CreateButton(container, ButtonTemplate, "OK", 40, 30, new Vector3(390, 0, 0), () => {
                handleStringSubmit();
                input.RemoveFocus();
                if (UICamera.selectedObject == input.gameObject) UICamera.selectedObject = null;
            });
            
            if (okBtn != null) {
                okBtn.gameObject.SetActive(false);
                UIEventListener.Get(input.gameObject).onSelect += (go, selected) => {
                    if (selected) okBtn.gameObject.SetActive(true);
                    else if (UICamera.hoveredObject != okBtn.gameObject) okBtn.gameObject.SetActive(false);
                };
            }

            return container;
        }

        private static GameObject CreateNumericIntWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            container.name = "Numeric_" + def.Id;
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, Vector3.zero);
            label.pivot = UIWidget.Pivot.Left;
            SetTooltip(label.gameObject, def.Tooltip);

            var valLabel = UIUtil.CreateLabelQuick(container, "0", 16, new Vector3(200, 0, 0));
            valLabel.alignment = NGUIText.Alignment.Center;

            void Refresh() => valLabel.text = GetValue<int>(def, settingsObject).ToString();
            Refresh();

            UIUtil.CreateButton(container, ButtonTemplate, "-", 40, 40, new Vector3(150, 0, 0), () => {
                int step = (def.StepSize.HasValue && def.StepSize.Value > 0) ? (int)def.StepSize.Value : 1;
                int v = GetValue<int>(def, settingsObject) - step;
                if (def.MinValue.HasValue) v = Mathf.Max(v, (int)def.MinValue.Value);
                if (TryApplyValue(def, settingsObject, v)) {
                    Refresh();
                    NotifyChange(def, settingsObject, panel);
                }
            });

            UIUtil.CreateButton(container, ButtonTemplate, "+", 40, 40, new Vector3(250, 0, 0), () => {
                int step = (def.StepSize.HasValue && def.StepSize.Value > 0) ? (int)def.StepSize.Value : 1;
                int v = GetValue<int>(def, settingsObject) + step;
                if (def.MaxValue.HasValue) v = Mathf.Min(v, (int)def.MaxValue.Value);
                if (TryApplyValue(def, settingsObject, v)) {
                    Refresh();
                    NotifyChange(def, settingsObject, panel);
                }
            });

            return container;
        }

        private static GameObject CreateSliderWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel, bool snapToInt)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            container.name = (snapToInt ? "Int_" : "Float_") + def.Id;
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, new Vector3(0, 5, 0));
            label.pivot = UIWidget.Pivot.Left;
            SetTooltip(label.gameObject, def.Tooltip);

            var valueLabel = UIUtil.CreateLabelQuick(container, "", 16, new Vector3(210, -20, 0));
            valueLabel.alignment = NGUIText.Alignment.Right;
            valueLabel.width = 60;
            
            var inputBG = NGUITools.AddChild<UISprite>(container);
            inputBG.name = "InputBG";
            inputBG.spriteName = "Blank";
            inputBG.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            inputBG.width = 65;
            inputBG.height = 24;
            inputBG.depth = WIDGET_DEPTH - 5;
            inputBG.transform.localPosition = new Vector3(210, -20, 0);

            var valCol = valueLabel.gameObject.AddComponent<BoxCollider>();
            valCol.size = new Vector3(65, 24, 1);
            valCol.center = Vector3.zero;

            var valInput = valueLabel.gameObject.AddComponent<UIInput>();
            valInput.label = valueLabel;
            valInput.activeTextColor = new Color(0.7f, 1f, 0.7f);
            valInput.validation = snapToInt ? UIInput.Validation.Integer : UIInput.Validation.Float;
            
             float min = def.MinValue ?? 0f;
             float max = def.MaxValue ?? (snapToInt ? 100f : 1f);
             UISlider slider = null;

            void RefreshDisplay(float val)
            {
                if (snapToInt)
                {
                    int iv = Mathf.RoundToInt(val);
                    valInput.value = iv.ToString();
                    float norm = Mathf.InverseLerp(min, max, iv);
                    if (slider != null) slider.value = norm;
                }
                else
                {
                    valInput.value = val.ToString("F2");
                    float norm = Mathf.InverseLerp(min, max, val);
                    if (slider != null) slider.value = norm;
                }
            }

            Action handleSliderSubmit = () => {
                string text = valInput.value;
                if (float.TryParse(text, out float fVal))
                {
                    fVal = Mathf.Clamp(fVal, min, max);
                    if (snapToInt)
                    {
                        int iVal = Mathf.RoundToInt(fVal);
                        if (def.StepSize.HasValue && def.StepSize.Value > 0)
                        {
                            float step = def.StepSize.Value;
                            iVal = Mathf.RoundToInt(Mathf.Round(fVal / step) * step);
                        }
                        if (TryApplyValue(def, settingsObject, iVal)) NotifyChange(def, settingsObject, panel);
                    }
                    else
                    {
                        if (TryApplyValue(def, settingsObject, fVal)) NotifyChange(def, settingsObject, panel);
                    }
                }
                valInput.isSelected = false; 
                float currentVal = snapToInt ? (float)GetValue<int>(def, settingsObject) : GetValue<float>(def, settingsObject);
                RefreshDisplay(currentVal);
            };

            var okBtn = UIUtil.CreateButton(container, ButtonTemplate, "OK", 30, 24, new Vector3(260, -20, 0), () => {
                 handleSliderSubmit();
                 valInput.RemoveFocus();
                 if (UICamera.selectedObject == valInput.gameObject) UICamera.selectedObject = null;
            });

            if (okBtn != null) {
                okBtn.gameObject.SetActive(false);
                UIEventListener.Get(valInput.gameObject).onSelect += (go, selected) => {
                    if (selected) okBtn.gameObject.SetActive(true);
                    else if (UICamera.hoveredObject != okBtn.gameObject) okBtn.gameObject.SetActive(false);
                };
            }

            if (SliderTemplate != null)
            {
                var sliderGO = UnityEngine.Object.Instantiate(SliderTemplate);
                sliderGO.transform.parent = container.transform;
                NGUITools.SetLayer(sliderGO, container.layer);
                sliderGO.transform.localScale = Vector3.one;
                sliderGO.transform.localPosition = new Vector3(10, -20, 0); 

                slider = sliderGO.GetComponentInChildren<UISlider>();
                if (slider != null) slider.numberOfSteps = 0;

                var sliderWidget = sliderGO.GetComponent<UIWidget>();
                if (sliderWidget != null) { sliderWidget.width = 200; sliderWidget.depth = WIDGET_DEPTH; }
            }

            EventDelegate.Add(valInput.onSubmit, () => {
                handleSliderSubmit();
                if (UICamera.selectedObject == valInput.gameObject) UICamera.selectedObject = null;
            });

            float current = snapToInt ? GetValue<int>(def, settingsObject) : GetValue<float>(def, settingsObject);
            RefreshDisplay(current);

            if (slider != null)
            {
                EventDelegate.Add(slider.onChange, () => {
                    float val = Mathf.Lerp(min, max, slider.value);
                    if (snapToInt)
                    {
                        int step = (def.StepSize.HasValue && def.StepSize.Value > 0) ? (int)def.StepSize.Value : 1;
                        int raw = Mathf.RoundToInt(val);
                        if (step > 1) {
                            int offset = raw - (int)min;
                            int remainder = offset % step;
                            raw = raw - remainder;
                            if (remainder > step / 2) raw += step;
                        }
                        if (raw < (int)min) raw = (int)min;
                        if (raw > (int)max) raw = (int)max;
                        valInput.value = raw.ToString();
                        if (GetValue<int>(def, settingsObject) != raw)
                        {
                            if (TryApplyValue(def, settingsObject, raw)) NotifyChange(def, settingsObject, panel);
                            else RefreshDisplay(GetValue<int>(def, settingsObject));
                        }
                    }
                    else
                    {
                        float step = (def.StepSize.HasValue && def.StepSize.Value > 0) ? def.StepSize.Value : 0.01f;
                        if (step > 0) val = Mathf.Round(val / step) * step;
                        if (val < min) val = min;
                        if (val > max) val = max;
                        valInput.value = val.ToString("F2");
                        if (Mathf.Abs(GetValue<float>(def, settingsObject) - val) > 0.001f)
                        {
                            if (TryApplyValue(def, settingsObject, val)) NotifyChange(def, settingsObject, panel);
                            else RefreshDisplay(GetValue<float>(def, settingsObject));
                        }
                    }
                });
            }
            return container;
        }

        private static GameObject CreateEnumWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, Vector3.zero);
            label.pivot = UIWidget.Pivot.Left;
            SetTooltip(label.gameObject, def.Tooltip);

            var valLabel = UIUtil.CreateLabelQuick(container, "", 16, new Vector3(200, 0, 0));
            valLabel.alignment = NGUIText.Alignment.Center;

            void Refresh() => valLabel.text = GetValue<object>(def, settingsObject)?.ToString();
            Refresh();

            UIUtil.CreateButton(container, ButtonTemplate, "CYCLE", 100, 40, new Vector3(300, 0, 0), () => {
                var vals = Enum.GetValues(def.EnumType);
                int idx = Array.IndexOf(vals, GetValue<object>(def, settingsObject));
                idx = (idx + 1) % vals.Length;
                var next = vals.GetValue(idx);
                if (TryApplyValue(def, settingsObject, next)) {
                    Refresh();
                    NotifyChange(def, settingsObject, panel);
                }
            });

            return container;
        }

        private static GameObject CreateChoiceWidget(SettingDefinition def, Transform parent, object settingsObject, ModSettingsPanel panel)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            NGUITools.SetLayer(container, parent.gameObject.layer);

            var label = UIUtil.CreateLabelQuick(container, def.Label, 16, Vector3.zero);
            label.pivot = UIWidget.Pivot.Left;
            SetTooltip(label.gameObject, def.Tooltip);

            var valLabel = UIUtil.CreateLabelQuick(container, "", 16, new Vector3(200, 0, 0));
            valLabel.alignment = NGUIText.Alignment.Center;

            void Refresh() => valLabel.text = GetValue<string>(def, settingsObject) ?? "None";
            Refresh();

            UIUtil.CreateButton(container, ButtonTemplate, "CYCLE", 100, 40, new Vector3(300, 0, 0), () => {
                var options = def.GetOptions?.Invoke(settingsObject)?.ToList() ?? new List<string>();
                if (options.Count == 0) return;
                string current = GetValue<string>(def, settingsObject);
                int idx = options.IndexOf(current);
                idx = (idx + 1) % options.Count;
                string next = options[idx];
                if (TryApplyValue(def, settingsObject, next)) {
                    Refresh();
                    NotifyChange(def, settingsObject, panel);
                }
            });

            return container;
        }

        private static GameObject CreateButtonWidget(SettingDefinition def, Transform parent, object settingsObject)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            NGUITools.SetLayer(container, parent.gameObject.layer);
            UIUtil.CreateButton(container, ButtonTemplate, def.Label, 300, 45, Vector3.zero, () => def.OnChanged?.Invoke(settingsObject));
            SetTooltip(container, def.Tooltip);
            return container;
        }

        private static GameObject CreateHeaderWidget(SettingDefinition def, Transform parent)
        {
            var container = NGUITools.AddChild(parent.gameObject);
            NGUITools.SetLayer(container, parent.gameObject.layer);
            var lbl = UIUtil.CreateLabelQuick(container, def.Label.ToUpper(), 18, Vector3.zero);
            lbl.pivot = UIWidget.Pivot.Left;
            lbl.color = def.HeaderColor ?? new Color(1f, 0.8f, 0.2f);
            return container;
        }

        private static T GetValue<T>(SettingDefinition def, object obj)
        {
            var field = obj.GetType().GetField(def.FieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) 
            {
                object val = field.GetValue(obj);
                if (val is T typed) return typed;
                try { return (T)Convert.ChangeType(val, typeof(T)); } catch { }
            }
            return (T)(def.DefaultValue ?? (typeof(T) == typeof(Color) ? (object)Color.white : default(T)));
        }

        private static bool TryApplyValue(SettingDefinition def, object settingsObject, object newVal)
        {
            if (def.Validate != null && !def.Validate(newVal, settingsObject)) return false;
            SetValue(def, settingsObject, newVal);
            return true;
        }

        private static void SetValue(SettingDefinition def, object obj, object val)
        {
            var field = obj.GetType().GetField(def.FieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return;
            try 
            {
                var converted = Convert.ChangeType(val, field.FieldType);
                field.SetValue(obj, converted);
            }
            catch { field.SetValue(obj, val); }
        }
    }
}
