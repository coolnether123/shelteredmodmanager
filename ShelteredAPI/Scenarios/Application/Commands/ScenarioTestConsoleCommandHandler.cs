using System;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;

namespace ShelteredAPI.Scenarios.Application.Commands
{
    internal sealed class ScenarioTestConsoleCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioEditorService _editorService;

        public ScenarioTestConsoleCommandHandler(IScenarioEditorService editorService)
        {
            _editorService = editorService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = actionId == ScenarioAuthoringActionIds.ActionTestConsoleHour
                || actionId == ScenarioAuthoringActionIds.ActionTestConsoleDay
                || actionId == ScenarioAuthoringActionIds.ActionTestConsoleNextEvent
                || (!string.IsNullOrEmpty(actionId) && (actionId.StartsWith(ScenarioAuthoringActionIds.ActionTestConsoleFirePrefix, StringComparison.Ordinal)
                    || actionId.StartsWith(ScenarioAuthoringActionIds.ActionTestConsoleStoryStagePrefix, StringComparison.Ordinal)));
            message = null;
            if (!handled)
                return false;

            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            if (session == null || session.PlaytestState != ScenarioPlaytestState.Playtesting)
            {
                message = "Test Console controls are available only during an active playtest.";
                return false;
            }

            ScenarioTestConsoleService console;
            try { console = ScenarioCompositionRoot.Resolve<ScenarioTestConsoleService>(); }
            catch (Exception ex) { message = "Test Console runtime is unavailable: " + ex.Message; return false; }
            if (console == null)
            {
                message = "Test Console runtime is unavailable.";
                return false;
            }

            if (actionId == ScenarioAuthoringActionIds.ActionTestConsoleHour)
                return console.TryAdvanceOneHour(out message);
            if (actionId == ScenarioAuthoringActionIds.ActionTestConsoleDay)
                return console.TryAdvanceOneDay(out message);
            if (actionId == ScenarioAuthoringActionIds.ActionTestConsoleNextEvent)
                return console.TryRunUntilNextAuthoredEvent(out message);

            string target;
            if (ScenarioAuthoringActionCodec.TryDecodeTokenActionId(actionId, ScenarioAuthoringActionIds.ActionTestConsoleFirePrefix, out target))
                return console.TryFireNow(session.WorkingDefinition, target, out message);
            if (ScenarioAuthoringActionCodec.TryDecodeTokenActionId(actionId, ScenarioAuthoringActionIds.ActionTestConsoleStoryStagePrefix, out target))
                return console.TryJumpToStoryStage(session.WorkingDefinition, target, out message);

            message = "Test Console action target is invalid.";
            return false;
        }
    }
}
