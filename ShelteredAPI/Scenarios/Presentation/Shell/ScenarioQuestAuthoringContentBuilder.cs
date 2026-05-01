using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioQuestAuthoringContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        private const int CatalogPreviewLimit = 20;
        private const int OverviewWarningLimit = 4;

        public ScenarioAuthoringWindowContentKind ContentKind
        {
            get { return ScenarioAuthoringWindowContentKind.Quests; }
        }

        public ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioDefinition definition = context != null ? context.Definition : null;
            QuestAuthoringSnapshot snapshot = QuestAuthoringSnapshot.From(definition);
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();

            sections.Add(BuildOverviewSection(snapshot));
            sections.Add(BuildToolsSection(snapshot));
            AppendAuthoredQuestSections(sections, snapshot);
            sections.Add(BuildPickerSection(snapshot));
            sections.Add(BuildRuntimeSection());

            return sections.ToArray();
        }

        // === Overview ===

        private static ScenarioAuthoringInspectorSection BuildOverviewSection(QuestAuthoringSnapshot snapshot)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            int authored = snapshot.AuthoredCount;
            int scheduled = snapshot.ScheduledCount;
            int triggered = authored - scheduled;
            int live = QuestAuthoringSnapshot.CountLiveQuests();

            if (authored == 0)
            {
                items.Add(ScenarioAuthoringPresentationBuilder.Text(
                    "You have no authored quests. Click Add Quest below, or pick from the Quest Library further down."));
            }
            else if (snapshot.HasNextScheduled)
            {
                items.Add(ScenarioAuthoringPresentationBuilder.Text("Next popup: " + snapshot.NextScheduledLabel));
            }
            else
            {
                items.Add(ScenarioAuthoringPresentationBuilder.Text(
                    "All your quests are trigger-started — none are on a schedule yet."));
            }

            items.Add(ScenarioAuthoringPresentationBuilder.Text(
                "Scheduled quests fire on a day/time. Trigger quests wait for an event."));

            items.Add(ScenarioAuthoringPresentationBuilder.Property("Authored quests", authored.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("On a schedule", scheduled.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Wait for trigger", triggered.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Running right now", live.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Library size", snapshot.CatalogCount.ToString(CultureInfo.InvariantCulture)));

            if (snapshot.Warnings.Count == 0)
            {
                items.Add(ScenarioAuthoringPresentationBuilder.Property(
                    "Validation",
                    authored == 0 ? "Nothing to validate" : "OK"));
            }
            else
            {
                items.Add(ScenarioAuthoringPresentationBuilder.Property(
                    "Validation",
                    snapshot.Warnings.Count.ToString(CultureInfo.InvariantCulture) + " warning(s)"));
                int max = Math.Min(snapshot.Warnings.Count, OverviewWarningLimit);
                for (int i = 0; i < max; i++)
                    items.Add(ScenarioAuthoringPresentationBuilder.Text("! " + snapshot.Warnings[i]));
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "quest_overview",
                Title = "Status",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        // === Tools ===

        private static ScenarioAuthoringInspectorSection BuildToolsSection(QuestAuthoringSnapshot snapshot)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                ScenarioAuthoringActionIds.ActionQuestScheduleAdd,
                "Add Quest",
                "Add a fresh authored quest entry, populated from the next library quest you have not used yet.",
                true,
                snapshot.AuthoredCount == 0,
                "Q+")));
            items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                ScenarioAuthoringActionIds.ActionQuestCaptureActive,
                "Capture Active",
                "Replace the authored list with every quest currently active in QuestManager.",
                true,
                false,
                "QC")));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "quest_tools",
                Title = "Tools",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = items.ToArray()
            };
        }

        // === Authored quests ===

        private static void AppendAuthoredQuestSections(
            List<ScenarioAuthoringInspectorSection> sections,
            QuestAuthoringSnapshot snapshot)
        {
            if (snapshot.AuthoredCount == 0)
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_authored_empty",
                    Title = "Your quests",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        ScenarioAuthoringPresentationBuilder.Text(
                            "No authored quests yet. Click Add Quest above or pick one from the Quest Library below.")
                    }
                });
                return;
            }

            QuestSectionBuilder builder = new QuestSectionBuilder(snapshot);
            for (int i = 0; i < snapshot.AuthoredCount; i++)
                builder.AppendQuest(sections, snapshot.Authored[i], i);
        }

        // === Picker ===

        private static ScenarioAuthoringInspectorSection BuildPickerSection(QuestAuthoringSnapshot snapshot)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();

            if (snapshot.Catalog.Count == 0)
            {
                items.Add(ScenarioAuthoringPresentationBuilder.Text(snapshot.CatalogReady
                    ? "QuestLibrary returned no quests."
                    : "QuestLibrary is not ready in this scene. Open a save or playtest first."));
            }
            else
            {
                items.Add(ScenarioAuthoringPresentationBuilder.Text(
                    "Click any library quest below to add it to your scenario as a scheduled popup."));

                int max = Math.Min(snapshot.Catalog.Count, CatalogPreviewLimit);
                for (int i = 0; i < max; i++)
                {
                    QuestDef quest = snapshot.Catalog[i];
                    if (quest == null)
                        continue;

                    bool available = QuestAuthoringSnapshot.IsQuestAvailable(quest);
                    string suffix = available ? string.Empty : "  (locked)";
                    items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                        ScenarioAuthoringActionIds.ActionQuestCatalogAddPrefix + i.ToString(CultureInfo.InvariantCulture),
                        "+ " + quest.id + "   " + quest.questType.ToString() + suffix,
                        "Add this QuestLibrary quest to the scenario draft.",
                        true,
                        false,
                        "Q+")));
                }

                if (snapshot.Catalog.Count > max)
                {
                    items.Add(ScenarioAuthoringPresentationBuilder.Text(
                        "Showing " + max.ToString(CultureInfo.InvariantCulture)
                        + " of " + snapshot.Catalog.Count.ToString(CultureInfo.InvariantCulture)
                        + " library quests. Use Cycle Quest Id on an authored quest to reach the rest."));
                }
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "quest_picker",
                Title = "Quest Library",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        // === Live runtime ===

        private static ScenarioAuthoringInspectorSection BuildRuntimeSection()
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            QuestManager manager = QuestManager.instance;
            List<QuestInstance> quests = manager != null ? manager.GetCurrentQuests(true, true, true) : null;
            for (int i = 0; quests != null && i < quests.Count; i++)
            {
                QuestInstance quest = quests[i];
                if (quest == null || quest.definition == null)
                    continue;

                string state = quest.state.ToString();
                if (quest.definition.IsScenario() && quest.stage != null)
                    state += " / " + quest.stage.id;
                items.Add(ScenarioAuthoringPresentationBuilder.Property(
                    ScenarioAuthoringPresentationBuilder.Safe(quest.definition.id),
                    state));
            }

            if (items.Count == 0)
            {
                items.Add(ScenarioAuthoringPresentationBuilder.Text(
                    "No quests are currently running in QuestManager."));
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "quest_runtime",
                Title = "Live Runtime",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        // === Per-quest builder ===

        private sealed class QuestSectionBuilder
        {
            private readonly QuestAuthoringSnapshot _snapshot;

            public QuestSectionBuilder(QuestAuthoringSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public void AppendQuest(
                List<ScenarioAuthoringInspectorSection> sections,
                QuestDefinition quest,
                int index)
            {
                if (quest == null)
                    return;

                string idPart = index.ToString(CultureInfo.InvariantCulture);
                bool triggerStarted = !string.IsNullOrEmpty(quest.StartTriggerId);
                QuestDef libraryQuest = QuestAuthoringSnapshot.FindQuestDef(quest.Id);

                sections.Add(BuildOverviewSection(quest, idPart, index, triggerStarted, libraryQuest));
                sections.Add(BuildModeSection(idPart, triggerStarted));
                if (triggerStarted)
                    sections.Add(BuildTriggerSection(quest, idPart));
                else
                    sections.Add(BuildScheduleSection(quest, idPart));
                sections.Add(BuildIdentitySection(quest, idPart, libraryQuest));
                sections.Add(BuildLifecycleSection(idPart, index, libraryQuest));
            }

            private ScenarioAuthoringInspectorSection BuildOverviewSection(
                QuestDefinition quest,
                string idPart,
                int index,
                bool triggerStarted,
                QuestDef libraryQuest)
            {
                string title = !string.IsNullOrEmpty(quest.Title) ? quest.Title : quest.Id;
                string when = triggerStarted
                    ? "On trigger '" + ScenarioAuthoringPresentationBuilder.Safe(quest.StartTriggerId) + "'"
                    : QuestAuthoringHelpers.FormatSchedule(quest.ScheduledStart);
                string sectionTitle = "Quest #" + (index + 1).ToString(CultureInfo.InvariantCulture)
                    + "  ·  " + ScenarioAuthoringPresentationBuilder.Safe(title)
                    + "  ·  " + when;
                string validation = QuestAuthoringHelpers.FormatQuestValidation(quest, _snapshot.Definition);

                List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
                items.Add(ScenarioAuthoringPresentationBuilder.Property(
                    "Quest id",
                    ScenarioAuthoringPresentationBuilder.Safe(quest.Id)));
                items.Add(ScenarioAuthoringPresentationBuilder.Property(
                    "Library",
                    libraryQuest != null
                        ? QuestAuthoringHelpers.BuildQuestLibrarySummary(libraryQuest)
                        : "not found"));
                items.Add(ScenarioAuthoringPresentationBuilder.Property("Validation", validation));

                return new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_authored_header_" + idPart,
                    Title = sectionTitle,
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = items.ToArray()
                };
            }

            private static ScenarioAuthoringInspectorSection BuildModeSection(
                string idPart,
                bool triggerStarted)
            {
                List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestStartModePrefix + idPart,
                    "Scheduled",
                    "Start at a specific day and time.",
                    triggerStarted,
                    !triggerStarted,
                    "SC")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestStartModePrefix + idPart,
                    "Trigger",
                    "Wait until a Trigger fires.",
                    !triggerStarted,
                    triggerStarted,
                    "TR")));

                return new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_authored_mode_" + idPart,
                    Title = "How does it start?",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.TabStrip,
                    Items = items.ToArray()
                };
            }

            private static ScenarioAuthoringInspectorSection BuildScheduleSection(
                QuestDefinition quest,
                string idPart)
            {
                ScenarioScheduleTime time = quest.ScheduledStart;
                int day = time != null ? time.Day : 1;
                int hour = time != null ? time.Hour : 8;
                int minute = time != null ? time.Minute : 0;
                string current = "Day " + day.ToString(CultureInfo.InvariantCulture)
                    + " · " + hour.ToString("D2", CultureInfo.InvariantCulture)
                    + ":" + minute.ToString("D2", CultureInfo.InvariantCulture);

                List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleDayPrefix + idPart + ".-1",
                    "Day -",
                    "Move this quest one day earlier.",
                    true,
                    false,
                    "D-")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleDayPrefix + idPart + ".1",
                    "Day +",
                    "Move this quest one day later.",
                    true,
                    false,
                    "D+")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleHourPrefix + idPart + ".-1",
                    "Hr -",
                    "Move this quest one hour earlier.",
                    true,
                    false,
                    "H-")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleHourPrefix + idPart + ".1",
                    "Hr +",
                    "Move this quest one hour later.",
                    true,
                    false,
                    "H+")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleMinutePrefix + idPart + ".-15",
                    "Min -15",
                    "Move this quest fifteen minutes earlier.",
                    true,
                    false,
                    "M-")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleMinutePrefix + idPart + ".15",
                    "Min +15",
                    "Move this quest fifteen minutes later.",
                    true,
                    false,
                    "M+")));
                items.Add(ScenarioAuthoringPresentationBuilder.Property("When", current));

                return new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_authored_schedule_" + idPart,
                    Title = "When does it pop up?",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                    Items = items.ToArray()
                };
            }

            private ScenarioAuthoringInspectorSection BuildTriggerSection(
                QuestDefinition quest,
                string idPart)
            {
                List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestTriggerCyclePrefix + idPart + ".-1",
                    "< Prev",
                    "Attach this quest to the previous authored trigger.",
                    _snapshot.HasAnyTriggers,
                    false,
                    "TG-")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestTriggerCyclePrefix + idPart + ".1",
                    "Next >",
                    "Attach this quest to the next authored trigger.",
                    _snapshot.HasAnyTriggers,
                    false,
                    "TG+")));
                items.Add(ScenarioAuthoringPresentationBuilder.Property(
                    "Trigger",
                    !string.IsNullOrEmpty(quest.StartTriggerId) ? quest.StartTriggerId : "<none>"));
                if (!_snapshot.HasAnyTriggers)
                {
                    items.Add(ScenarioAuthoringPresentationBuilder.Text(
                        "No triggers exist yet. Author one in the Triggers window first."));
                }

                return new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_authored_trigger_" + idPart,
                    Title = "Which trigger starts it?",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                    Items = items.ToArray()
                };
            }

            private static ScenarioAuthoringInspectorSection BuildIdentitySection(
                QuestDefinition quest,
                string idPart,
                QuestDef libraryQuest)
            {
                List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestIdCyclePrefix + idPart + ".-1",
                    "< Prev id",
                    "Switch this quest to the previous QuestLibrary id.",
                    true,
                    false,
                    "ID-")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestIdCyclePrefix + idPart + ".1",
                    "Next id >",
                    "Switch this quest to the next QuestLibrary id.",
                    true,
                    false,
                    "ID+")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestTitleSyncPrefix + idPart,
                    "Sync Title",
                    "Copy the QuestLibrary name key into this authored quest title.",
                    libraryQuest != null,
                    false,
                    "NM")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestDescriptionSyncPrefix + idPart,
                    "Sync Desc",
                    "Copy the QuestLibrary description key into this authored quest description.",
                    libraryQuest != null,
                    false,
                    "DS")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestCompletionCyclePrefix + idPart + ".1",
                    "Cycle Completion",
                    "Cycle the optional completion condition reference.",
                    true,
                    !string.IsNullOrEmpty(quest.CompletionConditionId),
                    "CC")));
                items.Add(ScenarioAuthoringPresentationBuilder.Property("Title", ScenarioAuthoringPresentationBuilder.Safe(quest.Title)));
                items.Add(ScenarioAuthoringPresentationBuilder.Property(
                    "Completion",
                    !string.IsNullOrEmpty(quest.CompletionConditionId) ? quest.CompletionConditionId : "<none>"));

                return new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_authored_id_" + idPart,
                    Title = "Quest content",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                    Items = items.ToArray()
                };
            }

            private static ScenarioAuthoringInspectorSection BuildLifecycleSection(
                string idPart,
                int index,
                QuestDef libraryQuest)
            {
                List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestSpawnNowPrefix + idPart,
                    "Spawn Now",
                    "Immediately ask QuestManager to spawn this quest so you can preview the popup.",
                    libraryQuest != null,
                    false,
                    "SP")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestMovePrefix + idPart + ".-1",
                    "Move Up",
                    "Move this quest earlier in the authored list.",
                    index > 0,
                    false,
                    "UP")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestMovePrefix + idPart + ".1",
                    "Move Down",
                    "Move this quest later in the authored list.",
                    true,
                    false,
                    "DN")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestDuplicatePrefix + idPart,
                    "Duplicate",
                    "Copy this authored quest entry.",
                    true,
                    false,
                    "CP")));
                items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleDeletePrefix + idPart,
                    "Remove",
                    "Remove this authored quest entry.",
                    true,
                    false,
                    "RM")));

                return new ScenarioAuthoringInspectorSection
                {
                    Id = "quest_authored_actions_" + idPart,
                    Title = "Tools",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                    Items = items.ToArray()
                };
            }
        }

        // === Snapshot ===

        internal sealed class QuestAuthoringSnapshot
        {
            private QuestAuthoringSnapshot(
                ScenarioDefinition definition,
                List<QuestDefinition> authored,
                List<QuestDef> catalog,
                bool catalogReady,
                List<string> warnings,
                bool hasAnyTriggers)
            {
                Definition = definition;
                Authored = authored;
                Catalog = catalog;
                CatalogReady = catalogReady;
                Warnings = warnings;
                HasAnyTriggers = hasAnyTriggers;

                int scheduled = 0;
                QuestDefinition next = null;
                for (int i = 0; i < authored.Count; i++)
                {
                    QuestDefinition quest = authored[i];
                    if (quest == null)
                        continue;
                    if (string.IsNullOrEmpty(quest.StartTriggerId))
                    {
                        scheduled++;
                        if (quest.ScheduledStart != null
                            && (next == null || QuestAuthoringHelpers.CompareSchedule(quest.ScheduledStart, next.ScheduledStart) < 0))
                            next = quest;
                    }
                }

                ScheduledCount = scheduled;
                if (next != null)
                {
                    HasNextScheduled = true;
                    string label = QuestAuthoringHelpers.FormatSchedule(next.ScheduledStart);
                    string title = !string.IsNullOrEmpty(next.Title) ? next.Title : next.Id;
                    NextScheduledLabel = label + " — " + ScenarioAuthoringPresentationBuilder.Safe(title);
                }
            }

            public ScenarioDefinition Definition { get; private set; }
            public List<QuestDefinition> Authored { get; private set; }
            public List<QuestDef> Catalog { get; private set; }
            public bool CatalogReady { get; private set; }
            public List<string> Warnings { get; private set; }
            public bool HasAnyTriggers { get; private set; }
            public int AuthoredCount { get { return Authored.Count; } }
            public int ScheduledCount { get; private set; }
            public int CatalogCount { get { return Catalog.Count; } }
            public bool HasNextScheduled { get; private set; }
            public string NextScheduledLabel { get; private set; }

            public static QuestAuthoringSnapshot From(ScenarioDefinition definition)
            {
                List<QuestDefinition> authored = new List<QuestDefinition>();
                if (definition != null && definition.Quests != null && definition.Quests.Quests != null)
                {
                    for (int i = 0; i < definition.Quests.Quests.Count; i++)
                        authored.Add(definition.Quests.Quests[i]);
                }

                bool catalogReady = QuestLibrary.instance != null;
                List<QuestDef> catalog = QuestAuthoringHelpers.GetQuestCatalog();
                List<string> warnings = QuestAuthoringHelpers.BuildQuestWarnings(definition);
                bool hasTriggers = QuestAuthoringHelpers.HasAnyTrigger(definition);
                return new QuestAuthoringSnapshot(definition, authored, catalog, catalogReady, warnings, hasTriggers);
            }

            public static int CountLiveQuests()
            {
                QuestManager manager = QuestManager.instance;
                List<QuestInstance> quests = manager != null ? manager.GetCurrentQuests(true, true, true) : null;
                return quests != null ? quests.Count : 0;
            }

            public static QuestDef FindQuestDef(string id)
            {
                if (string.IsNullOrEmpty(id) || QuestLibrary.instance == null)
                    return null;
                return QuestLibrary.instance.FindQuestDefinition(id);
            }

            public static bool IsQuestAvailable(QuestDef quest)
            {
                if (quest == null || QuestLibrary.instance == null)
                    return false;
                try
                {
                    return QuestLibrary.instance.IsAvailable(quest, true, false)
                        || QuestLibrary.instance.IsAvailable(quest, true, true);
                }
                catch
                {
                    return false;
                }
            }
        }

        // === Pure helpers ===

        internal static class QuestAuthoringHelpers
        {
            public static int CompareSchedule(ScenarioScheduleTime left, ScenarioScheduleTime right)
            {
                if (left == null && right == null)
                    return 0;
                if (left == null)
                    return 1;
                if (right == null)
                    return -1;
                int byDay = left.Day.CompareTo(right.Day);
                if (byDay != 0)
                    return byDay;
                int byHour = left.Hour.CompareTo(right.Hour);
                if (byHour != 0)
                    return byHour;
                return left.Minute.CompareTo(right.Minute);
            }

            public static string FormatSchedule(ScenarioScheduleTime time)
            {
                if (time == null)
                    return "unscheduled";
                return "day " + time.Day.ToString(CultureInfo.InvariantCulture)
                    + " " + time.Hour.ToString("D2", CultureInfo.InvariantCulture)
                    + ":" + time.Minute.ToString("D2", CultureInfo.InvariantCulture);
            }

            public static string FormatQuestValidation(QuestDefinition quest, ScenarioDefinition definition)
            {
                if (quest == null)
                    return "missing quest";
                if (string.IsNullOrEmpty(quest.Id))
                    return "missing id";
                if (QuestAuthoringSnapshot.FindQuestDef(quest.Id) == null)
                    return "missing QuestLibrary definition";
                if (definition != null
                    && !string.IsNullOrEmpty(quest.StartTriggerId)
                    && !HasTrigger(definition, quest.StartTriggerId))
                    return "missing trigger";
                if (definition != null
                    && !string.IsNullOrEmpty(quest.CompletionConditionId)
                    && !HasCompletionCondition(definition, quest.CompletionConditionId))
                    return "missing completion condition";

                QuestDef libraryQuest = QuestAuthoringSnapshot.FindQuestDef(quest.Id);
                return libraryQuest != null && QuestAuthoringSnapshot.IsQuestAvailable(libraryQuest)
                    ? "available now"
                    : "valid id, gated by vanilla availability";
            }

            public static List<string> BuildQuestWarnings(ScenarioDefinition definition)
            {
                List<string> warnings = new List<string>();
                Dictionary<string, bool> ids = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                int count = definition != null && definition.Quests != null && definition.Quests.Quests != null
                    ? definition.Quests.Quests.Count
                    : 0;

                for (int i = 0; i < count; i++)
                {
                    QuestDefinition quest = definition.Quests.Quests[i];
                    string label = quest != null && !string.IsNullOrEmpty(quest.Id)
                        ? quest.Id
                        : "#" + (i + 1).ToString(CultureInfo.InvariantCulture);
                    if (quest == null)
                    {
                        warnings.Add("Quest #" + (i + 1).ToString(CultureInfo.InvariantCulture) + " is empty.");
                        continue;
                    }
                    if (string.IsNullOrEmpty(quest.Id))
                        warnings.Add("Quest #" + (i + 1).ToString(CultureInfo.InvariantCulture) + " has no QuestLibrary id.");
                    else if (ids.ContainsKey(quest.Id))
                        warnings.Add("Duplicate quest id in draft: " + quest.Id);
                    else
                        ids[quest.Id] = true;

                    if (!string.IsNullOrEmpty(quest.Id) && QuestAuthoringSnapshot.FindQuestDef(quest.Id) == null)
                        warnings.Add("Quest '" + quest.Id + "' is not present in QuestLibrary.");
                    if (!string.IsNullOrEmpty(quest.StartTriggerId) && !HasTrigger(definition, quest.StartTriggerId))
                        warnings.Add("Quest '" + label + "' references missing trigger '" + quest.StartTriggerId + "'.");
                    if (string.IsNullOrEmpty(quest.StartTriggerId) && quest.ScheduledStart == null)
                        warnings.Add("Quest '" + label + "' has neither schedule nor trigger.");
                    if (!string.IsNullOrEmpty(quest.CompletionConditionId)
                        && !HasCompletionCondition(definition, quest.CompletionConditionId))
                        warnings.Add("Quest '" + label + "' references missing completion condition '" + quest.CompletionConditionId + "'.");
                }

                return warnings;
            }

            public static List<QuestDef> GetQuestCatalog()
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

            public static string BuildQuestLibrarySummary(QuestDef quest)
            {
                if (quest == null)
                    return "<missing>";

                string type = quest.questType.ToString();
                string spawn = quest.spawnOptions != null
                    ? quest.spawnOptions.minDistance.ToString(CultureInfo.InvariantCulture) + "-" + quest.spawnOptions.maxDistance.ToString(CultureInfo.InvariantCulture) + " tiles"
                    : "default spawn";
                return type + " · " + spawn;
            }

            public static bool HasAnyTrigger(ScenarioDefinition definition)
            {
                return definition != null
                    && definition.TriggersAndEvents != null
                    && definition.TriggersAndEvents.Triggers != null
                    && definition.TriggersAndEvents.Triggers.Count > 0;
            }

            private static bool HasTrigger(ScenarioDefinition definition, string triggerId)
            {
                if (string.IsNullOrEmpty(triggerId)
                    || definition == null
                    || definition.TriggersAndEvents == null
                    || definition.TriggersAndEvents.Triggers == null)
                    return false;
                for (int i = 0; i < definition.TriggersAndEvents.Triggers.Count; i++)
                {
                    TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                    if (trigger != null && string.Equals(trigger.Id, triggerId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }

            private static bool HasCompletionCondition(ScenarioDefinition definition, string conditionId)
            {
                if (definition == null || definition.WinLossConditions == null)
                    return false;
                return ContainsConditionId(definition.WinLossConditions.WinConditions, conditionId)
                    || ContainsConditionId(definition.WinLossConditions.LossConditions, conditionId);
            }

            private static bool ContainsConditionId(List<ConditionDef> conditions, string conditionId)
            {
                for (int i = 0; conditions != null && i < conditions.Count; i++)
                {
                    ConditionDef condition = conditions[i];
                    if (condition != null && string.Equals(condition.Id, conditionId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
        }
    }
}
