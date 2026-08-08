using System;

using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Authoring.Supplies;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Scheduling;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    /// <summary>Executable contract fixture for the Slice 7 Supplies workspace.</summary>
    internal static class ScenarioSuppliesWorkspaceVerification
    {
        public static void Verify(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.StartingInventory.Items.Add(new ItemEntry { ItemId = "storage.raw_water", Quantity = 3 });
            definition.StartingInventory.ScheduledChanges.Add(new TimedInventoryChangeDefinition
            {
                Id = "scheduled.storage.1",
                ItemId = "storage.raw_ration",
                Quantity = 2,
                Kind = ScenarioInventoryChangeKind.Add,
                When = new ScenarioScheduleTime { Day = 2, Hour = 8 }
            });

            ScenarioAuthoringRendererInteractionState state = new ScenarioAuthoringRendererInteractionState();
            state.SetWorkspaceSubtab(ScenarioSuppliesWorkspaceActions.WorkspaceId, ScenarioSuppliesWorkspaceActions.SubtabId);
            state.SetWorkspaceSelection(ScenarioSuppliesWorkspaceActions.WorkspaceId, ScenarioSuppliesWorkspaceActions.SubtabId, null);
            ScenarioSuppliesWorkspaceViewModelBuilder builder = new ScenarioSuppliesWorkspaceViewModelBuilder();
            ScenarioAuthoringWindowContentContext context = new ScenarioAuthoringWindowContentContext(new ScenarioAuthoringState(), null, null, definition, state);

            ScenarioAuthoringWorkspaceViewModel starting = builder.Build(context);
            Assert(starting != null && starting.LayoutKind == ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument,
                "Supplies workspace must use NavigatorDocument.", result);
            AssertRoots(starting, result);
            Assert(starting != null && starting.Document != null && string.Equals(starting.Document.Title, "Starting Items", StringComparison.Ordinal),
                "Supplies must default to the Starting Items document.", result);
            ScenarioInventorySlotGridViewModel startingGrid = FindInventoryGrid(starting != null ? starting.Document : null);
            Assert(ContainsSlotCommand(startingGrid, GameplayScheduleCommandKind.AddStartingItemAndPick, false),
                "Starting Items grid lost its typed add-and-pick command.", result);
            Assert(startingGrid != null && startingGrid.Slots != null && startingGrid.Slots.Length > 0
                    && !Contains(startingGrid.Slots[0].DisplayName, "storage.raw_water")
                    && !Contains(startingGrid.Slots[0].Detail, "storage.raw_water"),
                "Starting Items exposed a raw item storage ID on the primary grid.", result);
            Assert(IsAdvancedLast(starting != null && starting.Document != null ? starting.Document.Sections : null),
                "Starting Items must finish with Advanced.", result);
            AssertUniqueActions(starting, "Starting Items", result);

            ScenarioAuthoringNavigatorRowViewModel presetsRoot = Root(starting, 1);
            Assert(presetsRoot != null && presetsRoot.Children != null && presetsRoot.Children.Length == ScenarioSuppliesPresetCatalog.Count,
                "Supplies preset navigator children are incomplete.", result);
            Assert(presetsRoot != null && presetsRoot.Children != null && presetsRoot.Children.Length > 0
                    && presetsRoot.Children[0].SelectAction != null
                    && IsCommand(presetsRoot.Children[0].SelectAction, GameplayScheduleCommandKind.PreviewSuppliesPreset, 0),
                "Preset navigation must use the typed preview command.", result);

            int emptyPresetIndex = ScenarioSuppliesPresetCatalog.Count - 1;
            ScenarioSuppliesWorkspaceActions.SelectPresetDocument(emptyPresetIndex, state);
            ScenarioAuthoringWorkspaceViewModel preset = builder.Build(context);
            Assert(preset != null && preset.Document != null && Contains(preset.Document.Title, "Loadout"),
                "Preset selection did not open an inline Supplies document.", result);
            Assert(ContainsCommand(preset != null ? preset.Document : null, GameplayScheduleCommandKind.ApplySuppliesPreset, emptyPresetIndex),
                "Inline preset document lost its typed apply command.", result);
            Assert(!ContainsAction(preset != null ? preset.Document : null, ScenarioAuthoringActionIds.ActionFocusedEditorCancel),
                "Inline preset document still contains modal Cancel routing.", result);
            Assert(IsAdvancedLast(preset != null && preset.Document != null ? preset.Document.Sections : null),
                "Preset document must finish with Advanced.", result);
            AssertUniqueActions(preset, "preset", result);

            state.SetWorkspaceSelection(ScenarioSuppliesWorkspaceActions.WorkspaceId, ScenarioSuppliesWorkspaceActions.SubtabId, ScenarioSuppliesWorkspaceActions.Scheduled);
            ScenarioAuthoringWorkspaceViewModel scheduled = builder.Build(context);
            ScenarioInventorySlotGridViewModel scheduledGrid = FindInventoryGrid(scheduled != null ? scheduled.Document : null);
            Assert(ContainsSlotCommand(scheduledGrid, GameplayScheduleCommandKind.AddTimedItemAndPick, false)
                    && ContainsSlotCommand(scheduledGrid, GameplayScheduleCommandKind.AddTimedItemAndPick, true),
                "Scheduled grid lost its typed add/remove picker commands.", result);
            Assert(IsAdvancedLast(scheduled != null && scheduled.Document != null ? scheduled.Document.Sections : null),
                "Scheduled document must finish with Advanced.", result);
            AssertUniqueActions(scheduled, "Scheduled", result);

            state.SetWorkspaceSelection(ScenarioSuppliesWorkspaceActions.WorkspaceId, ScenarioSuppliesWorkspaceActions.SubtabId, ScenarioSuppliesWorkspaceActions.LiveReference);
            ScenarioAuthoringWorkspaceViewModel live = builder.Build(context);
            Assert(live != null && live.Document != null
                    && Contains(live.Document.Subtitle, "READ-ONLY")
                    && HasChip(live.Document.StatusChips, "Read-only"),
                "Live Reference is not clearly marked read-only.", result);
            Assert(IsAdvancedLast(live != null && live.Document != null ? live.Document.Sections : null),
                "Live Reference must finish with Advanced.", result);
            AssertUniqueActions(live, "Live Reference", result);

            state.SetWorkspaceSelection(ScenarioSuppliesWorkspaceActions.WorkspaceId, ScenarioSuppliesWorkspaceActions.SubtabId, null);
        }

        private static void AssertRoots(ScenarioAuthoringWorkspaceViewModel workspace, ScenarioValidationResult result)
        {
            string[] expected = { "Starting Items", "Presets", "Balance", "Scheduled", "Live Reference" };
            ScenarioAuthoringNavigatorRowViewModel[] rows = workspace != null && workspace.Navigator != null
                && workspace.Navigator.Groups != null && workspace.Navigator.Groups.Length == 1
                ? workspace.Navigator.Groups[0].Rows
                : null;
            Assert(rows != null && rows.Length == expected.Length,
                "Supplies navigator must expose exactly five fixed roots.", result);
            for (int i = 0; rows != null && i < rows.Length && i < expected.Length; i++)
                Assert(rows[i] != null && string.Equals(rows[i].Title, expected[i], StringComparison.Ordinal),
                    "Supplies navigator root order changed at " + expected[i] + ".", result);
        }

        private static ScenarioAuthoringNavigatorRowViewModel Root(ScenarioAuthoringWorkspaceViewModel workspace, int index)
        {
            return workspace != null && workspace.Navigator != null && workspace.Navigator.Groups != null
                && workspace.Navigator.Groups.Length > 0 && workspace.Navigator.Groups[0].Rows != null
                && index >= 0 && index < workspace.Navigator.Groups[0].Rows.Length
                ? workspace.Navigator.Groups[0].Rows[index]
                : null;
        }

        private static ScenarioInventorySlotGridViewModel FindInventoryGrid(ScenarioAuthoringWorkspaceDocumentViewModel document)
        {
            for (int i = 0; document != null && document.Sections != null && i < document.Sections.Length; i++)
                if (document.Sections[i] != null && document.Sections[i].InventorySlotGrid != null)
                    return document.Sections[i].InventorySlotGrid;
            return null;
        }

        private static bool ContainsSlotCommand(ScenarioInventorySlotGridViewModel grid, GameplayScheduleCommandKind kind, bool remove)
        {
            for (int i = 0; grid != null && grid.Slots != null && i < grid.Slots.Length; i++)
            {
                GameplayScheduleCommand command = grid.Slots[i] != null && grid.Slots[i].PrimaryAction != null
                    ? grid.Slots[i].PrimaryAction.Command as GameplayScheduleCommand : null;
                if (command != null && command.Kind == kind && command.Remove == remove) return true;
            }
            return false;
        }

        private static bool ContainsCommand(ScenarioAuthoringWorkspaceDocumentViewModel document, GameplayScheduleCommandKind kind, int index)
        {
            for (int i = 0; document != null && document.Sections != null && i < document.Sections.Length; i++)
                for (int j = 0; document.Sections[i] != null && document.Sections[i].Items != null && j < document.Sections[i].Items.Length; j++)
                    if (IsCommand(document.Sections[i].Items[j] != null ? document.Sections[i].Items[j].Action : null, kind, index)) return true;
            return false;
        }

        private static bool IsCommand(ScenarioAuthoringInspectorAction action, GameplayScheduleCommandKind kind, int index)
        {
            GameplayScheduleCommand command = action != null ? action.Command as GameplayScheduleCommand : null;
            return command != null && command.Kind == kind && command.Index == index;
        }

        private static bool ContainsAction(ScenarioAuthoringWorkspaceDocumentViewModel document, string actionId)
        {
            for (int i = 0; document != null && document.Sections != null && i < document.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = document.Sections[i];
                for (int j = 0; section != null && section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    if (item != null && item.Action != null && string.Equals(item.Action.Id, actionId, StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        private static bool IsAdvancedLast(ScenarioAuthoringInspectorSection[] sections)
        {
            if (sections == null || sections.Length == 0 || sections[sections.Length - 1] == null || !sections[sections.Length - 1].IsAdvanced)
                return false;
            for (int i = 0; i < sections.Length - 1; i++)
                if (sections[i] != null && sections[i].IsAdvanced) return false;
            return true;
        }

        private static bool HasChip(ScenarioAuthoringStatusChipViewModel[] chips, string text)
        {
            for (int i = 0; chips != null && i < chips.Length; i++)
                if (chips[i] != null && string.Equals(chips[i].Text, text, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void AssertUniqueActions(
            ScenarioAuthoringWorkspaceViewModel workspace,
            string documentName,
            ScenarioValidationResult result)
        {
            try
            {
                ScenarioAuthoringRendererActionManifest.BuildContractWindow(new ScenarioAuthoringShellViewModel
                {
                    Windows = new[]
                    {
                        new ScenarioAuthoringShellWindowViewModel
                        {
                            Id = "supplies.verification",
                            WorkspaceBody = workspace,
                            Sections = new ScenarioAuthoringInspectorSection[0]
                        }
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                Assert(false, "Supplies " + documentName + " emitted duplicate semantic actions: " + ex.Message, result);
            }
        }

        private static bool Contains(string text, string value)
        {
            return !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(value)
                && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition && result != null) result.AddError(message);
        }
    }
}
