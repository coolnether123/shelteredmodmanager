using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Content;
using ShelteredAPI.Saves;
using ShelteredScenarioEditor.Application.Assets;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Authoring.Supplies;
using ShelteredScenarioEditor.Application.Authoring.Tutorial;
using ShelteredScenarioEditor.Application.Map;
using ShelteredScenarioEditor.Application.Objects;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Application.Selection;
using ShelteredScenarioEditor.Application.Stages;
using ShelteredScenarioEditor.Application.Timeline;
using ShelteredScenarioEditor.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Journal;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Public;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredScenarioEditor.Domain.Timeline;
using ShelteredScenarioEditor.Infrastructure.Assets;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;
namespace ShelteredScenarioEditor.Application.Commands{
    internal sealed class PlacementOverlayCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacement;
        private readonly ScenarioSceneSpritePlacementAuthoringService _sceneSpritePlacement;
        private readonly ScenarioAuthoringLayoutService _layoutService;

        public PlacementOverlayCommandHandler(
            ScenarioBuildPlacementAuthoringService buildPlacement,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacement,
            ScenarioAuthoringLayoutService layoutService)
        {
            _buildPlacement = buildPlacement;
            _sceneSpritePlacement = sceneSpritePlacement;
            _layoutService = layoutService;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is PlacementOverlayCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            PlacementOverlayCommand placement = command as PlacementOverlayCommand;
            string message = "Character editor service is unavailable.";
            bool changed = CancelPlacement(state, out message);
            if (placement != null && placement.Kind == PlacementOverlayCommandKind.Done)
                changed |= _layoutService.SetWindowOpen(state, ScenarioAuthoringWindowIds.BuildTools, false);
            if (string.IsNullOrEmpty(message))
                message = changed ? "Placement closed." : "No placement was active.";
            return Result(changed, message);
        }

