using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using ShelteredAPI.Saves;
using ShelteredScenarioEditor.Application.Authoring.Supplies;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Application.Commands;
namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioGameplayScheduleAuthoringService
    {
        private readonly ScenarioEditorActorReferenceService _actorResolver;
        private readonly ScenarioAuthoringInventoryProjectionService _inventoryProjectionService;
        private readonly ScenarioAuthoringHistoryService _historyService;

        public ScenarioGameplayScheduleAuthoringService(
            ScenarioEditorActorReferenceService actorResolver,
            ScenarioAuthoringInventoryProjectionService inventoryProjectionService,
            ScenarioAuthoringHistoryService historyService)
        {
            _actorResolver = actorResolver;
            _inventoryProjectionService = inventoryProjectionService;
            _historyService = historyService;
        }

        public bool TryHandleCommand(ScenarioEditorSession session, GameplayScheduleCommand command, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }
            string reason = null;
            if (command == null || !command.ValidateStructure(out reason))
            {
                message = reason ?? "Gameplay schedule command is invalid.";
                return false;
            }

            switch (command.Kind)
            {
                case GameplayScheduleCommandKind.AddFutureSurvivor: return AddFutureSurvivor(session, out message);
                case GameplayScheduleCommandKind.AddStartingItem:
                    return FinishStartingInventoryMutation(session, AddStartingInventoryItem(session, out message), "add starting item", ref message);
                case GameplayScheduleCommandKind.ApplySuppliesPreset:
                {
                    bool changed = ApplyStarterPreset(session, command.Index, out message);
                    if (changed) FinishStartingInventoryMutation(session, true, "apply starter loadout", ref message);
                    return changed;
                }
                case GameplayScheduleCommandKind.MergeStartingDuplicates:
                {
                    bool changed = MergeStartingInventoryDuplicates(session, out message);
                    if (changed) FinishStartingInventoryMutation(session, true, "merge duplicate starting items", ref message);
                    return changed;
                }
                case GameplayScheduleCommandKind.AddTimedItem:
                    return AddInventoryChange(session, command.Remove ? ScenarioInventoryChangeKind.Remove : ScenarioInventoryChangeKind.Add, out message);
                case GameplayScheduleCommandKind.AddWeather: return AddWeatherEvent(session, out message);
                case GameplayScheduleCommandKind.CaptureActiveQuests: return CaptureActiveQuests(session, out message);
                case GameplayScheduleCommandKind.AddQuest: return AddScheduledQuest(session, out message);
                case GameplayScheduleCommandKind.AddCatalogQuest: return AddCatalogQuest(session, command.Index, out message);
            }

            if (IsFutureCommand(command.Kind))
                return HandleFutureCommand(session, command, out message);
            if (IsStartingInventoryCommand(command.Kind) || IsTimedInventoryCommand(command.Kind))
            {
                bool changed = HandleInventoryCommand(session, command, out message);
                if (changed && IsStartingInventoryCommand(command.Kind))
                    FinishStartingInventoryMutation(session, true, "edit starting item", ref message);
                return changed;
            }
            if (IsWeatherCommand(command.Kind))
                return HandleWeatherCommand(session, command, out message);
            if (IsQuestCommand(command.Kind))
                return HandleQuestCommand(session, command, out message);
            return false;
        }

        private static bool HandleFutureCommand(ScenarioEditorSession session, GameplayScheduleCommand command, out string message)
        {
            FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
            if (!ValidIndex(command.Index, family.FutureSurvivors.Count, "Future survivor", out message)) return false;
            FutureSurvivorDefinition survivor = family.FutureSurvivors[command.Index];
            switch (command.Kind)
            {
                case GameplayScheduleCommandKind.RemoveFutureSurvivor:
                    family.FutureSurvivors.RemoveAt(command.Index);
                    message = "Removed future survivor.";
                    break;
                case GameplayScheduleCommandKind.ToggleFutureAsk:
                    survivor.AskToJoin = !survivor.AskToJoin;
                    message = "Updated future survivor join mode.";
                    break;
                case GameplayScheduleCommandKind.StepFutureDay:
                    survivor.Arrival.Day = Math.Max(1, survivor.Arrival.Day + command.Delta);
                    message = "Updated scheduled day to " + survivor.Arrival.Day + ".";
                    break;
                case GameplayScheduleCommandKind.StepFutureHour:
                    survivor.Arrival.Hour = ScenarioAuthoringSchedule.Clamp(survivor.Arrival.Hour + command.Delta, 0, 23);
                    message = "Updated scheduled hour to " + survivor.Arrival.Hour + ".";
                    break;
                default: return false;
            }
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Family, ScenarioEditCategory.Family);
            return true;
        }

        private static bool HandleInventoryCommand(ScenarioEditorSession session, GameplayScheduleCommand command, out string message)
        {
            StartingInventoryDefinition inventory = EnsureInventory(session.WorkingDefinition);
            message = null;
            if (command.Kind == GameplayScheduleCommandKind.ToggleStartingOverride)
            {
                inventory.OverrideRandomStart = !inventory.OverrideRandomStart;
                MarkInventoryDirty(session);
                message = inventory.OverrideRandomStart ? "Vanilla random-start item pools will be suppressed when this scenario applies." : "Vanilla random-start item pools are allowed when this scenario applies.";
                return true;
            }
            if (IsStartingInventoryCommand(command.Kind))
            {
                if (!ValidIndex(command.Index, inventory.Items.Count, "Starting item", out message)) return false;
                ItemEntry entry = inventory.Items[command.Index];
                switch (command.Kind)
                {
                    case GameplayScheduleCommandKind.RemoveStartingItem: inventory.Items.RemoveAt(command.Index); message = "Removed shelter storage item."; break;
                    case GameplayScheduleCommandKind.StepStartingQuantity: entry.Quantity = Math.Max(1, entry.Quantity + command.Delta); message = "Updated shelter storage quantity to " + entry.Quantity + "."; break;
                    case GameplayScheduleCommandKind.CycleStartingItem: entry.ItemId = ScenarioInventoryItemCatalog.CycleItemId(entry.ItemId, command.Delta); message = "Changed shelter storage item to '" + entry.ItemId + "'."; break;
                    case GameplayScheduleCommandKind.SetStartingItem: entry.ItemId = command.Value; message = "Changed shelter storage item to '" + entry.ItemId + "'."; break;
                    default: return false;
                }
                MarkInventoryDirty(session);
                return true;
            }

            if (!ValidIndex(command.Index, inventory.ScheduledChanges.Count, "Timed inventory change", out message)) return false;
            TimedInventoryChangeDefinition change = inventory.ScheduledChanges[command.Index];
            switch (command.Kind)
            {
                case GameplayScheduleCommandKind.DeleteTimedItem: inventory.ScheduledChanges.RemoveAt(command.Index); message = "Removed timed inventory change."; break;
                case GameplayScheduleCommandKind.ToggleTimedKind: change.Kind = change.Kind == ScenarioInventoryChangeKind.Add ? ScenarioInventoryChangeKind.Remove : ScenarioInventoryChangeKind.Add; message = "Timed inventory change now " + change.Kind.ToString().ToLowerInvariant() + "s items."; break;
                case GameplayScheduleCommandKind.StepTimedQuantity: change.Quantity = Math.Max(1, change.Quantity + command.Delta); message = "Updated timed inventory quantity to " + change.Quantity + "."; break;
                case GameplayScheduleCommandKind.CycleTimedItem: change.ItemId = ScenarioInventoryItemCatalog.CycleItemId(change.ItemId, command.Delta); message = "Changed timed inventory item to '" + change.ItemId + "'."; break;
                case GameplayScheduleCommandKind.SetTimedItem: change.ItemId = command.Value; message = "Changed timed inventory item to '" + change.ItemId + "'."; break;
                case GameplayScheduleCommandKind.StepTimedDay: change.When.Day = Math.Max(1, change.When.Day + command.Delta); message = "Updated scheduled day to " + change.When.Day + "."; break;
                case GameplayScheduleCommandKind.StepTimedHour: change.When.Hour = ScenarioAuthoringSchedule.Clamp(change.When.Hour + command.Delta, 0, 23); message = "Updated scheduled hour to " + change.When.Hour + "."; break;
                case GameplayScheduleCommandKind.StepTimedMinute: change.When.Minute = ScenarioAuthoringSchedule.Clamp(change.When.Minute + command.Delta, 0, 59); message = "Updated scheduled minute to " + change.When.Minute + "."; break;
                default: return false;
            }
            MarkInventoryDirty(session);
            return true;
        }

        private static bool HandleWeatherCommand(ScenarioEditorSession session, GameplayScheduleCommand command, out string message)
        {
            TriggersAndEventsDefinition events = EnsureEvents(session.WorkingDefinition);
            if (!ValidIndex(command.Index, events.WeatherEvents.Count, "Weather event", out message)) return false;
            WeatherEventDefinition weather = events.WeatherEvents[command.Index];
            switch (command.Kind)
            {
                case GameplayScheduleCommandKind.DeleteWeather: events.WeatherEvents.RemoveAt(command.Index); message = "Removed weather event."; break;
                case GameplayScheduleCommandKind.SetWeatherState: weather.WeatherState = command.Value; message = "Weather event state set to " + weather.WeatherState + "."; break;
                case GameplayScheduleCommandKind.StepWeatherDuration: weather.DurationHours = Math.Max(0, weather.DurationHours + command.Delta); message = "Weather duration set to " + weather.DurationHours + " hour(s)."; break;
                case GameplayScheduleCommandKind.StepWeatherDay: weather.When.Day = Math.Max(1, weather.When.Day + command.Delta); message = "Updated scheduled day to " + weather.When.Day + "."; break;
                case GameplayScheduleCommandKind.StepWeatherHour: weather.When.Hour = ScenarioAuthoringSchedule.Clamp(weather.When.Hour + command.Delta, 0, 23); message = "Updated scheduled hour to " + weather.When.Hour + "."; break;
                case GameplayScheduleCommandKind.StepWeatherMinute: weather.When.Minute = ScenarioAuthoringSchedule.Clamp(weather.When.Minute + command.Delta, 0, 59); message = "Updated scheduled minute to " + weather.When.Minute + "."; break;
                default: return false;
            }
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            return true;
        }

        private static bool HandleQuestCommand(ScenarioEditorSession session, GameplayScheduleCommand command, out string message)
        {
            QuestAuthoringDefinition quests = EnsureQuests(session.WorkingDefinition);
            if (!ValidIndex(command.Index, quests.Quests.Count, "Quest", out message)) return false;
            QuestDefinition quest = quests.Quests[command.Index];
            switch (command.Kind)
            {
                case GameplayScheduleCommandKind.DeleteQuest: quests.Quests.RemoveAt(command.Index); MarkQuestDirty(session); message = "Removed quest."; return true;
                case GameplayScheduleCommandKind.MoveQuest:
                {
                    int target = command.Index + command.Delta;
                    if (target < 0 || target >= quests.Quests.Count) { message = "Quest is already at the edge of the schedule."; return true; }
                    quests.Quests.RemoveAt(command.Index); quests.Quests.Insert(target, quest); MarkQuestDirty(session); message = "Moved quest."; return true;
                }
                case GameplayScheduleCommandKind.DuplicateQuest:
                    quests.Quests.Insert(command.Index + 1, CopyQuest(quest, quests.Quests.Count + 1)); MarkQuestDirty(session); message = "Duplicated the selected quest popup."; return true;
                case GameplayScheduleCommandKind.CycleQuestId: return CycleQuestId(session, quest, command.Delta, out message);
                case GameplayScheduleCommandKind.SetQuestStartMode: return SetQuestStartMode(session, quest, session.WorkingDefinition, command.Value, out message);
                case GameplayScheduleCommandKind.ToggleQuestStartMode: return ToggleQuestStartMode(session, quest, session.WorkingDefinition, out message);
                case GameplayScheduleCommandKind.CycleQuestTrigger: return CycleQuestTrigger(session, quest, session.WorkingDefinition, command.Delta, out message);
                case GameplayScheduleCommandKind.CycleQuestCompletion: return CycleQuestCompletion(session, quest, session.WorkingDefinition, command.Delta, out message);
                case GameplayScheduleCommandKind.SyncQuestTitle: return SyncQuestTitle(session, quest, out message);
                case GameplayScheduleCommandKind.SyncQuestDescription: return SyncQuestDescription(session, quest, out message);
                case GameplayScheduleCommandKind.SpawnQuestNow: return SpawnQuestNow(quest, out message);
                case GameplayScheduleCommandKind.StepQuestDay: return StepQuestSchedule(session, quest, command.Delta, 0, out message);
                case GameplayScheduleCommandKind.StepQuestHour: return StepQuestSchedule(session, quest, command.Delta, 1, out message);
                case GameplayScheduleCommandKind.StepQuestMinute: return StepQuestSchedule(session, quest, command.Delta, 2, out message);
                default: return false;
            }
        }

        private static bool StepQuestSchedule(ScenarioEditorSession session, QuestDefinition quest, int delta, int unit, out string message)
        {
            if (quest.ScheduledStart == null) { message = "This quest is trigger-started and has no schedule to edit."; return false; }
            if (unit == 0) { quest.ScheduledStart.Day = Math.Max(1, quest.ScheduledStart.Day + delta); message = "Updated scheduled day to " + quest.ScheduledStart.Day + "."; }
            else if (unit == 1) { quest.ScheduledStart.Hour = ScenarioAuthoringSchedule.Clamp(quest.ScheduledStart.Hour + delta, 0, 23); message = "Updated scheduled hour to " + quest.ScheduledStart.Hour + "."; }
            else { quest.ScheduledStart.Minute = ScenarioAuthoringSchedule.Clamp(quest.ScheduledStart.Minute + delta, 0, 59); message = "Updated scheduled minute to " + quest.ScheduledStart.Minute + "."; }
            MarkQuestDirty(session);
            return true;
        }

        private bool AddCatalogQuest(ScenarioEditorSession session, int catalogIndex, out string message)
        {
            List<QuestDef> catalog = GetQuestCatalog();
            if (!ValidIndex(catalogIndex, catalog.Count, "Quest catalog entry", out message)) return false;
            RecordQuestCreation(session, "Add library quest");
            QuestAuthoringDefinition quests = EnsureQuests(session.WorkingDefinition);
            QuestDefinition quest = new QuestDefinition();
            ApplyLibraryQuest(quest, catalog[catalogIndex]);
            quest.ScheduledStart = ScenarioAuthoringSchedule.NextTime();
            quests.Quests.Add(quest);
            MarkQuestDirty(session);
            message = "Added the selected library quest to Authored.";
            return true;
        }

        private static bool ValidIndex(int index, int count, string label, out string message)
        {
            if (index >= 0 && index < count) { message = null; return true; }
            message = label + " is missing.";
            return false;
        }

        private static bool IsFutureCommand(GameplayScheduleCommandKind kind) { return kind >= GameplayScheduleCommandKind.RemoveFutureSurvivor && kind <= GameplayScheduleCommandKind.StepFutureHour; }
        private static bool IsStartingInventoryCommand(GameplayScheduleCommandKind kind) { return kind >= GameplayScheduleCommandKind.ToggleStartingOverride && kind <= GameplayScheduleCommandKind.SetStartingItem; }
        private static bool IsTimedInventoryCommand(GameplayScheduleCommandKind kind) { return kind >= GameplayScheduleCommandKind.DeleteTimedItem && kind <= GameplayScheduleCommandKind.StepTimedMinute; }
        private static bool IsWeatherCommand(GameplayScheduleCommandKind kind) { return kind >= GameplayScheduleCommandKind.DeleteWeather && kind <= GameplayScheduleCommandKind.StepWeatherMinute; }
        private static bool IsQuestCommand(GameplayScheduleCommandKind kind) { return kind >= GameplayScheduleCommandKind.DeleteQuest && kind <= GameplayScheduleCommandKind.StepQuestMinute; }
        private bool AddFutureSurvivor(ScenarioEditorSession session, out string message)
        {
            FamilySetupDefinition family = EnsureFamily(session.WorkingDefinition);
            FutureSurvivorDefinition survivor = new FutureSurvivorDefinition();
            survivor.Id = "future_survivor_" + (family.FutureSurvivors.Count + 1).ToString();
            survivor.Arrival = ScenarioAuthoringSchedule.NextTime();
            survivor.Survivor = ShelteredScenarioEditor.Domain.People.ScenarioFamilyConfigurationPolicy.CreateDefaultConfig(
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
            message = "Added shelter storage item '" + entry.ItemId + "'.";
            return true;
        }

        private bool ApplyStarterPreset(ScenarioEditorSession session, int presetIndex, out string message)
        {
            message = null;
            ScenarioSuppliesPresetCatalog.PresetInfo preset = ScenarioSuppliesPresetCatalog.ByIndex(presetIndex);
            if (preset == null)
            {
                message = "Unknown starter loadout preset.";
                return false;
            }

            ScenarioDefinition definition = session.WorkingDefinition;
            if (_historyService != null)
                _historyService.RecordAuthoringChange(definition, "Apply " + preset.DisplayName + " loadout", ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);

            StartingInventoryDefinition inventory = EnsureInventory(definition);
            List<ItemEntry> stacks = ScenarioSuppliesPresetCatalog.BuildStacks(preset);
            inventory.Items.Clear();
            for (int i = 0; i < stacks.Count; i++)
                inventory.Items.Add(stacks[i]);
            ScenarioSuppliesInventoryNormalizer.Normalize(inventory.Items);
            if (inventory.Items.Count > 0)
                inventory.OverrideRandomStart = true;

            MarkInventoryDirty(session);
            message = "Applied " + preset.DisplayName + " starter loadout (" + inventory.Items.Count + " stack(s)).";
            return true;
        }

        private bool MergeStartingInventoryDuplicates(ScenarioEditorSession session, out string message)
        {
            ScenarioDefinition definition = session.WorkingDefinition;
            StartingInventoryDefinition inventory = EnsureInventory(definition);
            if (!ScenarioSuppliesInventoryNormalizer.NeedsNormalize(inventory.Items))
            {
                message = "No duplicate or empty starting stacks to merge.";
                return false;
            }

            if (_historyService != null)
                _historyService.RecordAuthoringChange(definition, "Merge duplicate starting items", ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);

            ScenarioSuppliesInventoryNormalizer.NormalizeResult result = ScenarioSuppliesInventoryNormalizer.Normalize(inventory.Items);
            MarkInventoryDirty(session);
            message = "Merged " + result.MergedStacks + " duplicate stack(s) and removed " + result.RemovedStacks + " empty stack(s).";
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

        private bool AddScheduledQuest(ScenarioEditorSession session, out string message)
        {
            RecordQuestCreation(session, "Add quest popup");
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
                quest.Description = "Created in the scenario editor. Choose a quest from the library before playtesting.";
            }
            quest.ScheduledStart = ScenarioAuthoringSchedule.NextTime();
            quests.Quests.Add(quest);
            MarkQuestDirty(session);
            message = "Added a quest popup for " + ScenarioAuthoringSchedule.Format(quest.ScheduledStart) + ".";
            return true;
        }
        private static bool CaptureActiveQuests(ScenarioEditorSession session, out string message)
        {
            QuestManager manager = QuestManager.instance;
            if (manager == null)
            {
                message = "Live quests are not available yet; nothing was captured.";
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
        private static bool CycleQuestId(ScenarioEditorSession session, QuestDefinition quest, int delta, out string message)
        {
            message = null;
            if (quest == null)
                return true;

            List<QuestDef> catalog = GetQuestCatalog();
            if (catalog.Count == 0)
            {
                message = "The quest library is not available yet, so the source could not be changed.";
                return true;
            }

            int current = IndexOfQuest(catalog, quest.Id);
            int next = current < 0 ? 0 : Wrap(current + delta, catalog.Count);
            ApplyLibraryQuest(quest, catalog[next]);
            MarkQuestDirty(session);
            message = "Changed the selected quest library entry.";
            return true;
        }

        private void RecordQuestCreation(ScenarioEditorSession session, string description)
        {
            if (session == null || session.WorkingDefinition == null)
                return;
            if (_historyService != null)
                _historyService.RecordAuthoringChange(
                    session.WorkingDefinition,
                    description,
                    ScenarioDirtySection.Triggers,
                    ScenarioEditCategory.Triggers);
        }

        private static bool SetQuestStartMode(
            ScenarioEditorSession session,
            QuestDefinition quest,
            ScenarioDefinition definition,
            string mode,
            out string message)
        {
            message = null;
            if (quest == null)
                return true;

            if (string.Equals(mode, "scheduled", StringComparison.OrdinalIgnoreCase))
            {
                quest.StartTriggerId = null;
                if (quest.ScheduledStart == null)
                    quest.ScheduledStart = ScenarioAuthoringSchedule.NextTime();
                MarkQuestDirty(session);
                message = "Quest popup now starts on its schedule.";
                return true;
            }

            if (string.Equals(mode, "triggered", StringComparison.OrdinalIgnoreCase))
            {
                quest.ScheduledStart = null;
                if (string.IsNullOrEmpty(quest.StartTriggerId))
                    quest.StartTriggerId = EnsureFirstTriggerId(definition);
                MarkQuestDirty(session);
                message = "Quest popup now starts from the selected authored trigger.";
                return true;
            }

            message = "Unknown quest start option.";
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
                message = "Quest popup now starts from the selected authored trigger.";
            }
            else
            {
                quest.StartTriggerId = null;
                if (quest.ScheduledStart == null)
                    quest.ScheduledStart = ScenarioAuthoringSchedule.NextTime();
                message = "Quest popup now starts from its schedule.";
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
            message = "Selected a different authored trigger for this quest popup.";
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
                ? "Cleared the quest completion requirement."
                : "Selected a quest completion requirement.";
            return true;
        }

        private static bool SyncQuestTitle(ScenarioEditorSession session, QuestDefinition quest, out string message)
        {
            QuestDef def = FindQuestDef(quest != null ? quest.Id : null);
            if (quest == null || def == null)
            {
                message = "The selected quest library entry could not be found.";
                return true;
            }

            quest.Title = BuildQuestTitle(def);
            MarkQuestDirty(session);
            message = "Updated the popup title from the quest library.";
            return true;
        }

        private static bool SyncQuestDescription(ScenarioEditorSession session, QuestDefinition quest, out string message)
        {
            QuestDef def = FindQuestDef(quest != null ? quest.Id : null);
            if (quest == null || def == null)
            {
                message = "The selected quest library entry could not be found.";
                return true;
            }

            quest.Description = !string.IsNullOrEmpty(def.descriptionKey) ? def.descriptionKey : "QuestLibrary entry " + def.id;
            MarkQuestDirty(session);
            message = "Updated the popup description from the quest library.";
            return true;
        }

        private static bool SpawnQuestNow(QuestDefinition quest, out string message)
        {
            if (quest == null || string.IsNullOrEmpty(quest.Id))
            {
                message = "Choose a quest library entry before previewing this popup.";
                return true;
            }

            if (QuestManager.instance == null)
            {
                message = "Live quest preview is not available yet.";
                return true;
            }

            bool spawned = QuestManager.instance.SpawnQuestWithId(quest.Id);
            message = spawned
                ? "Opened the quest popup preview."
                : "The game could not open this quest popup. Check availability and the number of active quests.";
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

        private static void AddConditionIds(List<string> ids, List<ScenarioConditionRef> conditions)
        {
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                ScenarioConditionRef condition = conditions[i];
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

        private bool FinishStartingInventoryMutation(ScenarioEditorSession session, bool changed, string reason, ref string message)
        {
            if (!changed || _inventoryProjectionService == null)
                return changed;

            string projectionMessage;
            if (_inventoryProjectionService.TryProject(session, reason, out projectionMessage) && !string.IsNullOrEmpty(projectionMessage))
                message = string.IsNullOrEmpty(message) ? projectionMessage : message + " " + projectionMessage;
            return changed;
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
