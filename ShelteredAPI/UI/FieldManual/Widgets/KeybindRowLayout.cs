using ShelteredAPI.UI.FieldManual.Theme;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Widgets
{
    /// <summary>
    /// Shared row geometry for the keybind book layout. The gap between action labels
    /// and key slots deliberately clears Sheltered's book crease.
    /// </summary>
    internal sealed class KeybindRowLayout
    {
        public readonly int RowWidth;
        public readonly int KeySlotWidth;
        public readonly int KeySlotHeight;
        public readonly int SmallButtonWidth;
        public readonly int SmallButtonHeight;
        public readonly float ActionLabelX;
        public readonly float PrimaryCenterX;
        public readonly float SecondaryCenterX;
        public readonly float ClearCenterX;
        public readonly float ResetCenterX;

        private KeybindRowLayout(
            int rowWidth,
            int keySlotWidth,
            int keySlotHeight,
            int smallButtonWidth,
            int smallButtonHeight,
            float actionLabelX,
            float primaryCenterX,
            float secondaryCenterX,
            float clearCenterX,
            float resetCenterX)
        {
            RowWidth = rowWidth;
            KeySlotWidth = keySlotWidth;
            KeySlotHeight = keySlotHeight;
            SmallButtonWidth = smallButtonWidth;
            SmallButtonHeight = smallButtonHeight;
            ActionLabelX = actionLabelX;
            PrimaryCenterX = primaryCenterX;
            SecondaryCenterX = secondaryCenterX;
            ClearCenterX = clearCenterX;
            ResetCenterX = resetCenterX;
        }

        public static KeybindRowLayout Create(IThemeMetrics metrics)
        {
            int rowWidth = Mathf.RoundToInt(metrics.PanelWidth * 0.78f);
            int keySlotWidth = metrics.KeycapWidth;
            int keySlotHeight = metrics.KeycapHeight;
            int smallButtonWidth = 48;
            int smallButtonHeight = 34;
            int spacing = metrics.KeycapSpacing;

            float controlsRightEdge = rowWidth * 0.5f - 18f;
            float resetCenterX = controlsRightEdge - smallButtonWidth * 0.5f;
            float clearCenterX = resetCenterX - smallButtonWidth - spacing;
            float secondaryCenterX = clearCenterX - smallButtonWidth * 0.5f - spacing - keySlotWidth * 0.5f;
            float primaryCenterX = secondaryCenterX - keySlotWidth - spacing;
            float actionLabelX = -rowWidth * 0.5f + 8f;

            return new KeybindRowLayout(
                rowWidth,
                keySlotWidth,
                keySlotHeight,
                smallButtonWidth,
                smallButtonHeight,
                actionLabelX,
                primaryCenterX,
                secondaryCenterX,
                clearCenterX,
                resetCenterX);
        }
    }
}
