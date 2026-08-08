using ShelteredAPI.Scenarios.Diagnostics;
using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Application.Timeline;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Journal;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredScenarioEditor.Domain.Timeline;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;

namespace ShelteredScenarioEditor.Diagnostics
{
    /// <summary>Contract fixture for Timeline presets, creator summaries, and collision analysis.</summary>
    internal static class ScenarioTimelineUxVerification
    {
        public static void Verify(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.Quests.Quests.Add(new QuestDefinition { Id = "first_quest", Title = "First Quest" });
            string[] presets = { "deliver_supplies", "change_weather", "visitor_arrives", "journal_message", "start_quest", "set_flag" };
            for (int i = 0; i < presets.Length; i++)
            {
                string reason;
                ScenarioScheduledActionDefinition action = ScenarioTimelinePresetService.TryCreateAction(definition, presets[i], out reason);
                Assert(action != null && action.DueTime != null && action.Effects != null && action.Effects.Count > 0,
                    "Timeline preset did not create a runtime-compilable scheduled action: " + presets[i], result);
                if (action != null)
                    definition.ScheduledActions.Add(action);
            }

            ScenarioScheduledActionDefinition delivery = definition.ScheduledActions[0];
            delivery.DueTime.Day = 3;
            delivery.DueTime.Hour = 8;
            delivery.DueTime.Minute = 0;
            Assert(string.Equals(ScenarioTimelineCreatorText.ScheduledActionName(definition, delivery), "Deliver 5 water and 3 canned food", StringComparison.Ordinal),
                "Timeline delivery summary was not creator-friendly.", result);

            ScenarioScheduledActionDefinition removal = new ScenarioScheduledActionDefinition { Id = "remove_water", ActionType = ScenarioEffectKind.RemoveInventory.ToString() };
            removal.DueTime.Day = 3;
            removal.DueTime.Hour = 8;
            removal.DueTime.Minute = 0;
            removal.Effects.Add(new ScenarioEffectDefinition { Kind = ScenarioEffectKind.RemoveInventory, ItemId = "Water", TargetId = "Water", Quantity = 1 });
            definition.ScheduledActions.Add(removal);

            List<ScenarioTimelineEntry> entries = new ScenarioTimelineBuilder().BuildEntries(definition, null);
            bool collisionFound = false;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && !string.IsNullOrEmpty(entries[i].Warning) && entries[i].Warning.IndexOf("Adds and removes Water", StringComparison.Ordinal) >= 0)
                    collisionFound = true;
            Assert(collisionFound, "Timeline collision analysis did not warn about same-time add/remove ordering.", result);

            VerifyPacingAnalysis(result);
            VerifyRibbonDayAndCacheContract(result);
            VerifyMigratedWorkspaceRoutes(result);
            VerifyFocusedNavigationCommands(result);
        }

        private static void VerifyFocusedNavigationCommands(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            TriggerDef trigger = new TriggerDef { Id = "focus-trigger", Type = "dayreached" };
            trigger.Properties.Add(new ScenarioProperty { Key = "day", Value = "2" });
            definition.TriggersAndEvents.Triggers.Add(trigger);

            ScenarioScheduledActionDefinition scheduled = AddFixtureAction(definition, "focus-scheduled", 3);
            JournalEntryDefinition journal = new JournalEntryDefinition { Id = "focus-journal" };
            journal.DueTime.Day = 4;
            definition.Journal.Entries.Add(journal);

            ScenarioEditorSession session = new ScenarioEditorSession { WorkingDefinition = definition };
            ScenarioTimelineBuilder builder = new ScenarioTimelineBuilder();
            ScenarioTimelineNavigationService navigation = new ScenarioTimelineNavigationService(null, null, new ScenarioAuthoringRendererInteractionState());
            TimelineNavigationCommandHandler handler = new TimelineNavigationCommandHandler(new TimelineEditorServiceStub(session), builder, navigation);
            ScenarioAuthoringState state = new ScenarioAuthoringState();

            handler.Handle(state, TimelineNavigationCommand.FocusTrigger(0));
            Assert(string.Equals(state.FocusedEditorKind, "trigger", StringComparison.Ordinal)
                    && state.FocusedEditorIndex == 0
                    && string.Equals(state.TimelineSelectedEntryId, "trigger.focus-trigger", StringComparison.Ordinal),
                "Timeline trigger focus command did not resolve its indexed entry.", result);

            handler.Handle(state, TimelineNavigationCommand.FocusScheduledAction(0));
            Assert(string.Equals(state.FocusedEditorKind, "scheduled_action", StringComparison.Ordinal)
                    && state.FocusedEditorIndex == 0
                    && string.Equals(state.TimelineSelectedEntryId, scheduled.Id, StringComparison.Ordinal),
                "Timeline scheduled-action focus command did not resolve its indexed entry.", result);

            handler.Handle(state, TimelineNavigationCommand.FocusJournalEntry(0));
            Assert(string.Equals(state.FocusedEditorKind, "journal_entry", StringComparison.Ordinal)
                    && state.FocusedEditorIndex == 0
                    && string.Equals(state.TimelineSelectedEntryId, "journal.focus-journal", StringComparison.Ordinal),
                "Timeline journal focus command did not resolve its indexed entry.", result);
        }

