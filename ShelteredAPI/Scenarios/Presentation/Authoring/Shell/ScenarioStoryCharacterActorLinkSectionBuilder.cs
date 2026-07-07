using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal static class ScenarioStoryCharacterActorLinkSectionBuilder
    {
        public static void AppendSections(List<ScenarioAuthoringInspectorSection> sections, ScenarioDefinition definition)
        {
            if (sections == null || definition == null || definition.ScenarioCharacters == null || definition.ScenarioCharacters.Count == 0)
                return;

            for (int i = 0; i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                if (character == null)
                    continue;

                string indexText = i.ToString(CultureInfo.InvariantCulture);
                string characterId = !string.IsNullOrEmpty(character.CharacterId) ? character.CharacterId : "Character " + (i + 1).ToString(CultureInfo.InvariantCulture);
                string linked = ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, character.ActorRef, true, true, "No cast actor linked");
                List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
                items.Add(ScenarioInspectorItemFactory.Property("Story character", characterId, "Internal CharacterId stays unchanged."));
                items.Add(ScenarioInspectorItemFactory.Property("Actor link", character.ActorRef != null ? linked : "None"));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionIds.ActionStoryCharacterActorClearPrefix + indexText,
                    "Clear Actor Link",
                    "Keep the story CharacterId but remove the optional cast actor link.",
                    character.ActorRef != null,
                    false,
                    "CL")));

                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "story_character_actor_summary_" + indexText,
                    Title = characterId + " Actor Link",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                    Items = items.ToArray()
                });

                sections.Add(ScenarioCastMemberPickerBuilder.BuildSection(
                    "story_character_actor_picker_" + indexText,
                    "Link " + characterId + " To Cast",
                    definition,
                    true,
                    true,
                    character.ActorRef,
                    ScenarioAuthoringActionIds.ActionStoryCharacterActorPrefix,
                    indexText,
                    "Add starting or future survivors before linking this story character."));
            }
        }
    }
}
