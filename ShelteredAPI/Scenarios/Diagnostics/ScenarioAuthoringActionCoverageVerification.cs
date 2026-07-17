using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Domain.Story;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;

namespace ShelteredAPI.Scenarios.Diagnostics
{
    internal static class ScenarioAuthoringActionCoverageVerification
    {
        public static void Verify(ScenarioValidationResult result)
        {
            ScenarioAuthoringInspectorAction[] rendererActions = ScenarioAuthoringRendererActionManifest.Build(
                new ScenarioAuthoringState(),
                new ScenarioAuthoringShellWindowViewModel[0],
                null,
                null,
                null);
            ScenarioAuthoringShellViewModel shell = new ScenarioAuthoringShellViewModel
            {
                RendererActions = rendererActions,
                Windows = new ScenarioAuthoringShellWindowViewModel[0]
            };
            ScenarioAuthoringShellWindowViewModel contractWindow = ScenarioAuthoringRendererActionManifest.BuildContractWindow(shell);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            ScenarioAuthoringInspectorItem[] items = contractWindow != null
                && contractWindow.Sections != null
                && contractWindow.Sections.Length > 0
                ? contractWindow.Sections[0].Items
                : null;
            for (int i = 0; items != null && i < items.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = items[i] != null ? items[i].Action : null;
                if (action != null && !string.IsNullOrEmpty(action.Id)) ids.Add(action.Id);
            }

            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererMapFilterTogglePrefix, "map filter", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererPixelGroupTogglePrefix, "pixel group", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererHomeGroupTogglePrefix, "Home group", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererTimelineGroupTogglePrefix, "Timeline group", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererWorkshopGroupTogglePrefix, "Workshop group", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererAssetCategorySelectPrefix, "asset category", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererCandidateSearchPrefix, "candidate search", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererCandidateFilterPrefix, "candidate filter", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererWorkspaceSubtabSelectPrefix, "workspace subtab", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererWorkspaceEntitySelectPrefix, "workspace entity selection", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererWorkspaceWarningOpenPrefix, "workspace warning navigation", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererWorkspaceGroupTogglePrefix, "workspace group toggle", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererWorkspaceRowTogglePrefix, "workspace row toggle", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererWorkspaceSearchSetPrefix, "workspace search", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererWorkspaceBreadcrumbSelectPrefix, "workspace breadcrumb", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererWorkspaceBackPrefix, "workspace Back", result);
            Require(ids, ScenarioAuthoringActionIds.ActionRendererAssetSearchClear, "asset search clear", result);
            Require(ids, ScenarioAuthoringActionIds.ActionRendererPlacementBack, "placement Back", result);
            Require(ids, ScenarioAuthoringActionIds.ActionRendererPlacementDone, "placement Done", result);
            Require(ids, ScenarioAuthoringActionIds.ActionSettingTogglePrefix + "visuals.snap_to_grid", "snap toggle", result);
            Require(ids, ScenarioAuthoringActionIds.ActionSettingTogglePrefix + "visuals.show_grid", "grid toggle", result);
        }

