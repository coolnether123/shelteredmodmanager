using System;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    /// <summary>Executable guard for the shell's storage-to-display language boundary.</summary>
    internal static class ScenarioAuthoringDisplayNameVerification
    {
        private static readonly string[] RawFixtureValues =
        {
            "quest.convoycrashsite.name",
            "stage_1",
            "dialogue_step_1",
            "workshop.quests.quest_authored_header_0"
        };

        public static void Verify(ScenarioValidationResult result)
        {
            ScenarioAuthoringDisplayNameResolver resolver = new ScenarioAuthoringDisplayNameResolver(false);
            AssertResolvedFallback(resolver, RawFixtureValues[0], "Quest 2", result);
            AssertResolvedFallback(resolver, RawFixtureValues[1], "Stage 1", result);
            AssertResolvedFallback(resolver, RawFixtureValues[2], "Scene 1", result);
            AssertResolvedFallback(resolver, RawFixtureValues[3], "Quest 1", result);
            Assert(!StatusBarViewModelBuilder.ShouldShowStatusMessage(
                    "Disclosure toggled: " + RawFixtureValues[3] + "."),
                "Renderer disclosure state escaped into the author-facing status bar.", result);
            Assert(!StatusBarViewModelBuilder.ShouldShowStatusMessage(
                    "Candidate search updated: " + RawFixtureValues[2] + "."),
                "Renderer search state escaped into the author-facing status bar.", result);

            ScenarioDefinition definition = BuildFixture();
            ScenarioAuthoringRendererInteractionState.Instance.SetWorkspaceSubtab(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioStoryFocusedEditorActions.FlowSubtabId);
            ScenarioAuthoringRendererInteractionState.Instance.SetWorkspaceSelection(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioStoryFocusedEditorActions.FlowSubtabId,
                null);
            ScenarioAuthoringWorkspaceViewModel workspace = new ScenarioStoryWorkspaceViewModelBuilder().Build(
                new ScenarioAuthoringWindowContentContext(null, null, null, definition));
            Assert(workspace != null && workspace.Document != null,
                "Display-name verification could not build the Story workspace fixture.", result);
            if (workspace != null && workspace.Document != null)
            {
                AssertNoRawPrimaryText(workspace.Document.Sections, result);
                ScenarioStoryFocusedEditorActions.SelectStageDocument(definition, 0);
                workspace = new ScenarioStoryWorkspaceViewModelBuilder().Build(
                    new ScenarioAuthoringWindowContentContext(null, null, null, definition));
                Assert(workspace != null && workspace.Document != null
                        && AdvancedContains(workspace.Document.Sections, RawFixtureValues[1]),
                    "Raw Story stage ids were not retained in Advanced rows.", result);
                ScenarioStoryFocusedEditorActions.SelectSceneDocument(definition, 0, 0);
                workspace = new ScenarioStoryWorkspaceViewModelBuilder().Build(
                    new ScenarioAuthoringWindowContentContext(null, null, null, definition));
                Assert(workspace != null && workspace.Document != null
                        && AdvancedContains(workspace.Document.Sections, RawFixtureValues[0])
                        && AdvancedContains(workspace.Document.Sections, RawFixtureValues[2])
                        && AdvancedContains(workspace.Document.Sections, RawFixtureValues[3]),
                    "Raw Story ids and localization keys were not retained in Advanced rows.", result);
            }
        }

        private static ScenarioDefinition BuildFixture()
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            ScenarioFlowStageDefinition stage = new ScenarioFlowStageDefinition { Id = RawFixtureValues[1] };
            ScenarioIntercomStageDefinition scene = new ScenarioIntercomStageDefinition
            {
                Id = RawFixtureValues[2],
                StageDescriptionKey = RawFixtureValues[3]
            };
            scene.Dialogue.Add(new ScenarioDialogueLineDefinition { TextKey = RawFixtureValues[0] });
            stage.IntercomStages.Add(scene);
            definition.ScenarioFlow.Stages.Add(stage);
            definition.Quests.Quests.Add(new QuestDefinition
            {
                Id = RawFixtureValues[0],
                Title = RawFixtureValues[0]
            });
            return definition;
        }

        private static void AssertResolvedFallback(
            IScenarioAuthoringDisplayNameResolver resolver,
            string raw,
            string fallback,
            ScenarioValidationResult result)
        {
            ScenarioAuthoringDisplayName display = resolver.Resolve(raw, raw, raw, fallback);
            Assert(display != null
                    && string.Equals(display.Text, fallback, StringComparison.Ordinal)
                    && string.Equals(display.LocalizationKey, raw, StringComparison.Ordinal)
                    && string.Equals(display.StorageId, raw, StringComparison.Ordinal)
                    && !display.LocalizationResolved,
                "Display-name fallback did not preserve the technical value for '" + raw + "'.", result);
        }

        private static void AssertNoRawPrimaryText(
            ScenarioAuthoringInspectorSection[] sections,
            ScenarioValidationResult result)
        {
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                if (section == null || section.IsAdvanced)
                    continue;
                AssertClean(section.Title, "section title", result);
                if (section.StoryMap != null)
                {
                    for (int n = 0; section.StoryMap.Nodes != null && n < section.StoryMap.Nodes.Length; n++)
                        AssertClean(section.StoryMap.Nodes[n] != null ? section.StoryMap.Nodes[n].Label : null, "Story Map node", result);
                }
                for (int j = 0; section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    if (item == null || IsTechnicalLabel(item.Label))
                        continue;
                    AssertClean(item.Label, "item label", result);
                    AssertClean(item.Value, "item value", result);
                    AssertClean(item.Action != null ? item.Action.Label : null, "action label", result);
                }
            }
        }

        private static bool IsTechnicalLabel(string label)
        {
            return !string.IsNullOrEmpty(label)
                && (label.IndexOf("technical", StringComparison.OrdinalIgnoreCase) >= 0
                    || label.IndexOf("advanced", StringComparison.OrdinalIgnoreCase) >= 0
                    || label.IndexOf("internal", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool AdvancedContains(ScenarioAuthoringInspectorSection[] sections, string value)
        {
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                if (section == null || !section.IsAdvanced)
                    continue;
                for (int j = 0; section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    if (item != null && Contains(item.Value, value))
                        return true;
                }
            }
            return false;
        }

        private static void AssertClean(string text, string location, ScenarioValidationResult result)
        {
            for (int i = 0; i < RawFixtureValues.Length; i++)
                Assert(!Contains(text, RawFixtureValues[i]),
                    "Raw storage value '" + RawFixtureValues[i] + "' escaped into a primary " + location + ".", result);
        }

        private static bool Contains(string text, string value)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition && result != null)
                result.AddError(message);
        }
    }
}
