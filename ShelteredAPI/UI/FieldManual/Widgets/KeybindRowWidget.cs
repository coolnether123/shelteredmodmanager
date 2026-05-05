using ShelteredAPI.UI.Compatibility;
using System;
using ShelteredAPI.UI.Internal;
using ModAPI.Spine;
using ShelteredAPI.UI.Spine;
using UnityEngine;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using ShelteredAPI.UI.FieldManual.Tooltips;


using ShelteredAPI.UI.Internal.Settings;
namespace ShelteredAPI.UI.FieldManual.Widgets
{
    /// <summary>
    /// One row of the keybind list. Action text stays on the left page, binding slots
    /// stay on the right page, and the middle band is left clear for the book crease.
    /// </summary>
    internal sealed class KeybindRowWidget
    {
        private readonly IThemePalette _palette;
        private readonly IThemeMetrics _metrics;
        private readonly ITextureLibrary _textures;
        private readonly UIPrimitiveFactory _ui;
        private readonly ITooltipBus _tooltipBus;
        private readonly ISettingsProvider _settingsProvider;
        private readonly object _settingsObject;
        private readonly Func<SettingDefinition, object, object, bool> _applyValue;
        private readonly Action _notifyChanged;

        public KeybindRowWidget(
            IThemePalette palette,
            IThemeMetrics metrics,
            ITextureLibrary textures,
            UIPrimitiveFactory ui,
            ITooltipBus tooltipBus,
            ISettingsProvider settingsProvider,
            object settingsObject,
            Func<SettingDefinition, object, object, bool> applyValue,
            Action notifyChanged)
        {
            _palette = palette;
            _metrics = metrics;
            _textures = textures;
            _ui = ui;
            _tooltipBus = tooltipBus;
            _settingsProvider = settingsProvider;
            _settingsObject = settingsObject;
            _applyValue = applyValue;
            _notifyChanged = notifyChanged;
        }

