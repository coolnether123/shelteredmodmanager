using System;
using System.Collections.Generic;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioOverviewAuthoringContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        public ScenarioAuthoringWindowContentKind ContentKind { get { return ScenarioAuthoringWindowContentKind.Scenario; } }

        public ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioAuthoringState state = context != null ? context.State : null;
            ScenarioEditorSession editorSession = context != null ? context.EditorSession : null;
            ScenarioAuthoringSession authoringSession = context != null ? context.Session : null;
            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            ScenarioScoringAuthoringSummary.Summary scoring = ScenarioScoringAuthoringSummary.Build(definition);
            ScenarioHomeProgressFacts facts = ScenarioHomeProgressFacts.Build(definition, editorSession);
            bool showAdvancedDetails = state != null && state.Settings != null && state.Settings.GetBool("debug.show_advanced_details", false);
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "home_identity",
                Title = string.Empty,
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = BuildIdentityItems(editorSession, definition)
            });
            AddSetupChecklistSection(sections, state, definition);
            sections.Add(BuildBaseModeSection(definition, authoringSession));
            AddQuestionSections(sections, facts);
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "home_quick_actions",
                Title = "Scenario Setup",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSave, "Save Draft", "Persist the current scenario draft XML.", true, false, "SV")),
                    Item.ActionItem(Item.Action("stage.select." + ScenarioStageKind.Quests, "Story", "Open the story workspace for quests and dialogue beats.", true, false, "STORY")),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionPlaytest, editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting ? "Stop Test" : "Start Test Scenario", "Toggle scenario playtest mode.", true, false, "TS"))
                }
            });
            if (showAdvancedDetails)
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "home_advanced",
                    Title = "Advanced",
                    Expanded = false,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = BuildAdvancedItems(state, editorSession, scoring)
                });
            }

            return sections.ToArray();
        }

        private static ScenarioAuthoringInspectorItem[] BuildIdentityItems(
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(EditableProperty("Title", Item.Safe(definition != null ? definition.DisplayName : null)));
            items.Add(Item.Property("Base", FormatBaseMode(definition != null ? definition.BaseGameMode : ScenarioBaseGameMode.Survival)));
            items.Add(Item.Property("Save State", Item.CountDirtyFlags(editorSession) == 0 ? "Saved" : "Unsaved changes"));
            return items.ToArray();
        }

        private static ScenarioAuthoringInspectorSection BuildBaseModeSection(
            ScenarioDefinition definition,
            ScenarioAuthoringSession authoringSession)
        {
            ScenarioBaseGameMode mode = definition != null ? definition.BaseGameMode : ScenarioBaseGameMode.Survival;
            ScenarioBaseGameMode worldMode = authoringSession != null ? authoringSession.BaseMode : mode;
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Current", FormatBaseMode(mode)));
            if (worldMode != mode)
                items.Add(Item.Text("World shows " + FormatBaseMode(worldMode) + "; reopens as " + FormatBaseMode(mode) + "."));
            items.Add(Item.Property("Changes", "Base game rules and starting scene this scenario builds on."));
            items.Add(Item.Property("Map Data", "Quests and world map data are kept as authored."));
            items.Add(Item.Property("Supported Bases",
                "Standard " + (mode == ScenarioBaseGameMode.Survival ? "selected" : "available")
                + " / Stasis " + (mode == ScenarioBaseGameMode.Stasis ? "selected" : "available")
                + " / Surrounded " + (mode == ScenarioBaseGameMode.Surrounded ? "selected" : "available")));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionScenarioModePrevious, "< " + ResolveAdjacentModeName(definition, -1), "Choose how to switch to the previous supported base.", true, false, "M-")));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionScenarioModeNext, ResolveAdjacentModeName(definition, 1) + " >", "Choose how to switch to the next supported base.", true, false, "M+")));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "home_base_mode",
                Title = "Scenario Base",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorItem EditableProperty(string label, string value)
        {
            ScenarioAuthoringInspectorItem item = Item.Property(label, value);
            item.Editable = true;
            return item;
        }

        private static void AddQuestionSections(List<ScenarioAuthoringInspectorSection> sections, ScenarioHomeProgressFacts facts)
        {
            sections.Add(BuildQuestionSection("home_world", "Where does your story take place?", "Build rooms, objects, and scenery in the shelter world.", facts.WorldBadge, "stage.select." + ScenarioStageKind.Bunker, "Open World", "WORLD", TutorialContent.TopicWorldCamera, TutorialContent.TourEditorBasics));
            sections.Add(BuildQuestionSection("home_people", "Who lives in this world?", "Create the starting family and future arrivals.", facts.PeopleBadge, "stage.select." + ScenarioStageKind.People, "Open Cast", "CAST", TutorialContent.TopicCast, null));
            sections.Add(BuildQuestionSection("home_inventory", "What do they start with?", "Set starting supplies and scheduled deliveries.", facts.InventoryBadge, "stage.select." + ScenarioStageKind.InventoryStorage, "Open Supplies", "SUP", TutorialContent.TopicSupplies, null));
            sections.Add(BuildQuestionSection("home_events", "What happens, and when?", "Schedule events, triggers, and story beats.", facts.EventsBadge, "stage.select." + ScenarioStageKind.Events, "Open Timeline", "TIME", TutorialContent.TopicTimelineConditions, TutorialContent.TourTimelineEvent));
            sections.Add(BuildQuestionSection("home_art", "How does it look?", "Browse, replace, and edit sprites.", facts.ArtBadge, ScenarioAuthoringActionIds.ActionToolAssets, "Open Art", "ART", TutorialContent.TopicArtPixelEditor, TutorialContent.TourEditSprite));
            sections.Add(BuildQuestionSection("home_test", "Ready to try it?", "Playtest your scenario live.", facts.PlaytestBadge, "stage.select." + ScenarioStageKind.Test, "Open Test", "TEST", TutorialContent.TopicTest, null));
            sections.Add(BuildQuestionSection("home_publish", "Ready to share it?", "Validate and export.", facts.PublishBadge, "stage.select." + ScenarioStageKind.Publish, "Open Publish", "PUB", TutorialContent.TopicPublish, null));
        }

        private static ScenarioAuthoringInspectorSection BuildQuestionSection(string id, string question, string answer, string badge, string actionId, string actionLabel, string iconText, string topicId, string tourId)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.ActionItem(Item.Action(actionId, actionLabel, answer, true, false, iconText)));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + topicId, "Learn More", "Open help for this setup area.", true, false, "HELP")));
            if (!string.IsNullOrEmpty(tourId))
                items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionTourStartPrefix + tourId, "Walk Me Through It", "Start the related spotlight tour.", true, true, "TO")));
            items.Add(Item.Text(answer));
            items.Add(Item.Text(badge));

            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = question,
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = items.ToArray()
            };
        }

        private static void AddSetupChecklistSection(
            List<ScenarioAuthoringInspectorSection> sections,
            ScenarioAuthoringState state,
            ScenarioDefinition definition)
        {
            ScenarioAuthoringSetupState setup = state != null ? state.SetupState : null;
            if (setup == null || !setup.SetupFlowEnabled || setup.ChecklistDismissed)
                return;

            bool named = HasCustomName(definition);
            bool baseSelected = definition != null && Enum.IsDefined(typeof(ScenarioBaseGameMode), definition.BaseGameMode);
            bool worldTourDone = setup.HasCompletedTour(TutorialContent.TourEditorBasics);
            bool firstSurvivor = HasNamedStartingSurvivor(definition);
            if (named && baseSelected && worldTourDone && firstSurvivor)
                return;

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Text("Set up your scenario"));
            items.Add(BuildChecklistAction("Name", named, ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + TutorialContent.TopicSetup, "Review title and draft identity."));
            items.Add(BuildChecklistAction("Base", baseSelected, ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + TutorialContent.TopicBaseModes, "Review the selected base mode."));
            items.Add(BuildChecklistAction("World Tour", worldTourDone, ScenarioAuthoringActionIds.ActionTourStartPrefix + TutorialContent.TourEditorBasics, "Walk through the world and shell basics."));
            items.Add(BuildChecklistAction("First Survivor", firstSurvivor, "stage.select." + ScenarioStageKind.People, "Open Cast and add a starting survivor."));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSetupDismiss, "Dismiss", "Hide this setup checklist for the draft.", true, false, "X")));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "home_setup_checklist",
                Title = "Set Up Your Scenario",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = items.ToArray()
            });
        }

        private static ScenarioAuthoringInspectorItem BuildChecklistAction(string label, bool complete, string actionId, string hint)
        {
            return Item.ActionItem(Item.Action(actionId, (complete ? "Done: " : "Start: ") + label, hint, !complete, !complete, complete ? "OK" : "GO"));
        }

        private static bool HasCustomName(ScenarioDefinition definition)
        {
            return definition != null
                && !string.IsNullOrEmpty(definition.DisplayName)
                && !string.Equals(definition.DisplayName.Trim(), "Untitled Scenario", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasNamedStartingSurvivor(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.Members != null && i < definition.FamilySetup.Members.Count; i++)
            {
                FamilyMemberConfig member = definition.FamilySetup.Members[i];
                if (member != null && !string.IsNullOrEmpty(member.Name) && member.Name.Trim().Length > 0)
                    return true;
            }

            return false;
        }

        private static ScenarioAuthoringInspectorItem[] BuildAdvancedItems(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioScoringAuthoringSummary.Summary scoring)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Simulation", ScenarioAuthoringRuntimeGuards.IsPlaytesting() ? "Running (test)" : "Paused for workshop"));
            items.Add(Item.Property("Playtest", editorSession != null ? editorSession.PlaytestState.ToString() : "Unavailable"));
            items.Add(Item.Property("Applied To World", FormatAppliedState(editorSession)));
            items.Add(Item.Property("Scoring", scoring.Status));
            items.Add(Item.Property("Score Rules", scoring.RuleCount.ToString()));
            items.Add(Item.Property("Draft Id", Item.Safe(state != null ? state.ActiveDraftId : null)));
            items.Add(Item.Property("Dirty Sections", Item.CountDirtyFlags(editorSession).ToString()));

            return items.ToArray();
        }

        private static string ResolveAdjacentModeName(ScenarioDefinition definition, int direction)
        {
            ScenarioBaseGameMode mode = definition != null ? definition.BaseGameMode : ScenarioBaseGameMode.Survival;
            int count = Enum.GetValues(typeof(ScenarioBaseGameMode)).Length;
            int next = ((int)mode + direction + count) % count;
            return FormatBaseMode((ScenarioBaseGameMode)next);
        }

        private static string FormatBaseMode(ScenarioBaseGameMode mode)
        {
            if (mode == ScenarioBaseGameMode.Survival)
                return "Standard";
            return mode.ToString();
        }

        private static string FormatAppliedState(ScenarioEditorSession editorSession)
        {
            if (editorSession == null || !editorSession.HasAppliedToCurrentWorld)
                return "No";
            return editorSession.HasUnappliedDraftChanges ? "Stale" : "Yes";
        }
    }
}
