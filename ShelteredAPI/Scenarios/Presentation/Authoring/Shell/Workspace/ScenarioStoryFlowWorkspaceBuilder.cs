using System;
using System.Collections.Generic;
using System.Globalization;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Validation;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal sealed class ScenarioStoryFlowWorkspaceBuilder
    {
        public ScenarioAuthoringWorkspaceViewModel Build(
            ScenarioAuthoringWindowContentContext context,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioDefinition definition = context != null ? context.Definition : null;
            ScenarioStoryFlowIssue[] issues = new ScenarioStoryFlowValidationAnalyzer().Analyze(definition);
            ScenarioAuthoringRendererInteractionState rendererState = ScenarioAuthoringRendererInteractionState.Instance;
            string selected = ReconcileSelection(definition, rendererState);

            ScenarioAuthoringWorkspaceViewModel workspace = factory.CreateWorkspace(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument,
                ScenarioStoryFocusedEditorActions.FlowSubtabId);
            workspace.Navigator = BuildNavigator(definition, issues, selected, rendererState, factory);
            workspace.Document = BuildDocument(definition, issues, selected, factory);
            return workspace;
        }

        private static string ReconcileSelection(ScenarioDefinition definition, ScenarioAuthoringRendererInteractionState state)
        {
            string selected = state.GetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId);
            if (string.IsNullOrEmpty(selected))
                return null;
            int stageIndex;
            int sceneIndex;
            if (ScenarioStoryFocusedEditorActions.TryResolveSceneEntity(definition, selected, out stageIndex, out sceneIndex)
                || ScenarioStoryFocusedEditorActions.TryResolveStageEntity(definition, selected, out stageIndex))
                return selected;

            string parent;
            if (ScenarioStoryFocusedEditorActions.TryGetSceneParentEntityId(selected, out parent)
                && ScenarioStoryFocusedEditorActions.TryResolveStageEntity(definition, parent, out stageIndex))
            {
                state.SetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, parent);
                return parent;
            }

            state.SetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, null);
            return null;
        }

        private static ScenarioAuthoringNavigatorViewModel BuildNavigator(
            ScenarioDefinition definition,
            ScenarioStoryFlowIssue[] issues,
            string selected,
            ScenarioAuthoringRendererInteractionState state,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioAuthoringNavigatorViewModel navigator = factory.CreateNavigator("story.flow.navigator");
            navigator.SearchControlId = "story.flow.search";
            navigator.SearchText = state.GetWorkspaceSearch(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId);
            navigator.SearchPlaceholder = "Search stages and scenes";
            navigator.SelectedEntityId = selected;
            navigator.EmptyMessage = "No Story stages yet.";
            List<ScenarioAuthoringNavigatorRowViewModel> stages = new List<ScenarioAuthoringNavigatorRowViewModel>();
            List<ScenarioAuthoringNavigatorRowViewModel> endings = new List<ScenarioAuthoringNavigatorRowViewModel>();
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            string search = (navigator.SearchText ?? string.Empty).Trim();
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                string stageEntity = ScenarioStoryFocusedEditorActions.StageEntityId(definition, i);
                string stageTitle = DisplayStageTitle(stage, i);
                List<ScenarioAuthoringNavigatorRowViewModel> children = new List<ScenarioAuthoringNavigatorRowViewModel>();
                bool stageMatches = Matches(stageTitle, search);
                for (int j = 0; stage != null && stage.IntercomStages != null && j < stage.IntercomStages.Count; j++)
                {
                    ScenarioIntercomStageDefinition scene = stage.IntercomStages[j];
                    string sceneTitle = DisplaySceneTitle(scene, j);
                    if (!stageMatches && !Matches(sceneTitle, search))
                        continue;
                    ScenarioAuthoringNavigatorRowViewModel sceneRow = BuildSceneRow(definition, issues, selected, factory, i, j, scene, sceneTitle);
                    children.Add(sceneRow);
                    if (EndsStory(scene)) endings.Add(CloneEndingRow(sceneRow));
                }
                if (!stageMatches && children.Count == 0)
                    continue;
                int warningCount = CountIssues(issues, i, -1, false);
                stages.Add(new ScenarioAuthoringNavigatorRowViewModel
                {
                    EntityId = stageEntity,
                    Title = stageTitle,
                    Subtitle = DescribeStage(stage),
                    IconText = "ST",
                    Selected = string.Equals(selected, stageEntity, StringComparison.Ordinal),
                    Expanded = state.GetWorkspaceExpanded(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, stageEntity, true),
                    StatusChips = BuildStageChips(definition, issues, factory, i, stage, warningCount),
                    SelectAction = factory.CreateEntityAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, stageEntity, "Select " + stageTitle),
                    ToggleAction = factory.CreateRowToggleAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, stageEntity, "Toggle " + stageTitle),
                    Children = children.ToArray()
                });
            }

            ScenarioAuthoringNavigatorGroupViewModel stageGroup = new ScenarioAuthoringNavigatorGroupViewModel
            {
                Id = "stages",
                Label = "Stages",
                IconText = "ST",
                Expanded = state.GetWorkspaceExpanded(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, "stages", true),
                StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                ToggleAction = factory.CreateGroupToggleAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, "stages", "Toggle Stages"),
                CreateAction = ScenarioInspectorItemFactory.Action(ScenarioStoryFocusedEditorActions.ActionStageOpenNew, "Add Stage", "Create and select a new Story stage. Undo reverses it.", true, stages.Count == 0, "S+"),
                Rows = stages.ToArray()
            };
            ScenarioAuthoringNavigatorGroupViewModel endingGroup = new ScenarioAuthoringNavigatorGroupViewModel
            {
                Id = "endings",
                Label = "Endings",
                IconText = "END",
                Expanded = state.GetWorkspaceExpanded(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, "endings", true),
                StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                ToggleAction = factory.CreateGroupToggleAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, "endings", "Toggle Endings"),
                Rows = endings.ToArray()
            };
            navigator.Groups = new[] { stageGroup, endingGroup };
            return navigator;
        }

        private static ScenarioAuthoringNavigatorRowViewModel BuildSceneRow(
            ScenarioDefinition definition,
            ScenarioStoryFlowIssue[] issues,
            string selected,
            ScenarioAuthoringWorkspaceViewModelFactory factory,
            int stageIndex,
            int sceneIndex,
            ScenarioIntercomStageDefinition scene,
            string title)
        {
            string entity = ScenarioStoryFocusedEditorActions.SceneEntityId(definition, stageIndex, sceneIndex);
            int warnings = CountIssues(issues, stageIndex, sceneIndex, true);
            List<ScenarioAuthoringStatusChipViewModel> chips = new List<ScenarioAuthoringStatusChipViewModel>();
            if (warnings > 0) chips.Add(Chip("scene.warning." + stageIndex + "." + sceneIndex, warnings.ToString(CultureInfo.InvariantCulture) + " warnings", ScenarioAuthoringStatusTone.Warning, factory.CreateWarningAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, entity, "Open " + title + " warnings")));
            if (EndsStory(scene)) chips.Add(Chip("scene.ends." + stageIndex + "." + sceneIndex, "Ends story", ScenarioAuthoringStatusTone.Ready, null));
            else if (!HasSceneRoute(scene)) chips.Add(Chip("scene.route." + stageIndex + "." + sceneIndex, "No route", ScenarioAuthoringStatusTone.Warning, null));
            else if (warnings == 0) chips.Add(Chip("scene.ready." + stageIndex + "." + sceneIndex, "Ready", ScenarioAuthoringStatusTone.Ready, null));
            return new ScenarioAuthoringNavigatorRowViewModel
            {
                EntityId = entity,
                Title = title,
                Subtitle = DescribeScene(scene),
                IconText = "SC",
                Selected = string.Equals(selected, entity, StringComparison.Ordinal),
                StatusChips = chips.ToArray(),
                SelectAction = factory.CreateEntityAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, entity, "Select " + title),
                Children = new ScenarioAuthoringNavigatorRowViewModel[0]
            };
        }

        private static ScenarioAuthoringNavigatorRowViewModel CloneEndingRow(ScenarioAuthoringNavigatorRowViewModel source)
        {
            return new ScenarioAuthoringNavigatorRowViewModel
            {
                EntityId = source.EntityId,
                Title = source.Title,
                Subtitle = "Completes the Story",
                IconText = "END",
                Selected = source.Selected,
                StatusChips = new[] { Chip("ending." + source.EntityId, "Ends story", ScenarioAuthoringStatusTone.Ready, null) },
                SelectAction = source.SelectAction,
                Children = new ScenarioAuthoringNavigatorRowViewModel[0]
            };
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildDocument(
            ScenarioDefinition definition,
            ScenarioStoryFlowIssue[] issues,
            string selected,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            int stageIndex;
            int sceneIndex;
            if (ScenarioStoryFocusedEditorActions.TryResolveSceneEntity(definition, selected, out stageIndex, out sceneIndex))
                return BuildSceneDocument(definition, issues, factory, stageIndex, sceneIndex);
            if (ScenarioStoryFocusedEditorActions.TryResolveStageEntity(definition, selected, out stageIndex))
                return BuildStageDocument(definition, issues, factory, stageIndex);
            return BuildOverviewDocument(definition, issues, factory);
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildOverviewDocument(
            ScenarioDefinition definition,
            ScenarioStoryFlowIssue[] issues,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("story.flow.overview", "Story Flow");
            document.Subtitle = "See the complete Story, then select a stage or scene to edit it.";
            document.BackAction = factory.CreateBackAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, "Back to Navigator");
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "story_map",
                Title = "STORY MAP",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Default,
                StoryMap = ScenarioStoryGraphBuilder.Build(definition, issues),
                Items = new[] { ScenarioInspectorItemFactory.Text("Select a stage on the map to open the same document shown by the Navigator and timeline.") }
            });
            int stageCount = 0;
            int sceneCount = 0;
            int endingCount = 0;
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                stageCount++;
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                for (int j = 0; stage != null && stage.IntercomStages != null && j < stage.IntercomStages.Count; j++)
                {
                    sceneCount++;
                    if (EndsStory(stage.IntercomStages[j])) endingCount++;
                }
            }
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "story_flow_facts",
                Title = "STORY FACTS",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.Property("Stages", stageCount.ToString(CultureInfo.InvariantCulture)),
                    ScenarioInspectorItemFactory.Property("Scenes", sceneCount.ToString(CultureInfo.InvariantCulture)),
                    ScenarioInspectorItemFactory.Property("Endings", endingCount.ToString(CultureInfo.InvariantCulture)),
                    ScenarioInspectorItemFactory.Property("Warnings", (issues != null ? issues.Length : 0).ToString(CultureInfo.InvariantCulture))
                }
            });
            List<ScenarioAuthoringInspectorItem> warningItems = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                ScenarioStoryFlowIssue issue = issues[i];
                if (issue == null) continue;
                string entity = IssueEntityId(definition, issue);
                string label = IssueLabel(issue);
                if (!string.IsNullOrEmpty(entity))
                    warningItems.Add(ScenarioInspectorItemFactory.ActionItem(factory.CreateWarningAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, entity, label)));
                else
                    warningItems.Add(ScenarioInspectorItemFactory.Text(label));
            }
            if (warningItems.Count == 0) warningItems.Add(ScenarioInspectorItemFactory.Text("Story routing is ready."));
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "story_flow_warnings",
                Title = "WARNINGS",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = warningItems.ToArray()
            });
            document.Sections = sections.ToArray();
            return document;
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildStageDocument(ScenarioDefinition definition, ScenarioStoryFlowIssue[] issues, ScenarioAuthoringWorkspaceViewModelFactory factory, int stageIndex)
        {
            ScenarioFlowStageDefinition stage = definition.ScenarioFlow.Stages[stageIndex];
            string entity = ScenarioStoryFocusedEditorActions.StageEntityId(definition, stageIndex);
            string title = DisplayStageTitle(stage, stageIndex);
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("story.flow.stage." + stageIndex.ToString(CultureInfo.InvariantCulture), title);
            document.Subtitle = DescribeStage(stage);
            document.BackAction = factory.CreateBackAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, "Back to Navigator");
            document.Breadcrumbs = new[]
            {
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Story", Action = factory.CreateBreadcrumbAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, string.Empty, "Story") },
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Flow" },
                new ScenarioAuthoringBreadcrumbViewModel { Label = title }
            };
            int warnings = CountIssues(issues, stageIndex, -1, false);
            document.StatusChips = warnings > 0
                ? new[] { Chip("document.stage.warning." + stageIndex, warnings.ToString(CultureInfo.InvariantCulture) + " warnings", ScenarioAuthoringStatusTone.Warning, factory.CreateWarningAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, entity, "Stage warnings")) }
                : new[] { Chip("document.stage.ready." + stageIndex, "Ready", ScenarioAuthoringStatusTone.Ready, null) };
            document.Sections = ScenarioStoryFocusedEditorDocumentBuilder.BuildStageWorkspaceSections(definition, stageIndex, issues);
            return document;
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildSceneDocument(ScenarioDefinition definition, ScenarioStoryFlowIssue[] issues, ScenarioAuthoringWorkspaceViewModelFactory factory, int stageIndex, int sceneIndex)
        {
            ScenarioFlowStageDefinition stage = definition.ScenarioFlow.Stages[stageIndex];
            ScenarioIntercomStageDefinition scene = stage.IntercomStages[sceneIndex];
            string stageEntity = ScenarioStoryFocusedEditorActions.StageEntityId(definition, stageIndex);
            string sceneEntity = ScenarioStoryFocusedEditorActions.SceneEntityId(definition, stageIndex, sceneIndex);
            string stageTitle = DisplayStageTitle(stage, stageIndex);
            string title = DisplaySceneTitle(scene, sceneIndex);
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("story.flow.scene." + stageIndex.ToString(CultureInfo.InvariantCulture) + "." + sceneIndex.ToString(CultureInfo.InvariantCulture), title);
            document.Subtitle = DescribeScene(scene);
            document.BackAction = factory.CreateBackAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, "Back to Navigator");
            document.Breadcrumbs = new[]
            {
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Story", Action = factory.CreateBreadcrumbAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, string.Empty, "Story") },
                new ScenarioAuthoringBreadcrumbViewModel { Label = stageTitle, Action = factory.CreateBreadcrumbAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, stageEntity, stageTitle) },
                new ScenarioAuthoringBreadcrumbViewModel { Label = title }
            };
            int warnings = CountIssues(issues, stageIndex, sceneIndex, true);
            document.StatusChips = warnings > 0
                ? new[] { Chip("document.scene.warning." + stageIndex + "." + sceneIndex, warnings.ToString(CultureInfo.InvariantCulture) + " warnings", ScenarioAuthoringStatusTone.Warning, factory.CreateWarningAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, sceneEntity, "Scene warnings")) }
                : new[] { Chip("document.scene.ready." + stageIndex + "." + sceneIndex, EndsStory(scene) ? "Ends story" : "Ready", ScenarioAuthoringStatusTone.Ready, null) };
            document.Sections = ScenarioStoryFocusedEditorDocumentBuilder.BuildSceneWorkspaceSections(definition, stageIndex, sceneIndex, issues);
            return document;
        }

        private static ScenarioAuthoringStatusChipViewModel[] BuildStageChips(ScenarioDefinition definition, ScenarioStoryFlowIssue[] issues, ScenarioAuthoringWorkspaceViewModelFactory factory, int stageIndex, ScenarioFlowStageDefinition stage, int warnings)
        {
            List<ScenarioAuthoringStatusChipViewModel> chips = new List<ScenarioAuthoringStatusChipViewModel>();
            int scenes = stage != null && stage.IntercomStages != null ? stage.IntercomStages.Count : 0;
            chips.Add(Chip("stage.scenes." + stageIndex, scenes.ToString(CultureInfo.InvariantCulture) + (scenes == 1 ? " scene" : " scenes"), ScenarioAuthoringStatusTone.Informational, null));
            string entity = ScenarioStoryFocusedEditorActions.StageEntityId(definition, stageIndex);
            if (warnings > 0) chips.Add(Chip("stage.warnings." + stageIndex, warnings.ToString(CultureInfo.InvariantCulture) + " warnings", ScenarioAuthoringStatusTone.Warning, factory.CreateWarningAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, entity, "Open stage warnings")));
            if (StageEndsStory(stage)) chips.Add(Chip("stage.ends." + stageIndex, "Ends story", ScenarioAuthoringStatusTone.Ready, null));
            else if (!HasStageRoute(stage)) chips.Add(Chip("stage.route." + stageIndex, "No route", ScenarioAuthoringStatusTone.Warning, null));
            else if (warnings == 0) chips.Add(Chip("stage.ready." + stageIndex, "Ready", ScenarioAuthoringStatusTone.Ready, null));
            return chips.ToArray();
        }

        private static ScenarioAuthoringStatusChipViewModel Chip(string id, string text, ScenarioAuthoringStatusTone tone, ScenarioAuthoringInspectorAction action)
        {
            return new ScenarioAuthoringStatusChipViewModel { Id = id, Text = text, Tone = tone, Action = action };
        }

        private static string IssueEntityId(ScenarioDefinition definition, ScenarioStoryFlowIssue issue)
        {
            if (issue == null || issue.StageIndex < 0) return null;
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            if (flow == null || flow.Stages == null || issue.StageIndex >= flow.Stages.Count) return null;
            ScenarioFlowStageDefinition stage = flow.Stages[issue.StageIndex];
            return issue.IntercomIndex >= 0 && stage != null && stage.IntercomStages != null && issue.IntercomIndex < stage.IntercomStages.Count
                ? ScenarioStoryFocusedEditorActions.SceneEntityId(definition, issue.StageIndex, issue.IntercomIndex)
                : ScenarioStoryFocusedEditorActions.StageEntityId(definition, issue.StageIndex);
        }

        private static string IssueLabel(ScenarioStoryFlowIssue issue)
        {
            string owner = issue.StageIndex >= 0 ? "Stage " + (issue.StageIndex + 1).ToString(CultureInfo.InvariantCulture) : "Story";
            if (issue.IntercomIndex >= 0) owner += ", Scene " + (issue.IntercomIndex + 1).ToString(CultureInfo.InvariantCulture);
            string code = issue.Code ?? string.Empty;
            string problem = code.IndexOf("unreachable", StringComparison.OrdinalIgnoreCase) >= 0 ? "Cannot be reached"
                : code.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0 ? "Missing route or reference"
                : code.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0 ? "Duplicate stage identity"
                : code.IndexOf("required", StringComparison.OrdinalIgnoreCase) >= 0 ? "Required value is missing"
                : code.IndexOf("delay", StringComparison.OrdinalIgnoreCase) >= 0 ? "Timing needs attention"
                : code.IndexOf("no_intercom", StringComparison.OrdinalIgnoreCase) >= 0 ? "Add the first scene"
                : code.IndexOf("no_starting", StringComparison.OrdinalIgnoreCase) >= 0 ? "Add the first stage"
                : code.IndexOf("route", StringComparison.OrdinalIgnoreCase) >= 0 ? "Routing needs attention"
                : "Needs attention";
            return owner + " — " + problem;
        }

        private static int CountIssues(ScenarioStoryFlowIssue[] issues, int stageIndex, int sceneIndex, bool exactScene)
        {
            int count = 0;
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                ScenarioStoryFlowIssue issue = issues[i];
                if (issue == null || issue.StageIndex != stageIndex) continue;
                if (!exactScene || issue.IntercomIndex == sceneIndex) count++;
            }
            return count;
        }

        private static string DisplayStageTitle(ScenarioFlowStageDefinition stage, int index)
        {
            string title = null;
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                if (stage.IntercomStages[i] != null && !string.IsNullOrEmpty(stage.IntercomStages[i].StageDescriptionKey)) { title = stage.IntercomStages[i].StageDescriptionKey; break; }
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(title, title, stage != null ? stage.Id : null, "Stage " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        private static string DisplaySceneTitle(ScenarioIntercomStageDefinition scene, int index)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(scene != null ? scene.StageDescriptionKey : null, scene != null ? scene.StageDescriptionKey : null, scene != null ? scene.Id : null, "Scene " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        private static string DescribeStage(ScenarioFlowStageDefinition stage)
        {
            int count = stage != null && stage.IntercomStages != null ? stage.IntercomStages.Count : 0;
            return count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " scene" : " scenes");
        }

        private static string DescribeScene(ScenarioIntercomStageDefinition scene)
        {
            int lines = scene != null && scene.Dialogue != null ? scene.Dialogue.Count : 0;
            int choices = scene != null && scene.Options != null ? scene.Options.Count : 0;
            return lines.ToString(CultureInfo.InvariantCulture) + " dialogue lines · " + choices.ToString(CultureInfo.InvariantCulture) + " choices";
        }

        private static bool Matches(string value, string search)
        {
            return string.IsNullOrEmpty(search) || (!string.IsNullOrEmpty(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool HasSceneRoute(ScenarioIntercomStageDefinition scene)
        {
            return scene != null && (!string.IsNullOrEmpty(scene.NextId) || !string.IsNullOrEmpty(scene.AlternateNextId) || (scene.StageChange != null && !string.IsNullOrEmpty(scene.StageChange.Id)) || EndsStory(scene));
        }

        private static bool HasStageRoute(ScenarioFlowStageDefinition stage)
        {
            if (stage != null && !string.IsNullOrEmpty(stage.UnansweredNextStage)) return true;
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                if (HasSceneRoute(stage.IntercomStages[i])) return true;
            return false;
        }

        private static bool StageEndsStory(ScenarioFlowStageDefinition stage)
        {
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                if (EndsStory(stage.IntercomStages[i])) return true;
            return false;
        }

        private static bool EndsStory(ScenarioIntercomStageDefinition scene)
        {
            ScenarioEncounterEndOptionsDefinition end = scene != null ? scene.EndOptions : null;
            return end != null && (end.CompleteQuest || end.CompleteParentScenario || string.Equals(end.Type, "CompleteQuest", StringComparison.OrdinalIgnoreCase));
        }
    }
}
