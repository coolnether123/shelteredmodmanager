using System;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    public interface IScenarioAuthoringBackend
    {
        event Action<ScenarioAuthoringState> StateChanged;

        ScenarioAuthoringState CurrentState { get; }
        ScenarioAuthoringInspectorDocument GetShellDocument();
        ScenarioAuthoringInspectorDocument GetInspectorDocument();
        ScenarioAuthoringInspectorDocument GetHoverDocument();
        bool ExecuteAction(string actionId);
        void Refresh();
    }

    public static class ScenarioAuthoringActionIds
    {
        public const string ToggleShell = "sheltered.scenario_authoring.toggle_shell";
        public const string SelectionModifier = "sheltered.scenario_authoring.selection_modifier";
        public const string ConfirmSelection = "sheltered.scenario_authoring.confirm_selection";
        public const string ClearSelection = "sheltered.scenario_authoring.clear_selection";
        public const string SaveDraft = "sheltered.scenario_authoring.save_draft";
        public const string TogglePlaytest = "sheltered.scenario_authoring.toggle_playtest";

        public const string ActionShellToggle = "shell.toggle";
        public const string ActionSave = "editor.save";
        public const string ActionPlaytest = "editor.playtest.toggle";
        public const string ActionCloseEditor = "editor.close";
        public const string ActionConvertToNormal = "editor.convert_to_normal";
        public const string ActionSelectionClear = "selection.clear";
        public const string ActionCaptureFamily = "capture.family.current";
        public const string ActionCaptureInventory = "capture.inventory.current";
        public const string ActionCaptureShelterObjects = "capture.shelter.objects";
        public const string ActionCaptureSelectedObject = "capture.shelter.selected_object";
        public const string ActionRemoveSelectedObjectPlacement = "capture.shelter.remove_selected_object";
        public const string ActionToolSelect = "tool.select";
        public const string ActionToolFamily = "tool.family";
        public const string ActionToolInventory = "tool.inventory";
        public const string ActionToolShelter = "tool.shelter";
        public const string ActionToolObjects = "tool.objects";
        public const string ActionToolWiring = "tool.wiring";
        public const string ActionToolPeople = "tool.people";
        public const string ActionToolVehicle = "tool.vehicle";
        public const string ActionToolWinLoss = "tool.win_loss";
    }

    public enum ScenarioAuthoringTool
    {
        Select = 0,
        Family = 1,
        Inventory = 2,
        Shelter = 3,
        Objects = 4,
        Wiring = 5,
        People = 6,
        Vehicle = 7,
        WinLoss = 8
    }

    public enum ScenarioAuthoringTargetKind
    {
        None = 0,
        Unknown = 1,
        Character = 2,
        PlaceableObject = 3,
        Wall = 4,
        Wire = 5,
        Light = 6,
        Vehicle = 7,
        Room = 8,
        Tile = 9
    }

    public sealed class ScenarioAuthoringTarget
    {
        public string Id { get; set; }
        public ScenarioAuthoringTargetKind Kind { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string AdapterId { get; set; }
        public string GameObjectName { get; set; }
        public string TransformPath { get; set; }
        public UnityEngine.Object RuntimeObject { get; set; }
        public Vector3 WorldPosition { get; set; }
        public bool SupportsInspect { get; set; }
        public bool SupportsReplace { get; set; }

        public ScenarioAuthoringTarget Copy()
        {
            return new ScenarioAuthoringTarget
            {
                Id = Id,
                Kind = Kind,
                DisplayName = DisplayName,
                Description = Description,
                AdapterId = AdapterId,
                GameObjectName = GameObjectName,
                TransformPath = TransformPath,
                RuntimeObject = RuntimeObject,
                WorldPosition = WorldPosition,
                SupportsInspect = SupportsInspect,
                SupportsReplace = SupportsReplace
            };
        }
    }

    public sealed class ScenarioAuthoringState
    {
        public bool IsActive { get; set; }
        public bool ShellVisible { get; set; }
        public bool SelectionModeActive { get; set; }
        public ScenarioAuthoringTool ActiveTool { get; set; }
        public string ActiveDraftId { get; set; }
        public string ActiveScenarioFilePath { get; set; }
        public string StatusMessage { get; set; }
        public ScenarioAuthoringTarget HoveredTarget { get; set; }
        public ScenarioAuthoringTarget SelectedTarget { get; set; }

        public ScenarioAuthoringState Copy()
        {
            return new ScenarioAuthoringState
            {
                IsActive = IsActive,
                ShellVisible = ShellVisible,
                SelectionModeActive = SelectionModeActive,
                ActiveTool = ActiveTool,
                ActiveDraftId = ActiveDraftId,
                ActiveScenarioFilePath = ActiveScenarioFilePath,
                StatusMessage = StatusMessage,
                HoveredTarget = HoveredTarget != null ? HoveredTarget.Copy() : null,
                SelectedTarget = SelectedTarget != null ? SelectedTarget.Copy() : null
            };
        }
    }

    public sealed class ScenarioAuthoringInspectorDocument
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public ScenarioAuthoringInspectorAction[] HeaderActions { get; set; }
        public ScenarioAuthoringInspectorSection[] Sections { get; set; }
    }

    public sealed class ScenarioAuthoringInspectorAction
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Hint { get; set; }
        public bool Enabled { get; set; }
        public bool Emphasized { get; set; }
    }

    public sealed class ScenarioAuthoringInspectorSection
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public bool Expanded { get; set; }
        public ScenarioAuthoringInspectorItem[] Items { get; set; }
    }

    public enum ScenarioAuthoringInspectorItemKind
    {
        Text = 0,
        Property = 1,
        Action = 2
    }

    public sealed class ScenarioAuthoringInspectorItem
    {
        public ScenarioAuthoringInspectorItemKind Kind { get; set; }
        public string Label { get; set; }
        public string Value { get; set; }
        public ScenarioAuthoringInspectorAction Action { get; set; }
    }

    public sealed class ScenarioAuthoringTargetContext
    {
        public Camera Camera { get; set; }
        public Ray Ray { get; set; }
        public RaycastHit Hit { get; set; }
        public Collider Collider { get; set; }
        public GameObject GameObject { get; set; }
    }

    public interface IScenarioAuthoringTargetAdapter
    {
        string AdapterId { get; }
        int Priority { get; }
        bool TryCreateTarget(ScenarioAuthoringTargetContext context, out ScenarioAuthoringTarget target);
    }

    public sealed class ScenarioAuthoringPresentationSnapshot
    {
        public ScenarioAuthoringState State { get; set; }
        public ScenarioAuthoringInspectorDocument ShellDocument { get; set; }
        public ScenarioAuthoringInspectorDocument InspectorDocument { get; set; }
        public ScenarioAuthoringInspectorDocument HoverDocument { get; set; }
    }

    public interface IScenarioAuthoringRenderModule
    {
        string ModuleId { get; }
        int Priority { get; }
        bool CanRender();
        void Render(ScenarioAuthoringPresentationSnapshot snapshot);
        void Hide();
    }
}
