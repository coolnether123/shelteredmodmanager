using System;
using ModAPI.Internal.SpineUI;
using ModAPI.Spine;
using ModAPI.Spine.UI;
using UnityEngine;

namespace ModAPI.Internal.UI
{
    internal delegate GameObject ModSettingsButtonFactory(
        Transform parent,
        string name,
        string text,
        Vector3 pos,
        int fontSize,
        Color color,
        UIFont uiFont,
        Font ttfFont,
        int width,
        int height,
        Action onClick);

    internal sealed class ModSettingsKeybindWidgetBuilder
    {
        private const float SectionHeaderLocalX = 76f;
        private const int KeySlotWidth = 158;
        private const int KeySlotHeight = 38;
        private const int ActionLabelWidth = 250;
        private const int SmallButtonWidth = 96;
        private const int SmallButtonHeight = 38;

        private readonly GameObject _contentRoot;
        private readonly ISettingsProvider _settingsProvider;
        private readonly object _settingsObject;
        private readonly Texture2D _whiteTexture;
        private readonly UIFont _bitmapFont;
        private readonly Font _ttfFont;
        private readonly Color _textColor;
        private readonly Color _subtextColor;
        private readonly ModSettingsLabelFactory _createLabel;
        private readonly ModSettingsButtonFactory _createButton;
        private readonly Func<SettingDefinition, object, object, bool> _applySettingValue;
        private readonly Action _onSettingChanged;

        internal ModSettingsKeybindWidgetBuilder(
            GameObject contentRoot,
            ISettingsProvider settingsProvider,
            object settingsObject,
            Texture2D whiteTexture,
            UIFont bitmapFont,
            Font ttfFont,
            Color textColor,
            Color subtextColor,
            ModSettingsLabelFactory createLabel,
            ModSettingsButtonFactory createButton,
            Func<SettingDefinition, object, object, bool> applySettingValue,
            Action onSettingChanged)
        {
            _contentRoot = contentRoot;
            _settingsProvider = settingsProvider;
            _settingsObject = settingsObject;
            _whiteTexture = whiteTexture;
            _bitmapFont = bitmapFont;
            _ttfFont = ttfFont;
            _textColor = textColor;
            _subtextColor = subtextColor;
            _createLabel = createLabel;
            _createButton = createButton;
            _applySettingValue = applySettingValue;
            _onSettingChanged = onSettingChanged;
        }

        internal GameObject CreateColumnHeaderWidget()
        {
            GameObject container = CreateContainer("KeybindColumnHeader");
            CreateColumnHeaderLabel(container.transform, "ActionHeader", "ACTION", new Vector3(0, 0, 0), ActionLabelWidth, NGUIText.Alignment.Left);
            CreateColumnHeaderLabel(container.transform, "PrimaryHeader", "PRIMARY", new Vector3(290, 0, 0), KeySlotWidth, NGUIText.Alignment.Center);
            CreateColumnHeaderLabel(container.transform, "SecondaryHeader", "ALT", new Vector3(465, 0, 0), KeySlotWidth, NGUIText.Alignment.Center);
            CreateColumnHeaderLabel(container.transform, "ClearHeader", "CLEAR", new Vector3(630, 0, 0), SmallButtonWidth, NGUIText.Alignment.Center);
            CreateColumnHeaderLabel(container.transform, "ResetHeader", "RESET", new Vector3(740, 0, 0), SmallButtonWidth, NGUIText.Alignment.Center);
            return container;
        }

        internal GameObject CreateSectionHeaderWidget(SettingDefinition def)
        {
            GameObject container = CreateContainer("SectionHeader_" + (def != null ? def.Id : "Unknown"));

            string title = def != null && !string.IsNullOrEmpty(def.Label)
                ? def.Label.ToUpperInvariant()
                : "SECTION";

            UILabel label = _createLabel(
                container.transform,
                "SectionLabel",
                title,
                new Vector3(0, 0, 0),
                20,
                def != null && def.HeaderColor.HasValue ? def.HeaderColor.Value : new Color(0.35f, 0.70f, 0.90f, 1f),
                _bitmapFont,
                _ttfFont,
                102);
            label.pivot = UIWidget.Pivot.Left;
            label.alignment = NGUIText.Alignment.Left;
            label.transform.localPosition = new Vector3(SectionHeaderLocalX, 0, 0);
            label.width = 300;
            label.overflowMethod = UILabel.Overflow.ClampContent;
            label.multiLine = false;

            return container;
        }

