using System;
using System.Collections.Generic;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;

namespace ShelteredAPI.Scenarios.Diagnostics
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
            VerifyHistoryRestoresCreativeState(result);
        }

        private static void VerifyRouting(ScenarioValidationResult result)
        {
            string workspaceId = "routing.fixture." + Guid.NewGuid().ToString("N");
            const string subtabId = "fixture.tab";
            ScenarioAuthoringWorkspaceViewModelFactory factory = new ScenarioAuthoringWorkspaceViewModelFactory();
            ScenarioAuthoringRendererInteractionState rendererState = ScenarioAuthoringRendererInteractionState.Instance;
            ScenarioCommandDispatcher dispatcher = new ScenarioCommandDispatcher(new IScenarioCommandHandler[]
            {
                new RendererInteractionCommandHandler(null, null, null)
            });
            ScenarioAuthoringState authoredState = new ScenarioAuthoringState { StatusMessage = "unchanged" };

            DispatchAndRequire(dispatcher, authoredState, factory.CreateSubtabAction(workspaceId, subtabId, "Tab").Id,
                delegate { return string.Equals(rendererState.GetWorkspaceSubtab(workspaceId, null), subtabId, StringComparison.Ordinal); }, "subtab.select", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateEntityAction(workspaceId, subtabId, "entity.one", "Entity").Id,
                delegate { return string.Equals(rendererState.GetWorkspaceSelection(workspaceId, subtabId), "entity.one", StringComparison.Ordinal) && rendererState.GetWorkspaceNarrowPane(workspaceId, subtabId, false); }, "entity.select", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateWarningAction(workspaceId, subtabId, "entity.warning", "Warning").Id,
                delegate { return string.Equals(rendererState.GetWorkspaceSelection(workspaceId, subtabId), "entity.warning", StringComparison.Ordinal); }, "warning.open", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateGroupToggleAction(workspaceId, subtabId, "group.one", "Group").Id,
                delegate { return rendererState.GetWorkspaceExpanded(workspaceId, subtabId, "group.one", false); }, "group.toggle", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateRowToggleAction(workspaceId, subtabId, "row.one", "Row").Id,
                delegate { return rendererState.GetWorkspaceExpanded(workspaceId, subtabId, "row.one", false); }, "row.toggle", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateSearchAction(workspaceId, subtabId, "find me", "Search").Id,
                delegate { return string.Equals(rendererState.GetWorkspaceSearch(workspaceId, subtabId), "find me", StringComparison.Ordinal); }, "search.set", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateBreadcrumbAction(workspaceId, subtabId, "entity.root", "Root").Id,
                delegate { return string.Equals(rendererState.GetWorkspaceSelection(workspaceId, subtabId), "entity.root", StringComparison.Ordinal); }, "breadcrumb.select", result);
            DispatchAndRequire(dispatcher, authoredState, factory.CreateBackAction(workspaceId, subtabId, "Back").Id,
                delegate { return !rendererState.GetWorkspaceNarrowPane(workspaceId, subtabId, true); }, "back", result);

            string compatibilityAction = factory.CreateEntityAction(workspaceId, subtabId, "entity.compat", "Compatibility").Id;
            compatibilityAction = compatibilityAction.Substring("shell.renderer.".Length);
            DispatchAndRequire(dispatcher, authoredState, compatibilityAction,
                delegate { return string.Equals(rendererState.GetWorkspaceSelection(workspaceId, subtabId), "entity.compat", StringComparison.Ordinal); }, "compatibility entity.select", result);

        }

        private static void DispatchAndRequire(
            ScenarioCommandDispatcher dispatcher,
            ScenarioAuthoringState state,
            string actionId,
            Func<bool> stateChanged,
            string family,
            ScenarioValidationResult result)
        {
            ScenarioCommandDispatchResult dispatch = dispatcher.DispatchDetailed(state, actionId);
            if (!dispatch.Handled || !dispatch.Changed || !dispatch.Result || stateChanged == null || !stateChanged())
                result.AddError("Workspace " + family + " did not dispatch as result:true with its expected renderer-state change: " + actionId);
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
