using System;
using System.Collections.Generic;
using System.Reflection;
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

        private static readonly MethodInfo InteractionManagerShowInteractionMenu =
            typeof(InteractionManager).GetMethod("ShowInteractionMenu", BindingFlags.NonPublic | BindingFlags.Instance);

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
        private Obj_Base _pendingMenuObject;
        private float _pendingMenuOpenUntil;

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

            Obj_Base selected = ResolveInteractableUnderPointer();
            return HasPlayerInteractions(selected);
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

        public bool TryOpenWorldInteractionUnderPointer()
        {
            if (!CanStartWorldInteraction())
                return false;

            Obj_Base selected = ResolveInteractableUnderPointer();
            if (!HasPlayerInteractions(selected))
                return false;

            if (selected is Obj_Radio)
            {
                bool opened = ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionMapAuthoringOpen);
                if (opened)
                    return true;
            }

            BeginWorldObjectSession("Right-click object interaction.");
            _pendingMenuObject = selected;
            _pendingMenuOpenUntil = RealTime.time + 0.75f;
            _jobWatchUntil = RealTime.time + 2f;
            return true;
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

            if (_pendingMenuObject != null)
            {
                if (TryOpenPendingWorldMenu())
                    return false;

                if (RealTime.time < _pendingMenuOpenUntil)
                    return false;

                _pendingMenuObject = null;
                _pendingMenuOpenUntil = 0f;
            }

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
            _pendingMenuObject = null;
            _pendingMenuOpenUntil = 0f;
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

        private bool TryOpenPendingWorldMenu()
        {
            if (_pendingMenuObject == null)
                return false;

            if (UnityEngine.Input.GetMouseButton(1)
                || UnityEngine.Input.GetMouseButtonDown(1)
                || UnityEngine.Input.GetMouseButtonUp(1))
                return false;

            try
            {
                if (InteractionManager.Instance == null || InteractionManagerShowInteractionMenu == null)
                    return false;

                if (InteractionManager.Instance.SelectedObject != _pendingMenuObject)
                    InteractionManager.Instance.SelectObject(_pendingMenuObject);

                InteractionManagerShowInteractionMenu.Invoke(InteractionManager.Instance, null);
                _pendingMenuObject = null;
                _pendingMenuOpenUntil = 0f;
                _jobWatchUntil = RealTime.time + 2f;
                return true;
            }
            catch (Exception ex)
            {
                _pendingMenuObject = null;
                _pendingMenuOpenUntil = 0f;
                MMLog.WarnOnce("ScenarioVanillaInteraction.OpenMenu", ex.Message);
                return false;
            }
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

        private static Obj_Base ResolveInteractableUnderPointer()
        {
            EnsureSelectedFamilyMember();

            Obj_Base hovered = FindInteractableUnderPointer();
            if (HasPlayerInteractions(hovered))
                return SelectInteractionObject(hovered);

            ScenarioAuthoringState state = GetState();
            ScenarioAuthoringTarget authoringTarget = state != null ? state.HoveredTarget : null;
            UnityEngine.Object runtimeObject = authoringTarget != null ? authoringTarget.RuntimeObject : null;
            GameObject authoringObject = runtimeObject as GameObject;
            Component authoringComponent = runtimeObject as Component;
            if (authoringObject == null && authoringComponent != null)
                authoringObject = authoringComponent.gameObject;

            Obj_Base authoringHovered = ResolveObjBase(authoringObject);
            if (HasPlayerInteractions(authoringHovered))
                return SelectInteractionObject(authoringHovered);

            Obj_Base selected = InteractionManager.Instance != null ? InteractionManager.Instance.SelectedObject : null;
            return HasPlayerInteractions(selected) ? selected : null;
        }

        private static Obj_Base SelectInteractionObject(Obj_Base candidate)
        {
            if (candidate == null)
                return null;

            try
            {
                if (InteractionManager.Instance != null && InteractionManager.Instance.SelectedObject != candidate)
                    InteractionManager.Instance.SelectObject(candidate);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioVanillaInteraction.SelectHovered", ex.Message);
            }

            return candidate;
        }

        private static void EnsureSelectedFamilyMember()
        {
            InteractionManager manager = InteractionManager.Instance;
            if (manager == null || manager.GetSelectedFamilyMember() != null)
                return;

            for (int i = 0; i < manager.GetNumFamilyMembers(); i++)
            {
                FamilyMember member = manager.GetFamilyMemberByIndex(i);
                if (member == null || member.isDead)
                    continue;

                manager.SelectFamilyMemberByIndex(i);
                if (manager.GetSelectedFamilyMember() != null)
                    return;
            }
        }

        private static Obj_Base FindInteractableUnderPointer()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Camera[] cameras = Camera.allCameras;
                if (cameras == null || cameras.Length == 0)
                    return null;

                camera = cameras[0];
            }

            Vector3 mouse = UnityEngine.Input.mousePosition;
            mouse.z = Mathf.Abs(camera.transform.position.z);
            Vector3 worldPoint = camera.ScreenToWorldPoint(mouse);

            try
            {
                Collider2D[] hits2D = Physics2D.OverlapPointAll(new Vector2(worldPoint.x, worldPoint.y));
                for (int i = 0; hits2D != null && i < hits2D.Length; i++)
                {
                    Obj_Base candidate = ResolveObjBase(hits2D[i] != null ? hits2D[i].gameObject : null);
                    if (HasPlayerInteractions(candidate))
                        return candidate;
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioVanillaInteraction.HitTest2D", ex.Message);
            }

            try
            {
                Ray ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
                Array.Sort(hits, CompareRaycastHit);
                for (int i = 0; hits != null && i < hits.Length; i++)
                {
                    Collider collider = hits[i].collider;
                    Obj_Base candidate = ResolveObjBase(collider != null ? collider.gameObject : null);
                    if (HasPlayerInteractions(candidate))
                        return candidate;
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioVanillaInteraction.HitTest3D", ex.Message);
            }

            return null;
        }

        private static Obj_Base ResolveObjBase(GameObject gameObject)
        {
            if (gameObject == null)
                return null;

            Obj_Base direct = gameObject.GetComponent<Obj_Base>();
            if (direct != null)
                return direct;

            return gameObject.GetComponentInParent<Obj_Base>();
        }

        private static bool HasPlayerInteractions(Obj_Base obj)
        {
            if (obj == null)
                return false;

            try
            {
                List<string> interactions = obj.GetPlayerInteractions();
                return interactions != null && interactions.Count > 0;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioVanillaInteraction.PlayerInteractions", ex.Message);
                return false;
            }
        }

        private static int CompareRaycastHit(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }
    }
}
