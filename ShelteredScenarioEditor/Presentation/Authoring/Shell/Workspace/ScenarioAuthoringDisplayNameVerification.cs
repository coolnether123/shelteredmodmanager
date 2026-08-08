using System;

using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredScenarioEditor.Domain.Stages;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
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
            ScenarioAuthoringRendererInteractionState rendererInteraction = new ScenarioAuthoringRendererInteractionState();
            rendererInteraction.SetWorkspaceSubtab(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioStoryFocusedEditorActions.FlowSubtabId);
            rendererInteraction.SetWorkspaceSelection(
                ScenarioStoryFocusedEditorActions.WorkspaceId,
                ScenarioStoryFocusedEditorActions.FlowSubtabId,
                null);
            ScenarioAuthoringWorkspaceViewModel workspace = new ScenarioStoryWorkspaceViewModelBuilder().Build(
                new ScenarioAuthoringWindowContentContext(null, null, null, definition, rendererInteraction));
            Assert(workspace != null && workspace.Document != null,
                "Display-name verification could not build the Story workspace fixture.", result);
            if (workspace != null && workspace.Document != null)
            {
                AssertNoRawWorkspacePrimaryText(workspace, result);
                ScenarioStoryFocusedEditorActions.SelectStageDocument(definition, 0, rendererInteraction);
                workspace = new ScenarioStoryWorkspaceViewModelBuilder().Build(
                    new ScenarioAuthoringWindowContentContext(null, null, null, definition, rendererInteraction));
                Assert(workspace != null && workspace.Document != null
                        && AdvancedContains(workspace.Document.Sections, RawFixtureValues[1]),
                    "Raw Story stage ids were not retained in Advanced rows.", result);
                ScenarioStoryFocusedEditorActions.SelectSceneDocument(definition, 0, 0, rendererInteraction);
                workspace = new ScenarioStoryWorkspaceViewModelBuilder().Build(
                    new ScenarioAuthoringWindowContentContext(null, null, null, definition, rendererInteraction));
                Assert(workspace != null && workspace.Document != null
                        && AdvancedContains(workspace.Document.Sections, RawFixtureValues[0])
                        && AdvancedContains(workspace.Document.Sections, RawFixtureValues[2])
                        && AdvancedContains(workspace.Document.Sections, RawFixtureValues[3]),
                    "Raw Story ids and localization keys were not retained in Advanced rows.", result);
            }

            ScenarioAuthoringWorkspaceComposer composer = new ScenarioAuthoringWorkspaceComposer();
            ScenarioAuthoringState state = new ScenarioAuthoringState();
            ScenarioAuthoringWindowContentContext context = new ScenarioAuthoringWindowContentContext(state, null, null, definition, rendererInteraction);
            AssertNoRawWorkspacePrimaryText(composer.Build(ScenarioAuthoringWindowContentKind.Survivors, context), result);
            AssertNoRawWorkspacePrimaryText(composer.Build(ScenarioAuthoringWindowContentKind.Stockpile, context), result);
            AssertNoRawWorkspacePrimaryText(composer.Build(ScenarioAuthoringWindowContentKind.Map, context), result);
            AssertNoRawWorkspacePrimaryText(composer.Build(ScenarioAuthoringWindowContentKind.Publish, context), result);
            state.ActiveStage = ScenarioStageKind.Test;
            AssertNoRawWorkspacePrimaryText(composer.Build(ScenarioAuthoringWindowContentKind.Scenario, context), result);
        }

        private static ScenarioDefinition BuildFixture()
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.Id = RawFixtureValues[1];
            definition.DisplayName = RawFixtureValues[0];
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
            definition.FamilySetup.Members.Add(new FamilyMemberConfig { Name = RawFixtureValues[3] });
            definition.StartingInventory.Items.Add(new ItemEntry { ItemId = RawFixtureValues[2], Quantity = 2 });
            definition.Map.Locations.Add(new MapLocationDefinition { Id = RawFixtureValues[1], DisplayName = RawFixtureValues[0] });
            return definition;
        }

        private static void AssertNoRawWorkspacePrimaryText(
            ScenarioAuthoringWorkspaceViewModel workspace,
            ScenarioValidationResult result)
        {
            Assert(workspace != null, "Display-name verification could not build a migrated workspace fixture.", result);
            if (workspace == null)
                return;
            for (int i = 0; workspace.Subtabs != null && i < workspace.Subtabs.Length; i++)
            {
                ScenarioAuthoringWorkspaceSubtabViewModel subtab = workspace.Subtabs[i];
                if (subtab == null) continue;
                AssertClean(subtab.Label, "workspace subtab", result);
                AssertChipsClean(subtab.StatusChips, result);
            }
            for (int i = 0; workspace.Navigator != null && workspace.Navigator.Groups != null && i < workspace.Navigator.Groups.Length; i++)
            {
                ScenarioAuthoringNavigatorGroupViewModel group = workspace.Navigator.Groups[i];
                if (group == null) continue;
                AssertClean(group.Label, "navigator group", result);
                AssertClean(group.CreateAction != null ? group.CreateAction.Label : null, "create action", result);
                AssertChipsClean(group.StatusChips, result);
                AssertRowsClean(group.Rows, result);
            }
            ScenarioAuthoringWorkspaceDocumentViewModel document = workspace.Document;
            if (document == null) return;
            AssertClean(document.Title, "document title", result);
            AssertClean(document.Subtitle, "document subtitle", result);
            AssertChipsClean(document.StatusChips, result);
            for (int i = 0; document.Breadcrumbs != null && i < document.Breadcrumbs.Length; i++)
                AssertClean(document.Breadcrumbs[i] != null ? document.Breadcrumbs[i].Label : null, "breadcrumb", result);
            AssertNoRawPrimaryText(document.Sections, result);
        }

        private static void AssertRowsClean(ScenarioAuthoringNavigatorRowViewModel[] rows, ScenarioValidationResult result)
        {
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                ScenarioAuthoringNavigatorRowViewModel row = rows[i];
                if (row == null) continue;
                AssertClean(row.Title, "navigator row", result);
                AssertClean(row.Subtitle, "navigator row subtitle", result);
                AssertChipsClean(row.StatusChips, result);
                AssertRowsClean(row.Children, result);
            }
        }

        private static void AssertChipsClean(ScenarioAuthoringStatusChipViewModel[] chips, ScenarioValidationResult result)
        {
            for (int i = 0; chips != null && i < chips.Length; i++)
                AssertClean(chips[i] != null ? chips[i].Text : null, "status chip", result);
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
                if (section.InventorySlotGrid != null)
                {
                    for (int slotIndex = 0; section.InventorySlotGrid.Slots != null && slotIndex < section.InventorySlotGrid.Slots.Length; slotIndex++)
                    {
                        ScenarioInventorySlotViewModel slot = section.InventorySlotGrid.Slots[slotIndex];
                        if (slot == null) continue;
                        AssertClean(slot.DisplayName, "inventory slot name", result);
                        AssertClean(slot.Detail, "inventory slot detail", result);
                        AssertClean(slot.Badge, "inventory slot badge", result);
                        AssertClean(slot.ScheduleText, "inventory slot schedule", result);
                    }
                }
                for (int j = 0; section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    if (item == null || IsTechnicalLabel(item.Label))
                        continue;
                    AssertClean(item.Label, "item label", result);
                    AssertClean(item.Value, "item value", result);
                    AssertClean(item.Action != null ? item.Action.Label : null, "action label", result);
                    if (item.CastCard != null)
                    {
                        AssertClean(item.CastCard.Name, "cast card name", result);
                        AssertClean(item.CastCard.RoleLine, "cast card role", result);
                        AssertClean(item.CastCard.Status, "cast card status", result);
                        AssertClean(item.CastCard.ArrivalSummary, "cast card arrival", result);
                    }
                    if (item.Choice != null)
                    {
                        AssertClean(item.Choice.Label, "choice label", result);
                        for (int optionIndex = 0; item.Choice.Options != null && optionIndex < item.Choice.Options.Length; optionIndex++)
                            AssertClean(item.Choice.Options[optionIndex] != null ? item.Choice.Options[optionIndex].Label : null, "choice option", result);
                    }
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
