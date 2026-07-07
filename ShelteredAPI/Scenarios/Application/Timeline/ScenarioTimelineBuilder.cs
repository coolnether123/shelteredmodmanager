using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;

using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Bunker;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Journal;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Domain.Timeline;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Application.Timeline{
    internal sealed class ScenarioTimelineBuilder
    {
        public List<ScenarioTimelineDay> BuildDays(ScenarioDefinition definition, ScenarioRuntimeState runtimeState)
        {
            List<ScenarioTimelineEntry> entries = BuildEntries(definition, runtimeState);
            Dictionary<int, ScenarioTimelineDay> days = new Dictionary<int, ScenarioTimelineDay>();
            for (int i = 0; i < entries.Count; i++)
            {
                ScenarioTimelineEntry entry = entries[i];
                int dayNumber = entry != null && entry.When != null ? entry.When.Day : 1;
                ScenarioTimelineDay day;
                if (!days.TryGetValue(dayNumber, out day))
                {
                    day = new ScenarioTimelineDay();
                    day.Day = dayNumber;
                    days.Add(dayNumber, day);
                }
                day.Entries.Add(entry);
            }

            List<ScenarioTimelineDay> result = new List<ScenarioTimelineDay>(days.Values);
            result.Sort(delegate(ScenarioTimelineDay left, ScenarioTimelineDay right) { return left.Day.CompareTo(right.Day); });
            for (int i = 0; i < result.Count; i++)
                result[i].Entries.Sort(CompareEntryTime);
            return result;
        }

        public List<ScenarioTimelineEntry> BuildEntries(ScenarioDefinition definition, ScenarioRuntimeState runtimeState)
        {
            List<ScenarioTimelineEntry> entries = new List<ScenarioTimelineEntry>();
            AddFutureSurvivors(definition, runtimeState, entries);
            AddInventory(definition, runtimeState, entries);
            AddWeather(definition, runtimeState, entries);
            AddTriggers(definition, runtimeState, entries);
            AddQuests(definition, runtimeState, entries);
            AddStoryFlow(definition, runtimeState, entries);
            AddBunker(definition, runtimeState, entries);
            AddObjectActivations(definition, runtimeState, entries);
            AddJournal(definition, runtimeState, entries);
            AddScheduledActions(definition, runtimeState, entries);
            return entries;
        }

        private static void AddFutureSurvivors(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count; i++)
            {
                FutureSurvivorDefinition survivor = definition.FamilySetup.FutureSurvivors[i];
                string id = "legacy.survivor." + BuildId(survivor != null ? survivor.Id : null, i);
                ScenarioActorRef actorRef = survivor != null && survivor.ActorRef != null ? survivor.ActorRef : survivor != null && survivor.Survivor != null ? survivor.Survivor.ActorRef : null;
                string name = ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, actorRef, false, true, survivor != null && survivor.Survivor != null ? survivor.Survivor.Name : null);
                entries.Add(NewEntry(id, ScenarioTimelineEntryKind.Survivor, survivor != null ? survivor.Arrival : null, "Future survivor " + Safe(name), "FutureSurvivor", "People", survivor != null ? survivor.Id : null, runtimeState, "legacy", "future_survivor", "FamilySetup.FutureSurvivors", i, survivor != null ? survivor.Id : null, ScenarioAuthoringWindowIds.Survivors, ScenarioAuthoringLocalActionIds.ActionFutureSurvivorEditorOpenPrefix + i.ToString()));
            }
        }

        private static void AddInventory(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.StartingInventory != null && definition.StartingInventory.ScheduledChanges != null && i < definition.StartingInventory.ScheduledChanges.Count; i++)
            {
                TimedInventoryChangeDefinition change = definition.StartingInventory.ScheduledChanges[i];
                string id = "legacy.inventory." + BuildId(change != null ? change.Id : null, i);
                entries.Add(NewEntry(id, ScenarioTimelineEntryKind.Inventory, change != null ? change.When : null, (change != null ? change.Kind.ToString() : "Inventory") + " " + Safe(change != null ? change.ItemId : null), "Inventory", "Inventory / Storage", change != null ? change.Id : null, runtimeState, "legacy", "inventory_change", "StartingInventory.ScheduledChanges", i, change != null ? change.Id : null, ScenarioAuthoringWindowIds.Stockpile, ScenarioAuthoringActionIds.ActionInventoryScheduleDeletePrefix + i.ToString()));
            }
        }

        private static void AddWeather(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.WeatherEvents != null && i < definition.TriggersAndEvents.WeatherEvents.Count; i++)
            {
                WeatherEventDefinition weather = definition.TriggersAndEvents.WeatherEvents[i];
                string baseId = BuildId(weather != null ? weather.Id : null, i);
                entries.Add(NewEntry("legacy.weather." + baseId, ScenarioTimelineEntryKind.Weather, weather != null ? weather.When : null, "Weather " + Safe(weather != null ? weather.WeatherState : null), "Weather", "Events", weather != null ? weather.Id : null, runtimeState, "legacy", "weather_event", "TriggersAndEvents.WeatherEvents", i, weather != null ? weather.Id : null, ScenarioAuthoringWindowIds.Triggers, ScenarioAuthoringActionIds.ActionWeatherScheduleDeletePrefix + i.ToString()));
                if (weather != null && weather.DurationHours > 0)
                    entries.Add(NewEntry("legacy.weather." + baseId + ".restore", ScenarioTimelineEntryKind.Weather, AddHours(weather.When, weather.DurationHours), "Restore weather", "Weather", "Events", weather.Id, runtimeState, "legacy", "weather_restore", "TriggersAndEvents.WeatherEvents", i, weather.Id, ScenarioAuthoringWindowIds.Triggers, ScenarioAuthoringActionIds.ActionWeatherScheduleDurationPrefix + i.ToString() + ".-1"));
            }
        }

        private static void AddQuests(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.Quests != null && definition.Quests.Quests != null && i < definition.Quests.Quests.Count; i++)
            {
                QuestDefinition quest = definition.Quests.Quests[i];
                if (quest == null)
                    continue;
                entries.Add(NewEntry("legacy.quest." + BuildId(quest.Id, i), ScenarioTimelineEntryKind.Quest, quest.ScheduledStart, "Quest " + Safe(quest.Title ?? quest.Id), "Quest", "Quests", quest.Id, runtimeState, "legacy", "quest_popup", "Quests.Quests", i, quest.Id, ScenarioAuthoringWindowIds.Quests, ScenarioAuthoringActionIds.ActionQuestScheduleDeletePrefix + i.ToString()));
            }
        }

        private static void AddStoryFlow(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.ScenarioFlow != null && definition.ScenarioFlow.Stages != null && i < definition.ScenarioFlow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = definition.ScenarioFlow.Stages[i];
                if (stage == null)
                    continue;
                if (!string.IsNullOrEmpty(stage.UnansweredNextStage))
                {
                    entries.Add(NewEntry("flow.stage." + BuildId(stage.Id, i) + ".unanswered", ScenarioTimelineEntryKind.Story, DaysFromNow(stage.UnansweredNextDays), "Story unanswered -> " + Safe(stage.UnansweredNextStage), "StoryStage", "Story", stage.Id, runtimeState, "flow", "story_stage_unanswered", "ScenarioFlow.Stages", i, stage.Id, ScenarioAuthoringWindowIds.Quests, ScenarioAuthoringActionIds.ActionStoryStageUnansweredDelayPrefix + i.ToString()));
                }

                for (int s = 0; stage.IntercomStages != null && s < stage.IntercomStages.Count; s++)
                {
                    ScenarioIntercomStageDefinition intercom = stage.IntercomStages[s];
                    if (intercom == null || intercom.StageChange == null || string.IsNullOrEmpty(intercom.StageChange.Id))
                        continue;
                    string sourceId = stage.Id + "/" + (intercom.Id ?? s.ToString());
                    entries.Add(NewEntry("flow.stage_change." + BuildId(stage.Id, i) + "." + BuildId(intercom.Id, s), ScenarioTimelineEntryKind.Story, DaysFromNow(intercom.StageChange.DelayDays), "Story stage change -> " + Safe(intercom.StageChange.Id), "StoryStageChange", "Story", intercom.StageChange.Id, runtimeState, "flow", "story_stage_change", "ScenarioFlow.Stages[" + i.ToString() + "].IntercomStages", s, sourceId, ScenarioAuthoringWindowIds.Quests, ScenarioAuthoringActionIds.ActionStoryStageChangeDelayPrefix + i.ToString() + "." + s.ToString()));
                }
            }
        }

        private static void AddTriggers(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                ScenarioScheduledActionDefinition action;
                string reason;
                if (!ScenarioTriggerDefinitionCompiler.TryCreateAction(trigger, i, out action, out reason))
                    continue;

                entries.Add(NewEntry(action.Id, ScenarioTimelineEntryKind.CustomModded, action.DueTime, "Trigger " + Safe(trigger.Id), "Trigger", "Events", trigger.Id, runtimeState, "legacy", "trigger", "TriggersAndEvents.Triggers", i, trigger.Id, ScenarioAuthoringWindowIds.Triggers, ScenarioAuthoringActionIds.ActionTriggerDeletePrefix + i.ToString()));
            }
        }

        private static void AddBunker(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null && i < definition.BunkerGrid.Expansions.Count; i++)
            {
                ScenarioBunkerExpansionDefinition expansion = definition.BunkerGrid.Expansions[i];
                if (expansion == null || expansion.RequiredTime == null || string.IsNullOrEmpty(expansion.Id))
                    continue;
                entries.Add(NewEntry("legacy.bunker.expansion." + expansion.Id, ScenarioTimelineEntryKind.Bunker, expansion.RequiredTime, "Expansion " + Safe(expansion.DisplayName ?? expansion.Id), "BunkerExpansion", "Bunker", expansion.Id, runtimeState, "legacy", "bunker_expansion", "BunkerGrid.Expansions", i, expansion.Id, ScenarioAuthoringWindowIds.BuildTools, null));
            }
        }

        private static void AddObjectActivations(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null || string.IsNullOrEmpty(placement.ScheduledActivationId))
                    continue;
                entries.Add(NewEntry(placement.ScheduledActivationId, ScenarioTimelineEntryKind.Object, null, "Activate object " + Safe(placement.ScenarioObjectId ?? placement.DefinitionReference ?? placement.PrefabReference), "ObjectActivation", "Bunker", placement.ScenarioObjectId, runtimeState, "legacy", "object_activation", "BunkerEdits.ObjectPlacements", i, placement.ScenarioObjectId, ScenarioAuthoringWindowIds.BuildTools, null));
            }
        }

        private static void AddScheduledActions(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                if (action == null)
                    continue;
                entries.Add(NewEntry(action.Id, InferKind(action), action.DueTime, ResolveTitle(definition, action), action.ActionType, InferStage(action), ResolveTarget(definition, action), runtimeState, "shared", "scheduled_action", "ScheduledActions", i, action.Id, ScenarioAuthoringWindowIds.Triggers, ResolveFocusAction(definition, action, i)));
            }
        }

        private static void AddJournal(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.Journal != null && definition.Journal.Entries != null && i < definition.Journal.Entries.Count; i++)
            {
                JournalEntryDefinition entry = definition.Journal.Entries[i];
                if (entry == null || entry.DueTime == null)
                    continue;

                string id = "journal." + BuildId(entry.Id, i);
                entries.Add(NewEntry(
                    id,
                    ScenarioTimelineEntryKind.Journal,
                    entry.DueTime,
                    "Journal " + Safe(entry.Id),
                    "JournalEntry",
                    "Events",
                    entry.Id,
                    runtimeState,
                    "journal",
                    "journal_entry",
                    "Journal.Entries",
                    i,
                    entry.Id,
                    ScenarioAuthoringWindowIds.Triggers,
                    ScenarioAuthoringActionIds.ActionJournalEntryDeletePrefix + i.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static ScenarioTimelineEntry NewEntry(string id, ScenarioTimelineEntryKind kind, ScenarioScheduleTime when, string title, string type, string ownerStage, string targetId, ScenarioRuntimeState runtimeState, string source, string sourceKind, string sourceCollection, int sourceIndex, string sourceId, string ownerWindowId, string focusActionId)
        {
            ScenarioTimelineEntry entry = new ScenarioTimelineEntry();
            entry.Id = id;
            entry.Kind = kind;
            entry.When = when != null ? when : new ScenarioScheduleTime();
            entry.Title = title;
            entry.Type = type;
            entry.OwnerStage = ownerStage;
            entry.OwnerId = targetId;
            entry.TargetId = targetId;
            entry.Source = source;
            entry.SourceKind = sourceKind;
            entry.SourceCollection = sourceCollection;
            entry.SourceIndex = sourceIndex;
            entry.SourceId = sourceId;
            entry.OwnerWindowId = ownerWindowId;
            entry.FocusActionId = focusActionId;
            ApplyStatus(entry, runtimeState);
            return entry;
        }

        private static void ApplyStatus(ScenarioTimelineEntry entry, ScenarioRuntimeState runtimeState)
        {
            for (int i = 0; runtimeState != null && runtimeState.ExecutedActions != null && i < runtimeState.ExecutedActions.Count; i++)
            {
                ScenarioExecutedActionRecord record = runtimeState.ExecutedActions[i];
                if (record == null || !string.Equals(record.ActionKey, entry.Id, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (record.Status == ScenarioExecutedActionStatus.Succeeded)
                    entry.Status = ScenarioTimelineEntryStatus.Fired;
                else if (record.Status == ScenarioExecutedActionStatus.Blocked)
                    entry.Status = ScenarioTimelineEntryStatus.Blocked;
                else if (record.Status == ScenarioExecutedActionStatus.Failed)
                    entry.Status = ScenarioTimelineEntryStatus.Failed;
                entry.Warning = record.Message;
            }
        }

        private static ScenarioTimelineEntryKind InferKind(ScenarioScheduledActionDefinition action)
        {
            for (int i = 0; action != null && action.Effects != null && i < action.Effects.Count; i++)
            {
                ScenarioEffectDefinition effect = action.Effects[i];
                if (effect == null)
                    continue;
                switch (effect.Kind)
                {
                    case ScenarioEffectKind.AddInventory:
                    case ScenarioEffectKind.RemoveInventory:
                        return ScenarioTimelineEntryKind.Inventory;
                    case ScenarioEffectKind.SetWeather:
                    case ScenarioEffectKind.RestoreWeather:
                        return ScenarioTimelineEntryKind.Weather;
                    case ScenarioEffectKind.SpawnFutureSurvivor:
                        return ScenarioTimelineEntryKind.Survivor;
                    case ScenarioEffectKind.StartQuest:
                        return ScenarioTimelineEntryKind.Quest;
                    case ScenarioEffectKind.ActivateObject:
                    case ScenarioEffectKind.DeactivateObject:
                        return ScenarioTimelineEntryKind.Object;
                    case ScenarioEffectKind.UnlockBunkerExpansion:
                        return ScenarioTimelineEntryKind.Bunker;
                    case ScenarioEffectKind.WorldEvent:
                        return ScenarioTimelineEntryKind.WorldEvent;
                    case ScenarioEffectKind.WriteJournalEntry:
                        return ScenarioTimelineEntryKind.Journal;
                }
            }
            return ScenarioTimelineEntryKind.CustomModded;
        }

        private static string InferStage(ScenarioScheduledActionDefinition action)
        {
            ScenarioTimelineEntryKind kind = InferKind(action);
            switch (kind)
            {
                case ScenarioTimelineEntryKind.Inventory: return "Inventory / Storage";
                case ScenarioTimelineEntryKind.Weather: return "Events";
                case ScenarioTimelineEntryKind.Survivor: return "People";
                case ScenarioTimelineEntryKind.Quest: return "Quests";
                case ScenarioTimelineEntryKind.Story: return "Story";
                case ScenarioTimelineEntryKind.Bunker:
                case ScenarioTimelineEntryKind.Object: return "Bunker";
                case ScenarioTimelineEntryKind.Map: return "Map";
                case ScenarioTimelineEntryKind.WorldEvent: return "Events";
                case ScenarioTimelineEntryKind.Journal: return "Events";
                default: return "Events";
            }
        }

        private static string ResolveTitle(ScenarioDefinition definition, ScenarioScheduledActionDefinition action)
        {
            for (int i = 0; action != null && action.Effects != null && i < action.Effects.Count; i++)
            {
                ScenarioEffectDefinition effect = action.Effects[i];
                if (effect == null || effect.Kind != ScenarioEffectKind.SpawnFutureSurvivor)
                {
                    if (effect != null && effect.Kind == ScenarioEffectKind.WorldEvent)
                        return "World event " + Safe(ScenarioPropertyBag.GetString(effect.Properties, "eventType", effect.TargetId));
                    if (effect != null && effect.Kind == ScenarioEffectKind.WriteJournalEntry)
                        return "Journal " + Safe(ScenarioPropertyBag.GetString(effect.Properties, "entryId", effect.TargetId));
                    continue;
                }

                string name = effect.ActorRef != null
                    ? ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, effect.ActorRef, false, true, effect.SurvivorId ?? effect.TargetId)
                    : ResolveFutureSurvivorName(definition, effect.SurvivorId ?? effect.TargetId);
                return "Spawn future survivor " + Safe(name);
            }

            return Safe(action != null ? action.ActionType ?? action.Id : null);
        }

        private static string ResolveTarget(ScenarioDefinition definition, ScenarioScheduledActionDefinition action)
        {
            for (int i = 0; action != null && action.Effects != null && i < action.Effects.Count; i++)
            {
                ScenarioEffectDefinition effect = action.Effects[i];
                if (effect == null)
                    continue;
                if (effect.ActorRef != null)
                    return ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, effect.ActorRef, false, true, effect.SurvivorId ?? effect.TargetId);
                if (!string.IsNullOrEmpty(effect.TargetId)) return effect.TargetId;
                if (!string.IsNullOrEmpty(effect.ObjectId)) return effect.ObjectId;
                if (!string.IsNullOrEmpty(effect.QuestId)) return effect.QuestId;
                if (!string.IsNullOrEmpty(effect.TriggerId)) return effect.TriggerId;
                if (!string.IsNullOrEmpty(effect.SurvivorId)) return effect.SurvivorId;
                if (!string.IsNullOrEmpty(effect.ItemId)) return effect.ItemId;
                if (!string.IsNullOrEmpty(effect.BunkerExpansionId)) return effect.BunkerExpansionId;
            }
            return action != null ? action.Id : null;
        }

        private static string ResolveFocusAction(ScenarioDefinition definition, ScenarioScheduledActionDefinition action, int actionIndex)
        {
            for (int i = 0; action != null && action.Effects != null && i < action.Effects.Count; i++)
            {
                ScenarioEffectDefinition effect = action.Effects[i];
                if (effect == null || effect.Kind != ScenarioEffectKind.SpawnFutureSurvivor)
                    continue;

                ScenarioCastMemberReferenceCandidate candidate = effect.ActorRef != null
                    ? ScenarioCastMemberReferenceCatalog.FindByActorRef(definition, effect.ActorRef, false, true)
                    : ScenarioCastMemberReferenceCatalog.FindByFutureSurvivorId(definition, effect.SurvivorId ?? effect.TargetId);
                if (candidate != null && candidate.Index >= 0)
                    return ScenarioAuthoringLocalActionIds.ActionFutureSurvivorEditorOpenPrefix + candidate.Index.ToString(CultureInfo.InvariantCulture);
            }

            return ScenarioAuthoringActionIds.ActionScheduledActionDeletePrefix + actionIndex.ToString(CultureInfo.InvariantCulture);
        }

        private static string ResolveFutureSurvivorName(ScenarioDefinition definition, string survivorId)
        {
            ScenarioCastMemberReferenceCandidate candidate = ScenarioCastMemberReferenceCatalog.FindByFutureSurvivorId(definition, survivorId);
            return candidate != null ? candidate.DisplayName : survivorId;
        }

        private static int CompareEntryTime(ScenarioTimelineEntry left, ScenarioTimelineEntry right)
        {
            int hour = (left.When != null ? left.When.Hour : 0).CompareTo(right.When != null ? right.When.Hour : 0);
            if (hour != 0) return hour;
            return (left.When != null ? left.When.Minute : 0).CompareTo(right.When != null ? right.When.Minute : 0);
        }

        private static ScenarioScheduleTime AddHours(ScenarioScheduleTime time, int hours)
        {
            ScenarioScheduleTime result = new ScenarioScheduleTime();
            result.Day = time != null ? time.Day : 1;
            result.Hour = time != null ? time.Hour : 0;
            result.Minute = time != null ? time.Minute : 0;
            int totalHours = result.Hour + hours;
            result.Day += totalHours / 24;
            result.Hour = totalHours % 24;
            return result;
        }

        private static ScenarioScheduleTime DaysFromNow(int days)
        {
            ScenarioScheduleTime result = new ScenarioScheduleTime();
            result.Day = Math.Max(1, days);
            result.Hour = 6;
            result.Minute = 0;
            return result;
        }

        private static string BuildId(string id, int index)
        {
            return !string.IsNullOrEmpty(id) ? id : index.ToString();
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<missing>" : value;
        }
    }
}
