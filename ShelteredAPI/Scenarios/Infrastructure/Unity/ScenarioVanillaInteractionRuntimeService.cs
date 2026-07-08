using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ScenarioVanillaInteractionRuntimeService
    {
        public const string KindWorld = "World";
        public const string KindMap = "Map";
        public const string KindStorage = "Storage";

        // Authoring allows live world/inventory mutations and blocks only flows that leave
        // or replace the editable scenario world.
        private static readonly HashSet<string> BlockedInteractionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "move_on",
            "launch_rocket",
            "open_expedition_panel",
            "evacuate"
        };

        private int _trackedMemberId = -1;
        private int _trackedObjectId = -1;
        private string _trackedInteractionType;
        private float _jobWatchUntil;
        private bool _pauseReleasedForInteraction;

        public bool IsActive()
        {
            ScenarioAuthoringState state = GetState();
            return state != null && state.VanillaInteractionActive;
        }

        public bool CanStartWorldInteraction()
        {
            if (!ScenarioAuthoringRuntimeGuards.IsAuthoringActive()
                || ScenarioAuthoringRuntimeGuards.IsPlaytesting()
                || ScenarioAuthoringRuntimeGuards.IsOpeningCutscenePreviewActive())
                return false;

            ScenarioAuthoringState state = GetState();
            if (state == null || !state.IsActive || state.WorldLoading || state.ReloadPending)
                return false;
            if (state.ActiveTool != ScenarioAuthoringTool.Select)
                return false;
            if (ScenarioBuildPlacementAuthoringService.Instance.HasActivePlacement)
                return false;

            ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
            if (inputCapture != null && inputCapture.ShouldSuppressWorldInputNow())
                return false;
            if (UICamera.hoveredObject != null)
                return false;

            Obj_Base selected = InteractionManager.Instance != null ? InteractionManager.Instance.SelectedObject : null;
            return selected != null && selected.GetPlayerInteractions().Count > 0;
        }

        public bool TryResolveSyntheticLeftInteract(PlatformInput.InputButton button, bool isDown, bool isUp, bool isHeld, out bool result)
        {
            result = false;
            if (!CanStartWorldInteraction())
                return false;

            if ((button == PlatformInput.InputButton.Action || button == PlatformInput.InputButton.GoHere)
                && (UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.GetMouseButtonUp(0) || UnityEngine.Input.GetMouseButton(0)))
            {
                result = false;
                return true;
            }

            if (button == PlatformInput.InputButton.Interact && isUp && UnityEngine.Input.GetMouseButtonUp(0))
            {
                BeginWorldObjectSession("Left-click object interaction.");
                result = true;
                return true;
            }

            return false;
        }

        public void NotifyGameplayButtonResult(PlatformInput.InputButton button, bool isUp, bool result)
        {
            if (!result || !isUp || button != PlatformInput.InputButton.Interact)
                return;
            if (!CanStartWorldInteraction())
                return;

            BeginWorldObjectSession("Right-click object interaction.");
        }

        public void BeginPanelSession(ScenarioAuthoringState state, string kind, string assistNote)
        {
            if (state == null)
                return;

            BeginStateSession(state, kind, assistNote);
            ReleaseAuthoringPause("Vanilla " + FormatKind(kind) + " interaction opened.");
        }

        public void BeginWorldObjectSession(string reason)
        {
            ScenarioAuthoringBackendService.Instance.BeginVanillaInteractionSession(KindWorld, BuildAssistNote(KindWorld));
            _jobWatchUntil = RealTime.time + 0.75f;
            ReleaseAuthoringPause(reason);
        }

        public bool TryBlockInteraction(Obj_Base obj, string interactionType, out string message)
        {
            message = null;
            if (!ScenarioAuthoringRuntimeGuards.IsAuthoringActive() || ScenarioAuthoringRuntimeGuards.IsPlaytesting())
                return false;

            if (string.IsNullOrEmpty(interactionType) || !BlockedInteractionTypes.Contains(interactionType))
                return false;

            message = BuildBlockedInteractionMessage(interactionType);
            SetStatus(message);
            return true;
        }

        public void TrackInteractionResult(FamilyMember member, Obj_Base obj, string interactionType, bool result)
        {
            if (!result || !IsActive())
                return;

            _trackedMemberId = member != null ? member.GetId() : -1;
            _trackedObjectId = obj != null ? obj.objectId : -1;
            _trackedInteractionType = interactionType;
            _jobWatchUntil = RealTime.time + 1.5f;
        }

        public bool Synchronize(ScenarioAuthoringState state)
        {
            if (state == null || !state.VanillaInteractionActive)
                return false;

            state.VanillaInteractionAssistNote = BuildAssistNote(state.VanillaInteractionKind);

            if (HasActiveVanillaPanelOrMenu() || HasTrackedJob())
                return false;

            RestoreEditor(state, false);
            return true;
        }

        public void ReturnToEditor(ScenarioAuthoringState state)
        {
            RestoreEditor(state, true);
        }

        public void CloseVanillaAndReturnToEditor(ScenarioAuthoringState state)
        {
            CloseInteractionPanels();
            RestoreEditor(state, true);
        }

        public string BuildAssistNote(string kind)
        {
            if (string.Equals(kind, KindStorage, StringComparison.OrdinalIgnoreCase))
                return "Changes sync to your scenario. Storage live-truth is captured into the stockpile draft.";
            if (string.Equals(kind, KindMap, StringComparison.OrdinalIgnoreCase))
                return "Changes sync to your scenario. Map picks update authored location data.";
            return "Changes sync to your scenario. Vanilla object actions run against the live authoring world.";
        }

        private void BeginStateSession(ScenarioAuthoringState state, string kind, string assistNote)
        {
            if (!state.VanillaInteractionActive)
                state.VanillaInteractionPreviousShellVisible = state.ShellVisible;

            state.VanillaInteractionActive = true;
            state.VanillaInteractionKind = string.IsNullOrEmpty(kind) ? KindWorld : kind;
            state.VanillaInteractionAssistNote = !string.IsNullOrEmpty(assistNote) ? assistNote : BuildAssistNote(kind);
            state.ShellVisible = false;
            state.StatusMessage = state.VanillaInteractionAssistNote;
        }

        private void RestoreEditor(ScenarioAuthoringState state, bool explicitReturn)
        {
            if (state == null)
                return;

            bool wasStorage = string.Equals(state.VanillaInteractionKind, KindStorage, StringComparison.OrdinalIgnoreCase) || state.StorageAuthoringActive;
            bool wasMap = string.Equals(state.VanillaInteractionKind, KindMap, StringComparison.OrdinalIgnoreCase) || state.MapAuthoringActive;

            state.VanillaInteractionActive = false;
            state.ShellVisible = state.VanillaInteractionPreviousShellVisible || !state.ShellVisible || explicitReturn;
            state.VanillaInteractionPreviousShellVisible = false;
            state.VanillaInteractionKind = null;
            state.VanillaInteractionAssistNote = null;
            _trackedMemberId = -1;
            _trackedObjectId = -1;
            _trackedInteractionType = null;
            _jobWatchUntil = 0f;

            if (wasStorage)
            {
                ScenarioStorageAuthoringRuntimeService.RestoreSuppliesWorkspace(state);
                state.StatusMessage = explicitReturn ? "Returned to the Supplies workspace." : "Shelter storage closed. Supplies workspace active.";
            }
            else if (wasMap)
            {
                state.MapAuthoringActive = false;
                state.MapAuthoringPreviousShellVisible = false;
                state.ActiveStage = ScenarioStageKind.Map;
                state.ActiveShellTab = ScenarioAuthoringShellTab.Map;
                state.StatusMessage = explicitReturn ? "Returned to the Map workspace." : "Map authoring closed. Map workspace active.";
            }
            else
            {
                state.StatusMessage = explicitReturn ? "Editor shell restored." : "Vanilla interaction closed. Editor restored.";
            }

            if (_pauseReleasedForInteraction && ScenarioAuthoringRuntimeGuards.IsAuthoringActive() && !ScenarioAuthoringRuntimeGuards.IsPlaytesting())
            {
                ScenarioAuthoringPauseService.Instance.EnsurePaused("Vanilla authoring interaction finished.");
                _pauseReleasedForInteraction = false;
            }
        }

        private bool HasActiveVanillaPanelOrMenu()
        {
            ScenarioAuthoringVanillaPanelVisibilityService visibility = ScenarioCompositionRoot.Resolve<ScenarioAuthoringVanillaPanelVisibilityService>();
            return visibility != null && visibility.HasBlockingPanelOpen();
        }

        private bool HasTrackedJob()
        {
            if (_trackedMemberId < 0 || string.IsNullOrEmpty(_trackedInteractionType))
                return RealTime.time < _jobWatchUntil;

            FamilyMember member = FamilyManager.Instance != null ? FamilyManager.Instance.GetFamilyMember(_trackedMemberId) : null;
            JobQueue queue = member != null ? member.job_queue : null;
            if (queue == null)
                return false;

            for (int i = 0; i < queue.size; i++)
            {
                Job job = queue.GetAt(i);
                if (job == null)
                    continue;

                bool typeMatches = string.Equals(job.type, _trackedInteractionType, StringComparison.OrdinalIgnoreCase);
                bool objectMatches = _trackedObjectId < 0 || (job.obj != null && job.obj.objectId == _trackedObjectId);
                if (typeMatches && objectMatches && job.state != Job.JobState.Finished)
                    return true;
            }

            return RealTime.time < _jobWatchUntil;
        }

        private void CloseInteractionPanels()
        {
            try
            {
                UIPanelManager manager = UIPanelManager.Instance();
                if (manager == null)
                    return;

                BasePanel top = manager.GetTopPanel();
                if (top != null)
                    manager.PopPanel(top);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioVanillaInteraction] Failed to close top vanilla panel: " + ex.Message);
            }
        }

        private void ReleaseAuthoringPause(string reason)
        {
            if (_pauseReleasedForInteraction)
                return;

            ScenarioAuthoringPauseService.Instance.ReleasePause(reason);
            _pauseReleasedForInteraction = true;
        }

        private void SetStatus(string message)
        {
            ScenarioAuthoringBackendService backend = ScenarioAuthoringBackendService.Instance;
            if (backend != null)
                backend.SetStatusMessage(message);
        }

        private static ScenarioAuthoringState GetState()
        {
            return ScenarioAuthoringBackendService.Instance.CurrentState;
        }

        private static string FormatKind(string kind)
        {
            return string.IsNullOrEmpty(kind) ? "world" : kind.ToLowerInvariant();
        }

        private static string BuildBlockedInteractionMessage(string interactionType)
        {
            if (string.Equals(interactionType, "open_expedition_panel", StringComparison.OrdinalIgnoreCase))
                return "Expedition setup is blocked in authoring; use Playtest for expedition launches.";
            if (string.Equals(interactionType, "move_on", StringComparison.OrdinalIgnoreCase))
                return "Relocation is blocked in authoring because it replaces the shelter world.";
            if (string.Equals(interactionType, "launch_rocket", StringComparison.OrdinalIgnoreCase))
                return "Scenario-ending rocket launch is blocked while authoring.";
            if (string.Equals(interactionType, "evacuate", StringComparison.OrdinalIgnoreCase))
                return "Evacuation is blocked while authoring because it exits the authored shelter flow.";
            return "That vanilla action is blocked while authoring because it would leave the editable scenario world.";
        }
    }
}
