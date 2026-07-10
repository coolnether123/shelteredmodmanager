using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Validation;
using ShelteredAPI.Scenarios.Domain.Story;
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
            ScenarioStoryFlowIssue[] storyIssues = new ScenarioStoryFlowValidationAnalyzer().Analyze(definition);
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();

            sections.Add(BuildStoryOverviewSection(definition, snapshot, storyIssues));
            sections.Add(BuildStoryMapSection(definition, storyIssues));
            sections.Add(BuildStoryToolsSection(definition));
            ScenarioStoryCharacterActorLinkSectionBuilder.AppendSections(sections, definition);
            AppendConversationSections(sections, definition);
            sections.Add(BuildStageFlowSection(definition, storyIssues));
            AppendStoryStageSections(sections, definition, storyIssues);
            sections.Add(BuildSideQuestIntroSection(snapshot));
            sections.Add(BuildToolsSection(snapshot));
            AppendAuthoredQuestSections(sections, snapshot);
            sections.Add(BuildPickerSection(snapshot));
            sections.Add(BuildRuntimeSection());

            return sections.ToArray();
        }

        // === Scenario flow ===

        private static ScenarioAuthoringInspectorSection BuildStoryOverviewSection(ScenarioDefinition definition, QuestAuthoringSnapshot snapshot, ScenarioStoryFlowIssue[] storyIssues)
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
            items.Add(ScenarioInspectorItemFactory.Property("Story warnings", CountIssues(storyIssues).ToString(CultureInfo.InvariantCulture), FirstIssue(storyIssues)));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "story_flow_status",
                Title = "Story Flow",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildStageFlowSection(ScenarioDefinition definition, ScenarioStoryFlowIssue[] storyIssues)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            if (flow == null || flow.Stages == null || flow.Stages.Count == 0)
            {
                items.Add(ScenarioInspectorItemFactory.Text("No starting stage is authored yet. Add a stage to create the vanilla ScenarioDef flow."));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryFocusedEditorActions.ActionStageOpenNew, "Add First Stage", "Create the first story stage and open it in the focused editor.", true, true, "S+")));
            }
            else
            {
                for (int i = 0; i < flow.Stages.Count; i++)
                {
                    ScenarioFlowStageDefinition stage = flow.Stages[i];
                    int warnings = CountStageIssues(storyIssues, i);
                    items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioStoryFocusedEditorActions.StageOpen(i),
                        DisplayStageTitle(stage, i),
                        "Open this stage in the focused story editor.",
                        true,
                        warnings > 0,
                        EncounterBadge(stage),
                        BuildStageCardDetail(stage, i),
                        warnings > 0 ? "!" + warnings.ToString(CultureInfo.InvariantCulture) : "OK")));
                    items.Add(ScenarioInspectorItemFactory.Property("Route " + (i + 1).ToString(CultureInfo.InvariantCulture), BuildOutgoingSummary(flow, stage), FirstStageIssue(storyIssues, i)));
                }
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "story_stage_flow_map",
                Title = "Stage Flow View",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildStoryMapSection(ScenarioDefinition definition, ScenarioStoryFlowIssue[] storyIssues)
        {
            // The primary authoring graph: stages, routes, and problems built from the shared
            // flow analyzer + reference index, laid out deterministically. The renderer keys off
            // the section id "story_map" and draws the carried model.
            ScenarioStoryGraphModel model = ScenarioStoryGraphBuilder.Build(definition, storyIssues);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Text("Story Map shows every stage and how it connects. Click a stage to open its focused editor."));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "story_map",
                Title = "Story Map",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Default,
                Items = items.ToArray(),
                StoryMap = model
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
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = items.ToArray()
            };
        }

        private static void AppendConversationSections(List<ScenarioAuthoringInspectorSection> sections, ScenarioDefinition definition)
        {
            ScenarioConversationAuthoringDefinition conversations = definition != null ? definition.Conversations : null;
            ScenarioConversationSuppressionDefinition settings = conversations != null ? conversations.Settings : null;
            List<ScenarioConversationDefinition> authored = conversations != null ? conversations.Conversations : null;
            if (settings == null)
                settings = new ScenarioConversationSuppressionDefinition();

            sections.Add(BuildConversationOverviewSection(authored, settings));
            if (authored == null || authored.Count == 0)
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "story_conversations_empty",
                    Title = "Conversations",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Text("No authored NPC conversations yet. Add one to create member-to-member chatter or triggered dialogue.")
                    }
                });
                return;
            }

            for (int i = 0; i < authored.Count; i++)
                AppendConversation(sections, definition, authored[i], i, authored.Count);
        }

        private static ScenarioAuthoringInspectorSection BuildConversationOverviewSection(List<ScenarioConversationDefinition> authored, ScenarioConversationSuppressionDefinition settings)
        {
            int count = authored != null ? authored.Count : 0;
            int random = CountConversations(authored, ScenarioConversationTriggerSource.Random);
            int events = CountConversations(authored, ScenarioConversationTriggerSource.Event);
            int timeline = CountConversations(authored, ScenarioConversationTriggerSource.Timeline);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionStoryConversationAdd,
                "Add Conversation",
                "Create an authored member-to-member conversation.",
                true,
                count == 0,
                "C+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionStoryConversationSuppressionToggle,
                "Suppress Vanilla Random",
                "Toggle all vanilla random family chatter while authored conversations are active.",
                true,
                settings != null && settings.SuppressVanillaRandomChatter,
                "VN")));
            AddSuppressionCategoryAction(items, settings, "GenericBantz", "Generic Bantz");
            AddSuppressionCategoryAction(items, settings, "Illness", "Illness");
            items.Add(EditableProperty(
                "Stored topic keys",
                FormatList(settings != null ? settings.SuppressedVanillaTopicKeys : null),
                ScenarioAuthoringActionIds.ActionStoryConversationSuppressionTopicPrefix,
                "Stored only unless a future transpiler maps individual vanilla localization keys."));
            items.Add(ScenarioInspectorItemFactory.Property("Authored", count.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioInspectorItemFactory.Property("Random pool", random.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioInspectorItemFactory.Property("Event / Timeline", events.ToString(CultureInfo.InvariantCulture) + " / " + timeline.ToString(CultureInfo.InvariantCulture)));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "story_conversation_overview",
                Title = "Conversations",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = items.ToArray()
            };
        }

        private static void AppendConversation(List<ScenarioAuthoringInspectorSection> sections, ScenarioDefinition definition, ScenarioConversationDefinition conversation, int index, int count)
        {
            string indexText = index.ToString(CultureInfo.InvariantCulture);
            ScenarioConversationTriggerDefinition trigger = conversation != null ? conversation.Trigger : null;
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Text(FormatConversationSummary(definition, conversation), null, null, null, null, true));
            items.Add(EditableProperty("Conversation id", conversation != null ? conversation.Id : null, ScenarioAuthoringActionIds.ActionStoryConversationIdPrefix + indexText + ".", "Stable id used by StartConversation effects."));
            items.Add(ScenarioInspectorItemFactory.Property("Kind", "Conversation", "A timed sequence of shelter speech bubbles.", "CONVO"));
            items.Add(ScenarioInspectorItemFactory.Property("Validation", ValidateConversation(definition, conversation)));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationPreviewPrefix + indexText, "Run Preview", "Play this conversation now if live members resolve in the authoring world.", true, false, "PV")));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "story_conversation_" + indexText,
                Title = "CONVERSATION " + (index + 1).ToString(CultureInfo.InvariantCulture) + " / " + Safe(conversation != null ? conversation.Id : null),
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = items.ToArray()
            });

            sections.Add(BuildConversationWhenSection(conversation, index));
            AppendConversationParticipants(sections, definition, conversation, index);
            sections.Add(BuildConversationLinesSection(conversation, index));
            if ((conversation != null && conversation.Conditions != null && conversation.Conditions.Count > 0)
                || (conversation != null && conversation.Tags != null && conversation.Tags.Count > 0))
                sections.Add(BuildConversationAdvancedSection(conversation, index));
            sections.Add(BuildConversationFooterSection(index, count));
        }

        private static ScenarioAuthoringInspectorSection BuildConversationWhenSection(ScenarioConversationDefinition conversation, int index)
        {
            string indexText = index.ToString(CultureInfo.InvariantCulture);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Starts", FormatConversationTrigger(conversation != null ? conversation.Trigger : null)));
            AddConversationTriggerActions(items, conversation != null ? conversation.Trigger : null, indexText);
            return Section("story_conversation_when_" + indexText, "WHEN", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static ScenarioAuthoringInspectorSection BuildConversationAdvancedSection(ScenarioConversationDefinition conversation, int index)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; conversation != null && conversation.Conditions != null && i < conversation.Conditions.Count; i++)
            {
                if (conversation.Conditions[i] != null)
                    items.Add(ScenarioInspectorItemFactory.Property("Condition " + (i + 1).ToString(CultureInfo.InvariantCulture), ScenarioTimelineCreatorText.ConditionName(conversation.Conditions[i].Kind), ScenarioTimelineCreatorText.ConditionAdvancedDetail(conversation.Conditions[i].Kind)));
            }
            if (conversation != null && conversation.Tags != null && conversation.Tags.Count > 0)
                items.Add(ScenarioInspectorItemFactory.Property("Raw tags", string.Join(", ", conversation.Tags.ToArray()), "Runtime classification tags."));
            return Section("story_conversation_advanced_" + index.ToString(CultureInfo.InvariantCulture), "CONDITIONS & ADVANCED", ScenarioAuthoringInspectorSectionLayout.FactGrid, items);
        }

        private static ScenarioAuthoringInspectorSection BuildConversationFooterSection(int index, int count)
        {
            string indexText = index.ToString(CultureInfo.InvariantCulture);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationMovePrefix + indexText + ".-1", "Move Up", "Move this conversation earlier.", index > 0, false, "UP")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationMovePrefix + indexText + ".1", "Move Down", "Move this conversation later.", index + 1 < count, false, "DN")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationDuplicatePrefix + indexText, "Duplicate", "Copy this conversation.", true, false, "CP")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationDeletePrefix + indexText, "Remove", "Remove this conversation.", true, false, "RM")));
            return Section("story_conversation_footer_" + indexText, string.Empty, ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static void AppendConversationParticipants(List<ScenarioAuthoringInspectorSection> sections, ScenarioDefinition definition, ScenarioConversationDefinition conversation, int conversationIndex)
        {
            string conversationText = conversationIndex.ToString(CultureInfo.InvariantCulture);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationParticipantAddPrefix + conversationText, "Add Participant", "Add another participant slot.", true, false, "P+")));

            if (conversation == null || conversation.Participants == null || conversation.Participants.Count == 0)
                items.Add(ScenarioInspectorItemFactory.Text("No participants yet - this conversation has nobody to speak its lines."));
            for (int i = 0; conversation != null && conversation.Participants != null && i < conversation.Participants.Count; i++)
                AddParticipantItems(items, definition, conversation, conversationIndex, i);

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "story_conversation_participants_" + conversationText,
                Title = "WHAT / PARTICIPANTS",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = items.ToArray()
            });

            for (int i = 0; conversation != null && conversation.Participants != null && i < conversation.Participants.Count; i++)
            {
                ScenarioConversationParticipantDefinition participant = conversation.Participants[i];
                sections.Add(ScenarioCastMemberPickerBuilder.BuildSection(
                    "story_conversation_actor_" + conversationText + "_" + i.ToString(CultureInfo.InvariantCulture),
                    "Actor ref for " + Safe(participant != null ? participant.Slot : null),
                    definition,
                    true,
                    true,
                    participant != null ? participant.ActorRef : null,
                    ScenarioAuthoringActionIds.ActionStoryConversationParticipantActorPrefix,
                    conversationText + "." + i.ToString(CultureInfo.InvariantCulture),
                    "No cast members exist yet. Add starting/future survivors or use story-character/fallback selectors."));
            }
        }

        private static void AddParticipantItems(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioConversationDefinition conversation, int conversationIndex, int participantIndex)
        {
            ScenarioConversationParticipantDefinition participant = conversation.Participants[participantIndex];
            string pair = conversationIndex.ToString(CultureInfo.InvariantCulture) + "." + participantIndex.ToString(CultureInfo.InvariantCulture);
            items.Add(EditableProperty("Slot " + (participantIndex + 1).ToString(CultureInfo.InvariantCulture), participant != null ? participant.Slot : null, ScenarioAuthoringActionIds.ActionStoryConversationParticipantSlotPrefix + pair + ".", "Speaker slot referenced by lines."));
            items.Add(ScenarioInspectorItemFactory.Property("Binding " + Safe(participant != null ? participant.Slot : null), FormatParticipant(participant)));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationParticipantRequiredPrefix + pair, "Required", "Toggle whether unresolved participant blocks playback.", true, participant != null && participant.Required, "RQ")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationParticipantDeletePrefix + pair, "Remove Slot", "Remove this participant slot.", true, false, "RM")));

            AddStoryCharacterChoices(items, definition, participant, ScenarioAuthoringActionIds.ActionStoryConversationParticipantStoryPrefix + pair + ".");
            AddFallbackChoices(items, participant, ScenarioAuthoringActionIds.ActionStoryConversationParticipantFallbackPrefix + pair + ".");
        }

        private static ScenarioAuthoringInspectorSection BuildConversationLinesSection(ScenarioConversationDefinition conversation, int conversationIndex)
        {
            string conversationText = conversationIndex.ToString(CultureInfo.InvariantCulture);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationLineAddPrefix + conversationText, "Add Line", "Add a timed speech-bubble line.", true, false, "L+")));
            if (conversation == null || conversation.Lines == null || conversation.Lines.Count == 0)
                items.Add(ScenarioInspectorItemFactory.Text("No lines yet - this conversation will play without showing any speech."));
            for (int i = 0; conversation != null && conversation.Lines != null && i < conversation.Lines.Count; i++)
                AddLineItems(items, conversation, conversationIndex, i);

            return new ScenarioAuthoringInspectorSection
            {
                Id = "story_conversation_lines_" + conversationText,
                Title = "WHAT / SCRIPT",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = items.ToArray()
            };
        }

        private static void AddLineItems(List<ScenarioAuthoringInspectorItem> items, ScenarioConversationDefinition conversation, int conversationIndex, int lineIndex)
        {
            ScenarioConversationLineDefinition line = conversation.Lines[lineIndex];
            string pair = conversationIndex.ToString(CultureInfo.InvariantCulture) + "." + lineIndex.ToString(CultureInfo.InvariantCulture);
            items.Add(ScenarioInspectorItemFactory.Property("Line " + (lineIndex + 1).ToString(CultureInfo.InvariantCulture), Safe(line != null ? line.RawText : null), "Speaker " + Safe(line != null ? line.SpeakerSlot : null) + ", delay " + (line != null ? line.DelaySeconds.ToString("0.##", CultureInfo.InvariantCulture) : "0") + "s"));
            items.Add(EditableProperty("Text " + (lineIndex + 1).ToString(CultureInfo.InvariantCulture), line != null ? line.RawText : null, ScenarioAuthoringActionIds.ActionStoryConversationLineTextPrefix + pair + ".", "Raw authored text shown in the speech bubble."));
            for (int i = 0; conversation.Participants != null && i < conversation.Participants.Count; i++)
            {
                string slot = conversation.Participants[i] != null ? conversation.Participants[i].Slot : null;
                if (!string.IsNullOrEmpty(slot))
                {
                    items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionStoryConversationLineSpeakerPrefix + pair + "." + Uri.EscapeDataString(slot),
                        "Speaker " + slot,
                        "Use this participant slot for the line.",
                        true,
                        line != null && string.Equals(line.SpeakerSlot, slot, StringComparison.OrdinalIgnoreCase),
                        "SP")));
                }
            }
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationLineDelayPrefix + pair + ".1", "Delay +1s", "Increase delay before this line.", true, false, "D+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationLineDelayPrefix + pair + ".-1", "Delay -1s", "Decrease delay before this line.", true, false, "D-")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationLineDeletePrefix + pair, "Remove Line", "Remove this line.", true, false, "RM")));
        }

        private static void AddConversationTriggerActions(List<ScenarioAuthoringInspectorItem> items, ScenarioConversationTriggerDefinition trigger, string indexText)
        {
            AddTriggerSource(items, trigger, indexText, ScenarioConversationTriggerSource.Random);
            AddTriggerSource(items, trigger, indexText, ScenarioConversationTriggerSource.Event);
            AddTriggerSource(items, trigger, indexText, ScenarioConversationTriggerSource.Timeline);
            items.Add(EditableProperty("Event trigger id", trigger != null ? trigger.TriggerId : null, ScenarioAuthoringActionIds.ActionStoryConversationTriggerIdPrefix + indexText + ".", "For Event conversations, match an authored trigger id."));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationTriggerWeightPrefix + indexText + ".0.5", "Weight +", "Increase random pool weight.", true, false, "W+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationTriggerWeightPrefix + indexText + ".-0.5", "Weight -", "Decrease random pool weight.", true, false, "W-")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationTriggerCooldownPrefix + indexText + ".1", "Cooldown +", "Increase random cooldown by one day.", true, false, "C+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationTriggerCooldownPrefix + indexText + ".-1", "Cooldown -", "Decrease random cooldown by one day.", true, false, "C-")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationTriggerOncePrefix + indexText, "Once", "Run this conversation only once.", true, trigger != null && trigger.Once, "1X")));
            items.Add(ScenarioInspectorItemFactory.Property("Timeline", trigger != null ? ScenarioScheduleFormatter.Format(trigger.Time) : "unscheduled"));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationTriggerDayPrefix + indexText + ".1", "Day +", "Move timeline one day later.", true, false, "D+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationTriggerDayPrefix + indexText + ".-1", "Day -", "Move timeline one day earlier.", true, false, "D-")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationTriggerHourPrefix + indexText + ".1", "Hour +", "Move timeline one hour later.", true, false, "H+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationTriggerHourPrefix + indexText + ".-1", "Hour -", "Move timeline one hour earlier.", true, false, "H-")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationTriggerMinutePrefix + indexText + ".15", "Min +15", "Move timeline fifteen minutes later.", true, false, "M+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationTriggerMinutePrefix + indexText + ".-15", "Min -15", "Move timeline fifteen minutes earlier.", true, false, "M-")));
        }

        private static void AddTriggerSource(List<ScenarioAuthoringInspectorItem> items, ScenarioConversationTriggerDefinition trigger, string indexText, ScenarioConversationTriggerSource source)
        {
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionStoryConversationTriggerSourcePrefix + indexText + "." + source,
                source.ToString(),
                "Use " + source + " as the conversation trigger source.",
                true,
                trigger != null && trigger.Source == source,
                "TG")));
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

        private static void AppendStoryStageSections(List<ScenarioAuthoringInspectorSection> sections, ScenarioDefinition definition, ScenarioStoryFlowIssue[] storyIssues)
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
                AppendStoryStage(sections, definition, flow, flow.Stages[i], i, characterIds, storyIssues);
        }

        private static void AppendStoryStage(List<ScenarioAuthoringInspectorSection> sections, ScenarioDefinition definition, ScenarioFlowDefinition flow, ScenarioFlowStageDefinition stage, int index, List<string> characterIds, ScenarioStoryFlowIssue[] storyIssues)
        {
            if (stage == null)
                return;

            string indexText = index.ToString(CultureInfo.InvariantCulture);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryFocusedEditorActions.StageOpen(index), "Open Focused Editor", "Edit this stage as a labeled vanilla scenario-stage form.", true, CountStageIssues(storyIssues, index) > 0, "ED")));
            items.Add(ScenarioInspectorItemFactory.Property("Stage title", DisplayStageTitle(stage, index)));
            items.Add(ScenarioInspectorItemFactory.Property("Advanced stage id", Safe(stage.Id)));
            items.Add(ScenarioInspectorItemFactory.Property("Warnings", CountStageIssues(storyIssues, index).ToString(CultureInfo.InvariantCulture), FirstStageIssue(storyIssues, index), CountStageIssues(storyIssues, index) > 0 ? "!" : "OK"));
            ScenarioStoryCharacterActorLinkSectionBuilder.AppendUsages(items, definition, ScenarioReferenceTargetKind.Stage, stage.Id, "Removing this stage is blocked while references exist.");
            items.Add(ScenarioInspectorItemFactory.Property("Characters", stage.CharacterIds != null && stage.CharacterIds.Count > 0 ? string.Join(", ", stage.CharacterIds.ToArray()) : "No characters assigned - choose stage cast in the focused editor."));
            items.Add(ScenarioInspectorItemFactory.Property("Unanswered", FormatStageTarget(stage.UnansweredNextStage) + " / " + stage.UnansweredNextDays.ToString(CultureInfo.InvariantCulture) + " day(s)"));
            AddStageIdActions(items, flow, index);
            AddStageRouteActions(items, flow, index, stage.UnansweredNextStage);
            for (int c = 0; characterIds != null && c < characterIds.Count; c++)
            {
                string id = characterIds[c];
                bool selected = Contains(stage.CharacterIds, id);
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioStoryAuthoringActions.StageCharacterToggle(index, id),
                    FormatCharacterLabel(definition, id),
                    selected ? "Remove this character from the stage." : "Add this character to the stage.",
                    true,
                    selected,
                    "CH")));
            }
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.StageUnansweredDelay(index, 1), "Delay +", "Increase unanswered delay by one day.", true, false, "D+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.StageUnansweredDelay(index, -1), "Delay -", "Decrease unanswered delay by one day.", true, false, "D-")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.StagePunish(index), "Punish Unanswered", "Toggle vanilla unanswered punishment.", true, stage.PunishOnUnanswered, "PU")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.IntercomAdd(index), "Add Step", "Add an intercom step to this stage.", true, false, "I+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.StageMove(index, -1), "Move Up", "Move this stage earlier.", index > 0, false, "UP")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.StageMove(index, 1), "Move Down", "Move this stage later.", index + 1 < flow.Stages.Count, false, "DN")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.StageDuplicate(index), "Duplicate", "Copy this stage.", true, false, "CP")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.StageDelete(index), "Remove", "Remove this stage.", true, false, "RM")));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "story_stage_" + indexText,
                Title = "Stage " + (index + 1).ToString(CultureInfo.InvariantCulture) + " / " + DisplayStageTitle(stage, index),
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = items.ToArray()
            });

            // Read-only script of this stage's scene, so writers can read before they edit.
            sections.Add(ScenarioStoryScriptViewBuilder.BuildStageScript(definition, stage, index));

            // The verbose per-scene editing rows collapse by default: the script above is the
            // readable summary, and the detailed steppers expand on demand to cut the wall.
            for (int i = 0; stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                sections.Add(BuildIntercomSection(definition, flow, stage, stage.IntercomStages[i], index, i, characterIds));
        }

        private static ScenarioAuthoringInspectorSection BuildIntercomSection(ScenarioDefinition definition, ScenarioFlowDefinition flow, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex, List<string> characterIds)
        {
            string prefix = ScenarioStoryAuthoringActions.IntercomKey(stageIndex, intercomIndex);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Step id", Safe(intercom.Id)));
            items.Add(ScenarioInspectorItemFactory.Property("Routes", "Next " + FormatStageTarget(intercom.NextId) + " / Alt " + FormatStageTarget(intercom.AlternateNextId)));
            items.Add(ScenarioInspectorItemFactory.Property("Stage change", intercom.StageChange != null ? FormatStageTarget(intercom.StageChange.Id) + " after " + intercom.StageChange.DelayDays.ToString(CultureInfo.InvariantCulture) + " day(s)" : "No delayed next-stage transition."));
            bool revealAdvancedRouting = ScenarioStoryStageDisclosure.ShouldRevealAdvancedRouting(stage);
            AddIntercomIdActions(items, stage, stageIndex, intercomIndex);
            AddIntercomTargetActions(items, stage, intercom, stageIndex, intercomIndex, false, "Next");
            if (revealAdvancedRouting)
                AddIntercomTargetActions(items, stage, intercom, stageIndex, intercomIndex, true, "Alt");
            AddStageChangeTargetActions(items, flow, stageIndex, intercomIndex, intercom.StageChange != null ? intercom.StageChange.Id : null);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.StageChangeDelay(stageIndex, intercomIndex, 1), "Stage Delay +", "Increase stage-change delay.", true, false, "SD+")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.StageChangeDelay(stageIndex, intercomIndex, -1), "Stage Delay -", "Decrease stage-change delay.", true, false, "SD-")));
            AddIntercomTypeActions(items, stageIndex, intercomIndex, intercom.Type);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.DialogueAdd(stageIndex, intercomIndex), "Add Dialogue", "Add a dialogue key.", true, false, "D+")));
            for (int i = 0; intercom.Dialogue != null && i < intercom.Dialogue.Count; i++)
                AddDialogueActions(items, definition, stage, intercom.Dialogue[i], stageIndex, intercomIndex, i);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.OptionAdd(stageIndex, intercomIndex), "Add Option", "Add a response option.", true, false, "O+")));
            for (int i = 0; intercom.Options != null && i < intercom.Options.Count; i++)
                AddOptionActions(items, stage, intercom.Options[i], stageIndex, intercomIndex, i);
            if (revealAdvancedRouting)
                AddRandomRouteActions(items, stage, intercom, stageIndex, intercomIndex);
            else
                items.Add(ScenarioInspectorItemFactory.Text("Advanced routing (alternate and random routes) unlocks once this stage has a written dialogue line."));
            AddStoryItemActions(items, "Rewards", intercom.Items, false, stageIndex, intercomIndex);
            AddStoryItemActions(items, "Removals", intercom.ItemsToRemove, true, stageIndex, intercomIndex);
            AddMilestoneActions(items, intercom, stageIndex, intercomIndex);
            for (int i = 0; characterIds != null && i < characterIds.Count; i++)
            {
                string id = characterIds[i];
                bool selected = Contains(intercom.CharacterIdsToRecruit, id);
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.RecruitToggle(stageIndex, intercomIndex, id), "Recruit " + FormatCharacterLabel(definition, id), "Toggle recruitment for this character.", true, selected, "RC")));
            }
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.RecruitFamily(stageIndex, intercomIndex), "Recruit As Family", "Toggle family recruitment.", true, intercom.RecruitAsFamily, "RF")));
            AddEndTypeActions(items, stageIndex, intercomIndex, intercom.EndOptions != null ? intercom.EndOptions.Type : null);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.EndCompleteQuest(stageIndex, intercomIndex), "Complete Quest", "Toggle vanilla quest completion.", true, intercom.EndOptions != null && intercom.EndOptions.CompleteQuest, "CQ")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.EndCompleteScenario(stageIndex, intercomIndex), intercom.EndOptions != null && intercom.EndOptions.CompleteParentScenario ? "Clear Complete Scenario" : "Complete Scenario unavailable", "Use Victory conditions to complete the authored scenario.", intercom.EndOptions != null && intercom.EndOptions.CompleteParentScenario, false, "VC")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.IntercomMove(stageIndex, intercomIndex, -1), "Move Step Up", "Move this intercom step earlier.", intercomIndex > 0, false, "UP")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.IntercomMove(stageIndex, intercomIndex, 1), "Move Step Down", "Move this intercom step later.", intercomIndex + 1 < stage.IntercomStages.Count, false, "DN")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.IntercomDuplicate(stageIndex, intercomIndex), "Duplicate Step", "Copy this intercom step.", true, false, "CP")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.IntercomDelete(stageIndex, intercomIndex), "Remove Step", "Remove this intercom step.", true, false, "RM")));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "story_intercom_" + prefix.Replace('.', '_'),
                Title = "Edit Scene " + (intercomIndex + 1).ToString(CultureInfo.InvariantCulture) + " / " + Safe(intercom.Id),
                Expanded = false,
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
                    !string.IsNullOrEmpty(quest.definition.id) ? quest.definition.id : "Running quest with no id",
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
                    ScenarioStoryAuthoringActions.StageId(index, id + "_copy"),
                    "Id " + id + "_copy",
                    "Rename using this stage-id pattern.",
                    true,
                    false,
                    "ID")));
            }
        }

        private static void AddStageRouteActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowDefinition flow, int index, string current)
        {
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.StageUnanswered(index, null), "No Unanswered Route", "Clear unanswered stage routing.", true, string.IsNullOrEmpty(current), "UN")));
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                string id = flow.Stages[i] != null ? flow.Stages[i].Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioStoryAuthoringActions.StageUnanswered(index, id),
                    "Unanswered -> " + id,
                    "Route unanswered calls to this stage.",
                    true,
                    string.Equals(current, id, StringComparison.OrdinalIgnoreCase),
                    "UR")));
            }
        }

        private static void AddIntercomIdActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, int stageIndex, int intercomIndex)
        {
            int count = stage != null && stage.IntercomStages != null ? stage.IntercomStages.Count + 1 : 1;
            string candidate = "step_" + count.ToString(CultureInfo.InvariantCulture);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.IntercomId(stageIndex, intercomIndex, candidate), "Id " + candidate, "Rename using the next step-id pattern.", true, false, "ID")));
        }

        private static void AddIntercomTargetActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex, bool alternate, string label)
        {
            string current = alternate ? intercom.AlternateNextId : intercom.NextId;
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(alternate ? ScenarioStoryAuthoringActions.IntercomAlternate(stageIndex, intercomIndex, null) : ScenarioStoryAuthoringActions.IntercomNext(stageIndex, intercomIndex, null), label + " None", "Clear this intercom route.", true, string.IsNullOrEmpty(current), label)));
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                string id = stage.IntercomStages[i] != null ? stage.IntercomStages[i].Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(alternate ? ScenarioStoryAuthoringActions.IntercomAlternate(stageIndex, intercomIndex, id) : ScenarioStoryAuthoringActions.IntercomNext(stageIndex, intercomIndex, id), label + " -> " + id, "Route to this intercom step.", true, string.Equals(current, id, StringComparison.OrdinalIgnoreCase), label)));
            }
        }

        private static void AddStageChangeTargetActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowDefinition flow, int stageIndex, int intercomIndex, string current)
        {
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.StageChangeTarget(stageIndex, intercomIndex, null), "No Stage Change", "Clear delayed stage transition.", true, string.IsNullOrEmpty(current), "SC")));
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                string id = flow.Stages[i] != null ? flow.Stages[i].Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.StageChangeTarget(stageIndex, intercomIndex, id), "Stage -> " + id, "Change to this scenario stage.", true, string.Equals(current, id, StringComparison.OrdinalIgnoreCase), "SC")));
            }
        }

        private static void AddIntercomTypeActions(List<ScenarioAuthoringInspectorItem> items, int stageIndex, int intercomIndex, string current)
        {
            string[] types = { "Choice", "CheckItems", "CheckMilestone", "Randomizer", "EndEncounter", "EnterCode" };
            for (int i = 0; i < types.Length; i++)
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.IntercomType(stageIndex, intercomIndex, types[i]), types[i], "Set vanilla encounter branch type.", true, string.Equals(current, types[i], StringComparison.OrdinalIgnoreCase), "TY")));
        }

        private static void AddDialogueActions(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioFlowStageDefinition stage, ScenarioDialogueLineDefinition line, int stageIndex, int intercomIndex, int lineIndex)
        {
            items.Add(ScenarioInspectorItemFactory.Property("Dialogue " + (lineIndex + 1).ToString(CultureInfo.InvariantCulture), Safe(line != null ? line.TextKey : null), Safe(line != null ? FormatCharacterLabel(definition, line.Character) : null)));
            List<string> speakers = BuildCharacterIds(definition);
            for (int i = 0; i < speakers.Count; i++)
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.DialogueSpeaker(stageIndex, intercomIndex, lineIndex, speakers[i]), FormatCharacterLabel(definition, speakers[i]), "Set dialogue speaker.", true, line != null && string.Equals(line.Character, speakers[i], StringComparison.OrdinalIgnoreCase), "SP")));
            string key = line != null && !string.IsNullOrEmpty(line.TextKey) ? line.TextKey : "dialogue";
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.DialogueKey(stageIndex, intercomIndex, lineIndex, key + "_copy"), "Key " + key + "_copy", "Use the next localization-key pattern.", true, false, "KY")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.DialogueDelete(stageIndex, intercomIndex, lineIndex), "Remove Dialogue", "Remove this dialogue line.", true, false, "RM")));
        }

        private static void AddOptionActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioDialogueOptionDefinition option, int stageIndex, int intercomIndex, int optionIndex)
        {
            items.Add(ScenarioInspectorItemFactory.Property("Option " + (optionIndex + 1).ToString(CultureInfo.InvariantCulture), Safe(option != null ? option.TextKey : null), "Next " + FormatStageTarget(option != null ? option.NextId : null)));
            string key = option != null && !string.IsNullOrEmpty(option.TextKey) ? option.TextKey : "option";
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.OptionKey(stageIndex, intercomIndex, optionIndex, key + "_copy"), "Key " + key + "_copy", "Use the next option-key pattern.", true, false, "KY")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.OptionNext(stageIndex, intercomIndex, optionIndex, null), "Next None", "Clear this option route.", true, option == null || string.IsNullOrEmpty(option.NextId), "NX")));
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                string id = stage.IntercomStages[i] != null ? stage.IntercomStages[i].Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.OptionNext(stageIndex, intercomIndex, optionIndex, id), "Option -> " + id, "Route this option to an intercom step.", true, option != null && string.Equals(option.NextId, id, StringComparison.OrdinalIgnoreCase), "NX")));
            }
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.OptionDelete(stageIndex, intercomIndex, optionIndex), "Remove Option", "Remove this response option.", true, false, "RM")));
        }

        private static void AddRandomRouteActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.RandomRouteAdd(stageIndex, intercomIndex), "Add Random Route", "Add a randomized next-step candidate.", true, false, "R+")));
            for (int r = 0; intercom.RandomizedNextIds != null && r < intercom.RandomizedNextIds.Count; r++)
            {
                items.Add(ScenarioInspectorItemFactory.Property("Random route " + (r + 1).ToString(CultureInfo.InvariantCulture), FormatStageTarget(intercom.RandomizedNextIds[r])));
                for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                {
                    string id = stage.IntercomStages[i] != null ? stage.IntercomStages[i].Id : null;
                    if (!string.IsNullOrEmpty(id))
                        items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.RandomRouteTarget(stageIndex, intercomIndex, r, id), "Random -> " + id, "Set this random route target.", true, string.Equals(intercom.RandomizedNextIds[r], id, StringComparison.OrdinalIgnoreCase), "RN")));
                }
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.RandomRouteDelete(stageIndex, intercomIndex, r), "Remove Random Route", "Remove this randomized route.", true, false, "RM")));
            }
        }

        private static void AddStoryItemActions(List<ScenarioAuthoringInspectorItem> items, string title, List<ItemEntry> entries, bool removal, int stageIndex, int intercomIndex)
        {
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(removal ? ScenarioStoryAuthoringActions.RemovalAdd(stageIndex, intercomIndex) : ScenarioStoryAuthoringActions.RewardAdd(stageIndex, intercomIndex), "Add " + title, "Add an item row.", true, false, "I+")));
            List<ScenarioInventoryItemCatalogEntry> catalog = ScenarioInventoryItemCatalog.Build();
            int max = Math.Min(6, catalog.Count);
            for (int e = 0; entries != null && e < entries.Count; e++)
            {
                ItemEntry entry = entries[e];
                ScenarioInventoryItemCatalogEntry resolved = ScenarioInventoryItemCatalog.Resolve(entry != null ? entry.ItemId : null);
                items.Add(ScenarioInspectorItemFactory.Property(title + " " + (e + 1).ToString(CultureInfo.InvariantCulture), resolved.DisplayName, resolved.Detail, "x" + (entry != null ? entry.Quantity : 0).ToString(CultureInfo.InvariantCulture), null, resolved.PreviewSprite));
                for (int i = 0; i < max; i++)
                    items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(removal ? ScenarioStoryAuthoringActions.RemovalItem(stageIndex, intercomIndex, e, catalog[i].ItemId) : ScenarioStoryAuthoringActions.RewardItem(stageIndex, intercomIndex, e, catalog[i].ItemId), catalog[i].DisplayName, "Select this item.", true, entry != null && string.Equals(entry.ItemId, catalog[i].ItemId, StringComparison.OrdinalIgnoreCase), "IT", catalog[i].Detail, null, catalog[i].PreviewSprite)));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(removal ? ScenarioStoryAuthoringActions.RemovalQuantity(stageIndex, intercomIndex, e, 1) : ScenarioStoryAuthoringActions.RewardQuantity(stageIndex, intercomIndex, e, 1), "Qty +", "Increase quantity.", true, false, "+")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(removal ? ScenarioStoryAuthoringActions.RemovalQuantity(stageIndex, intercomIndex, e, -1) : ScenarioStoryAuthoringActions.RewardQuantity(stageIndex, intercomIndex, e, -1), "Qty -", "Decrease quantity.", true, false, "-")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(removal ? ScenarioStoryAuthoringActions.RemovalDelete(stageIndex, intercomIndex, e) : ScenarioStoryAuthoringActions.RewardDelete(stageIndex, intercomIndex, e), "Remove " + title, "Remove this item row.", true, false, "RM")));
            }
        }

        private static void AddMilestoneActions(List<ScenarioAuthoringInspectorItem> items, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.MilestoneAdd(stageIndex, intercomIndex), "Add Milestone", "Add a scenario milestone mutation.", true, false, "M+")));
            for (int i = 0; intercom.SetMilestones != null && i < intercom.SetMilestones.Count; i++)
            {
                ScenarioMilestoneDefinition milestone = intercom.SetMilestones[i];
                string name = milestone != null && !string.IsNullOrEmpty(milestone.Name) ? milestone.Name : "milestone";
                items.Add(ScenarioInspectorItemFactory.Property("Milestone " + (i + 1).ToString(CultureInfo.InvariantCulture), Safe(name)));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.MilestoneName(stageIndex, intercomIndex, i, name + "_copy"), "Name " + name + "_copy", "Use the next milestone-name pattern.", true, false, "MN")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.MilestoneDelete(stageIndex, intercomIndex, i), "Remove Milestone", "Remove this milestone.", true, false, "RM")));
            }
        }

        private static void AddEndTypeActions(List<ScenarioAuthoringInspectorItem> items, int stageIndex, int intercomIndex, string current)
        {
            string[] types = { "NothingHappens", "RewardItems", "EnterTrade", "EnterRecruit", "Combat", "CompleteQuest" };
            for (int i = 0; i < types.Length; i++)
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioStoryAuthoringActions.EndType(stageIndex, intercomIndex, types[i]), "End " + types[i], "Set vanilla encounter outcome type.", true, string.Equals(current, types[i], StringComparison.OrdinalIgnoreCase), "END")));
        }

        private static void AddSuppressionCategoryAction(List<ScenarioAuthoringInspectorItem> items, ScenarioConversationSuppressionDefinition settings, string category, string label)
        {
            bool selected = Contains(settings != null ? settings.SuppressedVanillaCategories : null, category);
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionStoryConversationSuppressionCategoryPrefix + Uri.EscapeDataString(category),
                label,
                "Toggle vanilla " + label + " chatter suppression.",
                true,
                selected,
                "SP")));
        }

        private static void AddStoryCharacterChoices(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioConversationParticipantDefinition participant, string actionPrefix)
        {
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                actionPrefix + ScenarioStoryAuthoringActions.NoneToken,
                "Story None",
                "Clear the story character binding for this participant.",
                true,
                participant == null || string.IsNullOrEmpty(participant.StoryCharacterId),
                "CH")));
            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                string id = character != null ? character.CharacterId : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    actionPrefix + Uri.EscapeDataString(id),
                    "Story " + FormatCharacterLabel(definition, id),
                    "Bind this participant to a story character.",
                    true,
                    participant != null && string.Equals(participant.StoryCharacterId, id, StringComparison.OrdinalIgnoreCase),
                    "CH")));
            }
        }

        private static void AddFallbackChoices(List<ScenarioAuthoringInspectorItem> items, ScenarioConversationParticipantDefinition participant, string actionPrefix)
        {
            string[] values = Enum.GetNames(typeof(ScenarioConversationParticipantFallback));
            for (int i = 0; values != null && i < values.Length; i++)
            {
                ScenarioConversationParticipantFallback fallback = ParseFallback(values[i]);
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    actionPrefix + Uri.EscapeDataString(values[i]),
                    "Fallback " + values[i],
                    "Use this selector when no explicit actor resolves.",
                    true,
                    participant != null && participant.Fallback == fallback,
                    "FB")));
            }
        }

        private static ScenarioConversationParticipantFallback ParseFallback(string value)
        {
            try
            {
                return (ScenarioConversationParticipantFallback)Enum.Parse(typeof(ScenarioConversationParticipantFallback), value, true);
            }
            catch
            {
                return ScenarioConversationParticipantFallback.None;
            }
        }

        private static ScenarioAuthoringInspectorItem EditableProperty(string label, string value, string actionPrefix, string hint)
        {
            ScenarioAuthoringInspectorItem item = ScenarioInspectorItemFactory.Property(label, Safe(value), hint);
            item.Editable = true;
            item.Action = ScenarioInspectorItemFactory.Action(actionPrefix, "Edit", hint, true, false, "ED");
            return item;
        }

        private static ScenarioAuthoringInspectorSection Section(string id, string title, ScenarioAuthoringInspectorSectionLayout layout, List<ScenarioAuthoringInspectorItem> items)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = title,
                Expanded = true,
                Layout = layout,
                Items = items.ToArray()
            };
        }

        private static int CountConversations(List<ScenarioConversationDefinition> conversations, ScenarioConversationTriggerSource source)
        {
            int count = 0;
            for (int i = 0; conversations != null && i < conversations.Count; i++)
                if (conversations[i] != null && conversations[i].Trigger != null && conversations[i].Trigger.Source == source)
                    count++;
            return count;
        }

        private static string FormatConversationTrigger(ScenarioConversationTriggerDefinition trigger)
        {
            if (trigger == null)
                return "No trigger settings";
            if (trigger.Source == ScenarioConversationTriggerSource.Event)
                return "Event " + Safe(trigger.TriggerId);
            if (trigger.Source == ScenarioConversationTriggerSource.Timeline)
                return "Timeline " + ScenarioScheduleFormatter.Format(trigger.Time);
            return "Random weight " + trigger.Weight.ToString("0.##", CultureInfo.InvariantCulture)
                + ", cooldown " + trigger.CooldownDays.ToString(CultureInfo.InvariantCulture)
                + " day(s)"
                + (trigger.Once ? ", once" : string.Empty);
        }

        private static string FormatConversationSummary(ScenarioDefinition definition, ScenarioConversationDefinition conversation)
        {
            string when = FormatConversationTrigger(conversation != null ? conversation.Trigger : null);
            if (conversation == null || conversation.Lines == null || conversation.Lines.Count == 0)
                return when + " - no dialogue lines yet.";
            ScenarioConversationLineDefinition first = conversation.Lines[0];
            string speaker = Safe(first != null ? first.SpeakerSlot : null);
            string line = Safe(first != null ? first.RawText : null);
            return when + " - " + speaker + " says: " + line;
        }

        private static string FormatParticipant(ScenarioConversationParticipantDefinition participant)
        {
            if (participant == null)
                return "Missing participant";
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(participant.StoryCharacterId))
                parts.Add("story " + participant.StoryCharacterId);
            if (participant.ActorRef != null)
                parts.Add("actor " + ScenarioCastMemberReferenceCatalog.FormatActorRef(participant.ActorRef));
            parts.Add("fallback " + participant.Fallback);
            parts.Add(participant.Required ? "required" : "optional");
            return string.Join(", ", parts.ToArray());
        }

        private static string FormatList(List<string> values)
        {
            return values != null && values.Count > 0 ? string.Join(", ", values.ToArray()) : string.Empty;
        }

        private static string ValidateConversation(ScenarioDefinition definition, ScenarioConversationDefinition conversation)
        {
            if (conversation == null)
                return "Conversation is empty.";
            if (string.IsNullOrEmpty(conversation.Id))
                return "Missing conversation id.";
            if (conversation.Participants == null || conversation.Participants.Count == 0)
                return "Add at least one participant.";
            for (int i = 0; conversation.Participants != null && i < conversation.Participants.Count; i++)
            {
                ScenarioConversationParticipantDefinition participant = conversation.Participants[i];
                if (participant == null || string.IsNullOrEmpty(participant.Slot))
                    return "Participant " + (i + 1).ToString(CultureInfo.InvariantCulture) + " needs a slot.";
                if (!string.IsNullOrEmpty(participant.StoryCharacterId) && !HasStoryCharacter(definition, participant.StoryCharacterId))
                    return "Participant " + participant.Slot + " references a missing story character.";
            }
            if (conversation.Lines == null || conversation.Lines.Count == 0)
                return "Add at least one line.";
            for (int i = 0; conversation.Lines != null && i < conversation.Lines.Count; i++)
            {
                ScenarioConversationLineDefinition line = conversation.Lines[i];
                if (line == null || (string.IsNullOrEmpty(line.RawText) && string.IsNullOrEmpty(line.TextKey)))
                    return "Line " + (i + 1).ToString(CultureInfo.InvariantCulture) + " has no text.";
                if (line != null && !HasParticipantSlot(conversation, line.SpeakerSlot))
                    return "Line " + (i + 1).ToString(CultureInfo.InvariantCulture) + " references a missing speaker slot.";
            }
            return "Ready.";
        }

        private static bool HasStoryCharacter(ScenarioDefinition definition, string id)
        {
            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
                if (definition.ScenarioCharacters[i] != null && string.Equals(definition.ScenarioCharacters[i].CharacterId, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool HasParticipantSlot(ScenarioConversationDefinition conversation, string slot)
        {
            for (int i = 0; conversation != null && conversation.Participants != null && i < conversation.Participants.Count; i++)
                if (conversation.Participants[i] != null && string.Equals(conversation.Participants[i].Slot, slot, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
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
            string[] vanillaSlots = { "LeadNpc", "Npc2", "Npc3", "Npc4", "BackgroundNpc", "Player" };
            for (int i = 0; i < vanillaSlots.Length; i++)
                if (!Contains(ids, vanillaSlots[i]))
                    ids.Add(vanillaSlots[i]);
            return ids;
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
            return string.IsNullOrEmpty(value) ? "No route selected" : value;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "Blank - choose or generate a value" : value;
        }

        private static int CountIssues(ScenarioStoryFlowIssue[] issues)
        {
            return issues != null ? issues.Length : 0;
        }

        private static string FirstIssue(ScenarioStoryFlowIssue[] issues)
        {
            for (int i = 0; issues != null && i < issues.Length; i++)
                if (issues[i] != null)
                    return issues[i].Message;
            return "No story warnings.";
        }

        private static int CountStageIssues(ScenarioStoryFlowIssue[] issues, int stageIndex)
        {
            int count = 0;
            for (int i = 0; issues != null && i < issues.Length; i++)
                if (issues[i] != null && issues[i].StageIndex == stageIndex)
                    count++;
            return count;
        }

        private static string FirstStageIssue(ScenarioStoryFlowIssue[] issues, int stageIndex)
        {
            for (int i = 0; issues != null && i < issues.Length; i++)
                if (issues[i] != null && issues[i].StageIndex == stageIndex)
                    return issues[i].Message;
            return "No stage warnings.";
        }

        private static string DisplayStageTitle(ScenarioFlowStageDefinition stage, int index)
        {
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition intercom = stage.IntercomStages[i];
                if (intercom != null && !string.IsNullOrEmpty(intercom.StageDescriptionKey))
                    return intercom.StageDescriptionKey;
            }
            if (stage != null && !string.IsNullOrEmpty(stage.Id))
                return stage.Id;
            return "Stage " + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static string EncounterBadge(ScenarioFlowStageDefinition stage)
        {
            string type = null;
            if (stage != null && stage.IntercomStages != null && stage.IntercomStages.Count > 0 && stage.IntercomStages[0] != null)
                type = stage.IntercomStages[0].Type;
            if (string.IsNullOrEmpty(type))
                return "ST";
            return type.Length <= 3 ? type.ToUpperInvariant() : type.Substring(0, 3).ToUpperInvariant();
        }

        private static string BuildStageCardDetail(ScenarioFlowStageDefinition stage, int index)
        {
            int steps = stage != null && stage.IntercomStages != null ? stage.IntercomStages.Count : 0;
            return "day-granular / " + steps.ToString(CultureInfo.InvariantCulture) + " encounter step(s) / " + BuildShortOutgoingSummary(stage);
        }

        private static string BuildShortOutgoingSummary(ScenarioFlowStageDefinition stage)
        {
            if (stage == null)
                return "no outgoing route";
            if (!string.IsNullOrEmpty(stage.UnansweredNextStage))
                return "ignored -> " + stage.UnansweredNextStage;
            for (int i = 0; stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition intercom = stage.IntercomStages[i];
                if (intercom != null && intercom.StageChange != null && !string.IsNullOrEmpty(intercom.StageChange.Id))
                    return "outcome -> " + intercom.StageChange.Id;
            }
            return "no next stage";
        }

        private static string BuildOutgoingSummary(ScenarioFlowDefinition flow, ScenarioFlowStageDefinition stage)
        {
            List<string> routes = new List<string>();
            string source = stage != null && !string.IsNullOrEmpty(stage.Id) ? stage.Id : "this stage";
            if (stage != null && !string.IsNullOrEmpty(stage.UnansweredNextStage))
                routes.Add(source + " --ignored--> " + stage.UnansweredNextStage);
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition intercom = stage.IntercomStages[i];
                if (intercom != null && intercom.StageChange != null && !string.IsNullOrEmpty(intercom.StageChange.Id))
                    routes.Add(source + " --outcome " + (i + 1).ToString(CultureInfo.InvariantCulture) + "--> " + intercom.StageChange.Id);
            }
            return routes.Count > 0 ? string.Join(" | ", routes.ToArray()) : "No outgoing stage arrow - this stage stays active or ends here.";
        }

        private static string FormatCharacterLabel(ScenarioDefinition definition, string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return "<missing>";

            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                if (character == null || !string.Equals(character.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
                    continue;

                string label = !string.IsNullOrEmpty(character.DisplayName) ? character.DisplayName + " [" + characterId + "]" : characterId;

                if (character.ActorRef == null)
                    return label;

                return label + " -> " + ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, character.ActorRef, true, true, label);
            }

            return characterId;
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
                    ? "On trigger '" + (!string.IsNullOrEmpty(quest.StartTriggerId) ? quest.StartTriggerId : "not selected") + "'"
                    : QuestAuthoringHelpers.FormatSchedule(quest.ScheduledStart);
                string sectionTitle = "Quest #" + (index + 1).ToString(CultureInfo.InvariantCulture)
                    + " / " + (!string.IsNullOrEmpty(title) ? title : "Untitled quest popup")
                    + " / " + when;
                string validation = QuestAuthoringHelpers.FormatQuestValidation(quest, _snapshot.Definition);

                List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
                items.Add(ScenarioInspectorItemFactory.Property(
                    "Quest id",
                    !string.IsNullOrEmpty(quest.Id) ? quest.Id : "No QuestLibrary id selected."));
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
                    !string.IsNullOrEmpty(quest.StartTriggerId) ? quest.StartTriggerId : "No trigger selected - this popup starts from its schedule."));
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
                items.Add(ScenarioInspectorItemFactory.Property("Title", !string.IsNullOrEmpty(quest.Title) ? quest.Title : "No title synced yet - use Sync Title when the library id is valid."));
                items.Add(ScenarioInspectorItemFactory.Property(
                    "Completion",
                    !string.IsNullOrEmpty(quest.CompletionConditionId) ? quest.CompletionConditionId : "No completion condition selected - quest completion is not gated."));

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
                    NextScheduledLabel = label + " - " + (!string.IsNullOrEmpty(title) ? title : "Untitled quest popup");
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
