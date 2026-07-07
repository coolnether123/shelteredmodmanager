using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Validation;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal static class ScenarioStoryFocusedEditorDocumentBuilder
    {
        private const int ItemPickerLimit = 6;

        public static bool TryBuild(ScenarioAuthoringState state, ScenarioDefinition definition, out ScenarioAuthoringInspectorDocument document)
        {
            document = null;
            if (state == null
                || definition == null
                || !string.Equals(state.FocusedEditorKind, ScenarioStoryFocusedEditorActions.FocusedEditorKind, StringComparison.OrdinalIgnoreCase))
                return false;

            ScenarioFlowStageDefinition stage = GetStage(definition, state.FocusedEditorIndex);
            if (stage == null)
                return false;

            ScenarioStoryFlowIssue[] issues = new ScenarioStoryFlowValidationAnalyzer().Analyze(definition);
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(BuildIdentitySection(definition, stage, state.FocusedEditorIndex, issues));
            sections.Add(BuildIgnoredCallSection(definition, stage, state.FocusedEditorIndex));
            sections.Add(BuildCastSection(definition, stage, state.FocusedEditorIndex));
            ScenarioStoryCharacterActorLinkSectionBuilder.AppendSections(sections, definition);

            for (int i = 0; stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition intercom = stage.IntercomStages[i];
                sections.Add(BuildEncounterSection(definition, stage, intercom, state.FocusedEditorIndex, i, issues));
                sections.Add(BuildOutcomeSection(definition, intercom, state.FocusedEditorIndex, i));
            }

            sections.Add(BuildStageToolsSection(state.FocusedEditorIndex, stage));
            sections.Add(BuildFooterSection());

            document = new ScenarioAuthoringInspectorDocument
            {
                Title = "Story Stage - " + DisplayStageTitle(stage, state.FocusedEditorIndex),
                Subtitle = "Edit one vanilla scenario stage, its encounter steps, and its next-stage outcomes.",
                HeaderActions = new[] { Action(ScenarioStoryFocusedEditorActions.ActionCancel, "x", "Close this story editor.", true, false, "HD") },
                Sections = sections.ToArray()
            };
            return true;
        }

        private static ScenarioAuthoringInspectorSection BuildIdentitySection(ScenarioDefinition definition, ScenarioFlowStageDefinition stage, int stageIndex, ScenarioStoryFlowIssue[] issues)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Author title", DisplayStageTitle(stage, stageIndex), "Stored as the first encounter step's stage description key."));
            items.Add(Property("Timing", "Day-granular", "Vanilla stages advance by day; delayed transitions use whole days."));
            items.Add(Property("Encounter type", PrimaryEncounterType(stage), "Type of the first encounter step."));
            items.Add(Property("Warnings", CountStageIssues(issues, stageIndex).ToString(CultureInfo.InvariantCulture), FirstStageIssue(issues, stageIndex)));
            items.Add(ActionItem(Action(ScenarioStoryFocusedEditorActions.StageTitle(stageIndex, "stage_" + (stageIndex + 1).ToString(CultureInfo.InvariantCulture) + "_title"), "Use Generated Title", "Fill the title key with a stage-specific label.", true, false, "TT")));
            items.Add(Property("Advanced internal id", Empty(stage.Id, "Internal stage id is blank - use an id action before publishing.")));
            AddStageIdActions(items, definition != null ? definition.ScenarioFlow : null, stageIndex);
            return Section("story_focused_identity", "Stage Identity", ScenarioAuthoringInspectorSectionLayout.FactGrid, items);
        }

        private static ScenarioAuthoringInspectorSection BuildIgnoredCallSection(ScenarioDefinition definition, ScenarioFlowStageDefinition stage, int stageIndex)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("If the player never answers", FormatIgnoredCall(stage), "Vanilla can keep the stage active when no explicit route is selected."));
            items.Add(Property("Delay", stage.UnansweredNextDays.ToString(CultureInfo.InvariantCulture) + " day(s)", "Vanilla checks stage start by scenario day."));
            items.Add(Property("Punishment", stage.PunishOnUnanswered ? "Punish after the NPC has visited" : "No explicit punishment", "Vanilla has an unanswered double-increment edge case; test ignored calls live."));
            AddStagePicker(items, definition, stageIndex, stage.UnansweredNextStage, true);
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.StageUnansweredDelay(stageIndex, 1), "Delay +", "Increase ignored-call delay by one day.", true, false, "D+")));
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.StageUnansweredDelay(stageIndex, -1), "Delay -", "Decrease ignored-call delay by one day.", true, false, "D-")));
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.StagePunish(stageIndex), "Toggle Punishment", "Toggle vanilla punishment for unanswered visited NPCs.", true, stage.PunishOnUnanswered, "PU")));
            return Section("story_focused_ignored", "Ignored Call Behavior", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
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
                items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.StageCharacterToggle(stageIndex, id), FormatCharacterLabel(definition, id), selected ? "Remove this character from the stage." : "Add this character to the stage.", true, selected, "CH")));
            }
            return Section("story_focused_cast", "Cast Picker", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static ScenarioAuthoringInspectorSection BuildEncounterSection(
            ScenarioDefinition definition,
            ScenarioFlowStageDefinition stage,
            ScenarioIntercomStageDefinition intercom,
            int stageIndex,
            int intercomIndex,
            ScenarioStoryFlowIssue[] issues)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            string label = "Step " + (intercomIndex + 1).ToString(CultureInfo.InvariantCulture);
            items.Add(Property("Encounter step", label, CountIntercomIssues(issues, stageIndex, intercomIndex).ToString(CultureInfo.InvariantCulture) + " warning(s)", WarningBadge(CountIntercomIssues(issues, stageIndex, intercomIndex))));
            items.Add(Property("Advanced step id", Empty(intercom != null ? intercom.Id : null, "Internal encounter-step id is blank.")));
            items.Add(Property("Branching type", Empty(intercom != null ? intercom.Type : null, "No type stored - vanilla will use its default."), "Use exact vanilla branch type names."));
            AddIntercomTypeActions(items, stageIndex, intercomIndex, intercom != null ? intercom.Type : null);

            AddDialogueItems(items, stage, intercom, stageIndex, intercomIndex);
            AddOptionItems(items, stage, intercom, stageIndex, intercomIndex);
            AddIntercomRoutePicker(items, stage, intercom, stageIndex, intercomIndex, false, "Success route");
            AddIntercomRoutePicker(items, stage, intercom, stageIndex, intercomIndex, true, "Failure / alternate route");
            AddStageChangePicker(items, definition, intercom, stageIndex, intercomIndex);
            return Section("story_focused_encounter_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + intercomIndex.ToString(CultureInfo.InvariantCulture), label + " Encounter Setup", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static ScenarioAuthoringInspectorSection BuildOutcomeSection(ScenarioDefinition definition, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioEncounterEndOptionsDefinition end = intercom != null ? intercom.EndOptions : null;
            items.Add(Property("End behavior", Empty(end != null ? end.Type : null, "No explicit end behavior stored - vanilla default applies."), "Use RewardItems, EnterTrade, EnterRecruit, Combat, or NothingHappens."));
            AddEndTypeActions(items, stageIndex, intercomIndex, end != null ? end.Type : null);

            AddStoryItemActions(items, "Check required items", intercom != null ? intercom.Items : null, false, stageIndex, intercomIndex);
            AddStoryItemActions(items, "Swap/remove items", intercom != null ? intercom.ItemsToRemove : null, true, stageIndex, intercomIndex);
            AddEndOptionItemActions(items, "End reward items", end != null ? end.RewardItems : null, true, stageIndex, intercomIndex);
            items.Add(ActionItem(Action(ScenarioStoryFocusedEditorActions.TradeOverride(stageIndex, intercomIndex), "Toggle Trade Override", "Use authored trade items instead of vanilla generated trade items.", true, end != null && end.OverrideTradeItems, "TR")));
            AddEndOptionItemActions(items, "Trade items", end != null ? end.TradeItems : null, false, stageIndex, intercomIndex);
            AddRecruitActions(items, definition, intercom, stageIndex, intercomIndex);
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.EndCompleteQuest(stageIndex, intercomIndex), "Complete Quest", "Mark this vanilla quest complete when the encounter ends.", true, end != null && end.CompleteQuest, "CQ")));
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.EndCompleteScenario(stageIndex, intercomIndex), end != null && end.CompleteParentScenario ? "Clear Complete Scenario" : "Complete Scenario unavailable", "Use Victory conditions to complete the authored scenario.", end != null && end.CompleteParentScenario, false, "VC")));
            return Section("story_focused_outcome_" + stageIndex.ToString(CultureInfo.InvariantCulture) + "_" + intercomIndex.ToString(CultureInfo.InvariantCulture), "Outcomes, Rewards, Trades, Recruit", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static ScenarioAuthoringInspectorSection BuildStageToolsSection(int stageIndex, ScenarioFlowStageDefinition stage)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.IntercomAdd(stageIndex), "Add Encounter Step", "Add another vanilla encounter step inside this stage.", true, false, "I+")));
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.StageDuplicate(stageIndex), "Duplicate Stage", "Copy this stage and its encounter setup.", true, false, "CP")));
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.StageDelete(stageIndex), "Remove Stage", "Remove this stage if nothing references it.", true, false, "RM")));
            if (stage != null && stage.IntercomStages != null)
            {
                for (int i = 0; i < stage.IntercomStages.Count; i++)
                {
                    items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.IntercomDuplicate(stageIndex, i), "Duplicate Step " + (i + 1).ToString(CultureInfo.InvariantCulture), "Copy this encounter step.", true, false, "CP")));
                    items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.IntercomDelete(stageIndex, i), "Remove Step " + (i + 1).ToString(CultureInfo.InvariantCulture), "Remove this encounter step.", true, false, "RM")));
                }
            }
            return Section("story_focused_tools", "Stage Tools", ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static ScenarioAuthoringInspectorSection BuildFooterSection()
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ActionItem(Action(ScenarioStoryFocusedEditorActions.ActionSave, "Save", "Close this story editor and keep the stage.", true, true, "SV")));
            items.Add(ActionItem(Action(ScenarioStoryFocusedEditorActions.ActionCancel, "Cancel", "Close this story editor. A newly-created stage is discarded.", true, false, "CL")));
            return Section("story_focused_footer", string.Empty, ScenarioAuthoringInspectorSectionLayout.ActionStrip, items);
        }

        private static void AddDialogueItems(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.DialogueAdd(stageIndex, intercomIndex), "Add Dialogue Line", "Add a line the NPC/intercom can say.", true, false, "D+")));
            if (intercom == null || intercom.Dialogue == null || intercom.Dialogue.Count == 0)
            {
                items.Add(Text("No dialogue lines yet - add a line so the player sees encounter text."));
                return;
            }

            string[] speakers = { "Player", "LeadNpc", "Npc2", "Npc3", "Npc4", "BackgroundNpc" };
            for (int i = 0; i < intercom.Dialogue.Count; i++)
            {
                ScenarioDialogueLineDefinition line = intercom.Dialogue[i];
                string textKey = line != null && !string.IsNullOrEmpty(line.TextKey) ? line.TextKey : "Dialogue text is blank - add a localization key.";
                string speaker = line != null && !string.IsNullOrEmpty(line.Character) ? line.Character : "No speaker selected - choose a speaker.";
                items.Add(Property("Dialogue " + (i + 1).ToString(CultureInfo.InvariantCulture), textKey, speaker));
                for (int s = 0; s < speakers.Length; s++)
                    items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.DialogueSpeaker(stageIndex, intercomIndex, i, speakers[s]), speakers[s], "Use this speaker.", true, line != null && string.Equals(line.Character, speakers[s], StringComparison.OrdinalIgnoreCase), "SP")));
                string key = line != null && !string.IsNullOrEmpty(line.TextKey) ? line.TextKey : "dialogue_" + (i + 1).ToString(CultureInfo.InvariantCulture);
                items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.DialogueKey(stageIndex, intercomIndex, i, key + "_copy"), "Use Next Text Key", "Use the next localization-key pattern.", true, false, "KY")));
                items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.DialogueDelete(stageIndex, intercomIndex, i), "Remove Dialogue", "Remove this dialogue line.", true, false, "RM")));
            }
        }

        private static void AddOptionItems(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.OptionAdd(stageIndex, intercomIndex), "Add Player Option", "Add a response option and route.", true, false, "O+")));
            if (intercom == null || intercom.Options == null || intercom.Options.Count == 0)
            {
                items.Add(Text("No player options yet - add one for a choice branch, or use success/failure routes below."));
                return;
            }

            for (int i = 0; i < intercom.Options.Count; i++)
            {
                ScenarioDialogueOptionDefinition option = intercom.Options[i];
                string textKey = option != null && !string.IsNullOrEmpty(option.TextKey) ? option.TextKey : "Option text is blank - add a localization key.";
                items.Add(Property("Option " + (i + 1).ToString(CultureInfo.InvariantCulture), textKey, "Routes to " + FormatIntercomTarget(option != null ? option.NextId : null)));
                string key = option != null && !string.IsNullOrEmpty(option.TextKey) ? option.TextKey : "option_" + (i + 1).ToString(CultureInfo.InvariantCulture);
                items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.OptionKey(stageIndex, intercomIndex, i, key + "_copy"), "Use Next Option Key", "Use the next option-key pattern.", true, false, "KY")));
                AddOptionRoutePicker(items, stage, option, stageIndex, intercomIndex, i);
                items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.OptionDelete(stageIndex, intercomIndex, i), "Remove Option", "Remove this response option.", true, false, "RM")));
            }
        }

        private static void AddOptionRoutePicker(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioDialogueOptionDefinition option, int stageIndex, int intercomIndex, int optionIndex)
        {
            string current = option != null ? option.NextId : null;
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.OptionNext(stageIndex, intercomIndex, optionIndex, null), "End Encounter Route", "Clear this option route so the encounter can end or use end options.", true, string.IsNullOrEmpty(current), "NX")));
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                string id = stage.IntercomStages[i] != null ? stage.IntercomStages[i].Id : null;
                if (!string.IsNullOrEmpty(id))
                    items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.OptionNext(stageIndex, intercomIndex, optionIndex, id), "Option -> " + DisplayIntercomTitle(stage.IntercomStages[i], i), "Route this option to an encounter step.", true, string.Equals(current, id, StringComparison.OrdinalIgnoreCase), "NX")));
            }
        }

        private static void AddIntercomRoutePicker(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex, bool alternate, string label)
        {
            string current = alternate ? intercom.AlternateNextId : intercom.NextId;
            items.Add(Property(label, FormatIntercomTarget(current)));
            items.Add(ActionItem(Action(alternate ? ScenarioStoryAuthoringActions.IntercomAlternate(stageIndex, intercomIndex, null) : ScenarioStoryAuthoringActions.IntercomNext(stageIndex, intercomIndex, null), label + ": End Encounter", "Clear this route.", true, string.IsNullOrEmpty(current), "RT")));
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                string id = stage.IntercomStages[i] != null ? stage.IntercomStages[i].Id : null;
                if (!string.IsNullOrEmpty(id))
                    items.Add(ActionItem(Action(alternate ? ScenarioStoryAuthoringActions.IntercomAlternate(stageIndex, intercomIndex, id) : ScenarioStoryAuthoringActions.IntercomNext(stageIndex, intercomIndex, id), label + " -> " + DisplayIntercomTitle(stage.IntercomStages[i], i), "Route to this encounter step.", true, string.Equals(current, id, StringComparison.OrdinalIgnoreCase), "RT")));
            }
        }

        private static void AddStageChangePicker(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            ScenarioStageChangeDefinition change = intercom != null ? intercom.StageChange : null;
            string current = change != null ? change.Id : null;
            items.Add(Property("Next stage after outcome", FormatStageTarget(current), change != null ? change.DelayDays.ToString(CultureInfo.InvariantCulture) + " day delay" : "No delayed next-stage transition."));
            AddStagePicker(items, definition, stageIndex, current, false, intercomIndex);
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.StageChangeDelay(stageIndex, intercomIndex, 1), "Stage Delay +", "Increase next-stage delay by one day.", true, false, "SD+")));
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.StageChangeDelay(stageIndex, intercomIndex, -1), "Stage Delay -", "Decrease next-stage delay by one day.", true, false, "SD-")));
        }

        private static void AddStagePicker(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, int stageIndex, string current, bool unanswered)
        {
            AddStagePicker(items, definition, stageIndex, current, unanswered, -1);
        }

        private static void AddStagePicker(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, int stageIndex, string current, bool unanswered, int intercomIndex)
        {
            items.Add(ActionItem(Action(unanswered ? ScenarioStoryAuthoringActions.StageUnanswered(stageIndex, null) : ScenarioStoryAuthoringActions.StageChangeTarget(stageIndex, intercomIndex, null), unanswered ? "Ignored Call: Stay Here" : "No Next Stage", unanswered ? "Clear ignored-call routing so vanilla keeps the current stage." : "Clear delayed next-stage routing.", true, string.IsNullOrEmpty(current), "ST")));
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                string id = flow.Stages[i] != null ? flow.Stages[i].Id : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                string actionId = unanswered ? ScenarioStoryAuthoringActions.StageUnanswered(stageIndex, id) : ScenarioStoryAuthoringActions.StageChangeTarget(stageIndex, intercomIndex, id);
                items.Add(ActionItem(Action(actionId, "Stage -> " + DisplayStageTitle(flow.Stages[i], i), "Pick this existing stage.", true, string.Equals(current, id, StringComparison.OrdinalIgnoreCase), "ST")));
            }
            items.Add(ActionItem(Action(unanswered ? ScenarioStoryFocusedEditorActions.UnansweredNewStage(stageIndex) : ScenarioStoryFocusedEditorActions.StageChangeNewStage(stageIndex, intercomIndex), "New Stage", "Create a new stage and select it for this route.", true, false, "S+")));
        }

        private static void AddIntercomTypeActions(List<ScenarioAuthoringInspectorItem> items, int stageIndex, int intercomIndex, string current)
        {
            string[] types = { "Choice", "CheckItems", "CheckMilestone", "Randomizer", "EndEncounter", "EnterCode" };
            for (int i = 0; i < types.Length; i++)
                items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.IntercomType(stageIndex, intercomIndex, types[i]), types[i], "Set the vanilla encounter branch type.", true, string.Equals(current, types[i], StringComparison.OrdinalIgnoreCase), "TY")));
        }

        private static void AddEndTypeActions(List<ScenarioAuthoringInspectorItem> items, int stageIndex, int intercomIndex, string current)
        {
            string[] types = { "NothingHappens", "RewardItems", "EnterTrade", "EnterRecruit", "Combat", "CompleteQuest" };
            for (int i = 0; i < types.Length; i++)
                items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.EndType(stageIndex, intercomIndex, types[i]), types[i], "Set the vanilla encounter outcome type.", true, string.Equals(current, types[i], StringComparison.OrdinalIgnoreCase), "END")));
        }

        private static void AddStoryItemActions(List<ScenarioAuthoringInspectorItem> items, string title, List<ItemEntry> entries, bool removal, int stageIndex, int intercomIndex)
        {
            items.Add(ActionItem(Action(removal ? ScenarioStoryAuthoringActions.RemovalAdd(stageIndex, intercomIndex) : ScenarioStoryAuthoringActions.RewardAdd(stageIndex, intercomIndex), "Add " + title, "Add an item row.", true, false, "I+")));
            AddItemRows(items, title, entries, removal, false, false, stageIndex, intercomIndex);
        }

        private static void AddEndOptionItemActions(List<ScenarioAuthoringInspectorItem> items, string title, List<ItemEntry> entries, bool reward, int stageIndex, int intercomIndex)
        {
            items.Add(ActionItem(Action(reward ? ScenarioStoryFocusedEditorActions.EndRewardAdd(stageIndex, intercomIndex) : ScenarioStoryFocusedEditorActions.TradeAdd(stageIndex, intercomIndex), "Add " + title, "Add an item row.", true, false, "I+")));
            AddItemRows(items, title, entries, false, reward, !reward, stageIndex, intercomIndex);
        }

        private static void AddItemRows(List<ScenarioAuthoringInspectorItem> items, string title, List<ItemEntry> entries, bool removal, bool endReward, bool trade, int stageIndex, int intercomIndex)
        {
            List<ScenarioInventoryItemCatalogEntry> catalog = ScenarioInventoryItemCatalog.Build();
            int max = Math.Min(ItemPickerLimit, catalog.Count);
            if (entries == null || entries.Count == 0)
            {
                items.Add(Text("No " + title.ToLowerInvariant() + " yet - use the add action to create an item row."));
                return;
            }

            for (int e = 0; e < entries.Count; e++)
            {
                ItemEntry entry = entries[e];
                ScenarioInventoryItemCatalogEntry resolved = ScenarioInventoryItemCatalog.Resolve(entry != null ? entry.ItemId : null);
                items.Add(ScenarioInspectorItemFactory.Property(title + " " + (e + 1).ToString(CultureInfo.InvariantCulture), resolved.DisplayName, resolved.Detail, "x" + (entry != null ? entry.Quantity : 0).ToString(CultureInfo.InvariantCulture), null, resolved.PreviewSprite));
                for (int i = 0; i < max; i++)
                {
                    string actionId = BuildItemSelectAction(removal, endReward, trade, stageIndex, intercomIndex, e, catalog[i].ItemId);
                    items.Add(ActionItem(Action(actionId, catalog[i].DisplayName, "Select this item.", true, entry != null && string.Equals(entry.ItemId, catalog[i].ItemId, StringComparison.OrdinalIgnoreCase), "IT", catalog[i].Detail, null, catalog[i].PreviewSprite)));
                }
                items.Add(ActionItem(Action(BuildItemQuantityAction(removal, endReward, trade, stageIndex, intercomIndex, e, 1), "Qty +", "Increase quantity.", true, false, "+")));
                items.Add(ActionItem(Action(BuildItemQuantityAction(removal, endReward, trade, stageIndex, intercomIndex, e, -1), "Qty -", "Decrease quantity.", true, false, "-")));
                items.Add(ActionItem(Action(BuildItemDeleteAction(removal, endReward, trade, stageIndex, intercomIndex, e), "Remove " + title, "Remove this item row.", true, false, "RM")));
            }
        }

        private static string BuildItemSelectAction(bool removal, bool endReward, bool trade, int stageIndex, int intercomIndex, int itemIndex, string itemId)
        {
            if (endReward)
                return ScenarioStoryFocusedEditorActions.EndRewardItem(stageIndex, intercomIndex, itemIndex, itemId);
            if (trade)
                return ScenarioStoryFocusedEditorActions.TradeItem(stageIndex, intercomIndex, itemIndex, itemId);
            if (!removal)
                return ScenarioStoryAuthoringActions.RewardItem(stageIndex, intercomIndex, itemIndex, itemId);
            return ScenarioStoryAuthoringActions.RemovalItem(stageIndex, intercomIndex, itemIndex, itemId);
        }

        private static string BuildItemQuantityAction(bool removal, bool endReward, bool trade, int stageIndex, int intercomIndex, int itemIndex, int delta)
        {
            if (endReward)
                return ScenarioStoryFocusedEditorActions.EndRewardQuantity(stageIndex, intercomIndex, itemIndex, delta);
            if (trade)
                return ScenarioStoryFocusedEditorActions.TradeQuantity(stageIndex, intercomIndex, itemIndex, delta);
            if (!removal)
                return ScenarioStoryAuthoringActions.RewardQuantity(stageIndex, intercomIndex, itemIndex, delta);
            return ScenarioStoryAuthoringActions.RemovalQuantity(stageIndex, intercomIndex, itemIndex, delta);
        }

        private static string BuildItemDeleteAction(bool removal, bool endReward, bool trade, int stageIndex, int intercomIndex, int itemIndex)
        {
            if (endReward)
                return ScenarioStoryFocusedEditorActions.EndRewardDelete(stageIndex, intercomIndex, itemIndex);
            if (trade)
                return ScenarioStoryFocusedEditorActions.TradeDelete(stageIndex, intercomIndex, itemIndex);
            if (!removal)
                return ScenarioStoryAuthoringActions.RewardDelete(stageIndex, intercomIndex, itemIndex);
            return ScenarioStoryAuthoringActions.RemovalDelete(stageIndex, intercomIndex, itemIndex);
        }

        private static void AddRecruitActions(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioIntercomStageDefinition intercom, int stageIndex, int intercomIndex)
        {
            List<string> ids = BuildCharacterIds(definition);
            for (int i = 0; ids != null && i < ids.Count; i++)
            {
                string id = ids[i];
                bool selected = intercom != null && Contains(intercom.CharacterIdsToRecruit, id);
                items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.RecruitToggle(stageIndex, intercomIndex, id), "Recruit " + FormatCharacterLabel(definition, id), "Toggle recruitment for this character.", true, selected, "RC")));
            }
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.RecruitFamily(stageIndex, intercomIndex), "Recruit As Family", "Toggle whether recruited characters join the family roster.", true, intercom != null && intercom.RecruitAsFamily, "RF")));
        }

        private static void AddStageIdActions(List<ScenarioAuthoringInspectorItem> items, ScenarioFlowDefinition flow, int stageIndex)
        {
            int count = flow != null && flow.Stages != null ? flow.Stages.Count + 1 : 1;
            string candidate = "stage_" + count.ToString(CultureInfo.InvariantCulture);
            items.Add(ActionItem(Action(ScenarioStoryAuthoringActions.StageId(stageIndex, candidate), "Use Id " + candidate, "Rename using the next stage-id pattern.", true, false, "ID")));
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
            if (!string.IsNullOrEmpty(title))
                return title;
            return !string.IsNullOrEmpty(stage != null ? stage.Id : null)
                ? stage.Id
                : "Stage " + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static string DisplayIntercomTitle(ScenarioIntercomStageDefinition intercom, int index)
        {
            return !string.IsNullOrEmpty(intercom != null ? intercom.Id : null)
                ? intercom.Id
                : "Step " + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static string PrimaryEncounterType(ScenarioFlowStageDefinition stage)
        {
            ScenarioIntercomStageDefinition first = stage != null && stage.IntercomStages != null && stage.IntercomStages.Count > 0 ? stage.IntercomStages[0] : null;
            return Empty(first != null ? first.Type : null, "No encounter step yet.");
        }

        private static string FormatIgnoredCall(ScenarioFlowStageDefinition stage)
        {
            if (stage == null || string.IsNullOrEmpty(stage.UnansweredNextStage))
                return "No explicit route - vanilla keeps this stage active.";
            return "After delay, route to " + stage.UnansweredNextStage + ".";
        }

        private static string FormatStageTarget(string value)
        {
            return string.IsNullOrEmpty(value) ? "No next stage selected - this outcome stays in the current stage or ends." : value;
        }

        private static string FormatIntercomTarget(string value)
        {
            return string.IsNullOrEmpty(value) ? "No step selected - encounter can end or use end options." : value;
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

                if (character.ActorRef == null)
                    return characterId;

                return characterId + " -> " + ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, character.ActorRef, true, true, characterId);
            }

            return characterId;
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

        private static ScenarioAuthoringInspectorAction Action(string id, string label, string hint, bool enabled, bool emphasized, string iconText, string detail, string badge, UnityEngine.Sprite sprite)
        {
            return ScenarioInspectorItemFactory.Action(id, label, hint, enabled, emphasized, iconText, detail, badge, sprite);
        }
    }
}
