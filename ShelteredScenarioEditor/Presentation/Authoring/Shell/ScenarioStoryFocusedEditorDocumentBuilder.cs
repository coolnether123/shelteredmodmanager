using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Validation;
using ShelteredScenarioEditor.Domain.Validation;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Presentation.Inspector;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal static class ScenarioStoryFocusedEditorDocumentBuilder
    {
        private const int ItemPickerLimit = 6;

        internal static ScenarioAuthoringInspectorSection[] BuildStageWorkspaceSections(
            ScenarioDefinition definition,
            int stageIndex,
            ScenarioStoryFlowIssue[] issues)
        {
            ScenarioFlowStageDefinition stage = GetStage(definition, stageIndex);
            if (stage == null)
                return new ScenarioAuthoringInspectorSection[0];

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            List<ScenarioAuthoringInspectorItem> overview = new List<ScenarioAuthoringInspectorItem>();
            overview.Add(Text(BuildStageSummary(definition, stage)));
            overview.Add(Property("Title", DisplayStageTitle(stage, stageIndex)));
            overview.Add(Property("Scenes", (stage.IntercomStages != null ? stage.IntercomStages.Count : 0).ToString(CultureInfo.InvariantCulture)));
            overview.Add(Property("Warnings", CountStageIssues(issues, stageIndex).ToString(CultureInfo.InvariantCulture), "Use the Story Flow warning list to open each affected stage or scene."));
            overview.Add(ActionItem(Action(StoryAuthoringCommands.SetStageTitle(stageIndex, "stage_" + (stageIndex + 1).ToString(CultureInfo.InvariantCulture) + "_title"), "Use Generated Title", "Create a stage-specific title value.", true, false, "TT")));
            overview.Add(ActionItem(Action(StoryAuthoringCommands.IntercomAdd(stageIndex), "Add Scene", "Add another scene to this stage.", true, stage.IntercomStages == null || stage.IntercomStages.Count == 0, "S+")));
            sections.Add(Section("story_stage_overview_" + stageIndex.ToString(CultureInfo.InvariantCulture), "OVERVIEW", ScenarioAuthoringInspectorSectionLayout.FactGrid, overview));

            List<ScenarioAuthoringInspectorItem> timing = new List<ScenarioAuthoringInspectorItem>();
            timing.Add(Property("Ignored-call delay", stage.UnansweredNextDays.ToString(CultureInfo.InvariantCulture) + " day(s)", "Vanilla Story stages advance in whole days."));
            timing.Add(Property("Ignored-call consequence", stage.PunishOnUnanswered ? "Punish after the visitor arrives" : "No explicit punishment"));
            timing.Add(ActionItem(Action(StoryAuthoringCommands.StageUnansweredDelay(stageIndex, 1), "Delay +1 Day", "Increase the ignored-call delay.", true, false, "D+")));
            timing.Add(ActionItem(Action(StoryAuthoringCommands.StageUnansweredDelay(stageIndex, -1), "Delay -1 Day", "Decrease the ignored-call delay.", true, false, "D-")));
            timing.Add(ActionItem(Action(StoryAuthoringCommands.StagePunish(stageIndex), "Punish If Ignored", "Toggle the vanilla ignored-call punishment.", true, stage.PunishOnUnanswered, "PU")));
            sections.Add(Section("story_stage_timing_" + stageIndex.ToString(CultureInfo.InvariantCulture), "TIMING", ScenarioAuthoringInspectorSectionLayout.ActionStrip, timing));

            List<ScenarioAuthoringInspectorItem> cast = new List<ScenarioAuthoringInspectorItem>();
            cast.Add(Property("Appearing characters", FormatStageCast(definition, stage)));
            cast.Add(ChoiceItem(BuildCharacterChoice(definition, stage, stageIndex)));
            sections.Add(Section("story_stage_cast_" + stageIndex.ToString(CultureInfo.InvariantCulture), "CAST", ScenarioAuthoringInspectorSectionLayout.ActionStrip, cast));

            List<ScenarioAuthoringInspectorItem> routing = new List<ScenarioAuthoringInspectorItem>();
            routing.Add(Property("If the call is ignored", FormatIgnoredCall(definition, stage)));
            routing.Add(ChoiceItem(BuildStageRouteChoice(definition, stageIndex, stage.UnansweredNextStage, true, -1)));
            sections.Add(Section("story_stage_routing_" + stageIndex.ToString(CultureInfo.InvariantCulture), "ROUTING", ScenarioAuthoringInspectorSectionLayout.ActionStrip, routing));

            List<ScenarioAuthoringInspectorItem> advanced = new List<ScenarioAuthoringInspectorItem>();
            advanced.Add(Property("Internal stage id", Empty(stage.Id, "Blank")));
            advanced.Add(Property("Raw ignored-call route", Empty(stage.UnansweredNextStage, "None")));
            for (int i = 0; stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition scene = stage.IntercomStages[i];
                if (scene != null && !string.IsNullOrEmpty(scene.StageDescriptionKey))
                    advanced.Add(Property("Technical title key " + (i + 1).ToString(CultureInfo.InvariantCulture), scene.StageDescriptionKey));
            }
            AddStageIdActions(advanced, definition != null ? definition.ScenarioFlow : null, stageIndex);
            advanced.Add(ActionItem(Action(StoryAuthoringCommands.StageMove(stageIndex, -1), "Move Stage Up", "Move this stage earlier.", stageIndex > 0, false, "UP")));
            int stageCount = definition != null && definition.ScenarioFlow != null && definition.ScenarioFlow.Stages != null ? definition.ScenarioFlow.Stages.Count : 0;
            advanced.Add(ActionItem(Action(StoryAuthoringCommands.StageMove(stageIndex, 1), "Move Stage Down", "Move this stage later.", stageIndex + 1 < stageCount, false, "DN")));
            advanced.Add(ActionItem(Action(StoryAuthoringCommands.StageDuplicate(stageIndex), "Duplicate Stage", "Copy this stage and all of its scenes.", true, false, "CP")));
            advanced.Add(ActionItem(Action(StoryAuthoringCommands.StageDelete(stageIndex), "Remove Stage", "Remove this stage when no references remain.", true, false, "RM")));
            sections.Add(AdvancedSection("story_stage_advanced_" + stageIndex.ToString(CultureInfo.InvariantCulture), "ADVANCED", ScenarioAuthoringInspectorSectionLayout.ActionStrip, advanced));
            return sections.ToArray();
        }

        internal static ScenarioAuthoringInspectorSection[] BuildSceneWorkspaceSections(
            ScenarioDefinition definition,
            int stageIndex,
            int sceneIndex,
            ScenarioStoryFlowIssue[] issues)
        {
            ScenarioFlowStageDefinition stage = GetStage(definition, stageIndex);
            ScenarioIntercomStageDefinition scene = stage != null && stage.IntercomStages != null && sceneIndex >= 0 && sceneIndex < stage.IntercomStages.Count
                ? stage.IntercomStages[sceneIndex]
                : null;
            if (scene == null)
                return new ScenarioAuthoringInspectorSection[0];

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            List<ScenarioAuthoringInspectorItem> dialogue = new List<ScenarioAuthoringInspectorItem>();
            dialogue.Add(ActionItem(Action(StoryAuthoringCommands.DialogueAdd(stageIndex, sceneIndex), "Add Dialogue Line", "Add a line spoken during this scene.", true, scene.Dialogue == null || scene.Dialogue.Count == 0, "D+")));
            if (scene.Dialogue == null || scene.Dialogue.Count == 0)
                dialogue.Add(Text("No dialogue yet. Add the first line to begin this scene."));
            for (int i = 0; scene.Dialogue != null && i < scene.Dialogue.Count; i++)
            {
                ScenarioDialogueLineDefinition line = scene.Dialogue[i];
                dialogue.Add(Property("Dialogue " + (i + 1).ToString(CultureInfo.InvariantCulture), ResolveText(line != null ? line.TextKey : null, "Dialogue " + (i + 1).ToString(CultureInfo.InvariantCulture)), line != null && !string.IsNullOrEmpty(line.Character) ? FormatCharacterLabel(definition, line.Character) : "No speaker selected"));
                dialogue.Add(ChoiceItem(BuildDialogueSpeakerChoice(definition, line, stageIndex, sceneIndex, i)));
                dialogue.Add(ActionItem(Action(StoryAuthoringCommands.DialogueDelete(stageIndex, sceneIndex, i), "Remove Dialogue " + (i + 1).ToString(CultureInfo.InvariantCulture), "Remove this line.", true, false, "RM")));
            }
            sections.Add(Section("story_scene_dialogue_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + sceneIndex.ToString(CultureInfo.InvariantCulture), "DIALOGUE", ScenarioAuthoringInspectorSectionLayout.ActionStrip, dialogue));

            List<ScenarioAuthoringInspectorItem> choices = new List<ScenarioAuthoringInspectorItem>();
            choices.Add(ActionItem(Action(StoryAuthoringCommands.OptionAdd(stageIndex, sceneIndex), "Add Player Choice", "Add a response choice and route.", true, scene.Options == null || scene.Options.Count == 0, "C+")));
            if (scene.Options == null || scene.Options.Count == 0)
                choices.Add(Text("No player choices. The scene can continue through its success or alternate route."));
            for (int i = 0; scene.Options != null && i < scene.Options.Count; i++)
            {
                ScenarioDialogueOptionDefinition option = scene.Options[i];
                choices.Add(Property("Choice " + (i + 1).ToString(CultureInfo.InvariantCulture), ResolveText(option != null ? option.TextKey : null, "Choice " + (i + 1).ToString(CultureInfo.InvariantCulture)), "Routes to " + FormatIntercomTarget(stage, option != null ? option.NextId : null)));
                choices.Add(ChoiceItem(BuildSceneRouteChoice(stage, stageIndex, sceneIndex, option != null ? option.NextId : null, "Choice route", delegate(string target) { return StoryAuthoringCommands.OptionNext(stageIndex, sceneIndex, i, target); })));
                choices.Add(ActionItem(Action(StoryAuthoringCommands.OptionDelete(stageIndex, sceneIndex, i), "Remove Choice " + (i + 1).ToString(CultureInfo.InvariantCulture), "Remove this response.", true, false, "RM")));
            }
            sections.Add(Section("story_scene_choices_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + sceneIndex.ToString(CultureInfo.InvariantCulture), "CHOICES", ScenarioAuthoringInspectorSectionLayout.ActionStrip, choices));

            List<ScenarioAuthoringInspectorItem> outcome = new List<ScenarioAuthoringInspectorItem>();
            outcome.Add(ChoiceItem(BuildIntercomTypeChoice(stageIndex, sceneIndex, scene.Type)));
            outcome.Add(ChoiceItem(BuildSceneRouteChoice(stage, stageIndex, sceneIndex, scene.NextId, "Success route", delegate(string target) { return StoryAuthoringCommands.IntercomNext(stageIndex, sceneIndex, target); })));
            outcome.Add(ChoiceItem(BuildSceneRouteChoice(stage, stageIndex, sceneIndex, scene.AlternateNextId, "Alternate route", delegate(string target) { return StoryAuthoringCommands.IntercomAlternate(stageIndex, sceneIndex, target); })));
            ScenarioEncounterEndOptionsDefinition end = scene.EndOptions;
            outcome.Add(ChoiceItem(BuildEndTypeChoice(stageIndex, sceneIndex, end != null ? end.Type : null)));
            outcome.Add(ChoiceItem(BuildStageRouteChoice(definition, stageIndex, scene.StageChange != null ? scene.StageChange.Id : null, false, sceneIndex)));
            outcome.Add(Property("Stage-change delay", (scene.StageChange != null ? scene.StageChange.DelayDays : 0).ToString(CultureInfo.InvariantCulture) + " day(s)"));
            outcome.Add(ActionItem(Action(StoryAuthoringCommands.StageChangeDelay(stageIndex, sceneIndex, 1), "Stage Delay +1 Day", "Increase the delayed stage transition.", true, false, "D+")));
            outcome.Add(ActionItem(Action(StoryAuthoringCommands.StageChangeDelay(stageIndex, sceneIndex, -1), "Stage Delay -1 Day", "Decrease the delayed stage transition.", true, false, "D-")));
            AddStoryItemActions(outcome, "Required items", scene.Items, false, stageIndex, sceneIndex);
            AddStoryItemActions(outcome, "Swap/remove items", scene.ItemsToRemove, true, stageIndex, sceneIndex);
            AddEndOptionItemActions(outcome, "Reward items", end != null ? end.RewardItems : null, true, stageIndex, sceneIndex);
            AddEndOptionItemActions(outcome, "Trade items", end != null ? end.TradeItems : null, false, stageIndex, sceneIndex);
            AddRecruitActions(outcome, definition, scene, stageIndex, sceneIndex);
            sections.Add(Section("story_scene_outcome_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + sceneIndex.ToString(CultureInfo.InvariantCulture), "OUTCOME", ScenarioAuthoringInspectorSectionLayout.ActionStrip, outcome));

            List<ScenarioAuthoringInspectorItem> advanced = new List<ScenarioAuthoringInspectorItem>();
            advanced.Add(Property("Internal scene id", Empty(scene.Id, "Blank")));
            advanced.Add(Property("Technical scene title key", Empty(scene.StageDescriptionKey, "Blank")));
            advanced.Add(Property("Raw branch type", Empty(scene.Type, "Default")));
            advanced.Add(Property("Raw success route", Empty(scene.NextId, "None")));
            advanced.Add(Property("Raw alternate route", Empty(scene.AlternateNextId, "None")));
            advanced.Add(Property("Raw stage-change target", scene.StageChange != null ? Empty(scene.StageChange.Id, "None") : "None"));
            advanced.Add(Property("Raw end type", end != null ? Empty(end.Type, "Default") : "Default"));
            for (int i = 0; scene.Dialogue != null && i < scene.Dialogue.Count; i++)
                if (scene.Dialogue[i] != null && !string.IsNullOrEmpty(scene.Dialogue[i].TextKey)) advanced.Add(Property("Technical dialogue key " + (i + 1).ToString(CultureInfo.InvariantCulture), scene.Dialogue[i].TextKey));
            for (int i = 0; scene.Options != null && i < scene.Options.Count; i++)
                if (scene.Options[i] != null && !string.IsNullOrEmpty(scene.Options[i].TextKey)) advanced.Add(Property("Technical choice key " + (i + 1).ToString(CultureInfo.InvariantCulture), scene.Options[i].TextKey));
            advanced.Add(Property("Warnings", CountIntercomIssues(issues, stageIndex, sceneIndex).ToString(CultureInfo.InvariantCulture)));
            advanced.Add(ActionItem(Action(StoryAuthoringCommands.IntercomMove(stageIndex, sceneIndex, -1), "Move Scene Up", "Move this scene earlier.", sceneIndex > 0, false, "UP")));
            advanced.Add(ActionItem(Action(StoryAuthoringCommands.IntercomMove(stageIndex, sceneIndex, 1), "Move Scene Down", "Move this scene later.", sceneIndex + 1 < stage.IntercomStages.Count, false, "DN")));
            advanced.Add(ActionItem(Action(StoryAuthoringCommands.IntercomDuplicate(stageIndex, sceneIndex), "Duplicate Scene", "Copy this scene.", true, false, "CP")));
            advanced.Add(ActionItem(Action(StoryAuthoringCommands.IntercomDelete(stageIndex, sceneIndex), "Remove Scene", "Remove this scene.", true, false, "RM")));
            sections.Add(AdvancedSection("story_scene_advanced_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + sceneIndex.ToString(CultureInfo.InvariantCulture), "ADVANCED", ScenarioAuthoringInspectorSectionLayout.ActionStrip, advanced));
            return sections.ToArray();
        }

        private static ScenarioAuthoringInspectorSection BuildIdentitySection(ScenarioDefinition definition, ScenarioFlowStageDefinition stage, int stageIndex, ScenarioStoryFlowIssue[] issues)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Text(BuildStageSummary(definition, stage)));
            items.Add(Property("Author title", DisplayStageTitle(stage, stageIndex), "Stored as the first encounter step's stage description key."));
            items.Add(Property("Kind", "Story stage", "A day-granular chapter in the vanilla scenario flow.", "STAGE"));
            ScenarioStoryCharacterActorLinkSectionBuilder.AppendUsages(items, definition, ScenarioReferenceTargetKind.Stage, stage.Id, "Removing this stage is blocked while references exist.");
            items.Add(Property("Timing", "Day-granular", "Vanilla stages advance by day; delayed transitions use whole days."));
            items.Add(Property("Encounter type", PrimaryEncounterType(stage), "Type of the first encounter step."));
            items.Add(Property("Warnings", CountStageIssues(issues, stageIndex).ToString(CultureInfo.InvariantCulture), FirstStageIssue(issues, stageIndex)));
            items.Add(ActionItem(Action(StoryAuthoringCommands.SetStageTitle(stageIndex, "stage_" + (stageIndex + 1).ToString(CultureInfo.InvariantCulture) + "_title"), "Use Generated Title", "Fill the title key with a stage-specific label.", true, false, "TT")));
            return Section("story_focused_identity", "STAGE", ScenarioAuthoringInspectorSectionLayout.FactGrid, items);
        }

        private static ScenarioAuthoringInspectorSection BuildWhenSection(ScenarioDefinition definition, ScenarioFlowStageDefinition stage, int stageIndex)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("If the player never answers", FormatIgnoredCall(definition, stage), "Vanilla can keep the stage active when no explicit route is selected."));
            items.Add(Property("Delay", stage.UnansweredNextDays.ToString(CultureInfo.InvariantCulture) + " day(s)", "Vanilla checks stage start by scenario day."));
            items.Add(Property("Punishment", stage.PunishOnUnanswered ? "Punish after the NPC has visited" : "No explicit punishment", "Vanilla has an unanswered double-increment edge case; test ignored calls live."));
            AddStagePicker(items, definition, stageIndex, stage.UnansweredNextStage, true);
            items.Add(ActionItem(Action(StoryAuthoringCommands.StageUnansweredDelay(stageIndex, 1), "Delay +", "Increase ignored-call delay by one day.", true, false, "D+")));
            items.Add(ActionItem(Action(StoryAuthoringCommands.StageUnansweredDelay(stageIndex, -1), "Delay -", "Decrease ignored-call delay by one day.", true, false, "D-")));
            items.Add(ActionItem(Action(StoryAuthoringCommands.StagePunish(stageIndex), "Toggle Punishment", "Toggle vanilla punishment for unanswered visited NPCs.", true, stage.PunishOnUnanswered, "PU")));
            return Section("story_focused_when", "WHEN / IGNORED CALL", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static ScenarioAuthoringInspectorSection BuildCastSection(ScenarioDefinition definition, ScenarioFlowStageDefinition stage, int stageIndex)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            List<string> ids = BuildCharacterIds(definition);
            items.Add(Property("Stage cast", stage.CharacterIds != null && stage.CharacterIds.Count > 0 ? string.Join(", ", stage.CharacterIds.ToArray()) : "No characters assigned - choose who appears in this stage."));
            for (int i = 0; ids != null && i < ids.Count; i++)
            {
                string id = ids[i];
                bool selected = Contains(stage.CharacterIds, id);
                items.Add(ActionItem(Action(StoryAuthoringCommands.StageCharacterToggle(stageIndex, id), FormatCharacterLabel(definition, id), selected ? "Remove this character from the stage." : "Add this character to the stage.", true, selected, "CH")));
            }
            return Section("story_focused_cast", "WHAT / CAST", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static ScenarioAuthoringInspectorSection BuildEncounterHeaderSection(
            ScenarioDefinition definition,
            ScenarioFlowStageDefinition stage,
            ScenarioIntercomStageDefinition intercom,
            int stageIndex,
            int intercomIndex,
            ScenarioStoryFlowIssue[] issues)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            string label = "Step " + (intercomIndex + 1).ToString(CultureInfo.InvariantCulture);
            items.Add(Text(BuildIntercomSummary(definition, stage, intercom)));
            items.Add(Property("Encounter step", label, CountIntercomIssues(issues, stageIndex, intercomIndex).ToString(CultureInfo.InvariantCulture) + " warning(s)", WarningBadge(CountIntercomIssues(issues, stageIndex, intercomIndex))));
            items.Add(Property("Kind", "Intercom step", "One scene inside this story stage.", "STEP"));
            ScenarioStoryCharacterActorLinkSectionBuilder.AppendUsages(items, definition, ScenarioReferenceTargetKind.IntercomStep, intercom != null ? intercom.Id : null, "Removing this step is blocked while references exist.");
            return Section("story_focused_encounter_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + intercomIndex.ToString(CultureInfo.InvariantCulture), label.ToUpperInvariant(), ScenarioAuthoringInspectorSectionLayout.FactGrid, items);
        }

        private static ScenarioAuthoringInspectorSection BuildDialogueSection(ScenarioDefinition definition, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            AddDialogueItems(items, definition, stage, intercom, stageIndex, intercomIndex);
            AddOptionItems(items, stage, intercom, stageIndex, intercomIndex);
            return Section("story_focused_dialogue_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + intercomIndex.ToString(CultureInfo.InvariantCulture), "WHAT / DIALOGUE & CHOICES", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static ScenarioAuthoringInspectorSection BuildConditionsSection(ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            bool hasRequired = intercom != null && intercom.Items != null && intercom.Items.Count > 0;
            if (!hasRequired)
                items.Add(Text("No required items - every player can reach this step."));
            AddStoryItemActions(items, "Check required items", intercom != null ? intercom.Items : null, false, stageIndex, intercomIndex);
            return Section("story_focused_conditions_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + intercomIndex.ToString(CultureInfo.InvariantCulture), "CONDITIONS", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static ScenarioAuthoringInspectorSection BuildAdvancedRoutingSection(ScenarioDefinition definition, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Internal step id", Empty(intercom != null ? intercom.Id : null, "Internal encounter-step id is blank.")));
            items.Add(Property("Branching type", Empty(intercom != null ? intercom.Type : null, "No type stored - vanilla will use its default."), "Use exact vanilla branch type names."));
            AddIntercomTypeActions(items, stageIndex, intercomIndex, intercom != null ? intercom.Type : null);
            AddIntercomRoutePicker(items, stage, intercom, stageIndex, intercomIndex, false, "Success route");
            AddIntercomRoutePicker(items, stage, intercom, stageIndex, intercomIndex, true, "Failure / alternate route");
            AddStageChangePicker(items, definition, intercom, stageIndex, intercomIndex);
            return AdvancedSection("story_focused_advanced_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + intercomIndex.ToString(CultureInfo.InvariantCulture), "ADVANCED / ROUTING", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static ScenarioAuthoringInspectorSection BuildOutcomeSection(ScenarioDefinition definition, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioEncounterEndOptionsDefinition end = intercom != null ? intercom.EndOptions : null;
            items.Add(Property("End behavior", Empty(end != null ? end.Type : null, "No explicit end behavior stored - vanilla default applies."), "Use RewardItems, EnterTrade, EnterRecruit, Combat, or NothingHappens."));
            AddEndTypeActions(items, stageIndex, intercomIndex, end != null ? end.Type : null);

            AddStoryItemActions(items, "Swap/remove items", intercom != null ? intercom.ItemsToRemove : null, true, stageIndex, intercomIndex);
            AddEndOptionItemActions(items, "End reward items", end != null ? end.RewardItems : null, true, stageIndex, intercomIndex);
            items.Add(ActionItem(Action(StoryAuthoringCommands.ToggleTradeOverride(stageIndex, intercomIndex), "Toggle Trade Override", "Use authored trade items instead of vanilla generated trade items.", true, end != null && end.OverrideTradeItems, "TR")));
            AddEndOptionItemActions(items, "Trade items", end != null ? end.TradeItems : null, false, stageIndex, intercomIndex);
            AddRecruitActions(items, definition, intercom, stageIndex, intercomIndex);
            items.Add(ActionItem(Action(StoryAuthoringCommands.EndCompleteQuest(stageIndex, intercomIndex), "Complete Quest", "Mark this vanilla quest complete when the encounter ends.", true, end != null && end.CompleteQuest, "CQ")));
            items.Add(ActionItem(Action(StoryAuthoringCommands.EndCompleteScenario(stageIndex, intercomIndex), end != null && end.CompleteParentScenario ? "Clear Complete Scenario" : "Complete Scenario unavailable", "Use Victory conditions to complete the authored scenario.", end != null && end.CompleteParentScenario, false, "VC")));
            return Section("story_focused_outcome_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + intercomIndex.ToString(CultureInfo.InvariantCulture), "WHAT / OUTCOMES", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static ScenarioAuthoringInspectorSection BuildAdvancedStageSection(ScenarioDefinition definition, ScenarioFlowStageDefinition stage, int stageIndex)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Internal stage id", Empty(stage != null ? stage.Id : null, "Internal stage id is blank - assign one before publishing.")));
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition step = stage.IntercomStages[i];
                if (step == null)
                    continue;
                if (!string.IsNullOrEmpty(step.StageDescriptionKey))
                    items.Add(Property("Technical stage title key", step.StageDescriptionKey));
                for (int d = 0; step.Dialogue != null && d < step.Dialogue.Count; d++)
                    if (step.Dialogue[d] != null && !string.IsNullOrEmpty(step.Dialogue[d].TextKey))
                        items.Add(Property("Technical dialogue key " + (d + 1).ToString(CultureInfo.InvariantCulture), step.Dialogue[d].TextKey));
                for (int o = 0; step.Options != null && o < step.Options.Count; o++)
                    if (step.Options[o] != null && !string.IsNullOrEmpty(step.Options[o].TextKey))
                        items.Add(Property("Technical option key " + (o + 1).ToString(CultureInfo.InvariantCulture), step.Options[o].TextKey));
            }
            AddStageIdActions(items, definition != null ? definition.ScenarioFlow : null, stageIndex);
            if (!ScenarioStoryStageDisclosure.ShouldRevealAdvancedRouting(stage))
                items.Add(Text("Advanced step routing appears after this stage has its first written dialogue line."));
            return AdvancedSection("story_focused_advanced", "ADVANCED", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static void AddDialogueItems(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            items.Add(ActionItem(Action(StoryAuthoringCommands.DialogueAdd(stageIndex, intercomIndex), "Add Dialogue Line", "Add a line the NPC/intercom can say.", true, false, "D+")));
            if (intercom == null || intercom.Dialogue == null || intercom.Dialogue.Count == 0)
            {
                items.Add(Text("No dialogue lines yet - add a line so the player sees encounter text."));
                return;
            }

            List<string> speakers = BuildCharacterIds(definition);
            for (int i = 0; i < intercom.Dialogue.Count; i++)
            {
                ScenarioDialogueLineDefinition line = intercom.Dialogue[i];
                string textKey = ResolveText(line != null ? line.TextKey : null, "Dialogue " + (i + 1).ToString(CultureInfo.InvariantCulture));
                string speaker = line != null && !string.IsNullOrEmpty(line.Character) ? FormatCharacterLabel(definition, line.Character) : "No speaker selected - choose a speaker.";
                items.Add(Property("Dialogue " + (i + 1).ToString(CultureInfo.InvariantCulture), textKey, speaker));
                for (int s = 0; s < speakers.Count; s++)
                    items.Add(ActionItem(Action(StoryAuthoringCommands.DialogueSpeaker(stageIndex, intercomIndex, i, speakers[s]), FormatCharacterLabel(definition, speakers[s]), "Use this speaker.", true, line != null && string.Equals(line.Character, speakers[s], StringComparison.OrdinalIgnoreCase), "SP")));
                string key = line != null && !string.IsNullOrEmpty(line.TextKey) ? line.TextKey : "dialogue_" + (i + 1).ToString(CultureInfo.InvariantCulture);
                items.Add(ActionItem(Action(StoryAuthoringCommands.DialogueKey(stageIndex, intercomIndex, i, key + "_copy"), "Use Next Text Key", "Use the next localization-key pattern.", true, false, "KY")));
                items.Add(ActionItem(Action(StoryAuthoringCommands.DialogueDelete(stageIndex, intercomIndex, i), "Remove Dialogue", "Remove this dialogue line.", true, false, "RM")));
            }
        }

        private static void AddOptionItems(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            items.Add(ActionItem(Action(StoryAuthoringCommands.OptionAdd(stageIndex, intercomIndex), "Add Player Option", "Add a response option and route.", true, false, "O+")));
            if (intercom == null || intercom.Options == null || intercom.Options.Count == 0)
            {
                items.Add(Text("No player options yet - add one for a choice branch, or use success/failure routes below."));
                return;
            }

            for (int i = 0; i < intercom.Options.Count; i++)
            {
                ScenarioDialogueOptionDefinition option = intercom.Options[i];
                string textKey = ResolveText(option != null ? option.TextKey : null, "Option " + (i + 1).ToString(CultureInfo.InvariantCulture));
                items.Add(Property("Option " + (i + 1).ToString(CultureInfo.InvariantCulture), textKey, "Routes to " + FormatIntercomTarget(stage, option != null ? option.NextId : null)));
                string key = option != null && !string.IsNullOrEmpty(option.TextKey) ? option.TextKey : "option_" + (i + 1).ToString(CultureInfo.InvariantCulture);
                items.Add(ActionItem(Action(StoryAuthoringCommands.OptionKey(stageIndex, intercomIndex, i, key + "_copy"), "Use Next Option Key", "Use the next option-key pattern.", true, false, "KY")));
                AddOptionRoutePicker(items, stage, option, stageIndex, intercomIndex, i);
                items.Add(ActionItem(Action(StoryAuthoringCommands.OptionDelete(stageIndex, intercomIndex, i), "Remove Option", "Remove this response option.", true, false, "RM")));
            }
        }

        private static void AddOptionRoutePicker(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioDialogueOptionDefinition option, int stageIndex, int intercomIndex, int optionIndex)
        {
            string current = option != null ? option.NextId : null;
            items.Add(ActionItem(Action(StoryAuthoringCommands.OptionNext(stageIndex, intercomIndex, optionIndex, null), "End Encounter Route", "Clear this option route so the encounter can end or use end options.", true, string.IsNullOrEmpty(current), "NX")));
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                string id = stage.IntercomStages[i] != null ? stage.IntercomStages[i].Id : null;
                if (!string.IsNullOrEmpty(id))
                    items.Add(ActionItem(Action(StoryAuthoringCommands.OptionNext(stageIndex, intercomIndex, optionIndex, id), "Option -> " + DisplayIntercomTitle(stage.IntercomStages[i], i), "Route this option to an encounter step.", true, string.Equals(current, id, StringComparison.OrdinalIgnoreCase), "NX")));
            }
        }

        private static void AddIntercomRoutePicker(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex, bool alternate, string label)
        {
            string current = alternate ? intercom.AlternateNextId : intercom.NextId;
            items.Add(Property(label, FormatIntercomTarget(stage, current)));
            items.Add(ActionItem(Action(alternate ? StoryAuthoringCommands.IntercomAlternate(stageIndex, intercomIndex, null) : StoryAuthoringCommands.IntercomNext(stageIndex, intercomIndex, null), label + ": End Encounter", "Clear this route.", true, string.IsNullOrEmpty(current), "RT")));
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                string id = stage.IntercomStages[i] != null ? stage.IntercomStages[i].Id : null;
                if (!string.IsNullOrEmpty(id))
                    items.Add(ActionItem(Action(alternate ? StoryAuthoringCommands.IntercomAlternate(stageIndex, intercomIndex, id) : StoryAuthoringCommands.IntercomNext(stageIndex, intercomIndex, id), label + " -> " + DisplayIntercomTitle(stage.IntercomStages[i], i), "Route to this encounter step.", true, string.Equals(current, id, StringComparison.OrdinalIgnoreCase), "RT")));
            }
        }

        private static void AddStageChangePicker(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            ScenarioStageChangeDefinition change = intercom != null ? intercom.StageChange : null;
            string current = change != null ? change.Id : null;
            items.Add(Property("Next stage after outcome", FormatStageTarget(definition, current), change != null ? change.DelayDays.ToString(CultureInfo.InvariantCulture) + " day delay" : "No delayed next-stage transition."));
            AddStagePicker(items, definition, stageIndex, current, false, intercomIndex);
            items.Add(ActionItem(Action(StoryAuthoringCommands.StageChangeDelay(stageIndex, intercomIndex, 1), "Stage Delay +", "Increase next-stage delay by one day.", true, false, "SD+")));
            items.Add(ActionItem(Action(StoryAuthoringCommands.StageChangeDelay(stageIndex, intercomIndex, -1), "Stage Delay -", "Decrease next-stage delay by one day.", true, false, "SD-")));
        }

        private static void AddStagePicker(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, int stageIndex, string current, bool unanswered)
        {
            AddStagePicker(items, definition, stageIndex, current, unanswered, -1);
        }

        private static void AddStagePicker(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, int stageIndex, string current, bool unanswered, int intercomIndex)
        {
            items.Add(ActionItem(Action(unanswered ? StoryAuthoringCommands.StageUnanswered(stageIndex, null) : StoryAuthoringCommands.StageChangeTarget(stageIndex, intercomIndex, null), unanswered ? "Ignored Call: Stay Here" : "No Next Stage", unanswered ? "Clear ignored-call routing so vanilla keeps the current stage." : "Clear delayed next-stage routing.", true, string.IsNullOrEmpty(current), "ST")));
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                string id = flow.Stages[i] != null ? flow.Stages[i].Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                ScenarioAuthoringCommand command = unanswered ? StoryAuthoringCommands.StageUnanswered(stageIndex, id) : StoryAuthoringCommands.StageChangeTarget(stageIndex, intercomIndex, id);
                items.Add(ActionItem(Action(command, "Stage -> " + DisplayStageTitle(flow.Stages[i], i), "Pick this existing stage.", true, string.Equals(current, id, StringComparison.OrdinalIgnoreCase), "ST")));
            }
            items.Add(ActionItem(Action(StoryAuthoringCommands.AddRoutedStage(stageIndex, intercomIndex, unanswered), "New Stage", "Create a new stage and select it for this route.", true, false, "S+")));
        }

        private static void AddIntercomTypeActions(List<ScenarioAuthoringInspectorItem> items, int stageIndex, int intercomIndex, string current)
        {
            string[] types = { "Choice", "CheckItems", "CheckMilestone", "Randomizer", "EndEncounter", "EnterCode" };
            for (int i = 0; i < types.Length; i++)
                items.Add(ActionItem(Action(StoryAuthoringCommands.IntercomType(stageIndex, intercomIndex, types[i]), types[i], "Set the vanilla encounter branch type.", true, string.Equals(current, types[i], StringComparison.OrdinalIgnoreCase), "TY")));
        }

        private static void AddEndTypeActions(List<ScenarioAuthoringInspectorItem> items, int stageIndex, int intercomIndex, string current)
        {
            string[] types = { "NothingHappens", "RewardItems", "EnterTrade", "EnterRecruit", "Combat", "CompleteQuest" };
            for (int i = 0; i < types.Length; i++)
                items.Add(ActionItem(Action(StoryAuthoringCommands.EndType(stageIndex, intercomIndex, types[i]), types[i], "Set the vanilla encounter outcome type.", true, string.Equals(current, types[i], StringComparison.OrdinalIgnoreCase), "END")));
        }

        private static void AddStoryItemActions(List<ScenarioAuthoringInspectorItem> items, string title, List<ItemEntry> entries, bool removal, int stageIndex, int intercomIndex)
        {
            items.Add(ActionItem(Action(removal ? StoryAuthoringCommands.RemovalAdd(stageIndex, intercomIndex) : StoryAuthoringCommands.RewardAdd(stageIndex, intercomIndex), "Add " + title, "Add an item row.", true, false, "I+")));
            AddItemRows(items, title, entries, removal, false, false, stageIndex, intercomIndex);
        }

        private static void AddEndOptionItemActions(List<ScenarioAuthoringInspectorItem> items, string title, List<ItemEntry> entries, bool reward, int stageIndex, int intercomIndex)
        {
            items.Add(ActionItem(Action(StoryAuthoringCommands.AddOutcomeItem(stageIndex, intercomIndex, reward), "Add " + title, "Add an item row.", true, false, "I+")));
            AddItemRows(items, title, entries, false, reward, !reward, stageIndex, intercomIndex);
        }

        private static void AddItemRows(List<ScenarioAuthoringInspectorItem> items, string title, List<ItemEntry> entries, bool removal, bool endReward, bool trade, int stageIndex, int intercomIndex)
        {
            if (entries == null || entries.Count == 0)
            {
                items.Add(Text("No " + title.ToLowerInvariant() + " yet - use the add action to create an item row."));
                return;
            }

            List<ScenarioInventoryItemCatalogEntry> catalog = ScenarioInventoryItemCatalog.Build();
            int max = Math.Min(ItemPickerLimit, catalog.Count);
            for (int e = 0; e < entries.Count; e++)
            {
                ItemEntry entry = entries[e];
                ScenarioInventoryItemCatalogEntry resolved = ScenarioInventoryItemCatalog.Resolve(entry != null ? entry.ItemId : null);
                items.Add(ScenarioInspectorItemFactory.Property(title + " " + (e + 1).ToString(CultureInfo.InvariantCulture), resolved.DisplayName, resolved.Detail, "x" + (entry != null ? entry.Quantity : 0).ToString(CultureInfo.InvariantCulture), null, resolved.PreviewSprite));
                for (int i = 0; i < max; i++)
                {
                    ScenarioAuthoringCommand command = BuildItemSelectCommand(removal, endReward, trade, stageIndex, intercomIndex, e, catalog[i].ItemId);
                    items.Add(ActionItem(Action(command, catalog[i].DisplayName, "Select this item.", true, entry != null && string.Equals(entry.ItemId, catalog[i].ItemId, StringComparison.OrdinalIgnoreCase), "IT", catalog[i].Detail, null, catalog[i].PreviewSprite)));
                }
                items.Add(ActionItem(Action(BuildItemQuantityCommand(removal, endReward, trade, stageIndex, intercomIndex, e, 1), "Qty +", "Increase quantity.", true, false, "+")));
                items.Add(ActionItem(Action(BuildItemQuantityCommand(removal, endReward, trade, stageIndex, intercomIndex, e, -1), "Qty -", "Decrease quantity.", true, false, "-")));
                items.Add(ActionItem(Action(BuildItemDeleteCommand(removal, endReward, trade, stageIndex, intercomIndex, e), "Remove " + title, "Remove this item row.", true, false, "RM")));
            }
        }

        private static ScenarioAuthoringCommand BuildItemSelectCommand(bool removal, bool endReward, bool trade, int stageIndex, int intercomIndex, int itemIndex, string itemId)
        {
            if (endReward)
                return StoryAuthoringCommands.SetOutcomeItem(stageIndex, intercomIndex, itemIndex, true, itemId);
            if (trade)
                return StoryAuthoringCommands.SetOutcomeItem(stageIndex, intercomIndex, itemIndex, false, itemId);
            if (!removal)
                return StoryAuthoringCommands.RewardItem(stageIndex, intercomIndex, itemIndex, itemId);
            return StoryAuthoringCommands.RemovalItem(stageIndex, intercomIndex, itemIndex, itemId);
        }

        private static ScenarioAuthoringCommand BuildItemQuantityCommand(bool removal, bool endReward, bool trade, int stageIndex, int intercomIndex, int itemIndex, int delta)
        {
            if (endReward)
                return StoryAuthoringCommands.StepOutcomeQuantity(stageIndex, intercomIndex, itemIndex, true, delta);
            if (trade)
                return StoryAuthoringCommands.StepOutcomeQuantity(stageIndex, intercomIndex, itemIndex, false, delta);
            if (!removal)
                return StoryAuthoringCommands.RewardQuantity(stageIndex, intercomIndex, itemIndex, delta);
            return StoryAuthoringCommands.RemovalQuantity(stageIndex, intercomIndex, itemIndex, delta);
        }

        private static ScenarioAuthoringCommand BuildItemDeleteCommand(bool removal, bool endReward, bool trade, int stageIndex, int intercomIndex, int itemIndex)
        {
            if (endReward)
                return StoryAuthoringCommands.DeleteOutcomeItem(stageIndex, intercomIndex, itemIndex, true);
            if (trade)
                return StoryAuthoringCommands.DeleteOutcomeItem(stageIndex, intercomIndex, itemIndex, false);
            if (!removal)
                return StoryAuthoringCommands.RewardDelete(stageIndex, intercomIndex, itemIndex);
            return StoryAuthoringCommands.RemovalDelete(stageIndex, intercomIndex, itemIndex);
        }

        private static void AddRecruitActions(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            List<string> ids = BuildCharacterIds(definition);
            for (int i = 0; ids != null && i < ids.Count; i++)
            {
                string id = ids[i];
                bool selected = intercom != null && Contains(intercom.CharacterIdsToRecruit, id);
                items.Add(ActionItem(Action(StoryAuthoringCommands.RecruitToggle(stageIndex, intercomIndex, id), "Recruit " + FormatCharacterLabel(definition, id), "Toggle recruitment for this character.", true, selected, "RC")));
            }
            items.Add(ActionItem(Action(StoryAuthoringCommands.RecruitFamily(stageIndex, intercomIndex), "Recruit As Family", "Toggle whether recruited characters join the family roster.", true, intercom != null && intercom.RecruitAsFamily, "RF")));
        }

        private static void AddStageIdActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowDefinition flow, int stageIndex)
        {
            int count = flow != null && flow.Stages != null ? flow.Stages.Count + 1 : 1;
            string candidate = "stage_" + count.ToString(CultureInfo.InvariantCulture);
            items.Add(ActionItem(Action(StoryAuthoringCommands.StageId(stageIndex, candidate), "Use Id " + candidate, "Rename using the next stage-id pattern.", true, false, "ID")));
        }

        private static ScenarioAuthoringCompactChoiceViewModel BuildCharacterChoice(
            ScenarioDefinition definition,
            ScenarioFlowStageDefinition stage,
            int stageIndex)
        {
            ScenarioAuthoringCompactChoiceViewModel choice = NewChoice("story_stage_cast_choice_" + stageIndex.ToString(CultureInfo.InvariantCulture), "Stage cast", "Select everyone who appears", 3);
            List<ScenarioAuthoringCompactChoiceOptionViewModel> options = new List<ScenarioAuthoringCompactChoiceOptionViewModel>();
            List<string> ids = BuildCharacterIds(definition);
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                options.Add(ChoiceOption(
                    "character_" + i.ToString(CultureInfo.InvariantCulture),
                    FormatCharacterLabel(definition, id),
                    Contains(stage.CharacterIds, id),
                    StoryAuthoringCommands.StageCharacterToggle(stageIndex, id),
                    "Toggle this character in the stage cast."));
            }
            choice.Options = options.ToArray();
            return choice;
        }

        private static ScenarioAuthoringCompactChoiceViewModel BuildDialogueSpeakerChoice(
            ScenarioDefinition definition,
            ScenarioDialogueLineDefinition line,
            int stageIndex,
            int sceneIndex,
            int lineIndex)
        {
            ScenarioAuthoringCompactChoiceViewModel choice = NewChoice(
                "story_scene_speaker_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + sceneIndex.ToString(CultureInfo.InvariantCulture) + "_" + lineIndex.ToString(CultureInfo.InvariantCulture),
                "Speaker",
                line != null && !string.IsNullOrEmpty(line.Character) ? FormatCharacterLabel(definition, line.Character) : "Choose a speaker",
                3);
            List<string> ids = BuildCharacterIds(definition);
            List<ScenarioAuthoringCompactChoiceOptionViewModel> options = new List<ScenarioAuthoringCompactChoiceOptionViewModel>();
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                options.Add(ChoiceOption(
                    "speaker_" + i.ToString(CultureInfo.InvariantCulture),
                    FormatCharacterLabel(definition, id),
                    line != null && string.Equals(line.Character, id, StringComparison.OrdinalIgnoreCase),
                    StoryAuthoringCommands.DialogueSpeaker(stageIndex, sceneIndex, lineIndex, id),
                    "Use this speaker."));
            }
            choice.Options = options.ToArray();
            return choice;
        }

        private static ScenarioAuthoringCompactChoiceViewModel BuildStageRouteChoice(
            ScenarioDefinition definition,
            int stageIndex,
            string current,
            bool unanswered,
            int sceneIndex)
        {
            string label = unanswered ? "Ignored-call route" : "Stage-change target";
            ScenarioAuthoringCompactChoiceViewModel choice = NewChoice(
                "story_stage_route_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + sceneIndex.ToString(CultureInfo.InvariantCulture) + "_" + (unanswered ? "ignored" : "outcome"),
                label,
                FormatStageTarget(definition, current),
                3);
            List<ScenarioAuthoringCompactChoiceOptionViewModel> options = new List<ScenarioAuthoringCompactChoiceOptionViewModel>();
            options.Add(ChoiceOption(
                "none",
                unanswered ? "Stay In This Stage" : "No Stage Change",
                string.IsNullOrEmpty(current),
                unanswered ? StoryAuthoringCommands.StageUnanswered(stageIndex, null) : StoryAuthoringCommands.StageChangeTarget(stageIndex, sceneIndex, null),
                "Clear this route."));
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition target = flow.Stages[i];
                string id = target != null ? target.Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                options.Add(ChoiceOption(
                    "stage_" + i.ToString(CultureInfo.InvariantCulture),
                    DisplayStageTitle(target, i),
                    string.Equals(current, id, StringComparison.OrdinalIgnoreCase),
                    unanswered ? StoryAuthoringCommands.StageUnanswered(stageIndex, id) : StoryAuthoringCommands.StageChangeTarget(stageIndex, sceneIndex, id),
                    "Route to this stage."));
            }
            options.Add(new ScenarioAuthoringCompactChoiceOptionViewModel
            {
                Id = "new",
                Label = "Add New Stage",
                Selected = false,
                Action = Action(StoryAuthoringCommands.AddRoutedStage(stageIndex, sceneIndex, unanswered), "Add New Stage", "Create a stage, route here, and select it.", true, false, null)
            });
            choice.Options = options.ToArray();
            return choice;
        }

        private static ScenarioAuthoringCompactChoiceViewModel BuildSceneRouteChoice(
            ScenarioFlowStageDefinition stage,
            int stageIndex,
            int sceneIndex,
            string current,
            string label,
            Func<string, ScenarioAuthoringCommand> commandFactory)
        {
            ScenarioAuthoringCompactChoiceViewModel choice = NewChoice(
                "story_scene_route_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + sceneIndex.ToString(CultureInfo.InvariantCulture) + "_" + ScenarioAutomationIdCodec.EncodeToken(label),
                label,
                FormatIntercomTarget(stage, current),
                3);
            List<ScenarioAuthoringCompactChoiceOptionViewModel> options = new List<ScenarioAuthoringCompactChoiceOptionViewModel>();
            options.Add(ChoiceOption("end", "End Scene", string.IsNullOrEmpty(current), commandFactory(null), "Clear this scene route."));
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition target = stage.IntercomStages[i];
                string id = target != null ? target.Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                options.Add(ChoiceOption(
                    "scene_" + i.ToString(CultureInfo.InvariantCulture),
                    DisplayIntercomTitle(target, i),
                    string.Equals(current, id, StringComparison.OrdinalIgnoreCase),
                    commandFactory(id),
                    "Continue to this scene."));
            }
            choice.Options = options.ToArray();
            return choice;
        }

        private static ScenarioAuthoringCompactChoiceViewModel BuildIntercomTypeChoice(int stageIndex, int sceneIndex, string current)
        {
            string[] values = { "Choice", "CheckItems", "CheckMilestone", "Randomizer", "EndEncounter", "EnterCode" };
            string[] labels = { "Conversation", "Item Check", "Milestone Check", "Random Branch", "End Encounter", "Code Entry" };
            ScenarioAuthoringCompactChoiceViewModel choice = NewChoice(
                "story_scene_type_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + sceneIndex.ToString(CultureInfo.InvariantCulture),
                "Scene behavior",
                HumanChoiceLabel(values, labels, current, "Vanilla default"),
                3);
            ScenarioAuthoringCompactChoiceOptionViewModel[] options = new ScenarioAuthoringCompactChoiceOptionViewModel[values.Length];
            for (int i = 0; i < values.Length; i++)
                options[i] = ChoiceOption("type_" + i.ToString(CultureInfo.InvariantCulture), labels[i], string.Equals(current, values[i], StringComparison.OrdinalIgnoreCase), StoryAuthoringCommands.IntercomType(stageIndex, sceneIndex, values[i]), "Set the scene behavior.");
            choice.Options = options;
            return choice;
        }

        private static ScenarioAuthoringCompactChoiceViewModel BuildEndTypeChoice(int stageIndex, int sceneIndex, string current)
        {
            string[] values = { "NothingHappens", "RewardItems", "EnterTrade", "EnterRecruit", "Combat", "CompleteQuest" };
            string[] labels = { "Nothing Else", "Give Rewards", "Open Trade", "Recruit", "Start Combat", "Complete Quest" };
            ScenarioAuthoringCompactChoiceViewModel choice = NewChoice(
                "story_scene_end_type_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + sceneIndex.ToString(CultureInfo.InvariantCulture),
                "End behavior",
                HumanChoiceLabel(values, labels, current, "Nothing else"),
                3);
            ScenarioAuthoringCompactChoiceOptionViewModel[] options = new ScenarioAuthoringCompactChoiceOptionViewModel[values.Length];
            for (int i = 0; i < values.Length; i++)
                options[i] = ChoiceOption("end_" + i.ToString(CultureInfo.InvariantCulture), labels[i], string.Equals(current, values[i], StringComparison.OrdinalIgnoreCase), StoryAuthoringCommands.EndType(stageIndex, sceneIndex, values[i]), "Choose what happens when the scene ends.");
            choice.Options = options;
            return choice;
        }

        private static string HumanChoiceLabel(string[] values, string[] labels, string current, string fallback)
        {
            for (int i = 0; values != null && labels != null && i < values.Length && i < labels.Length; i++)
                if (string.Equals(current, values[i], StringComparison.OrdinalIgnoreCase)) return labels[i];
            return fallback;
        }

        private static string FormatStageCast(ScenarioDefinition definition, ScenarioFlowStageDefinition stage)
        {
            List<string> names = new List<string>();
            for (int i = 0; stage != null && stage.CharacterIds != null && i < stage.CharacterIds.Count; i++)
                names.Add(FormatCharacterLabel(definition, stage.CharacterIds[i]));
            return names.Count > 0 ? string.Join(", ", names.ToArray()) : "No characters assigned";
        }

        private static ScenarioAuthoringCompactChoiceViewModel NewChoice(string id, string label, string current, int columns)
        {
            return new ScenarioAuthoringCompactChoiceViewModel
            {
                Id = id,
                Label = label,
                CurrentLabel = current,
                ColumnCount = columns,
                Options = new ScenarioAuthoringCompactChoiceOptionViewModel[0]
            };
        }

        private static ScenarioAuthoringCompactChoiceOptionViewModel ChoiceOption(
            string id,
            string label,
            bool selected,
            string actionId,
            string hint)
        {
            return new ScenarioAuthoringCompactChoiceOptionViewModel
            {
                Id = id,
                Label = label,
                Selected = selected,
                Action = Action(actionId, label, hint, true, selected, null)
            };
        }

        private static ScenarioAuthoringCompactChoiceOptionViewModel ChoiceOption(
            string id,
            string label,
            bool selected,
            ScenarioAuthoringCommand command,
            string hint)
        {
            return new ScenarioAuthoringCompactChoiceOptionViewModel
            {
                Id = id,
                Label = label,
                Selected = selected,
                Action = Action(command, label, hint, true, selected, null)
            };
        }

        private static ScenarioAuthoringInspectorItem ChoiceItem(ScenarioAuthoringCompactChoiceViewModel choice)
        {
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Choice,
                Choice = choice
            };
        }

        private static ScenarioFlowStageDefinition GetStage(ScenarioDefinition definition, int index)
        {
            return definition != null
                && definition.ScenarioFlow != null
                && definition.ScenarioFlow.Stages != null
                && index >= 0
                && index < definition.ScenarioFlow.Stages.Count
                    ? definition.ScenarioFlow.Stages[index]
                    : null;
        }

        private static string DisplayStageTitle(ScenarioFlowStageDefinition stage, int index)
        {
            string title = null;
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                if (stage.IntercomStages[i] != null && !string.IsNullOrEmpty(stage.IntercomStages[i].StageDescriptionKey))
                {
                    title = stage.IntercomStages[i].StageDescriptionKey;
                    break;
                }
            }
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                title,
                title,
                stage != null ? stage.Id : null,
                "Stage " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        private static string DisplayIntercomTitle(ScenarioIntercomStageDefinition intercom, int index)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                intercom != null ? intercom.StageDescriptionKey : null,
                intercom != null ? intercom.StageDescriptionKey : null,
                intercom != null ? intercom.Id : null,
                "Scene " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        private static string BuildStageSummary(ScenarioDefinition definition, ScenarioFlowStageDefinition stage)
        {
            if (stage == null || stage.IntercomStages == null || stage.IntercomStages.Count == 0)
                return "This stage has no encounter steps yet.";
            ScenarioIntercomStageDefinition first = stage.IntercomStages[0];
            return "Starts with '" + DisplayIntercomTitle(first, 0) + "'. When that scene ends: "
                + ScenarioStoryScriptViewBuilder.DescribeStepEnding(definition, stage, first) + ".";
        }

        private static string BuildIntercomSummary(ScenarioDefinition definition, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom)
        {
            int dialogue = intercom != null && intercom.Dialogue != null ? intercom.Dialogue.Count : 0;
            int options = intercom != null && intercom.Options != null ? intercom.Options.Count : 0;
            return dialogue.ToString(CultureInfo.InvariantCulture) + " spoken line(s), "
                + options.ToString(CultureInfo.InvariantCulture) + " player choice(s). When this scene ends: "
                + ScenarioStoryScriptViewBuilder.DescribeStepEnding(definition, stage, intercom) + ".";
        }

        private static string PrimaryEncounterType(ScenarioFlowStageDefinition stage)
        {
            ScenarioIntercomStageDefinition first = stage != null && stage.IntercomStages != null && stage.IntercomStages.Count > 0 ? stage.IntercomStages[0] : null;
            return Empty(first != null ? first.Type : null, "No encounter step yet.");
        }

        private static string FormatIgnoredCall(ScenarioDefinition definition, ScenarioFlowStageDefinition stage)
        {
            if (stage == null || string.IsNullOrEmpty(stage.UnansweredNextStage))
                return "No explicit route - vanilla keeps this stage active.";
            return "After delay, route to " + FormatStageTarget(definition, stage.UnansweredNextStage) + ".";
        }

        private static string FormatStageTarget(ScenarioDefinition definition, string value)
        {
            if (string.IsNullOrEmpty(value))
                return "No next stage selected - this outcome stays in the current stage or ends.";
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
                if (flow.Stages[i] != null && string.Equals(flow.Stages[i].Id, value, StringComparison.OrdinalIgnoreCase))
                    return DisplayStageTitle(flow.Stages[i], i);
            return "Missing stage";
        }

        private static string FormatIntercomTarget(ScenarioFlowStageDefinition stage, string value)
        {
            if (string.IsNullOrEmpty(value))
                return "No step selected - encounter can end or use end options.";
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                if (stage.IntercomStages[i] != null && string.Equals(stage.IntercomStages[i].Id, value, StringComparison.OrdinalIgnoreCase))
                    return DisplayIntercomTitle(stage.IntercomStages[i], i);
            return "Missing scene";
        }

        private static string ResolveText(string textOrKey, string fallback)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(textOrKey, textOrKey, null, fallback).Text;
        }

        private static string Empty(string value, string emptyMessage)
        {
            return string.IsNullOrEmpty(value) ? emptyMessage : value;
        }

        private static string WarningBadge(int count)
        {
            return count > 0 ? "!" + count.ToString(CultureInfo.InvariantCulture) : "OK";
        }

        private static int CountStageIssues(ScenarioStoryFlowIssue[] issues, int stageIndex)
        {
            int count = 0;
            for (int i = 0; issues != null && i < issues.Length; i++)
                if (issues[i] != null && issues[i].StageIndex == stageIndex)
                    count++;
            return count;
        }

        private static int CountIntercomIssues(ScenarioStoryFlowIssue[] issues, int stageIndex, int intercomIndex)
        {
            int count = 0;
            for (int i = 0; issues != null && i < issues.Length; i++)
                if (issues[i] != null && issues[i].StageIndex == stageIndex && issues[i].IntercomIndex == intercomIndex)
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

        private static string FormatCharacterLabel(ScenarioDefinition definition, string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return "<missing>";

            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                if (character == null || !string.Equals(character.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
                    continue;

                string label = ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                    character.DisplayName,
                    null,
                    characterId,
                    "Story character " + (i + 1).ToString(CultureInfo.InvariantCulture)).Text;

                if (character.ActorRef == null)
                    return label;

                return label + " -> " + ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, character.ActorRef, true, true, label);
            }

            string[] vanillaLabels = { "Lead NPC", "NPC 2", "NPC 3", "NPC 4", "Background NPC", "Player" };
            string[] vanillaIds = { "LeadNpc", "Npc2", "Npc3", "Npc4", "BackgroundNpc", "Player" };
            for (int i = 0; i < vanillaIds.Length; i++)
                if (string.Equals(characterId, vanillaIds[i], StringComparison.OrdinalIgnoreCase)) return vanillaLabels[i];
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(null, null, characterId, "Story character").Text;
        }

        private static bool Contains(List<string> values, string value)
        {
            for (int i = 0; values != null && i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
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

        private static ScenarioAuthoringInspectorSection AdvancedSection(string id, string title, ScenarioAuthoringInspectorSectionLayout layout, List<ScenarioAuthoringInspectorItem> items)
        {
            ScenarioAuthoringInspectorSection section = Section(id, title, layout, items);
            section.IsAdvanced = true;
            return section;
        }

        private static ScenarioAuthoringInspectorItem Text(string value)
        {
            return ScenarioInspectorItemFactory.Text(value);
        }

        private static ScenarioAuthoringInspectorItem Property(string label, string value)
        {
            return ScenarioInspectorItemFactory.Property(label, value);
        }

        private static ScenarioAuthoringInspectorItem Property(string label, string value, string detail)
        {
            return ScenarioInspectorItemFactory.Property(label, value, detail);
        }

        private static ScenarioAuthoringInspectorItem Property(string label, string value, string detail, string badge)
        {
            return ScenarioInspectorItemFactory.Property(label, value, detail, badge);
        }

        private static ScenarioAuthoringInspectorItem ActionItem(ScenarioAuthoringInspectorAction action)
        {
            return ScenarioInspectorItemFactory.ActionItem(action);
        }

        private static ScenarioAuthoringInspectorAction Action(string id, string label, string hint, bool enabled, bool emphasized, string iconText)
        {
            return ScenarioInspectorItemFactory.Action(id, label, hint, enabled, emphasized, iconText);
        }

        private static ScenarioAuthoringInspectorAction Action(ScenarioAuthoringCommand command, string label, string hint, bool enabled, bool emphasized, string iconText)
        {
            return ScenarioInspectorItemFactory.Action(command, label, hint, enabled, emphasized, iconText);
        }

        private static ScenarioAuthoringInspectorAction Action(string id, string label, string hint, bool enabled, bool emphasized, string iconText, string detail, string badge, UnityEngine.Sprite sprite)
        {
            return ScenarioInspectorItemFactory.Action(id, label, hint, enabled, emphasized, iconText, detail, badge, sprite);
        }

        private static ScenarioAuthoringInspectorAction Action(ScenarioAuthoringCommand command, string label, string hint, bool enabled, bool emphasized, string iconText, string detail, string badge, UnityEngine.Sprite sprite)
        {
            return ScenarioInspectorItemFactory.Action(command, label, hint, enabled, emphasized, iconText, detail, badge, sprite);
        }
    }
}
