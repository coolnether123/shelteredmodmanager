using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using ShelteredAPI.Hooks;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioGameplayScheduleAuthoringService
    {
        private readonly ScenarioActorResolver _actorResolver;

        public ScenarioGameplayScheduleAuthoringService()
            : this(null)
        {
        }

        public ScenarioGameplayScheduleAuthoringService(ScenarioActorResolver actorResolver)
        {
            _actorResolver = actorResolver;
        }

        public bool TryHandleAction(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionFutureSurvivorAdd, StringComparison.Ordinal))
                return AddFutureSurvivor(session, out message);
            if (TryHandleFutureSurvivor(session, actionId, out message))
                return true;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionInventoryStartingAdd, StringComparison.Ordinal))
                return AddStartingInventoryItem(session, out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionInventoryScheduleAdd, StringComparison.Ordinal))
                return AddInventoryChange(session, ScenarioInventoryChangeKind.Add, out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionInventoryScheduleRemove, StringComparison.Ordinal))
                return AddInventoryChange(session, ScenarioInventoryChangeKind.Remove, out message);
            if (TryHandleInventoryChange(session, actionId, out message))
                return true;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionWeatherScheduleAdd, StringComparison.Ordinal))
                return AddWeatherEvent(session, out message);
            if (TryHandleWeatherEvent(session, actionId, out message))
                return true;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionQuestCaptureActive, StringComparison.Ordinal))
                return CaptureActiveQuests(session, out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionQuestScheduleAdd, StringComparison.Ordinal))
                return AddScheduledQuest(session, out message);
            if (TryAddCatalogQuest(session, actionId, out message))
                return true;
            if (TryHandleQuest(session, actionId, out message))
                return true;

            return false;
        }

        private bool AddFutureSurvivor(ScenarioEditorSession session, out string message)
        {
            FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
            FutureSurvivorDefinition survivor = new FutureSurvivorDefinition();
            survivor.Id = "future_survivor_" + (family.FutureSurvivors.Count + 1).ToString();
            survivor.Arrival = ScenarioAuthoringSchedule.NextTime();
            survivor.Survivor = ScenarioFamilyMemberFactory.CreateDefaultConfig(
                "New Survivor " + (family.FutureSurvivors.Count + 1).ToString(),
                ScenarioGender.Any);
            if (_actorResolver != null)
                _actorResolver.EnsureFutureSurvivorRef(session.WorkingDefinition, survivor, family.FutureSurvivors.Count);
            family.FutureSurvivors.Add(survivor);
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Family, ScenarioEditCategory.Family);
            message = "Added future survivor arrival for " + ScenarioAuthoringSchedule.Format(survivor.Arrival) + ".";
            return true;
        }

        private static bool AddStartingInventoryItem(ScenarioEditorSession session, out string message)
        {
            StartingInventoryDefinition inventory = EnsureInventory(session.WorkingDefinition);
            ItemEntry entry = new ItemEntry();
            entry.ItemId = ScenarioInventoryItemCatalog.DefaultItemId();
            entry.Quantity = 1;
            inventory.OverrideRandomStart = true;
            inventory.Items.Add(entry);
            MarkInventoryDirty(session);
            message = "Added starting stockpile item '" + entry.ItemId + "'.";
            return true;
        }

        private static bool AddInventoryChange(ScenarioEditorSession session, ScenarioInventoryChangeKind kind, out string message)
        {
            StartingInventoryDefinition inventory = EnsureInventory(session.WorkingDefinition);
            TimedInventoryChangeDefinition change = new TimedInventoryChangeDefinition();
            change.Id = "inventory_" + kind.ToString().ToLowerInvariant() + "_" + (inventory.ScheduledChanges.Count + 1).ToString();
            change.Kind = kind;
            change.ItemId = ScenarioInventoryItemCatalog.DefaultItemId();
            change.Quantity = 1;
            change.When = ScenarioAuthoringSchedule.NextTime();
            inventory.ScheduledChanges.Add(change);
            MarkInventoryDirty(session);
            message = "Added timed inventory " + kind.ToString().ToLowerInvariant() + " for " + ScenarioAuthoringSchedule.Format(change.When) + ".";
            return true;
        }

        private static bool AddWeatherEvent(ScenarioEditorSession session, out string message)
        {
            TriggersAndEventsDefinition events = EnsureEvents(session.WorkingDefinition);
            WeatherEventDefinition weather = new WeatherEventDefinition();
            weather.Id = "weather_" + (events.WeatherEvents.Count + 1).ToString();
            weather.WeatherState = "Rain";
            weather.When = ScenarioAuthoringSchedule.NextTime();
            events.WeatherEvents.Add(weather);
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Added weather event for " + ScenarioAuthoringSchedule.Format(weather.When) + ".";
            return true;
        }

        private static bool AddScheduledQuest(ScenarioEditorSession session, out string message)
        {
            QuestAuthoringDefinition quests = EnsureQuests(session.WorkingDefinition);
            QuestDefinition quest = new QuestDefinition();
            QuestDef libraryQuest = FindFirstUnusedCatalogQuest(quests);
            if (libraryQuest != null)
            {
                ApplyLibraryQuest(quest, libraryQuest);
            }
            else
            {
                quest.Id = "quest_" + (quests.Quests.Count + 1).ToString();
                quest.Title = "Scheduled Quest " + (quests.Quests.Count + 1).ToString();
                quest.Description = "Created from the scenario editor. Replace the id with a QuestLibrary id before playtesting.";
            }
            quest.ScheduledStart = ScenarioAuthoringSchedule.NextTime();
            quests.Quests.Add(quest);
            MarkQuestDirty(session);
            message = "Added quest '" + quest.Id + "' for " + ScenarioAuthoringSchedule.Format(quest.ScheduledStart) + ".";
            return true;
        }

        private static bool TryAddCatalogQuest(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            int catalogIndex;
            List<QuestDef> catalog = GetQuestCatalog();
            if (!ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionQuestCatalogAddPrefix, catalog.Count, out catalogIndex))
                return false;

            QuestAuthoringDefinition quests = EnsureQuests(session.WorkingDefinition);
            QuestDefinition quest = new QuestDefinition();
            ApplyLibraryQuest(quest, catalog[catalogIndex]);
            quest.ScheduledStart = ScenarioAuthoringSchedule.NextTime();
            quests.Quests.Add(quest);
            MarkQuestDirty(session);
            message = "Added quest '" + quest.Id + "' from QuestLibrary.";
            return true;
        }

        private static bool CaptureActiveQuests(ScenarioEditorSession session, out string message)
        {
            QuestManager manager = QuestManager.instance;
            if (manager == null)
            {
                message = "QuestManager is not ready; active quest capture skipped.";
                return true;
            }

            QuestAuthoringDefinition quests = EnsureQuests(session.WorkingDefinition);
            quests.Quests.Clear();
            System.Collections.Generic.List<QuestInstance> liveQuests = manager.GetCurrentQuests(true, true, true);
            for (int i = 0; liveQuests != null && i < liveQuests.Count; i++)
            {
                QuestInstance liveQuest = liveQuests[i];
                if (liveQuest == null || liveQuest.definition == null)
                    continue;

                QuestDefinition quest = new QuestDefinition();
                quest.Id = liveQuest.definition.id;
                quest.Title = liveQuest.definition.id;
                quest.Description = liveQuest.descriptionKey;
                quest.ScheduledStart.Day = GameTime.Day;
                quest.ScheduledStart.Hour = GameTime.Hour;
                quest.ScheduledStart.Minute = GameTime.Minute;
                quests.Quests.Add(quest);
            }

            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Captured active quest list: " + quests.Quests.Count + " quest(s).";
            return true;
        }

        private static bool TryHandleFutureSurvivor(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
            int removeIndex;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionFutureSurvivorRemovePrefix, family.FutureSurvivors.Count, out removeIndex))
            {
                family.FutureSurvivors.RemoveAt(removeIndex);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Family, ScenarioEditCategory.Family);
                message = "Removed future survivor.";
                return true;
            }
            int askIndex;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionFutureSurvivorToggleAskPrefix, family.FutureSurvivors.Count, out askIndex))
            {
                family.FutureSurvivors[askIndex].AskToJoin = !family.FutureSurvivors[askIndex].AskToJoin;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Family, ScenarioEditCategory.Family);
                message = "Updated future survivor join mode.";
                return true;
            }
            return TryStepSchedule(actionId, ScenarioAuthoringActionIds.ActionFutureSurvivorDayPrefix, ScenarioAuthoringActionIds.ActionFutureSurvivorHourPrefix, family.FutureSurvivors.Count, delegate(int index) { return family.FutureSurvivors[index].Arrival; }, session, ScenarioDirtySection.Family, ScenarioEditCategory.Family, out message);
        }

        private static bool TryHandleInventoryChange(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            StartingInventoryDefinition inventory = EnsureInventory(session.WorkingDefinition);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionInventoryStartingOverrideToggle, StringComparison.Ordinal))
            {
                inventory.OverrideRandomStart = !inventory.OverrideRandomStart;
                MarkInventoryDirty(session);
                message = inventory.OverrideRandomStart
                    ? "Starting stockpile now overrides random starting items."
                    : "Random starting items are allowed alongside authored stockpile items.";
                return true;
            }

            int index;
            int delta;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionInventoryStartingRemovePrefix, inventory.Items.Count, out index))
            {
                inventory.Items.RemoveAt(index);
                MarkInventoryDirty(session);
                message = "Removed starting stockpile item.";
                return true;
            }

            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionInventoryStartingQuantityPrefix, inventory.Items.Count, out index, out delta))
            {
                ItemEntry entry = inventory.Items[index];
                entry.Quantity = Math.Max(1, entry.Quantity + delta);
                MarkInventoryDirty(session);
                message = "Updated starting stockpile quantity to " + entry.Quantity + ".";
                return true;
            }

            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionInventoryStartingItemPrefix, inventory.Items.Count, out index, out delta))
            {
                ItemEntry entry = inventory.Items[index];
                entry.ItemId = ScenarioInventoryItemCatalog.CycleItemId(entry.ItemId, delta);
                MarkInventoryDirty(session);
                message = "Changed starting stockpile item to '" + entry.ItemId + "'.";
                return true;
            }

            string itemToken;
            if (ScenarioAuthoringActionParser.TryIndexToken(actionId, ScenarioAuthoringActionIds.ActionInventoryStartingItemSelectPrefix, inventory.Items.Count, out index, out itemToken))
            {
                ItemEntry entry = inventory.Items[index];
                entry.ItemId = DecodeToken(itemToken);
                MarkInventoryDirty(session);
                message = "Changed starting stockpile item to '" + entry.ItemId + "'.";
                return true;
            }

            int removeIndex;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionInventoryScheduleDeletePrefix, inventory.ScheduledChanges.Count, out removeIndex))
            {
                inventory.ScheduledChanges.RemoveAt(removeIndex);
                MarkInventoryDirty(session);
                message = "Removed timed inventory change.";
                return true;
            }

            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionInventoryScheduleKindPrefix, inventory.ScheduledChanges.Count, out index))
            {
                TimedInventoryChangeDefinition change = inventory.ScheduledChanges[index];
                change.Kind = change.Kind == ScenarioInventoryChangeKind.Add ? ScenarioInventoryChangeKind.Remove : ScenarioInventoryChangeKind.Add;
                MarkInventoryDirty(session);
                message = "Timed inventory change now " + change.Kind.ToString().ToLowerInvariant() + "s items.";
                return true;
            }

            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionInventoryScheduleQuantityPrefix, inventory.ScheduledChanges.Count, out index, out delta))
            {
                TimedInventoryChangeDefinition change = inventory.ScheduledChanges[index];
                change.Quantity = Math.Max(1, change.Quantity + delta);
                MarkInventoryDirty(session);
                message = "Updated timed inventory quantity to " + change.Quantity + ".";
                return true;
            }

            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionInventoryScheduleItemPrefix, inventory.ScheduledChanges.Count, out index, out delta))
            {
                TimedInventoryChangeDefinition change = inventory.ScheduledChanges[index];
                change.ItemId = ScenarioInventoryItemCatalog.CycleItemId(change.ItemId, delta);
                MarkInventoryDirty(session);
                message = "Changed timed inventory item to '" + change.ItemId + "'.";
                return true;
            }

            if (ScenarioAuthoringActionParser.TryIndexToken(actionId, ScenarioAuthoringActionIds.ActionInventoryScheduleItemSelectPrefix, inventory.ScheduledChanges.Count, out index, out itemToken))
            {
                TimedInventoryChangeDefinition change = inventory.ScheduledChanges[index];
                change.ItemId = DecodeToken(itemToken);
                MarkInventoryDirty(session);
                message = "Changed timed inventory item to '" + change.ItemId + "'.";
                return true;
            }

            if (TryStepSchedule(actionId, ScenarioAuthoringActionIds.ActionInventoryScheduleDayPrefix, ScenarioAuthoringActionIds.ActionInventoryScheduleHourPrefix, inventory.ScheduledChanges.Count, delegate(int itemIndex) { return inventory.ScheduledChanges[itemIndex].When; }, session, ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory, out message))
                return true;

            return TryStepScheduleMinute(actionId, ScenarioAuthoringActionIds.ActionInventoryScheduleMinutePrefix, inventory.ScheduledChanges.Count, delegate(int itemIndex) { return inventory.ScheduledChanges[itemIndex].When; }, session, ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory, out message);
        }

        private static bool TryHandleWeatherEvent(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            TriggersAndEventsDefinition events = EnsureEvents(session.WorkingDefinition);
            int removeIndex;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionWeatherScheduleDeletePrefix, events.WeatherEvents.Count, out removeIndex))
            {
                events.WeatherEvents.RemoveAt(removeIndex);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed weather event.";
                return true;
            }

            int index;
            string token;
            if (ScenarioAuthoringActionParser.TryIndexToken(actionId, ScenarioAuthoringActionIds.ActionWeatherScheduleStatePrefix, events.WeatherEvents.Count, out index, out token))
            {
                events.WeatherEvents[index].WeatherState = DecodeToken(token);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Weather event state set to " + events.WeatherEvents[index].WeatherState + ".";
                return true;
            }

            int delta;
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionWeatherScheduleDurationPrefix, events.WeatherEvents.Count, out index, out delta))
            {
                events.WeatherEvents[index].DurationHours = Math.Max(0, events.WeatherEvents[index].DurationHours + delta);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Weather duration set to " + events.WeatherEvents[index].DurationHours + " hour(s).";
                return true;
            }

            if (TryStepSchedule(actionId, ScenarioAuthoringActionIds.ActionWeatherScheduleDayPrefix, ScenarioAuthoringActionIds.ActionWeatherScheduleHourPrefix, events.WeatherEvents.Count, delegate(int itemIndex) { return events.WeatherEvents[itemIndex].When; }, session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers, out message))
                return true;

            return TryStepScheduleMinute(actionId, ScenarioAuthoringActionIds.ActionWeatherScheduleMinutePrefix, events.WeatherEvents.Count, delegate(int itemIndex) { return events.WeatherEvents[itemIndex].When; }, session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers, out message);
        }

        private static bool TryHandleQuest(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            QuestAuthoringDefinition quests = EnsureQuests(session.WorkingDefinition);
            int removeIndex;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionQuestScheduleDeletePrefix, quests.Quests.Count, out removeIndex))
            {
                quests.Quests.RemoveAt(removeIndex);
                MarkQuestDirty(session);
                message = "Removed quest.";
                return true;
            }

            int index;
            int delta;
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionQuestMovePrefix, quests.Quests.Count, out index, out delta))
            {
                int target = index + delta;
                if (target < 0 || target >= quests.Quests.Count)
                {
                    message = "Quest is already at the edge of the schedule.";
                    return true;
                }
                QuestDefinition moving = quests.Quests[index];
                quests.Quests.RemoveAt(index);
                quests.Quests.Insert(target, moving);
                MarkQuestDirty(session);
                message = "Moved quest.";
                return true;
            }

            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionQuestDuplicatePrefix, quests.Quests.Count, out index))
            {
                QuestDefinition copy = CopyQuest(quests.Quests[index], quests.Quests.Count + 1);
                quests.Quests.Insert(index + 1, copy);
                MarkQuestDirty(session);
                message = "Duplicated quest '" + copy.Id + "'.";
                return true;
            }

            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionQuestIdCyclePrefix, quests.Quests.Count, out index, out delta))
                return CycleQuestId(session, quests.Quests[index], delta, out message);

            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionQuestStartModePrefix, quests.Quests.Count, out index))
                return ToggleQuestStartMode(session, quests.Quests[index], session.WorkingDefinition, out message);

            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionQuestTriggerCyclePrefix, quests.Quests.Count, out index, out delta))
                return CycleQuestTrigger(session, quests.Quests[index], session.WorkingDefinition, delta, out message);

            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionQuestCompletionCyclePrefix, quests.Quests.Count, out index, out delta))
                return CycleQuestCompletion(session, quests.Quests[index], session.WorkingDefinition, delta, out message);

            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionQuestTitleSyncPrefix, quests.Quests.Count, out index))
                return SyncQuestTitle(session, quests.Quests[index], out message);

            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionQuestDescriptionSyncPrefix, quests.Quests.Count, out index))
                return SyncQuestDescription(session, quests.Quests[index], out message);

            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionQuestSpawnNowPrefix, quests.Quests.Count, out index))
                return SpawnQuestNow(quests.Quests[index], out message);

            if (TryStepSchedule(actionId, ScenarioAuthoringActionIds.ActionQuestScheduleDayPrefix, ScenarioAuthoringActionIds.ActionQuestScheduleHourPrefix, quests.Quests.Count, delegate(int itemIndex) { return quests.Quests[itemIndex].ScheduledStart; }, session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers, out message))
                return true;

            return TryStepScheduleMinutes(actionId, quests.Quests.Count, delegate(int itemIndex) { return quests.Quests[itemIndex].ScheduledStart; }, session, out message);
        }

        private static bool TryStepSchedule(string actionId, string dayPrefix, string hourPrefix, int count, ScheduleGetter getter, ScenarioEditorSession session, ScenarioDirtySection section, ScenarioEditCategory category, out string message)
        {
            message = null;
            int index;
            int delta;
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, dayPrefix, count, out index, out delta))
            {
                ScenarioScheduleTime time = getter(index);
                if (time == null)
                {
                    message = "This item is trigger-started and has no schedule to edit.";
                    return false;
                }
                time.Day = Math.Max(1, time.Day + delta);
                ScenarioAuthoringMutation.MarkDirty(session, section, category);
                message = "Updated scheduled day to " + time.Day + ".";
                return true;
            }
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, hourPrefix, count, out index, out delta))
            {
                ScenarioScheduleTime time = getter(index);
                if (time == null)
                {
                    message = "This item is trigger-started and has no schedule to edit.";
                    return false;
                }
                time.Hour = ScenarioAuthoringSchedule.Clamp(time.Hour + delta, 0, 23);
                ScenarioAuthoringMutation.MarkDirty(session, section, category);
                message = "Updated scheduled hour to " + time.Hour + ".";
                return true;
            }
            return false;
        }

        private static bool TryStepScheduleMinutes(string actionId, int count, ScheduleGetter getter, ScenarioEditorSession session, out string message)
        {
            message = null;
            int index;
            int delta;
            if (!ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionQuestScheduleMinutePrefix, count, out index, out delta))
                return false;

            ScenarioScheduleTime time = getter(index);
            if (time == null)
            {
                message = "This quest is trigger-started and has no schedule to edit.";
                return false;
            }
            time.Minute = ScenarioAuthoringSchedule.Clamp(time.Minute + delta, 0, 59);
            MarkQuestDirty(session);
            message = "Updated scheduled minute to " + time.Minute + ".";
            return true;
        }

        private static bool TryStepScheduleMinute(string actionId, string minutePrefix, int count, ScheduleGetter getter, ScenarioEditorSession session, ScenarioDirtySection section, ScenarioEditCategory category, out string message)
        {
            message = null;
            int index;
            int delta;
            if (!ScenarioAuthoringActionParser.TrySignedIndex(actionId, minutePrefix, count, out index, out delta))
                return false;

            ScenarioScheduleTime time = getter(index);
            if (time == null)
            {
                message = "This item has no schedule to edit.";
                return false;
            }
            time.Minute = ScenarioAuthoringSchedule.Clamp(time.Minute + delta, 0, 59);
            ScenarioAuthoringMutation.MarkDirty(session, section, category);
            message = "Updated scheduled minute to " + time.Minute + ".";
            return true;
        }

        private static string DecodeToken(string token)
        {
            return string.IsNullOrEmpty(token) ? string.Empty : Uri.UnescapeDataString(token);
        }

        private static bool CycleQuestId(ScenarioEditorSession session, QuestDefinition quest, int delta, out string message)
        {
            message = null;
            if (quest == null)
                return true;

            List<QuestDef> catalog = GetQuestCatalog();
            if (catalog.Count == 0)
            {
                message = "QuestLibrary is not ready; quest id cannot be cycled.";
                return true;
            }

            int current = IndexOfQuest(catalog, quest.Id);
            int next = current < 0 ? 0 : Wrap(current + delta, catalog.Count);
            ApplyLibraryQuest(quest, catalog[next]);
            MarkQuestDirty(session);
            message = "Quest id changed to '" + quest.Id + "'.";
            return true;
        }

        private static bool ToggleQuestStartMode(ScenarioEditorSession session, QuestDefinition quest, ScenarioDefinition definition, out string message)
        {
            message = null;
            if (quest == null)
                return true;

            if (string.IsNullOrEmpty(quest.StartTriggerId))
            {
                quest.ScheduledStart = null;
                quest.StartTriggerId = EnsureFirstTriggerId(definition);
                message = "Quest now starts from trigger '" + quest.StartTriggerId + "'.";
            }
            else
            {
                quest.StartTriggerId = null;
                if (quest.ScheduledStart == null)
                    quest.ScheduledStart = ScenarioAuthoringSchedule.NextTime();
                message = "Quest now starts from its schedule.";
            }

            MarkQuestDirty(session);
            return true;
        }

        private static bool CycleQuestTrigger(ScenarioEditorSession session, QuestDefinition quest, ScenarioDefinition definition, int delta, out string message)
        {
            message = null;
            if (quest == null)
                return true;

            List<string> ids = GetTriggerIds(definition);
            if (ids.Count == 0)
                ids.Add(EnsureFirstTriggerId(definition));

            int current = IndexOf(ids, quest.StartTriggerId);
            int next = current < 0 ? 0 : Wrap(current + delta, ids.Count);
            quest.StartTriggerId = ids[next];
            quest.ScheduledStart = null;
            MarkQuestDirty(session);
            message = "Quest trigger set to '" + quest.StartTriggerId + "'.";
            return true;
        }

        private static bool CycleQuestCompletion(ScenarioEditorSession session, QuestDefinition quest, ScenarioDefinition definition, int delta, out string message)
        {
            message = null;
            if (quest == null)
                return true;

            List<string> ids = GetConditionIds(definition);
            ids.Insert(0, string.Empty);
            int current = IndexOf(ids, quest.CompletionConditionId ?? string.Empty);
            int next = current < 0 ? 0 : Wrap(current + delta, ids.Count);
            quest.CompletionConditionId = string.IsNullOrEmpty(ids[next]) ? null : ids[next];
            MarkQuestDirty(session);
            message = string.IsNullOrEmpty(quest.CompletionConditionId)
                ? "Quest completion condition cleared."
                : "Quest completion condition set to '" + quest.CompletionConditionId + "'.";
            return true;
        }

        private static bool SyncQuestTitle(ScenarioEditorSession session, QuestDefinition quest, out string message)
        {
            QuestDef def = FindQuestDef(quest != null ? quest.Id : null);
            if (quest == null || def == null)
            {
                message = "QuestLibrary definition was not found for this quest id.";
                return true;
            }

            quest.Title = BuildQuestTitle(def);
            MarkQuestDirty(session);
            message = "Quest title synced from QuestLibrary.";
            return true;
        }

        private static bool SyncQuestDescription(ScenarioEditorSession session, QuestDefinition quest, out string message)
        {
            QuestDef def = FindQuestDef(quest != null ? quest.Id : null);
            if (quest == null || def == null)
            {
                message = "QuestLibrary definition was not found for this quest id.";
                return true;
            }

            quest.Description = !string.IsNullOrEmpty(def.descriptionKey) ? def.descriptionKey : "QuestLibrary entry " + def.id;
            MarkQuestDirty(session);
            message = "Quest description synced from QuestLibrary.";
            return true;
        }

        private static bool SpawnQuestNow(QuestDefinition quest, out string message)
        {
            if (quest == null || string.IsNullOrEmpty(quest.Id))
            {
                message = "Quest id is missing.";
                return true;
            }

            if (QuestManager.instance == null)
            {
                message = "QuestManager is not ready; quest was not spawned.";
                return true;
            }

            bool spawned = QuestManager.instance.SpawnQuestWithId(quest.Id);
            message = spawned
                ? "Spawned quest '" + quest.Id + "' for preview."
                : "QuestManager rejected quest '" + quest.Id + "'. Check availability, max active quests, and QuestLibrary id.";
            return true;
        }

        private static QuestDefinition CopyQuest(QuestDefinition source, int fallbackIndex)
        {
            QuestDefinition copy = new QuestDefinition();
            if (source == null)
                return copy;

            copy.Id = source.Id;
            copy.Title = string.IsNullOrEmpty(source.Title) ? source.Id : source.Title + " Copy";
            copy.Description = source.Description;
            copy.StartTriggerId = source.StartTriggerId;
            copy.CompletionConditionId = source.CompletionConditionId;
            copy.ScheduledStart = source.ScheduledStart != null
                ? new ScenarioScheduleTime { Day = source.ScheduledStart.Day, Hour = source.ScheduledStart.Hour, Minute = source.ScheduledStart.Minute }
                : null;
            for (int i = 0; source.Properties != null && i < source.Properties.Count; i++)
            {
                ScenarioProperty property = source.Properties[i];
                if (property != null)
                    copy.Properties.Add(new ScenarioProperty { Key = property.Key, Value = property.Value });
            }
            if (string.IsNullOrEmpty(copy.Title))
                copy.Title = "Quest " + fallbackIndex.ToString();
            return copy;
        }

        private static void ApplyLibraryQuest(QuestDefinition quest, QuestDef def)
        {
            if (quest == null || def == null)
                return;

            quest.Id = def.id;
            quest.Title = BuildQuestTitle(def);
            quest.Description = !string.IsNullOrEmpty(def.descriptionKey) ? def.descriptionKey : "QuestLibrary entry " + def.id;
        }

        private static string BuildQuestTitle(QuestDef def)
        {
            if (def == null)
                return string.Empty;
            return !string.IsNullOrEmpty(def.nameKey) ? def.nameKey : def.id;
        }

        private static QuestDef FindFirstUnusedCatalogQuest(QuestAuthoringDefinition authored)
        {
            List<QuestDef> catalog = GetQuestCatalog();
            for (int i = 0; i < catalog.Count; i++)
            {
                bool used = false;
                for (int j = 0; authored != null && authored.Quests != null && j < authored.Quests.Count; j++)
                {
                    if (authored.Quests[j] != null && string.Equals(authored.Quests[j].Id, catalog[i].id, StringComparison.OrdinalIgnoreCase))
                    {
                        used = true;
                        break;
                    }
                }
                if (!used)
                    return catalog[i];
            }
            return catalog.Count > 0 ? catalog[0] : null;
        }

        private static QuestDef FindQuestDef(string id)
        {
            if (string.IsNullOrEmpty(id) || QuestLibrary.instance == null)
                return null;
            return QuestLibrary.instance.FindQuestDefinition(id);
        }

        private static List<QuestDef> GetQuestCatalog()
        {
            List<QuestDef> result = new List<QuestDef>();
            if (QuestLibrary.instance == null)
                return result;

            List<QuestDef> all = QuestLibrary.instance.GetAllQuests();
            for (int i = 0; all != null && i < all.Count; i++)
            {
                QuestDef quest = all[i];
                if (quest != null && !string.IsNullOrEmpty(quest.id))
                    result.Add(quest);
            }
            result.Sort(delegate(QuestDef left, QuestDef right)
            {
                return string.Compare(left != null ? left.id : null, right != null ? right.id : null, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        private static int IndexOfQuest(List<QuestDef> catalog, string id)
        {
            for (int i = 0; catalog != null && i < catalog.Count; i++)
                if (catalog[i] != null && string.Equals(catalog[i].id, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static string EnsureFirstTriggerId(ScenarioDefinition definition)
        {
            TriggersAndEventsDefinition events = EnsureEvents(definition);
            if (events.Triggers.Count == 0)
            {
                TriggerDef trigger = new TriggerDef();
                trigger.Id = "quest_trigger_1";
                trigger.Type = "manual";
                events.Triggers.Add(trigger);
            }

            TriggerDef first = events.Triggers[0];
            if (string.IsNullOrEmpty(first.Id))
                first.Id = "quest_trigger_1";
            return first.Id;
        }

        private static List<string> GetTriggerIds(ScenarioDefinition definition)
        {
            List<string> ids = new List<string>();
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                if (trigger != null && !string.IsNullOrEmpty(trigger.Id))
                    ids.Add(trigger.Id);
            }
            return ids;
        }

        private static List<string> GetConditionIds(ScenarioDefinition definition)
        {
            List<string> ids = new List<string>();
            AddConditionIds(ids, definition != null && definition.WinLossConditions != null ? definition.WinLossConditions.WinConditions : null);
            AddConditionIds(ids, definition != null && definition.WinLossConditions != null ? definition.WinLossConditions.LossConditions : null);
            return ids;
        }

        private static void AddConditionIds(List<string> ids, List<ConditionDef> conditions)
        {
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                ConditionDef condition = conditions[i];
                if (condition != null && !string.IsNullOrEmpty(condition.Id))
                    ids.Add(condition.Id);
            }
        }

        private static int IndexOf(List<string> values, string value)
        {
            for (int i = 0; values != null && i < values.Count; i++)
                if (string.Equals(values[i] ?? string.Empty, value ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static int Wrap(int value, int count)
        {
            if (count <= 0)
                return 0;
            while (value < 0)
                value += count;
            while (value >= count)
                value -= count;
            return value;
        }

        private static void MarkQuestDirty(ScenarioEditorSession session)
        {
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
        }

        private static void MarkInventoryDirty(ScenarioEditorSession session)
        {
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);
        }

        private static FamilySetupDefinition EnsureFamily(ScenarioDefinition definition)
        {
            if (definition.FamilySetup == null)
                definition.FamilySetup = new FamilySetupDefinition();
            return definition.FamilySetup;
        }

        private static StartingInventoryDefinition EnsureInventory(ScenarioDefinition definition)
        {
            if (definition.StartingInventory == null)
                definition.StartingInventory = new StartingInventoryDefinition();
            return definition.StartingInventory;
        }

        private static TriggersAndEventsDefinition EnsureEvents(ScenarioDefinition definition)
        {
            if (definition.TriggersAndEvents == null)
                definition.TriggersAndEvents = new TriggersAndEventsDefinition();
            return definition.TriggersAndEvents;
        }

        private static QuestAuthoringDefinition EnsureQuests(ScenarioDefinition definition)
        {
            if (definition.Quests == null)
                definition.Quests = new QuestAuthoringDefinition();
            return definition.Quests;
        }

        private delegate ScenarioScheduleTime ScheduleGetter(int index);
    }
}
