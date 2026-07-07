using System;
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
            if (sections == null || definition == null)
                return;

            sections.Add(BuildCharactersSection(definition));

            for (int i = 0; definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                if (character == null)
                    continue;

                string indexText = i.ToString(CultureInfo.InvariantCulture);
                string characterId = FormatCharacterId(character, i);
                string displayName = FormatDisplayName(character, i);
                string linked = ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, character.ActorRef, true, true, "No cast actor linked");
                List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
                items.Add(ScenarioInspectorItemFactory.Property("Story character", characterId, "Internal CharacterId stays unchanged."));
                items.Add(EditableProperty("Display name " + (i + 1).ToString(CultureInfo.InvariantCulture), displayName, "displayName", i, "Shown in story character pickers. CharacterId remains immutable."));
                items.Add(EditableProperty("Preset " + (i + 1).ToString(CultureInfo.InvariantCulture), character.PresetId, "presetId", i, "Optional vanilla NPC preset id."));
                items.Add(EditableProperty("Personality " + (i + 1).ToString(CultureInfo.InvariantCulture), character.Personality, "personality", i, "Optional vanilla personality id."));
                items.Add(EditableProperty("Species " + (i + 1).ToString(CultureInfo.InvariantCulture), character.Species, "species", i, "Optional species override."));
                items.Add(ScenarioInspectorItemFactory.Property("Actor link", character.ActorRef != null ? linked : "None"));
                items.Add(ScenarioInspectorItemFactory.Property("References", BuildReferenceSummary(definition, character.CharacterId), "Delete is available only after these references are cleared."));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioStoryAuthoringActions.CharacterDelete(i),
                    "Remove Character",
                    "Remove this story character. If references exist, the editor lists what to clear first.",
                    true,
                    false,
                    "RM")));
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
                    Title = displayName + " Story Character",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                    Items = items.ToArray()
                });

                sections.Add(ScenarioCastMemberPickerBuilder.BuildSection(
                    "story_character_actor_picker_" + indexText,
                    "Link " + displayName + " To Cast",
                    definition,
                    true,
                    true,
                    character.ActorRef,
                    ScenarioAuthoringActionIds.ActionStoryCharacterActorPrefix,
                    indexText,
                    "Add starting or future survivors before linking this story character."));
            }
        }

        private static ScenarioAuthoringInspectorSection BuildCharactersSection(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionStoryCharacterAdd,
                "Add Character",
                "Create a new story CharacterId and display name.",
                true,
                definition == null || definition.ScenarioCharacters == null || definition.ScenarioCharacters.Count == 0,
                "C+")));

            if (definition == null || definition.ScenarioCharacters == null || definition.ScenarioCharacters.Count == 0)
            {
                items.Add(ScenarioInspectorItemFactory.Text("No story characters yet. Add one before assigning stage cast, dialogue speakers, intercom recruits, or actor links."));
            }
            else
            {
                for (int i = 0; i < definition.ScenarioCharacters.Count; i++)
                {
                    ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                    if (character == null)
                        continue;

                    string linked = ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, character.ActorRef, true, true, "No actor link");
                    items.Add(ScenarioInspectorItemFactory.Property(
                        FormatDisplayName(character, i),
                        FormatCharacterId(character, i),
                        character.ActorRef != null ? "Actor linked: " + linked : "No actor link",
                        character.ActorRef != null ? "LINK" : "UNLINKED"));
                }
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "story_characters",
                Title = "Characters",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorItem EditableProperty(string label, string value, string field, int characterIndex, string hint)
        {
            ScenarioAuthoringInspectorItem item = ScenarioInspectorItemFactory.Property(label, value ?? string.Empty, hint);
            item.Editable = true;
            item.Action = ScenarioInspectorItemFactory.Action(
                ScenarioStoryAuthoringActions.CharacterEditPrefix(characterIndex, field),
                label,
                hint,
                true,
                false,
                "ED");
            return item;
        }

        private static string BuildReferenceSummary(ScenarioDefinition definition, string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return "No references.";

            int stageCast = 0;
            int dialogue = 0;
            int recruit = 0;
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                if (Contains(stage != null ? stage.CharacterIds : null, characterId))
                    stageCast++;

                for (int s = 0; stage != null && stage.IntercomStages != null && s < stage.IntercomStages.Count; s++)
                {
                    ScenarioIntercomStageDefinition intercom = stage.IntercomStages[s];
                    for (int d = 0; intercom != null && intercom.Dialogue != null && d < intercom.Dialogue.Count; d++)
                    {
                        ScenarioDialogueLineDefinition line = intercom.Dialogue[d];
                        if (line != null && string.Equals(line.Character, characterId, StringComparison.OrdinalIgnoreCase))
                            dialogue++;
                    }

                    if (Contains(intercom != null ? intercom.CharacterIdsToRecruit : null, characterId))
                        recruit++;
                }
            }

            if (stageCast == 0 && dialogue == 0 && recruit == 0)
                return "No references.";

            return stageCast.ToString(CultureInfo.InvariantCulture)
                + " stage cast, "
                + dialogue.ToString(CultureInfo.InvariantCulture)
                + " dialogue, "
                + recruit.ToString(CultureInfo.InvariantCulture)
                + " recruit reference(s).";
        }

        private static string FormatCharacterId(ScenarioNpcDefinition character, int index)
        {
            return !string.IsNullOrEmpty(character != null ? character.CharacterId : null)
                ? character.CharacterId
                : "Character " + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatDisplayName(ScenarioNpcDefinition character, int index)
        {
            return !string.IsNullOrEmpty(character != null ? character.DisplayName : null)
                ? character.DisplayName
                : FormatCharacterId(character, index);
        }

        private static bool Contains(List<string> values, string value)
        {
            for (int i = 0; values != null && i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
