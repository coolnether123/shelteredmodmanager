using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Presentation.Inspector;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    /// <summary>
    /// Read-only "script" view of one story stage. It renders a stage's intercom/dialogue
    /// flow the way a writer reads a radio drama: each speaker (with a portrait when the cast
    /// resolver can supply one) says a line, and the player's reply options are listed beneath
    /// with plain-language routing ("Continues to 'Second call'", "Ends the conversation",
    /// "Starts stage 'The Deal'"). Every line and reply carries an Edit affordance that jumps
    /// into the existing focused stage editor. Editing itself stays in the focused editor;
    /// this surface only reads.
    /// </summary>
    internal static class ScenarioStoryScriptViewBuilder
    {
        /// <summary>Build the read-only script section for a single stage.</summary>
        public static ScenarioAuthoringInspectorSection BuildStageScript(ScenarioDefinition definition, ScenarioFlowStageDefinition stage, int stageIndex)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();

            if (stage == null || stage.IntercomStages == null || stage.IntercomStages.Count == 0)
            {
                items.Add(ScenarioInspectorItemFactory.Text("This stage has no dialogue yet. Open the focused editor to write the first line."));
                items.Add(EditAction(stageIndex, "Open Focused Editor", "Open this stage in the focused story editor to add dialogue."));
                return Section(stageIndex, items);
            }

            items.Add(ScenarioInspectorItemFactory.Text("Read this scene like a script. Use Edit on any line to change it in the focused editor."));

            for (int s = 0; s < stage.IntercomStages.Count; s++)
            {
                ScenarioIntercomStageDefinition step = stage.IntercomStages[s];
                if (step == null)
                    continue;

                AppendStep(items, definition, stage, step, stageIndex, s);
            }

            return Section(stageIndex, items);
        }

        private static void AppendStep(
            List<ScenarioAuthoringInspectorItem> items,
            ScenarioDefinition definition,
            ScenarioFlowStageDefinition stage,
            ScenarioIntercomStageDefinition step,
            int stageIndex,
            int stepIndex)
        {
            // Scene header for this intercom step.
            items.Add(ScenarioInspectorItemFactory.Text(
                StepTitle(step, stepIndex),
                "Scene " + (stepIndex + 1).ToString(CultureInfo.InvariantCulture) + DescribeStepType(step),
                null,
                null,
                null,
                true));

            // Spoken lines, each with its speaker and (when available) portrait.
            bool spokeSomething = false;
            for (int d = 0; step.Dialogue != null && d < step.Dialogue.Count; d++)
            {
                ScenarioDialogueLineDefinition line = step.Dialogue[d];
                if (line == null)
                    continue;
                spokeSomething = true;

                string speaker = SpeakerName(definition, line.Character);
                ScenarioAuthoringDisplayName spokenName = ResolveText(
                    line.TextKey,
                    null,
                    "Dialogue " + (d + 1).ToString(CultureInfo.InvariantCulture));
                string spoken = spokenName.Text;
                Sprite portrait = ResolveSpeakerPortrait(definition, line.Character);
                string detail = string.IsNullOrEmpty(line.Character) ? "No speaker chosen" : ("Technical speaker id: " + line.Character);
                if (!string.IsNullOrEmpty(spokenName.LocalizationKey))
                    detail += ". Technical localization key: " + spokenName.LocalizationKey;

                items.Add(ScenarioInspectorItemFactory.Property(speaker, spoken, detail, null, null, portrait));
                items.Add(EditAction(stageIndex, "Edit line", "Open the focused editor to change what " + speaker + " says."));
            }

            if (!spokeSomething)
                items.Add(ScenarioInspectorItemFactory.Text("(No spoken lines in this scene yet.)"));

            // Player replies, each showing in plain language where it leads.
            for (int o = 0; step.Options != null && o < step.Options.Count; o++)
            {
                ScenarioDialogueOptionDefinition option = step.Options[o];
                if (option == null)
                    continue;

                string reply = ResolveText(
                    option.TextKey,
                    null,
                    "Reply " + (o + 1).ToString(CultureInfo.InvariantCulture)).Text;
                items.Add(ScenarioInspectorItemFactory.Property(
                    "Reply " + (o + 1).ToString(CultureInfo.InvariantCulture),
                    reply,
                    DescribeOptionRoute(stage, option)));
                items.Add(EditAction(stageIndex, "Edit reply", "Open the focused editor to change this reply or where it leads."));
            }

            // When there is no branching reply, describe how the scene moves on.
            if (step.Options == null || step.Options.Count == 0)
                items.Add(ScenarioInspectorItemFactory.Text("When this scene ends: " + DescribeStepEnding(definition, stage, step)));
        }

        // === Plain-language routing =====================================================
        // These are the testable heart of the script view: they turn stored ids into the
        // sentences a writer reads ("Continues to 'Second call'", "Ends the conversation",
        // "Starts stage 'The Deal'").

        /// <summary>Where a player reply leads, in plain language.</summary>
        public static string DescribeOptionRoute(ScenarioFlowStageDefinition stage, ScenarioDialogueOptionDefinition option)
        {
            string nextId = option != null ? option.NextId : null;
            if (string.IsNullOrEmpty(nextId))
                return "Ends the conversation";
            return "Continues to '" + StepTitleById(stage, nextId) + "'";
        }

        /// <summary>How a scene with no replies moves on: its next step and/or a stage change.</summary>
        public static string DescribeStepEnding(ScenarioDefinition definition, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition step)
        {
            List<string> parts = new List<string>();
            if (step != null && !string.IsNullOrEmpty(step.NextId))
                parts.Add("Continues to '" + StepTitleById(stage, step.NextId) + "'");
            if (step != null && !string.IsNullOrEmpty(step.AlternateNextId))
                parts.Add("or '" + StepTitleById(stage, step.AlternateNextId) + "' on the alternate route");

            string stageChange = DescribeStageChange(definition, step != null ? step.StageChange : null);
            if (!string.IsNullOrEmpty(stageChange))
                parts.Add(stageChange);

            if (parts.Count == 0)
                return "Ends the conversation";
            return string.Join(", ", parts.ToArray());
        }

        /// <summary>A delayed stage transition in plain language, or null when there is none.</summary>
        public static string DescribeStageChange(ScenarioDefinition definition, ScenarioStageChangeDefinition change)
        {
            if (change == null || string.IsNullOrEmpty(change.Id))
                return null;
            string sentence = "Starts stage '" + StageTitleById(definition, change.Id) + "'";
            if (change.DelayDays > 0)
                sentence += " after " + change.DelayDays.ToString(CultureInfo.InvariantCulture) + " day(s)";
            return sentence;
        }

        // === Labels and lookups =========================================================

        private static string DescribeStepType(ScenarioIntercomStageDefinition step)
        {
            string type = step != null ? step.Type : null;
            if (string.IsNullOrEmpty(type) || string.Equals(type, "Standard", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            return " (" + type + ")";
        }

        private static string StepTitle(ScenarioIntercomStageDefinition step, int index)
        {
            string fallback = "Scene " + (index + 1).ToString(CultureInfo.InvariantCulture);
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                null,
                null,
                step != null ? step.Id : null,
                fallback).Text;
        }

        private static string StepTitleById(ScenarioFlowStageDefinition stage, string stepId)
        {
            if (string.IsNullOrEmpty(stepId))
                return stepId;
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition step = stage.IntercomStages[i];
                if (step != null && string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase))
                    return StepTitle(step, i);
            }
            return "Missing scene";
        }

        private static string StageTitleById(ScenarioDefinition definition, string stageId)
        {
            if (string.IsNullOrEmpty(stageId))
                return stageId;
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                if (stage != null && string.Equals(stage.Id, stageId, StringComparison.OrdinalIgnoreCase))
                    return DisplayStageTitle(stage, i);
            }
            return "Missing stage";
        }

        private static string SpeakerName(ScenarioDefinition definition, string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return "Narration";
            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                if (character != null && string.Equals(character.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
                    return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                        character.DisplayName,
                        null,
                        character.CharacterId,
                        "Story character " + (i + 1).ToString(CultureInfo.InvariantCulture)).Text;
            }
            return "Story character";
        }

        private static string DisplayStageTitle(ScenarioFlowStageDefinition stage, int index)
        {
            string key = null;
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition step = stage.IntercomStages[i];
                if (step != null && !string.IsNullOrEmpty(step.StageDescriptionKey))
                {
                    key = step.StageDescriptionKey;
                    break;
                }
            }
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                key,
                key,
                stage != null ? stage.Id : null,
                "Stage " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        private static ScenarioAuthoringDisplayName ResolveText(string textOrKey, string storageId, string fallback)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(textOrKey, textOrKey, storageId, fallback);
        }

        private static Sprite ResolveSpeakerPortrait(ScenarioDefinition definition, string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || definition == null || definition.ScenarioCharacters == null)
                return null;
            for (int i = 0; i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                if (character == null || !string.Equals(character.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (character.ActorRef == null)
                    return null;
                ScenarioCastMemberReferenceCandidate candidate = ScenarioCastMemberReferenceCatalog.FindByActorRef(definition, character.ActorRef, true, true);
                if (candidate != null && candidate.Member != null)
                    return ScenarioCastPortraitResolver.Resolve(candidate.Member);
                return null;
            }
            return null;
        }

        private static ScenarioAuthoringInspectorItem EditAction(int stageIndex, string label, string hint)
        {
            return ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                FocusedStoryCommand.OpenStage(stageIndex),
                label,
                hint,
                true,
                false,
                "ED"));
        }

        private static ScenarioAuthoringInspectorSection Section(int stageIndex, List<ScenarioAuthoringInspectorItem> items)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "story_script_" + stageIndex.ToString(CultureInfo.InvariantCulture),
                Title = "Read the Scene",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }
    }
}
