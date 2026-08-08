using ShelteredAPI.Scenarios.Diagnostics;
using System;
using System.Collections.Generic;

using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;

namespace ShelteredScenarioEditor.Diagnostics
{
    /// <summary>
    /// Executable regression fixture for the complete workspace command family. It
    /// deliberately uses ScenarioCommandDispatcher so it covers the same handled/result
    /// boundary consumed by both IMGUI and /authoring/action.
    /// </summary>
    internal static class ScenarioWorkspaceRoutingVerification
    {
        public static string[] Run()
        {
            ScenarioValidationResult result = new ScenarioValidationResult();
            VerifyRouting(result);
            List<string> errors = new List<string>();
            ScenarioValidationIssue[] issues = result.Issues;
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                if (issues[i] != null && issues[i].Severity == ScenarioIssueSeverity.Error)
                    errors.Add(issues[i].Message);
            }
            return errors.ToArray();
        }

        public static void Verify(ScenarioValidationResult result)
        {
            VerifyRouting(result);
            VerifyStoryBreadcrumbs(result);
            VerifyHistoryRestoresCreativeState(result);
        }

        private static void VerifyRouting(ScenarioValidationResult result)
        {
            string workspaceId = "routing.fixture." + Guid.NewGuid().ToString("N");
            const string subtabId = "fixture.tab";
            ScenarioAuthoringWorkspaceViewModelFactory factory = new ScenarioAuthoringWorkspaceViewModelFactory();
            ScenarioAuthoringRendererInteractionState rendererState = new ScenarioAuthoringRendererInteractionState();
            ScenarioCommandDispatcher dispatcher = new ScenarioCommandDispatcher();
            dispatcher.Register(new TypedRendererInteractionCommandHandler(rendererState));
            ScenarioAuthoringState authoredState = new ScenarioAuthoringState { StatusMessage = "unchanged" };

            DispatchAndRequire(dispatcher, authoredState, factory.CreateSubtabAction(workspaceId, subtabId, "Tab").Command,
                delegate { return string.Equals(rendererState.GetWorkspaceSubtab(workspaceId, null), subtabId, StringComparison.Ordinal); }, "subtab.select", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateEntityAction(workspaceId, subtabId, "entity.one", "Entity").Command,
                delegate { return string.Equals(rendererState.GetWorkspaceSelection(workspaceId, subtabId), "entity.one", StringComparison.Ordinal) && rendererState.GetWorkspaceNarrowPane(workspaceId, subtabId, false); }, "entity.select", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateWarningAction(workspaceId, subtabId, "entity.warning", "Warning").Command,
                delegate { return string.Equals(rendererState.GetWorkspaceSelection(workspaceId, subtabId), "entity.warning", StringComparison.Ordinal); }, "warning.open", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateGroupToggleAction(workspaceId, subtabId, "group.one", "Group").Command,
                delegate { return rendererState.GetWorkspaceExpanded(workspaceId, subtabId, "group.one", false); }, "group.toggle", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateRowToggleAction(workspaceId, subtabId, "row.one", "Row").Command,
                delegate { return rendererState.GetWorkspaceExpanded(workspaceId, subtabId, "row.one", false); }, "row.toggle", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateSearchAction(workspaceId, subtabId, "find me", "Search").Command,
                delegate { return string.Equals(rendererState.GetWorkspaceSearch(workspaceId, subtabId), "find me", StringComparison.Ordinal); }, "search.set", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateBreadcrumbAction(workspaceId, subtabId, "entity.root", "Root").Command,
                delegate { return string.Equals(rendererState.GetWorkspaceSelection(workspaceId, subtabId), "entity.root", StringComparison.Ordinal); }, "breadcrumb.select", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateBreadcrumbAction(workspaceId, subtabId, string.Empty, "Workspace").Command,
                delegate { return string.IsNullOrEmpty(rendererState.GetWorkspaceSelection(workspaceId, subtabId)); }, "breadcrumb.root", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateBackAction(workspaceId, subtabId, "Back").Command,
                delegate { return !rendererState.GetWorkspaceNarrowPane(workspaceId, subtabId, true); }, "back", result);

        }