        public static void VerifyWorkspaceFoundation(ScenarioValidationResult result)
        {
            const string workspaceId = "fixture.workspace";
            const string subtabId = "fixture.subtab";
            ScenarioAuthoringWorkspaceViewModelFactory factory = new ScenarioAuthoringWorkspaceViewModelFactory();
            ScenarioAuthoringInspectorAction subtabAction = factory.CreateSubtabAction(workspaceId, subtabId, "Flow");
            ScenarioAuthoringInspectorAction entityAction = factory.CreateEntityAction(workspaceId, subtabId, "stage.1", "Stage 1");
            ScenarioAuthoringInspectorAction warningAction = factory.CreateWarningAction(workspaceId, subtabId, "stage.2", "Open warning");
            ScenarioAuthoringInspectorAction groupAction = factory.CreateGroupToggleAction(workspaceId, subtabId, "stages", "Toggle Stages");
            ScenarioAuthoringInspectorAction rowAction = factory.CreateRowToggleAction(workspaceId, subtabId, "stage.1", "Toggle Stage 1");
            ScenarioAuthoringInspectorAction searchAction = factory.CreateSearchAction(workspaceId, subtabId, "find me", "Search");
            ScenarioAuthoringInspectorAction breadcrumbAction = factory.CreateBreadcrumbAction(workspaceId, subtabId, "overview", "Story");
            ScenarioAuthoringInspectorAction backAction = factory.CreateBackAction(workspaceId, subtabId, "Back");

            ScenarioAuthoringInspectorSection section = factory.CreateSection("fixture.section", "Fixture", false);
            section.StatusChips = new[] { Chip("fixture.section.chip") };
            section.Items = new[]
            {
                new ScenarioAuthoringInspectorItem
                {
                    Kind = ScenarioAuthoringInspectorItemKind.Choice,
                    Choice = new ScenarioAuthoringCompactChoiceViewModel
                    {
                        Id = "fixture.choice",
                        Label = "Choice",
                        Options = new[]
                        {
                            new ScenarioAuthoringCompactChoiceOptionViewModel
                            {
                                Id = "one",
                                Label = "One",
                                Action = FixtureAction("fixture.choice.option")
                            }
                        }
                    }
                }
            };
            section.StoryMap = new ScenarioStoryGraphModel
            {
                Nodes = new[]
                {
                    new ScenarioStoryGraphNode
                    {
                        Id = "stage.1",
                        Label = "Stage 1",
                        NavActionId = "fixture.story_map.node"
                    }
                }
            };

            ScenarioAuthoringWorkspaceViewModel workspace = factory.CreateWorkspace(
                workspaceId,
                ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument,
                subtabId);
            workspace.Subtabs = new[]
            {
                new ScenarioAuthoringWorkspaceSubtabViewModel
                {
                    Id = subtabId,
                    Label = "Flow",
                    SelectAction = subtabAction,
                    StatusChips = new[] { Chip("fixture.subtab.chip") }
                }
            };
            workspace.Navigator = factory.CreateNavigator("fixture.navigator");
            workspace.Navigator.Groups = new[]
            {
                new ScenarioAuthoringNavigatorGroupViewModel
                {
                    Id = "stages",
                    Label = "Stages",
                    ToggleAction = groupAction,
                    CreateAction = FixtureAction("fixture.group.create"),
                    StatusChips = new[] { Chip("fixture.group.chip") },
                    Rows = new[]
                    {
                        new ScenarioAuthoringNavigatorRowViewModel
                        {
                            EntityId = "stage.1",
                            Title = "Stage 1",
                            SelectAction = entityAction,
                            ToggleAction = rowAction,
                            StatusChips = new[]
                            {
                                new ScenarioAuthoringStatusChipViewModel
                                {
                                    Id = "fixture.row.warning",
                                    Text = "Warning",
                                    Tone = ScenarioAuthoringStatusTone.Warning,
                                    Action = warningAction
                                }
                            },
                            Children = new[]
                            {
                                new ScenarioAuthoringNavigatorRowViewModel
                                {
                                    EntityId = "scene.1",
                                    Title = "Scene 1",
                                    SelectAction = FixtureAction("fixture.child.select")
                                }
                            }
                        }
                    }
                }
            };
            workspace.Document = factory.CreateDocument("fixture.document", "Stage 1");
            workspace.Document.BackAction = backAction;
            workspace.Document.HeaderActions = new[] { FixtureAction("fixture.document.header") };
            workspace.Document.StatusChips = new[] { Chip("fixture.document.chip") };
            workspace.Document.Breadcrumbs = new[]
            {
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Story", Action = breadcrumbAction }
            };
            workspace.Document.Sections = new[] { section };

            ScenarioAuthoringShellWindowViewModel fixtureWindow = new ScenarioAuthoringShellWindowViewModel
            {
                Id = "fixture.window",
                WorkspaceBody = workspace,
                Sections = new[]
                {
                    new ScenarioAuthoringInspectorSection
                    {
                        Id = "legacy.must.not.project",
                        Items = new[]
                        {
                            new ScenarioAuthoringInspectorItem
                            {
                                Kind = ScenarioAuthoringInspectorItemKind.Action,
                                Action = FixtureAction("fixture.legacy.must.not.collect")
                            }
                        }
                    }
                }
            };
            ScenarioAuthoringShellViewModel shell = new ScenarioAuthoringShellViewModel
            {
                RendererActions = new[] { searchAction },
                Windows = new[] { fixtureWindow }
            };
            HashSet<string> ids = CollectContractIds(ScenarioAuthoringRendererActionManifest.BuildContractWindow(shell));
            string[] expected =
            {
                subtabAction.Id,
                entityAction.Id,
                warningAction.Id,
                groupAction.Id,
                rowAction.Id,
                searchAction.Id,
                breadcrumbAction.Id,
                backAction.Id,
                "fixture.subtab.chip",
                "fixture.group.create",
                "fixture.group.chip",
                "fixture.child.select",
                "fixture.document.header",
                "fixture.document.chip",
                "fixture.section.chip",
                "fixture.choice.option",
                "fixture.story_map.node"
            };
            for (int i = 0; i < expected.Length; i++)
                Require(ids, expected[i], "workspace fixture", result);
            if (ids.Contains("fixture.legacy.must.not.collect"))
                result.AddError("Workspace contract projection also collected the legacy window Sections path.");

            ScenarioAuthoringWorkspaceRenderPlan widePlan = ScenarioAuthoringShellImguiRenderModule.BuildWorkspaceRenderPlan(
                new Rect(0f, 0f, 1440f, 680f),
                fixtureWindow,
                true);
            if (!widePlan.Wide
                || !widePlan.ShowsNavigator
                || !widePlan.ShowsDocument
                || widePlan.VisibleScrollOwnerCount != 2
                || string.IsNullOrEmpty(widePlan.NavigatorScrollOwnerId)
                || string.IsNullOrEmpty(widePlan.DocumentScrollOwnerId)
                || string.Equals(widePlan.NavigatorScrollOwnerId, widePlan.DocumentScrollOwnerId, StringComparison.Ordinal)
                || widePlan.NavigatorRect.width < ScenarioAuthoringShellLayout.MasterDetailNavigatorMinWidth
                || widePlan.NavigatorRect.width > ScenarioAuthoringShellLayout.MasterDetailNavigatorMaxWidth
                || widePlan.DocumentRect.x < widePlan.NavigatorRect.xMax + ScenarioAuthoringShellLayout.MasterDetailPaneGutter)
            {
                result.AddError("Wide workspace renderer fixture did not produce two independent navigator/document scroll panes.");
            }

            Rect compactTop = new Rect(0f, 0f, 1280f, ScenarioAuthoringShellLayout.TopBarHeight);
            Rect compactStatus = ScenarioAuthoringShellLayout.BuildStatusRect(1280f, 720f);
            Rect compactContent = ScenarioAuthoringShellLayout.BuildContentRect(1280f, compactTop, compactStatus);
            Rect compactPage = ScenarioAuthoringShellLayout.BuildWorkshopPageRect(compactContent);
            Rect compactBody = new Rect(
                compactPage.x,
                compactPage.y + ScenarioAuthoringShellLayout.WorkshopTimelineRibbonHeight + ScenarioAuthoringShellLayout.WorkshopTimelineRibbonBodyGutter,
                compactPage.width,
                compactPage.height - ScenarioAuthoringShellLayout.WorkshopTimelineRibbonHeight - ScenarioAuthoringShellLayout.WorkshopTimelineRibbonBodyGutter);
            ScenarioAuthoringWorkspaceRenderPlan compactPlan = ScenarioAuthoringShellImguiRenderModule.BuildWorkspaceRenderPlan(
                compactBody,
                fixtureWindow,
                true);
            if (compactPlan.Wide
                || compactPlan.ShowsNavigator
                || !compactPlan.ShowsDocument
                || compactPlan.VisibleScrollOwnerCount != 1)
            {
                result.AddError("1280x720 workspace renderer fixture did not select the one-pane document fallback.");
            }

            VerifyWorkspaceCommandState(result, workspaceId, subtabId, subtabAction, entityAction, warningAction, groupAction, rowAction, searchAction, breadcrumbAction, backAction);
            VerifyDuplicateContractIds(result);

            ScenarioAuthoringWorkspaceComposer composer = new ScenarioAuthoringWorkspaceComposer();
            Array contentKinds = Enum.GetValues(typeof(ScenarioAuthoringWindowContentKind));
            ScenarioAuthoringWindowContentContext context = new ScenarioAuthoringWindowContentContext(
                new ScenarioAuthoringState(),
                null,
                null,
                null);
            for (int i = 0; i < contentKinds.Length; i++)
            {
                ScenarioAuthoringWindowContentKind contentKind = (ScenarioAuthoringWindowContentKind)contentKinds.GetValue(i);
                ScenarioAuthoringWorkspaceViewModel composed = composer.Build(contentKind, context);
                if (contentKind == ScenarioAuthoringWindowContentKind.Quests)
                {
                    if (composed == null || composed.Subtabs == null || composed.Subtabs.Length != 4)
                        result.AddError("Story content kind did not produce its four-tab WorkspaceBody.");
                }
                else if (contentKind == ScenarioAuthoringWindowContentKind.Survivors)
                {
                    if (composed == null || composed.LayoutKind != ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument)
                        result.AddError("Cast content kind did not produce its navigator-document WorkspaceBody.");
                }
                else if (composed != null)
                    result.AddError("Unmigrated window content kind '" + contentKind + "' unexpectedly produced a WorkspaceBody.");
            }
            VerifyStoryWorkspace(result);
            VerifyCastWorkspace(result);
        }

