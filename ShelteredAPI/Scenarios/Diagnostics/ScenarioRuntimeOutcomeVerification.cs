using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;

namespace ShelteredAPI.Scenarios.Diagnostics
{
    internal static class ScenarioRuntimeOutcomeVerification
    {
        internal static void Verify(ScenarioValidationResult result)
        {
            VerifyPresenterSelection(result);
            VerifyRetryableEffectContract(result);
        }

        private static void VerifyPresenterSelection(ScenarioValidationResult result)
        {
            StubSessionContext context = new StubSessionContext();
            StubPlaytestPresenter playtest = new StubPlaytestPresenter();
            StubInstalledPresenter installed = new StubInstalledPresenter();
            ScenarioEndGamePresenter presenter = new ScenarioEndGamePresenter(context, playtest, installed);
            string reason;

            context.HasSession = true;
            Assert(presenter.TryPresent(true, out reason) && playtest.Calls == 1 && installed.Calls == 0,
                "Active authoring sessions must select the return-to-editor ending presenter.", result);

            context.HasSession = false;
            Assert(presenter.TryPresent(false, out reason) && playtest.Calls == 1 && installed.Calls == 1,
                "Installed scenario runs must select the vanilla game-over ending presenter.", result);
        }

        private static void VerifyRetryableEffectContract(ScenarioValidationResult result)
        {
            ScenarioEffectDispatcher dispatcher = new ScenarioEffectDispatcher();
            dispatcher.Register(new StubRetryableHandler());
            string message;
            bool retryable;
            bool handled = dispatcher.Dispatch(
                new ScenarioDefinition(),
                new ScenarioEffectDefinition { Kind = ScenarioEffectKind.StartConversation },
                new ScenarioRuntimeState(),
                out message,
                out retryable);

            Assert(!handled && retryable && !ScenarioScheduleRuntimeCoordinator.ShouldJournalEffectFailure(retryable),
                "Participant-resolution failures must remain unjournaled so a once-only conversation retries.", result);
            Assert(ScenarioScheduleRuntimeCoordinator.ShouldJournalEffectFailure(false),
                "Non-retryable effect failures must retain the normal failure journal contract.", result);
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition)
                result.AddError(message);
        }

        private sealed class StubSessionContext : IScenarioAuthoringSessionContext
        {
            public bool HasSession;
            public bool HasActiveSession { get { return HasSession; } }
        }

        private sealed class StubPlaytestPresenter : IScenarioEndGamePresentationTarget
        {
            public int Calls;
            public bool TryPresent(bool success, out string reason)
            {
                Calls++;
                reason = null;
                return true;
            }
        }

        private sealed class StubInstalledPresenter : IScenarioEndGamePresentationTarget
        {
            public int Calls;
            public bool TryPresent(bool success, out string reason)
            {
                Calls++;
                reason = null;
                return true;
            }
        }

        private sealed class StubRetryableHandler : IScenarioRetryableEffectHandler
        {
            public bool CanHandle(ScenarioEffectKind kind)
            {
                return kind == ScenarioEffectKind.StartConversation;
            }

            public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
            {
                bool retryable;
                return Handle(definition, effect, state, out message, out retryable);
            }

            public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message, out bool retryable)
            {
                message = "Starting family is not materialized yet.";
                retryable = true;
                return false;
            }
        }
    }
}
