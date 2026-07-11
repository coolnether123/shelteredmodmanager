using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredAPI.Scenarios.Diagnostics
{
    internal static class ScenarioRuntimeOutcomeVerification
    {
        internal static void Verify(ScenarioValidationResult result)
        {
            VerifyPresenterSelection(result);
            VerifyInstalledPresenterModeBranching(result);
            VerifyVictoryPresentationText(result);
            VerifyRetryableEffectContract(result);
            VerifyAuthoredVisitorPriorityContract(result);
        }

        private static void VerifyPresenterSelection(ScenarioValidationResult result)
        {
            StubSessionContext context = new StubSessionContext();
            StubPlaytestPresenter playtest = new StubPlaytestPresenter();
            StubInstalledPresenter installed = new StubInstalledPresenter();
            ScenarioEndGamePresenter presenter = new ScenarioEndGamePresenter(context, playtest, installed);
            ScenarioEndGamePresentation presentation = new ScenarioEndGamePresentation
            {
                Success = true,
                BaseGameMode = ScenarioBaseGameMode.Survival
            };
            string reason;

            context.HasSession = true;
            Assert(presenter.TryPresent(presentation, out reason) && playtest.Calls == 1 && installed.Calls == 0,
                "Active authoring sessions must select the return-to-editor ending presenter.", result);

            context.HasSession = false;
            presentation.Success = false;
            Assert(presenter.TryPresent(presentation, out reason) && playtest.Calls == 1 && installed.Calls == 1,
                "Installed scenario runs must select the vanilla game-over ending presenter.", result);
        }

        private static void VerifyInstalledPresenterModeBranching(ScenarioValidationResult result)
        {
            Assert(ScenarioInstalledEndGamePresenter.UsesScenarioVictoryPanel(true, ScenarioBaseGameMode.Survival),
                "Survival authored wins must use the ShelteredAPI scenario victory panel.", result);
            Assert(!ScenarioInstalledEndGamePresenter.UsesScenarioVictoryPanel(false, ScenarioBaseGameMode.Survival),
                "Survival authored losses must retain the vanilla loss flow.", result);
            Assert(!ScenarioInstalledEndGamePresenter.UsesScenarioVictoryPanel(true, ScenarioBaseGameMode.Surrounded)
                && !ScenarioInstalledEndGamePresenter.UsesScenarioVictoryPanel(true, ScenarioBaseGameMode.Stasis),
                "Surrounded and Stasis authored wins must retain their native success score panels.", result);
        }

        private static void VerifyVictoryPresentationText(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition { DisplayName = "Ten Day Trial" };
            ConditionDef condition = new ConditionDef { Id = "win_1", Type = "surviveDays" };
            ScenarioPropertyBag.Set(condition.Properties, "days", "10");
            Assert(ScenarioWinLossOutcomeService.BuildFulfilledConditionText(definition, condition) == "Survived for 10 days.",
                "Victory presentation must describe the fulfilled authored condition.", result);
            ScenarioEndGamePresentation presentation = ScenarioWinLossOutcomeService.BuildPresentation(definition, condition, true);
            Assert(presentation.Success && presentation.ScenarioDisplayName == "Ten Day Trial"
                && presentation.BaseGameMode == ScenarioBaseGameMode.Survival,
                "Victory presentation must carry authored identity and base-mode routing facts.", result);
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

        private static void VerifyAuthoredVisitorPriorityContract(ScenarioValidationResult result)
        {
            ScenarioScheduledActionDefinition action = new ScenarioScheduledActionDefinition { Id = "visitor_due" };
            ScenarioEffectDefinition effect = new ScenarioEffectDefinition { Kind = ScenarioEffectKind.WorldEvent };
            ScenarioPropertyBag.Set(effect.Properties, "eventType", "NpcVisit");
            action.Effects.Add(effect);
            Assert(ScenarioScheduleRuntimeCoordinator.ContainsAuthoredVisitorEffect(action),
                "NpcVisit world-event actions must be classified for authored visitor priority.", result);

            ScenarioDefinition definition = new ScenarioDefinition();
            ScenarioWorldEventRuntimeState.Bind(definition);
            ScenarioWorldEventRuntimeState.SetAuthoredVisitorPriority(true);
            Assert(ScenarioWorldEventRuntimeState.SuppressRandomVisitors,
                "Due-and-pending authored visitors must suppress new vanilla random visitors.", result);
            ScenarioWorldEventRuntimeState.Bind(null);
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
            public bool TryPresent(ScenarioEndGamePresentation presentation, out string reason)
            {
                Calls++;
                reason = null;
                return true;
            }
        }

        private sealed class StubInstalledPresenter : IScenarioEndGamePresentationTarget
        {
            public int Calls;
            public bool TryPresent(ScenarioEndGamePresentation presentation, out string reason)
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