        private static void VerifyMigratedWorkspaceRoutes(ScenarioValidationResult result)
        {
            ScenarioAuthoringRendererInteractionState interaction = new ScenarioAuthoringRendererInteractionState();
            ScenarioTimelineNavigationService navigation = new ScenarioTimelineNavigationService(null, null, interaction);
            ScenarioAuthoringState state = new ScenarioAuthoringState();
            string message;

            FocusedStoryCommand storyCommand = FocusedStoryCommand.OpenStage(1);
            ScenarioTimelineEntry story = new ScenarioTimelineEntry
            {
                Id = "story.route",
                Kind = ScenarioTimelineEntryKind.Story,
                FocusAutomationId = storyCommand.AutomationId,
                FocusCommand = storyCommand,
                TargetId = "stage.1"
            };
            navigation.Navigate(state, story, out message);
            Assert(string.Equals(
                    interaction.GetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId),
                    ScenarioStoryFocusedEditorActions.StageEntityId(null, 1),
                    StringComparison.Ordinal),
                "Timeline Story marker did not select the matching Story workspace stage.", result);

            ScenarioTimelineEntry future = new ScenarioTimelineEntry
            {
                Id = "cast.route",
                Kind = ScenarioTimelineEntryKind.Survivor,
                FocusAutomationId = GameplayScheduleCommands.OpenFutureSurvivor(2).AutomationId,
                FocusCommand = GameplayScheduleCommands.OpenFutureSurvivor(2),
                TargetId = "future.2"
            };
            navigation.Navigate(state, future, out message);
            Assert(string.Equals(
                    interaction.GetWorkspaceSelection(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId),
                    ScenarioCastWorkspaceActions.FutureEntityId(null, 2),
                    StringComparison.Ordinal),
                "Timeline survivor marker did not select the matching Cast workspace arrival.", result);

