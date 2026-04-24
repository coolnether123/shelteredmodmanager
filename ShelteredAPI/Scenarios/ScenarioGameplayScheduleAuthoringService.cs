using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioGameplayScheduleAuthoringService
    {
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
            if (TryHandleQuest(session, actionId, out message))
                return true;

            return false;
        }

        private static bool AddFutureSurvivor(ScenarioEditorSession session, out string message)
        {
            FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
            FutureSurvivorDefinition survivor = new FutureSurvivorDefinition();
            survivor.Id = "future_survivor_" + (family.FutureSurvivors.Count + 1).ToString();
            survivor.Arrival = NextScheduleTime();
            survivor.Survivor.Name = "New Survivor " + (family.FutureSurvivors.Count + 1).ToString();
            survivor.Survivor.Gender = ScenarioGender.Any;
            family.FutureSurvivors.Add(survivor);
            MarkDirty(session, ScenarioDirtySection.Family, ScenarioEditCategory.Family);
            message = "Added future survivor arrival for " + FormatTime(survivor.Arrival) + ".";
            return true;
        }

        private static bool AddInventoryChange(ScenarioEditorSession session, ScenarioInventoryChangeKind kind, out string message)
        {
            StartingInventoryDefinition inventory = EnsureInventory(session.WorkingDefinition);
            TimedInventoryChangeDefinition change = new TimedInventoryChangeDefinition();
            change.Id = "inventory_" + kind.ToString().ToLowerInvariant() + "_" + (inventory.ScheduledChanges.Count + 1).ToString();
            change.Kind = kind;
            change.ItemId = "Food";
            change.Quantity = 1;
            change.When = NextScheduleTime();
            inventory.ScheduledChanges.Add(change);
            MarkDirty(session, ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);
            message = "Added timed inventory " + kind.ToString().ToLowerInvariant() + " for " + FormatTime(change.When) + ".";
            return true;
        }

        private static bool AddWeatherEvent(ScenarioEditorSession session, out string message)
        {
            TriggersAndEventsDefinition events = EnsureEvents(session.WorkingDefinition);
            WeatherEventDefinition weather = new WeatherEventDefinition();
            weather.Id = "weather_" + (events.WeatherEvents.Count + 1).ToString();
            weather.WeatherState = "Rain";
            weather.When = NextScheduleTime();
            events.WeatherEvents.Add(weather);
            MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Added weather event for " + FormatTime(weather.When) + ".";
            return true;
        }

        private static bool AddScheduledQuest(ScenarioEditorSession session, out string message)
        {
            QuestAuthoringDefinition quests = EnsureQuests(session.WorkingDefinition);
            QuestDefinition quest = new QuestDefinition();
            quest.Id = "quest_" + (quests.Quests.Count + 1).ToString();
            quest.Title = "Scheduled Quest " + (quests.Quests.Count + 1).ToString();
            quest.Description = "Created from the scenario editor.";
            quest.ScheduledStart = NextScheduleTime();
            quests.Quests.Add(quest);
            MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Added scheduled quest for " + FormatTime(quest.ScheduledStart) + ".";
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

            MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Captured active quest list: " + quests.Quests.Count + " quest(s).";
            return true;
        }

        private static bool TryHandleFutureSurvivor(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
            int removeIndex;
            if (TryRemove(actionId, ScenarioAuthoringActionIds.ActionFutureSurvivorRemovePrefix, family.FutureSurvivors.Count, out removeIndex))
            {
                family.FutureSurvivors.RemoveAt(removeIndex);
                MarkDirty(session, ScenarioDirtySection.Family, ScenarioEditCategory.Family);
                message = "Removed future survivor.";
                return true;
            }
            int askIndex;
            if (TryIndex(actionId, ScenarioAuthoringActionIds.ActionFutureSurvivorToggleAskPrefix, family.FutureSurvivors.Count, out askIndex))
            {
                family.FutureSurvivors[askIndex].AskToJoin = !family.FutureSurvivors[askIndex].AskToJoin;
                MarkDirty(session, ScenarioDirtySection.Family, ScenarioEditCategory.Family);
                message = "Updated future survivor join mode.";
                return true;
            }
            return TryStepSchedule(actionId, ScenarioAuthoringActionIds.ActionFutureSurvivorDayPrefix, ScenarioAuthoringActionIds.ActionFutureSurvivorHourPrefix, family.FutureSurvivors.Count, delegate(int index) { return family.FutureSurvivors[index].Arrival; }, session, ScenarioDirtySection.Family, ScenarioEditCategory.Family, out message);
        }

        private static bool TryHandleInventoryChange(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            StartingInventoryDefinition inventory = EnsureInventory(session.WorkingDefinition);
            int removeIndex;
            if (TryRemove(actionId, ScenarioAuthoringActionIds.ActionInventoryScheduleDeletePrefix, inventory.ScheduledChanges.Count, out removeIndex))
            {
                inventory.ScheduledChanges.RemoveAt(removeIndex);
                MarkDirty(session, ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);
                message = "Removed timed inventory change.";
                return true;
            }
            return TryStepSchedule(actionId, ScenarioAuthoringActionIds.ActionInventoryScheduleDayPrefix, ScenarioAuthoringActionIds.ActionInventoryScheduleHourPrefix, inventory.ScheduledChanges.Count, delegate(int index) { return inventory.ScheduledChanges[index].When; }, session, ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory, out message);
        }

        private static bool TryHandleWeatherEvent(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            TriggersAndEventsDefinition events = EnsureEvents(session.WorkingDefinition);
            int removeIndex;
            if (TryRemove(actionId, ScenarioAuthoringActionIds.ActionWeatherScheduleDeletePrefix, events.WeatherEvents.Count, out removeIndex))
            {
                events.WeatherEvents.RemoveAt(removeIndex);
                MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed weather event.";
                return true;
            }
            return TryStepSchedule(actionId, ScenarioAuthoringActionIds.ActionWeatherScheduleDayPrefix, ScenarioAuthoringActionIds.ActionWeatherScheduleHourPrefix, events.WeatherEvents.Count, delegate(int index) { return events.WeatherEvents[index].When; }, session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers, out message);
        }

        private static bool TryHandleQuest(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            QuestAuthoringDefinition quests = EnsureQuests(session.WorkingDefinition);
            int removeIndex;
            if (TryRemove(actionId, ScenarioAuthoringActionIds.ActionQuestScheduleDeletePrefix, quests.Quests.Count, out removeIndex))
            {
                quests.Quests.RemoveAt(removeIndex);
                MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed quest.";
                return true;
            }
            return TryStepSchedule(actionId, ScenarioAuthoringActionIds.ActionQuestScheduleDayPrefix, ScenarioAuthoringActionIds.ActionQuestScheduleHourPrefix, quests.Quests.Count, delegate(int index) { return quests.Quests[index].ScheduledStart; }, session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers, out message);
        }

        private static bool TryStepSchedule(string actionId, string dayPrefix, string hourPrefix, int count, ScheduleGetter getter, ScenarioEditorSession session, ScenarioDirtySection section, ScenarioEditCategory category, out string message)
        {
            message = null;
            int index;
            int delta;
            if (TrySignedIndex(actionId, dayPrefix, count, out index, out delta))
            {
                ScenarioScheduleTime time = getter(index);
                time.Day = Math.Max(1, time.Day + delta);
                MarkDirty(session, section, category);
                message = "Updated scheduled day to " + time.Day + ".";
                return true;
            }
            if (TrySignedIndex(actionId, hourPrefix, count, out index, out delta))
            {
                ScenarioScheduleTime time = getter(index);
                time.Hour = Clamp(time.Hour + delta, 0, 23);
                MarkDirty(session, section, category);
                message = "Updated scheduled hour to " + time.Hour + ".";
                return true;
            }
            return false;
        }

        private static ScenarioScheduleTime NextScheduleTime()
        {
            ScenarioScheduleTime time = new ScenarioScheduleTime();
            try
            {
                time.Day = Math.Max(1, GameTime.Day + 1);
                time.Hour = Clamp(GameTime.Hour, 0, 23);
                time.Minute = Clamp(GameTime.Minute, 0, 59);
            }
            catch
            {
                time.Day = 2;
                time.Hour = 8;
                time.Minute = 0;
            }
            return time;
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

        private static void MarkDirty(ScenarioEditorSession session, ScenarioDirtySection section, ScenarioEditCategory category)
        {
            if (!session.DirtyFlags.Contains(section))
                session.DirtyFlags.Add(section);
            session.CurrentEditCategory = category;
            session.HasAppliedToCurrentWorld = true;
        }

        private static bool TryRemove(string actionId, string prefix, int count, out int index)
        {
            return TryIndex(actionId, prefix, count, out index);
        }

        private static bool TryIndex(string actionId, string prefix, int count, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            if (!int.TryParse(actionId.Substring(prefix.Length), out index))
                return false;
            return index >= 0 && index < count;
        }

        private static bool TrySignedIndex(string actionId, string prefix, int count, out int index, out int delta)
        {
            index = -1;
            delta = 0;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            string[] parts = actionId.Substring(prefix.Length).Split('.');
            if (parts.Length != 2 || !int.TryParse(parts[0], out index) || !int.TryParse(parts[1], out delta))
                return false;
            return index >= 0 && index < count && delta != 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private static string FormatTime(ScenarioScheduleTime time)
        {
            if (time == null)
                return "unscheduled";
            return "day " + time.Day + " at " + time.Hour.ToString("D2") + ":" + time.Minute.ToString("D2");
        }

        private delegate ScenarioScheduleTime ScheduleGetter(int index);
    }
}