        private static void VerifyCastWorkspace(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.FamilySetup = new FamilySetupDefinition();
            FamilyMemberConfig starting = new FamilyMemberConfig { Name = "Morgan", Gender = ScenarioGender.Any };
            definition.FamilySetup.Members.Add(starting);
            FutureSurvivorDefinition future = new FutureSurvivorDefinition
            {
                Id = "future_storage_id",
                Arrival = new ScenarioScheduleTime { Day = 3, Hour = 9 },
                Survivor = new FamilyMemberConfig { Name = "Riley", Gender = ScenarioGender.Any }
            };
            definition.FamilySetup.FutureSurvivors.Add(future);

            ScenarioAuthoringWindowContentContext context = new ScenarioAuthoringWindowContentContext(
                new ScenarioAuthoringState(),
                null,
                null,
                definition);
            ScenarioAuthoringWorkspaceComposer composer = new ScenarioAuthoringWorkspaceComposer();
            ScenarioAuthoringWorkspaceViewModel workspace = composer.Build(ScenarioAuthoringWindowContentKind.Survivors, context);
            if (workspace == null || workspace.Navigator == null || workspace.Navigator.Groups == null || workspace.Navigator.Groups.Length != 2)
            {
                result.AddError("Cast workspace did not produce the two authored survivor navigator groups.");
                return;
            }
            if (!string.Equals(workspace.Navigator.Groups[0].Label, "Starting Survivors", StringComparison.Ordinal)
                || !string.Equals(workspace.Navigator.Groups[1].Label, "Future Arrivals", StringComparison.Ordinal))
                result.AddError("Cast navigator group labels changed from Starting Survivors and Future Arrivals.");

        }

