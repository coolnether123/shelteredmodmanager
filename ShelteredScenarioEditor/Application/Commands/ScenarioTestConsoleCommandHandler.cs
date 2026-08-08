using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Runtime;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal static class ScenarioTestConsoleAutomationIds
    {
        public const string AdvanceOneHour = "test_console.advance.hour";
        public const string AdvanceOneDay = "test_console.advance.day";
        public const string RunUntilNextEvent = "test_console.advance.next_event";
        public const string FireNowPrefix = "test_console.fire.";
        public const string JumpToStoryStagePrefix = "test_console.story_stage.";
    }

    internal enum ScenarioTestConsoleCommandKind
    {
        AdvanceOneHour,
        AdvanceOneDay,
        RunUntilNextEvent,
        FireNow,
        JumpToStoryStage
    }

    internal sealed class ScenarioTestConsoleCommand : ScenarioAuthoringCommand
    {
        private static readonly ScenarioAuthoringCommandPolicy WorldPolicy = ScenarioAuthoringCommandPolicy.World;

        private ScenarioTestConsoleCommand(ScenarioTestConsoleCommandKind kind, string targetId, string automationId)
            : base(automationId, WorldPolicy)
        {
            Kind = kind;
            TargetId = targetId;
        }

        public ScenarioTestConsoleCommandKind Kind { get; private set; }
        public string TargetId { get; private set; }

        public static ScenarioTestConsoleCommand AdvanceOneHour()
        {
            return new ScenarioTestConsoleCommand(ScenarioTestConsoleCommandKind.AdvanceOneHour, null, ScenarioTestConsoleAutomationIds.AdvanceOneHour);
        }

        public static ScenarioTestConsoleCommand AdvanceOneDay()
        {
            return new ScenarioTestConsoleCommand(ScenarioTestConsoleCommandKind.AdvanceOneDay, null, ScenarioTestConsoleAutomationIds.AdvanceOneDay);
        }

        public static ScenarioTestConsoleCommand RunUntilNextEvent()
        {
            return new ScenarioTestConsoleCommand(ScenarioTestConsoleCommandKind.RunUntilNextEvent, null, ScenarioTestConsoleAutomationIds.RunUntilNextEvent);
        }

        public static ScenarioTestConsoleCommand FireNow(string targetId)
        {
            return new ScenarioTestConsoleCommand(
                ScenarioTestConsoleCommandKind.FireNow,
                targetId,
                ScenarioTestConsoleAutomationIds.FireNowPrefix + ScenarioAutomationIdCodec.EncodeToken(targetId));
        }

        public static ScenarioTestConsoleCommand JumpToStoryStage(string targetId)
        {
            return new ScenarioTestConsoleCommand(
                ScenarioTestConsoleCommandKind.JumpToStoryStage,
                targetId,
                ScenarioTestConsoleAutomationIds.JumpToStoryStagePrefix + ScenarioAutomationIdCodec.EncodeToken(targetId));
        }
    }

    internal sealed class ScenarioTestConsoleCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioTestConsoleService _console;

        public ScenarioTestConsoleCommandHandler(
            IScenarioEditorService editorService,
            ScenarioTestConsoleService console)
        {
            _editorService = editorService;
            _console = console;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is ScenarioTestConsoleCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            ScenarioTestConsoleCommand testCommand = command as ScenarioTestConsoleCommand;
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            if (session == null || session.PlaytestState != ScenarioPlaytestState.Playtesting)
                return Result(false, "Test Console controls are available only during an active playtest.");
            if (_console == null)
                return Result(false, "Test Console runtime is unavailable.");

            string message;
            bool changed;
            switch (testCommand.Kind)
            {
                case ScenarioTestConsoleCommandKind.AdvanceOneHour:
                    changed = _console.TryAdvanceOneHour(out message);
                    break;
                case ScenarioTestConsoleCommandKind.AdvanceOneDay:
                    changed = _console.TryAdvanceOneDay(out message);
                    break;
                case ScenarioTestConsoleCommandKind.RunUntilNextEvent:
                    changed = _console.TryRunUntilNextAuthoredEvent(out message);
                    break;
                case ScenarioTestConsoleCommandKind.FireNow:
                    changed = _console.TryFireNow(session.WorkingDefinition, testCommand.TargetId, out message);
                    break;
                case ScenarioTestConsoleCommandKind.JumpToStoryStage:
                    changed = _console.TryJumpToStoryStage(session.WorkingDefinition, testCommand.TargetId, out message);
                    break;
                default:
                    changed = false;
                    message = "Test Console command is not available.";
                    break;
            }

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
}
