using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Validation;
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
                // Real creator language first: the writer edits the character by name, not by a
                // "Display name 1" debug stepper. Optional vanilla fields are labelled as optional,
                // and the raw internal CharacterId drops to an Advanced row at the bottom.
                items.Add(EditableProperty("Display name", displayName, "displayName", i, "The name writers see in pickers, dialogue, and the script view."));
                items.Add(EditableProperty("Vanilla preset (optional)", character.PresetId, "presetId", i, "Leave blank unless you are cloning a specific vanilla NPC preset."));
                items.Add(EditableProperty("Personality (optional)", character.Personality, "personality", i, "Leave blank to let the game pick a personality."));
                items.Add(EditableProperty("Species (optional)", character.Species, "species", i, "Leave blank for the default human survivor."));
                items.Add(ScenarioInspectorItemFactory.Property("Actor link", character.ActorRef != null ? linked : "None"));
                AppendUsages(items, definition, ScenarioReferenceTargetKind.StoryCharacter, character.CharacterId, "Delete is available only after these references are cleared.");
                items.Add(ScenarioInspectorItemFactory.Property("Advanced: internal id", characterId, "Stable CharacterId used by save files and references. It never changes."));
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

        // Shared Find Usages affordance: a plain-language "Used in N places" line backed by the
        // reference index, plus a clickable "Go to" action per usage that reuses the focused-editor
        // navigation seam. Kept here so every editor surface renders references the same way.
        internal static void AppendUsages(
            List<ScenarioAuthoringInspectorItem> items,
            ScenarioDefinition definition,
            ScenarioReferenceTargetKind kind,
            string id,
            string hint)
        {
            const int MaxNavActions = 8;
            List<ScenarioReferenceUsage> usages = ScenarioReferenceIndex.FindUsages(definition, kind, id);
            string detail = usages.Count > 0 ? DescribeFirstUsages(usages) : "Nothing references this yet.";
            items.Add(ScenarioInspectorItemFactory.Property(
                "References",
                ScenarioReferenceIndex.Summarize(usages.Count),
                string.IsNullOrEmpty(hint) ? detail : detail + " " + hint,
                usages.Count > 0 ? "USE" : "OK"));

            int shown = 0;
            for (int i = 0; i < usages.Count && shown < MaxNavActions; i++)
            {
                ScenarioReferenceUsage usage = usages[i];
                if (usage.NavStageIndex < 0)
                    continue;
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioStoryFocusedEditorActions.StageOpen(usage.NavStageIndex),
                    "Go to: " + usage.OwnerLabel,
                    "Open " + usage.OwnerLabel + " (" + usage.DisplayLabel + ") in the focused story editor.",
                    true,
                    false,
                    "->")));
                shown++;
            }
        }

        private static string DescribeFirstUsages(List<ScenarioReferenceUsage> usages)
        {
            const int MaxDescribed = 3;
            List<string> parts = new List<string>();
            for (int i = 0; i < usages.Count && i < MaxDescribed; i++)
                parts.Add(usages[i].OwnerLabel + " " + usages[i].DisplayLabel);
            string joined = string.Join("; ", parts.ToArray());
            if (usages.Count > MaxDescribed)
                joined += "; +" + (usages.Count - MaxDescribed).ToString(CultureInfo.InvariantCulture) + " more";
            return joined + ".";
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

    }
}
