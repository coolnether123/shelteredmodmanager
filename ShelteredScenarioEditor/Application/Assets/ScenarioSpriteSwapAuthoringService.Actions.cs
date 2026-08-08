using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;

namespace ShelteredScenarioEditor.Application.Assets
{
    internal sealed partial class ScenarioSpriteSwapAuthoringService
    {
        public bool Execute(ScenarioAuthoringState state, SpriteSwapCommand command, out string message)
        {
            message = null;
            if (state == null || command == null)
                return false;

            ReconcilePickerTarget(state);
            switch (command.Kind)
            {
                case SpriteSwapCommandKind.OpenPicker: return OpenPicker(state, out message);
                case SpriteSwapCommandKind.SavePicker: return SavePicker(state, out message);
                case SpriteSwapCommandKind.CancelPicker: return CancelPicker(state, out message);
                case SpriteSwapCommandKind.ApplyCandidate: return ApplyCandidateImmediately(state, command.Token, out message);
                case SpriteSwapCommandKind.PreviewCandidate: return PreviewCandidate(state, command.Token, out message);
                case SpriteSwapCommandKind.Clear: return HasCustomEditor(state) ? DiscardCustomEdit(state, out message) : ClearActiveSwap(state, out message);
                case SpriteSwapCommandKind.Copy: return HasCustomEditor(state) ? CopyCustomPixels(state, out message) : CopyActiveSwap(state, out message);
                case SpriteSwapCommandKind.Paste: return HasCustomEditor(state) ? PasteCustomPixels(state, out message) : PasteSwap(state, out message);
                case SpriteSwapCommandKind.Undo: return Undo(state, out message);
                case SpriteSwapCommandKind.Redo: return Redo(state, out message);
                case SpriteSwapCommandKind.BeginCustomEdit: return BeginCustomEdit(state, out message);
                case SpriteSwapCommandKind.ImportPng: return ImportPngReplacement(state, out message);
                case SpriteSwapCommandKind.DiscardCustomEdit: return DiscardCustomEdit(state, out message);
                case SpriteSwapCommandKind.SelectTool: return SelectCustomTool(state, ToCustomTool(command.Tool), out message);
                case SpriteSwapCommandKind.SelectBrush: return SelectCustomBrush(state, command.First, out message);
                case SpriteSwapCommandKind.SelectPreset: return SelectCustomPreset(state, command.First, out message);
                case SpriteSwapCommandKind.SetColor: return SetCustomColor(state, command.Color, -1, out message);
                case SpriteSwapCommandKind.PaintPixel: return PaintCustomPixel(state, command.First, command.Second, out message);
                case SpriteSwapCommandKind.PickPixel: return PickCustomColor(state, command.First, command.Second, out message);
                case SpriteSwapCommandKind.BeginStroke: return BeginCustomPixelStroke(state, out message);
                case SpriteSwapCommandKind.BeginSelection: return StartCustomSelection(state, command.First, command.Second, out message);
                case SpriteSwapCommandKind.DragSelection: return DragCustomSelection(state, command.First, command.Second, out message);
                case SpriteSwapCommandKind.EndSelection: return EndCustomSelection(state, command.First, command.Second, out message);
                case SpriteSwapCommandKind.ClearSelection: return ClearCustomSelection(state, out message);
                case SpriteSwapCommandKind.CopyPixels: return CopyCustomPixels(state, out message);
                case SpriteSwapCommandKind.PastePixels: return PasteCustomPixels(state, out message);
                case SpriteSwapCommandKind.Zoom: return AdjustEditorZoom(state, command.First, out message);
                case SpriteSwapCommandKind.ResetZoom: return ResetEditorZoom(state, out message);
                case SpriteSwapCommandKind.SelectCharacterPart: return SwitchCharacterPart(state, command.CharacterPart, out message);
                case SpriteSwapCommandKind.SelectAnimationFrame: return SelectAnimationFrame(state, command.First, out message);
                case SpriteSwapCommandKind.CopyAnimationFrame: return CopyAnimationFrameFrom(state, command.First, out message);
                case SpriteSwapCommandKind.ToggleAnimationPlayback: return ToggleAnimationPlayback(state, out message);
                case SpriteSwapCommandKind.ToggleAnimationWorldPlayback: return ToggleAnimationWorldPlayback(state, out message);
                case SpriteSwapCommandKind.StepAnimationFrame: return StepAnimationFrame(state, command.First, out message);
                case SpriteSwapCommandKind.ToggleOnionSkin: return ToggleOnionSkin(state, out message);
                case SpriteSwapCommandKind.ToggleOriginalComparison: return ToggleOriginalComparison(state, out message);
                case SpriteSwapCommandKind.RevertAnimationFrame: return RevertAnimationFrame(state, out message);
                case SpriteSwapCommandKind.RevertAnimation: return RevertAnimation(state, out message);
                case SpriteSwapCommandKind.SetAnimationSpeed: return SetAnimationPlaybackSpeed(state, command.Number, out message);
                default:
                    message = "Sprite authoring command is not supported.";
                    return false;
            }
        }

        private static CustomEditorTool ToCustomTool(SpriteEditorTool tool)
        {
            switch (tool)
            {
                case SpriteEditorTool.Pick: return CustomEditorTool.Pick;
                case SpriteEditorTool.Select: return CustomEditorTool.Select;
                default: return CustomEditorTool.Paint;
            }
        }
    }
}