        private static void VerifyStoryWorkspace(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            ScenarioFlowStageDefinition stage = new ScenarioFlowStageDefinition { Id = "opening_stage" };
            ScenarioIntercomStageDefinition scene = new ScenarioIntercomStageDefinition
            {
                Id = "first_scene",
                StageDescriptionKey = "First Contact"
            };
            scene.Dialogue.Add(new ScenarioDialogueLineDefinition { Character = "LeadNpc", TextKey = "Welcome to the shelter" });
            stage.IntercomStages.Add(scene);
            definition.ScenarioFlow.Stages.Add(stage);
            definition.ScenarioCharacters.Add(new ScenarioNpcDefinition { CharacterId = "guide", DisplayName = "The Guide" });
            ScenarioConversationDefinition conversation = new ScenarioConversationDefinition { Id = "welcome_chat" };
            conversation.Participants.Add(new ScenarioConversationParticipantDefinition { Slot = "Guide", StoryCharacterId = "guide" });
            conversation.Lines.Add(new ScenarioConversationLineDefinition { SpeakerSlot = "Guide", RawText = "We should get moving." });
            definition.Conversations.Conversations.Add(conversation);

            ScenarioAuthoringRendererInteractionState state = ScenarioAuthoringRendererInteractionState.Instance;
            state.SetWorkspaceSubtab(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId);
            state.SetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, null);
            ScenarioStoryWorkspaceViewModelBuilder builder = new ScenarioStoryWorkspaceViewModelBuilder();
            ScenarioAuthoringWorkspaceViewModel workspace = builder.Build(new ScenarioAuthoringWindowContentContext(null, null, null, definition));
            string[] labels = { "Flow", "Characters", "Conversations", "Quest Popups" };
            if (workspace == null || workspace.Subtabs == null || workspace.Subtabs.Length != labels.Length)
            {
                result.AddError("Story workspace did not expose the four fixed local tabs.");
                return;
            }
            for (int i = 0; i < labels.Length; i++)
                if (!string.Equals(workspace.Subtabs[i].Label, labels[i], StringComparison.Ordinal)) result.AddError("Story workspace tab label/order changed: expected " + labels[i] + ".");
            if (workspace.Navigator == null || workspace.Navigator.Groups == null || workspace.Navigator.Groups.Length != 2
                || !string.Equals(workspace.Navigator.Groups[0].Label, "Stages", StringComparison.Ordinal)
                || !string.Equals(workspace.Navigator.Groups[1].Label, "Endings", StringComparison.Ordinal))
                result.AddError("Story Flow navigator is not Stages-to-Scenes plus Endings.");
            if (workspace.Document == null || workspace.Document.Sections == null || workspace.Document.Sections.Length < 2
                || workspace.Document.Sections[0].StoryMap == null
                || !string.Equals(workspace.Document.Sections[1].Title, "STORY FACTS", StringComparison.Ordinal))
                result.AddError("Story Flow overview is missing Story Map or Story facts.");

