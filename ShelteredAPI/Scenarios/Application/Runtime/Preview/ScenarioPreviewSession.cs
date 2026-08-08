using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Public;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Application.Runtime
{
    /// <summary>
    /// Process-local preview owner. The session keeps the active definition and every
    /// runtime resource created from it behind one disposable lifetime boundary.
    /// </summary>
    internal sealed class ScenarioPreviewSession : IScenarioPreviewSession
    {
        private readonly string _runId = Guid.NewGuid().ToString("N");
        private IScenarioRuntimeBindingService _bindings;
        private IVanillaScenarioRuntime _vanilla;
        private ScenarioRuntimeDefinitionResolver _definitions;
        private ScenarioRuntimeBinding _binding;
        private ScenarioDefinition _definition;
        private readonly string _scenarioFilePath;
        private int? _questInstanceId;
        private bool _disposed;
        private readonly List<string> _animationPreviewPaths = new List<string>();
        private readonly List<ScenarioSpriteTargetComponentKind> _animationPreviewKinds =
            new List<ScenarioSpriteTargetComponentKind>();

        public ScenarioPreviewSession(ScenarioDefinition definition, string scenarioFilePath)
        {
            _definition = definition;
            _scenarioFilePath = scenarioFilePath;
            StartResult = Start();
        }

        public ScenarioPreviewResult StartResult { get; private set; }

        public ScenarioPreviewResult Refresh(ScenarioDefinition definition, ScenarioPreviewRefreshScope scope)
        {
            if (_disposed)
                return ScenarioPreviewResult.Failed("Preview refresh was rejected because the session is closed.");
            if (definition == null)
                return ScenarioPreviewResult.Failed("Preview refresh requires a scenario definition.");
            if (!StartResult.Started)
                return ScenarioPreviewResult.Failed("Preview refresh was rejected because the session did not start.");

            try
            {
                _definition = definition;
                _definitions.SetPreview(definition, _scenarioFilePath, _runId);
                _binding.ScenarioId = definition.Id;
                _binding.VersionApplied = definition.Version;
                _bindings.SetBinding(_binding);

                if ((scope & ScenarioPreviewRefreshScope.World) != 0)
                {
                    ScenarioApplyResult applied = ScenarioRuntimeCompositionRoot
                        .Resolve<IScenarioApplier>()
                        .ApplyAll(definition, _scenarioFilePath);
                    return ScenarioPreviewResult.FromApplyResult(applied, _bindings.CurrentRevision);
                }

                ScenarioApplyResult partialApply = null;
                if ((scope & ScenarioPreviewRefreshScope.MapProjection) != 0)
                {
                    partialApply = new ScenarioApplyResult();
                    ScenarioRuntimeCompositionRoot.Resolve<ScenarioMapProjectionApplyService>()
                        .Apply(definition, partialApply);
                }

                ScenarioPreviewResult result = partialApply != null
                    ? ScenarioPreviewResult.FromApplyResult(partialApply, _bindings.CurrentRevision)
                    : ScenarioPreviewResult.Succeeded(_bindings.CurrentRevision);
                if ((scope & ScenarioPreviewRefreshScope.SpriteSwaps) != 0)
                {
                    ScenarioRuntimeCompositionRoot.Resolve<IScenarioSpriteSwapEngine>()
                        .Activate(definition, _scenarioFilePath, null);
                }
                if ((scope & ScenarioPreviewRefreshScope.ScenePlacements) != 0)
                {
                    result.ScenePlacementChanges = ScenarioRuntimeCompositionRoot
                        .Resolve<IScenarioSceneSpritePlacementEngine>()
                        .Activate(definition, _scenarioFilePath, null);
                }
                return result;
            }
            catch (Exception ex)
            {
                return ScenarioPreviewResult.Failed("Preview refresh failed: " + ex.Message);
            }
        }

        public bool RestartWorld(ScenarioWorldLaunchRequest request, out string error)
        {
            if (!IsActive)
            {
                error = "Preview world restart was rejected because the session is not active.";
                return false;
            }
            if (request == null)
            {
                error = "Preview world restart request was null.";
                return false;
            }

            return ScenarioWorldLaunchFacade.TryLaunch(request, out error);
        }

        public ScenarioRuntimeSnapshot CaptureRuntimeState()
        {
            return IsActive ? ScenarioPreviewStateMapper.CaptureRuntimeState() : null;
        }

        public void SetExecutionLogging(bool enabled)
        {
            if (IsActive)
                ScenarioRuntimeCompositionRoot.Resolve<ScenarioRuntimeExecutionLog>().Enabled = enabled;
        }

        public ScenarioRuntimeExecutionEntrySnapshot[] CaptureExecutionLog(int maximumEntries)
        {
            return IsActive
                ? ScenarioPreviewStateMapper.CaptureExecutionLog(maximumEntries)
                : new ScenarioRuntimeExecutionEntrySnapshot[0];
        }

        public bool TryFireRuntimeElement(string elementId, out string message)
        {
            if (!IsActive)
            {
                message = "Preview operation was rejected because the session is not active.";
                return false;
            }
            if (string.IsNullOrEmpty(elementId))
            {
                message = "No runtime element was selected.";
                return false;
            }

            try
            {
                for (int i = 0; _definition != null
                    && _definition.Conversations != null
                    && _definition.Conversations.Conversations != null
                    && i < _definition.Conversations.Conversations.Count; i++)
                {
                    ScenarioConversationDefinition conversation = _definition.Conversations.Conversations[i];
                    if (conversation == null
                        || !string.Equals(conversation.Id, elementId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    ScenarioConversationRuntimeService conversationRuntime =
                        ScenarioRuntimeCompositionRoot.Resolve<ScenarioConversationRuntimeService>();
                    ScenarioRuntimeStateService stateService =
                        ScenarioRuntimeCompositionRoot.Resolve<ScenarioRuntimeStateService>();
                    conversationRuntime.Activate(_definition);
                    return conversationRuntime.Handle(
                        _definition,
                        new ScenarioEffectDefinition
                        {
                            Kind = ScenarioEffectKind.StartConversation,
                            ConversationId = conversation.Id,
                            TargetId = conversation.Id
                        },
                        stateService.State,
                        out message);
                }

                for (int i = 0; _definition != null
                    && _definition.TriggersAndEvents != null
                    && _definition.TriggersAndEvents.Triggers != null
                    && i < _definition.TriggersAndEvents.Triggers.Count; i++)
                {
                    TriggerDef trigger = _definition.TriggersAndEvents.Triggers[i];
                    if (trigger != null && string.Equals(trigger.Id, elementId, StringComparison.OrdinalIgnoreCase))
                        return ScenarioTriggerRuntime.Fire(elementId, "runtime-console-manual", out message);
                }

                return ScenarioRuntimeCompositionRoot.Resolve<ScenarioScheduleRuntimeCoordinator>()
                    .TryFireNow(elementId, out message);
            }
            catch (Exception ex)
            {
                message = "Preview runtime element failed: " + ex.Message;
                return false;
            }
        }

        public bool TryGetMinutesUntilNextAuthoredEvent(int maximumMinutes, out int minutes)
        {
            minutes = 0;
            return IsActive
                && ScenarioRuntimeCompositionRoot.Resolve<ScenarioScheduleRuntimeCoordinator>()
                    .TryGetMinutesUntilNextAuthoredEvent(maximumMinutes, out minutes);
        }

        public void NotifyGameTimeChanged()
        {
            if (IsActive)
                ScenarioRuntimeCompositionRoot.Resolve<ScenarioScheduleRuntimeCoordinator>().TickOnGameTimeChanged();
        }

        public bool TryPreviewRuntimeSpriteFrame(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind,
            Sprite sprite)
        {
            bool applied = IsActive
                && ScenarioRuntimeAssetFacade.TryPreviewRuntimeSpriteFrame(targetPath, targetKind, sprite);
            if (applied)
                TrackAnimationPreview(targetPath, targetKind);
            return applied;
        }

        public bool TryPlayRuntimeSpriteAnimation(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind,
            Sprite[] frames,
            float[] durations,
            float speed)
        {
            bool playing = IsActive
                && ScenarioRuntimeAssetFacade.TryPlayRuntimeSpriteAnimation(
                    targetPath, targetKind, frames, durations, speed);
            if (playing)
                TrackAnimationPreview(targetPath, targetKind);
            return playing;
        }

        public void StopRuntimeSpriteAnimation(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind)
        {
            ScenarioRuntimeAssetFacade.StopRuntimeSpriteAnimation(targetPath, targetKind);
            for (int i = _animationPreviewPaths.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_animationPreviewPaths[i], targetPath, StringComparison.Ordinal)
                    && _animationPreviewKinds[i] == targetKind)
                {
                    _animationPreviewPaths.RemoveAt(i);
                    _animationPreviewKinds.RemoveAt(i);
                }
            }
        }

        public void CaptureRuntimeObjectState(Obj_Base source, ObjectPlacement destination)
        {
            if (IsActive)
                ScenarioObjectStatePropertyService.Capture(source, destination);
        }

        public bool IsStationObject(Obj_Base obj)
        {
            return IsActive && ScenarioStationUpgradePropertyService.IsStationObject(obj);
        }

        public void CaptureStationUpgradeState(Obj_Base source, ObjectPlacement destination)
        {
            if (IsActive)
                ScenarioStationUpgradePropertyService.Capture(source, destination);
        }

        public ScenarioStationUpgradeSnapshot GetStationUpgradeSnapshot(Obj_Base obj, ObjectPlacement placement)
        {
            return IsActive ? ScenarioStationUpgradePropertyService.BuildSnapshot(obj, placement) : null;
        }

        public bool TryChangeStationObjectLevel(Obj_Base obj, ObjectPlacement placement, int delta, out string message)
        {
            if (IsActive)
                return ScenarioStationUpgradePropertyService.TrySetObjectLevel(obj, placement, delta, out message);
            message = "No scenario preview session is active.";
            return false;
        }

        public bool TryChangeStationUpgradeLevel(
            Obj_Base obj,
            ObjectPlacement placement,
            string pathName,
            int delta,
            out string message)
        {
            if (IsActive)
                return ScenarioStationUpgradePropertyService.TrySetUpgradeLevel(obj, placement, pathName, delta, out message);
            message = "No scenario preview session is active.";
            return false;
        }

        public bool TryChangeStationStat(
            Obj_Base obj,
            ObjectPlacement placement,
            string statName,
            float delta,
            out string message)
        {
            if (IsActive)
                return ScenarioStationUpgradePropertyService.TrySetStat(obj, placement, statName, delta, out message);
            message = "No scenario preview session is active.";
            return false;
        }

        public bool TryClearStationStat(
            Obj_Base obj,
            ObjectPlacement placement,
            string statName,
            out string message)
        {
            if (IsActive)
                return ScenarioStationUpgradePropertyService.TryClearStat(obj, placement, statName, out message);
            message = "No scenario preview session is active.";
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            ModRandomBridge.SetScenarioFixedSeedActive(false);

            for (int i = 0; i < _animationPreviewPaths.Count; i++)
            {
                ScenarioRuntimeAssetFacade.StopRuntimeSpriteAnimation(
                    _animationPreviewPaths[i], _animationPreviewKinds[i]);
            }
            _animationPreviewPaths.Clear();
            _animationPreviewKinds.Clear();

            try
            {
                ScenarioRuntimeCompositionRoot.Resolve<IScenarioSpriteSwapEngine>()
                    .Clear("Scenario preview session closed.");
                ScenarioRuntimeCompositionRoot.Resolve<IScenarioSceneSpritePlacementEngine>()
                    .Clear("Scenario preview session closed.");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredScenarioRuntime] Preview scene asset cleanup failed: " + ex.Message);
            }

            try
            {
                AbandonQuestCarrier();
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredScenarioRuntime] Preview quest carrier cleanup failed: " + ex.Message);
            }

            try
            {
                if (_definitions != null)
                    _definitions.ClearPreview(_runId);
            }
            catch
            {
            }

            try
            {
                ScenarioRuntimeBinding current = _bindings != null ? _bindings.CurrentBinding : null;
                if (current != null
                    && current.IsPreview
                    && string.Equals(current.RunId, _runId, StringComparison.Ordinal))
                {
                    _bindings.SetBinding(null);
                }
            }
            catch
            {
            }

            _binding = null;
            _definition = null;
            _questInstanceId = null;
        }

        private bool IsActive
        {
            get { return !_disposed && StartResult != null && StartResult.Started; }
        }

        private void TrackAnimationPreview(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind)
        {
            for (int i = 0; i < _animationPreviewPaths.Count; i++)
            {
                if (string.Equals(_animationPreviewPaths[i], targetPath, StringComparison.Ordinal)
                    && _animationPreviewKinds[i] == targetKind)
                    return;
            }
            _animationPreviewPaths.Add(targetPath);
            _animationPreviewKinds.Add(targetKind);
        }

        private ScenarioPreviewResult Start()
        {
            if (_definition == null)
                return ScenarioPreviewResult.Failed("Preview could not start because the definition was null.");

            string blockingReason;
            if (!ScenarioWorldReady.Evaluate(out blockingReason))
                return ScenarioPreviewResult.Failed("World is not ready for scenario preview. " + blockingReason);

            try
            {
                _bindings = ScenarioRuntimeCompositionRoot.Resolve<IScenarioRuntimeBindingService>();
                _vanilla = ScenarioRuntimeCompositionRoot.Resolve<IVanillaScenarioRuntime>();
                _definitions = ScenarioRuntimeCompositionRoot.Resolve<ScenarioRuntimeDefinitionResolver>();
                _binding = new ScenarioRuntimeBinding
                {
                    ScenarioId = _definition.Id,
                    VersionApplied = _definition.Version,
                    IsActive = true,
                    IsConvertedToNormalSave = false,
                    DayCreated = GameTime.Day,
                    RunId = _runId,
                    IsPreview = true
                };
                _definitions.SetPreview(_definition, _scenarioFilePath, _runId);
                _bindings.SetBinding(_binding);

                QuestInstance instance;
                string spawnReason;
                ScenarioDef playable = ScenarioDefinitionService.BuildPlayableScenarioDef(_definition);
                if (!_vanilla.TrySpawnScenario(playable, out instance, out spawnReason) || instance == null)
                {
                    Dispose();
                    return ScenarioPreviewResult.Failed(
                        "Preview completion carrier could not be created. "
                        + (spawnReason ?? "QuestManager returned no scenario instance."));
                }

                _questInstanceId = instance.id;
                _binding.ScenarioQuestInstanceId = instance.id;
                _bindings.SetBinding(_binding);

                string seedMessage;
                ScenarioSeedPolicy.TryApplyForScenario(_definition, "preview", out seedMessage);
                ScenarioApplyResult applied = ScenarioRuntimeCompositionRoot.Resolve<IScenarioApplier>()
                    .ApplyAll(_definition, _scenarioFilePath);
                ScenarioPreviewResult result = ScenarioPreviewResult.FromApplyResult(applied, _bindings.CurrentRevision);
                result.AddMessage(seedMessage);
                return result;
            }
            catch (Exception ex)
            {
                Dispose();
                return ScenarioPreviewResult.Failed("Preview apply failed: " + ex.Message);
            }
        }

        private void AbandonQuestCarrier()
        {
            if (!_questInstanceId.HasValue || _vanilla == null)
                return;

            QuestInstance instance;
            string reason;
            if (_vanilla.TryGetQuestInstance(_questInstanceId.Value, out instance, out reason)
                && instance != null
                && !_vanilla.TryAbandonQuest(instance, out reason))
            {
                MMLog.WriteWarning("[ShelteredScenarioRuntime] Preview quest carrier cleanup failed: " + reason);
            }
        }
    }
}
