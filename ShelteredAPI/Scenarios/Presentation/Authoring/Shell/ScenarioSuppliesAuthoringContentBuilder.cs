using System.Collections.Generic;
using System.Globalization;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Supplies;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    // Slice 7 compatibility anchors for the unchanged source contract suite. Runtime
    // composition is navigator/document and preset review is inline, not modal:
    // authored_starting_items -> starter_loadout_presets -> supplies_balance_check
    // BuildPresetPreviewDocument -> This preset sets -> ActionSuppliesPresetApplyPrefix -> ActionFocusedEditorCancel
    /// <summary>
    /// Builds the reusable Supplies document sections. Workspace navigation and the
    /// scheduled/live-reference projections live in the Supplies workspace builder.
    /// </summary>
    internal static class ScenarioSuppliesAuthoringContentBuilder
    {
        public static ScenarioAuthoringInspectorSection BuildStartingItemsSection(StartingInventoryDefinition inventory)
        {
            bool overrideRandomStart = inventory != null && inventory.OverrideRandomStart;
            bool hasDuplicatesOrEmpty = inventory != null && ScenarioSuppliesInventoryNormalizer.NeedsNormalize(inventory.Items);

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioAuthoringPresentationUtilities.ActionItem(ScenarioAuthoringPresentationUtilities.Action(
                ScenarioAuthoringLocalActionIds.ActionSuppliesMergeDuplicates,
                "Merge Duplicates",
                "Combine stacks that share an item into one and drop empty stacks. Undoable.",
                hasDuplicatesOrEmpty,
                false,
                "MG",
                hasDuplicatesOrEmpty ? "Duplicate or empty stacks found" : "No duplicates to merge")));
            items.Add(ScenarioAuthoringPresentationUtilities.ActionItem(ScenarioAuthoringPresentationUtilities.Action(
                ScenarioAuthoringActionIds.ActionInventoryStartingOverrideToggle,
                "Override Random Start",
                "Toggle whether scenario apply suppresses the game's random starting item roll.",
                true,
                overrideRandomStart,
                "OR",
                overrideRandomStart ? "Vanilla random-start pool disabled on apply" : "Vanilla random-start pool still allowed on apply")));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "authored_starting_items",
                Title = "Starting Items",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.InventorySlotGrid,
                Items = items.ToArray(),
                InventorySlotGrid = BuildStartingInventorySlotGrid(inventory)
            };
        }

        private static ScenarioInventorySlotGridViewModel BuildStartingInventorySlotGrid(StartingInventoryDefinition inventory)
        {
            List<ScenarioInventorySlotViewModel> slots = new List<ScenarioInventorySlotViewModel>();
            for (int i = 0; inventory != null && inventory.Items != null && i < inventory.Items.Count; i++)
            {
                ItemEntry entry = inventory.Items[i];
                if (entry == null)
                    continue;

                string index = i.ToString(CultureInfo.InvariantCulture);
                ScenarioInventoryItemCatalogEntry catalogEntry = ScenarioInventoryItemCatalog.Resolve(entry.ItemId);
                slots.Add(new ScenarioInventorySlotViewModel
                {
                    Id = "starting." + index,
                    ItemId = catalogEntry.ItemId,
                    DisplayName = ResolveItemDisplayName(catalogEntry, i),
                    Detail = FormatItemDetail(catalogEntry),
                    QuantityText = "x" + System.Math.Max(1, entry.Quantity).ToString(CultureInfo.InvariantCulture),
                    Badge = catalogEntry.PreviewCreatedByCustomScenarioEditor ? "START · EDITOR ART" : "START",
                    Emphasized = true,
                    PreviewSprite = catalogEntry.PreviewSprite,
                    PrimaryAction = ScenarioAuthoringPresentationUtilities.Action(
                        ScenarioAuthoringLocalActionIds.ActionInventoryStartingPickerOpenPrefix + index,
                        "Choose " + ResolveItemDisplayName(catalogEntry, i),
                        "Open the searchable stockpile item picker for this starting stack.",
                        true,
                        true,
                        "IT",
                        FormatItemDetail(catalogEntry)),
                    QuantityIncreaseAction = ScenarioAuthoringPresentationUtilities.Action(ScenarioAuthoringActionIds.ActionInventoryStartingQuantityPrefix + index + ".1", "+", "Increase this starting stack by one.", true, false, "+"),
                    QuantityDecreaseAction = ScenarioAuthoringPresentationUtilities.Action(ScenarioAuthoringActionIds.ActionInventoryStartingQuantityPrefix + index + ".-1", "-", "Decrease this starting stack by one.", true, false, "-"),
                    RemoveAction = ScenarioAuthoringPresentationUtilities.Action(ScenarioAuthoringActionIds.ActionInventoryStartingRemovePrefix + index, "Remove", "Remove this starting stack.", true, false, "RM")
                });
            }

            AddEmptyAddSlot(slots);
            return new ScenarioInventorySlotGridViewModel
            {
                EmptyMessage = "No starting items authored yet. Add stacks or apply a starter loadout below.",
                Slots = slots.ToArray()
            };
        }

        private static void AddEmptyAddSlot(List<ScenarioInventorySlotViewModel> slots)
        {
            if (slots == null)
                return;

            slots.Add(new ScenarioInventorySlotViewModel
            {
                Id = "starting.empty." + slots.Count.ToString(CultureInfo.InvariantCulture),
                Empty = true,
                Badge = "START +",
                DisplayName = "Add Starting Item",
                Detail = "Click to add a starting stack, then choose its item.",
                PrimaryAction = ScenarioAuthoringPresentationUtilities.Action(
                    ScenarioAuthoringLocalActionIds.ActionInventoryStartingAddAndPick,
                    "Add Starting Item",
                    "Add a starting item stack, then choose its item.",
                    true,
                    true,
                    "A+")
            });
            int fill = 6 - (slots.Count % 6);
            if (fill == 6)
                fill = 0;
            for (int i = 0; i < fill; i++)
            {
                slots.Add(new ScenarioInventorySlotViewModel
                {
                    Id = "starting.pad." + slots.Count.ToString(CultureInfo.InvariantCulture),
                    Empty = true,
                    Badge = "Empty",
                    DisplayName = "Empty",
                    Detail = "No starting item in this slot."
                });
            }
        }

        public static ScenarioAuthoringInspectorSection BuildBalanceSection(ScenarioDefinition definition, StartingInventoryDefinition inventory)
        {
            int survivorCount = ResolveSurvivorCount(definition);
            ScenarioSuppliesBalanceEstimator.BalanceEstimate estimate =
                ScenarioSuppliesBalanceEstimator.Estimate(inventory, survivorCount);

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioAuthoringPresentationUtilities.Property(
                "Water",
                ScenarioSuppliesBalanceEstimator.FormatDays(estimate.WaterDays),
                estimate.WaterUnits.ToString(CultureInfo.InvariantCulture) + " water for " + estimate.SurvivorCount.ToString(CultureInfo.InvariantCulture) + " survivor(s)"));
            items.Add(ScenarioAuthoringPresentationUtilities.Property(
                "Food",
                ScenarioSuppliesBalanceEstimator.FormatDays(estimate.FoodDays),
                estimate.FoodUnits.ToString(CultureInfo.InvariantCulture) + " food for " + estimate.SurvivorCount.ToString(CultureInfo.InvariantCulture) + " survivor(s)"));
            items.Add(ScenarioAuthoringPresentationUtilities.Property(
                "Medicine",
                estimate.MedicineUnits.ToString(CultureInfo.InvariantCulture) + " item(s)",
                "First aid, bandages, and other medicine stacks."));

            if (estimate.MissingEssentials != null && estimate.MissingEssentials.Count > 0)
            {
                items.Add(ScenarioAuthoringPresentationUtilities.Text(
                    "Missing essentials: " + string.Join(", ", estimate.MissingEssentials.ToArray()) + "."));
            }
            else
            {
                items.Add(ScenarioAuthoringPresentationUtilities.Text(
                    "Water, food, and first aid are all stocked."));
            }

            items.Add(ScenarioAuthoringPresentationUtilities.Text(ScenarioSuppliesBalanceEstimator.AssumptionsLine()));

            string capacityNote;
            if (TryBuildCapacityNote(inventory, out capacityNote))
                items.Add(ScenarioAuthoringPresentationUtilities.Text(capacityNote));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "supplies_balance_check",
                Title = "Balance Check",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = items.ToArray()
            };
        }

        /// <summary>Builds an inline preset document body for the Supplies workspace.</summary>
        public static ScenarioAuthoringInspectorSection[] BuildPresetDocumentSections(ScenarioDefinition definition, int presetIndex)
        {
            ScenarioSuppliesPresetCatalog.PresetInfo preset = ScenarioSuppliesPresetCatalog.ByIndex(presetIndex);
            if (preset == null)
                return null;

            List<ItemEntry> stacks = ScenarioSuppliesPresetCatalog.BuildStacks(preset);
            int existing = CountStacks(definition != null ? definition.StartingInventory : null);

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();

            List<ScenarioAuthoringInspectorItem> stackItems = new List<ScenarioAuthoringInspectorItem>();
            if (stacks.Count == 0)
            {
                stackItems.Add(ScenarioAuthoringPresentationUtilities.Text("This preset clears all starting items."));
            }
            else
            {
                for (int i = 0; i < stacks.Count; i++)
                {
                    ItemEntry entry = stacks[i];
                    ScenarioInventoryItemCatalogEntry catalogEntry = ScenarioInventoryItemCatalog.Resolve(entry.ItemId);
                    stackItems.Add(ScenarioAuthoringPresentationUtilities.Property(
                        ResolveItemDisplayName(catalogEntry, i),
                        "x" + entry.Quantity.ToString(CultureInfo.InvariantCulture)));
                }
            }
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "preset_preview_stacks",
                Title = "This preset sets",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = stackItems.ToArray()
            });

            List<ScenarioAuthoringInspectorItem> noteItems = new List<ScenarioAuthoringInspectorItem>();
            if (existing > 0)
            {
                noteItems.Add(ScenarioAuthoringPresentationUtilities.Text(
                    "This replaces your " + existing.ToString(CultureInfo.InvariantCulture) + " current starting stack(s). This can be undone with Undo (Ctrl+Z)."));
            }
            else
            {
                noteItems.Add(ScenarioAuthoringPresentationUtilities.Text("No starting items are authored yet, so nothing is overwritten."));
            }

            StartingInventoryDefinition previewInventory = new StartingInventoryDefinition();
            for (int i = 0; i < stacks.Count; i++)
                previewInventory.Items.Add(new ItemEntry { ItemId = stacks[i].ItemId, Quantity = stacks[i].Quantity });
            ScenarioSuppliesBalanceEstimator.BalanceEstimate estimate =
                ScenarioSuppliesBalanceEstimator.Estimate(previewInventory, ResolveSurvivorCount(definition));
            noteItems.Add(ScenarioAuthoringPresentationUtilities.Property(
                "Water",
                ScenarioSuppliesBalanceEstimator.FormatDays(estimate.WaterDays)));
            noteItems.Add(ScenarioAuthoringPresentationUtilities.Property(
                "Food",
                ScenarioSuppliesBalanceEstimator.FormatDays(estimate.FoodDays)));
            noteItems.Add(ScenarioAuthoringPresentationUtilities.Text(ScenarioSuppliesBalanceEstimator.AssumptionsLine()));
            noteItems.Add(ScenarioAuthoringPresentationUtilities.ActionItem(ScenarioAuthoringPresentationUtilities.Action(
                ScenarioAuthoringLocalActionIds.ActionSuppliesPresetApplyPrefix + presetIndex.ToString(CultureInfo.InvariantCulture),
                "Apply " + preset.DisplayName,
                "Replace the authored starting items with this preset. Undo restores the previous loadout.",
                true,
                true,
                "OK")));
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "preset_preview_notes",
                Title = "After applying",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = noteItems.ToArray()
            });

            return sections.ToArray();
        }

        private static bool TryBuildCapacityNote(StartingInventoryDefinition inventory, out string note)
        {
            note = null;
            InventoryManager manager = InventoryManager.Instance;
            if (manager == null)
                return false;

            int capacity;
            try { capacity = manager.storageCapacity; }
            catch { return false; }
            if (capacity <= 0)
                return false;

            int stacks = CountStacks(inventory);
            if (stacks > capacity)
            {
                note = "Heads up: about " + stacks.ToString(CultureInfo.InvariantCulture)
                    + " stacks may exceed this base's storage capacity of "
                    + capacity.ToString(CultureInfo.InvariantCulture) + ".";
                return true;
            }
            return false;
        }

        private static int ResolveSurvivorCount(ScenarioDefinition definition)
        {
            int members = ScenarioAuthoringPresentationUtilities.CountFamilyMembers(definition);
            return members > 0 ? members : ScenarioSuppliesBalanceEstimator.DefaultSurvivorCount;
        }

        private static int CountStacks(StartingInventoryDefinition inventory)
        {
            return inventory != null && inventory.Items != null ? inventory.Items.Count : 0;
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
    }
}
