using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Inspector;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
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

            sections.Add(BuildStoryOverviewSection(definition, snapshot));
            sections.Add(BuildStoryToolsSection(definition));
            AppendStoryStageSections(sections, definition);
            sections.Add(BuildSideQuestIntroSection(snapshot));
            sections.Add(BuildToolsSection(snapshot));
            AppendAuthoredQuestSections(sections, snapshot);
            sections.Add(BuildPickerSection(snapshot));
            sections.Add(BuildRuntimeSection());

            return sections.ToArray();
        }

        // === Scenario flow ===

        private static ScenarioAuthoringInspectorSection BuildStoryOverviewSection(ScenarioDefinition definition, QuestAuthoringSnapshot snapshot)
        {
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            int stages = flow != null && flow.Stages != null ? flow.Stages.Count : 0;
            int steps = 0;
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
                steps += flow.Stages[i] != null && flow.Stages[i].IntercomStages != null ? flow.Stages[i].IntercomStages.Count : 0;

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Text("Scenario flow uses vanilla day-granular stage timing. Custom scheduled actions use exact day/hour/minute time."));
            items.Add(ScenarioInspectorItemFactory.Property("Story stages", stages.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioInspectorItemFactory.Property("Intercom steps", steps.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioInspectorItemFactory.Property("Side quest popups", snapshot.AuthoredCount.ToString(CultureInfo.InvariantCulture)));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "story_flow_status",
                Title = "Story Flow",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildStoryToolsSection(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionStoryStageAdd,
                "Add Stage",
                "Add a vanilla scenario flow stage with one intercom step.",
                true,
                definition == null || definition.ScenarioFlow == null || definition.ScenarioFlow.Stages.Count == 0,
                "S+")));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "story_flow_tools",
                Title = "Stage Tools",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildSideQuestIntroSection(QuestAuthoringSnapshot snapshot)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "quest_popups_intro",
                Title = "Quest Popups / Side Quests",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.Text("These are flat QuestLibrary popups. They are separate from the vanilla scenario story flow above."),
                    ScenarioInspectorItemFactory.Property("Authored popups", snapshot.AuthoredCount.ToString(CultureInfo.InvariantCulture))
                }
            };
        }

        private static void AppendStoryStageSections(List<ScenarioAuthoringInspectorSection> sections, ScenarioDefinition definition)
        {
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            if (flow == null || flow.Stages == null || flow.Stages.Count == 0)
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "story_flow_empty",
                    Title = "Stages",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[] { ScenarioInspectorItemFactory.Text("No story stages yet. Add a stage to start the scenario flow.") }
                });
                return;
            }

            List<string> characterIds = BuildCharacterIds(definition);
            for (int i = 0; i < flow.Stages.Count; i++)
                AppendStoryStage(sections, flow, flow.Stages[i], i, characterIds);
        }

        private static void AppendStoryStage(List<ScenarioAuthoringInspectorSection> sections, ScenarioFlowDefinition flow, ScenarioFlowStageDefinition stage, int index, List<string> characterIds)
        {
            if (stage == null)
                return;

            string indexText = index.ToString(CultureInfo.InvariantCulture);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Stage id", Safe(stage.Id)));
            items.Add(ScenarioInspectorItemFactory.Property("Characters", stage.CharacterIds != null && stage.CharacterIds.Count > 0 ? string.Join(", ", stage.CharacterIds.ToArray()) : "<none>"));
            items.Add(ScenarioInspectorItemFactory.Property("Unanswered", FormatStageTarget(stage.UnansweredNextStage) + " / " + stage.UnansweredNextDays.ToString(CultureInfo.InvariantCulture) + " day(s)"));
            AddStageIdActions(items, flow, index);
            AddStageRouteActions(items, flow, index, stage.UnansweredNextStage);
            for (int c = 0; characterIds != null && c < characterIds.Count; c++)
            {
                string id = characterIds[c];
                bool selected = Contains(stage.CharacterIds, id);
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionStoryStageCharacterTogglePrefix + indexText + "." + Encode(id),
                    id,
                    selected ? "Remove this character from the stage." : "Add this character to the stage.",
                    true,
                    selected,
                    "CH")));
            }
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryStageUnansweredDelayPrefix + indexText + ".1", "Delay +", "Increase unanswered delay by one day.", true, false, "D+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryStageUnansweredDelayPrefix + indexText + ".-1", "Delay -", "Decrease unanswered delay by one day.", true, false, "D-")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryStagePunishPrefix + indexText, "Punish Unanswered", "Toggle vanilla unanswered punishment.", true, stage.PunishOnUnanswered, "PU")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryIntercomAddPrefix + indexText, "Add Step", "Add an intercom step to this stage.", true, false, "I+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryStageMovePrefix + indexText + ".-1", "Move Up", "Move this stage earlier.", index > 0, false, "UP")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryStageMovePrefix + indexText + ".1", "Move Down", "Move this stage later.", index + 1 < flow.Stages.Count, false, "DN")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryStageDuplicatePrefix + indexText, "Duplicate", "Copy this stage.", true, false, "CP")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryStageDeletePrefix + indexText, "Remove", "Remove this stage.", true, false, "RM")));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "story_stage_" + indexText,
                Title = "Stage " + (index + 1).ToString(CultureInfo.InvariantCulture) + " / " + Safe(stage.Id),
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = items.ToArray()
            });

            for (int i = 0; stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                sections.Add(BuildIntercomSection(flow, stage, stage.IntercomStages[i], index, i, characterIds));
        }

        private static ScenarioAuthoringInspectorSection BuildIntercomSection(ScenarioFlowDefinition flow, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex, List<string> characterIds)
        {
            string prefix = stageIndex.ToString(CultureInfo.InvariantCulture) + "." + intercomIndex.ToString(CultureInfo.InvariantCulture);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Step id", Safe(intercom.Id)));
            items.Add(ScenarioInspectorItemFactory.Property("Routes", "Next " + FormatStageTarget(intercom.NextId) + " / Alt " + FormatStageTarget(intercom.AlternateNextId)));
            items.Add(ScenarioInspectorItemFactory.Property("Stage change", intercom.StageChange != null ? FormatStageTarget(intercom.StageChange.Id) + " after " + intercom.StageChange.DelayDays.ToString(CultureInfo.InvariantCulture) + " day(s)" : "<none>"));
            AddIntercomIdActions(items, stage, stageIndex, intercomIndex);
            AddIntercomTargetActions(items, stage, intercom, ScenarioAuthoringActionIds.ActionStoryIntercomNextPrefix, prefix, "Next");
            AddIntercomTargetActions(items, stage, intercom, ScenarioAuthoringActionIds.ActionStoryIntercomAlternatePrefix, prefix, "Alt");
            AddStageChangeTargetActions(items, flow, prefix, intercom.StageChange != null ? intercom.StageChange.Id : null);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryStageChangeDelayPrefix + prefix + ".1", "Stage Delay +", "Increase stage-change delay.", true, false, "SD+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryStageChangeDelayPrefix + prefix + ".-1", "Stage Delay -", "Decrease stage-change delay.", true, false, "SD-")));
            AddIntercomTypeActions(items, prefix, intercom.Type);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryDialogueAddPrefix + prefix, "Add Dialogue", "Add a dialogue key.", true, false, "D+")));
            for (int i = 0; intercom.Dialogue != null && i < intercom.Dialogue.Count; i++)
                AddDialogueActions(items, stage, intercom.Dialogue[i], stageIndex, intercomIndex, i);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryOptionAddPrefix + prefix, "Add Option", "Add a response option.", true, false, "O+")));
            for (int i = 0; intercom.Options != null && i < intercom.Options.Count; i++)
                AddOptionActions(items, stage, intercom.Options[i], stageIndex, intercomIndex, i);
            AddRandomRouteActions(items, stage, intercom, stageIndex, intercomIndex);
            AddStoryItemActions(items, "Rewards", intercom.Items, ScenarioAuthoringActionIds.ActionStoryRewardAddPrefix, ScenarioAuthoringActionIds.ActionStoryRewardDeletePrefix, ScenarioAuthoringActionIds.ActionStoryRewardItemPrefix, ScenarioAuthoringActionIds.ActionStoryRewardQuantityPrefix, stageIndex, intercomIndex);
            AddStoryItemActions(items, "Removals", intercom.ItemsToRemove, ScenarioAuthoringActionIds.ActionStoryRemovalAddPrefix, ScenarioAuthoringActionIds.ActionStoryRemovalDeletePrefix, ScenarioAuthoringActionIds.ActionStoryRemovalItemPrefix, ScenarioAuthoringActionIds.ActionStoryRemovalQuantityPrefix, stageIndex, intercomIndex);
            AddMilestoneActions(items, intercom, stageIndex, intercomIndex);
            for (int i = 0; characterIds != null && i < characterIds.Count; i++)
            {
                string id = characterIds[i];
                bool selected = Contains(intercom.CharacterIdsToRecruit, id);
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryRecruitTogglePrefix + prefix + "." + Encode(id), "Recruit " + id, "Toggle recruitment for this character.", true, selected, "RC")));
            }
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryRecruitFamilyPrefix + prefix, "Recruit As Family", "Toggle family recruitment.", true, intercom.RecruitAsFamily, "RF")));
            AddEndTypeActions(items, prefix, intercom.EndOptions != null ? intercom.EndOptions.Type : null);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryEndCompleteQuestPrefix + prefix, "Complete Quest", "Toggle vanilla quest completion.", true, intercom.EndOptions != null && intercom.EndOptions.CompleteQuest, "CQ")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryEndCompleteScenarioPrefix + prefix, "Complete Scenario", "Toggle parent scenario completion.", true, intercom.EndOptions != null && intercom.EndOptions.CompleteParentScenario, "CS")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryIntercomMovePrefix + prefix + ".-1", "Move Step Up", "Move this intercom step earlier.", intercomIndex > 0, false, "UP")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryIntercomMovePrefix + prefix + ".1", "Move Step Down", "Move this intercom step later.", intercomIndex + 1 < stage.IntercomStages.Count, false, "DN")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryIntercomDuplicatePrefix + prefix, "Duplicate Step", "Copy this intercom step.", true, false, "CP")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryIntercomDeletePrefix + prefix, "Remove Step", "Remove this intercom step.", true, false, "RM")));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "story_intercom_" + prefix.Replace('.', '_'),
                Title = "Step " + (intercomIndex + 1).ToString(CultureInfo.InvariantCulture) + " / " + Safe(intercom.Id),
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = items.ToArray()
            };
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
                items.Add(ScenarioInspectorItemFactory.Text(
                    "You have no authored quests. Click Add Quest below, or pick from the Quest Library further down."));
            }
            else if (snapshot.HasNextScheduled)
            {
                items.Add(ScenarioInspectorItemFactory.Text("Next popup: " + snapshot.NextScheduledLabel));
            }
            else
            {
                items.Add(ScenarioInspectorItemFactory.Text(
                    "All your quests are trigger-started - none are on a schedule yet."));
            }

            items.Add(ScenarioInspectorItemFactory.Text(
                "Scheduled quests fire on a day/time. Trigger quests wait for an event."));

            items.Add(ScenarioInspectorItemFactory.Property("Authored quests", authored.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioInspectorItemFactory.Property("On a schedule", scheduled.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioInspectorItemFactory.Property("Wait for trigger", triggered.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioInspectorItemFactory.Property("Running right now", live.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioInspectorItemFactory.Property("Library size", snapshot.CatalogCount.ToString(CultureInfo.InvariantCulture)));

            if (snapshot.Warnings.Count == 0)
            {
                items.Add(ScenarioInspectorItemFactory.Property(
                    "Validation",
                    authored == 0 ? "Nothing to validate" : "OK"));
            }
            else
            {
                items.Add(ScenarioInspectorItemFactory.Property(
                    "Validation",
                    snapshot.Warnings.Count.ToString(CultureInfo.InvariantCulture) + " warning(s)"));
                int max = Math.Min(snapshot.Warnings.Count, OverviewWarningLimit);
                for (int i = 0; i < max; i++)
                    items.Add(ScenarioInspectorItemFactory.Text("! " + snapshot.Warnings[i]));
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
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionQuestScheduleAdd,
                "Add Quest",
                "Add a fresh authored quest entry, populated from the next library quest you have not used yet.",
                true,
                snapshot.AuthoredCount == 0,
                "Q+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
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
                        ScenarioInspectorItemFactory.Text(
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
                items.Add(ScenarioInspectorItemFactory.Text(snapshot.CatalogReady
                    ? "QuestLibrary returned no quests."
                    : "QuestLibrary is not ready in this scene. Open a save or playtest first."));
            }
            else
            {
                items.Add(ScenarioInspectorItemFactory.Text(
                    "Click any library quest below to add it to your scenario as a scheduled popup."));

                int max = Math.Min(snapshot.Catalog.Count, CatalogPreviewLimit);
                for (int i = 0; i < max; i++)
                {
                    QuestDef quest = snapshot.Catalog[i];
                    if (quest == null)
                        continue;

                    bool available = QuestAuthoringSnapshot.IsQuestAvailable(quest);
                    string suffix = available ? string.Empty : "  (locked)";
                    items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionQuestCatalogAddPrefix + i.ToString(CultureInfo.InvariantCulture),
                        "+ " + quest.id + "   " + quest.questType.ToString() + suffix,
                        "Add this QuestLibrary quest to the scenario draft.",
                        true,
                        false,
                        "Q+")));
                }

                if (snapshot.Catalog.Count > max)
                {
                    items.Add(ScenarioInspectorItemFactory.Text(
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
                items.Add(ScenarioInspectorItemFactory.Property(
                    ScenarioInspectorItemFactory.Safe(quest.definition.id),
                    state));
            }

            if (items.Count == 0)
            {
                items.Add(ScenarioInspectorItemFactory.Text(
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

        private static void AddStageIdActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowDefinition flow, int index)
        {
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                string id = flow.Stages[i] != null ? flow.Stages[i].Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionStoryStageIdPrefix + index.ToString(CultureInfo.InvariantCulture) + "." + Encode(id + "_copy"),
                    "Id " + id + "_copy",
                    "Rename using this stage-id pattern.",
                    true,
                    false,
                    "ID")));
            }
        }

        private static void AddStageRouteActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowDefinition flow, int index, string current)
        {
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryStageUnansweredPrefix + index.ToString(CultureInfo.InvariantCulture) + ".none", "No Unanswered Route", "Clear unanswered stage routing.", true, string.IsNullOrEmpty(current), "UN")));
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                string id = flow.Stages[i] != null ? flow.Stages[i].Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionStoryStageUnansweredPrefix + index.ToString(CultureInfo.InvariantCulture) + "." + Encode(id),
                    "Unanswered -> " + id,
                    "Route unanswered calls to this stage.",
                    true,
                    string.Equals(current, id, StringComparison.OrdinalIgnoreCase),
                    "UR")));
            }
        }

        private static void AddIntercomIdActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, int stageIndex, int intercomIndex)
        {
            string prefix = stageIndex.ToString(CultureInfo.InvariantCulture) + "." + intercomIndex.ToString(CultureInfo.InvariantCulture);
            int count = stage != null && stage.IntercomStages != null ? stage.IntercomStages.Count + 1 : 1;
            string candidate = "step_" + count.ToString(CultureInfo.InvariantCulture);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryIntercomIdPrefix + prefix + "." + Encode(candidate), "Id " + candidate, "Rename using the next step-id pattern.", true, false, "ID")));
        }

        private static void AddIntercomTargetActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, string actionPrefix, string prefix, string label)
        {
            string current = actionPrefix == ScenarioAuthoringActionIds.ActionStoryIntercomAlternatePrefix ? intercom.AlternateNextId : intercom.NextId;
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(actionPrefix + prefix + ".none", label + " None", "Clear this intercom route.", true, string.IsNullOrEmpty(current), label)));
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                string id = stage.IntercomStages[i] != null ? stage.IntercomStages[i].Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(actionPrefix + prefix + "." + Encode(id), label + " -> " + id, "Route to this intercom step.", true, string.Equals(current, id, StringComparison.OrdinalIgnoreCase), label)));
            }
        }

        private static void AddStageChangeTargetActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowDefinition flow, string prefix, string current)
        {
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryStageChangeTargetPrefix + prefix + ".none", "No Stage Change", "Clear delayed stage transition.", true, string.IsNullOrEmpty(current), "SC")));
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                string id = flow.Stages[i] != null ? flow.Stages[i].Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryStageChangeTargetPrefix + prefix + "." + Encode(id), "Stage -> " + id, "Change to this scenario stage.", true, string.Equals(current, id, StringComparison.OrdinalIgnoreCase), "SC")));
            }
        }

        private static void AddIntercomTypeActions(List<ScenarioAuthoringInspectorItem> items, string prefix, string current)
        {
            string[] types = { "Standard", "GiveItems", "RemoveItems", "RecruitCharacters" };
            for (int i = 0; i < types.Length; i++)
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryIntercomTypePrefix + prefix + "." + Encode(types[i]), types[i], "Set intercom step type.", true, string.Equals(current, types[i], StringComparison.OrdinalIgnoreCase), "TY")));
        }

        private static void AddDialogueActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioDialogueLineDefinition line, int stageIndex, int intercomIndex, int lineIndex)
        {
            string prefix = stageIndex.ToString(CultureInfo.InvariantCulture) + "." + intercomIndex.ToString(CultureInfo.InvariantCulture) + "." + lineIndex.ToString(CultureInfo.InvariantCulture);
            items.Add(ScenarioInspectorItemFactory.Property("Dialogue " + (lineIndex + 1).ToString(CultureInfo.InvariantCulture), Safe(line != null ? line.TextKey : null), Safe(line != null ? line.Character : null)));
            string[] speakers = { "Player", "LeadNpc", "Npc2", "Npc3", "Npc4", "BackgroundNpc" };
            for (int i = 0; i < speakers.Length; i++)
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryDialogueSpeakerPrefix + prefix + "." + Encode(speakers[i]), speakers[i], "Set dialogue speaker.", true, line != null && string.Equals(line.Character, speakers[i], StringComparison.OrdinalIgnoreCase), "SP")));
            string key = line != null && !string.IsNullOrEmpty(line.TextKey) ? line.TextKey : "dialogue";
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryDialogueKeyPrefix + prefix + "." + Encode(key + "_copy"), "Key " + key + "_copy", "Use the next localization-key pattern.", true, false, "KY")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryDialogueDeletePrefix + prefix, "Remove Dialogue", "Remove this dialogue line.", true, false, "RM")));
        }

        private static void AddOptionActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioDialogueOptionDefinition option, int stageIndex, int intercomIndex, int optionIndex)
        {
            string prefix = stageIndex.ToString(CultureInfo.InvariantCulture) + "." + intercomIndex.ToString(CultureInfo.InvariantCulture) + "." + optionIndex.ToString(CultureInfo.InvariantCulture);
            items.Add(ScenarioInspectorItemFactory.Property("Option " + (optionIndex + 1).ToString(CultureInfo.InvariantCulture), Safe(option != null ? option.TextKey : null), "Next " + FormatStageTarget(option != null ? option.NextId : null)));
            string key = option != null && !string.IsNullOrEmpty(option.TextKey) ? option.TextKey : "option";
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryOptionKeyPrefix + prefix + "." + Encode(key + "_copy"), "Key " + key + "_copy", "Use the next option-key pattern.", true, false, "KY")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryOptionNextPrefix + prefix + ".none", "Next None", "Clear this option route.", true, option == null || string.IsNullOrEmpty(option.NextId), "NX")));
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                string id = stage.IntercomStages[i] != null ? stage.IntercomStages[i].Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryOptionNextPrefix + prefix + "." + Encode(id), "Option -> " + id, "Route this option to an intercom step.", true, option != null && string.Equals(option.NextId, id, StringComparison.OrdinalIgnoreCase), "NX")));
            }
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryOptionDeletePrefix + prefix, "Remove Option", "Remove this response option.", true, false, "RM")));
        }

        private static void AddRandomRouteActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            string prefix = stageIndex.ToString(CultureInfo.InvariantCulture) + "." + intercomIndex.ToString(CultureInfo.InvariantCulture);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryIntercomRandomAddPrefix + prefix, "Add Random Route", "Add a randomized next-step candidate.", true, false, "R+")));
            for (int r = 0; intercom.RandomizedNextIds != null && r < intercom.RandomizedNextIds.Count; r++)
            {
                string child = prefix + "." + r.ToString(CultureInfo.InvariantCulture);
                items.Add(ScenarioInspectorItemFactory.Property("Random route " + (r + 1).ToString(CultureInfo.InvariantCulture), FormatStageTarget(intercom.RandomizedNextIds[r])));
                for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                {
                    string id = stage.IntercomStages[i] != null ? stage.IntercomStages[i].Id : null;
                    if (!string.IsNullOrEmpty(id))
                        items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryIntercomRandomTargetPrefix + child + "." + Encode(id), "Random -> " + id, "Set this random route target.", true, string.Equals(intercom.RandomizedNextIds[r], id, StringComparison.OrdinalIgnoreCase), "RN")));
                }
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryIntercomRandomDeletePrefix + child, "Remove Random Route", "Remove this randomized route.", true, false, "RM")));
            }
        }

        private static void AddStoryItemActions(List<ScenarioAuthoringInspectorItem> items, string title, List<ItemEntry> entries, string addPrefix, string deletePrefix, string itemPrefix, string quantityPrefix, int stageIndex, int intercomIndex)
        {
            string prefix = stageIndex.ToString(CultureInfo.InvariantCulture) + "." + intercomIndex.ToString(CultureInfo.InvariantCulture);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(addPrefix + prefix, "Add " + title, "Add an item row.", true, false, "I+")));
            List<ScenarioInventoryItemCatalogEntry> catalog = ScenarioInventoryItemCatalog.Build();
            int max = Math.Min(6, catalog.Count);
            for (int e = 0; entries != null && e < entries.Count; e++)
            {
                ItemEntry entry = entries[e];
                string child = prefix + "." + e.ToString(CultureInfo.InvariantCulture);
                ScenarioInventoryItemCatalogEntry resolved = ScenarioInventoryItemCatalog.Resolve(entry != null ? entry.ItemId : null);
                items.Add(ScenarioInspectorItemFactory.Property(title + " " + (e + 1).ToString(CultureInfo.InvariantCulture), resolved.DisplayName, resolved.Detail, "x" + (entry != null ? entry.Quantity : 0).ToString(CultureInfo.InvariantCulture), null, resolved.PreviewSprite));
                for (int i = 0; i < max; i++)
                    items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(itemPrefix + child + "." + Encode(catalog[i].ItemId), catalog[i].DisplayName, "Select this item.", true, entry != null && string.Equals(entry.ItemId, catalog[i].ItemId, StringComparison.OrdinalIgnoreCase), "IT", catalog[i].Detail, null, catalog[i].PreviewSprite)));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(quantityPrefix + child + ".1", "Qty +", "Increase quantity.", true, false, "+")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(quantityPrefix + child + ".-1", "Qty -", "Decrease quantity.", true, false, "-")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(deletePrefix + child, "Remove " + title, "Remove this item row.", true, false, "RM")));
            }
        }

        private static void AddMilestoneActions(List<ScenarioAuthoringInspectorItem> items, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            string prefix = stageIndex.ToString(CultureInfo.InvariantCulture) + "." + intercomIndex.ToString(CultureInfo.InvariantCulture);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryMilestoneAddPrefix + prefix, "Add Milestone", "Add a scenario milestone mutation.", true, false, "M+")));
            for (int i = 0; intercom.SetMilestones != null && i < intercom.SetMilestones.Count; i++)
            {
                ScenarioMilestoneDefinition milestone = intercom.SetMilestones[i];
                string child = prefix + "." + i.ToString(CultureInfo.InvariantCulture);
                string name = milestone != null && !string.IsNullOrEmpty(milestone.Name) ? milestone.Name : "milestone";
                items.Add(ScenarioInspectorItemFactory.Property("Milestone " + (i + 1).ToString(CultureInfo.InvariantCulture), Safe(name)));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryMilestoneNamePrefix + child + "." + Encode(name + "_copy"), "Name " + name + "_copy", "Use the next milestone-name pattern.", true, false, "MN")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryMilestoneDeletePrefix + child, "Remove Milestone", "Remove this milestone.", true, false, "RM")));
            }
        }

        private static void AddEndTypeActions(List<ScenarioAuthoringInspectorItem> items, string prefix, string current)
        {
            string[] types = { "NothingHappens", "GiveItems", "Trade", "Combat", "RecruitCharacters" };
            for (int i = 0; i < types.Length; i++)
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryEndTypePrefix + prefix + "." + Encode(types[i]), "End " + types[i], "Set encounter end option type.", true, string.Equals(current, types[i], StringComparison.OrdinalIgnoreCase), "END")));
        }

        private static List<string> BuildCharacterIds(ScenarioDefinition definition)
        {
            List<string> ids = new List<string>();
            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                string id = definition.ScenarioCharacters[i] != null ? definition.ScenarioCharacters[i].CharacterId : null;
                if (!string.IsNullOrEmpty(id) && !Contains(ids, id))
                    ids.Add(id);
            }
            string[] vanillaSlots = { "LeadNpc", "Npc2", "Npc3", "Npc4", "BackgroundNpc" };
            for (int i = 0; i < vanillaSlots.Length; i++)
                if (!Contains(ids, vanillaSlots[i]))
                    ids.Add(vanillaSlots[i]);
            return ids;
        }

        private static string Encode(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }

        private static bool Contains(List<string> values, string value)
        {
            for (int i = 0; values != null && i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string FormatStageTarget(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value;
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
                    ? "On trigger '" + ScenarioInspectorItemFactory.Safe(quest.StartTriggerId) + "'"
                    : QuestAuthoringHelpers.FormatSchedule(quest.ScheduledStart);
                string sectionTitle = "Quest #" + (index + 1).ToString(CultureInfo.InvariantCulture)
                    + " / " + ScenarioInspectorItemFactory.Safe(title)
                    + " / " + when;
                string validation = QuestAuthoringHelpers.FormatQuestValidation(quest, _snapshot.Definition);

                List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
                items.Add(ScenarioInspectorItemFactory.Property(
                    "Quest id",
                    ScenarioInspectorItemFactory.Safe(quest.Id)));
                items.Add(ScenarioInspectorItemFactory.Property(
                    "Library",
                    libraryQuest != null
                        ? QuestAuthoringHelpers.BuildQuestLibrarySummary(libraryQuest)
                        : "not found"));
                items.Add(ScenarioInspectorItemFactory.Property("Validation", validation));

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
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestStartModePrefix + idPart,
                    "Scheduled",
                    "Start at a specific day and time.",
                    triggerStarted,
                    !triggerStarted,
                    "SC")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
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
                    + " / " + hour.ToString("D2", CultureInfo.InvariantCulture)
                    + ":" + minute.ToString("D2", CultureInfo.InvariantCulture);

                List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleDayPrefix + idPart + ".-1",
                    "Day -",
                    "Move this quest one day earlier.",
                    true,
                    false,
                    "D-")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleDayPrefix + idPart + ".1",
                    "Day +",
                    "Move this quest one day later.",
                    true,
                    false,
                    "D+")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleHourPrefix + idPart + ".-1",
                    "Hr -",
                    "Move this quest one hour earlier.",
                    true,
                    false,
                    "H-")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleHourPrefix + idPart + ".1",
                    "Hr +",
                    "Move this quest one hour later.",
                    true,
                    false,
                    "H+")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleMinutePrefix + idPart + ".-15",
                    "Min -15",
                    "Move this quest fifteen minutes earlier.",
                    true,
                    false,
                    "M-")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestScheduleMinutePrefix + idPart + ".15",
                    "Min +15",
                    "Move this quest fifteen minutes later.",
                    true,
                    false,
                    "M+")));
                items.Add(ScenarioInspectorItemFactory.Property("When", current));

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
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestTriggerCyclePrefix + idPart + ".-1",
                    "< Prev",
                    "Attach this quest to the previous authored trigger.",
                    _snapshot.HasAnyTriggers,
                    false,
                    "TG-")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestTriggerCyclePrefix + idPart + ".1",
                    "Next >",
                    "Attach this quest to the next authored trigger.",
                    _snapshot.HasAnyTriggers,
                    false,
                    "TG+")));
                items.Add(ScenarioInspectorItemFactory.Property(
                    "Trigger",
                    !string.IsNullOrEmpty(quest.StartTriggerId) ? quest.StartTriggerId : "<none>"));
                if (!_snapshot.HasAnyTriggers)
                {
                    items.Add(ScenarioInspectorItemFactory.Text(
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
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestIdCyclePrefix + idPart + ".-1",
                    "< Prev id",
                    "Switch this quest to the previous QuestLibrary id.",
                    true,
                    false,
                    "ID-")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestIdCyclePrefix + idPart + ".1",
                    "Next id >",
                    "Switch this quest to the next QuestLibrary id.",
                    true,
                    false,
                    "ID+")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestTitleSyncPrefix + idPart,
                    "Sync Title",
                    "Copy the QuestLibrary name key into this authored quest title.",
                    libraryQuest != null,
                    false,
                    "NM")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestDescriptionSyncPrefix + idPart,
                    "Sync Desc",
                    "Copy the QuestLibrary description key into this authored quest description.",
                    libraryQuest != null,
                    false,
                    "DS")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestCompletionCyclePrefix + idPart + ".1",
                    "Cycle Completion",
                    "Cycle the optional completion condition reference.",
                    true,
                    !string.IsNullOrEmpty(quest.CompletionConditionId),
                    "CC")));
                items.Add(ScenarioInspectorItemFactory.Property("Title", ScenarioInspectorItemFactory.Safe(quest.Title)));
                items.Add(ScenarioInspectorItemFactory.Property(
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
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestSpawnNowPrefix + idPart,
                    "Spawn Now",
                    "Immediately ask QuestManager to spawn this quest so you can preview the popup.",
                    libraryQuest != null,
                    false,
                    "SP")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestMovePrefix + idPart + ".-1",
                    "Move Up",
                    "Move this quest earlier in the authored list.",
                    index > 0,
                    false,
                    "UP")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestMovePrefix + idPart + ".1",
                    "Move Down",
                    "Move this quest later in the authored list.",
                    true,
                    false,
                    "DN")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionQuestDuplicatePrefix + idPart,
                    "Duplicate",
                    "Copy this authored quest entry.",
                    true,
                    false,
                    "CP")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
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
                    NextScheduledLabel = label + " - " + ScenarioInspectorItemFactory.Safe(title);
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
                return ScenarioScheduleFormatter.Format(time);
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
                    && !ScenarioDefinitionLookup.HasTrigger(definition, quest.StartTriggerId))
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
                    if (!string.IsNullOrEmpty(quest.StartTriggerId) && !ScenarioDefinitionLookup.HasTrigger(definition, quest.StartTriggerId))
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
                return type + " / " + spawn;
            }

            public static bool HasAnyTrigger(ScenarioDefinition definition)
            {
                return definition != null
                    && definition.TriggersAndEvents != null
                    && definition.TriggersAndEvents.Triggers != null
                    && definition.TriggersAndEvents.Triggers.Count > 0;
            }

            private static bool HasCompletionCondition(ScenarioDefinition definition, string conditionId)
            {
                return ScenarioDefinitionLookup.HasCondition(definition, conditionId);
            }
        }
    }
}
