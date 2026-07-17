using System;
using System.Collections.Generic;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal sealed class ScenarioStoryWorkspaceViewModelBuilder
    {
        private readonly ScenarioAuthoringWorkspaceViewModelFactory _factory;
        private readonly ScenarioStoryFlowWorkspaceBuilder _flowBuilder;
        private readonly ScenarioStoryCharactersWorkspaceBuilder _charactersBuilder;
        private readonly ScenarioStoryConversationsWorkspaceBuilder _conversationsBuilder;
        private readonly ScenarioQuestAuthoringContentBuilder _questBuilder;

        public ScenarioStoryWorkspaceViewModelBuilder()
        {
            _factory = new ScenarioAuthoringWorkspaceViewModelFactory();
            _flowBuilder = new ScenarioStoryFlowWorkspaceBuilder();
            _charactersBuilder = new ScenarioStoryCharactersWorkspaceBuilder();
            _conversationsBuilder = new ScenarioStoryConversationsWorkspaceBuilder();
            _questBuilder = new ScenarioQuestAuthoringContentBuilder();
        }

        public ScenarioAuthoringWorkspaceViewModel Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioAuthoringRendererInteractionState state = ScenarioAuthoringRendererInteractionState.Instance;
            string activeSubtab = state.GetWorkspaceSubtab(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioStoryFocusedEditorActions.FlowSubtabId);
            if (!IsKnownSubtab(activeSubtab))
            {
                activeSubtab = ScenarioStoryFocusedEditorActions.FlowSubtabId;
                state.SetWorkspaceSubtab(ScenarioStoryFocusedEditorActions.WorkspaceId, activeSubtab);
            }

            ScenarioAuthoringWorkspaceViewModel workspace;
            if (string.Equals(activeSubtab, ScenarioStoryFocusedEditorActions.CharactersSubtabId, StringComparison.Ordinal))
                workspace = _charactersBuilder.Build(context, _factory);
            else if (string.Equals(activeSubtab, ScenarioStoryFocusedEditorActions.ConversationsSubtabId, StringComparison.Ordinal))
                workspace = _conversationsBuilder.Build(context, _factory);
            else if (string.Equals(activeSubtab, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, StringComparison.Ordinal))
                workspace = BuildQuestComingNext(context);
            else
                workspace = _flowBuilder.Build(context, _factory);

            workspace.ActiveSubtabId = activeSubtab;
            workspace.Subtabs = BuildSubtabs(activeSubtab);
            return workspace;
        }

        private ScenarioAuthoringWorkspaceViewModel BuildQuestComingNext(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioAuthoringWorkspaceViewModel workspace = _factory.CreateWorkspace(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioAuthoringWorkspaceLayoutKind.DocumentOnly,
                ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId);
            ScenarioAuthoringWorkspaceDocumentViewModel document = _factory.CreateDocument("story.quest-popups.coming-next", "Quest Popups");
            document.Subtitle = "Navigator and focused quest documents arrive in Slice 5.";
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "story_quest_popups_coming_next",
                Title = "COMING NEXT",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.Text("Quest Popup authoring remains available below while its dedicated navigator and documents are prepared for the next slice.")
                }
            });
            sections.AddRange(_questBuilder.BuildQuestPopupSections(context));
            document.Sections = sections.ToArray();
            workspace.Document = document;
            return workspace;
        }

        private ScenarioAuthoringWorkspaceSubtabViewModel[] BuildSubtabs(string activeSubtab)
        {
            string[] ids =
            {
                ScenarioStoryFocusedEditorActions.FlowSubtabId,
                ScenarioStoryFocusedEditorActions.CharactersSubtabId,
                ScenarioStoryFocusedEditorActions.ConversationsSubtabId,
                ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId
            };
            string[] labels = { "Flow", "Characters", "Conversations", "Quest Popups" };
            string[] icons = { "FL", "CH", "CV", "QP" };
            ScenarioAuthoringWorkspaceSubtabViewModel[] tabs = new ScenarioAuthoringWorkspaceSubtabViewModel[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                tabs[i] = new ScenarioAuthoringWorkspaceSubtabViewModel
                {
                    Id = ids[i],
                    Label = labels[i],
                    IconText = icons[i],
                    Selected = string.Equals(activeSubtab, ids[i], StringComparison.Ordinal),
                    StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                    SelectAction = _factory.CreateSubtabAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ids[i], labels[i])
                };
            }
            return tabs;
        }

        private static bool IsKnownSubtab(string value)
        {
            return string.Equals(value, ScenarioStoryFocusedEditorActions.FlowSubtabId, StringComparison.Ordinal)
                || string.Equals(value, ScenarioStoryFocusedEditorActions.CharactersSubtabId, StringComparison.Ordinal)
                || string.Equals(value, ScenarioStoryFocusedEditorActions.ConversationsSubtabId, StringComparison.Ordinal)
                || string.Equals(value, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, StringComparison.Ordinal);
        }
    }
}
