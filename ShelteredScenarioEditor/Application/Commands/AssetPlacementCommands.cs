using System.Globalization;
using UnityEngine;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Infrastructure.Assets;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal static class AssetBrowserAutomationIds
    {
        public const string InventoryPrefix = "asset_browser.inventory.";
        public const string RelinkPrefix = InventoryPrefix + "relink.";
        public const string RemovePrefix = InventoryPrefix + "remove.";
        public const string KeepPrefix = InventoryPrefix + "keep.";
        public const string CreditPrefix = InventoryPrefix + "credit.";
        public const string NavigatePrefix = InventoryPrefix + "navigate.";
    }

    internal enum PlacementOverlayCommandKind { Back, Done }

    internal sealed class PlacementOverlayCommand : ScenarioAuthoringCommand
    {
        private PlacementOverlayCommand(PlacementOverlayCommandKind kind, string automationId)
            : base(automationId, ScenarioAuthoringCommandPolicy.World)
        {
            Kind = kind;
        }

        public PlacementOverlayCommandKind Kind { get; private set; }
        public static PlacementOverlayCommand Back() { return new PlacementOverlayCommand(PlacementOverlayCommandKind.Back, ScenarioAuthoringActionIds.ActionRendererPlacementBack); }
        public static PlacementOverlayCommand Done() { return new PlacementOverlayCommand(PlacementOverlayCommandKind.Done, ScenarioAuthoringActionIds.ActionRendererPlacementDone); }
    }

    internal enum BuildPlacementCommandKind
    {
        Cancel,
        CommitGrid,
        StartObject,
        StartRoom,
        StartLadder,
        StartLight,
        DeleteObject,
        DeleteRoom,
        DeleteLadder,
        DeleteLight,
        ResetWall,
        ResetWire,
        ApplyWall,
        ApplyWire
    }

    internal sealed class BuildPlacementCommand : ScenarioAuthoringCommand
    {
        private BuildPlacementCommand(
            BuildPlacementCommandKind kind,
            ObjectManager.ObjectType objectType,
            int level,
            int gridX,
            int gridY,
            int catalogIndex,
            string runtimeSpriteKey,
            string automationId)
            : base(automationId,
                kind == BuildPlacementCommandKind.DeleteObject
                    || kind == BuildPlacementCommandKind.DeleteRoom
                    || kind == BuildPlacementCommandKind.DeleteLadder
                    || kind == BuildPlacementCommandKind.DeleteLight
                        ? ScenarioAuthoringCommandPolicy.WorldSafetySnapshot
                        : ScenarioAuthoringCommandPolicy.World)
        {
            Kind = kind;
            ObjectType = objectType;
            Level = level;
            GridX = gridX;
            GridY = gridY;
            CatalogIndex = catalogIndex;
            RuntimeSpriteKey = runtimeSpriteKey;
        }

        public BuildPlacementCommandKind Kind { get; private set; }
        public ObjectManager.ObjectType ObjectType { get; private set; }
        public int Level { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public int CatalogIndex { get; private set; }
        public string RuntimeSpriteKey { get; private set; }
        public bool StartsPlacement
        {
            get
            {
                return Kind == BuildPlacementCommandKind.StartObject
                    || Kind == BuildPlacementCommandKind.StartRoom
                    || Kind == BuildPlacementCommandKind.StartLadder
                    || Kind == BuildPlacementCommandKind.StartLight;
            }
        }

        public static BuildPlacementCommand Cancel() { return Simple(BuildPlacementCommandKind.Cancel, ScenarioAuthoringActionIds.ActionBuildPlacementCancel); }
        public static BuildPlacementCommand CommitGrid(int gridX, int gridY)
        {
            return new BuildPlacementCommand(BuildPlacementCommandKind.CommitGrid, default(ObjectManager.ObjectType), 0, gridX, gridY, -1, null,
                ScenarioAuthoringActionIds.ActionBuildPlacementCommitGridPrefix + gridX.ToString(CultureInfo.InvariantCulture) + "." + gridY.ToString(CultureInfo.InvariantCulture));
        }
        public static BuildPlacementCommand StartObject(ObjectManager.ObjectType objectType, int level)
        {
            string metadata = objectType + "|" + level.ToString(CultureInfo.InvariantCulture);
            return new BuildPlacementCommand(BuildPlacementCommandKind.StartObject, objectType, level, 0, 0, -1, null,
                ScenarioAuthoringActionIds.ActionBuildObjectPlacePrefix + ScenarioAutomationIdCodec.EncodeToken(metadata));
        }
        public static BuildPlacementCommand StartRoom() { return Simple(BuildPlacementCommandKind.StartRoom, ScenarioAuthoringActionIds.ActionBuildStructureRoom); }
        public static BuildPlacementCommand StartLadder() { return Simple(BuildPlacementCommandKind.StartLadder, ScenarioAuthoringActionIds.ActionBuildStructureLadder); }
        public static BuildPlacementCommand StartLight() { return Simple(BuildPlacementCommandKind.StartLight, ScenarioAuthoringActionIds.ActionBuildStructureLight); }
        public static BuildPlacementCommand DeleteObject() { return Simple(BuildPlacementCommandKind.DeleteObject, ScenarioAuthoringActionIds.ActionBuildDeleteObject); }
        public static BuildPlacementCommand DeleteRoom() { return Simple(BuildPlacementCommandKind.DeleteRoom, ScenarioAuthoringActionIds.ActionBuildDeleteRoom); }
        public static BuildPlacementCommand DeleteLadder() { return Simple(BuildPlacementCommandKind.DeleteLadder, ScenarioAuthoringActionIds.ActionBuildDeleteLadder); }
        public static BuildPlacementCommand DeleteLight() { return Simple(BuildPlacementCommandKind.DeleteLight, ScenarioAuthoringActionIds.ActionBuildDeleteLight); }
        public static BuildPlacementCommand ResetWall() { return Simple(BuildPlacementCommandKind.ResetWall, ScenarioAuthoringActionIds.ActionBuildResetWall); }
        public static BuildPlacementCommand ResetWire() { return Simple(BuildPlacementCommandKind.ResetWire, ScenarioAuthoringActionIds.ActionBuildResetWire); }
        public static BuildPlacementCommand ApplyWall(int catalogIndex) { return Apply(BuildPlacementCommandKind.ApplyWall, catalogIndex, null, ScenarioAuthoringActionIds.ActionBuildWallApplyPrefix); }
        public static BuildPlacementCommand ApplyWall(string runtimeSpriteKey) { return Apply(BuildPlacementCommandKind.ApplyWall, -1, runtimeSpriteKey, ScenarioAuthoringActionIds.ActionBuildWallApplyPrefix); }
        public static BuildPlacementCommand ApplyWire(int catalogIndex) { return Apply(BuildPlacementCommandKind.ApplyWire, catalogIndex, null, ScenarioAuthoringActionIds.ActionBuildWireApplyPrefix); }
        public static BuildPlacementCommand ApplyWire(string runtimeSpriteKey) { return Apply(BuildPlacementCommandKind.ApplyWire, -1, runtimeSpriteKey, ScenarioAuthoringActionIds.ActionBuildWireApplyPrefix); }

        private static BuildPlacementCommand Apply(BuildPlacementCommandKind kind, int index, string key, string prefix)
        {
            string suffix = index >= 0 ? index.ToString(CultureInfo.InvariantCulture) : ScenarioAutomationIdCodec.EncodeToken(key);
            return new BuildPlacementCommand(kind, default(ObjectManager.ObjectType), 0, 0, 0, index, key, prefix + suffix);
        }

        private static BuildPlacementCommand Simple(BuildPlacementCommandKind kind, string automationId)
        {
            return new BuildPlacementCommand(kind, default(ObjectManager.ObjectType), 0, 0, 0, -1, null, automationId);
        }

    }

    internal enum SceneSpritePlacementCommandKind { Start, Remove, Cancel }

    internal sealed class SceneSpritePlacementCommand : ScenarioAuthoringCommand
    {
        private SceneSpritePlacementCommand(SceneSpritePlacementCommandKind kind, string candidateToken, string automationId)
            : base(automationId, kind == SceneSpritePlacementCommandKind.Remove
                ? ScenarioAuthoringCommandPolicy.WorldSafetySnapshot
                : ScenarioAuthoringCommandPolicy.World)
        {
            Kind = kind;
            CandidateToken = candidateToken;
        }

        public SceneSpritePlacementCommandKind Kind { get; private set; }
        public string CandidateToken { get; private set; }
        public static SceneSpritePlacementCommand Start(string token)
        {
            return new SceneSpritePlacementCommand(SceneSpritePlacementCommandKind.Start, token,
                ScenarioAuthoringActionIds.ActionSceneSpritePlacementApplyPrefix + ScenarioAutomationIdCodec.EncodeToken(token));
        }
        public static SceneSpritePlacementCommand Remove() { return new SceneSpritePlacementCommand(SceneSpritePlacementCommandKind.Remove, null, ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove); }
        public static SceneSpritePlacementCommand Cancel() { return new SceneSpritePlacementCommand(SceneSpritePlacementCommandKind.Cancel, null, ScenarioAuthoringActionIds.ActionSceneSpritePlacementCancel); }
    }

    internal enum SpriteSwapCommandKind
    {
        OpenPicker, SavePicker, CancelPicker, ApplyCandidate, PreviewCandidate, Clear, Copy, Paste, Undo, Redo,
        BeginCustomEdit, ImportPng, DiscardCustomEdit, SelectTool, SelectBrush, SelectPreset, SetColor,
        PaintPixel, PickPixel, BeginStroke, BeginSelection, DragSelection, EndSelection, ClearSelection,
        CopyPixels, PastePixels, Zoom, ResetZoom, SelectCharacterPart, SelectAnimationFrame, CopyAnimationFrame,
        ToggleAnimationPlayback, ToggleAnimationWorldPlayback, StepAnimationFrame, ToggleOnionSkin,
        ToggleOriginalComparison, RevertAnimationFrame, RevertAnimation, SetAnimationSpeed
    }

    internal enum SpriteEditorTool { Paint, Pick, Select }

    internal sealed class SpriteSwapCommand : ScenarioAuthoringCommand
    {
        private SpriteSwapCommand(SpriteSwapCommandKind kind, string token, int first, int second, float number, Color color, SpriteEditorTool tool, ScenarioEditorCharacterTexturePart characterPart, string automationId)
            : base(automationId, ScenarioAuthoringCommandPolicy.Default)
        {
            Kind = kind;
            Token = token;
            First = first;
            Second = second;
            Number = number;
            Color = color;
            Tool = tool;
            CharacterPart = characterPart;
        }

        public SpriteSwapCommandKind Kind { get; private set; }
        public string Token { get; private set; }
        public int First { get; private set; }
        public int Second { get; private set; }
        public float Number { get; private set; }
        public Color Color { get; private set; }
        public SpriteEditorTool Tool { get; private set; }
        public ScenarioEditorCharacterTexturePart CharacterPart { get; private set; }

        public static SpriteSwapCommand OpenPicker() { return Simple(SpriteSwapCommandKind.OpenPicker, ScenarioAuthoringActionIds.ActionSpriteSwapPickerOpen); }
        public static SpriteSwapCommand SavePicker() { return Simple(SpriteSwapCommandKind.SavePicker, ScenarioAuthoringActionIds.ActionSpriteSwapPickerSave); }
        public static SpriteSwapCommand CancelPicker() { return Simple(SpriteSwapCommandKind.CancelPicker, ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel); }
        public static SpriteSwapCommand Clear() { return Simple(SpriteSwapCommandKind.Clear, ScenarioAuthoringActionIds.ActionSpriteSwapClear); }
        public static SpriteSwapCommand Revert() { return Simple(SpriteSwapCommandKind.Clear, ScenarioAuthoringActionIds.ActionSpriteSwapRevert); }
        public static SpriteSwapCommand Copy() { return Simple(SpriteSwapCommandKind.Copy, ScenarioAuthoringActionIds.ActionSpriteSwapCopy); }
        public static SpriteSwapCommand Paste() { return Simple(SpriteSwapCommandKind.Paste, ScenarioAuthoringActionIds.ActionSpriteSwapPaste); }
        public static SpriteSwapCommand Undo() { return Simple(SpriteSwapCommandKind.Undo, ScenarioAuthoringActionIds.ActionHistoryUndo); }
        public static SpriteSwapCommand Redo() { return Simple(SpriteSwapCommandKind.Redo, ScenarioAuthoringActionIds.ActionHistoryRedo); }
        public static SpriteSwapCommand BeginCustomEdit() { return Simple(SpriteSwapCommandKind.BeginCustomEdit, ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditStart); }
        public static SpriteSwapCommand ImportPng() { return Simple(SpriteSwapCommandKind.ImportPng, ScenarioAuthoringActionIds.ActionSpriteSwapImportPng); }
        public static SpriteSwapCommand DiscardCustomEdit() { return Simple(SpriteSwapCommandKind.DiscardCustomEdit, ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditDiscard); }
        public static SpriteSwapCommand BeginStroke() { return Simple(SpriteSwapCommandKind.BeginStroke, ScenarioAuthoringActionIds.ActionSpriteSwapCustomStrokeBegin); }
        public static SpriteSwapCommand ClearSelection() { return Simple(SpriteSwapCommandKind.ClearSelection, ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectionClear); }
        public static SpriteSwapCommand CopyPixels() { return Simple(SpriteSwapCommandKind.CopyPixels, ScenarioAuthoringActionIds.ActionSpriteSwapCustomCopy); }
        public static SpriteSwapCommand PastePixels() { return Simple(SpriteSwapCommandKind.PastePixels, ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaste); }
        public static SpriteSwapCommand ResetZoom() { return Simple(SpriteSwapCommandKind.ResetZoom, ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomReset); }
        public static SpriteSwapCommand ToggleAnimationPlayback() { return Simple(SpriteSwapCommandKind.ToggleAnimationPlayback, ScenarioAuthoringActionIds.ActionSpriteSwapAnimationPlayPause); }
        public static SpriteSwapCommand ToggleAnimationWorldPlayback() { return Simple(SpriteSwapCommandKind.ToggleAnimationWorldPlayback, ScenarioAuthoringActionIds.ActionSpriteSwapAnimationPlayInWorld); }
        public static SpriteSwapCommand ToggleOnionSkin() { return Simple(SpriteSwapCommandKind.ToggleOnionSkin, ScenarioAuthoringActionIds.ActionSpriteSwapAnimationOnionToggle); }
        public static SpriteSwapCommand ToggleOriginalComparison() { return Simple(SpriteSwapCommandKind.ToggleOriginalComparison, ScenarioAuthoringActionIds.ActionSpriteSwapAnimationCompareToggle); }
        public static SpriteSwapCommand RevertAnimationFrame() { return Simple(SpriteSwapCommandKind.RevertAnimationFrame, ScenarioAuthoringActionIds.ActionSpriteSwapAnimationRevertFrame); }
        public static SpriteSwapCommand RevertAnimation() { return Simple(SpriteSwapCommandKind.RevertAnimation, ScenarioAuthoringActionIds.ActionSpriteSwapAnimationRevertAll); }
        public static SpriteSwapCommand ApplyCandidate(string token) { return TokenCommand(SpriteSwapCommandKind.ApplyCandidate, token, ScenarioAuthoringActionIds.ActionSpriteSwapApplyPrefix); }
        public static SpriteSwapCommand PreviewCandidate(string token) { return TokenCommand(SpriteSwapCommandKind.PreviewCandidate, token, ScenarioAuthoringActionIds.ActionSpriteSwapPreviewPrefix); }
        public static SpriteSwapCommand SelectBrush(int index) { return IntCommand(SpriteSwapCommandKind.SelectBrush, index, ScenarioAuthoringActionIds.ActionSpriteSwapCustomBrushPrefix); }
        public static SpriteSwapCommand SelectPreset(int index) { return IntCommand(SpriteSwapCommandKind.SelectPreset, index, ScenarioAuthoringActionIds.ActionSpriteSwapCustomPresetPrefix); }
        public static SpriteSwapCommand SelectAnimationFrame(int index) { return IntCommand(SpriteSwapCommandKind.SelectAnimationFrame, index, ScenarioAuthoringActionIds.ActionSpriteSwapAnimationFramePrefix); }
        public static SpriteSwapCommand CopyAnimationFrame(int index) { return IntCommand(SpriteSwapCommandKind.CopyAnimationFrame, index, ScenarioAuthoringActionIds.ActionSpriteSwapAnimationCopyPrefix); }
        public static SpriteSwapCommand StepAnimationFrame(int delta) { return IntCommand(SpriteSwapCommandKind.StepAnimationFrame, delta, delta < 0 ? ScenarioAuthoringActionIds.ActionSpriteSwapAnimationStepPrevious : ScenarioAuthoringActionIds.ActionSpriteSwapAnimationStepNext); }
        public static SpriteSwapCommand Zoom(int delta) { return IntCommand(SpriteSwapCommandKind.Zoom, delta, delta < 0 ? ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomOut : ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomIn); }
        public static SpriteSwapCommand SelectTool(SpriteEditorTool tool)
        {
            string id = tool == SpriteEditorTool.Paint ? ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPaint : tool == SpriteEditorTool.Pick ? ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPick : ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolSelect;
            return new SpriteSwapCommand(SpriteSwapCommandKind.SelectTool, null, 0, 0, 0f, Color.clear, tool, default(ScenarioEditorCharacterTexturePart), id);
        }
        public static SpriteSwapCommand SelectCharacterPart(ScenarioEditorCharacterTexturePart part)
        {
            string id = part == ScenarioEditorCharacterTexturePart.Head ? ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartHead : part == ScenarioEditorCharacterTexturePart.Torso ? ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartTorso : ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartLegs;
            return new SpriteSwapCommand(SpriteSwapCommandKind.SelectCharacterPart, null, 0, 0, 0f, Color.clear, default(SpriteEditorTool), part, id);
        }
        public static SpriteSwapCommand SetColor(Color color) { return new SpriteSwapCommand(SpriteSwapCommandKind.SetColor, null, 0, 0, 0f, color, default(SpriteEditorTool), default(ScenarioEditorCharacterTexturePart), ScenarioAuthoringActionIds.ActionSpriteSwapCustomColorPrefix + ColorUtility.ToHtmlStringRGBA(color)); }
        public static SpriteSwapCommand PaintPixel(int x, int y) { return Pixel(SpriteSwapCommandKind.PaintPixel, x, y, ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaintPrefix); }
        public static SpriteSwapCommand PickPixel(int x, int y) { return Pixel(SpriteSwapCommandKind.PickPixel, x, y, ScenarioAuthoringActionIds.ActionSpriteSwapCustomPickPrefix); }
        public static SpriteSwapCommand BeginSelection(int x, int y) { return Pixel(SpriteSwapCommandKind.BeginSelection, x, y, ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectStartPrefix); }
        public static SpriteSwapCommand DragSelection(int x, int y) { return Pixel(SpriteSwapCommandKind.DragSelection, x, y, ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectDragPrefix); }
        public static SpriteSwapCommand EndSelection(int x, int y) { return Pixel(SpriteSwapCommandKind.EndSelection, x, y, ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectEndPrefix); }
        public static SpriteSwapCommand SetAnimationSpeed(float speed) { return new SpriteSwapCommand(SpriteSwapCommandKind.SetAnimationSpeed, null, 0, 0, speed, Color.clear, default(SpriteEditorTool), default(ScenarioEditorCharacterTexturePart), ScenarioAuthoringActionIds.ActionSpriteSwapAnimationSpeedPrefix + speed.ToString("0.##", CultureInfo.InvariantCulture)); }

        private static SpriteSwapCommand Simple(SpriteSwapCommandKind kind, string id) { return new SpriteSwapCommand(kind, null, 0, 0, 0f, Color.clear, default(SpriteEditorTool), default(ScenarioEditorCharacterTexturePart), id); }
        private static SpriteSwapCommand TokenCommand(SpriteSwapCommandKind kind, string token, string prefix) { return new SpriteSwapCommand(kind, token, 0, 0, 0f, Color.clear, default(SpriteEditorTool), default(ScenarioEditorCharacterTexturePart), prefix + ScenarioAutomationIdCodec.EncodeToken(token)); }
        private static SpriteSwapCommand IntCommand(SpriteSwapCommandKind kind, int value, string prefix) { return new SpriteSwapCommand(kind, null, value, 0, 0f, Color.clear, default(SpriteEditorTool), default(ScenarioEditorCharacterTexturePart), prefix + value.ToString(CultureInfo.InvariantCulture)); }
        private static SpriteSwapCommand Pixel(SpriteSwapCommandKind kind, int x, int y, string prefix) { return new SpriteSwapCommand(kind, null, x, y, 0f, Color.clear, default(SpriteEditorTool), default(ScenarioEditorCharacterTexturePart), prefix + x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture)); }
    }

    internal sealed class AssetBrowserSelection
    {
        public string AutomationId { get; set; }
        public ScenarioAuthoringCommand PrimaryCommand { get; set; }
        public ScenarioAuthoringTool Tool { get; set; }
        public string EditableTargetId { get; set; }
    }

    internal enum AssetBrowserCommandKind { Select, PlaceSelected, EditSelected, Relink, Remove, Keep, SetCredit, Navigate }

    internal sealed class AssetBrowserCommand : ScenarioAuthoringCommand, IScenarioTextValueCommand
    {
        private AssetBrowserCommand(AssetBrowserCommandKind kind, AssetBrowserSelection selection, string value, string text, string automationId)
            : base(automationId,
                kind == AssetBrowserCommandKind.PlaceSelected
                    ? ScenarioAuthoringCommandPolicy.World
                    : (kind == AssetBrowserCommandKind.Remove
                        ? ScenarioAuthoringCommandPolicy.SafetySnapshot
                        : ScenarioAuthoringCommandPolicy.Default))
        {
            Kind = kind;
            Selection = selection;
            Value = value;
            Text = text;
        }

        public AssetBrowserCommandKind Kind { get; private set; }
        public AssetBrowserSelection Selection { get; private set; }
        public string Value { get; private set; }
        public string Text { get; private set; }
        public static AssetBrowserCommand Select(AssetBrowserSelection selection) { return new AssetBrowserCommand(AssetBrowserCommandKind.Select, selection, null, null, ScenarioAuthoringActionIds.ActionAssetBrowserSelectPrefix + (selection != null ? selection.AutomationId : string.Empty)); }
        public static AssetBrowserCommand PlaceSelected() { return Simple(AssetBrowserCommandKind.PlaceSelected, ScenarioAuthoringActionIds.ActionAssetBrowserPlaceSelected); }
        public static AssetBrowserCommand EditSelected() { return Simple(AssetBrowserCommandKind.EditSelected, ScenarioAuthoringActionIds.ActionAssetBrowserEditSelected); }
        public static AssetBrowserCommand Relink(string path) { return ValueCommand(AssetBrowserCommandKind.Relink, path, AssetBrowserAutomationIds.RelinkPrefix); }
        public static AssetBrowserCommand Remove(string path) { return ValueCommand(AssetBrowserCommandKind.Remove, path, AssetBrowserAutomationIds.RemovePrefix); }
        public static AssetBrowserCommand Keep(string path) { return ValueCommand(AssetBrowserCommandKind.Keep, path, AssetBrowserAutomationIds.KeepPrefix); }
        public static AssetBrowserCommand SetCredit(string path, string credit) { return new AssetBrowserCommand(AssetBrowserCommandKind.SetCredit, null, path, credit, AssetBrowserAutomationIds.CreditPrefix + ScenarioAutomationIdCodec.EncodeToken(path)); }
        public static AssetBrowserCommand Navigate(string reference) { return ValueCommand(AssetBrowserCommandKind.Navigate, reference, AssetBrowserAutomationIds.NavigatePrefix); }
        public ScenarioAuthoringCommand WithTextValue(string value) { return Kind == AssetBrowserCommandKind.SetCredit ? SetCredit(Value, value) : this; }
        private static AssetBrowserCommand Simple(AssetBrowserCommandKind kind, string id) { return new AssetBrowserCommand(kind, null, null, null, id); }
        private static AssetBrowserCommand ValueCommand(AssetBrowserCommandKind kind, string value, string prefix) { return new AssetBrowserCommand(kind, null, value, null, prefix + ScenarioAutomationIdCodec.EncodeToken(value)); }
    }
}
