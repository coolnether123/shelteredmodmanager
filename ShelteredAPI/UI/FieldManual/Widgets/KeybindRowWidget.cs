using System;
using ModAPI.Internal.UI;
using ModAPI.Spine;
using ModAPI.Spine.UI;
using UnityEngine;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using ShelteredAPI.UI.FieldManual.Tooltips;

namespace ShelteredAPI.UI.FieldManual.Widgets
{
    /// <summary>
    /// One row of the keybind list:
    ///     [ Action label ]   [ primary keycap ]   [ alt keycap ]   (× ↺ on hover)
    /// Owns two <see cref="KeycapWidget"/>s and two <see cref="KeybindCaptureListener"/>s,
    /// and reuses the existing ModSettingsKeybindRuntime to read/write the underlying
    /// SettingDefinition values. Everything visual is themed; everything functional is
    /// delegated to existing services.
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
            int rowWidth = (int)(_metrics.PanelWidth * 0.78f);

            GameObject row = _ui.CreateChild(parent, "Row_" + (entry.Primary != null ? entry.Primary.Id : "row"), Vector3.zero);

            // Action label
            int textDepth = _ui.NextDepth();
            UILabel actionText = _ui.CreateLabel(row, "Action", actionLabel,
                new Vector3(-rowWidth * 0.5f + 8, 0, 0),
                17, _palette.Ink,
                _metrics.ActionLabelWidth, _metrics.RowHeight - 8,
                NGUIText.Alignment.Left, UIWidget.Pivot.Left, textDepth);
            actionText.overflowMethod = UILabel.Overflow.ShrinkContent;

            // Keycaps positioned to the right of the label
            int kw = _metrics.KeycapWidth;
            int kh = _metrics.KeycapHeight;
            int spacing = _metrics.KeycapSpacing;
            float capsRightEdge = rowWidth * 0.5f - 70; // leave room for hover icons
            float altCenterX = capsRightEdge - kw * 0.5f;
            float primaryCenterX = altCenterX - kw - spacing;

            KeycapWidget primaryCap = null;
            KeycapWidget secondaryCap = null;
            KeybindCaptureListener primaryCapture = null;
            KeybindCaptureListener secondaryCapture = null;

            Func<string> primaryDisplay = delegate
            {
                return entry.Primary == null
                    ? "—"
                    : ModSettingsKeybindLayout.FormatKeyCode(ModSettingsKeybindRuntime.ReadKeyCode(entry.Primary, _settingsObject));
            };
            Func<string> secondaryDisplay = delegate
            {
                return entry.Secondary == null
                    ? "—"
                    : ModSettingsKeybindLayout.FormatKeyCode(ModSettingsKeybindRuntime.ReadKeyCode(entry.Secondary, _settingsObject));
            };

            primaryCap = KeycapWidget.Create(row, "Primary", new Vector3(primaryCenterX, 0, 0),
                kw, kh, primaryDisplay(), _textures, _palette, _ui,
                delegate
                {
                    if (primaryCap == null || primaryCapture == null) return;
                    primaryCap.StartPulse();
                    primaryCapture.StartCapture();
                });

            secondaryCap = KeycapWidget.Create(row, "Alt", new Vector3(altCenterX, 0, 0),
                kw, kh, secondaryDisplay(), _textures, _palette, _ui,
                delegate
                {
                    if (secondaryCap == null || secondaryCapture == null) return;
                    secondaryCap.StartPulse();
                    secondaryCapture.StartCapture();
                });

            primaryCapture = AttachCapture(primaryCap, entry.Primary, primaryDisplay);
            secondaryCapture = AttachCapture(secondaryCap, entry.Secondary, secondaryDisplay);

            // Action-edit icons (always visible at low opacity, no longer reveal-on-hover —
            // tooltip strip in the panel footer explains them).
            int iconsDepth = _ui.NextDepth();
            Color iconColor = new Color(_palette.InkFaded.r, _palette.InkFaded.g, _palette.InkFaded.b, 0.55f);
            UILabel clearIcon = _ui.CreateLabel(row, "ClearIcon", "x",
                new Vector3(altCenterX + kw * 0.5f + 22, 0, 0),
                18, iconColor,
                28, 28, NGUIText.Alignment.Center, UIWidget.Pivot.Center, iconsDepth);
            UILabel resetIcon = _ui.CreateLabel(row, "ResetIcon", "↺",
                new Vector3(altCenterX + kw * 0.5f + 52, 0, 0),
                18, iconColor,
                28, 28, NGUIText.Alignment.Center, UIWidget.Pivot.Center, iconsDepth);

            _ui.AddClickCollider(clearIcon.gameObject, 28, 28, delegate
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
            _ui.AddClickCollider(resetIcon.gameObject, 28, 28, delegate
            {
                if (ModSettingsKeybindActionReset.Reset(_settingsProvider, entry.Primary, entry.Secondary, _settingsObject, _applyValue))
                {
                    primaryCap.SetText(primaryDisplay());
                    secondaryCap.SetText(secondaryDisplay());
                    if (_notifyChanged != null) _notifyChanged();
                }
            });

            // Row-wide collider for both scroll-drag forwarding and a fallback tooltip surface.
            _ui.AddClickCollider(row, rowWidth, _metrics.RowHeight, null);

            // Tooltip wiring — every interactive surface pushes its own message onto the bus.
            AttachTooltips(row, entry, actionLabel, primaryCap, secondaryCap, clearIcon.gameObject, resetIcon.gameObject);

            return row;
        }

        private void AttachTooltips(
            GameObject row,
            ModSettingsKeybindDisplayEntry entry,
            string actionLabel,
            KeycapWidget primaryCap,
            KeycapWidget secondaryCap,
            GameObject clearIcon,
            GameObject resetIcon)
        {
            if (_tooltipBus == null) return;

            string actionBody = entry.Primary != null && !string.IsNullOrEmpty(entry.Primary.Tooltip)
                ? entry.Primary.Tooltip
                : (entry.Secondary != null && !string.IsNullOrEmpty(entry.Secondary.Tooltip) ? entry.Secondary.Tooltip : "Click a key to rebind. Click x or ↺ to clear or reset.");

            HoverTooltipTrigger.Attach(row, _tooltipBus,
                TooltipMessage.Info(actionLabel, actionBody));

            if (primaryCap != null)
                HoverTooltipTrigger.Attach(primaryCap.gameObject, _tooltipBus,
                    TooltipMessage.Info(actionLabel + " — Primary",
                        "Click to rebind the primary key. Press Escape to cancel."));

            if (secondaryCap != null)
                HoverTooltipTrigger.Attach(secondaryCap.gameObject, _tooltipBus,
                    TooltipMessage.Info(actionLabel + " — Alternate",
                        "Click to rebind the alternate key. Press Escape to cancel."));

            if (clearIcon != null)
                HoverTooltipTrigger.Attach(clearIcon, _tooltipBus,
                    TooltipMessage.Info("Clear bindings",
                        "Removes both primary and alternate bindings for " + actionLabel + "."));

            if (resetIcon != null)
                HoverTooltipTrigger.Attach(resetIcon, _tooltipBus,
                    TooltipMessage.Info("Restore defaults",
                        "Resets " + actionLabel + " to its reserved default keys."));
        }

        private bool ApplyValue(SettingDefinition def, object value)
        {
            return _applyValue != null && _applyValue(def, _settingsObject, value);
        }

        private KeybindCaptureListener AttachCapture(KeycapWidget cap, SettingDefinition def, Func<string> displayProvider)
        {
            if (cap == null) return null;
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
                if (def != null && ApplyValue(def, key))
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
