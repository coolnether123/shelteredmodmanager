using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioOverviewAuthoringContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        private static readonly object ValidationCacheSync = new object();
        private static ScenarioDefinition _cachedValidationDefinition;
        private static string _cachedValidationPath;
        private static int _cachedValidationRevision = -1;
        private static ScenarioAuthoringValidationSnapshot _cachedValidation;

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
                Items = BuildIdentityItems(state, editorSession, definition)
            });
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "home_metadata",
                Title = "Scenario Details",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = ScenarioMetadataAuthoringContent.BuildEditableItems(definition, false)
            });
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "home_save_status",
                Title = "Save & Export Status",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = ScenarioMetadataAuthoringContent.BuildStatusItems(state != null ? state.ActiveScenarioFilePath : null)
            });
            AddSetupChecklistSection(sections, state, definition);
            sections.Add(BuildBaseModeSection(definition, authoringSession));
            AddQuestionSections(sections, facts, definition);
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "home_quick_actions",
                Title = "Quick Actions",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    Item.ActionItem(Item.Action("stage.select." + ScenarioStageKind.Quests, "Open Story", "Open the story workspace for quests and dialogue beats.", true, false, "STORY"))
                }
            });
            if (showAdvancedDetails)
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "home_metadata_advanced",
                    Title = "Advanced Metadata",
                    Expanded = false,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[] { ScenarioMetadataAuthoringContent.BuildIdItem(definition) }
                });
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
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioAuthoringValidationSnapshot validation = GetCachedValidation(state, editorSession, definition);
            string validationLabel = FormatValidationChip(validation);
            string playtestDisabledReason;
            string playtestLabel = FormatPlaytestReadiness(editorSession, validation, definition, out playtestDisabledReason);
            bool canOpenTest = string.IsNullOrEmpty(playtestDisabledReason);
            int dirtyCount = Item.CountDirtyFlags(editorSession);
            items.Add(EditableProperty("Title", Item.Safe(definition != null ? definition.DisplayName : null)));
            items.Add(BuildGoalItem(definition));
            items.Add(BuildVictoryItem(definition));
            items.Add(Item.ActionItem(Item.Action(
                ScenarioAuthoringActionIds.ActionSave,
                dirtyCount == 0 ? "Saved" : "Unsaved changes",
                dirtyCount == 0 ? "No unsaved draft changes." : "Save the current scenario draft.",
                dirtyCount > 0,
                dirtyCount > 0,
                "SV")));
            items.Add(Item.ActionItem(Item.Action(
                "stage.select." + ScenarioStageKind.Publish,
                validationLabel,
                BuildHomeValidationHint(validation),
                true,
                false,
                validation != null && validation.ErrorCount > 0 ? "!" : "OK")));
            ScenarioValidationIssue topIssue = ScenarioTopIssueResolver.ResolveTopIssue(validation);
            if (topIssue != null)
            {
                items.Add(Item.Text("Next: " + topIssue.Message));
                ScenarioAuthoringInspectorAction nextAction = ScenarioTopIssueResolver.BuildNextAction(topIssue);
                if (nextAction != null)
                    items.Add(Item.ActionItem(nextAction));
            }
            ScenarioAuthoringInspectorAction testAction = Item.Action(
                "stage.select." + ScenarioStageKind.Test,
                playtestLabel,
                canOpenTest ? "Open the Test workspace." : playtestDisabledReason,
                canOpenTest,
                string.Equals(playtestLabel, "Ready to test", StringComparison.OrdinalIgnoreCase),
                "TS",
                canOpenTest ? null : playtestDisabledReason);
            if (!canOpenTest)
            {
                testAction.DisabledReason = playtestDisabledReason;
                ScenarioAuthoringInspectorAction fixAction = ScenarioPlaytestFixActionResolver.BuildFixAction(playtestDisabledReason);
                if (fixAction != null)
                    items.Add(Item.ActionItem(fixAction));
            }
            items.Add(Item.ActionItem(testAction));
            items.Add(Item.ActionItem(Item.Action(
                ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + TutorialContent.TopicPublish,
                "What Draft Health Means",
                "Open publish, validation, and export guidance for this card.",
                true,
                false,
                "HP")));
            return items.ToArray();
        }

        private static ScenarioAuthoringInspectorItem BuildGoalItem(ScenarioDefinition definition)
        {
            ScenarioAuthoringInspectorItem item = Item.Property("Goal", Item.Safe(definition != null ? definition.Goal : null));
            item.Editable = true;
            item.HoverHint = "What is the player trying to do? One line, shown to players and in the export README.";
            item.Action = Item.Action(
                ScenarioAuthoringActionIds.ActionDraftGoalPrefix,
                "Commit Goal",
                "Update the scenario goal players read.",
                true,
                false,
                "GL");
            return item;
        }

        private static ScenarioAuthoringInspectorItem BuildVictoryItem(ScenarioDefinition definition)
        {
            ScenarioAuthoringInspectorItem item = Item.Property("Victory", FormatVictorySummary(definition));
            item.HoverHint = "The victory condition backing the goal above. Author intent (Goal) versus implementation (Victory).";
            return item;
        }

        internal static string FormatVictorySummary(ScenarioDefinition definition)
        {
            WinLossConditionsDefinition winLoss = definition != null ? definition.WinLossConditions : null;
            int wins = winLoss != null && winLoss.WinConditions != null ? winLoss.WinConditions.Count : 0;
            int losses = winLoss != null && winLoss.LossConditions != null ? winLoss.LossConditions.Count : 0;
            if (!ScenarioPacingAnalysisService.HasAuthoredEndCondition(definition))
                return "No victory condition - scenario runs forever";
            return wins.ToString(CultureInfo.InvariantCulture) + " win / " + losses.ToString(CultureInfo.InvariantCulture) + " loss condition(s)";
        }

        private static ScenarioAuthoringValidationSnapshot GetCachedValidation(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition)
        {
            string path = state != null ? state.ActiveScenarioFilePath : null;
            int revision = editorSession != null ? editorSession.DraftRevision : -1;
            lock (ValidationCacheSync)
            {
                if (_cachedValidation != null
                    && ReferenceEquals(_cachedValidationDefinition, definition)
                    && string.Equals(_cachedValidationPath, path, StringComparison.OrdinalIgnoreCase)
                    && _cachedValidationRevision == revision)
                {
                    return _cachedValidation;
                }

                IScenarioDefinitionValidator validator = null;
                try
                {
                    validator = ScenarioCompositionRoot.Resolve<IScenarioDefinitionValidator>();
                }
                catch
                {
                    validator = null;
                }

                _cachedValidation = ScenarioAuthoringValidationSnapshot.Evaluate(validator, definition, path);
                _cachedValidationDefinition = definition;
                _cachedValidationPath = path;
                _cachedValidationRevision = revision;
                return _cachedValidation;
            }
        }

        private static string FormatValidation(ScenarioAuthoringValidationSnapshot validation)
        {
            if (validation == null || !validation.ValidationAvailable)
                return "Unavailable";
            if (validation.ErrorCount > 0)
                return validation.ErrorCount.ToString(CultureInfo.InvariantCulture) + " error(s), " + validation.WarningCount.ToString(CultureInfo.InvariantCulture) + " warning(s)";
            if (validation.WarningCount > 0)
                return "Ready with " + validation.WarningCount.ToString(CultureInfo.InvariantCulture) + " warning(s)";
            return "Ready";
        }

        private static string FormatValidationChip(ScenarioAuthoringValidationSnapshot validation)
        {
            if (validation == null || !validation.ValidationAvailable)
                return "Validation unavailable";
            if (validation.ErrorCount > 0)
                return validation.ErrorCount.ToString(CultureInfo.InvariantCulture) + " errors";
            if (validation.WarningCount > 0)
                return validation.WarningCount.ToString(CultureInfo.InvariantCulture) + " warnings";
            return "OK";
        }

        private static string FormatPlaytestReadiness(
            ScenarioEditorSession editorSession,
            ScenarioAuthoringValidationSnapshot validation,
            ScenarioDefinition definition,
            out string disabledReason)
        {
            disabledReason = null;
            if (editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting)
                return "Running";
            string playStartReason;
            if (!new ScenarioPlayStartReadiness().CanStartPlay(definition, out playStartReason))
            {
                disabledReason = playStartReason;
                return "No starting survivors";
            }
            if (Item.CountDirtyFlags(editorSession) > 0)
            {
                disabledReason = ScenarioPlayStartReadiness.UnsavedDraftDisabledReason;
                return "Save draft before testing";
            }
            if (validation == null || !validation.ValidationAvailable)
            {
                disabledReason = ScenarioPlayStartReadiness.ValidationUnavailableDisabledReason;
                return "Validation unavailable";
            }
            if (validation.ErrorCount > 0)
                return "Fix validation errors first";
            return "Ready to test";
        }

        private static ScenarioAuthoringInspectorSection BuildBaseModeSection(
            ScenarioDefinition definition,
            ScenarioAuthoringSession authoringSession)
        {
            ScenarioBaseGameMode mode = definition != null ? definition.BaseGameMode : ScenarioBaseGameMode.Survival;
            ScenarioBaseGameMode worldMode = authoringSession != null ? authoringSession.BaseMode : mode;
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(BuildBaseModeOption(mode, ScenarioBaseGameMode.Survival));
            items.Add(BuildBaseModeOption(mode, ScenarioBaseGameMode.Stasis));
            items.Add(BuildBaseModeOption(mode, ScenarioBaseGameMode.Surrounded));
            if (ScenarioOpeningCutsceneAuthoringService.HasVanillaOpeningCutscene(mode))
            {
                items.Add(Item.ActionItem(Item.Action(
                    ScenarioBaseModeAuthoringActions.ActionWatchOpeningCutscene,
                    "Watch opening cutscene",
                    "Play the selected base mode's opening cutscene on demand.",
                    true,
                    false,
                    "CUT")));
            }
            else
            {
                items.Add(Item.Text(ScenarioOpeningCutsceneAuthoringService.BuildNoOpeningCutsceneMessage(mode)));
            }
            string hint = "Each base mode has its own saved shelter world. Switching saves this world's rooms, objects, walls, wiring, ladders, lights, and scene placements, then restores the target world as you left it. Supplies, cast, story, map, timeline, art, and victory stay shared.";
            if (worldMode != mode)
                hint = "World shows " + FormatBaseMode(worldMode) + "; reopens as " + FormatBaseMode(mode) + ". " + hint;
            items.Add(Item.Text(hint));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "home_base_mode",
                Title = "Scenario Base",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorItem BuildBaseModeOption(ScenarioBaseGameMode current, ScenarioBaseGameMode target)
        {
            bool selected = current == target;
            ScenarioAuthoringInspectorAction action = Item.Action(
                ResolveBaseModeActionId(current, target),
                FormatBaseMode(target),
                selected ? FormatBaseMode(target) + " is the current base." : "Choose how to switch this scenario to " + FormatBaseMode(target) + ".",
                !selected,
                selected,
                selected ? "OK" : null);
            if (selected)
                action.DisabledReason = FormatBaseMode(target) + " is already selected.";
            return Item.ActionItem(action);
        }

        private static string ResolveBaseModeActionId(ScenarioBaseGameMode current, ScenarioBaseGameMode target)
        {
            if (current == target)
                return ScenarioAuthoringActionIds.ActionScenarioModeNext;

            int currentIndex = (int)current;
            int targetIndex = (int)target;
            int count = Enum.GetValues(typeof(ScenarioBaseGameMode)).Length;
            int nextIndex = (currentIndex + 1 + count) % count;
            return targetIndex == nextIndex
                ? ScenarioAuthoringActionIds.ActionScenarioModeNext
                : ScenarioAuthoringActionIds.ActionScenarioModePrevious;
        }

        private static ScenarioAuthoringInspectorItem EditableProperty(string label, string value)
        {
            ScenarioAuthoringInspectorItem item = Item.Property(label, value);
            item.Editable = true;
            if (string.Equals(label, "Title", StringComparison.OrdinalIgnoreCase))
            {
                item.Action = Item.Action(
                    ScenarioAuthoringActionIds.ActionDraftTitlePrefix,
                    "Commit Title",
                    "Update the scenario title.",
                    true,
                    false,
                    "TT");
            }
            return item;
        }

        private static void AddQuestionSections(List<ScenarioAuthoringInspectorSection> sections, ScenarioHomeProgressFacts facts, ScenarioDefinition definition)
        {
            sections.Add(BuildQuestionSection("home_world", "Where does your story take place?", "Build rooms, objects, and scenery in the shelter world.", facts.WorldBadge, "stage.select." + ScenarioStageKind.Bunker, "Open World", "MAP", TutorialContent.TopicWorldCamera, TutorialContent.TourEditorBasics));
            sections.Add(BuildQuestionSection("home_people", "Who lives in this world?", "Create the starting family and future arrivals.", facts.PeopleBadge, "stage.select." + ScenarioStageKind.People, "Open Cast", "CAST", TutorialContent.TopicCast, null));
            sections.Add(BuildQuestionSection("home_inventory", "What do they start with?", "Set starting supplies and scheduled deliveries.", facts.InventoryBadge, "stage.select." + ScenarioStageKind.InventoryStorage, "Open Supplies", "BOX", TutorialContent.TopicSupplies, null));
            sections.Add(BuildQuestionSection("home_events", "What happens, and when?", "Schedule events, triggers, and story beats.", facts.EventsBadge, "stage.select." + ScenarioStageKind.Events, "Open Timeline", "TIME", TutorialContent.TopicTimelineConditions, TutorialContent.TourTimelineEvent));
            sections.Add(BuildQuestionSection("home_art", "How does it look?", "Browse, replace, and edit sprites.", facts.ArtBadge, "stage.select." + ScenarioStageKind.Assets, "Browse Assets", "ART", TutorialContent.TopicArtPixelEditor, TutorialContent.TourEditSprite));
            string playStartReason;
            bool canStartPlay = new ScenarioPlayStartReadiness().CanStartPlay(definition, out playStartReason);
            sections.Add(BuildQuestionSection("home_test", "Ready to try it?", canStartPlay ? "Playtest your scenario live." : playStartReason, facts.PlaytestBadge, "stage.select." + ScenarioStageKind.Test, "Open Test Console", "TEST", TutorialContent.TopicTest, null, canStartPlay ? null : playStartReason));
            sections.Add(BuildQuestionSection("home_publish", "Ready to share it?", "Validate and package a local export.", facts.PublishBadge, "stage.select." + ScenarioStageKind.Publish, "Open Package / Export", "FLAG", TutorialContent.TopicPublish, null));
        }

        private static ScenarioAuthoringInspectorSection BuildQuestionSection(string id, string question, string answer, string badge, string actionId, string actionLabel, string iconText, string topicId, string tourId, string disabledReason = null)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            bool enabled = string.IsNullOrEmpty(disabledReason);
            ScenarioAuthoringInspectorAction primaryAction = Item.Action(actionId, actionLabel, answer, enabled, enabled, iconText, disabledReason);
            if (!enabled)
                primaryAction.DisabledReason = disabledReason;
            items.Add(Item.ActionItem(primaryAction));
            if (!enabled)
            {
                ScenarioAuthoringInspectorAction fixAction = ScenarioPlaytestFixActionResolver.BuildFixAction(disabledReason);
                if (fixAction != null)
                    items.Add(Item.ActionItem(fixAction));
            }
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
            bool hasStartingSurvivor = ScenarioPlayStartReadiness.HasStartingSurvivor(definition);
            if (named && baseSelected && worldTourDone && hasStartingSurvivor)
                return;

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(BuildChecklistAction("Name", named, ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + TutorialContent.TopicSetup, "Review title and draft identity."));
            items.Add(BuildChecklistAction("Base", baseSelected, ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + TutorialContent.TopicBaseModes, "Review the selected base mode."));
            items.Add(BuildChecklistAction("World Tour", worldTourDone, ScenarioAuthoringActionIds.ActionTourStartPrefix + TutorialContent.TourEditorBasics, "Walk through the world and shell basics."));
            items.Add(BuildChecklistAction("First Survivor", hasStartingSurvivor, "stage.select." + ScenarioStageKind.People, "Open Cast and add a starting survivor."));
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
            return Item.ActionItem(Item.Action(actionId, (complete ? "Done: " : "Start: ") + label, hint, !complete, false, complete ? "OK" : "GO"));
        }

        private static bool HasCustomName(ScenarioDefinition definition)
        {
            return definition != null
                && !string.IsNullOrEmpty(definition.DisplayName)
                && !string.Equals(definition.DisplayName.Trim(), "Untitled Scenario", StringComparison.OrdinalIgnoreCase);
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

        private static string BuildHomeValidationHint(ScenarioAuthoringValidationSnapshot validation)
        {
            if (validation == null)
                return "Validation did not run. Open Publish to initialize checks.";
            if (!validation.ValidationAvailable)
                return "Validation is unavailable. Open Publish to refresh checks.";
            if (validation.ErrorCount > 0)
                return "Blocking errors must be fixed before playtest or export. Open Publish for fixes.";
            if (validation.WarningCount > 0)
                return "Warnings are advisory. Open Publish to review impact before shipping.";
            return "Validation is clean. Open Publish to validate and export.";
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