            ScenarioTimelineEntry quest = new ScenarioTimelineEntry
            {
                Id = "quest.route",
                Kind = ScenarioTimelineEntryKind.Quest,
                Title = "Quest quest.armsdealer.name",
                SourceId = "ArmsDealer",
                TargetId = "ArmsDealer"
            };
            navigation.Navigate(state, quest, out message);
            Assert(!string.IsNullOrEmpty(message)
                    && message.IndexOf("quest.armsdealer.name", StringComparison.OrdinalIgnoreCase) < 0,
                "Timeline quest navigation status exposed a raw localization key.", result);
        }

        private static void VerifyRibbonDayAndCacheContract(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.ScenarioFlow.Stages.Add(new ScenarioFlowStageDefinition
            {
                Id = "opening",
                UnansweredNextStage = "middle",
                UnansweredNextDays = 0
            });
            definition.ScenarioFlow.Stages.Add(new ScenarioFlowStageDefinition
            {
                Id = "middle",
                UnansweredNextStage = "ending",
                UnansweredNextDays = 1
            });
            definition.ScenarioFlow.Stages.Add(new ScenarioFlowStageDefinition { Id = "ending" });
            AddFixtureAction(definition, "day_two_change", 2);

            ScenarioTimelineBuilder timelineBuilder = new ScenarioTimelineBuilder();
            ScenarioDayTimelineRibbonViewModelBuilder ribbonBuilder = new ScenarioDayTimelineRibbonViewModelBuilder(timelineBuilder);
            ScenarioEditorSession session = new ScenarioEditorSession { WorkingDefinition = definition };
            ScenarioDayTimelineRibbonViewModel ribbon = ribbonBuilder.Build(session);
            ScenarioDayTimelineRibbonViewModel cachedRibbon = ribbonBuilder.Build(session);
            Assert(object.ReferenceEquals(ribbon, cachedRibbon),
                "Timeline ribbon rebuilt without a definition or draft-revision change.", result);

            List<ScenarioTimelineDay> timelineDays = timelineBuilder.BuildDays(definition, null);
            for (int i = 0; i < timelineDays.Count; i++)
            {
                ScenarioTimelineDay timelineDay = timelineDays[i];
                if (timelineDay == null || timelineDay.Day < ribbon.FirstDay || timelineDay.Day > ribbon.LastDay)
                    continue;
                ScenarioDayTimelineRibbonDayViewModel ribbonDay = ribbon.Days[timelineDay.Day - ribbon.FirstDay];
                Assert(ribbonDay != null && ribbonDay.MarkerCount == timelineDay.Entries.Count,
                    "Timeline ribbon day counts drifted from ScenarioTimelineBuilder.BuildDays for day " + timelineDay.Day + ".", result);
            }

            Assert(ribbon.Days[0].ChapterCount == 1 && ribbon.Days[1].ChapterCount == 1,
                "Zero-based story delays did not map to scenario days 1 and 2.", result);

            AddFixtureAction(definition, "day_three_change", 3);
            session.MarkDraftChanged(ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            ScenarioDayTimelineRibbonViewModel refreshedRibbon = ribbonBuilder.Build(session);
            Assert(!object.ReferenceEquals(ribbon, refreshedRibbon) && refreshedRibbon.Days[2].MarkerCount == 1,
                "Timeline ribbon cache did not refresh after DraftRevision advanced.", result);
        }

        private static void VerifyPacingAnalysis(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            AddFixtureAction(definition, "opening_1", 1);
            AddFixtureAction(definition, "opening_2", 1);
            AddFixtureAction(definition, "opening_3", 1);
            AddFixtureAction(definition, "opening_4", 2);
            AddFixtureAction(definition, "opening_5", 2);
            ScenarioConversationDefinition conversation = new ScenarioConversationDefinition { Id = "late_story" };
            conversation.Trigger.Source = ScenarioConversationTriggerSource.Timeline;
            conversation.Trigger.Time.Day = 9;
            definition.Conversations.Conversations.Add(conversation);
            FutureSurvivorDefinition arrival = new FutureSurvivorDefinition { Id = "late_arrival" };
            arrival.Arrival.Day = 9;
            definition.FamilySetup.FutureSurvivors.Add(arrival);
            ScenarioScheduledActionDefinition finalWorldEvent = AddFixtureAction(definition, "final_world_event", 10);
            finalWorldEvent.Effects.Add(new ScenarioEffectDefinition { Kind = ScenarioEffectKind.WorldEvent, TargetId = "finale" });

            ScenarioPacingAnalysis analysis = new ScenarioPacingAnalysisService(new ScenarioTimelineBuilder()).Analyze(definition);
            Assert(analysis.TotalAuthoredHappenings == 8
                    && analysis.GetCount(1) == 3
                    && analysis.GetCount(2) == 2
                    && analysis.GetCount(9) == 2
                    && analysis.GetCount(10) == 1,
                "Pacing density counts drifted from the authored fixture.", result);
            Assert(analysis.FirstAuthoredDay == 1
                    && analysis.LastAuthoredDay == 10
                    && analysis.LongestQuietDayCount == 6
                    && analysis.LongestQuietStartDay == 3
                    && analysis.LongestQuietEndDay == 8,
                "Pacing quiet-stretch analysis drifted from the authored fixture.", result);
            Assert(analysis.BusiestCount == 3
                    && analysis.BusiestDays.Length == 1
                    && analysis.BusiestDays[0] == 1,
                "Pacing busiest-day analysis drifted from the authored fixture.", result);
            Assert(string.Equals(analysis.Reading, "Busy start (5 events days 1-2), quiet days 3-8, nothing after day 10", StringComparison.Ordinal),
                "Pacing creator reading drifted from the authored fixture.", result);
            Assert(!string.IsNullOrEmpty(analysis.QuietCallout)
                    && analysis.QuietCallout.IndexOf("6 quiet days in a row", StringComparison.Ordinal) >= 0,
                "Pacing quiet guidance did not describe the fixture gap.", result);
            Assert(!string.IsNullOrEmpty(analysis.EndingCallout)
                    && analysis.EndingCallout.IndexOf("Nothing authored after day 10", StringComparison.Ordinal) >= 0,
                "Pacing ending guidance did not describe an open-ended fixture.", result);

            definition.WinLossConditions.WinConditions.Add(new ScenarioConditionRef { Id = "survive", Kind = ScenarioConditionKind.SurviveDays, Quantity = 1 });
            ScenarioPacingAnalysis ended = new ScenarioPacingAnalysisService(new ScenarioTimelineBuilder()).Analyze(definition);
            Assert(string.IsNullOrEmpty(ended.EndingCallout),
                "Pacing ending guidance remained after the fixture gained an end condition.", result);
        }

        private static ScenarioScheduledActionDefinition AddFixtureAction(ScenarioDefinition definition, string id, int day)
        {
            ScenarioScheduledActionDefinition action = new ScenarioScheduledActionDefinition { Id = id, ActionType = "Fixture" };
            action.DueTime.Day = day;
            action.DueTime.Hour = 8;
            definition.ScheduledActions.Add(action);
            return action;
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition && result != null)
                result.AddError(message);
        }

        private sealed class TimelineEditorServiceStub : IScenarioEditorService
        {
            public TimelineEditorServiceStub(ScenarioEditorSession session) { CurrentSession = session; }
            public ScenarioEditorSession CurrentSession { get; private set; }
            public ScenarioEditorSession EnterEditMode(ScenarioBaseGameMode baseMode) { return CurrentSession; }
            public ScenarioEditorSession LoadEditMode(string scenarioFilePath) { return CurrentSession; }
            public ScenarioValidationResult CommitChanges(string scenarioFilePath) { return new ScenarioValidationResult(); }
            public ScenarioEditorPlaytestResult BeginPlaytest() { return ScenarioEditorPlaytestResult.Failed("Not used by timeline verification."); }
            public void EndPlaytest() { }
            public void ConvertToNormalSave() { }
            public void RequestRestart() { }
            public void CloseEditor(bool resumeGame) { }
            public void MaintainAuthoringPause() { }
        }
    }
}
