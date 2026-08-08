using System;
using System.Collections.Generic;
using System.Globalization;

using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Authoring.Supplies;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredScenarioEditor.Infrastructure.Unity;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    internal static class ScenarioSuppliesWorkspaceActions
    {
        public const string WorkspaceId = "supplies";
        public const string SubtabId = "supplies";
        public const string StartingItems = "starting-items";
        public const string Presets = "presets";
        public const string Balance = "balance";
        public const string Scheduled = "scheduled";
        public const string LiveReference = "live-reference";
        private const string PresetPrefix = "preset.";

        public static string PresetEntityId(int index)
        {
            ScenarioSuppliesPresetCatalog.PresetInfo preset = ScenarioSuppliesPresetCatalog.ByIndex(index);
            string id = preset != null ? preset.Id : index.ToString(CultureInfo.InvariantCulture);
            return PresetPrefix + ScenarioAutomationIdCodec.EncodeToken(id);
        }

        public static bool TryResolvePreset(string entityId, out int index)
        {
            index = -1;
            for (int i = 0; i < ScenarioSuppliesPresetCatalog.Count; i++)
            {
                if (string.Equals(PresetEntityId(i), entityId, StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        public static bool IsRoot(string entityId)
        {
            return string.Equals(entityId, StartingItems, StringComparison.Ordinal)
                || string.Equals(entityId, Presets, StringComparison.Ordinal)
                || string.Equals(entityId, Balance, StringComparison.Ordinal)
                || string.Equals(entityId, Scheduled, StringComparison.Ordinal)
                || string.Equals(entityId, LiveReference, StringComparison.Ordinal);
        }

        public static void SelectPresetDocument(int index, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            rendererInteraction.SetWorkspaceSubtab(WorkspaceId, SubtabId);
            rendererInteraction.SetWorkspaceSelection(WorkspaceId, SubtabId, PresetEntityId(index));
            rendererInteraction.SetWorkspaceNarrowPane(WorkspaceId, SubtabId, true);
        }
    }

    /// <summary>
    /// Projects Supplies into the cached Navigator + Document workspace. This is called
    /// only from shell presentation composition, never from OnGUI or an idle-frame hook.
    /// </summary>
    internal sealed class ScenarioSuppliesWorkspaceViewModelBuilder
    {
        private readonly ScenarioAuthoringWorkspaceViewModelFactory _factory;

        public ScenarioSuppliesWorkspaceViewModelBuilder()
        {
            _factory = new ScenarioAuthoringWorkspaceViewModelFactory();
        }

        public ScenarioAuthoringWorkspaceViewModel Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioDefinition definition = context != null ? context.Definition : null;
            StartingInventoryDefinition inventory = definition != null ? definition.StartingInventory : null;
            ScenarioAuthoringRendererInteractionState rendererState = context.RendererInteraction;
            string selected = rendererState.GetWorkspaceSelection(
                ScenarioSuppliesWorkspaceActions.WorkspaceId,
                ScenarioSuppliesWorkspaceActions.SubtabId);
            int presetIndex;
            if (!ScenarioSuppliesWorkspaceActions.IsRoot(selected)
                && !ScenarioSuppliesWorkspaceActions.TryResolvePreset(selected, out presetIndex))
            {
                selected = ScenarioSuppliesWorkspaceActions.StartingItems;
                rendererState.SetWorkspaceSelection(
                    ScenarioSuppliesWorkspaceActions.WorkspaceId,
                    ScenarioSuppliesWorkspaceActions.SubtabId,
                    selected);
            }

            ScenarioAuthoringWorkspaceViewModel workspace = _factory.CreateWorkspace(
                ScenarioSuppliesWorkspaceActions.WorkspaceId,
                ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument,
                ScenarioSuppliesWorkspaceActions.SubtabId);
            workspace.Navigator = BuildNavigator(definition, inventory, selected, rendererState);
            workspace.Document = BuildDocument(definition, inventory, selected);
            return workspace;
        }

        private ScenarioAuthoringNavigatorViewModel BuildNavigator(
            ScenarioDefinition definition,
            StartingInventoryDefinition inventory,
            string selected,
            ScenarioAuthoringRendererInteractionState state)
        {
            ScenarioAuthoringNavigatorViewModel navigator = _factory.CreateNavigator("supplies.navigator");
            navigator.SelectedEntityId = selected;
            navigator.EmptyMessage = "Supplies documents are always available.";
            navigator.Groups = new[]
            {
                new ScenarioAuthoringNavigatorGroupViewModel
                {
                    Id = "supplies-documents",
                    Label = "Supplies",
                    IconText = "SU",
                    Expanded = state.GetWorkspaceExpanded(
                        ScenarioSuppliesWorkspaceActions.WorkspaceId,
                        ScenarioSuppliesWorkspaceActions.SubtabId,
                        "supplies-documents",
                        true),
                    ToggleAction = _factory.CreateGroupToggleAction(
                        ScenarioSuppliesWorkspaceActions.WorkspaceId,
                        ScenarioSuppliesWorkspaceActions.SubtabId,
                        "supplies-documents",
                        "Toggle Supplies"),
                    StatusChips = new[] { Chip("supplies.group.status", "Authoring", ScenarioAuthoringStatusTone.Informational) },
                    Rows = new[]
                    {
                        RootRow(ScenarioSuppliesWorkspaceActions.StartingItems, "Starting Items", "Authored shelter loadout", "ST", StartingStatus(inventory, "supplies.starting.nav.status"), selected, null, false, null),
                        RootRow(
                            ScenarioSuppliesWorkspaceActions.Presets,
                            "Presets",
                            "Review and apply a starter loadout",
                            "PR",
                            Chip("supplies.presets.status", "Choose a loadout", ScenarioAuthoringStatusTone.Informational),
                            selected,
                            BuildPresetRows(selected),
                            state.GetWorkspaceExpanded(ScenarioSuppliesWorkspaceActions.WorkspaceId, ScenarioSuppliesWorkspaceActions.SubtabId, ScenarioSuppliesWorkspaceActions.Presets, true),
                            _factory.CreateRowToggleAction(ScenarioSuppliesWorkspaceActions.WorkspaceId, ScenarioSuppliesWorkspaceActions.SubtabId, ScenarioSuppliesWorkspaceActions.Presets, "Toggle Presets")),
                        RootRow(ScenarioSuppliesWorkspaceActions.Balance, "Balance", "Approximate opening-day coverage", "BA", BalanceStatus(definition, inventory, "supplies.balance.nav.status"), selected, null, false, null),
                        RootRow(ScenarioSuppliesWorkspaceActions.Scheduled, "Scheduled", "Timed stockpile additions and removals", "SC", ScheduledStatus(inventory, "supplies.scheduled.nav.status"), selected, null, false, null),
                        RootRow(ScenarioSuppliesWorkspaceActions.LiveReference, "Live Reference", "Read-only shelter snapshot", "LR", Chip("supplies.live.status", "Read-only", ScenarioAuthoringStatusTone.Neutral), selected, null, false, null)
                    }
                }
            };
            return navigator;
        }

        private ScenarioAuthoringNavigatorRowViewModel RootRow(
            string entityId,
            string title,
            string subtitle,
            string icon,
            ScenarioAuthoringStatusChipViewModel status,
            string selected,
            ScenarioAuthoringNavigatorRowViewModel[] children,
            bool expanded,
            ScenarioAuthoringInspectorAction toggleAction)
        {
            return new ScenarioAuthoringNavigatorRowViewModel
            {
                EntityId = entityId,
                Title = title,
                Subtitle = subtitle,
                IconText = icon,
                Selected = string.Equals(selected, entityId, StringComparison.Ordinal),
                Expanded = expanded,
                StatusChips = new[] { status },
                SelectAction = _factory.CreateEntityAction(
                    ScenarioSuppliesWorkspaceActions.WorkspaceId,
                    ScenarioSuppliesWorkspaceActions.SubtabId,
                    entityId,
                    "Select " + title),
                ToggleAction = toggleAction,
                Children = children ?? new ScenarioAuthoringNavigatorRowViewModel[0]
            };
        }

        private static ScenarioAuthoringNavigatorRowViewModel[] BuildPresetRows(string selected)
        {
            List<ScenarioAuthoringNavigatorRowViewModel> rows = new List<ScenarioAuthoringNavigatorRowViewModel>();
            ScenarioSuppliesPresetCatalog.PresetInfo[] presets = ScenarioSuppliesPresetCatalog.All();
            for (int i = 0; i < presets.Length; i++)
            {
                ScenarioSuppliesPresetCatalog.PresetInfo preset = presets[i];
                if (preset == null)
                    continue;
                string entity = ScenarioSuppliesWorkspaceActions.PresetEntityId(i);
                rows.Add(new ScenarioAuthoringNavigatorRowViewModel
                {
                    EntityId = entity,
                    Title = preset.DisplayName,
                    Subtitle = preset.Description,
                    IconText = "LO",
                    Selected = string.Equals(selected, entity, StringComparison.Ordinal),
                    StatusChips = new[] { Chip("supplies.preset.status." + i.ToString(CultureInfo.InvariantCulture), "Ready to review", ScenarioAuthoringStatusTone.Ready) },
                    SelectAction = Action(
                        GameplayScheduleCommands.PreviewSuppliesPreset(i),
                        "Select " + preset.DisplayName,
                        "Open this preset in the Supplies document pane.",
                        true,
                        false,
                        "LO"),
                    Children = new ScenarioAuthoringNavigatorRowViewModel[0]
                });
            }
            return rows.ToArray();
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildDocument(
            ScenarioDefinition definition,
            StartingInventoryDefinition inventory,
            string selected)
        {
            int presetIndex;
            if (ScenarioSuppliesWorkspaceActions.TryResolvePreset(selected, out presetIndex))
                return BuildPresetDocument(definition, presetIndex);
            if (string.Equals(selected, ScenarioSuppliesWorkspaceActions.Presets, StringComparison.Ordinal))
                return BuildPresetsDocument();
            if (string.Equals(selected, ScenarioSuppliesWorkspaceActions.Balance, StringComparison.Ordinal))
                return BuildBalanceDocument(definition, inventory);
            if (string.Equals(selected, ScenarioSuppliesWorkspaceActions.Scheduled, StringComparison.Ordinal))
                return BuildScheduledDocument(inventory);
            if (string.Equals(selected, ScenarioSuppliesWorkspaceActions.LiveReference, StringComparison.Ordinal))
                return BuildLiveReferenceDocument(inventory);
            return BuildStartingItemsDocument(inventory);
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildStartingItemsDocument(StartingInventoryDefinition inventory)
        {
            ScenarioAuthoringWorkspaceDocumentViewModel document = CreateDocument(
                "supplies.starting",
                "Supply Setup",
                "Starting Items",
                "Author the exact item stacks available when the scenario begins.",
                StartingStatus(inventory, "supplies.starting.document.status"));
            document.Sections = new[]
            {
                ScenarioSuppliesAuthoringContentBuilder.BuildStartingItemsSection(inventory),
                BuildStartingAdvancedSection(inventory)
            };
            return document;
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildPresetsDocument()
        {
            ScenarioAuthoringWorkspaceDocumentViewModel document = CreateDocument(
                "supplies.presets",
                "Supply Setup",
                "Presets",
                "Choose a preset beneath Presets in the navigator to review it inline.",
                Chip("supplies.presets.document.status", "No changes until applied", ScenarioAuthoringStatusTone.Informational));
            document.Sections = new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "supplies_presets_guide",
                    Title = "STARTER LOADOUTS",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[]
                    {
                        Text("Preset documents show every resulting stack and the estimated food and water coverage before you apply them."),
                        Text("Applying a preset replaces Starting Items immediately and can be reversed with Undo.")
                    }
                },
                BuildPresetCatalogAdvancedSection()
            };
            return document;
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildPresetDocument(ScenarioDefinition definition, int presetIndex)
        {
            ScenarioSuppliesPresetCatalog.PresetInfo preset = ScenarioSuppliesPresetCatalog.ByIndex(presetIndex);
            ScenarioAuthoringWorkspaceDocumentViewModel document = CreateDocument(
                "supplies.preset." + presetIndex.ToString(CultureInfo.InvariantCulture),
                "Presets",
                preset != null ? preset.DisplayName + " Loadout" : "Preset",
                preset != null ? preset.Description : "Review this loadout before applying.",
                Chip("supplies.preset.document.status", "Ready to apply", ScenarioAuthoringStatusTone.Ready));
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            ScenarioAuthoringInspectorSection[] body = ScenarioSuppliesAuthoringContentBuilder.BuildPresetDocumentSections(definition, presetIndex);
            for (int i = 0; body != null && i < body.Length; i++)
                if (body[i] != null) sections.Add(body[i]);
            sections.Add(BuildPresetAdvancedSection(preset, presetIndex));
            document.Sections = sections.ToArray();
            return document;
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildBalanceDocument(
            ScenarioDefinition definition,
            StartingInventoryDefinition inventory)
        {
            ScenarioAuthoringWorkspaceDocumentViewModel document = CreateDocument(
                "supplies.balance",
                "Supply Setup",
                "Balance",
                "Review approximate food, water, and medical coverage for the starting cast.",
                BalanceStatus(definition, inventory, "supplies.balance.document.status"));
            document.Sections = new[]
            {
                ScenarioSuppliesAuthoringContentBuilder.BuildBalanceSection(definition, inventory),
                BuildBalanceAdvancedSection(definition, inventory)
            };
            return document;
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildScheduledDocument(StartingInventoryDefinition inventory)
        {
            ScenarioAuthoringWorkspaceDocumentViewModel document = CreateDocument(
                "supplies.scheduled",
                "Supply Setup",
                "Scheduled",
                "Add or remove stockpile items at authored scenario times.",
                ScheduledStatus(inventory, "supplies.scheduled.document.status"));
            document.Sections = new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scheduled_stockpile",
                    Title = "Timed Item Changes",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.InventorySlotGrid,
                    Items = new[] { Text("Use the add and remove slots in the grid. Choosing an item stays in the bounded picker modal.") },
                    InventorySlotGrid = BuildScheduledInventorySlotGrid(inventory)
                },
                BuildScheduledAdvancedSection(inventory)
            };
            return document;
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildLiveReferenceDocument(StartingInventoryDefinition inventory)
        {
            StorageSummary summary = BuildStorageSummary(inventory);
            ScenarioAuthoringInspectorAction openStorageAction = Item.Action(
                new StorageAuthoringCommand(),
                "Open Shelter Storage",
                "Open Sheltered's separate storage window. Changes made there synchronize back to the draft.",
                true,
                false,
                "ST",
                "Separate live storage window",
                "VANILLA");
            ScenarioAuthoringWorkspaceDocumentViewModel document = CreateDocument(
                "supplies.live-reference",
                "Supply Setup",
                "Live Reference",
                "READ-ONLY: this document reports the current shelter inventory and does not edit stacks.",
                Chip("supplies.live.document.status", "Read-only", ScenarioAuthoringStatusTone.Neutral));
            document.Sections = new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "live_shelter_reference",
                    Title = "READ-ONLY LIVE SNAPSHOT",
                    Expanded = false,
                    Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                    Items = new[]
                    {
                        Text("This workspace document is a reference. Use Starting Items or Scheduled to author supplies."),
                        Property("Total Items", summary.TotalItems.ToString(CultureInfo.InvariantCulture)),
                        Property("Slots Used", summary.SlotsUsed.ToString(CultureInfo.InvariantCulture) + " / " + FormatStorageCapacity(summary)),
                        ActionItem(openStorageAction)
                    }
                },
                BuildLiveAdvancedSection(summary)
            };
            return document;
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel CreateDocument(
            string id,
            string groupLabel,
            string title,
            string subtitle,
            ScenarioAuthoringStatusChipViewModel status)
        {
            ScenarioAuthoringWorkspaceDocumentViewModel document = _factory.CreateDocument(id, title);
            document.Subtitle = subtitle;
            document.BackAction = _factory.CreateBackAction(
                ScenarioSuppliesWorkspaceActions.WorkspaceId,
                ScenarioSuppliesWorkspaceActions.SubtabId,
                "Back to Navigator");
            document.Breadcrumbs = new[]
            {
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Supplies" },
                new ScenarioAuthoringBreadcrumbViewModel { Label = groupLabel },
                new ScenarioAuthoringBreadcrumbViewModel { Label = title }
            };
            document.StatusChips = new[] { status };
            return document;
        }

        private static ScenarioAuthoringInspectorSection BuildStartingAdvancedSection(StartingInventoryDefinition inventory)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Override random start", inventory != null && inventory.OverrideRandomStart ? "Enabled" : "Disabled"));
            for (int i = 0; inventory != null && inventory.Items != null && i < inventory.Items.Count; i++)
            {
                ItemEntry entry = inventory.Items[i];
                if (entry != null)
                    items.Add(Property("Stack " + (i + 1).ToString(CultureInfo.InvariantCulture) + " item ID", (entry.ItemId ?? string.Empty) + "; quantity " + entry.Quantity.ToString(CultureInfo.InvariantCulture)));
            }
            return Advanced("supplies_starting_advanced", items.ToArray());
        }

        private static ScenarioAuthoringInspectorSection BuildPresetCatalogAdvancedSection()
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioSuppliesPresetCatalog.PresetInfo[] presets = ScenarioSuppliesPresetCatalog.All();
            for (int i = 0; i < presets.Length; i++)
                if (presets[i] != null) items.Add(Property("Preset " + (i + 1).ToString(CultureInfo.InvariantCulture) + " ID", presets[i].Id));
            return Advanced("supplies_presets_advanced", items.ToArray());
        }

        private static ScenarioAuthoringInspectorSection BuildPresetAdvancedSection(
            ScenarioSuppliesPresetCatalog.PresetInfo preset,
            int presetIndex)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Preset ID", preset != null ? preset.Id : presetIndex.ToString(CultureInfo.InvariantCulture)));
            List<ItemEntry> stacks = ScenarioSuppliesPresetCatalog.BuildStacks(preset);
            for (int i = 0; i < stacks.Count; i++)
                items.Add(Property("Stack " + (i + 1).ToString(CultureInfo.InvariantCulture) + " item ID", (stacks[i].ItemId ?? string.Empty) + "; quantity " + stacks[i].Quantity.ToString(CultureInfo.InvariantCulture)));
            return Advanced("supplies_preset_advanced", items.ToArray());
        }

        private static ScenarioAuthoringInspectorSection BuildBalanceAdvancedSection(
            ScenarioDefinition definition,
            StartingInventoryDefinition inventory)
        {
            int survivorCount = ScenarioAuthoringPresentationUtilities.CountFamilyMembers(definition);
            ScenarioSuppliesBalanceEstimator.BalanceEstimate estimate = ScenarioSuppliesBalanceEstimator.Estimate(
                inventory,
                survivorCount > 0 ? survivorCount : ScenarioSuppliesBalanceEstimator.DefaultSurvivorCount);
            return Advanced("supplies_balance_advanced", new[]
            {
                Property("Estimated survivor count", estimate.SurvivorCount.ToString(CultureInfo.InvariantCulture)),
                Property("Water units", estimate.WaterUnits.ToString(CultureInfo.InvariantCulture)),
                Property("Food units", estimate.FoodUnits.ToString(CultureInfo.InvariantCulture)),
                Property("Medicine units", estimate.MedicineUnits.ToString(CultureInfo.InvariantCulture))
            });
        }

        private static ScenarioAuthoringInspectorSection BuildScheduledAdvancedSection(StartingInventoryDefinition inventory)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; inventory != null && inventory.ScheduledChanges != null && i < inventory.ScheduledChanges.Count; i++)
            {
                TimedInventoryChangeDefinition change = inventory.ScheduledChanges[i];
                if (change == null)
                    continue;
                items.Add(Property(
                    "Change " + (i + 1).ToString(CultureInfo.InvariantCulture),
                    "ID " + (change.Id ?? string.Empty) + "; item ID " + (change.ItemId ?? string.Empty)
                    + "; " + change.Kind + " x" + change.Quantity.ToString(CultureInfo.InvariantCulture)
                    + "; " + ScenarioScheduleFormatter.Format(change.When)));
            }
            return Advanced("supplies_scheduled_advanced", items.ToArray());
        }

        private static ScenarioAuthoringInspectorSection BuildLiveAdvancedSection(StorageSummary summary)
        {
            return Advanced("supplies_live_advanced", new[]
            {
                Property("Projection source", summary != null && summary.Live ? "InventoryManager" : "Draft fallback"),
                Property("Storage capacity", FormatStorageCapacity(summary))
            });
        }

        private static ScenarioAuthoringInspectorSection Advanced(string id, ScenarioAuthoringInspectorItem[] items)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = "ADVANCED",
                Expanded = true,
                IsAdvanced = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items ?? new ScenarioAuthoringInspectorItem[0]
            };
        }

        private static ScenarioInventorySlotGridViewModel BuildScheduledInventorySlotGrid(StartingInventoryDefinition inventory)
        {
            List<ScenarioInventorySlotViewModel> slots = new List<ScenarioInventorySlotViewModel>();
            for (int i = 0; inventory != null && inventory.ScheduledChanges != null && i < inventory.ScheduledChanges.Count; i++)
            {
                TimedInventoryChangeDefinition change = inventory.ScheduledChanges[i];
                if (change == null)
                    continue;

                int index = i;
                ScenarioInventoryItemCatalogEntry catalogEntry = ScenarioInventoryItemCatalog.Resolve(change.ItemId);
                string displayName = ResolveItemDisplayName(catalogEntry, i);
                bool add = change.Kind == ScenarioInventoryChangeKind.Add;
                slots.Add(new ScenarioInventorySlotViewModel
                {
                    Id = "timed." + index.ToString(CultureInfo.InvariantCulture),
                    ItemId = catalogEntry.ItemId,
                    DisplayName = displayName,
                    Detail = FormatItemDetail(catalogEntry),
                    QuantityText = "x" + Math.Max(1, change.Quantity).ToString(CultureInfo.InvariantCulture),
                    Badge = add ? "TIMED +" : "TIMED -",
                    ScheduleText = ScenarioScheduleFormatter.Format(change.When),
                    Emphasized = add,
                    PreviewSprite = catalogEntry.PreviewSprite,
                    PrimaryAction = Item.Action(
                        GameplayScheduleCommands.OpenTimedPicker(index),
                        "Choose " + displayName,
                        "Open the searchable stockpile item picker for this timed change.",
                        true,
                        add,
                        "IT",
                        FormatItemDetail(catalogEntry)),
                    QuantityIncreaseAction = Action(GameplayScheduleCommands.StepTimedQuantity(index, 1), "+", "Increase this timed change quantity by one.", true, false, "+"),
                    QuantityDecreaseAction = Action(GameplayScheduleCommands.StepTimedQuantity(index, -1), "-", "Decrease this timed change quantity by one.", true, false, "-"),
                    RemoveAction = Action(GameplayScheduleCommands.DeleteTimedItem(index), "Remove", "Remove this timed stockpile change.", true, false, "RM"),
                    KindAction = Action(GameplayScheduleCommands.ToggleTimedKind(index), add ? "Add" : "Remove", "Switch this timed change between adding and removing items.", true, add, add ? "A+" : "R-"),
                    TimeActions = new[]
                    {
                        Action(GameplayScheduleCommands.StepTimedDay(index, 1), "D+", "Move this timed change one day later.", true, false, "D+"),
                        Action(GameplayScheduleCommands.StepTimedDay(index, -1), "D-", "Move this timed change one day earlier.", true, false, "D-"),
                        Action(GameplayScheduleCommands.StepTimedHour(index, 1), "H+", "Move this timed change one hour later.", true, false, "H+"),
                        Action(GameplayScheduleCommands.StepTimedHour(index, -1), "H-", "Move this timed change one hour earlier.", true, false, "H-"),
                        Action(GameplayScheduleCommands.StepTimedMinute(index, 15), "M+", "Move this timed change fifteen minutes later.", true, false, "M+"),
                        Action(GameplayScheduleCommands.StepTimedMinute(index, -15), "M-", "Move this timed change fifteen minutes earlier.", true, false, "M-")
                    }
                });
            }

            AddEmptyInventorySlots(
                slots,
                1,
                Action(GameplayScheduleCommands.AddTimedItemAndPick(false), "Schedule Add", "Add a timed item delivery, then choose its item.", true, true, "A+"),
                "TIMED +",
                "Click to schedule an item delivery.");
            AddEmptyInventorySlots(
                slots,
                1,
                Action(GameplayScheduleCommands.AddTimedItemAndPick(true), "Schedule Remove", "Add a timed item removal, then choose its item.", true, false, "R-"),
                "TIMED -",
                "Click to schedule an item removal.");
            int remainder = slots.Count % 6;
            AddEmptyInventorySlots(slots, remainder == 0 ? 0 : 6 - remainder, null, "Empty", "No timed change in this slot.");
            return new ScenarioInventorySlotGridViewModel
            {
                EmptyMessage = "No timed stockpile changes have been authored yet.",
                Slots = slots.ToArray()
            };
        }

        private static void AddEmptyInventorySlots(
            List<ScenarioInventorySlotViewModel> slots,
            int count,
            ScenarioAuthoringInspectorAction action,
            string badge,
            string detail)
        {
            if (slots == null)
                return;
            for (int i = 0; i < count; i++)
            {
                slots.Add(new ScenarioInventorySlotViewModel
                {
                    Id = "empty." + slots.Count.ToString(CultureInfo.InvariantCulture),
                    Empty = true,
                    Badge = badge,
                    DisplayName = action != null ? action.Label : "Empty",
                    Detail = detail,
                    PrimaryAction = action
                });
            }
        }

        private static StorageSummary BuildStorageSummary(StartingInventoryDefinition inventory)
        {
            StorageSummary summary = new StorageSummary();
            InventoryManager manager = InventoryManager.Instance;
            if (manager != null)
            {
                List<ItemStack> stacks = manager.GetItems();
                for (int i = 0; stacks != null && i < stacks.Count; i++)
                {
                    ItemStack stack = stacks[i];
                    if (stack != null && stack.m_count > 0 && stack.m_type != ItemManager.ItemType.Undefined)
                        summary.TotalItems += stack.m_count;
                }
                summary.SlotsUsed = Math.Max(0, manager.GetTotalStackCount());
                summary.StorageCapacity = Math.Max(0, manager.storageCapacity);
                summary.Live = true;
                return summary;
            }

            for (int i = 0; inventory != null && inventory.Items != null && i < inventory.Items.Count; i++)
            {
                ItemEntry entry = inventory.Items[i];
                if (entry == null || entry.Quantity <= 0)
                    continue;
                summary.TotalItems += entry.Quantity;
                summary.SlotsUsed++;
            }
            summary.StorageCapacity = -1;
            return summary;
        }

        private static string FormatStorageCapacity(StorageSummary summary)
        {
            return summary != null && summary.StorageCapacity >= 0
                ? summary.StorageCapacity.ToString(CultureInfo.InvariantCulture)
                : "Unavailable";
        }

        private static ScenarioAuthoringStatusChipViewModel StartingStatus(StartingInventoryDefinition inventory, string id)
        {
            bool any = inventory != null && inventory.Items != null && inventory.Items.Count > 0;
            bool needsMerge = any && ScenarioSuppliesInventoryNormalizer.NeedsNormalize(inventory.Items);
            return needsMerge
                ? Chip(id, "Needs cleanup", ScenarioAuthoringStatusTone.Warning)
                : Chip(id, any ? "Authored" : "Empty", any ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Warning);
        }

        private static ScenarioAuthoringStatusChipViewModel BalanceStatus(
            ScenarioDefinition definition,
            StartingInventoryDefinition inventory,
            string id)
        {
            int survivorCount = ScenarioAuthoringPresentationUtilities.CountFamilyMembers(definition);
            ScenarioSuppliesBalanceEstimator.BalanceEstimate estimate = ScenarioSuppliesBalanceEstimator.Estimate(
                inventory,
                survivorCount > 0 ? survivorCount : ScenarioSuppliesBalanceEstimator.DefaultSurvivorCount);
            bool ready = estimate.MissingEssentials == null || estimate.MissingEssentials.Count == 0;
            return Chip(id, ready ? "Essentials stocked" : "Needs essentials", ready ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Warning);
        }

        private static ScenarioAuthoringStatusChipViewModel ScheduledStatus(StartingInventoryDefinition inventory, string id)
        {
            bool any = inventory != null && inventory.ScheduledChanges != null && inventory.ScheduledChanges.Count > 0;
            return Chip(id, any ? "Scheduled" : "Optional", any ? ScenarioAuthoringStatusTone.Informational : ScenarioAuthoringStatusTone.Neutral);
        }

        private static ScenarioAuthoringStatusChipViewModel Chip(string id, string text, ScenarioAuthoringStatusTone tone)
        {
            return new ScenarioAuthoringStatusChipViewModel { Id = id, Text = text, Tone = tone };
        }

        private static string ResolveItemDisplayName(ScenarioInventoryItemCatalogEntry entry, int index)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                entry != null ? entry.DisplayName : null,
                null,
                entry != null ? entry.ItemId : null,
                "Item " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        private static string FormatItemDetail(ScenarioInventoryItemCatalogEntry entry)
        {
            return entry != null && entry.Category != ItemManager.ItemCategory.Undefined
                ? entry.Category.ToString() + " supply"
                : "Supply item";
        }

        private static ScenarioAuthoringInspectorAction Action(
            string id,
            string label,
            string tooltip,
            bool enabled,
            bool emphasized,
            string iconText)
        {
            return Item.Action(id, label, tooltip, enabled, emphasized, iconText);
        }

        private static ScenarioAuthoringInspectorAction Action(
            ScenarioAuthoringCommand command,
            string label,
            string tooltip,
            bool enabled,
            bool emphasized,
            string iconText)
        {
            return Item.Action(command, label, tooltip, enabled, emphasized, iconText);
        }

        private static ScenarioAuthoringInspectorAction Action(
            string id,
            string label,
            string tooltip,
            bool enabled,
            bool emphasized,
            string iconText,
            string detail)
        {
            return Item.Action(id, label, tooltip, enabled, emphasized, iconText, detail);
        }

        private static ScenarioAuthoringInspectorAction Action(
            string id,
            string label,
            string tooltip,
            bool enabled,
            bool emphasized,
            string iconText,
            string detail,
            string badge)
        {
            return Item.Action(id, label, tooltip, enabled, emphasized, iconText, detail, badge);
        }

        private static ScenarioAuthoringInspectorItem Text(string value)
        {
            return Item.Text(value);
        }

        private static ScenarioAuthoringInspectorItem Property(string label, string value)
        {
            return Item.Property(label, value);
        }

        private static ScenarioAuthoringInspectorItem ActionItem(ScenarioAuthoringInspectorAction action)
        {
            return Item.ActionItem(action);
        }

        private sealed class StorageSummary
        {
            public int TotalItems;
            public int SlotsUsed;
            public int StorageCapacity;
            public bool Live;
        }
    }
}