            ScenarioStoryFocusedEditorActions.SelectSceneDocument(definition, 0, 0);
            workspace = builder.Build(new ScenarioAuthoringWindowContentContext(null, null, null, definition));
            string[] sceneSections = { "DIALOGUE", "CHOICES", "OUTCOME", "ADVANCED" };
            if (workspace.Document == null || workspace.Document.Sections == null || workspace.Document.Sections.Length != sceneSections.Length)
                result.AddError("Selected Story scene did not produce its four document cards.");
            else
            {
                for (int i = 0; i < sceneSections.Length; i++)
                    if (!string.Equals(workspace.Document.Sections[i].Title, sceneSections[i], StringComparison.Ordinal)) result.AddError("Story scene card order changed at " + sceneSections[i] + ".");
                if (!workspace.Document.Sections[workspace.Document.Sections.Length - 1].IsAdvanced)
                    result.AddError("Story scene Advanced card is not last.");
                if (!ContainsCompactChoice(workspace.Document.Sections[2]))
                    result.AddError("Story scene outcome did not project compact branch/end/route choices.");
            }

            stage.IntercomStages.RemoveAt(0);
            workspace = builder.Build(new ScenarioAuthoringWindowContentContext(null, null, null, definition));
            string expectedStage = ScenarioStoryFocusedEditorActions.StageEntityId(definition, 0);
            if (!string.Equals(state.GetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId), expectedStage, StringComparison.Ordinal))
                result.AddError("Deleted Story scene did not reconcile selection to its parent stage.");
            definition.ScenarioFlow.Stages.RemoveAt(0);
            workspace = builder.Build(new ScenarioAuthoringWindowContentContext(null, null, null, definition));
            if (!string.IsNullOrEmpty(state.GetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId)))
                result.AddError("Deleted Story stage did not reconcile selection to the overview.");

            ScenarioAuthoringState authoredState = new ScenarioAuthoringState { IsActive = true, FocusedEditorKind = ScenarioStoryFocusedEditorActions.FocusedEditorKind };
            authoredState.WindowStates.Add(new ScenarioAuthoringWindowState { Id = "quests", Visible = true, ZIndex = 1 });
            ScenarioAuthoringSurfaceState surface = new ScenarioAuthoringSurfaceResolver().Resolve(authoredState, null, false);
            if (surface != null && surface.Kind == ScenarioAuthoringSurfaceKind.Modal)
                result.AddError("Legacy Story focused-editor state still resolves as a modal surface.");
        }

        private static bool ContainsCompactChoice(ScenarioAuthoringInspectorSection section)
        {
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
                if (section.Items[i] != null && section.Items[i].Kind == ScenarioAuthoringInspectorItemKind.Choice && section.Items[i].Choice != null) return true;
            return false;
        }

        private static void VerifyWorkspaceCommandState(
            ScenarioValidationResult result,
            string workspaceId,
            string subtabId,
            params ScenarioAuthoringInspectorAction[] actions)
        {
            RendererInteractionCommandHandler handler = new RendererInteractionCommandHandler(null, null, null);
            ScenarioAuthoringState authoredState = new ScenarioAuthoringState { StatusMessage = "unchanged" };
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                bool handled;
                string message;
                bool changed = handler.TryHandle(authoredState, actions[i].Id, out handled, out message);
                if (!handled || !changed || !string.IsNullOrEmpty(message))
                    result.AddError("Workspace renderer command did not complete as one presentation-only interaction: " + actions[i].Id);
            }

            ScenarioAuthoringRendererInteractionState state = ScenarioAuthoringRendererInteractionState.Instance;
            if (!string.Equals(state.GetWorkspaceSubtab(workspaceId, null), subtabId, StringComparison.Ordinal)
                || !string.Equals(state.GetWorkspaceSelection(workspaceId, subtabId), "overview", StringComparison.Ordinal)
                || !state.GetWorkspaceExpanded(workspaceId, subtabId, "stages", false)
                || !state.GetWorkspaceExpanded(workspaceId, subtabId, "stage.1", false)
                || !string.Equals(state.GetWorkspaceSearch(workspaceId, subtabId), "find me", StringComparison.Ordinal)
                || state.GetWorkspaceNarrowPane(workspaceId, subtabId, true)
                || !string.Equals(authoredState.StatusMessage, "unchanged", StringComparison.Ordinal))
            {
                result.AddError("Workspace renderer commands did not isolate subtab, selection, expansion, search, and narrow-pane state from authored state.");
            }
        }

        private static void VerifyDuplicateContractIds(ScenarioValidationResult result)
        {
            try
            {
                ScenarioAuthoringRendererActionManifest.VerifyUniqueActionIdsForContract(new[]
                {
                    FixtureAction("fixture.duplicate"),
                    FixtureAction("fixture.duplicate")
                });
                result.AddError("Duplicate semantic action IDs were not rejected before contract serialization.");
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static HashSet<string> CollectContractIds(ScenarioAuthoringShellWindowViewModel contractWindow)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            ScenarioAuthoringInspectorItem[] items = contractWindow != null
                && contractWindow.Sections != null
                && contractWindow.Sections.Length > 0
                ? contractWindow.Sections[0].Items
                : null;
            for (int i = 0; items != null && i < items.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = items[i] != null ? items[i].Action : null;
                if (action != null && !string.IsNullOrEmpty(action.Id)) ids.Add(action.Id);
            }
            return ids;
        }

        private static ScenarioAuthoringStatusChipViewModel Chip(string actionId)
        {
            return new ScenarioAuthoringStatusChipViewModel
            {
                Id = actionId,
                Text = actionId,
                Action = FixtureAction(actionId)
            };
        }

        private static ScenarioAuthoringInspectorAction FixtureAction(string id)
        {
            return new ScenarioAuthoringInspectorAction { Id = id, Label = id, Enabled = true };
        }

        private static void RequireFamily(HashSet<string> ids, string prefix, string label, ScenarioValidationResult result)
        {
            foreach (string id in ids)
                if (id.StartsWith(prefix, StringComparison.Ordinal)) return;
            result.AddError("Authoring shell contract did not expose the " + label + " action family.");
        }

        private static void Require(HashSet<string> ids, string id, string label, ScenarioValidationResult result)
        {
            if (!ids.Contains(id)) result.AddError("Authoring shell contract did not expose " + label + " action '" + id + "'.");
        }
    }
}