        internal GameObject CreateDualKeybindWidget(SettingDefinition primaryDef, SettingDefinition secondaryDef)
        {
            GameObject container = CreateContainer("DualKeybind_" + (primaryDef != null ? primaryDef.Id : "Unknown"));
            string actionLabel = ModSettingsKeybindLayout.GetActionLabel(primaryDef, secondaryDef);

            UILabel label = _createLabel(container.transform, "ActionLabel", actionLabel, Vector3.zero, 16, _textColor, _bitmapFont, _ttfFont, 102);
            label.pivot = UIWidget.Pivot.Left;
            label.alignment = NGUIText.Alignment.Left;
            label.transform.localPosition = Vector3.zero;
            label.width = ActionLabelWidth;
            label.overflowMethod = UILabel.Overflow.ClampContent;
            label.multiLine = false;
            SpineWidgetRuntime.SetTooltip(label.gameObject, primaryDef != null ? primaryDef.Tooltip : (secondaryDef != null ? secondaryDef.Tooltip : null));

            KeybindCaptureListener primaryCapture = null;
            KeybindCaptureListener secondaryCapture = null;

            Func<string> primaryDisplay = () => ModSettingsKeybindLayout.FormatKeyCode(ModSettingsKeybindRuntime.ReadKeyCode(primaryDef, _settingsObject));
            Func<string> secondaryDisplay = () => ModSettingsKeybindLayout.FormatKeyCode(ModSettingsKeybindRuntime.ReadKeyCode(secondaryDef, _settingsObject));

            Action refreshCapture = delegate
            {
                if (primaryCapture != null && primaryCapture.DisplayTextProvider != null && primaryCapture.ValueLabel != null)
                    primaryCapture.ValueLabel.text = primaryCapture.DisplayTextProvider();
                if (secondaryCapture != null && secondaryCapture.DisplayTextProvider != null && secondaryCapture.ValueLabel != null)
                    secondaryCapture.ValueLabel.text = secondaryCapture.DisplayTextProvider();
            };

            primaryCapture = CreateClickableKeySlot(
                container.transform,
                "Primary",
                new Vector3(290, 0, 0),
                primaryDisplay,
                delegate { Report("Press a key for " + actionLabel + " primary. Escape cancels capture.", false); },
                delegate(KeyCode key)
                {
                    if (ApplyValue(primaryDef, key))
                    {
                        NotifyChanged();
                        refreshCapture();
                    }
                },
                BuildKeySlotTooltip(actionLabel, "primary", primaryDef));

            secondaryCapture = CreateClickableKeySlot(
                container.transform,
                "Secondary",
                new Vector3(465, 0, 0),
                secondaryDisplay,
                delegate { Report("Press a key for " + actionLabel + " alternate. Escape cancels capture.", false); },
                delegate(KeyCode key)
                {
                    if (ApplyValue(secondaryDef, key))
                    {
                        NotifyChanged();
                        refreshCapture();
                    }
                },
                BuildKeySlotTooltip(actionLabel, "alternate", secondaryDef));

            GameObject clearButton = _createButton(
                container.transform,
                "Clear",
                "CLEAR",
                new Vector3(630, 0, 0),
                13,
                Color.white,
                _bitmapFont,
                _ttfFont,
                SmallButtonWidth,
                SmallButtonHeight,
                delegate
                {
                    bool primaryChanged = ApplyValue(primaryDef, KeyCode.None);
                    bool secondaryChanged = ApplyValue(secondaryDef, KeyCode.None);
                    bool changed = primaryChanged || secondaryChanged;
                    if (changed)
                    {
                        NotifyChanged();
                        refreshCapture();
                        Report("Cleared both bindings for " + actionLabel + ".", false);
                    }
                    else
                    {
                        Report(actionLabel + " is already unbound.", true);
                    }
                });
            SpineWidgetRuntime.SetTooltip(clearButton, "Clear both primary and alternate bindings for " + actionLabel + ".");

            GameObject resetButton = _createButton(
                container.transform,
                "Reset",
                "RESET",
                new Vector3(740, 0, 0),
                13,
                Color.white,
                _bitmapFont,
                _ttfFont,
                SmallButtonWidth,
                SmallButtonHeight,
                delegate
                {
                    if (ModSettingsKeybindActionReset.Reset(_settingsProvider, primaryDef, secondaryDef, _settingsObject, _applySettingValue))
                    {
                        NotifyChanged();
                        refreshCapture();
                        Report("Restored " + actionLabel + " to its default bindings.", false);
                    }
                    else
                    {
                        Report("Could not reset " + actionLabel + ".", true);
                    }
                });
            SpineWidgetRuntime.SetTooltip(resetButton, "Restore only " + actionLabel + " to its default primary and alternate keys.");

            return container;
        }

