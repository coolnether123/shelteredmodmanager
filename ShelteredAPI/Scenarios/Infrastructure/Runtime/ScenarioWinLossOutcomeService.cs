using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Core;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class ScenarioWinLossOutcomeService : IScenarioWinLossOutcomeService
    {
        private readonly IScenarioQuestInstanceResolver _questInstanceResolver;
        private readonly IScenarioWinLossConditionAdapter _conditionAdapter;
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
            IScenarioWinLossConditionAdapter conditionAdapter,
            ScenarioConditionEvaluatorRegistry conditionEvaluator,
            IVanillaScenarioRuntime vanillaRuntime,
            ScenarioRuntimeExecutionLog executionLog,
            IScenarioEndGamePresenter endGamePresenter)
        {
            _questInstanceResolver = questInstanceResolver;
            _conditionAdapter = conditionAdapter;
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

            ConditionDef condition;
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

        private void ResolveSatisfiedOutcome(ScenarioRuntimeState state, bool success, ConditionDef condition)
        {
            QuestInstance instance;
            string reason;
            if (!_questInstanceResolver.TryResolve(_binding, out instance, out reason))
            {
                LogBlocked(reason);
                ReturnAuthoringPlaytestToEditor();
                return;
            }

            Resolve(instance, state, success, condition);
        }

        private bool TryFindSatisfied(List<ConditionDef> conditions, ScenarioRuntimeState state, out ConditionDef satisfied, out string reason)
        {
            satisfied = null;
            reason = null;
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                ConditionDef condition = conditions[i];
                if (condition == null)
                    continue;

                ScenarioConditionRef conditionRef;
                string adapterReason;
                if (!_conditionAdapter.TryCreateConditionRef(_definition, _binding, condition, out conditionRef, out adapterReason))
                {
                    reason = adapterReason;
                    continue;
                }

                string evaluatorReason;
                if (_conditionEvaluator.AreConditionsSatisfied(_definition, new ScenarioConditionRef[] { conditionRef }, state, out evaluatorReason))
                {
                    satisfied = condition;
                    return true;
                }

                reason = evaluatorReason;
            }

            return false;
        }

        private void Resolve(QuestInstance instance, ScenarioRuntimeState state, bool success, ConditionDef condition)
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
                    condition != null ? condition.Type : null,
                    success ? "Vanilla scenario completed successfully." : "Vanilla scenario failed.");
            }
            MMLog.WriteInfo("[ScenarioWinLoss] Resolved scenario QuestInstance " + instance.id.ToString()
                + " as " + state.ScenarioOutcome
                + " via condition '" + (state.ScenarioOutcomeConditionId ?? string.Empty) + "'.");
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

        internal static ScenarioEndGamePresentation BuildPresentation(ScenarioDefinition definition, ConditionDef condition, bool success)
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

        internal static string BuildFulfilledConditionText(ScenarioDefinition definition, ConditionDef condition)
        {
            if (condition == null)
                return definition != null && !string.IsNullOrEmpty(definition.Goal)
                    ? definition.Goal
                    : "The authored victory condition was fulfilled.";

            string type = ScenarioWinLossConditionSupport.Normalize(condition.Type);
            if (type == "survivedays")
            {
                int days = ScenarioPropertyBag.GetInt(condition.Properties, "days", ScenarioPropertyBag.GetInt(condition.Properties, "day", 0));
                return "Survived for " + Math.Max(1, days).ToString(CultureInfo.InvariantCulture) + " days.";
            }
            if (type == "timereached" || type == "dayreached")
            {
                int day = ScenarioPropertyBag.GetInt(condition.Properties, "day", ScenarioPropertyBag.GetInt(condition.Properties, "days", 1));
                int hour = ScenarioPropertyBag.GetInt(condition.Properties, "hour", 0);
                int minute = ScenarioPropertyBag.GetInt(condition.Properties, "minute", 0);
                return "Reached day " + Math.Max(1, day).ToString(CultureInfo.InvariantCulture)
                    + " at " + hour.ToString("D2", CultureInfo.InvariantCulture)
                    + ":" + minute.ToString("D2", CultureInfo.InvariantCulture) + ".";
            }
            if (type == "itemquantityavailable" || type == "itemquantity" || type == "hasitem")
            {
                int quantity = ScenarioPropertyBag.GetInt(condition.Properties, "quantity", 1);
                string item = ScenarioPropertyBag.FirstString(condition.Properties, "itemId", "targetId");
                return "Secured " + Math.Max(1, quantity).ToString(CultureInfo.InvariantCulture) + " " + SafePresentationValue(item, "required item") + ".";
            }
            if (type == "questcompleted")
                return "Completed quest " + SafePresentationValue(ScenarioPropertyBag.FirstString(condition.Properties, "questId", "targetId"), "objective") + ".";
            if (type == "survivorpresent")
                return SafePresentationValue(ScenarioPropertyBag.FirstString(condition.Properties, "survivorId", "name", "targetId"), "The required survivor") + " is present.";
            if (type == "bunkerexpansionunlocked" || type == "technologyunlocked")
                return "Unlocked " + SafePresentationValue(ScenarioPropertyBag.FirstString(condition.Properties, "bunkerExpansionId", "technologyId", "targetId"), "the required shelter upgrade") + ".";
            if (type == "scenarioflagset" || type == "flagset")
                return "Completed objective " + SafePresentationValue(ScenarioPropertyBag.FirstString(condition.Properties, "flagId", "targetId"), "scenario flag") + ".";
            if (type == "customtrigger" || type == "trigger")
                return "Triggered " + SafePresentationValue(ScenarioPropertyBag.FirstString(condition.Properties, "triggerId", "targetId"), "the authored objective") + ".";

            return definition != null && !string.IsNullOrEmpty(definition.Goal)
                ? definition.Goal
                : "Fulfilled victory condition " + SafePresentationValue(condition.Id, condition.Type) + ".";
        }

        private static string SafePresentationValue(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? (fallback ?? "objective") : value.Replace("_", " ");
        }

        private static void ReturnAuthoringPlaytestToEditor()
        {
            try
            {
                ScenarioEditorController editor = ScenarioEditorController.Instance;
                if (editor != null && editor.CurrentSession != null)
                    editor.EndPlaytest();
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioWinLoss] Outcome resolved, but authoring return could not be completed: " + ex.Message);
            }
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
