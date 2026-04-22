using System;
using ModAPI.Core;
using ModAPI.InputActions;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    public sealed class ScenarioAuthoringBackendService : IScenarioAuthoringBackend
    {
        private readonly object _sync = new object();
        private readonly ScenarioAuthoringSelectionService _selectionService;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringPresentationBuilder _presentationBuilder;
        private readonly ScenarioAuthoringContextMenuService _contextMenuService;
        private readonly ScenarioAuthoringCommandService _commandService;
        private readonly ScenarioAuthoringSettingsService _settingsService;
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private ScenarioAuthoringState _state = new ScenarioAuthoringState();
        private ScenarioAuthoringSession _activeSession;

        public static ScenarioAuthoringBackendService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioAuthoringBackendService>(); }
        }

        public event Action<ScenarioAuthoringState> StateChanged;

        public ScenarioAuthoringState CurrentState
        {
            get
            {
                lock (_sync)
                {
                    return _state.Copy();
                }
            }
        }

        internal ScenarioAuthoringBackendService(
            ScenarioAuthoringSelectionService selectionService,
            IScenarioEditorService editorService,
            ScenarioAuthoringPresentationBuilder presentationBuilder,
            ScenarioAuthoringContextMenuService contextMenuService,
            ScenarioAuthoringCommandService commandService,
            ScenarioAuthoringSettingsService settingsService,
            ScenarioAuthoringLayoutService layoutService)
        {
            _selectionService = selectionService ?? new ScenarioAuthoringSelectionService();
            _editorService = editorService;
            _presentationBuilder = presentationBuilder;
            _contextMenuService = contextMenuService;
            _commandService = commandService;
            _settingsService = settingsService ?? new ScenarioAuthoringSettingsService();
            _layoutService = layoutService;
        }

        internal void SetActiveSession(ScenarioAuthoringSession session)
        {
            if (session == null)
                return;

            lock (_sync)
            {
                _activeSession = session;
                _state = new ScenarioAuthoringState
                {
                    IsActive = true,
                    ShellVisible = true,
                    SelectionModeActive = false,
                    ActiveTool = ScenarioAuthoringTool.Shelter,
                    ActiveShellTab = ScenarioAuthoringShellTab.Build,
                    AssetMode = ScenarioAssetAuthoringMode.ReplaceExisting,
                    ActiveLayoutPreset = "default",
                    InspectorTab = ScenarioAuthoringInspectorTab.Properties,
                    ActiveDraftId = session.DraftId,
                    ActiveScenarioFilePath = session.ScenarioFilePath,
                    StatusMessage = "Scenario authoring shell is active. Use playtest to make live shelter changes, then capture them back into the draft.",
                    Settings = _settingsService.Load()
                };
                _layoutService.InitializeState(_state);
            }

            ScenarioAuthoringSelectionMenuService.Instance.Reset();
            _contextMenuService.Close();
            ScenarioSpriteSwapAuthoringService.Instance.Invalidate();
            ScenarioSceneSpritePlacementAuthoringService.Instance.Invalidate();
            ScenarioAuthoringHistoryService.Instance.BindSession(session.DraftId);
            ScenarioSpriteSwapClipboard.Clear();
            ScenarioHoverVisualService.Instance.ClearSecondary();
            MMLog.WriteInfo("[ScenarioAuthoringBackend] Active session set. DraftId=" + session.DraftId
                + ", ScenarioFile=" + session.ScenarioFilePath + ".");
            RaiseStateChanged();
        }

        internal void ClearActiveSession(string reason)
        {
            lock (_sync)
            {
                _activeSession = null;
                _state = new ScenarioAuthoringState
                {
                    IsActive = false,
                    StatusMessage = reason ?? string.Empty,
                    Settings = _settingsService.Load()
                };
            }

            ScenarioHoverVisualService.Instance.Clear();
            ScenarioAuthoringSelectionMenuService.Instance.Reset();
            _contextMenuService.Close();
            ScenarioSpriteSwapAuthoringService.Instance.Invalidate();
            ScenarioSceneSpritePlacementAuthoringService.Instance.Invalidate();
            ScenarioAuthoringHistoryService.Instance.Reset();
            ScenarioSpriteSwapClipboard.Clear();
            MMLog.WriteInfo("[ScenarioAuthoringBackend] Active session cleared. Reason=" + (reason ?? "unspecified") + ".");
            RaiseStateChanged();
        }

        internal void Update()
        {
            ScenarioAuthoringState snapshot;
            lock (_sync)
            {
                snapshot = _state.Copy();
            }

            if (snapshot == null || !snapshot.IsActive)
                return;

            bool changed = false;
            _contextMenuService.SyncTarget(snapshot.SelectedTarget);

            if (InputActionRegistry.IsDown(ScenarioAuthoringActionIds.ToggleShell))
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionShellToggle);
            if (InputActionRegistry.IsDown(ScenarioAuthoringActionIds.SaveDraft))
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionSave);
            if (InputActionRegistry.IsDown(ScenarioAuthoringActionIds.TogglePlaytest))
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionPlaytest);

            if (ScenarioAuthoringInputActions.IsUndoDown())
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionHistoryUndo);
            if (ScenarioAuthoringInputActions.IsRedoDown())
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionHistoryRedo);
            if (ScenarioAuthoringInputActions.IsCopyDown())
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionSpriteSwapCopy);
            if (ScenarioAuthoringInputActions.IsPasteDown())
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionSpriteSwapPaste);
            if (ScenarioAuthoringInputActions.IsRevertDown())
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionSpriteSwapRevert);

            changed |= _selectionService.Update(snapshot);

            lock (_sync)
            {
                _state = snapshot;
            }

            if (changed)
                RaiseStateChanged();
        }

        public void Refresh()
        {
            RaiseStateChanged();
        }

        public bool ExecuteAction(string actionId)
        {
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionCloseEditor, StringComparison.Ordinal))
            {
                ScenarioAuthoringBootstrapService.Instance.RequestCloseActiveSession("Closed from authoring shell.", true);
                return true;
            }

            ScenarioAuthoringState snapshot;
            lock (_sync)
            {
                snapshot = _state.Copy();
            }

            if (snapshot == null || !snapshot.IsActive)
                return false;

            bool changed = _commandService.Execute(snapshot, actionId);
            lock (_sync)
            {
                _state = snapshot;
            }

            if (changed)
                RaiseStateChanged();
            return changed;
        }

        internal void OpenContextMenu(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            _presentationBuilder.OpenContextMenu(state, target, _contextMenuService);
        }

        public ScenarioAuthoringShellViewModel GetShellViewModel()
        {
            ScenarioAuthoringState state = CurrentState;
            return _presentationBuilder.BuildShellViewModel(
                state,
                _editorService.CurrentSession,
                GetActiveSession(),
                _contextMenuService.Current);
        }

        public ScenarioAuthoringInspectorDocument GetShellDocument()
        {
            return _presentationBuilder.BuildShellDocument(CurrentState, _editorService.CurrentSession, GetActiveSession());
        }

        public ScenarioAuthoringInspectorDocument GetInspectorDocument()
        {
            return _presentationBuilder.BuildInspectorDocument(CurrentState, _editorService.CurrentSession);
        }

        public ScenarioAuthoringInspectorDocument GetHoverDocument()
        {
            return _presentationBuilder.BuildHoverDocument(CurrentState);
        }

        private void RaiseStateChanged()
        {
            Action<ScenarioAuthoringState> handler = StateChanged;
            if (handler == null)
                return;

            try
            {
                handler(CurrentState);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioAuthoringBackend.StateChanged", ex.Message);
            }
        }

        private ScenarioAuthoringSession GetActiveSession()
        {
            lock (_sync)
            {
                return _activeSession;
            }
        }
    }
}
