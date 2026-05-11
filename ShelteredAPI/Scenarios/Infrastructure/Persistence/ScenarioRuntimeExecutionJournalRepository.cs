using System;
using System.Collections;
using ModAPI.Core;
using ShelteredAPI.Events;
using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Objects;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Infrastructure.Persistence{
    internal sealed class ScenarioRuntimeExecutionJournalRepository
    {
        private const string SaveGroupName = "CustomScenarioRuntimeState";
        private ScenarioRuntimeState _state = new ScenarioRuntimeState();
        private bool _hooked;

        public ScenarioRuntimeState State
        {
            get { return _state; }
        }

        public void Replace(ScenarioRuntimeState state)
        {
            _state = state ?? new ScenarioRuntimeState();
        }

        public void EnsureHooked()
        {
            if (_hooked)
                return;
            GameEvents.OnBeforeSave += HandleBeforeSave;
            GameEvents.OnAfterLoad += HandleAfterLoad;
            GameEvents.OnNewGame += HandleNewGame;
            _hooked = true;
        }

        private void HandleBeforeSave(SaveData data)
        {
            if (data == null || !data.isSaving)
                return;
            try { SaveLoad(data, _state); }
            catch (Exception ex) { MMLog.WriteWarning("[ScenarioRuntimeJournal] Failed to save runtime state: " + ex.Message); }
        }

        private void HandleAfterLoad(SaveData data)
        {
            if (data == null || !data.isLoading)
                return;
            try
            {
                ScenarioRuntimeState loaded = new ScenarioRuntimeState();
                SaveLoad(data, loaded);
                _state = loaded;
            }
            catch
            {
                _state = new ScenarioRuntimeState();
            }
        }

        private void HandleNewGame()
        {
            _state = new ScenarioRuntimeState();
        }

        private static void SaveLoad(SaveData data, ScenarioRuntimeState state)
        {
            data.GroupStart(SaveGroupName);
            string scenarioId = state.ScenarioId ?? string.Empty;
            string scenarioVersion = state.ScenarioVersion ?? string.Empty;
            string runtimeBindingId = state.RuntimeBindingId ?? string.Empty;
            string scenarioOutcome = state.ScenarioOutcome ?? string.Empty;
            string scenarioOutcomeConditionId = state.ScenarioOutcomeConditionId ?? string.Empty;
            int lastDay = state.LastProcessedDay;
            int lastHour = state.LastProcessedHour;
            int lastMinute = state.LastProcessedMinute;

            data.SaveLoad("ScenarioId", ref scenarioId);
            data.SaveLoad("ScenarioVersion", ref scenarioVersion);
            data.SaveLoad("RuntimeBindingId", ref runtimeBindingId);
            data.SaveLoad("ScenarioOutcome", ref scenarioOutcome);
            data.SaveLoad("ScenarioOutcomeConditionId", ref scenarioOutcomeConditionId);
            data.SaveLoad("LastProcessedDay", ref lastDay);
            data.SaveLoad("LastProcessedHour", ref lastHour);
            data.SaveLoad("LastProcessedMinute", ref lastMinute);

            state.ScenarioId = scenarioId;
            state.ScenarioVersion = scenarioVersion;
            state.RuntimeBindingId = runtimeBindingId;
            state.ScenarioOutcome = scenarioOutcome;
            state.ScenarioOutcomeConditionId = scenarioOutcomeConditionId;
            state.LastProcessedDay = lastDay;
            state.LastProcessedHour = lastHour;
            state.LastProcessedMinute = lastMinute;

            SaveLoadScoreSnapshot(data, state);
            SaveLoadExecuted(data, state);
            SaveLoadFlags(data, state);
            SaveLoadFiredTriggers(data, state);
            SaveLoadBunker(data, state);
            SaveLoadObjects(data, state);
            data.GroupEnd();
        }

        private static void SaveLoadScoreSnapshot(SaveData data, ScenarioRuntimeState state)
        {
            bool hasSnapshot = state.ScoreSnapshot != null;
            data.SaveLoad("HasScoreSnapshot", ref hasSnapshot);
            if (!hasSnapshot)
            {
                if (data.isLoading)
                    state.ScoreSnapshot = null;
                return;
            }

            if (state.ScoreSnapshot == null)
                state.ScoreSnapshot = new ScenarioScoreSnapshot();

            data.GroupStart("ScoreSnapshot");
            string scenarioId = state.ScoreSnapshot.ScenarioId ?? string.Empty;
            string scenarioVersion = state.ScoreSnapshot.ScenarioVersion ?? string.Empty;
            string runtimeBindingId = state.ScoreSnapshot.RuntimeBindingId ?? string.Empty;
            string outcome = state.ScoreSnapshot.Outcome ?? string.Empty;
            string outcomeConditionId = state.ScoreSnapshot.OutcomeConditionId ?? string.Empty;
            int completionState = (int)state.ScoreSnapshot.CompletionState;
            bool hasTotalScore = state.ScoreSnapshot.HasTotalScore;
            int totalScore = state.ScoreSnapshot.TotalScore;
            int day = state.ScoreSnapshot.Day;
            int hour = state.ScoreSnapshot.Hour;
            int minute = state.ScoreSnapshot.Minute;

            data.SaveLoad("ScenarioId", ref scenarioId);
            data.SaveLoad("ScenarioVersion", ref scenarioVersion);
            data.SaveLoad("RuntimeBindingId", ref runtimeBindingId);
            data.SaveLoad("CompletionState", ref completionState);
            data.SaveLoad("Outcome", ref outcome);
            data.SaveLoad("OutcomeConditionId", ref outcomeConditionId);
            data.SaveLoad("HasTotalScore", ref hasTotalScore);
            data.SaveLoad("TotalScore", ref totalScore);
            data.SaveLoad("Day", ref day);
            data.SaveLoad("Hour", ref hour);
            data.SaveLoad("Minute", ref minute);

            if (!Enum.IsDefined(typeof(ScenarioScoreCompletionState), completionState))
                completionState = (int)ScenarioScoreCompletionState.Unknown;

            state.ScoreSnapshot.ScenarioId = scenarioId;
            state.ScoreSnapshot.ScenarioVersion = scenarioVersion;
            state.ScoreSnapshot.RuntimeBindingId = runtimeBindingId;
            state.ScoreSnapshot.CompletionState = (ScenarioScoreCompletionState)completionState;
            state.ScoreSnapshot.Outcome = outcome;
            state.ScoreSnapshot.OutcomeConditionId = outcomeConditionId;
            state.ScoreSnapshot.HasTotalScore = hasTotalScore;
            state.ScoreSnapshot.TotalScore = totalScore;
            state.ScoreSnapshot.Day = day;
            state.ScoreSnapshot.Hour = hour;
            state.ScoreSnapshot.Minute = minute;

            SaveLoadScoreCategories(data, state.ScoreSnapshot);
            SaveLoadScoreRules(data, state.ScoreSnapshot);
            SaveLoadScoreMetadata(data, state.ScoreSnapshot);
            data.GroupEnd();
        }

        private static void SaveLoadScoreCategories(SaveData data, ScenarioScoreSnapshot snapshot)
        {
            ArrayList loaded = new ArrayList();
            data.SaveLoadList("ScoreCategories", (IList)snapshot.Categories,
                delegate(int i)
                {
                    ScenarioScoreCategorySnapshot category = snapshot.Categories[i];
                    SaveLoadScoreCategory(data, category);
                },
                delegate(int i)
                {
                    ScenarioScoreCategorySnapshot category = new ScenarioScoreCategorySnapshot();
                    SaveLoadScoreCategory(data, category);
                    loaded.Add(category);
                });

            if (data.isLoading)
            {
                snapshot.Categories.Clear();
                for (int i = 0; i < loaded.Count; i++)
                    snapshot.Categories.Add((ScenarioScoreCategorySnapshot)loaded[i]);
            }
        }

        private static void SaveLoadScoreCategory(SaveData data, ScenarioScoreCategorySnapshot category)
        {
            string categoryId = category.CategoryId ?? string.Empty;
            string displayName = category.DisplayName ?? string.Empty;
            int score = category.Score;
            data.SaveLoad("CategoryId", ref categoryId);
            data.SaveLoad("DisplayName", ref displayName);
            data.SaveLoad("Score", ref score);
            category.CategoryId = categoryId;
            category.DisplayName = displayName;
            category.Score = score;
        }

        private static void SaveLoadScoreRules(SaveData data, ScenarioScoreSnapshot snapshot)
        {
            ArrayList loaded = new ArrayList();
            data.SaveLoadList("ScoreRules", (IList)snapshot.Rules,
                delegate(int i)
                {
                    ScenarioScoreRuleSnapshot rule = snapshot.Rules[i];
                    SaveLoadScoreRule(data, rule);
                },
                delegate(int i)
                {
                    ScenarioScoreRuleSnapshot rule = new ScenarioScoreRuleSnapshot();
                    SaveLoadScoreRule(data, rule);
                    loaded.Add(rule);
                });

            if (data.isLoading)
            {
                snapshot.Rules.Clear();
                for (int i = 0; i < loaded.Count; i++)
                    snapshot.Rules.Add((ScenarioScoreRuleSnapshot)loaded[i]);
            }
        }

        private static void SaveLoadScoreRule(SaveData data, ScenarioScoreRuleSnapshot rule)
        {
            string ruleId = rule.RuleId ?? string.Empty;
            string categoryId = rule.CategoryId ?? string.Empty;
            string displayName = rule.DisplayName ?? string.Empty;
            string source = rule.Source ?? string.Empty;
            float value = rule.Value;
            int score = rule.Score;
            data.SaveLoad("RuleId", ref ruleId);
            data.SaveLoad("CategoryId", ref categoryId);
            data.SaveLoad("DisplayName", ref displayName);
            data.SaveLoad("Source", ref source);
            data.SaveLoad("Value", ref value);
            data.SaveLoad("Score", ref score);
            rule.RuleId = ruleId;
            rule.CategoryId = categoryId;
            rule.DisplayName = displayName;
            rule.Source = source;
            rule.Value = value;
            rule.Score = score;
        }

        private static void SaveLoadScoreMetadata(SaveData data, ScenarioScoreSnapshot snapshot)
        {
            ArrayList loaded = new ArrayList();
            data.SaveLoadList("ScoreMetadata", (IList)snapshot.Metadata,
                delegate(int i)
                {
                    ScenarioProperty property = snapshot.Metadata[i];
                    SaveLoadScoreProperty(data, property);
                },
                delegate(int i)
                {
                    ScenarioProperty property = new ScenarioProperty();
                    SaveLoadScoreProperty(data, property);
                    loaded.Add(property);
                });

            if (data.isLoading)
            {
                snapshot.Metadata.Clear();
                for (int i = 0; i < loaded.Count; i++)
                    snapshot.Metadata.Add((ScenarioProperty)loaded[i]);
            }
        }

        private static void SaveLoadScoreProperty(SaveData data, ScenarioProperty property)
        {
            string key = property.Key ?? string.Empty;
            string value = property.Value ?? string.Empty;
            data.SaveLoad("Key", ref key);
            data.SaveLoad("Value", ref value);
            property.Key = key;
            property.Value = value;
        }

        private static void SaveLoadExecuted(SaveData data, ScenarioRuntimeState state)
        {
            ArrayList loaded = new ArrayList();
            data.SaveLoadList("ExecutedActions", (IList)state.ExecutedActions,
                delegate(int i)
                {
                    ScenarioExecutedActionRecord record = state.ExecutedActions[i];
                    SaveLoadRecord(data, record);
                },
                delegate(int i)
                {
                    ScenarioExecutedActionRecord record = new ScenarioExecutedActionRecord();
                    SaveLoadRecord(data, record);
                    loaded.Add(record);
                });

            if (data.isLoading)
            {
                state.ExecutedActions.Clear();
                for (int i = 0; i < loaded.Count; i++)
                    state.ExecutedActions.Add((ScenarioExecutedActionRecord)loaded[i]);
            }
        }

        private static void SaveLoadRecord(SaveData data, ScenarioExecutedActionRecord record)
        {
            string scenarioId = record.ScenarioId ?? string.Empty;
            string scenarioVersion = record.ScenarioVersion ?? string.Empty;
            string runtimeBindingId = record.RuntimeBindingId ?? string.Empty;
            string actionKey = record.ActionKey ?? string.Empty;
            string actionType = record.ActionType ?? string.Empty;
            string message = record.Message ?? string.Empty;
            int day = record.FiredDay;
            int hour = record.FiredHour;
            int minute = record.FiredMinute;
            int status = (int)record.Status;
            data.SaveLoad("ScenarioId", ref scenarioId);
            data.SaveLoad("ScenarioVersion", ref scenarioVersion);
            data.SaveLoad("RuntimeBindingId", ref runtimeBindingId);
            data.SaveLoad("ActionKey", ref actionKey);
            data.SaveLoad("ActionType", ref actionType);
            data.SaveLoad("FiredDay", ref day);
            data.SaveLoad("FiredHour", ref hour);
            data.SaveLoad("FiredMinute", ref minute);
            data.SaveLoad("Status", ref status);
            data.SaveLoad("Message", ref message);
            record.ScenarioId = scenarioId;
            record.ScenarioVersion = scenarioVersion;
            record.RuntimeBindingId = runtimeBindingId;
            record.ActionKey = actionKey;
            record.ActionType = actionType;
            record.FiredDay = day;
            record.FiredHour = hour;
            record.FiredMinute = minute;
            record.Status = (ScenarioExecutedActionStatus)status;
            record.Message = message;
        }

        private static void SaveLoadFlags(SaveData data, ScenarioRuntimeState state)
        {
            ArrayList loaded = new ArrayList();
            data.SaveLoadList("Flags", (IList)state.Flags,
                delegate(int i)
                {
                    ScenarioRuntimeFlag flag = state.Flags[i];
                    SaveLoadFlag(data, flag);
                },
                delegate(int i)
                {
                    ScenarioRuntimeFlag flag = new ScenarioRuntimeFlag();
                    SaveLoadFlag(data, flag);
                    loaded.Add(flag);
                });
            if (data.isLoading)
            {
                state.Flags.Clear();
                for (int i = 0; i < loaded.Count; i++)
                    state.Flags.Add((ScenarioRuntimeFlag)loaded[i]);
            }
        }

        private static void SaveLoadFlag(SaveData data, ScenarioRuntimeFlag flag)
        {
            string id = flag.FlagId ?? string.Empty;
            string value = flag.Value ?? string.Empty;
            data.SaveLoad("FlagId", ref id);
            data.SaveLoad("Value", ref value);
            flag.FlagId = id;
            flag.Value = value;
        }

        private static void SaveLoadFiredTriggers(SaveData data, ScenarioRuntimeState state)
        {
            ArrayList loaded = new ArrayList();
            data.SaveLoadList("FiredTriggers", (IList)state.FiredTriggers,
                delegate(int i)
                {
                    ScenarioFiredTriggerRecord record = state.FiredTriggers[i];
                    SaveLoadFiredTrigger(data, record);
                },
                delegate(int i)
                {
                    ScenarioFiredTriggerRecord record = new ScenarioFiredTriggerRecord();
                    SaveLoadFiredTrigger(data, record);
                    loaded.Add(record);
                });
            if (data.isLoading)
            {
                state.FiredTriggers.Clear();
                for (int i = 0; i < loaded.Count; i++)
                    state.FiredTriggers.Add((ScenarioFiredTriggerRecord)loaded[i]);
            }
        }

        private static void SaveLoadFiredTrigger(SaveData data, ScenarioFiredTriggerRecord record)
        {
            string triggerId = record.TriggerId ?? string.Empty;
            string source = record.Source ?? string.Empty;
            int day = record.FiredDay;
            int hour = record.FiredHour;
            int minute = record.FiredMinute;
            int fireCount = record.FireCount;
            data.SaveLoad("TriggerId", ref triggerId);
            data.SaveLoad("Source", ref source);
            data.SaveLoad("FiredDay", ref day);
            data.SaveLoad("FiredHour", ref hour);
            data.SaveLoad("FiredMinute", ref minute);
            data.SaveLoad("FireCount", ref fireCount);
            record.TriggerId = triggerId;
            record.Source = source;
            record.FiredDay = day;
            record.FiredHour = hour;
            record.FiredMinute = minute;
            record.FireCount = fireCount;
        }

        private static void SaveLoadBunker(SaveData data, ScenarioRuntimeState state)
        {
            ArrayList loaded = new ArrayList();
            data.SaveLoadList("UnlockedBunker", (IList)state.UnlockedBunker,
                delegate(int i)
                {
                    ScenarioUnlockedBunkerRecord record = state.UnlockedBunker[i];
                    SaveLoadUnlocked(data, record);
                },
                delegate(int i)
                {
                    ScenarioUnlockedBunkerRecord record = new ScenarioUnlockedBunkerRecord();
                    SaveLoadUnlocked(data, record);
                    loaded.Add(record);
                });
            if (data.isLoading)
            {
                state.UnlockedBunker.Clear();
                for (int i = 0; i < loaded.Count; i++)
                    state.UnlockedBunker.Add((ScenarioUnlockedBunkerRecord)loaded[i]);
            }
        }

        private static void SaveLoadUnlocked(SaveData data, ScenarioUnlockedBunkerRecord record)
        {
            string id = record.ExpansionId ?? string.Empty;
            int day = record.Day;
            int hour = record.Hour;
            int minute = record.Minute;
            data.SaveLoad("ExpansionId", ref id);
            data.SaveLoad("Day", ref day);
            data.SaveLoad("Hour", ref hour);
            data.SaveLoad("Minute", ref minute);
            record.ExpansionId = id;
            record.Day = day;
            record.Hour = hour;
            record.Minute = minute;
        }

        private static void SaveLoadObjects(SaveData data, ScenarioRuntimeState state)
        {
            ArrayList loaded = new ArrayList();
            data.SaveLoadList("ObjectStates", (IList)state.ObjectStates,
                delegate(int i)
                {
                    ScenarioObjectRuntimeStateRecord record = state.ObjectStates[i];
                    SaveLoadObject(data, record);
                },
                delegate(int i)
                {
                    ScenarioObjectRuntimeStateRecord record = new ScenarioObjectRuntimeStateRecord();
                    SaveLoadObject(data, record);
                    loaded.Add(record);
                });
            if (data.isLoading)
            {
                state.ObjectStates.Clear();
                for (int i = 0; i < loaded.Count; i++)
                    state.ObjectStates.Add((ScenarioObjectRuntimeStateRecord)loaded[i]);
            }
        }

        private static void SaveLoadObject(SaveData data, ScenarioObjectRuntimeStateRecord record)
        {
            string objectId = record.ScenarioObjectId ?? string.Empty;
            string binding = record.RuntimeBindingKey ?? string.Empty;
            int stateValue = (int)record.State;
            bool active = record.Active;
            bool locked = record.Locked;
            bool hidden = record.Hidden;
            data.SaveLoad("ScenarioObjectId", ref objectId);
            data.SaveLoad("RuntimeBindingKey", ref binding);
            data.SaveLoad("State", ref stateValue);
            data.SaveLoad("Active", ref active);
            data.SaveLoad("Locked", ref locked);
            data.SaveLoad("Hidden", ref hidden);
            record.ScenarioObjectId = objectId;
            record.RuntimeBindingKey = binding;
            record.State = (ScenarioObjectStartState)stateValue;
            record.Active = active;
            record.Locked = locked;
            record.Hidden = hidden;
        }
    }
}