        internal static void NormalizeWideKeybindWidgetAlignment(GameObject widget, ModSettingsKeybindDisplayEntry entry)
        {
            if (widget == null || entry == null) return;

            UILabel[] labels = widget.GetComponentsInChildren<UILabel>(true);
            if (labels == null || labels.Length == 0) return;

            bool isHeader = ModSettingsKeybindLayout.IsSectionHeaderEntry(entry);
            string target = isHeader
                ? ((entry.Primary != null && !string.IsNullOrEmpty(entry.Primary.Label))
                    ? entry.Primary.Label.ToUpperInvariant()
                    : "SECTION")
                : ModSettingsKeybindLayout.GetActionLabel(entry.Primary, entry.Secondary);

            UILabel best = null;
            for (int i = 0; i < labels.Length; i++)
            {
                UILabel candidate = labels[i];
                if (candidate == null) continue;

                string text = candidate.text ?? string.Empty;
                if (string.Equals(text.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    best = candidate;
                    break;
                }
            }

            if (best == null)
                best = labels[0];
            if (best == null) return;

            best.pivot = UIWidget.Pivot.Left;
            best.alignment = NGUIText.Alignment.Left;
            best.multiLine = false;
            best.overflowMethod = UILabel.Overflow.ClampContent;
            best.width = isHeader ? 320 : ActionLabelWidth;

            Vector3 pos = best.transform.localPosition;
            best.transform.localPosition = new Vector3(isHeader ? SectionHeaderLocalX : 0f, pos.y, pos.z);
        }

        private GameObject CreateContainer(string name)
        {
            GameObject container = NGUITools.AddChild(_contentRoot);
            container.name = name;
            NGUITools.SetLayer(container, _contentRoot.layer);
            return container;
        }

        private void CreateColumnHeaderLabel(Transform parent, string name, string text, Vector3 position, int width, NGUIText.Alignment alignment)
        {
            UILabel label = _createLabel(parent, name, text, position, 12, _subtextColor, _bitmapFont, _ttfFont, 102);
            label.width = width;
            label.height = 18;
            label.alignment = alignment;
            label.pivot = alignment == NGUIText.Alignment.Left ? UIWidget.Pivot.Left : UIWidget.Pivot.Center;
            label.multiLine = false;
            label.overflowMethod = UILabel.Overflow.ClampContent;
        }

        private KeybindCaptureListener CreateClickableKeySlot(
            Transform parent,
            string name,
            Vector3 localPosition,
            Func<string> displayTextProvider,
            Action onSelected,
            Action<KeyCode> onCaptured,
            string tooltipText)
        {
            GameObject slot = new GameObject(name);
            slot.transform.SetParent(parent, false);
            slot.transform.localPosition = localPosition;
            slot.layer = parent.gameObject.layer;

            UITexture bg = slot.AddComponent<UITexture>();
            bg.mainTexture = _whiteTexture;
            bg.width = KeySlotWidth;
            bg.height = KeySlotHeight;
            bg.depth = 100;
            bg.color = new Color(0.19f, 0.15f, 0.12f, 0.95f);

            UILabel valueLabel = _createLabel(
                slot.transform,
                "Value",
                displayTextProvider != null ? displayTextProvider() : string.Empty,
                Vector3.zero,
                14,
                Color.white,
                _bitmapFont,
                _ttfFont,
                101);
            valueLabel.alignment = NGUIText.Alignment.Center;
            valueLabel.width = Mathf.Max(40, KeySlotWidth - 8);
            valueLabel.height = Mathf.Max(20, KeySlotHeight - 4);
            valueLabel.overflowMethod = UILabel.Overflow.ClampContent;
            valueLabel.multiLine = false;

            BoxCollider col = slot.AddComponent<BoxCollider>();
            col.size = new Vector3(KeySlotWidth, KeySlotHeight, 1);
            col.center = Vector3.zero;

            KeybindCaptureListener capture = slot.AddComponent<KeybindCaptureListener>();
            capture.ValueLabel = valueLabel;
            capture.DisplayTextProvider = displayTextProvider;
            capture.OnCanceled = delegate
            {
                if (displayTextProvider != null)
                    valueLabel.text = displayTextProvider();
                Report("Binding capture cancelled.", true);
            };
            capture.OnCaptured = delegate(KeyCode key)
            {
                if (onCaptured != null) onCaptured(key);
                if (displayTextProvider != null)
                    valueLabel.text = displayTextProvider();
            };

            SpineWidgetRuntime.SetTooltip(slot, tooltipText);

            UIEventListener.Get(slot).onClick = delegate
            {
                if (onSelected != null) onSelected();
                capture.StartCapture();
            };

            return capture;
        }

        private bool ApplyValue(SettingDefinition def, object value)
        {
            return _applySettingValue != null && _applySettingValue(def, _settingsObject, value);
        }

        private void NotifyChanged()
        {
            if (_onSettingChanged != null)
                _onSettingChanged();
        }

        private static void Report(string message, bool warning)
        {
            ModSettingsKeybindStatusReporter.Report(message, warning);
        }

        private static string BuildKeySlotTooltip(string actionLabel, string slotName, SettingDefinition def)
        {
            string description = def != null ? def.Tooltip : null;
            string prefix = string.IsNullOrEmpty(description) ? string.Empty : description + "\n\n";
            return prefix
                + "Click to change the " + slotName + " key for " + actionLabel + ". "
                + "Press Escape to cancel capture. Use RESET to restore reserved default keys.";
        }
    }
}
