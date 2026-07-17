using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Validation;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal sealed class ScenarioStoryCharactersWorkspaceBuilder
    {
        public ScenarioAuthoringWorkspaceViewModel Build(
            ScenarioAuthoringWindowContentContext context,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioDefinition definition = context != null ? context.Definition : null;
            ScenarioAuthoringRendererInteractionState state = ScenarioAuthoringRendererInteractionState.Instance;
            string selected = state.GetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.CharactersSubtabId);
            int selectedIndex;
            if (!string.IsNullOrEmpty(selected) && !ScenarioStoryFocusedEditorActions.TryResolveCharacterEntity(definition, selected, out selectedIndex))
            {
                selected = null;
                state.SetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.CharactersSubtabId, null);
            }

            ScenarioAuthoringWorkspaceViewModel workspace = factory.CreateWorkspace(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument,
                ScenarioStoryFocusedEditorActions.CharactersSubtabId);
            workspace.Navigator = BuildNavigator(definition, selected, state, factory);
            workspace.Document = ScenarioStoryFocusedEditorActions.TryResolveCharacterEntity(definition, selected, out selectedIndex)
                ? BuildCharacterDocument(definition, selectedIndex, factory)
                : BuildOverview(definition, factory);
            return workspace;
        }

        private static ScenarioAuthoringNavigatorViewModel BuildNavigator(
            ScenarioDefinition definition,
            string selected,
            ScenarioAuthoringRendererInteractionState state,
            ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioAuthoringNavigatorViewModel navigator = factory.CreateNavigator("story.characters.navigator");
            navigator.SearchControlId = "story.characters.search";
            navigator.SearchText = state.GetWorkspaceSearch(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.CharactersSubtabId);
            navigator.SearchPlaceholder = "Search characters";
            navigator.SelectedEntityId = selected;
            navigator.EmptyMessage = "No Story characters yet.";
            List<ScenarioAuthoringNavigatorRowViewModel> rows = new List<ScenarioAuthoringNavigatorRowViewModel>();
            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                if (character == null) continue;
                string title = DisplayName(character, i);
                if (!string.IsNullOrEmpty(navigator.SearchText) && title.IndexOf(navigator.SearchText, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string entity = ScenarioStoryFocusedEditorActions.CharacterEntityId(definition, i);
                int usageCount = ScenarioReferenceIndex.FindUsages(definition, ScenarioReferenceTargetKind.StoryCharacter, character.CharacterId).Count;
                List<ScenarioAuthoringStatusChipViewModel> chips = new List<ScenarioAuthoringStatusChipViewModel>();
                chips.Add(new ScenarioAuthoringStatusChipViewModel
                {
                    Id = "character.uses." + i.ToString(CultureInfo.InvariantCulture),
                    Text = usageCount.ToString(CultureInfo.InvariantCulture) + (usageCount == 1 ? " use" : " uses"),
                    Tone = ScenarioAuthoringStatusTone.Informational
                });
                chips.Add(new ScenarioAuthoringStatusChipViewModel
                {
                    Id = "character.link." + i.ToString(CultureInfo.InvariantCulture),
                    Text = character.ActorRef != null ? "Cast linked" : "No cast link",
                    Tone = character.ActorRef != null ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Neutral
                });
                rows.Add(new ScenarioAuthoringNavigatorRowViewModel
                {
                    EntityId = entity,
                    Title = title,
                    Subtitle = character.ActorRef != null
                        ? ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, character.ActorRef, true, true, "Linked cast member")
                        : "Authored Story character",
                    IconText = "CH",
                    Selected = string.Equals(selected, entity, StringComparison.Ordinal),
                    StatusChips = chips.ToArray(),
                    SelectAction = factory.CreateEntityAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.CharactersSubtabId, entity, "Select " + title),
                    Children = new ScenarioAuthoringNavigatorRowViewModel[0]
                });
            }
            navigator.Groups = new[]
            {
                new ScenarioAuthoringNavigatorGroupViewModel
                {
                    Id = "characters",
                    Label = "Story Characters",
                    IconText = "CH",
                    Expanded = state.GetWorkspaceExpanded(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.CharactersSubtabId, "characters", true),
                    ToggleAction = factory.CreateGroupToggleAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.CharactersSubtabId, "characters", "Toggle Story Characters"),
                    CreateAction = ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryCharacterAdd, "Add Character", "Create and select a Story character.", true, rows.Count == 0, "C+"),
                    StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                    Rows = rows.ToArray()
                }
            };
            return navigator;
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildOverview(ScenarioDefinition definition, ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            int count = definition != null && definition.ScenarioCharacters != null ? definition.ScenarioCharacters.Count : 0;
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("story.characters.overview", "Story Characters");
            document.Subtitle = "Create named Story roles, connect them to cast members, and see where they are used.";
            document.BackAction = factory.CreateBackAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.CharactersSubtabId, "Back to Navigator");
            document.Sections = new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "story_characters_overview",
                    Title = "CHARACTERS",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Property("Authored characters", count.ToString(CultureInfo.InvariantCulture)),
                        ScenarioInspectorItemFactory.Text("Select a character to edit identity, cast link, and usage."),
                        ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionStoryCharacterAdd, "Add Character", "Create a new Story character.", true, count == 0, "C+"))
                    }
                }
            };
            return document;
        }

        private static ScenarioAuthoringWorkspaceDocumentViewModel BuildCharacterDocument(ScenarioDefinition definition, int index, ScenarioAuthoringWorkspaceViewModelFactory factory)
        {
            ScenarioNpcDefinition character = definition.ScenarioCharacters[index];
            string title = DisplayName(character, index);
            ScenarioAuthoringWorkspaceDocumentViewModel document = factory.CreateDocument("story.character." + index.ToString(CultureInfo.InvariantCulture), title);
            document.Subtitle = character.ActorRef != null ? "Linked Story role" : "Authored Story role";
            document.BackAction = factory.CreateBackAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.CharactersSubtabId, "Back to Navigator");
            document.Breadcrumbs = new[]
            {
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Story", Action = factory.CreateBreadcrumbAction(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.CharactersSubtabId, string.Empty, "Story") },
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Characters" },
                new ScenarioAuthoringBreadcrumbViewModel { Label = title }
            };
            document.Sections = ScenarioStoryCharacterActorLinkSectionBuilder.BuildWorkspaceDocumentSections(definition, index);
            return document;
        }

        private static string DisplayName(ScenarioNpcDefinition character, int index)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                character != null ? character.DisplayName : null,
                null,
                character != null ? character.CharacterId : null,
                "Story Character " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }
    }
}
