using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal sealed class ScenarioStoryConversationsWorkspaceBuilder
    {
        public ScenarioAuthoringWorkspaceViewModel Build(
            ScenarioAuthoringWindowContentContext context,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioDefinition definition = context != null ? context.Definition : null;
            ScenarioAuthoringRendererInteractionState state = ScenarioAuthoringRendererInteractionState.Instance;
            string selected = state.GetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.ConversationsSubtabId);
            int selectedIndex;
            if (!string.IsNullOrEmpty(selected) && !ScenarioStoryFocusedEditorActions.TryResolveConversationEntity(definition, selected, out selectedIndex))
            {
                selected = null;
                state.SetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.ConversationsSubtabId, null);
            }

            ScenarioAuthoringWorkspaceViewModel workspace = factory.CreateWorkspace(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument,
                ScenarioStoryFocusedEditorActions.ConversationsSubtabId);
            workspace.Navigator = BuildNavigator(definition, selected, state, factory);
            workspace.Document = ScenarioStoryFocusedEditorActions.TryResolveConversationEntity(definition, selected, out selectedIndex)
                ? BuildConversationDocument(definition, selectedIndex, factory)
                : BuildOverview(definition, factory);
            return workspace;
        }

        private static ScenarioAuthoringNavigatorViewModel BuildNavigator(
            ScenarioDefinition definition,
            string selected,
            ScenarioAuthoringRendererInteractionState state,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioAuthoringNavigatorViewModel navigator = factory.CreateNavigator("story.conversations.navigator");
            navigator.SearchControlId = "story.conversations.search";
            navigator.SearchText = state.GetWorkspaceSearch(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.ConversationsSubtabId);
            navigator.SearchPlaceholder = "Search conversations";
            navigator.SelectedEntityId = selected;
            navigator.EmptyMessage = "No authored conversations yet.";
            navigator.Groups = new[]
            {
                BuildGroup(definition, selected, state, factory, ScenarioConversationTriggerSource.Random, "random", "Random Chatter", "RN", true, navigator.SearchText),
                BuildGroup(definition, selected, state, factory, ScenarioConversationTriggerSource.Event, "events", "Event Conversations", "EV", false, navigator.SearchText),
                BuildGroup(definition, selected, state, factory, ScenarioConversationTriggerSource.Timeline, "timeline", "Scheduled Conversations", "TL", false, navigator.SearchText)
            };
            return navigator;
        }

        private static ScenarioAuthoringNavigatorGroupViewModel BuildGroup(
            ScenarioDefinition definition,
            string selected,
            ScenarioAuthoringRendererInteractionState state,
            ScenarioAuthoringWorkspaceViewModelFactory factory,
            ScenarioConversationTriggerSource source,
            string groupId,
            string label,
            string icon,
            bool create,
            string search)
        {
            List<ScenarioAuthoringNavigatorRowViewModel> rows = new List<ScenarioAuthoringNavigatorRowViewModel>();
            ScenarioConversationAuthoringDefinition authored = definition != null ? definition.Conversations : null;
            for (int i = 0; authored != null && authored.Conversations != null && i < authored.Conversations.Count; i++)
            {
                ScenarioConversationDefinition conversation = authored.Conversations[i];
                ScenarioConversationTriggerDefinition trigger = conversation != null ? conversation.Trigger : null;
                if (conversation == null || (trigger != null ? trigger.Source : ScenarioConversationTriggerSource.Random) != source) continue;
                string title = DisplayName(conversation, i);
                string preview = FirstLine(conversation);
                if (!string.IsNullOrEmpty(search) && title.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 && preview.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string entity = ScenarioStoryFocusedEditorActions.ConversationEntityId(definition, i);
                bool ready = IsReady(conversation);
                rows.Add(new ScenarioAuthoringNavigatorRowViewModel
                {
                    EntityId = entity,
                    Title = title,
                    Subtitle = preview,
                    IconText = icon,
                    Selected = string.Equals(selected, entity, StringComparison.Ordinal),
                    StatusChips = new[]
                    {
                        new ScenarioAuthoringStatusChipViewModel
                        {
                            Id = "conversation.status." + i.ToString(CultureInfo.InvariantCulture),
                            Text = ready ? "Ready" : "Needs content",
                            Tone = ready ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Warning
                        }
                    },
                    SelectAction = factory.CreateEntityAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.ConversationsSubtabId, entity, "Select " + title),
                    Children = new ScenarioAuthoringNavigatorRowViewModel[0]
                });
            }
            return new ScenarioAuthoringNavigatorGroupViewModel
            {
                Id = groupId,
                Label = label,
                IconText = icon,
                Expanded = state.GetWorkspaceExpanded(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.ConversationsSubtabId, groupId, true),
                ToggleAction = factory.CreateGroupToggleAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.ConversationsSubtabId, groupId, "Toggle " + label),
                CreateAction = create ? ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationAdd, "Add Conversation", "Create and select a random conversation.", true, rows.Count == 0, "C+") : null,
                StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                Rows = rows.ToArray()
            };
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildOverview(ScenarioDefinition definition, ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioConversationAuthoringDefinition authored = definition != null ? definition.Conversations : null;
            int count = authored != null && authored.Conversations != null ? authored.Conversations.Count : 0;
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("story.conversations.overview", "Conversations");
            document.Subtitle = "Author random chatter, event dialogue, and scheduled conversations in plain language.";
            document.BackAction = factory.CreateBackAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.ConversationsSubtabId, "Back to Navigator");
            document.Sections = new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "story_conversations_overview",
                    Title = "AUTHORED CONVERSATIONS",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Property("Conversations", count.ToString(CultureInfo.InvariantCulture)),
                        ScenarioInspectorItemFactory.Property("Vanilla random chatter", authored != null && authored.Settings != null && authored.Settings.SuppressVanillaRandomChatter ? "Suppressed" : "Available"),
                        ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationAdd, "Add Conversation", "Create a random authored conversation.", true, count == 0, "C+")),
                        ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryConversationSuppressionToggle, "Suppress Vanilla Random Chatter", "Toggle vanilla random chatter while authored conversations are active.", true, authored != null && authored.Settings != null && authored.Settings.SuppressVanillaRandomChatter, "VN"))
                    }
                }
            };
            return document;
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildConversationDocument(ScenarioDefinition definition, int index, ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioConversationDefinition conversation = definition.Conversations.Conversations[index];
            string title = DisplayName(conversation, index);
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("story.conversation." + index.ToString(CultureInfo.InvariantCulture), title);
            document.Subtitle = TriggerLabel(conversation != null ? conversation.Trigger : null);
            document.BackAction = factory.CreateBackAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.ConversationsSubtabId, "Back to Navigator");
            document.Breadcrumbs = new[]
            {
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Story", Action = factory.CreateBreadcrumbAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.ConversationsSubtabId, string.Empty, "Story") },
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Conversations" },
                new ScenarioAuthoringBreadcrumbViewModel { Label = title }
            };
            document.StatusChips = new[]
            {
                new ScenarioAuthoringStatusChipViewModel
                {
                    Id = "conversation.document.status." + index.ToString(CultureInfo.InvariantCulture),
                    Text = IsReady(conversation) ? "Ready" : "Needs content",
                    Tone = IsReady(conversation) ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Warning
                }
            };
            document.Sections = ScenarioQuestAuthoringContentBuilder.BuildConversationWorkspaceDocumentSections(definition, index);
            return document;
        }

        private static string DisplayName(ScenarioConversationDefinition conversation, int index)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(null, null, conversation != null ? conversation.Id : null, "Conversation " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        private static string FirstLine(ScenarioConversationDefinition conversation)
        {
            ScenarioConversationLineDefinition line = conversation != null && conversation.Lines != null && conversation.Lines.Count > 0 ? conversation.Lines[0] : null;
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(line != null ? line.RawText : null, line != null ? line.TextKey : null, null, "No lines yet").Text;
        }

        private static string TriggerLabel(ScenarioConversationTriggerDefinition trigger)
        {
            if (trigger == null || trigger.Source == ScenarioConversationTriggerSource.Random) return "Random chatter";
            if (trigger.Source == ScenarioConversationTriggerSource.Event) return "Triggered by an authored event";
            return "Scheduled on the Story timeline";
        }

        private static bool IsReady(ScenarioConversationDefinition conversation)
        {
            return conversation != null && conversation.Participants != null && conversation.Participants.Count > 0 && conversation.Lines != null && conversation.Lines.Count > 0;
        }
    }
}