        private static void DispatchAndRequire(
            ScenarioCommandDispatcher dispatcher,
            ScenarioAuthoringState state,
            ScenarioAuthoringCommand command,
            Func<bool> stateChanged,
            string family,
            ScenarioValidationResult result)
        {
            ScenarioCommandDispatchResult dispatch = dispatcher.DispatchDetailed(state, command);
            if (!dispatch.Handled || !dispatch.Changed || stateChanged == null || !stateChanged())
                result.AddError("Workspace " + family + " did not dispatch as result:true with its expected renderer-state change: " + (command != null ? command.AutomationId : "<null>"));
        }

        private static void VerifyStoryBreadcrumbs(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            ScenarioFlowStageDefinition stage = new ScenarioFlowStageDefinition { Id = "fixture_stage" };
            ScenarioIntercomStageDefinition scene = new ScenarioIntercomStageDefinition { Id = "fixture_scene" };
            scene.Dialogue.Add(new ScenarioDialogueLineDefinition { TextKey = "Fixture dialogue" });
            scene.Options.Add(new ScenarioDialogueOptionDefinition { TextKey = "Fixture choice" });
            stage.IntercomStages.Add(scene);
            definition.ScenarioFlow.Stages.Add(stage);

            ScenarioAuthoringRendererInteractionState interaction = new ScenarioAuthoringRendererInteractionState();
            ScenarioStoryFlowWorkspaceBuilder builder = new ScenarioStoryFlowWorkspaceBuilder();
            ScenarioAuthoringWorkspaceViewModelFactory factory = new ScenarioAuthoringWorkspaceViewModelFactory();
            ScenarioAuthoringWindowContentContext context = new ScenarioAuthoringWindowContentContext(null, null, null, definition, interaction);

            interaction.SetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, null);
            ScenarioAuthoringWorkspaceViewModel root = builder.Build(context, factory);
            if (root == null || root.Document == null || root.Document.Breadcrumbs == null
                || root.Document.Breadcrumbs.Length != 1 || !string.Equals(root.Document.Breadcrumbs[0].Label, "Story", StringComparison.Ordinal))
            {
                result.AddError("Story root breadcrumb must contain only the workspace name.");
            }

            interaction.SetWorkspaceSelection(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioStoryFocusedEditorActions.FlowSubtabId,
                ScenarioStoryFocusedEditorActions.SceneEntityId(definition, 0, 0));
            ScenarioAuthoringWorkspaceViewModel selected = builder.Build(context, factory);
            if (selected == null || selected.Document == null || selected.Document.Breadcrumbs == null
                || selected.Document.Breadcrumbs.Length != 3
                || !string.Equals(selected.Document.Breadcrumbs[0].Label, "Story", StringComparison.Ordinal)
                || !string.Equals(selected.Document.Breadcrumbs[1].Label, "Stage 1", StringComparison.Ordinal)
                || !string.Equals(selected.Document.Breadcrumbs[2].Label, "Scene 1", StringComparison.Ordinal))
            {
                result.AddError("Story scene breadcrumb must follow Workspace / Entity / Child.");
            }
            if (selected == null || selected.Document == null
                || !string.Equals(selected.Document.Subtitle, "1 dialogue line · 1 choice", StringComparison.Ordinal))
            {
                result.AddError("Story scene singular count formatting regressed.");
            }

            interaction.SetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.FlowSubtabId, null);
        }

        private static void VerifyHistoryRestoresCreativeState(ScenarioValidationResult result)
        {
            ScenarioAuthoringHistoryService history = new ScenarioAuthoringHistoryService();
            ScenarioDefinition definition = new ScenarioDefinition { Id = "routing.history.fixture" };
            history.BindSession(definition.Id);
            history.RecordAuthoringChange(definition, "Create story content", ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            definition.ScenarioFlow.Stages.Add(new ScenarioFlowStageDefinition { Id = "stage_1" });
            definition.ScenarioCharacters.Add(new ScenarioNpcDefinition { CharacterId = "character_1" });
            definition.Quests.Quests.Add(new QuestDefinition { Id = "quest_1" });
            definition.Conversations.Conversations.Add(new ScenarioConversationDefinition { Id = "conversation_1" });

            string description;
            if (!history.Undo(definition, out description)
                || definition.ScenarioFlow.Stages.Count != 0
                || definition.ScenarioCharacters.Count != 0
                || definition.Quests.Quests.Count != 0
                || definition.Conversations.Conversations.Count != 0)
            {
                result.AddError("Undo history did not restore stage, character, quest, and conversation creation state.");
            }
        }
    }
}