        private bool CancelPlacement(ScenarioAuthoringState state, out string message)
        {
            message = null;
            bool changed = false;
            string nextMessage;
            if (_buildPlacement != null && _buildPlacement.Execute(state, BuildPlacementCommand.Cancel(), out nextMessage))
            {
                changed = true;
                message = nextMessage;
            }
            if (_sceneSpritePlacement != null && _sceneSpritePlacement.Execute(state, SceneSpritePlacementCommand.Cancel(), out nextMessage))
            {
                changed = true;
                message = nextMessage;
            }
            return changed;
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }
    }

    internal sealed class AssetBrowserCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacement;
        private readonly ScenarioSceneSpritePlacementAuthoringService _sceneSpritePlacement;
        private readonly ScenarioSpriteSwapAuthoringService _spriteSwap;
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly ScenarioWeatherEffectSpriteCatalogService _weatherEffectSpriteCatalog;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAssetInventoryMutationService _assetInventoryMutations;
        private readonly IScenarioEditorSessionStore _sessionStore;
        private readonly ScenarioAuthoringSettingsService _settingsService;
        private readonly BuildCommandHandler _buildHandler;
        private readonly SceneSpriteCommandHandler _sceneSpriteHandler;
        private readonly SpriteCommandHandler _spriteHandler;

        public AssetBrowserCommandHandler(
            ScenarioBuildPlacementAuthoringService buildPlacement,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacement,
            ScenarioSpriteSwapAuthoringService spriteSwap,
            ScenarioAuthoringLayoutService layoutService,
            ScenarioWeatherEffectSpriteCatalogService weatherEffectSpriteCatalog,
            IScenarioEditorService editorService,
            ScenarioAssetInventoryMutationService assetInventoryMutations,
            IScenarioEditorSessionStore sessionStore,
            ScenarioSelectionScopeService scopeService,
            ScenarioAuthoringSettingsService settingsService)
        {
            _buildPlacement = buildPlacement;
            _sceneSpritePlacement = sceneSpritePlacement;
            _spriteSwap = spriteSwap;
            _layoutService = layoutService;
            _weatherEffectSpriteCatalog = weatherEffectSpriteCatalog;
            _editorService = editorService;
            _assetInventoryMutations = assetInventoryMutations;
            _sessionStore = sessionStore;
            _settingsService = settingsService;
            _buildHandler = new BuildCommandHandler(buildPlacement, sceneSpritePlacement);
            _sceneSpriteHandler = new SceneSpriteCommandHandler(sceneSpritePlacement, buildPlacement, scopeService);
            _spriteHandler = new SpriteCommandHandler(spriteSwap, scopeService, layoutService, buildPlacement);
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is AssetBrowserCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            AssetBrowserCommand asset = command as AssetBrowserCommand;
            string message;
            bool changed;
            if (state == null || asset == null)
                return Result(false, "Asset browser is unavailable.");
            switch (asset.Kind)
            {
                case AssetBrowserCommandKind.Select:
                    changed = SelectAsset(state, asset.Selection, out message);
                    if (changed)
                        ScenarioAssetBrowserUx.RecordRecent(state, asset.Selection.AutomationId, _settingsService);
                    break;
                case AssetBrowserCommandKind.PlaceSelected: changed = PlaceSelectedAsset(state, out message); break;
                case AssetBrowserCommandKind.EditSelected: changed = EditSelectedAsset(state, out message); break;
                default: changed = HandleInventoryAction(state, asset, out message); break;
            }
            return Result(changed, message);
        }

        private bool HandleInventoryAction(ScenarioAuthoringState state, AssetBrowserCommand command, out string message)
        {
            message = null;
            if (_assetInventoryMutations == null)
            {
                message = "Asset inventory actions are unavailable.";
                return false;
            }

            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            string currentFilePath = _sessionStore != null ? _sessionStore.CurrentFilePath : null;
            switch (command.Kind)
            {
                case AssetBrowserCommandKind.Relink:
                    return _assetInventoryMutations.RelinkMissing(session, currentFilePath, command.Value, out message);
                case AssetBrowserCommandKind.Remove:
                    return _assetInventoryMutations.RemoveOrphan(session, currentFilePath, command.Value, out message);
                case AssetBrowserCommandKind.Keep:
                    return _assetInventoryMutations.KeepOrphan(command.Value, out message);
                case AssetBrowserCommandKind.SetCredit:
                    return _assetInventoryMutations.SetCredit(session, command.Value, command.Text, out message);
                case AssetBrowserCommandKind.Navigate:
                    if (_layoutService != null) _layoutService.SelectTool(state, ScenarioAuthoringTool.Assets);
                    message = "Opened the closest editor workspace for this asset reference.";
                    return true;
                default:
                    message = "Asset inventory command is not supported.";
                    return false;
            }
        }

        private static bool SelectAsset(ScenarioAuthoringState state, AssetBrowserSelection selection, out string message)
        {
            if (selection == null || string.IsNullOrEmpty(selection.AutomationId))
            {
                message = "Asset browser selection is unavailable.";
                return false;
            }

            state.AssetBrowserSelectedActionId = selection.AutomationId;
            state.AssetBrowserSelection = selection;
            message = "Asset selected in browser.";
            return true;
        }

        private bool PlaceSelectedAsset(ScenarioAuthoringState state, out string message)
        {
            message = null;
            AssetBrowserSelection selection = state != null ? state.AssetBrowserSelection : null;
            if (selection == null || selection.PrimaryCommand == null)
            {
                message = "Select an asset before placing it in the world.";
                return false;
            }

            if (_layoutService != null)
                _layoutService.SelectTool(state, selection.Tool);
            ScenarioCommandDispatchResult result;
            if (selection.PrimaryCommand is SceneSpritePlacementCommand)
            {
                result = _sceneSpriteHandler.Handle(state, selection.PrimaryCommand);
                message = result.Message;
                return result.Changed;
            }
            if (selection.PrimaryCommand is BuildPlacementCommand)
            {
                result = _buildHandler.Handle(state, selection.PrimaryCommand);
                message = result.Message;
                return result.Changed;
            }

            message = "The selected asset is not placeable.";
            return false;
        }

        private bool EditSelectedAsset(ScenarioAuthoringState state, out string message)
        {
            message = null;
            AssetBrowserSelection selection = state != null ? state.AssetBrowserSelection : null;
            if (selection == null)
            {
                message = "Select an editable art asset before opening the pixel editor.";
                return false;
            }

            if (string.IsNullOrEmpty(selection.EditableTargetId))
            {
                message = "The selected asset does not expose an editable sprite target.";
                return false;
            }

            string targetId = selection.EditableTargetId;
            ScenarioAuthoringTarget target;
            if (_weatherEffectSpriteCatalog == null || !_weatherEffectSpriteCatalog.TryFindTarget(targetId, out target) || target == null)
            {
                message = "Editable art target is not loaded: " + targetId + ".";
                return false;
            }

            state.SelectedTarget = target.Copy();
            state.HoveredTarget = target.Copy();
            state.MultiSelection.Clear();
            state.MultiSelection.Add(target.Copy());

            ScenarioCommandDispatchResult result = _spriteHandler.Handle(state, SpriteSwapCommand.BeginCustomEdit());
            message = result.Message;
            return result.Changed;
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }
    }

    internal sealed class SpriteCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioSpriteSwapAuthoringService _service;
        private readonly ScenarioSelectionScopeService _scopeService;
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacement;

        public SpriteCommandHandler(
            ScenarioSpriteSwapAuthoringService service,
            ScenarioSelectionScopeService scopeService,
            ScenarioAuthoringLayoutService layoutService,
            ScenarioBuildPlacementAuthoringService buildPlacement)
        {
            _service = service;
            _scopeService = scopeService;
            _layoutService = layoutService;
            _buildPlacement = buildPlacement;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is SpriteSwapCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            SpriteSwapCommand sprite = command as SpriteSwapCommand;
            string message;
            if (_service == null || sprite == null)
                return Result(false, "Sprite authoring is unavailable.");
            if (RequiresScopedTarget(sprite.Kind) && !_scopeService.CanSelectTargetForCurrentStage(state, state.SelectedTarget, out message))
            {
                return Result(true, message);
            }

            bool changed = _service.Execute(state, sprite, out message);
            if (changed
                && sprite.Kind == SpriteSwapCommandKind.BeginCustomEdit
                && _buildPlacement != null)
            {
                _buildPlacement.Reset();
            }

            if (changed
                && sprite.Kind == SpriteSwapCommandKind.BeginCustomEdit
                && _layoutService != null)
            {
                _layoutService.BeginPixelEditorFocus(state);
                changed |= _layoutService.SetWindowOpen(state, ScenarioAuthoringWindowIds.PixelEditor, true);
            }

            if (IsPixelEditorTerminalAction(sprite.Kind) && _service.GetCustomEditorModel(state) == null && _layoutService != null)
            {
                changed |= _layoutService.SetWindowOpen(state, ScenarioAuthoringWindowIds.PixelEditor, false);
                _layoutService.EndPixelEditorFocus(state);
            }
            return Result(changed, message);
        }

        private static bool RequiresScopedTarget(SpriteSwapCommandKind kind)
        {
            return kind != SpriteSwapCommandKind.Undo
                && kind != SpriteSwapCommandKind.Redo
                && kind != SpriteSwapCommandKind.CancelPicker;
        }

        private static bool IsPixelEditorTerminalAction(SpriteSwapCommandKind kind)
        {
            return kind == SpriteSwapCommandKind.SavePicker
                || kind == SpriteSwapCommandKind.CancelPicker
                || kind == SpriteSwapCommandKind.DiscardCustomEdit
                || kind == SpriteSwapCommandKind.Clear;
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }
    }

    internal sealed class SceneSpriteCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioSceneSpritePlacementAuthoringService _service;
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacement;
        private readonly ScenarioSelectionScopeService _scopeService;

        public SceneSpriteCommandHandler(
            ScenarioSceneSpritePlacementAuthoringService service,
            ScenarioBuildPlacementAuthoringService buildPlacement,
            ScenarioSelectionScopeService scopeService)
        {
            _service = service;
            _buildPlacement = buildPlacement;
            _scopeService = scopeService;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is SceneSpritePlacementCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            SceneSpritePlacementCommand sprite = command as SceneSpritePlacementCommand;
            string message;
            if (_service == null || sprite == null)
                return Result(false, "Scene sprite placement is unavailable.");
            if (state != null
                && state.SelectedTarget != null
                && !_scopeService.CanSelectTargetForCurrentStage(state, state.SelectedTarget, out message))
            {
                return Result(true, message);
            }

            bool changed = _service.Execute(state, sprite, out message);
            if (changed && sprite.Kind == SceneSpritePlacementCommandKind.Start && _service.HasActivePlacement && _buildPlacement != null)
                _buildPlacement.Reset();

            return Result(changed, message);
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }
    }

    internal sealed class BuildCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioBuildPlacementAuthoringService _service;
        private readonly ScenarioSceneSpritePlacementAuthoringService _sceneSpritePlacement;

        public BuildCommandHandler(
            ScenarioBuildPlacementAuthoringService service,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacement)
        {
            _service = service;
            _sceneSpritePlacement = sceneSpritePlacement;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is BuildPlacementCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            BuildPlacementCommand build = command as BuildPlacementCommand;
            string message;
            if (_service == null || build == null)
                return Result(false, "Build placement is unavailable.");
            bool changed = _service.Execute(state, build, out message);
            if (changed && build.StartsPlacement && _service.HasActivePlacement && _sceneSpritePlacement != null)
                _sceneSpritePlacement.Reset();

            return Result(changed, message);
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }
    }

    internal sealed class EditHistoryCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioSpriteSwapAuthoringService _authoringService;

        public EditHistoryCommandHandler(ScenarioSpriteSwapAuthoringService authoringService)
        {
            _authoringService = authoringService;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is EditHistoryCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            EditHistoryCommand history = command as EditHistoryCommand;
            if (_authoringService == null || history == null)
                return Result(false, "Edit history is unavailable.");

            string message;
            bool changed = _authoringService.Execute(
                state,
                history.Kind == EditHistoryCommandKind.Undo ? SpriteSwapCommand.Undo() : SpriteSwapCommand.Redo(),
                out message);
            return Result(changed, message);
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }
    }

    internal sealed class TimelineNavigationCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioTimelineBuilder _timelineBuilder;
        private readonly ScenarioTimelineNavigationService _navigationService;

        public TimelineNavigationCommandHandler(
            IScenarioEditorService editorService,
            ScenarioTimelineBuilder timelineBuilder,
            ScenarioTimelineNavigationService navigationService)
        {
            _editorService = editorService;
            _timelineBuilder = timelineBuilder;
            _navigationService = navigationService;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is TimelineNavigationCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            TimelineNavigationCommand timeline = command as TimelineNavigationCommand;
            if (state == null || timeline == null)
                return Result(false, "Timeline navigation is unavailable.");

            if (timeline.Kind == TimelineNavigationCommandKind.SelectDay)
            {
                state.TimelineSelectedDayId = timeline.Value;
                state.TimelineSelectionId = timeline.Value;
                return Result(true, "Timeline day " + timeline.Value + " selected.");
            }

            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            List<ScenarioTimelineEntry> entries = _timelineBuilder.BuildEntries(definition, null);
            ScenarioTimelineEntry entry = null;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                ScenarioTimelineEntry candidate = entries[i];
                TimelineNavigationCommand focus = candidate != null
                    ? candidate.FocusCommand as TimelineNavigationCommand
                    : null;
                bool matches = timeline.Kind == TimelineNavigationCommandKind.OpenEntry
                    ? candidate != null && string.Equals(candidate.Id, timeline.Value, StringComparison.OrdinalIgnoreCase)
                    : focus != null && focus.Kind == timeline.Kind && focus.Index == timeline.Index;
                if (matches)
                {
                    entry = candidate;
                    break;
                }
            }
            if (entry == null)
                return Result(true, "Timeline entry target is missing: " + timeline.AutomationId + ".");

            string message;
            bool changed = _navigationService.Navigate(state, entry, out message);
            return Result(changed, message);
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult
            {
                Handled = true,
                Changed = changed,
                Message = message
            };
        }

    }

    internal sealed class CaptureAuthoringCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioAuthoringCaptureService _captureService;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioSelectionScopeService _scopeService;

        public CaptureAuthoringCommandHandler(
            ScenarioAuthoringCaptureService captureService,
            IScenarioEditorService editorService,
            ScenarioSelectionScopeService scopeService)
        {
            _captureService = captureService;
            _editorService = editorService;
            _scopeService = scopeService;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is CaptureAuthoringCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            CaptureAuthoringCommand capture = command as CaptureAuthoringCommand;
            string message = null;
            bool changed;
            switch (capture.Kind)
            {
                case CaptureAuthoringCommandKind.PreviewFamily:
                    changed = OpenCapturePreview(state, ScenarioAuthoringLocalActionIds.FocusedKindCaptureFamily, out message);
                    break;
                case CaptureAuthoringCommandKind.ConfirmFamily:
                    changed = ConfirmCapture(state, delegate(ScenarioEditorSession session, out string text) { return _captureService.CaptureCurrentFamily(session, out text); }, out message);
                    break;
                case CaptureAuthoringCommandKind.CaptureShelterObjects:
                    if (_scopeService.ResolveActiveScope(state) != ScenarioTargetScope.BunkerInside)
                    {
                        message = "Shelter object capture is available only in the Inside selection scope.";
                        changed = true;
                        break;
                    }
                    changed = Capture(state, delegate(ScenarioEditorSession session, out string text) { return _captureService.CaptureCurrentShelterObjects(session, out text); }, out message);
                    break;
                case CaptureAuthoringCommandKind.CaptureSelectedObject:
                    {
                        if (!_scopeService.CanSelectTargetForCurrentStage(state, state.SelectedTarget, out message))
                        {
                            changed = true;
                            break;
                        }
                        bool captured = _captureService.CaptureSelectedObject(_editorService.CurrentSession, state.SelectedTarget, out message);
                        changed = captured || !string.IsNullOrEmpty(message);
                        break;
                    }
                case CaptureAuthoringCommandKind.RemoveSelectedPlacement:
                    {
                        if (!_scopeService.CanSelectTargetForCurrentStage(state, state.SelectedTarget, out message))
                        {
                            string status = message;
                            changed = FailWithoutChange(state, status, out message);
                            break;
                        }
                        bool removed = _captureService.RemoveSelectedObjectPlacement(_editorService.CurrentSession, state.SelectedTarget, out message);
                        if (!removed)
                        {
                            string status = message;
                            changed = FailWithoutChange(state, status, out message);
                            break;
                        }
                        changed = true;
                        break;
                    }
                default:
                    return Result(false, "Capture command was not recognized.");
            }

            return Result(changed, message);
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }

        private static bool FailWithoutChange(ScenarioAuthoringState state, string statusMessage, out string message)
        {
            if (state != null && !string.IsNullOrEmpty(statusMessage))
                state.StatusMessage = statusMessage;
            message = null;
            return false;
        }

        private bool OpenCapturePreview(ScenarioAuthoringState state, string kind, out string message)
        {
            message = "Review the world capture preview, then confirm or cancel.";
            if (state != null)
            {
                state.FocusedEditorKind = kind;
                state.FocusedEditorIndex = 0;
                state.FocusedEditorIsNew = false;
                state.StatusMessage = message;
            }
            return true;
        }

        private bool ConfirmCapture(ScenarioAuthoringState state, CaptureAction action, out string message)
        {
            bool captured = Capture(state, action, out message);
            if (state != null)
            {
                state.FocusedEditorKind = null;
                state.FocusedEditorIndex = -1;
                state.FocusedEditorIsNew = false;
            }
            return captured;
        }

        private bool Capture(ScenarioAuthoringState state, CaptureAction action, out string message)
        {
            bool captured = action(_editorService.CurrentSession, out message);
            if (state != null)
                state.StatusMessage = message;
            return captured || !string.IsNullOrEmpty(message);
        }

        private delegate bool CaptureAction(ScenarioEditorSession session, out string message);
    }

    internal sealed class StationUpgradeCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioSelectionScopeService _scopeService;
        private readonly ScenarioObjectIdentityAssignmentService _identityAssignmentService;
        private readonly ScenarioAuthoringHistoryService _historyService;
        private readonly ScenarioPreviewSessionHost _previewHost;

        public StationUpgradeCommandHandler(
            IScenarioEditorService editorService,
            ScenarioSelectionScopeService scopeService,
            ScenarioObjectIdentityAssignmentService identityAssignmentService,
            ScenarioAuthoringHistoryService historyService,
            ScenarioPreviewSessionHost previewHost)
        {
            _editorService = editorService;
            _scopeService = scopeService;
            _identityAssignmentService = identityAssignmentService;
            _historyService = historyService;
            _previewHost = previewHost;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is StationUpgradeCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            StationUpgradeCommand station = command as StationUpgradeCommand;
            string message;
            if (!_scopeService.CanSelectTargetForCurrentStage(state, state != null ? state.SelectedTarget : null, out message))
                return Result(true, message);

            Obj_Base obj = ResolveSelectedObject(state);
            if (obj == null || !_previewHost.IsStationObject(obj))
                return Result(true, "Select a station object before editing station upgrades.");

            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            if (session == null || session.WorkingDefinition == null)
                return Result(true, "No active authoring session is available.");

            ScenarioDefinition beforeDefinition = ScenarioEditorDefinitionCloner.Clone(session.WorkingDefinition);
            ObjectPlacement placement = EnsurePlacement(session, obj);
            if (placement == null)
                return Result(true, "Could not create or locate a station placement record.");

            bool changed = false;
            message = null;
            if (station.Kind == StationUpgradeCommandKind.ChangeObjectLevel)
                changed = _previewHost.TryChangeStationObjectLevel(obj, placement, station.LevelDelta, out message);
            else if (station.Kind == StationUpgradeCommandKind.ChangeUpgradeLevel)
                changed = _previewHost.TryChangeStationUpgradeLevel(obj, placement, station.Name, station.LevelDelta, out message);
            else if (station.Kind == StationUpgradeCommandKind.ClearStat)
                changed = _previewHost.TryClearStationStat(obj, placement, station.Name, out message);
            else if (station.Kind == StationUpgradeCommandKind.ChangeStat)
                changed = _previewHost.TryChangeStationStat(obj, placement, station.Name, station.StatDelta, out message);

            if (changed)
            {
                if (_historyService != null)
                    _historyService.RecordBunkerChange(beforeDefinition, message ?? "Change station upgrade");
                ScenarioBunkerDraftService.MarkBunkerDirty(session);
            }
            return Result(true, message);
        }

        private static ScenarioCommandDispatchResult Result(bool result, string message)
        {
            return new ScenarioCommandDispatchResult { Handled = true, Changed = result, Message = message };
        }

        private ObjectPlacement EnsurePlacement(ScenarioEditorSession session, Obj_Base obj)
        {
            BunkerEditsDefinition edits = ScenarioBunkerDraftService.EnsureBunkerEdits(session);
            int index = ScenarioBunkerDraftService.FindPlacementIndex(edits.ObjectPlacements, obj);
            if (index >= 0)
                return edits.ObjectPlacements[index];

            ObjectPlacement placement = ScenarioBunkerDraftService.CreatePlacement(obj, _previewHost);
            edits.ObjectPlacements.Add(placement);
            if (_identityAssignmentService != null)
                _identityAssignmentService.AssignMissingIds(session);
            return placement;
        }

        private static Obj_Base ResolveSelectedObject(ScenarioAuthoringState state)
        {
            if (state == null || state.SelectedTarget == null)
                return null;

            GameObject gameObject = state.SelectedTarget.RuntimeObject as GameObject;
            if (gameObject == null)
            {
                Component component = state.SelectedTarget.RuntimeObject as Component;
                gameObject = component != null ? component.gameObject : null;
            }

            return gameObject != null ? gameObject.GetComponent<Obj_Base>() : null;
        }

    }

    internal sealed class EventAuthoringCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioEventAuthoringService _service;
        private readonly IScenarioEditorService _editorService;

        public EventAuthoringCommandHandler(
            ScenarioEventAuthoringService service,
            IScenarioEditorService editorService)
        {
            _service = service;
            _editorService = editorService;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is EventAuthoringCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            EventAuthoringCommand eventCommand = command as EventAuthoringCommand;
            if (eventCommand == null)
                return new ScenarioCommandDispatchResult();

            string message;
            bool changed;
            if (eventCommand.Operation == EventAuthoringOperation.OpenWorldEventEditor)
            {
                SetFocusedEditor(state, ScenarioAuthoringLocalActionIds.FocusedKindWorldEvent, eventCommand.Index, false);
                message = "Opened world event editor.";
                changed = true;
            }
            else if (eventCommand.Operation == EventAuthoringOperation.OpenWorldEventItemPicker)
            {
                changed = OpenWorldEventItemPicker(state, eventCommand, out message);
            }
            else
            {
                message = null;
                changed = _service != null && _service.Execute(_editorService.CurrentSession, eventCommand, out message);
                if (changed)
                {
                    CloseWorldEventItemPickerAfterSelection(state, eventCommand.Operation);
                    FocusEventEditor(state, _editorService.CurrentSession, eventCommand.Operation);
                }
            }

            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }

        private static void FocusEventEditor(ScenarioAuthoringState state, ScenarioEditorSession session, EventAuthoringOperation operation)
        {
            if (state == null || session == null || session.WorkingDefinition == null)
                return;

            ScenarioDefinition definition = session.WorkingDefinition;
            if (definition.TriggersAndEvents != null
                && (operation == EventAuthoringOperation.AddManualTrigger || operation == EventAuthoringOperation.AddScheduledTrigger))
                SetFocusedEditor(state, "trigger", definition.TriggersAndEvents.Triggers.Count - 1, true);
            else if (operation == EventAuthoringOperation.AddGate)
                SetFocusedEditor(state, "gate", definition.Gates != null ? definition.Gates.Count - 1 : -1, true);
            else if (operation == EventAuthoringOperation.AddScheduledAction)
                SetFocusedEditor(state, "scheduled_action", definition.ScheduledActions != null ? definition.ScheduledActions.Count - 1 : -1, true);
            else if (operation == EventAuthoringOperation.AddWorldEvent)
                SetFocusedEditor(state, ScenarioAuthoringLocalActionIds.FocusedKindWorldEvent, definition.ScheduledActions != null ? definition.ScheduledActions.Count - 1 : -1, true);
            else if (operation == EventAuthoringOperation.ApplyTimelinePreset)
                SetFocusedEditor(state, "scheduled_action", definition.ScheduledActions != null ? definition.ScheduledActions.Count - 1 : -1, true);
            else if (operation == EventAuthoringOperation.AddJournalEntry)
                SetFocusedEditor(state, "journal_entry", definition.Journal != null && definition.Journal.Entries != null ? definition.Journal.Entries.Count - 1 : -1, true);
        }

        private static bool OpenWorldEventItemPicker(ScenarioAuthoringState state, EventAuthoringCommand command, out string message)
        {
            message = null;
            if (state == null || command == null)
                return false;
            int actionIndex = command.Index;
            int itemIndex = command.ChildIndex;
            string listKey = command.Category;
            if (!string.Equals(listKey, "trade", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(listKey, "weapon", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(listKey, "armor", StringComparison.OrdinalIgnoreCase))
            {
                message = "World event item picker list is invalid.";
                return true;
            }

            state.FocusedEditorKind = ScenarioAuthoringLocalActionIds.FocusedKindWorldEventItemPickerPrefix + listKey + ":" + itemIndex.ToString(CultureInfo.InvariantCulture);
            state.FocusedEditorIndex = actionIndex;
            state.FocusedEditorIsNew = false;
            state.TimelineSelectedEntryId = ScenarioAuthoringLocalActionIds.FocusedKindWorldEvent + ":" + actionIndex.ToString(CultureInfo.InvariantCulture);
            message = "Opened world event item picker.";
            return true;
        }

        private static void CloseWorldEventItemPickerAfterSelection(ScenarioAuthoringState state, EventAuthoringOperation operation)
        {
            if (state == null || string.IsNullOrEmpty(state.FocusedEditorKind))
                return;
            if (!state.FocusedEditorKind.StartsWith(ScenarioAuthoringLocalActionIds.FocusedKindWorldEventItemPickerPrefix, StringComparison.Ordinal))
                return;
            if (operation != EventAuthoringOperation.SetWorldEventTradeItem
                && operation != EventAuthoringOperation.SetWorldEventWeapon
                && operation != EventAuthoringOperation.SetWorldEventArmor)
                return;

            int actionIndex = state.FocusedEditorIndex;
            state.FocusedEditorKind = ScenarioAuthoringLocalActionIds.FocusedKindWorldEvent;
            state.FocusedEditorIndex = actionIndex;
            state.FocusedEditorIsNew = false;
        }

        private static void SetFocusedEditor(ScenarioAuthoringState state, string kind, int index, bool isNew)
        {
            if (index < 0)
                return;
            state.FocusedEditorKind = kind;
            state.FocusedEditorIndex = index;
            state.FocusedEditorIsNew = isNew;
            state.TimelineSelectedEntryId = kind + ":" + index.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class CharacterEditorCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioCharacterEditorAuthoringService _service;
        private readonly IScenarioEditorService _editorService;

        public CharacterEditorCommandHandler(
            ScenarioCharacterEditorAuthoringService service,
            IScenarioEditorService editorService)
        {
            _service = service;
            _editorService = editorService;
        }

        public bool CanHandle(ScenarioAuthoringCommand command) { return command is CharacterEditorCommand; }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            string message = "Character editor service is unavailable.";
            bool changed = _service != null && _service.Execute(_editorService.CurrentSession, state, command as CharacterEditorCommand, out message);
            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }
    }

    internal sealed class EditorLifecycleCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacementService;
        private readonly ScenarioSceneSpritePlacementAuthoringService _sceneSpritePlacementService;
        private readonly ScenarioAuthoringBaseModeReloadService _baseModeReloadService;
        private readonly ScenarioOpeningCutsceneAuthoringService _openingCutsceneService;
        private readonly ScenarioAuthoringPauseService _pauseService;
        private readonly ScenarioAuthoringSessionLifecycleService _sessionLifecycle;
        private readonly IScenarioEditorSessionStore _sessionStore;

        public EditorLifecycleCommandHandler(
            IScenarioEditorService editorService,
            ScenarioBuildPlacementAuthoringService buildPlacementService,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacementService,
            ScenarioAuthoringBaseModeReloadService baseModeReloadService,
            ScenarioOpeningCutsceneAuthoringService openingCutsceneService,
            ScenarioAuthoringPauseService pauseService,
            ScenarioAuthoringSessionLifecycleService sessionLifecycle,
            IScenarioEditorSessionStore sessionStore)
        {
            _editorService = editorService;
            _buildPlacementService = buildPlacementService;
            _sceneSpritePlacementService = sceneSpritePlacementService;
            _baseModeReloadService = baseModeReloadService;
            _openingCutsceneService = openingCutsceneService;
            _pauseService = pauseService;
            _sessionLifecycle = sessionLifecycle;
            _sessionStore = sessionStore;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is EditorLifecycleCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            EditorLifecycleCommand lifecycle = command as EditorLifecycleCommand;
            if (lifecycle == null)
                return new ScenarioCommandDispatchResult();

            string message;
            bool changed;
            switch (lifecycle.Kind)
            {
                case EditorLifecycleCommandKind.SaveDraft:
                    changed = SaveDraft(state, out message);
                    break;
                case EditorLifecycleCommandKind.CopyDraftPath:
                    changed = CopyDraftPath(state, out message);
                    break;
                case EditorLifecycleCommandKind.TogglePlaytest:
                    changed = TogglePlaytest(state, out message);
                    break;
                case EditorLifecycleCommandKind.RestartPlaytest:
                    changed = RestartPlaytest(state, out message);
                    break;
                case EditorLifecycleCommandKind.WatchOpeningCutscene:
                    changed = _openingCutsceneService.TryWatchOpeningCutscene(_editorService.CurrentSession, state, out message);
                    break;
                case EditorLifecycleCommandKind.UseRandomSeed:
                    changed = SetScenarioSeedRandom(out message);
                    break;
                case EditorLifecycleCommandKind.UseFixedSeed:
                    changed = SetScenarioSeedFixed(out message);
                    break;
                case EditorLifecycleCommandKind.RerollSeed:
                    changed = RerollScenarioSeed(out message);
                    break;
                case EditorLifecycleCommandKind.SetSeed:
                    changed = CommitScenarioSeed(lifecycle.TextValue, out message);
                    break;
                case EditorLifecycleCommandKind.OpenPauseMenu:
                    changed = OpenPauseMenu(out message);
                    break;
                case EditorLifecycleCommandKind.ConvertToNormalSave:
                    _editorService.ConvertToNormalSave();
                    message = "Scenario binding converted to a normal save.";
                    changed = true;
                    break;
                case EditorLifecycleCommandKind.OpenAdjacentBaseMode:
                    changed = OpenBaseModeDialog(state, lifecycle.Direction, out message);
                    break;
                case EditorLifecycleCommandKind.SwitchBaseModeAndReload:
                    changed = SaveAndReloadBaseMode(state, lifecycle.BaseMode, lifecycle.FamilyChoice, out message);
                    break;
                case EditorLifecycleCommandKind.SwitchBaseModeWithoutReload:
                    changed = SwitchBaseModeOnly(state, lifecycle.BaseMode, lifecycle.FamilyChoice, out message);
                    break;
                case EditorLifecycleCommandKind.CancelBaseModeSwitch:
                    changed = CloseBaseModeDialog(state, "Base switch canceled.", out message);
                    break;
                case EditorLifecycleCommandKind.CommitDraftTitle:
                    changed = CommitDraftTitle(lifecycle.TextValue, out message);
                    break;
                case EditorLifecycleCommandKind.UpdateMetadata:
                    changed = UpdateMetadata(lifecycle.MetadataField, lifecycle.TextValue, out message);
                    break;
                case EditorLifecycleCommandKind.BumpVersion:
                    changed = BumpVersion(lifecycle.MinorVersionBump, out message);
                    break;
                case EditorLifecycleCommandKind.CloseFocusedEditor:
                    changed = CloseFocusedEditor(state, lifecycle.Cancel, out message);
                    break;
                case EditorLifecycleCommandKind.ExitToMainMenu:
                    changed = _sessionLifecycle.RequestCloseToMainMenu(state, "Closed from authoring shell.", out message);
                    break;
                default:
                    return new ScenarioCommandDispatchResult();
            }

            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }

        private bool CopyDraftPath(ScenarioAuthoringState state, out string message)
        {
            string path = _sessionStore.CurrentFilePath;
            if (string.IsNullOrEmpty(path))
            {
                message = "No draft path is active.";
                return true;
            }

            GUIUtility.systemCopyBuffer = path;
            message = "Draft path copied.";
            return true;
        }

        private bool CommitDraftTitle(string value, out string message)
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            string title = value != null ? value.Trim() : string.Empty;
            if (string.IsNullOrEmpty(title))
            {
                message = "Scenario title is required.";
                return true;
            }

            if (string.Equals(definition.DisplayName, title, StringComparison.Ordinal))
            {
                message = "Scenario title is unchanged.";
                return true;
            }

            definition.DisplayName = title;
            session.MarkDraftChanged(ScenarioDirtySection.Meta);
            message = "Scenario title updated.";
            return true;
        }

        private bool UpdateMetadata(ScenarioMetadataField field, string value, out string message)
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            string trimmed = (value ?? string.Empty).Trim();
            switch (field)
            {
                case ScenarioMetadataField.Description:
                    definition.Description = trimmed;
                    message = "Scenario description updated.";
                    break;
                case ScenarioMetadataField.Goal:
                    definition.Goal = trimmed;
                    message = "Scenario goal updated.";
                    break;
                case ScenarioMetadataField.Author:
                    definition.Author = trimmed;
                    message = "Scenario author updated.";
                    break;
                case ScenarioMetadataField.Version:
                    definition.Version = trimmed;
                    message = "Scenario version updated.";
                    break;
                case ScenarioMetadataField.Credits:
                    definition.Credits = trimmed;
                    message = "Scenario credits updated.";
                    break;
                case ScenarioMetadataField.Tags:
                    ReplaceTags(definition.Tags, trimmed);
                    message = "Scenario tags updated.";
                    break;
                case ScenarioMetadataField.Id:
                    if (string.IsNullOrEmpty(trimmed))
                    {
                        message = "Scenario ID cannot be empty.";
                        return true;
                    }
                    definition.Id = trimmed;
                    message = "Scenario ID updated. Keep this stable after sharing.";
                    break;
                default:
                    message = "Scenario metadata field is unavailable.";
                    return true;
            }

            session.MarkDraftChanged(ScenarioDirtySection.Meta);
            return true;
        }

        private bool BumpVersion(bool minor, out string message)
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            definition.Version = ShelteredScenarioAuthoring.BumpVersion(definition.Version, minor);
            session.MarkDraftChanged(ScenarioDirtySection.Meta);
            message = "Version bumped to " + definition.Version + ".";
            return true;
        }

        private static void ReplaceTags(List<string> tags, string raw)
        {
            if (tags == null)
                return;

            tags.Clear();
            string[] entries = (raw ?? string.Empty).Split(',');
            for (int i = 0; i < entries.Length; i++)
            {
                string tag = entries[i].Trim();
                if (!string.IsNullOrEmpty(tag) && !tags.Contains(tag))
                    tags.Add(tag);
            }
        }

        private bool CloseFocusedEditor(ScenarioAuthoringState state, bool cancel, out string message)
        {
            message = null;
            if (state == null || string.IsNullOrEmpty(state.FocusedEditorKind))
            {
                message = "No focused editor is open.";
                return true;
            }

            string kind = state.FocusedEditorKind;
            int index = state.FocusedEditorIndex;
            if (kind.StartsWith(ScenarioAuthoringLocalActionIds.FocusedKindWorldEventItemPickerPrefix, StringComparison.Ordinal))
            {
                state.FocusedEditorKind = ScenarioAuthoringLocalActionIds.FocusedKindWorldEvent;
                state.FocusedEditorIndex = index;
                state.FocusedEditorIsNew = false;
                state.TimelineSelectedEntryId = ScenarioAuthoringLocalActionIds.FocusedKindWorldEvent + ":" + index.ToString(CultureInfo.InvariantCulture);
                message = "Returned to world event editor.";
                return true;
            }

            bool discard = cancel && state.FocusedEditorIsNew;
            if (discard)
                DiscardFocusedEntry(kind, index);
            state.TimelineSelectedEntryId = discard ? null : kind + ":" + index.ToString(CultureInfo.InvariantCulture);
            state.FocusedEditorKind = null;
            state.FocusedEditorIndex = -1;
            state.FocusedEditorIsNew = false;
            state.SurvivorColorPickerChannel = null;
            state.SurvivorColorPickerRequestId = 0;
            message = discard
                ? "New editor entry discarded."
                : (cancel ? "Editor closed without additional changes." : "Editor changes kept.");
            return true;
        }

        private bool RestartPlaytest(ScenarioAuthoringState state, out string message)
        {
            if (_baseModeReloadService == null)
            {
                message = "Playtest restart service is unavailable.";
                return true;
            }

            return _baseModeReloadService.SaveAndReloadCurrentWorld(_editorService.CurrentSession, out message);
        }

        private bool SetScenarioSeedRandom(out string message)
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            if (!definition.SeedOverride.HasValue)
            {
                message = "Scenario seed is already Random.";
                return true;
            }

            definition.SeedOverride = null;
            session.MarkDraftChanged(ScenarioDirtySection.Meta);
            message = "Scenario seed set to Random.";
            return true;
        }

        private bool SetScenarioSeedFixed(out string message)
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            if (!definition.SeedOverride.HasValue)
            {
                definition.SeedOverride = GenerateScenarioSeed();
                session.MarkDraftChanged(ScenarioDirtySection.Meta);
                message = "Scenario seed set to fixed value " + definition.SeedOverride.Value.ToString(CultureInfo.InvariantCulture) + ".";
                return true;
            }

            message = "Scenario seed is already Fixed.";
            return true;
        }

        private bool RerollScenarioSeed(out string message)
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            definition.SeedOverride = GenerateScenarioSeed();
            session.MarkDraftChanged(ScenarioDirtySection.Meta);
            message = "Scenario fixed seed rerolled to " + definition.SeedOverride.Value.ToString(CultureInfo.InvariantCulture) + ".";
            return true;
        }

        private bool CommitScenarioSeed(string value, out string message)
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            int seed;
            if (!int.TryParse(value != null ? value.Trim() : string.Empty, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
            {
                message = "Fixed scenario seed must be a signed 32-bit integer.";
                return true;
            }

            definition.SeedOverride = seed;
            session.MarkDraftChanged(ScenarioDirtySection.Meta);
            message = "Scenario fixed seed updated to " + seed.ToString(CultureInfo.InvariantCulture) + ".";
            return true;
        }

        private static int GenerateScenarioSeed()
        {
            int seed = ModRandom.Range(1, int.MaxValue);
            return seed == 0 ? 1 : seed;
        }

        private bool OpenBaseModeDialog(ScenarioAuthoringState state, int direction, out string message)
        {
            message = null;
            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            ScenarioAuthoringSession loadingSession = _sessionLifecycle != null
                ? _sessionLifecycle.CurrentOrPending
                : null;
            if (state == null || (definition == null && loadingSession == null))
            {
                message = "No active scenario definition is available.";
                return true;
            }

            ScenarioBaseGameMode currentMode = definition != null
                ? definition.BaseGameMode
                : loadingSession.BaseMode;
            string draftId = definition != null ? definition.Id : loadingSession.DraftId;
            if (_baseModeReloadService != null)
                currentMode = _baseModeReloadService.ResolveModeSelectionBase(draftId, currentMode);

            ScenarioBaseGameMode nextMode = ResolveAdjacentBaseMode(currentMode, direction);
            if (nextMode == currentMode)
            {
                message = "Base mode is already " + ScenarioAuthoringBaseModeReloadService.FormatBaseMode(nextMode) + ".";
                return true;
            }

            state.FocusedEditorKind = ScenarioBaseModeAuthoringActions.FocusedEditorKind;
            state.FocusedEditorIndex = (int)nextMode;
            state.FocusedEditorIsNew = false;
            state.TimelineSelectedEntryId = null;
            message = "Choose how to switch base to " + ScenarioAuthoringBaseModeReloadService.FormatBaseMode(nextMode) + ".";
            return true;
        }

        private bool SaveAndReloadBaseMode(
            ScenarioAuthoringState state,
            ScenarioBaseGameMode baseMode,
            string familyChoice,
            out string message)
        {
            CloseBaseModeDialogState(state);
            if (_baseModeReloadService == null)
            {
                message = "Base reload service is unavailable.";
                return true;
            }

            return _baseModeReloadService.SaveAndReload(_editorService.CurrentSession, baseMode, familyChoice, out message);
        }

        private bool SwitchBaseModeOnly(
            ScenarioAuthoringState state,
            ScenarioBaseGameMode baseMode,
            string familyChoice,
            out string message)
        {
            CloseBaseModeDialogState(state);
            if (_baseModeReloadService == null)
            {
                message = "Base reload service is unavailable.";
                return true;
            }

            return _baseModeReloadService.SaveBaseModeOnly(_editorService.CurrentSession, baseMode, familyChoice, out message);
        }

        private static bool CloseBaseModeDialog(ScenarioAuthoringState state, string closeMessage, out string message)
        {
            CloseBaseModeDialogState(state);
            message = closeMessage;
            return true;
        }

        private static void CloseBaseModeDialogState(ScenarioAuthoringState state)
        {
            if (state == null)
                return;

            if (string.Equals(state.FocusedEditorKind, ScenarioBaseModeAuthoringActions.FocusedEditorKind, StringComparison.OrdinalIgnoreCase))
            {
                state.FocusedEditorKind = null;
                state.FocusedEditorIndex = -1;
                state.FocusedEditorIsNew = false;
                state.TimelineSelectedEntryId = null;
            }
        }

        private void DiscardFocusedEntry(string kind, int index)
        {
            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null || index < 0)
                return;

            if (string.Equals(kind, "weather", StringComparison.OrdinalIgnoreCase)
                && definition.TriggersAndEvents != null
                && index < definition.TriggersAndEvents.WeatherEvents.Count)
            {
                definition.TriggersAndEvents.WeatherEvents.RemoveAt(index);
                MarkDirty(session, ScenarioDirtySection.Triggers);
            }
            else if (string.Equals(kind, "trigger", StringComparison.OrdinalIgnoreCase)
                && definition.TriggersAndEvents != null
                && index < definition.TriggersAndEvents.Triggers.Count)
            {
                definition.TriggersAndEvents.Triggers.RemoveAt(index);
                MarkDirty(session, ScenarioDirtySection.Triggers);
            }
            else if (string.Equals(kind, "gate", StringComparison.OrdinalIgnoreCase)
                && definition.Gates != null
                && index < definition.Gates.Count)
            {
                string id = definition.Gates[index] != null ? definition.Gates[index].Id : null;
                definition.Gates.RemoveAt(index);
                ClearGateReferences(definition, id);
                MarkDirty(session, ScenarioDirtySection.Triggers);
            }
            else if ((string.Equals(kind, "scheduled_action", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(kind, ScenarioAuthoringLocalActionIds.FocusedKindWorldEvent, StringComparison.OrdinalIgnoreCase))
                && definition.ScheduledActions != null
                && index < definition.ScheduledActions.Count)
            {
                definition.ScheduledActions.RemoveAt(index);
                MarkDirty(session, ScenarioDirtySection.Triggers);
            }
            else if (string.Equals(kind, "journal_entry", StringComparison.OrdinalIgnoreCase)
                && definition.Journal != null
                && definition.Journal.Entries != null
                && index < definition.Journal.Entries.Count)
            {
                definition.Journal.Entries.RemoveAt(index);
                MarkDirty(session, ScenarioDirtySection.Triggers);
            }
        }

        private static void ClearGateReferences(ScenarioDefinition definition, string gateId)
        {
            if (definition == null || string.IsNullOrEmpty(gateId))
                return;

            for (int i = 0; definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                if (action != null && string.Equals(action.GateId, gateId, StringComparison.OrdinalIgnoreCase))
                    action.GateId = null;
            }
            for (int i = 0; definition.Journal != null && definition.Journal.Entries != null && i < definition.Journal.Entries.Count; i++)
            {
                JournalEntryDefinition entry = definition.Journal.Entries[i];
                if (entry != null && string.Equals(entry.GateId, gateId, StringComparison.OrdinalIgnoreCase))
                    entry.GateId = null;
            }
        }

        private bool OpenPauseMenu(out string message)
        {
            bool opened = _pauseService != null && _pauseService.OpenPauseMenu("Scenario authoring pause menu button.");
            message = opened ? "Pause menu opened." : "Pause menu could not be opened.";
            return true;
        }

        private static ScenarioBaseGameMode ResolveAdjacentBaseMode(ScenarioBaseGameMode mode, int direction)
        {
            int count = Enum.GetValues(typeof(ScenarioBaseGameMode)).Length;
            int next = ((int)mode + direction) % count;
            if (next < 0)
                next += count;

            return (ScenarioBaseGameMode)next;
        }

        private static void MarkDirty(ScenarioEditorSession session, ScenarioDirtySection section)
        {
            if (session == null)
                return;

            session.MarkDraftChanged(section);
        }

        private bool SaveDraft(ScenarioAuthoringState state, out string message)
        {
            try
            {
                ScenarioValidationResult validation = _editorService.CommitChanges(null);
                ScenarioEditorSession session = _editorService.CurrentSession;
                string savedMessage = session != null && session.HasUnappliedDraftChanges
                    ? "Scenario draft saved. Running playtest predates recent edits; stop and restart playtest to verify the saved draft."
                    : "Scenario draft saved.";
                message = validation != null && !validation.IsValid
                    ? savedMessage + " Validation has errors: " + FormatValidationSummary(validation)
                    : savedMessage;
                return true;
            }
            catch (Exception ex)
            {
                message = "Scenario draft save failed: " + ex.Message;
                MMLog.WriteWarning("[ScenarioAuthoringBackend] Save failed: " + ex.Message);
                return true;
            }
        }

        private bool TogglePlaytest(ScenarioAuthoringState state, out string message)
        {
            try
            {
                ScenarioEditorSession editorSession = _editorService.CurrentSession;
                if (editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting)
                {
                    _editorService.EndPlaytest();
                    message = "Playtest ended. Authoring pause restored.";
                    return true;
                }

                string placementMessage = null;
                if (_buildPlacementService != null && _buildPlacementService.HasActivePlacement)
                    _buildPlacementService.CancelForPlaytest(out placementMessage);

                if (_sceneSpritePlacementService != null && _sceneSpritePlacementService.HasActivePlacement)
                {
                    _sceneSpritePlacementService.Reset();
                    if (string.IsNullOrEmpty(placementMessage))
                        placementMessage = "Placement cancelled before playtest started.";
                }

                ScenarioEditorPlaytestResult result = _editorService.BeginPlaytest();
                string playtestMessage = BuildPlaytestStatus(result);
                message = !string.IsNullOrEmpty(placementMessage)
                    ? placementMessage + " " + playtestMessage
                    : playtestMessage;
                return true;
            }
            catch (Exception ex)
            {
                message = "Playtest toggle failed: " + ex.Message;
                MMLog.WriteWarning("[ScenarioAuthoringBackend] Playtest toggle failed: " + ex.Message);
                return true;
            }
        }

        private static string BuildPlaytestStatus(ScenarioEditorPlaytestResult result)
        {
            if (result == null || result.Messages == null || result.Messages.Length == 0)
                return "Playtest started.";

            return string.Join(" ", result.Messages);
        }

        private static string FormatValidationSummary(ScenarioValidationResult validation)
        {
            if (validation == null)
                return "Unknown validation error.";

            ScenarioValidationIssue[] issues = validation.Issues;
            if (issues == null || issues.Length == 0)
                return "Unknown validation error.";

            List<string> messages = new List<string>();
            for (int i = 0; i < issues.Length && messages.Count < 2; i++)
            {
                ScenarioValidationIssue issue = issues[i];
                if (issue != null && !string.IsNullOrEmpty(issue.Message))
                    messages.Add(issue.Message);
            }

            return messages.Count > 0 ? string.Join(" | ", messages.ToArray()) : "Unknown validation error.";
        }
    }

    internal sealed class SelectionCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioWeatherEffectSpriteCatalogService _weatherEffectSpriteCatalog;
        private readonly ScenarioAuthoringSelectionService _selectionService;

        public SelectionCommandHandler(
            ScenarioWeatherEffectSpriteCatalogService weatherEffectSpriteCatalog,
            ScenarioAuthoringSelectionService selectionService)
        {
            _weatherEffectSpriteCatalog = weatherEffectSpriteCatalog;
            _selectionService = selectionService;
        }

        public bool CanHandle(ScenarioAuthoringCommand command) { return command is SelectionCommand; }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            string message;
            bool changed = Execute(state, command as SelectionCommand, out message);
            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }

        private bool Execute(ScenarioAuthoringState state, SelectionCommand selection, out string message)
        {
            message = null;
            if (state == null || selection == null)
                return false;

            if (selection.Kind == SelectionCommandKind.Clear)
            {
                if (state.SelectedTarget == null && (state.MultiSelection == null || state.MultiSelection.Count == 0))
                {
                    message = "Selection is already clear.";
                    return true;
                }

                state.SelectedTarget = null;
                state.MultiSelection.Clear();
                if (state.SelectionStack != null)
                    state.SelectionStack.Clear();
                state.SelectionStackSignature = null;
                state.ActiveSelectionStackIndex = 0;
                state.SelectionStackExpanded = false;
                message = "Selection cleared.";
                return true;
            }

            if (selection.Kind == SelectionCommandKind.CycleStack)
                return CycleSelectionStack(state, out message);

            if (selection.Kind == SelectionCommandKind.ToggleStack)
            {
                state.SelectionStackExpanded = !state.SelectionStackExpanded;
                message = state.SelectionStackExpanded ? "Selection stack expanded." : "Selection stack collapsed.";
                return true;
            }

            if (selection.Kind == SelectionCommandKind.SelectStackIndex)
                return SelectStackIndex(state, selection.Index, out message);

            if (selection.Kind == SelectionCommandKind.SelectWeatherEffect)
                return SelectWeatherEffectTarget(state, selection.TargetId, out message);

            if (selection.Kind == SelectionCommandKind.SelectBackdrop)
                return SelectResolvedTarget(state, selection.TargetId, "Backdrop Layers", "Backdrop layer", out message);

            if (selection.Kind == SelectionCommandKind.SelectHierarchy)
                return SelectResolvedTarget(state, selection.TargetId, "hierarchy", "Hierarchy target", out message);

            return false;
        }

        private bool SelectResolvedTarget(
            ScenarioAuthoringState state,
            string targetId,
            string sourceLabel,
            string missingLabel,
            out string message)
        {
            message = null;
            ScenarioAuthoringTarget target;
            if (_selectionService == null
                || string.IsNullOrEmpty(targetId)
                || !_selectionService.TryResolveTarget(state, targetId, out target)
                || target == null)
            {
                message = missingLabel + " is not live in the current scene: " + (targetId ?? "<missing>") + ".";
                return true;
            }

            if (!_selectionService.TryApplyDirectSelection(state, target, out message))
                return true;

            message = "Selected " + target.DisplayName + " from " + sourceLabel + ".";
            return true;
        }

        private static bool CycleSelectionStack(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (state == null || state.SelectionStack == null || state.SelectionStack.Count == 0)
            {
                message = "No selection stack candidates are available.";
                return true;
            }

            int count = state.SelectionStack.Count;
            int next = (state.ActiveSelectionStackIndex + 1) % count;
            return SelectStackIndex(state, next, out message);
        }

        private static bool SelectStackIndex(ScenarioAuthoringState state, int index, out string message)
        {
            message = null;
            if (state == null || state.SelectionStack == null || state.SelectionStack.Count == 0)
            {
                message = "No selection stack candidates are available.";
                return true;
            }

            if (index < 0 || index >= state.SelectionStack.Count)
            {
                message = "Selection stack row is out of range.";
                return true;
            }

            state.ActiveSelectionStackIndex = index;
            ScenarioAuthoringTarget target = state.SelectionStack[index];
            if (target == null)
            {
                message = "Selection stack target is missing.";
                return true;
            }

            state.SelectedTarget = target.Copy();
            state.HoveredTarget = target.Copy();
            state.MultiSelection.Clear();
            state.MultiSelection.Add(target.Copy());
            message = "Selected " + target.DisplayName + " from the stack.";
            return true;
        }

        private bool SelectWeatherEffectTarget(ScenarioAuthoringState state, string targetId, out string message)
        {
            message = null;
            if (_weatherEffectSpriteCatalog == null || string.IsNullOrEmpty(targetId))
            {
                message = "Weather/effect sprite target is unavailable.";
                return true;
            }

            ScenarioAuthoringTarget target;
            if (!_weatherEffectSpriteCatalog.TryFindTarget(targetId, out target) || target == null)
            {
                message = "Weather/effect sprite target is not loaded: " + targetId + ".";
                return true;
            }

            if (_selectionService == null || !_selectionService.TryApplyDirectSelection(state, target, out message))
                return true;
            message = "Selected " + target.DisplayName + " from Weather & Effects.";
            return true;
        }

    }

    internal sealed class ToolCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacement;

        public ToolCommandHandler(
            ScenarioAuthoringLayoutService layoutService,
            ScenarioBuildPlacementAuthoringService buildPlacement)
        {
            _layoutService = layoutService;
            _buildPlacement = buildPlacement;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is ToolCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            ToolCommand toolCommand = command as ToolCommand;
            if (toolCommand == null)
                return new ScenarioCommandDispatchResult();

            string message;
            bool changed;
            if (toolCommand.Tool == ScenarioAuthoringTool.Select)
            {
                changed = FocusSelection(state, out message);
            }
            else if (toolCommand.Tool == ScenarioAuthoringTool.Assets)
            {
                bool modeChanged = state != null && state.AssetMode != ScenarioAssetAuthoringMode.PlaceNew;
                if (state != null)
                    state.AssetMode = ScenarioAssetAuthoringMode.PlaceNew;
                bool toolChanged = SetTool(state, toolCommand.Tool, out message);
                if (!toolChanged && modeChanged)
                    message = BuildToolStatus(state, toolCommand.Tool, true);
                changed = toolChanged || modeChanged;
            }
            else
            {
                changed = SetTool(state, toolCommand.Tool, out message);
            }

            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }

        private bool SetTool(ScenarioAuthoringState state, ScenarioAuthoringTool tool, out string message)
        {
            message = null;
            if (state == null)
                return false;

            ScenarioAuthoringWorkflowTransition transition = _layoutService.SelectTool(state, tool);
            if (!transition.Changed)
                return false;

            message = BuildToolStatus(state, tool, transition.StageChanged);
            string placementMessage;
            if (_buildPlacement != null && _buildPlacement.HasActivePlacement && _buildPlacement.CancelForToolSwitch(out placementMessage))
            {
                message = !string.IsNullOrEmpty(placementMessage)
                    ? message + " " + placementMessage
                    : message;
            }
            return true;
        }

        private bool FocusSelection(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (state == null)
                return false;

            bool changed = false;
            string placementMessage;
            if (_buildPlacement != null && _buildPlacement.HasActivePlacement && _buildPlacement.CancelForToolSwitch(out placementMessage))
            {
                message = placementMessage;
                changed = true;
            }

            if (state.ActiveTool == ScenarioAuthoringTool.Select)
                return changed;

            ScenarioAuthoringWorkflowTransition transition = _layoutService.SelectTool(state, ScenarioAuthoringTool.Select);
            if (transition.Changed)
            {
                message = string.IsNullOrEmpty(message)
                    ? "Selection focused."
                    : message + " Selection focused.";
                changed = true;
            }

            return changed;
        }

        private static string BuildToolStatus(ScenarioAuthoringState state, ScenarioAuthoringTool requestedTool, bool stageChanged)
        {
            string toolLabel = ScenarioAuthoringWorkflowLabels.GetToolLabel(state != null ? state.ActiveTool : requestedTool);
            string stageLabel = ScenarioAuthoringWorkflowLabels.GetStageLabel(state != null ? state.ActiveStage : ScenarioStageKind.None, false);
            string workspace = ScenarioAuthoringWorkflowRules.ShouldShowToolWorkspace(state)
                ? " Tool workspace opened."
                : string.Empty;

            if (stageChanged)
                return toolLabel + " tool active in " + stageLabel + "." + workspace;

            return toolLabel + " tool active." + workspace;
        }
    }
}
