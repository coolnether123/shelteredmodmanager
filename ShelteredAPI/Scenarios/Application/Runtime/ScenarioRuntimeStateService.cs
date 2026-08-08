using System;
using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Persistence;
namespace ShelteredAPI.Scenarios.Application.Runtime{
    internal sealed class ScenarioRuntimeStateService
    {
        private readonly ScenarioRuntimeExecutionJournalRepository _repository;

        public ScenarioRuntimeStateService(ScenarioRuntimeExecutionJournalRepository repository)
        {
            _repository = repository;
        }

        public ScenarioRuntimeState State
        {
            get { return _repository.State; }
        }

        public void EnsureHooked()
        {
            _repository.EnsureHooked();
        }

        public ScenarioRuntimeState Bind(ScenarioDefinition definition, ScenarioRuntimeBinding binding)
        {
            string scenarioId = definition != null ? definition.Id : null;
            string version = definition != null ? definition.Version : null;
            string runtimeBindingId = BuildRuntimeBindingId(binding, scenarioId, version);
            ScenarioRuntimeState state = _repository.State;
            if (state == null
                || !string.Equals(state.ScenarioId ?? string.Empty, scenarioId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(state.RuntimeBindingId ?? string.Empty, runtimeBindingId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                state = new ScenarioRuntimeState();
                _repository.Replace(state);
            }

            state.ScenarioId = scenarioId;
            state.ScenarioVersion = version;
            state.RuntimeBindingId = runtimeBindingId;
            return state;
        }

        internal static string BuildRuntimeBindingId(ScenarioRuntimeBinding binding, string scenarioId, string version)
        {
            if (binding == null)
                return (scenarioId ?? string.Empty) + "@" + (version ?? string.Empty);
            return (binding.ScenarioId ?? scenarioId ?? string.Empty)
                + "@"
                + (binding.VersionApplied ?? version ?? string.Empty)
                + "#"
                + binding.DayCreated.ToString()
                + "#"
                + (binding.IsPreview ? "preview:" : "run:")
                + (binding.RunId ?? string.Empty);
        }
    }

    internal sealed class ScenarioScoreSnapshotService : IScenarioScoreSnapshotService
    {
        private readonly ScenarioRuntimeStateService _stateService;

        public ScenarioScoreSnapshotService(ScenarioRuntimeStateService stateService)
        {
            _stateService = stateService;
        }

        public ScenarioScoreSnapshot GetSnapshot()
        {
            ScenarioRuntimeState state = _stateService != null ? _stateService.State : null;
            return CloneSnapshot(state != null ? state.ScoreSnapshot : null);
        }

        public void SetSnapshot(ScenarioScoreSnapshot snapshot)
        {
            ScenarioRuntimeState state = _stateService != null ? _stateService.State : null;
            if (state == null)
                return;

            if (snapshot == null)
            {
                state.ScoreSnapshot = null;
                return;
            }

            ScenarioScoreSnapshot copy = CloneSnapshot(snapshot);
            if (string.IsNullOrEmpty(copy.ScenarioId))
                copy.ScenarioId = state.ScenarioId;
            if (string.IsNullOrEmpty(copy.ScenarioVersion))
                copy.ScenarioVersion = state.ScenarioVersion;
            if (string.IsNullOrEmpty(copy.RuntimeBindingId))
                copy.RuntimeBindingId = state.RuntimeBindingId;
            if (string.IsNullOrEmpty(copy.Outcome))
                copy.Outcome = state.ScenarioOutcome;
            if (string.IsNullOrEmpty(copy.OutcomeConditionId))
                copy.OutcomeConditionId = state.ScenarioOutcomeConditionId;
            StampCurrentTimeIfUnset(copy);
            state.ScoreSnapshot = copy;
        }

        public void ClearSnapshot()
        {
            ScenarioRuntimeState state = _stateService != null ? _stateService.State : null;
            if (state != null)
                state.ScoreSnapshot = null;
        }

        private static void StampCurrentTimeIfUnset(ScenarioScoreSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Day != 0 || snapshot.Hour != 0 || snapshot.Minute != 0)
                return;

            try
            {
                snapshot.Day = GameTime.Day;
                snapshot.Hour = GameTime.Hour;
                snapshot.Minute = GameTime.Minute;
            }
            catch
            {
            }
        }

        private static ScenarioScoreSnapshot CloneSnapshot(ScenarioScoreSnapshot source)
        {
            if (source == null)
                return null;

            ScenarioScoreSnapshot target = new ScenarioScoreSnapshot();
            target.ScenarioId = source.ScenarioId;
            target.ScenarioVersion = source.ScenarioVersion;
            target.RuntimeBindingId = source.RuntimeBindingId;
            target.CompletionState = source.CompletionState;
            target.Outcome = source.Outcome;
            target.OutcomeConditionId = source.OutcomeConditionId;
            target.HasTotalScore = source.HasTotalScore;
            target.TotalScore = source.TotalScore;
            target.Day = source.Day;
            target.Hour = source.Hour;
            target.Minute = source.Minute;

            for (int i = 0; source.Categories != null && i < source.Categories.Count; i++)
            {
                ScenarioScoreCategorySnapshot category = source.Categories[i];
                if (category == null)
                    continue;

                target.Categories.Add(new ScenarioScoreCategorySnapshot
                {
                    CategoryId = category.CategoryId,
                    DisplayName = category.DisplayName,
                    Score = category.Score
                });
            }

            for (int i = 0; source.Rules != null && i < source.Rules.Count; i++)
            {
                ScenarioScoreRuleSnapshot rule = source.Rules[i];
                if (rule == null)
                    continue;

                target.Rules.Add(new ScenarioScoreRuleSnapshot
                {
                    RuleId = rule.RuleId,
                    CategoryId = rule.CategoryId,
                    DisplayName = rule.DisplayName,
                    Source = rule.Source,
                    Value = rule.Value,
                    Score = rule.Score
                });
            }

            for (int i = 0; source.Metadata != null && i < source.Metadata.Count; i++)
            {
                ScenarioProperty property = source.Metadata[i];
                if (property == null)
                    continue;

                target.Metadata.Add(new ScenarioProperty
                {
                    Key = property.Key,
                    Value = property.Value
                });
            }

            return target;
        }
    }
}
