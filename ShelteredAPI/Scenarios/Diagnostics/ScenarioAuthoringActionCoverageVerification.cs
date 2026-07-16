using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Commands;
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

            ScenarioAuthoringShellViewModel shell = new ScenarioAuthoringShellViewModel
            {
                RendererActions = new[] { searchAction },
                Windows = new[]
                {
                    new ScenarioAuthoringShellWindowViewModel
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
                    }
                }
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
                if (composer.Build(contentKind, context) != null)
                    result.AddError("Unmigrated window content kind '" + contentKind + "' unexpectedly produced a WorkspaceBody.");
            }
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
