using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    public interface IScenarioAuthoringBackend
    {
        event Action<ScenarioAuthoringState> StateChanged;

        ScenarioAuthoringState CurrentState { get; }
        ScenarioAuthoringShellViewModel GetShellViewModel();
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
        public const string UndoKey = "sheltered.scenario_authoring.undo";
        public const string RedoKey = "sheltered.scenario_authoring.redo";
        public const string CopyKey = "sheltered.scenario_authoring.copy";
        public const string PasteKey = "sheltered.scenario_authoring.paste";
        public const string RevertKey = "sheltered.scenario_authoring.revert";

        public const string ActionShellToggle = "shell.toggle";
        public const string ActionShellShow = "shell.show";
        public const string ActionShellHideAll = "shell.hide_all";
        public const string ActionShellResetLayout = "shell.layout.reset";
        public const string ActionShellMinimalMode = "shell.layout.minimal_mode";
        public const string ActionShellFocusSelection = "shell.layout.focus_selection";
        public const string ActionShellOpenSettings = "shell.settings.open";
        public const string ActionShellCloseSettings = "shell.settings.close";
        public const string ActionShellSettingsReset = "shell.settings.reset";
        public const string ActionShellToggleWindowMenu = "shell.menu.windows";
        public const string ActionWindowTogglePrefix = "shell.window.toggle.";
        public const string ActionWindowCollapsePrefix = "shell.window.collapse.";
        public const string ActionWindowRestorePrefix = "shell.window.restore.";
        public const string ActionSettingTogglePrefix = "shell.setting.toggle.";
        public const string ActionSettingIncreasePrefix = "shell.setting.increase.";
        public const string ActionSettingDecreasePrefix = "shell.setting.decrease.";
        public const string ActionSettingSelectPrefix = "shell.setting.select.";
        public const string ActionInspectorTabPrefix = "inspector.tab.";
        public const string ActionStageSelectPrefix = "stage.select.";
        public const string ActionShellTabShelter = "shell.tab.shelter";
        public const string ActionShellTabBuild = "shell.tab.build";
        public const string ActionShellTabSurvivors = "shell.tab.survivors";
        public const string ActionShellTabStockpile = "shell.tab.stockpile";
        public const string ActionShellTabTriggers = "shell.tab.triggers";
        public const string ActionShellTabJobs = "shell.tab.jobs";
        public const string ActionShellTabQuests = "shell.tab.quests";
        public const string ActionShellTabArt = "shell.tab.art";
        public const string ActionShellTabMap = "shell.tab.map";
        public const string ActionShellTabTest = "shell.tab.test";
        public const string ActionShellTabPublish = "shell.tab.publish";
        public const string ActionShellTabShell = "shell.tab.shell";
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
        public const string ActionSpriteSwapClear = "sprite_swap.clear";
        public const string ActionSpriteSwapRevert = "sprite_swap.revert";
        public const string ActionSpriteSwapCopy = "sprite_swap.copy";
        public const string ActionSpriteSwapPaste = "sprite_swap.paste";
        public const string ActionSpriteSwapApplyPrefix = "sprite_swap.apply.";
        public const string ActionSpriteSwapPickerOpen = "sprite_swap.picker.open";
        public const string ActionSpriteSwapPickerSave = "sprite_swap.picker.save";
        public const string ActionSpriteSwapPickerCancel = "sprite_swap.picker.cancel";
        public const string ActionSpriteSwapPreviewPrefix = "sprite_swap.preview.";
        public const string ActionSpriteSwapCustomEditStart = "sprite_swap.custom.start";
        public const string ActionSpriteSwapCustomEditDiscard = "sprite_swap.custom.discard";
        public const string ActionSpriteSwapCustomBrushPrefix = "sprite_swap.custom.brush.";
        public const string ActionSpriteSwapCustomPaintPrefix = "sprite_swap.custom.paint.";
        public const string ActionSpriteSwapCustomPickPrefix = "sprite_swap.custom.pick.";
        public const string ActionSpriteSwapCustomToolPaint = "sprite_swap.custom.tool.paint";
        public const string ActionSpriteSwapCustomToolPick = "sprite_swap.custom.tool.pick";
        public const string ActionSpriteSwapCustomToolSelect = "sprite_swap.custom.tool.select";
        public const string ActionSpriteSwapCustomSelectionClear = "sprite_swap.custom.selection.clear";
        public const string ActionSpriteSwapCustomCopy = "sprite_swap.custom.copy";
        public const string ActionSpriteSwapCustomPaste = "sprite_swap.custom.paste";
        public const string ActionSpriteSwapCustomPresetPrefix = "sprite_swap.custom.preset.";
        public const string ActionSpriteSwapCustomColorPrefix = "sprite_swap.custom.color.";
        public const string ActionSpriteSwapCustomZoomIn = "sprite_swap.custom.zoom_in";
        public const string ActionSpriteSwapCustomZoomOut = "sprite_swap.custom.zoom_out";
        public const string ActionSpriteSwapCustomZoomReset = "sprite_swap.custom.zoom_reset";
        public const string ActionSpriteSwapCharacterPartHead = "sprite_swap.character.part.head";
        public const string ActionSpriteSwapCharacterPartTorso = "sprite_swap.character.part.torso";
        public const string ActionSpriteSwapCharacterPartLegs = "sprite_swap.character.part.legs";
        public const string ActionSpriteSwapCustomSelectStartPrefix = "sprite_swap.custom.select.start.";
        public const string ActionSpriteSwapCustomSelectDragPrefix = "sprite_swap.custom.select.drag.";
        public const string ActionSpriteSwapCustomSelectEndPrefix = "sprite_swap.custom.select.end.";
        public const string ActionHistoryUndo = "history.undo";
        public const string ActionHistoryRedo = "history.redo";
        public const string ActionSceneSpritePlacementRemove = "scene_sprite.remove";
        public const string ActionSceneSpritePlacementApplyPrefix = "scene_sprite.apply.";
        public const string ActionBuildPlacementCancel = "build.place.cancel";
        public const string ActionBuildObjectPlacePrefix = "build.place.object.";
        public const string ActionBuildStructureRoom = "build.place.room";
        public const string ActionBuildStructureLadder = "build.place.ladder";
        public const string ActionBuildStructureLight = "build.place.light";
        public const string ActionBuildWallApplyPrefix = "build.wall.apply.";
        public const string ActionBuildWireApplyPrefix = "build.wire.apply.";
        public const string ActionAssetModeReplace = "asset.mode.replace";
        public const string ActionAssetModePlace = "asset.mode.place";
        public const string ActionToolSelect = "tool.select";
        public const string ActionToolFamily = "tool.family";
        public const string ActionToolInventory = "tool.inventory";
        public const string ActionToolShelter = "tool.shelter";
        public const string ActionToolAssets = "tool.assets";
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
        WinLoss = 8,
        Assets = 9
    }

    public enum ScenarioAssetAuthoringMode
    {
        ReplaceExisting = 0,
        PlaceNew = 1
    }

    public enum ScenarioAuthoringShellTab
    {
        Shelter = 0,
        Build = 1,
        Survivors = 2,
        Stockpile = 3,
        Triggers = 4,
        Jobs = 5,
        Quests = 6,
        Art = 7,
        Map = 8,
        Test = 9,
        Publish = 10,
        Shell = 11
    }

    public enum ScenarioAuthoringInspectorTab
    {
        Properties = 0,
        Interactions = 1,
        Visuals = 2,
        Runtime = 3,
        Notes = 4
    }

    public enum ScenarioAuthoringShellDock
    {
        Top = 0,
        Left = 1,
        Right = 2,
        Bottom = 3,
        Overlay = 4,
        Floating = 5,
        Status = 6
    }

    public enum ScenarioAuthoringSettingKind
    {
        Toggle = 0,
        Float = 1,
        Integer = 2,
        Choice = 3,
        ReadOnly = 4
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
        Tile = 9,
        Background = 10,
        SceneSprite = 11
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
        public string ScenarioReferenceId { get; set; }
        public UnityEngine.Object RuntimeObject { get; set; }
        public UnityEngine.Object HighlightObject { get; set; }
        public Vector3 WorldPosition { get; set; }
        public int? GridX { get; set; }
        public int? GridY { get; set; }
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
                ScenarioReferenceId = ScenarioReferenceId,
                RuntimeObject = RuntimeObject,
                HighlightObject = HighlightObject,
                WorldPosition = WorldPosition,
                GridX = GridX,
                GridY = GridY,
                SupportsInspect = SupportsInspect,
                SupportsReplace = SupportsReplace
            };
        }
    }

    public sealed class ScenarioAuthoringState
    {
        public ScenarioAuthoringState()
        {
            WindowStates = new List<ScenarioAuthoringWindowState>();
            MultiSelection = new List<ScenarioAuthoringTarget>();
            ScrollStates = new List<ScenarioAuthoringPanelScrollState>();
            Settings = new ScenarioAuthoringSettingsSnapshot();
        }

        public bool IsActive { get; set; }
        public bool ShellVisible { get; set; }
        public bool SelectionModeActive { get; set; }
        public ScenarioStageKind ActiveStage { get; set; }
        public ScenarioStageKind ActiveBunkerStage { get; set; }
        public ScenarioAuthoringTool ActiveTool { get; set; }
        public ScenarioAuthoringShellTab ActiveShellTab { get; set; }
        public ScenarioAssetAuthoringMode AssetMode { get; set; }
        public string ActiveLayoutPreset { get; set; }
        public bool MinimalMode { get; set; }
        public bool FocusSelectionMode { get; set; }
        public string ActiveDraftId { get; set; }
        public string ActiveScenarioFilePath { get; set; }
        public string StatusMessage { get; set; }
        public ScenarioAuthoringTarget HoveredTarget { get; set; }
        public ScenarioAuthoringTarget SelectedTarget { get; set; }
        public List<ScenarioAuthoringTarget> MultiSelection { get; private set; }
        public string TimelineSelectionId { get; set; }
        public ScenarioAuthoringInspectorTab InspectorTab { get; set; }
        public string FilterText { get; set; }
        public string SearchText { get; set; }
        public bool SettingsWindowOpen { get; set; }
        public ScenarioSpriteSwapPickerState SpriteSwapPicker { get; set; }
        public List<ScenarioAuthoringWindowState> WindowStates { get; private set; }
        public List<ScenarioAuthoringPanelScrollState> ScrollStates { get; private set; }
        public ScenarioAuthoringSettingsSnapshot Settings { get; set; }

        public ScenarioAuthoringState Copy()
        {
            ScenarioAuthoringState copy = new ScenarioAuthoringState
            {
                IsActive = IsActive,
                ShellVisible = ShellVisible,
                SelectionModeActive = SelectionModeActive,
                ActiveStage = ActiveStage,
                ActiveBunkerStage = ActiveBunkerStage,
                ActiveTool = ActiveTool,
                ActiveShellTab = ActiveShellTab,
                AssetMode = AssetMode,
                ActiveLayoutPreset = ActiveLayoutPreset,
                MinimalMode = MinimalMode,
                FocusSelectionMode = FocusSelectionMode,
                ActiveDraftId = ActiveDraftId,
                ActiveScenarioFilePath = ActiveScenarioFilePath,
                StatusMessage = StatusMessage,
                HoveredTarget = HoveredTarget != null ? HoveredTarget.Copy() : null,
                SelectedTarget = SelectedTarget != null ? SelectedTarget.Copy() : null,
                TimelineSelectionId = TimelineSelectionId,
                InspectorTab = InspectorTab,
                FilterText = FilterText,
                SearchText = SearchText,
                SettingsWindowOpen = SettingsWindowOpen,
                SpriteSwapPicker = SpriteSwapPicker != null ? SpriteSwapPicker.Copy() : null,
                Settings = Settings != null ? Settings.Copy() : new ScenarioAuthoringSettingsSnapshot()
            };

            for (int i = 0; MultiSelection != null && i < MultiSelection.Count; i++)
            {
                ScenarioAuthoringTarget target = MultiSelection[i];
                if (target != null)
                    copy.MultiSelection.Add(target.Copy());
            }

            for (int i = 0; WindowStates != null && i < WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState state = WindowStates[i];
                if (state != null)
                    copy.WindowStates.Add(state.Copy());
            }

            for (int i = 0; ScrollStates != null && i < ScrollStates.Count; i++)
            {
                ScenarioAuthoringPanelScrollState scroll = ScrollStates[i];
                if (scroll != null)
                    copy.ScrollStates.Add(scroll.Copy());
            }

            return copy;
        }
    }

    public sealed class ScenarioSpriteSwapPickerState
    {
        public bool IsOpen { get; set; }
        public ScenarioAuthoringTarget Target { get; set; }
        public string TargetPath { get; set; }
        public string SavedCandidateToken { get; set; }
        public string SavedCandidateLabel { get; set; }
        public string PreviewCandidateToken { get; set; }
        public string PreviewCandidateLabel { get; set; }

        public ScenarioSpriteSwapPickerState Copy()
        {
            return new ScenarioSpriteSwapPickerState
            {
                IsOpen = IsOpen,
                Target = Target != null ? Target.Copy() : null,
                TargetPath = TargetPath,
                SavedCandidateToken = SavedCandidateToken,
                SavedCandidateLabel = SavedCandidateLabel,
                PreviewCandidateToken = PreviewCandidateToken,
                PreviewCandidateLabel = PreviewCandidateLabel
            };
        }
    }

    public sealed class ScenarioAuthoringWindowDefinition
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public ScenarioAuthoringShellDock Dock { get; set; }
        public bool DefaultVisible { get; set; }
        public bool DefaultCollapsed { get; set; }
        public bool DefaultPinned { get; set; }
        public int Order { get; set; }
        public float DefaultWidth { get; set; }
        public float DefaultHeight { get; set; }
        public float MinWidth { get; set; }
        public float MinHeight { get; set; }
    }

    public sealed class ScenarioAuthoringWindowState
    {
        public string Id { get; set; }
        public bool Visible { get; set; }
        public bool Collapsed { get; set; }
        public bool Pinned { get; set; }
        public int Order { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public ScenarioAuthoringWindowState Copy()
        {
            return new ScenarioAuthoringWindowState
            {
                Id = Id,
                Visible = Visible,
                Collapsed = Collapsed,
                Pinned = Pinned,
                Order = Order,
                Width = Width,
                Height = Height
            };
        }
    }

    public sealed class ScenarioAuthoringPanelScrollState
    {
        public string PanelId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        public ScenarioAuthoringPanelScrollState Copy()
        {
            return new ScenarioAuthoringPanelScrollState
            {
                PanelId = PanelId,
                X = X,
                Y = Y
            };
        }
    }

    public sealed class ScenarioAuthoringSettingDefinition
    {
        public string Id { get; set; }
        public string Section { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public ScenarioAuthoringSettingKind Kind { get; set; }
        public string DefaultValue { get; set; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; }
        public float Step { get; set; }
        public string[] ChoiceValues { get; set; }
        public string[] ChoiceLabels { get; set; }
    }

    public sealed class ScenarioAuthoringSettingValue
    {
        public string Id { get; set; }
        public string Value { get; set; }

        public ScenarioAuthoringSettingValue Copy()
        {
            return new ScenarioAuthoringSettingValue
            {
                Id = Id,
                Value = Value
            };
        }
    }

    public sealed class ScenarioAuthoringSettingsSnapshot
    {
        private readonly List<ScenarioAuthoringSettingValue> _values = new List<ScenarioAuthoringSettingValue>();

        public List<ScenarioAuthoringSettingValue> Values
        {
            get { return _values; }
        }

        public string Get(string id, string fallback)
        {
            for (int i = 0; i < _values.Count; i++)
            {
                ScenarioAuthoringSettingValue value = _values[i];
                if (value != null && string.Equals(value.Id, id, StringComparison.OrdinalIgnoreCase))
                    return value.Value ?? fallback;
            }

            return fallback;
        }

        public bool GetBool(string id, bool fallback)
        {
            string value = Get(id, fallback ? "true" : "false");
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }

        public int GetInt(string id, int fallback)
        {
            string value = Get(id, fallback.ToString());
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : fallback;
        }

        public float GetFloat(string id, float fallback)
        {
            string value = Get(id, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture));
            float parsed;
            return float.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out parsed)
                ? parsed
                : fallback;
        }

        public void Set(string id, string value)
        {
            if (string.IsNullOrEmpty(id))
                return;

            for (int i = 0; i < _values.Count; i++)
            {
                ScenarioAuthoringSettingValue entry = _values[i];
                if (entry != null && string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    entry.Value = value;
                    return;
                }
            }

            _values.Add(new ScenarioAuthoringSettingValue
            {
                Id = id,
                Value = value
            });
        }

        public ScenarioAuthoringSettingsSnapshot Copy()
        {
            ScenarioAuthoringSettingsSnapshot copy = new ScenarioAuthoringSettingsSnapshot();
            for (int i = 0; i < _values.Count; i++)
            {
                ScenarioAuthoringSettingValue entry = _values[i];
                if (entry != null)
                    copy.Values.Add(entry.Copy());
            }

            return copy;
        }
    }

    public sealed class ScenarioAuthoringShellWindowViewModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public ScenarioAuthoringShellDock Dock { get; set; }
        public bool Visible { get; set; }
        public bool Collapsed { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public ScenarioAuthoringInspectorAction[] HeaderActions { get; set; }
        public ScenarioAuthoringInspectorSection[] Sections { get; set; }
    }

    public sealed class ScenarioAuthoringSettingsItemViewModel
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public string ValueText { get; set; }
        public ScenarioAuthoringSettingKind Kind { get; set; }
        public bool BoolValue { get; set; }
        public bool Enabled { get; set; }
        public bool CanIncrease { get; set; }
        public bool CanDecrease { get; set; }
        public string[] ChoiceLabels { get; set; }
        public string[] ChoiceValues { get; set; }
        public int SelectedChoiceIndex { get; set; }
    }

    public sealed class ScenarioAuthoringSettingsSectionViewModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public ScenarioAuthoringSettingsItemViewModel[] Items { get; set; }
    }

    public sealed class ScenarioAuthoringSettingsViewModel
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public ScenarioAuthoringInspectorAction[] HeaderActions { get; set; }
        public ScenarioAuthoringSettingsSectionViewModel[] Sections { get; set; }
    }

    public sealed class ScenarioAuthoringContextMenuModel
    {
        public bool Visible { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public float AnchorX { get; set; }
        public float AnchorY { get; set; }
        public ScenarioAuthoringInspectorAction[] Actions { get; set; }

        public ScenarioAuthoringContextMenuModel Copy()
        {
            ScenarioAuthoringContextMenuModel copy = new ScenarioAuthoringContextMenuModel
            {
                Visible = Visible,
                Title = Title,
                Detail = Detail,
                AnchorX = AnchorX,
                AnchorY = AnchorY
            };

            if (Actions != null)
            {
                copy.Actions = new ScenarioAuthoringInspectorAction[Actions.Length];
                Array.Copy(Actions, copy.Actions, Actions.Length);
            }

            return copy;
        }
    }

    public sealed class ScenarioAuthoringShellViewModel
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string DraftLabel { get; set; }
        public string ModeLabel { get; set; }
        public string TimeLabel { get; set; }
        public ScenarioAuthoringInspectorAction[] Tabs { get; set; }
        public ScenarioAuthoringInspectorAction[] ToolbarActions { get; set; }
        public ScenarioAuthoringInspectorAction[] LayoutActions { get; set; }
        public ScenarioAuthoringInspectorAction[] WindowMenuActions { get; set; }
        public ScenarioAuthoringShellWindowViewModel[] Windows { get; set; }
        public ScenarioAuthoringInspectorDocument SpritePickerDocument { get; set; }
        public ScenarioAuthoringSettingsViewModel Settings { get; set; }
        public ScenarioAuthoringContextMenuModel ContextMenu { get; set; }
        public string[] StatusEntries { get; set; }
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
        public string Detail { get; set; }
        public string Badge { get; set; }
        public string IconText { get; set; }
        public Sprite PreviewSprite { get; set; }
        public bool Enabled { get; set; }
        public bool Emphasized { get; set; }
    }

    public enum ScenarioAuthoringInspectorSectionLayout
    {
        Default = 0,
        MetricGrid = 1,
        PropertyList = 2,
        NoteList = 3,
        ActionStrip = 4,
        TabStrip = 5,
        Summary = 6,
        CandidateGrid = 7
    }

    public sealed class ScenarioAuthoringInspectorSection
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public bool Expanded { get; set; }
        public ScenarioAuthoringInspectorSectionLayout Layout { get; set; }
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
        public string Detail { get; set; }
        public string Badge { get; set; }
        public string IconText { get; set; }
        public Sprite PreviewSprite { get; set; }
        public bool Emphasized { get; set; }
        public ScenarioAuthoringInspectorAction Action { get; set; }
    }

    public sealed class ScenarioAuthoringTargetContext
    {
        public Camera Camera { get; set; }
        public Ray Ray { get; set; }
        public RaycastHit Hit { get; set; }
        public Collider Collider { get; set; }
        public GameObject GameObject { get; set; }
        public Vector3 WorldPoint { get; set; }
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
        public ScenarioAuthoringShellViewModel ShellViewModel { get; set; }
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