        public GameObject Build(GameObject parent, ModSettingsKeybindDisplayEntry entry)
        {
            string actionLabel = ModSettingsKeybindLayout.GetActionLabel(entry.Primary, entry.Secondary);
            KeybindRowLayout layout = KeybindRowLayout.Create(_metrics);

            GameObject row = _ui.CreateChild(parent, "Row_" + (entry.Primary != null ? entry.Primary.Id : "row"), Vector3.zero);

            UILabel actionText = _ui.CreateLabel(row, "Action", actionLabel,
                new Vector3(layout.ActionLabelX, 0, 0),
                17, _palette.Ink,
                _metrics.ActionLabelWidth, _metrics.RowHeight - 8,
                NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            actionText.overflowMethod = UILabel.Overflow.ShrinkContent;

            KeycapWidget primaryCap = null;
            KeycapWidget secondaryCap = null;
            KeybindCaptureListener primaryCapture = null;
            KeybindCaptureListener secondaryCapture = null;

            Func<string> primaryDisplay = delegate
            {
                return entry.Primary == null
                    ? "--"
                    : ModSettingsKeybindLayout.FormatKeyCode(ModSettingsKeybindRuntime.ReadKeyCode(entry.Primary, _settingsObject));
            };
            Func<string> secondaryDisplay = delegate
            {
                return entry.Secondary == null
                    ? "--"
                    : ModSettingsKeybindLayout.FormatKeyCode(ModSettingsKeybindRuntime.ReadKeyCode(entry.Secondary, _settingsObject));
            };

            primaryCap = KeycapWidget.Create(row, "Primary", new Vector3(layout.PrimaryCenterX, 0, 0),
                layout.KeySlotWidth, layout.KeySlotHeight, primaryDisplay(), _textures, _palette, _ui,
                delegate
                {
                    if (primaryCap == null || primaryCapture == null) return;
                    primaryCap.StartPulse();
                    primaryCapture.StartCapture();
                });

            secondaryCap = KeycapWidget.Create(row, "Alt", new Vector3(layout.SecondaryCenterX, 0, 0),
                layout.KeySlotWidth, layout.KeySlotHeight, secondaryDisplay(), _textures, _palette, _ui,
                delegate
                {
                    if (secondaryCap == null || secondaryCapture == null) return;
                    secondaryCap.StartPulse();
                    secondaryCapture.StartCapture();
                });

            primaryCapture = AttachCapture(primaryCap, entry.Primary, primaryDisplay);
            secondaryCapture = AttachCapture(secondaryCap, entry.Secondary, secondaryDisplay);

            GameObject clearButton = CreateSmallRowButton(row, "Clear", "CLR", new Vector3(layout.ClearCenterX, 0, 0),
                layout.SmallButtonWidth, layout.SmallButtonHeight,
                delegate
                {
                    bool changed = false;
                    if (entry.Primary != null) changed |= ApplyValue(entry.Primary, KeyCode.None);
                    if (entry.Secondary != null) changed |= ApplyValue(entry.Secondary, KeyCode.None);
                    if (changed)
                    {
                        primaryCap.SetText(primaryDisplay());
                        secondaryCap.SetText(secondaryDisplay());
                        if (_notifyChanged != null) _notifyChanged();
                    }
                });

            GameObject resetButton = CreateSmallRowButton(row, "Reset", "RST", new Vector3(layout.ResetCenterX, 0, 0),
                layout.SmallButtonWidth, layout.SmallButtonHeight,
                delegate
                {
                    if (ModSettingsKeybindActionReset.Reset(_settingsProvider, entry.Primary, entry.Secondary, _settingsObject, _applyValue))
                    {
                        primaryCap.SetText(primaryDisplay());
                        secondaryCap.SetText(secondaryDisplay());
                        if (_notifyChanged != null) _notifyChanged();
                    }
                });

            _ui.AddClickCollider(row, layout.RowWidth, _metrics.RowHeight, null);
            AttachTooltips(row, entry, actionLabel, primaryCap, secondaryCap, clearButton, resetButton);

            return row;
        }

        private GameObject CreateSmallRowButton(GameObject parent, string name, string text, Vector3 position, int width, int height, Action onClick)
        {
            UITexture bg = _ui.CreateQuad(parent, name + "Bg", _textures.Keycap(width, height, KeycapState.Rest),
                position, width, height, Color.white, _ui.NextDepth());
            UILabel label = _ui.CreateLabel(parent, name + "Label", text,
                position, 12, _palette.KeycapInk,
                Mathf.Max(20, width - 6), Mathf.Max(18, height - 4),
                NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
            _ui.AddClickCollider(bg.gameObject, width, height, onClick);
            return bg.gameObject;
        }

        private void AttachTooltips(
            GameObject row,
            ModSettingsKeybindDisplayEntry entry,
            string actionLabel,
            KeycapWidget primaryCap,
            KeycapWidget secondaryCap,
            GameObject clearButton,
            GameObject resetButton)
        {
            if (_tooltipBus == null) return;

            string actionBody = entry.Primary != null && !string.IsNullOrEmpty(entry.Primary.Tooltip)
                ? entry.Primary.Tooltip
                : (entry.Secondary != null && !string.IsNullOrEmpty(entry.Secondary.Tooltip) ? entry.Secondary.Tooltip : "Click a binding to change it. Use CLR or RST to clear or restore it.");

            HoverTooltipTrigger.Attach(row, _tooltipBus,
                TooltipMessage.Info(actionLabel, actionBody));

            if (primaryCap != null)
                HoverTooltipTrigger.Attach(primaryCap.gameObject, _tooltipBus,
                    TooltipMessage.Info(actionLabel + " - Primary",
                        "Click to rebind the primary key. Press Escape to cancel."));

            if (secondaryCap != null && entry.Secondary != null)
                HoverTooltipTrigger.Attach(secondaryCap.gameObject, _tooltipBus,
                    TooltipMessage.Info(actionLabel + " - Alternate",
                        "Click to rebind the alternate key. Press Escape to cancel."));

            if (clearButton != null)
                HoverTooltipTrigger.Attach(clearButton, _tooltipBus,
                    TooltipMessage.Info("Clear bindings",
                        "Removes both primary and alternate bindings for " + actionLabel + "."));

            if (resetButton != null)
                HoverTooltipTrigger.Attach(resetButton, _tooltipBus,
                    TooltipMessage.Info("Restore defaults",
                        "Resets " + actionLabel + " to its default keys."));
        }

        private bool ApplyValue(SettingDefinition def, object value)
        {
            return _applyValue != null && _applyValue(def, _settingsObject, value);
        }

        private KeybindCaptureListener AttachCapture(KeycapWidget cap, SettingDefinition def, Func<string> displayProvider)
        {
            if (cap == null || def == null) return null;
            KeybindCaptureListener capture = cap.gameObject.AddComponent<KeybindCaptureListener>();
            capture.ValueLabel = cap.ValueLabel;
            capture.DisplayTextProvider = displayProvider;
            capture.OnCanceled = delegate
            {
                cap.StopPulse();
                cap.SetText(displayProvider());
            };
            capture.OnCaptured = delegate(KeyCode key)
            {
                cap.StopPulse();
                if (ApplyValue(def, key))
                {
                    cap.SetText(displayProvider());
                    if (_notifyChanged != null) _notifyChanged();
                }
                else
                {
                    cap.SetText(displayProvider());
                }
            };
            return capture;
        }
    }
}
