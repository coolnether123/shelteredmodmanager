using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Core;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class ScenarioWinLossOutcomeService : IScenarioWinLossOutcomeService
    {
        private readonly IScenarioQuestInstanceResolver _questInstanceResolver;
        private readonly ScenarioConditionEvaluatorRegistry _conditionEvaluator;
        private readonly IVanillaScenarioRuntime _vanillaRuntime;
        private readonly ScenarioRuntimeExecutionLog _executionLog;
        private readonly IScenarioEndGamePresenter _endGamePresenter;
        private ScenarioDefinition _definition;
        private ScenarioRuntimeBinding _binding;
        private string _lastBlockedReason;
        private bool _presentationPending;
        private ScenarioEndGamePresentation _pendingPresentation;
        private string _lastPresentationFailure;
        private bool _outcomeArmed;

        public bool IsOutcomeArmed { get { return _outcomeArmed; } }
        public bool IsPresentationPending { get { return _presentationPending; } }

        public ScenarioWinLossOutcomeService(
            IScenarioQuestInstanceResolver questInstanceResolver,
            ScenarioConditionEvaluatorRegistry conditionEvaluator,
            IVanillaScenarioRuntime vanillaRuntime,
            ScenarioRuntimeExecutionLog executionLog,
            IScenarioEndGamePresenter endGamePresenter)
        {
            _questInstanceResolver = questInstanceResolver;
            _conditionEvaluator = conditionEvaluator;
            _vanillaRuntime = vanillaRuntime;
            _executionLog = executionLog;
            _endGamePresenter = endGamePresenter;
        }

        public void Initialize(ScenarioDefinition definition, ScenarioRuntimeBinding binding)
        {
            ResetForNewRun();
            _definition = definition;
            _binding = binding;
        }

        public void ResetForNewRun()
        {
            if (_endGamePresenter != null)
                _endGamePresenter.ResetForNewRun();
            _definition = null;
            _binding = null;
            _lastBlockedReason = null;
            _presentationPending = false;
            _pendingPresentation = null;
            _lastPresentationFailure = null;
            _outcomeArmed = false;
        }

        public void Tick(ScenarioRuntimeState state)
        {
            if (_definition == null || _definition.WinLossConditions == null || _binding == null || state == null)
                return;

            if (!string.IsNullOrEmpty(state.ScenarioOutcome))
            {
                _outcomeArmed = true;
                if (_presentationPending)
                    PresentOutcome(_pendingPresentation);
                return;
            }

            ScenarioConditionRef condition;
            string reason;
            if (TryFindSatisfied(_definition.WinLossConditions.LossConditions, state, out condition, out reason))
            {
                ResolveSatisfiedOutcome(state, false, condition);
                return;
            }

            if (TryFindSatisfied(_definition.WinLossConditions.WinConditions, state, out condition, out reason))
                ResolveSatisfiedOutcome(state, true, condition);
            else if (!string.IsNullOrEmpty(reason))
                LogBlocked(reason);
        }

        private void ResolveSatisfiedOutcome(ScenarioRuntimeState state, bool success, ScenarioConditionRef condition)
        {
            QuestInstance instance;
            string reason;
            if (!_questInstanceResolver.TryResolve(_binding, out instance, out reason))
            {
                LogBlocked(reason);
                return;
            }

            Resolve(instance, state, success, condition);
        }

        private bool TryFindSatisfied(List<ScenarioConditionRef> conditions, ScenarioRuntimeState state, out ScenarioConditionRef satisfied, out string reason)
        {
            satisfied = null;
            reason = null;
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                ScenarioConditionRef condition = conditions[i];
                if (condition == null)
                    continue;

                ScenarioConditionRef evaluatedCondition = condition;
                if (condition.Kind == ScenarioConditionKind.SurviveDays)
                {
                    if (condition.Quantity <= 0)
                    {
                        reason = "SurviveDays condition requires a positive day quantity.";
                        continue;
                    }

                    evaluatedCondition = new ScenarioConditionRef
                    {
                        Id = condition.Id,
                        Kind = ScenarioConditionKind.TimeReached,
                        Time = new ScenarioScheduleTime
                        {
                            Day = Math.Max(1, _binding.DayCreated + condition.Quantity - 1),
                            Hour = condition.Time != null ? condition.Time.Hour : 0,
                            Minute = condition.Time != null ? condition.Time.Minute : 0
                        }
                    };
                }

                string evaluatorReason;
                if (_conditionEvaluator.AreConditionsSatisfied(_definition, new ScenarioConditionRef[] { evaluatedCondition }, state, out evaluatorReason))
                {
                    satisfied = condition;
                    return true;
                }

                reason = evaluatorReason;
            }

            return false;
        }

        private void Resolve(QuestInstance instance, ScenarioRuntimeState state, bool success, ScenarioConditionRef condition)
        {
            if (instance == null || state == null)
                return;

            string reason;
            if (!_vanillaRuntime.TryFinishQuest(instance, success, out reason))
            {
                LogBlocked(reason);
                return;
            }

            state.ScenarioOutcome = success ? "Win" : "Loss";
            state.ScenarioOutcomeConditionId = condition != null ? condition.Id : null;
            UpdateScoreSnapshotOutcome(state, success);
            if (_executionLog != null)
            {
                _executionLog.Record(
                    condition != null ? condition.Id : "scenario-outcome",
                    success ? "Victory" : "Defeat",
                    "Scenario outcome",
                    ScenarioRuntimeExecutionLogOutcome.Fired,
                    condition != null ? condition.Kind.ToString() : null,
                    success ? "Vanilla scenario completed successfully." : "Vanilla scenario failed.");
            }
            MMLog.WriteInfo("[ScenarioWinLoss] Resolved scenario QuestInstance " + instance.id.ToString()
                + " as " + state.ScenarioOutcome
                + " via condition '" + (state.ScenarioOutcomeConditionId ?? string.Empty) + "'.");
            if (_binding != null && _binding.IsPreview)
            {
                _presentationPending = false;
                _outcomeArmed = false;
                _pendingPresentation = null;
                MMLog.WriteInfo("[ScenarioWinLoss] Preview outcome retained in runtime state without end-game presentation.");
                return;
            }
            _presentationPending = true;
            _outcomeArmed = true;
            _pendingPresentation = BuildPresentation(_definition, condition, success);
            PresentOutcome(_pendingPresentation);
        }

        private void PresentOutcome(ScenarioEndGamePresentation presentation)
        {
            string reason = null;
            if (_endGamePresenter != null && _endGamePresenter.TryPresent(presentation, out reason))
            {
                _presentationPending = false;
                _lastPresentationFailure = null;
                return;
            }

            reason = reason ?? "No scenario end-game presenter was registered.";
            if (!string.Equals(_lastPresentationFailure, reason, StringComparison.OrdinalIgnoreCase))
            {
                _lastPresentationFailure = reason;
                MMLog.WriteWarning("[ScenarioWinLoss] Outcome resolved; presentation remains pending: " + reason);
            }
        }

        internal static ScenarioEndGamePresentation BuildPresentation(ScenarioDefinition definition, ScenarioConditionRef condition, bool success)
        {
            int day = 0;
            try { day = GameTime.Day; }
            catch { }

            return new ScenarioEndGamePresentation
            {
                Success = success,
                BaseGameMode = definition != null ? definition.BaseGameMode : ScenarioBaseGameMode.Survival,
                ScenarioDisplayName = definition != null && !string.IsNullOrEmpty(definition.DisplayName)
                    ? definition.DisplayName
                    : "Custom Scenario",
                DaysSurvived = Math.Max(0, day),
                FulfilledConditionText = BuildFulfilledConditionText(definition, condition)
            };
        }

        internal static string BuildFulfilledConditionText(ScenarioDefinition definition, ScenarioConditionRef condition)
        {
            if (condition == null)
                return definition != null && !string.IsNullOrEmpty(definition.Goal)
                    ? definition.Goal
                    : "The authored victory condition was fulfilled.";

            if (condition.Kind == ScenarioConditionKind.SurviveDays)
            {
                return "Survived for " + Math.Max(1, condition.Quantity).ToString(CultureInfo.InvariantCulture) + " days.";
            }
            if (condition.Kind == ScenarioConditionKind.TimeReached)
            {
                int day = condition.Time != null ? condition.Time.Day : 1;
                int hour = condition.Time != null ? condition.Time.Hour : 0;
                int minute = condition.Time != null ? condition.Time.Minute : 0;
                return "Reached day " + Math.Max(1, day).ToString(CultureInfo.InvariantCulture)
                    + " at " + hour.ToString("D2", CultureInfo.InvariantCulture)
                    + ":" + minute.ToString("D2", CultureInfo.InvariantCulture) + ".";
            }
            if (condition.Kind == ScenarioConditionKind.ItemQuantityAvailable)
            {
                return "Secured " + Math.Max(1, condition.Quantity).ToString(CultureInfo.InvariantCulture) + " " + SafePresentationValue(condition.TargetId, "required item") + ".";
            }
            if (condition.Kind == ScenarioConditionKind.QuestCompleted)
                return "Completed quest " + SafePresentationValue(condition.TargetId, "objective") + ".";
            if (condition.Kind == ScenarioConditionKind.QuestFailed)
                return "Quest " + SafePresentationValue(condition.TargetId, "objective") + " failed.";
            if (condition.Kind == ScenarioConditionKind.QuestActive)
                return "Quest " + SafePresentationValue(condition.TargetId, "objective") + " became active.";
            if (condition.Kind == ScenarioConditionKind.SurvivorPresent)
                return SafePresentationValue(condition.TargetId, "The required survivor") + " is present.";
            if (condition.Kind == ScenarioConditionKind.BunkerExpansionUnlocked || condition.Kind == ScenarioConditionKind.TechnologyUnlocked)
                return "Unlocked " + SafePresentationValue(condition.TargetId, "the required shelter upgrade") + ".";
            if (condition.Kind == ScenarioConditionKind.ScenarioFlagSet)
                return "Completed objective " + SafePresentationValue(condition.FlagId ?? condition.TargetId, "scenario flag") + ".";
            if (condition.Kind == ScenarioConditionKind.CustomTrigger)
                return "Triggered " + SafePresentationValue(condition.TargetId, "the authored objective") + ".";

            return definition != null && !string.IsNullOrEmpty(definition.Goal)
                ? definition.Goal
                : "Fulfilled victory condition " + SafePresentationValue(condition.Id, condition.Kind.ToString()) + ".";
        }

        private static string SafePresentationValue(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? (fallback ?? "objective") : value.Replace("_", " ");
        }

        private static void UpdateScoreSnapshotOutcome(ScenarioRuntimeState state, bool success)
        {
            if (state == null || state.ScoreSnapshot == null)
                return;

            state.ScoreSnapshot.CompletionState = success ? ScenarioScoreCompletionState.Won : ScenarioScoreCompletionState.Lost;
            state.ScoreSnapshot.Outcome = state.ScenarioOutcome;
            state.ScoreSnapshot.OutcomeConditionId = state.ScenarioOutcomeConditionId;
            try
            {
                state.ScoreSnapshot.Day = GameTime.Day;
                state.ScoreSnapshot.Hour = GameTime.Hour;
                state.ScoreSnapshot.Minute = GameTime.Minute;
            }
            catch
            {
            }
        }

        private void LogBlocked(string reason)
        {
            if (string.IsNullOrEmpty(reason)
                || string.Equals(_lastBlockedReason, reason, StringComparison.OrdinalIgnoreCase))
                return;

            _lastBlockedReason = reason;
            MMLog.WriteInfo("[ScenarioWinLoss] Waiting: " + reason);
        }
    }
}
