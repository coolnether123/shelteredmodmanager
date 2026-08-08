using ShelteredAPI.Scenarios.Domain.Validation;
using ShelteredScenarioEditor.Domain.Validation;
using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Presentation.Inspector;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal static class ScenarioStoryCharacterActorLinkSectionBuilder
    {
        internal static ScenarioAuthoringInspectorSection[] BuildWorkspaceDocumentSections(ScenarioDefinition definition, int characterIndex)
        {
            ScenarioNpcDefinition character = definition != null && definition.ScenarioCharacters != null
                && characterIndex >= 0 && characterIndex < definition.ScenarioCharacters.Count
                    ? definition.ScenarioCharacters[characterIndex]
                    : null;
            if (character == null)
                return new ScenarioAuthoringInspectorSection[0];

            string indexText = characterIndex.ToString(CultureInfo.InvariantCulture);
            string displayName = FormatDisplayName(character, characterIndex);
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();

            List<ScenarioAuthoringInspectorItem> identity = new List<ScenarioAuthoringInspectorItem>();
            identity.Add(EditableProperty("Display name", displayName, "displayName", characterIndex, "The name shown throughout Story authoring."));
            identity.Add(EditableProperty("Personality (optional)", character.Personality, "personality", characterIndex, "Leave blank to let the game choose a personality."));
            identity.Add(EditableProperty("Species (optional)", character.Species, "species", characterIndex, "Leave blank for the default human character."));
            sections.Add(Section("story_character_identity_" + indexText, "IDENTITY", ScenarioAuthoringInspectorSectionLayout.FactGrid, identity, false));

            string linked = ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, character.ActorRef, true, true, "No cast member linked");
            List<ScenarioAuthoringInspectorItem> cast = new List<ScenarioAuthoringInspectorItem>();
            cast.Add(ScenarioInspectorItemFactory.Property("Cast link", character.ActorRef != null ? linked : "None", "Linking lets Story reuse an authored starting or arriving survivor."));
            cast.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                StoryAuthoringCommands.ClearStoryCharacterActor(characterIndex),
                "Clear Cast Link",
                "Keep this story character while removing the cast link.",
                character.ActorRef != null,
                false,
                "CL")));
            sections.Add(Section("story_character_cast_" + indexText, "CAST LINK", ScenarioAuthoringInspectorSectionLayout.ActionStrip, cast, false));
            sections.Add(ScenarioCastMemberPickerBuilder.BuildSection(
                "story_character_cast_picker_" + indexText,
                "CHOOSE CAST MEMBER",
                definition,
                true,
                true,
                character.ActorRef,
                candidate => StoryAuthoringCommands.SetStoryCharacterActor(characterIndex, candidate.Token),
                "Add a starting or arriving survivor before linking this story character."));

            List<ScenarioAuthoringInspectorItem> usage = new List<ScenarioAuthoringInspectorItem>();
            List<ScenarioReferenceUsage> characterUsages = ScenarioReferenceIndex.FindUsages(definition, ScenarioReferenceTargetKind.StoryCharacter, character.CharacterId);
            usage.Add(ScenarioInspectorItemFactory.Property(
                "Usage",
                ScenarioReferenceIndex.Summarize(characterUsages.Count),
                characterUsages.Count > 0 ? "Clear these references before removing the character." : "Nothing references this character yet."));
            for (int i = 0; i < characterUsages.Count && i < 8; i++)
            {
                ScenarioReferenceUsage reference = characterUsages[i];
                if (reference == null || reference.NavStageIndex < 0)
                    continue;
                string stageTitle = ResolveStageTitle(definition, reference.NavStageIndex);
                usage.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    FocusedStoryCommand.OpenStage(reference.NavStageIndex),
                    "Go to " + stageTitle,
                    "Open the stage that uses this character.",
                    true,
                    false,
                    "->")));
            }
            usage.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                StoryAuthoringCommands.CharacterDelete(characterIndex),
                "Remove Character",
                "Remove this character when no Story references remain.",
                true,
                false,
                "RM")));
            sections.Add(Section("story_character_usage_" + indexText, "USAGE / TOOLS", ScenarioAuthoringInspectorSectionLayout.ActionStrip, usage, false));

            List<ScenarioAuthoringInspectorItem> advanced = new List<ScenarioAuthoringInspectorItem>();
            advanced.Add(ScenarioInspectorItemFactory.Property("Internal character id", FormatCharacterId(character, characterIndex), "Stable value used by routes, dialogue, and saved definitions."));
            advanced.Add(EditableProperty("Vanilla actor preset", character.PresetId, "presetId", characterIndex, "Optional vanilla NPC preset implementation detail."));
            advanced.Add(ScenarioInspectorItemFactory.Property("Actor reference", character.ActorRef != null ? ScenarioCastMemberReferenceCatalog.FormatActorRef(character.ActorRef) : "None"));
            sections.Add(Section("story_character_advanced_" + indexText, "ADVANCED", ScenarioAuthoringInspectorSectionLayout.PropertyList, advanced, true));
            return sections.ToArray();
        }

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
                    StoryAuthoringCommands.CharacterDelete(i),
                    "Remove Character",
                    "Remove this story character. If references exist, the editor lists what to clear first.",
                    true,
                    false,
                    "RM")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    StoryAuthoringCommands.ClearStoryCharacterActor(i),
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
                    candidate => StoryAuthoringCommands.SetStoryCharacterActor(i, candidate.Token),
                    "Add starting or future survivors before linking this story character."));
            }
        }

        private static ScenarioAuthoringInspectorSection BuildCharactersSection(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                StoryAuthoringCommands.AddStoryCharacter(),
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
                StoryAuthoringCommands.CharacterEdit(characterIndex, field),
                label,
                hint,
                true,
                false,
                "ED");
            return item;
        }

        private static ScenarioAuthoringInspectorSection Section(
            string id,
            string title,
            ScenarioAuthoringInspectorSectionLayout layout,
            List<ScenarioAuthoringInspectorItem> items,
            bool advanced)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = title,
                Expanded = true,
                Layout = layout,
                IsAdvanced = advanced,
                Items = items.ToArray()
            };
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
                    FocusedStoryCommand.OpenStage(usage.NavStageIndex),
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
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                character != null ? character.DisplayName : null,
                null,
                character != null ? character.CharacterId : null,
                "Story Character " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        private static string ResolveStageTitle(ScenarioDefinition definition, int stageIndex)
        {
            ScenarioFlowStageDefinition stage = definition != null && definition.ScenarioFlow != null && definition.ScenarioFlow.Stages != null
                && stageIndex >= 0 && stageIndex < definition.ScenarioFlow.Stages.Count
                    ? definition.ScenarioFlow.Stages[stageIndex]
                    : null;
            string title = null;
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                if (stage.IntercomStages[i] != null && !string.IsNullOrEmpty(stage.IntercomStages[i].StageDescriptionKey))
                {
                    title = stage.IntercomStages[i].StageDescriptionKey;
                    break;
                }
            }
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                title,
                title,
                stage != null ? stage.Id : null,
                "Stage " + (stageIndex + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

    }
}
