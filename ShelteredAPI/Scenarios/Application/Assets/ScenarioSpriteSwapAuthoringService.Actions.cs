using System;
using UnityEngine;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
namespace ShelteredAPI.Scenarios.Application.Assets{
    internal sealed partial class ScenarioSpriteSwapAuthoringService
    {
        public bool TryHandleAction(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            if (TryHandleStaticAction(state, actionId, out handled, out message))
                return true;

            if (handled)
                return false;

            if (TryHandleCustomEditorAction(state, actionId, out handled, out message))
                return true;

            if (handled)
                return false;

            if (TryHandlePickerAction(state, actionId, out handled, out message))
                return true;

            if (handled)
                return false;

            return TryHandleApplyAction(state, actionId, out handled, out message);
        }

        private bool TryHandleStaticAction(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = true;
            message = null;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapPickerOpen, StringComparison.Ordinal))
                return OpenPicker(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartHead, StringComparison.Ordinal))
                return SwitchCharacterPart(state, ScenarioCharacterTexturePart.Head, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartTorso, StringComparison.Ordinal))
                return SwitchCharacterPart(state, ScenarioCharacterTexturePart.Torso, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartLegs, StringComparison.Ordinal))
                return SwitchCharacterPart(state, ScenarioCharacterTexturePart.Legs, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomIn, StringComparison.Ordinal))
                return AdjustEditorZoom(state, +1, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomOut, StringComparison.Ordinal))
                return AdjustEditorZoom(state, -1, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomReset, StringComparison.Ordinal))
                return ResetEditorZoom(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapPickerSave, StringComparison.Ordinal))
                return SavePicker(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel, StringComparison.Ordinal))
                return CancelPicker(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditStart, StringComparison.Ordinal))
                return BeginCustomEdit(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapImportPng, StringComparison.Ordinal))
                return ImportPngReplacement(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditDiscard, StringComparison.Ordinal))
                return DiscardCustomEdit(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPaint, StringComparison.Ordinal))
                return SelectCustomTool(state, CustomEditorTool.Paint, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPick, StringComparison.Ordinal))
                return SelectCustomTool(state, CustomEditorTool.Pick, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolSelect, StringComparison.Ordinal))
                return SelectCustomTool(state, CustomEditorTool.Select, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectionClear, StringComparison.Ordinal))
                return ClearCustomSelection(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomCopy, StringComparison.Ordinal))
                return CopyCustomPixels(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaste, StringComparison.Ordinal))
                return PasteCustomPixels(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapClear, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapRevert, StringComparison.Ordinal))
            {
                if (HasCustomEditor(state))
                    return DiscardCustomEdit(state, out message);

                return ClearActiveSwap(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCopy, StringComparison.Ordinal))
            {
                if (HasCustomEditor(state))
                    return CopyCustomPixels(state, out message);

                return CopyActiveSwap(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapPaste, StringComparison.Ordinal))
            {
                if (HasCustomEditor(state))
                    return PasteCustomPixels(state, out message);

                return PasteSwap(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryUndo, StringComparison.Ordinal))
                return Undo(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryRedo, StringComparison.Ordinal))
                return Redo(state, out message);

            handled = false;
            return false;
        }

        private bool TryHandleCustomEditorAction(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = true;
            message = null;

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomBrushPrefix, StringComparison.Ordinal))
                return TryHandleBrushAction(state, actionId, out message);

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomPresetPrefix, StringComparison.Ordinal))
                return TryHandlePresetAction(state, actionId, out message);

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaintPrefix, StringComparison.Ordinal))
                return TryHandlePixelAction(state, actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaintPrefix, "Custom sprite paint coordinates could not be decoded.", PaintCustomPixel, out message);

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomPickPrefix, StringComparison.Ordinal))
                return TryHandlePixelAction(state, actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomPickPrefix, "Custom sprite pick coordinates could not be decoded.", PickCustomColor, out message);

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectStartPrefix, StringComparison.Ordinal))
                return TryHandlePixelAction(state, actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectStartPrefix, "Custom sprite selection start could not be decoded.", StartCustomSelection, out message);

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectDragPrefix, StringComparison.Ordinal))
                return TryHandlePixelAction(state, actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectDragPrefix, "Custom sprite selection drag could not be decoded.", DragCustomSelection, out message);

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectEndPrefix, StringComparison.Ordinal))
                return TryHandlePixelAction(state, actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectEndPrefix, "Custom sprite selection end could not be decoded.", EndCustomSelection, out message);

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomColorPrefix, StringComparison.Ordinal))
                return TryHandleColorAction(state, actionId, out message);

            handled = false;
            return false;
        }

        private bool TryHandlePickerAction(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = true;
            message = null;

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapPreviewPrefix, StringComparison.Ordinal))
            {
                string previewToken = DecodeActionToken(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapPreviewPrefix.Length));
                if (string.IsNullOrEmpty(previewToken))
                {
                    message = "Sprite preview selection could not be decoded.";
                    return false;
                }

                return PreviewCandidate(state, previewToken, out message);
            }

            handled = false;
            return false;
        }

        private bool TryHandleApplyAction(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (!actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapApplyPrefix, StringComparison.Ordinal))
                return false;

            handled = true;
            string token = DecodeActionToken(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapApplyPrefix.Length));
            if (string.IsNullOrEmpty(token))
            {
                message = "Sprite swap selection could not be decoded.";
                return false;
            }

            return ApplyCandidateImmediately(state, token, out message);
        }

        private bool TryHandleBrushAction(ScenarioAuthoringState state, string actionId, out string message)
        {
            int brushIndex;
            if (!int.TryParse(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapCustomBrushPrefix.Length), out brushIndex))
            {
                message = "Custom sprite brush selection could not be decoded.";
                return false;
            }

            return SelectCustomBrush(state, brushIndex, out message);
        }

        private bool TryHandlePresetAction(ScenarioAuthoringState state, string actionId, out string message)
        {
            int presetIndex;
            if (!int.TryParse(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapCustomPresetPrefix.Length), out presetIndex))
            {
                message = "Custom color preset could not be decoded.";
                return false;
            }

            return SelectCustomPreset(state, presetIndex, out message);
        }

        private bool TryHandleColorAction(ScenarioAuthoringState state, string actionId, out string message)
        {
            Color color;
            if (!TryDecodeColor(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapCustomColorPrefix.Length), out color))
            {
                message = "Custom sprite color could not be decoded.";
                return false;
            }

            return SetCustomColor(state, color, -1, out message);
        }

        private bool TryHandlePixelAction(
            ScenarioAuthoringState state,
            string actionId,
            string prefix,
            string decodeFailureMessage,
            PixelActionHandler handler,
            out string message)
        {
            int pixelX;
            int pixelY;
            if (!TryDecodePixel(actionId.Substring(prefix.Length), out pixelX, out pixelY))
            {
                message = decodeFailureMessage;
                return false;
            }

            return handler(state, pixelX, pixelY, out message);
        }

        private delegate bool PixelActionHandler(ScenarioAuthoringState state, int pixelX, int pixelY, out string message);
    }
}
