using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
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
            AddQuests(definition, runtimeState, entries);
            AddBunker(definition, runtimeState, entries);
            AddObjectActivations(definition, runtimeState, entries);
            AddScheduledActions(definition, runtimeState, entries);
            return entries;
        }

        private static void AddFutureSurvivors(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count; i++)
            {
                FutureSurvivorDefinition survivor = definition.FamilySetup.FutureSurvivors[i];
                string id = "legacy.survivor." + BuildId(survivor != null ? survivor.Id : null, i);
                entries.Add(NewEntry(id, ScenarioTimelineEntryKind.Survivor, survivor != null ? survivor.Arrival : null, "Future survivor " + Safe(survivor != null && survivor.Survivor != null ? survivor.Survivor.Name : null), "FutureSurvivor", "People", survivor != null ? survivor.Id : null, runtimeState, "legacy"));
            }
        }

        private static void AddInventory(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.StartingInventory != null && definition.StartingInventory.ScheduledChanges != null && i < definition.StartingInventory.ScheduledChanges.Count; i++)
            {
                TimedInventoryChangeDefinition change = definition.StartingInventory.ScheduledChanges[i];
                string id = "legacy.inventory." + BuildId(change != null ? change.Id : null, i);
                entries.Add(NewEntry(id, ScenarioTimelineEntryKind.Inventory, change != null ? change.When : null, (change != null ? change.Kind.ToString() : "Inventory") + " " + Safe(change != null ? change.ItemId : null), "Inventory", "Inventory / Storage", change != null ? change.Id : null, runtimeState, "legacy"));
            }
        }

        private static void AddWeather(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.WeatherEvents != null && i < definition.TriggersAndEvents.WeatherEvents.Count; i++)
            {
                WeatherEventDefinition weather = definition.TriggersAndEvents.WeatherEvents[i];
                string baseId = BuildId(weather != null ? weather.Id : null, i);
                entries.Add(NewEntry("legacy.weather." + baseId, ScenarioTimelineEntryKind.Weather, weather != null ? weather.When : null, "Weather " + Safe(weather != null ? weather.WeatherState : null), "Weather", "Events", weather != null ? weather.Id : null, runtimeState, "legacy"));
                if (weather != null && weather.DurationHours > 0)
                    entries.Add(NewEntry("legacy.weather." + baseId + ".restore", ScenarioTimelineEntryKind.Weather, AddHours(weather.When, weather.DurationHours), "Restore weather", "Weather", "Events", weather.Id, runtimeState, "legacy"));
            }
        }

        private static void AddQuests(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.Quests != null && definition.Quests.Quests != null && i < definition.Quests.Quests.Count; i++)
            {
                QuestDefinition quest = definition.Quests.Quests[i];
                if (quest == null || !string.IsNullOrEmpty(quest.StartTriggerId))
                    continue;
                entries.Add(NewEntry("legacy.quest." + BuildId(quest.Id, i), ScenarioTimelineEntryKind.Quest, quest.ScheduledStart, "Quest " + Safe(quest.Title ?? quest.Id), "Quest", "Quests", quest.Id, runtimeState, "legacy"));
            }
        }

        private static void AddBunker(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null && i < definition.BunkerGrid.Expansions.Count; i++)
            {
                ScenarioBunkerExpansionDefinition expansion = definition.BunkerGrid.Expansions[i];
                if (expansion == null || expansion.RequiredTime == null || string.IsNullOrEmpty(expansion.Id))
                    continue;
                entries.Add(NewEntry("legacy.bunker.expansion." + expansion.Id, ScenarioTimelineEntryKind.Bunker, expansion.RequiredTime, "Expansion " + Safe(expansion.DisplayName ?? expansion.Id), "BunkerExpansion", "Bunker", expansion.Id, runtimeState, "legacy"));
            }
        }

        private static void AddObjectActivations(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null || string.IsNullOrEmpty(placement.ScheduledActivationId))
                    continue;
                entries.Add(NewEntry(placement.ScheduledActivationId, ScenarioTimelineEntryKind.Object, null, "Activate object " + Safe(placement.ScenarioObjectId ?? placement.DefinitionReference ?? placement.PrefabReference), "ObjectActivation", "Bunker", placement.ScenarioObjectId, runtimeState, "legacy"));
            }
        }

        private static void AddScheduledActions(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, List<ScenarioTimelineEntry> entries)
        {
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                if (action == null)
                    continue;
                entries.Add(NewEntry(action.Id, InferKind(action), action.DueTime, Safe(action.ActionType ?? action.Id), action.ActionType, InferStage(action), ResolveTarget(action), runtimeState, "shared"));
            }
        }

        private static ScenarioTimelineEntry NewEntry(string id, ScenarioTimelineEntryKind kind, ScenarioScheduleTime when, string title, string type, string ownerStage, string targetId, ScenarioRuntimeState runtimeState, string source)
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
                case ScenarioTimelineEntryKind.Bunker:
                case ScenarioTimelineEntryKind.Object: return "Bunker";
                case ScenarioTimelineEntryKind.Map: return "Map";
                default: return "Events";
            }
        }

        private static string ResolveTarget(ScenarioScheduledActionDefinition action)
        {
            for (int i = 0; action != null && action.Effects != null && i < action.Effects.Count; i++)
            {
                ScenarioEffectDefinition effect = action.Effects[i];
                if (effect == null)
                    continue;
                if (!string.IsNullOrEmpty(effect.TargetId)) return effect.TargetId;
                if (!string.IsNullOrEmpty(effect.ObjectId)) return effect.ObjectId;
                if (!string.IsNullOrEmpty(effect.QuestId)) return effect.QuestId;
                if (!string.IsNullOrEmpty(effect.SurvivorId)) return effect.SurvivorId;
                if (!string.IsNullOrEmpty(effect.ItemId)) return effect.ItemId;
                if (!string.IsNullOrEmpty(effect.BunkerExpansionId)) return effect.BunkerExpansionId;
            }
            return action != null ? action.Id : null;
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
