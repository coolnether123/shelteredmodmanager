using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using ShelteredScenarioEditor.Application.Assets;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Presentation.Inspector;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    internal sealed class ScenarioAssetInventoryContentBuilder
    {
        private readonly ScenarioAssetInventoryService _inventoryService;
        private readonly IScenarioEditorSessionStore _sessionStore;

        public ScenarioAssetInventoryContentBuilder(
            ScenarioAssetInventoryService inventoryService,
            IScenarioEditorSessionStore sessionStore)
        {
            _inventoryService = inventoryService;
            _sessionStore = sessionStore;
        }

        public List<ScenarioAuthoringInspectorSection> Build(ScenarioAuthoringState state, ScenarioEditorSession editorSession)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            ScenarioAssetInventory inventory = _inventoryService.Build(
                editorSession != null ? editorSession.WorkingDefinition : null,
                _sessionStore.CurrentFilePath);

            List<ScenarioAuthoringInspectorItem> summary = new List<ScenarioAuthoringInspectorItem>();
            int missing = CountState(inventory, ScenarioAssetInventoryState.Missing);
            int orphan = CountState(inventory, ScenarioAssetInventoryState.Orphan);
            int large = CountLarge(inventory);
            summary.Add(ScenarioInspectorItemFactory.Property("Files", inventory.Items.Count.ToString(CultureInfo.InvariantCulture)));
            summary.Add(ScenarioInspectorItemFactory.Property("Payload", FormatBytes(inventory.TotalPayloadSize)));
            summary.Add(ScenarioInspectorItemFactory.Property("Warnings", (missing + orphan + large).ToString(CultureInfo.InvariantCulture)));
            summary.Add(ScenarioInspectorItemFactory.Property("Payload warning threshold", "25 MB"));
            if (inventory.PayloadWarning)
                summary.Add(ScenarioInspectorItemFactory.Text("Large asset payload: packages above 25 MB take longer to export, install, and load."));
            if (missing > 0 || orphan > 0 || large > 0)
                summary.Add(ScenarioInspectorItemFactory.Text(FormatWarningSummary(missing, orphan, large)));
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "asset_inventory_summary",
                Title = "Asset Inventory",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.MetricGrid,
                Items = summary.ToArray()
            });

            if (inventory.Items.Count == 0)
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "asset_inventory_empty",
                    Title = "Files",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[] { ScenarioInspectorItemFactory.Text("This scenario does not reference any asset files, and its Assets folder is empty.") }
                });
                return sections;
            }

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "asset_inventory_filters",
                Title = "Filter",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                RendererKind = ScenarioAuthoringInspectorSectionRendererKind.AssetInventoryFilters,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.Text("all"),
                    ScenarioInspectorItemFactory.Text("missing"),
                    ScenarioInspectorItemFactory.Text("orphan"),
                    ScenarioInspectorItemFactory.Text("large")
                }
            });

            for (int i = 0; i < inventory.Items.Count; i++)
                sections.Add(BuildItemSection(inventory.Items[i], i, ShowAdvanced(state)));
            return sections;
        }

        private static ScenarioAuthoringInspectorSection BuildItemSection(ScenarioAssetInventoryItem asset, int index, bool showAdvanced)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioAuthoringInspectorItem file = ScenarioInspectorItemFactory.Property("File name", asset.FileName ?? asset.RelativePath);
            file.Detail = showAdvanced ? asset.RelativePath : null;
            file.PreviewSprite = asset.Thumbnail;
            file.Badge = BuildBadge(asset.State, asset.IsLarge);
            file.Emphasized = asset.State != ScenarioAssetInventoryState.Available;
            items.Add(file);
            items.Add(ScenarioInspectorItemFactory.Property("Dimensions", asset.Width > 0 && asset.Height > 0 ? asset.Width.ToString(CultureInfo.InvariantCulture) + " x " + asset.Height.ToString(CultureInfo.InvariantCulture) + " px" : "Unknown"));
            items.Add(ScenarioInspectorItemFactory.Property("File size", asset.State == ScenarioAssetInventoryState.Missing ? "Missing" : FormatBytes(asset.Size)));
            items.Add(ScenarioInspectorItemFactory.Property("Source", FormatSource(asset.Source)));
            items.Add(ScenarioInspectorItemFactory.Property("References", asset.References.Count.ToString(CultureInfo.InvariantCulture)));

            if (asset.State == ScenarioAssetInventoryState.Missing)
            {
                items.Add(ScenarioInspectorItemFactory.Text("This sprite will show as missing in game."));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    AssetBrowserCommand.Relink(asset.RelativePath),
                    "Relink Asset",
                    "Use the newest replacement file from the scenario Imports folder and update every matching reference atomically.",
                    true, false, "LINK")));
            }
            else if (asset.State == ScenarioAssetInventoryState.Orphan)
            {
                items.Add(ScenarioInspectorItemFactory.Text("This file is unused and will not be included in export."));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    AssetBrowserCommand.Remove(asset.RelativePath),
                    "Remove file",
                    "Choose twice to confirm permanent deletion from the draft asset folder.",
                    true, false, "DEL")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    AssetBrowserCommand.Keep(asset.RelativePath),
                    "Keep",
                    "Leave this unreferenced file in the draft Assets folder.",
                    true, false, "KEEP")));
            }

            if (asset.IsLarge)
                items.Add(ScenarioInspectorItemFactory.Text("Large texture: files over 2048 px on either side or 2 MB use more memory and can slow loading."));

            if (asset.References.Count == 0)
            {
                items.Add(ScenarioInspectorItemFactory.Property("Referenced by", "Nothing"));
            }
            else
            {
                for (int r = 0; r < asset.References.Count; r++)
                {
                    ScenarioAssetInventoryReference reference = asset.References[r];
                    if (!string.IsNullOrEmpty(reference.NavigationToken))
                    {
                        items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                            AssetBrowserCommand.Navigate(reference.NavigationToken),
                            (r == 0 ? "Referenced by: " : "Also: ") + reference.Label,
                            "Navigate to the closest existing editor seam for this use.",
                            true, false, "GO")));
                    }
                    else
                    {
                        items.Add(ScenarioInspectorItemFactory.Property(r == 0 ? "Referenced by" : "Also", reference.Label));
                    }
                }
            }

            ScenarioAuthoringInspectorItem credit = ScenarioInspectorItemFactory.Property("Author / credit note", asset.Credit ?? string.Empty);
            credit.Editable = true;
            credit.HoverHint = "Optional asset-specific author or source credit. Press Enter or leave the field to save.";
            credit.Action = ScenarioInspectorItemFactory.Action(
                AssetBrowserCommand.SetCredit(asset.RelativePath, asset.Credit),
                "Save asset credit", credit.HoverHint, true, false, "CR");
            items.Add(credit);

            return new ScenarioAuthoringInspectorSection
            {
                Id = "asset_inventory_file_" + FilterToken(asset) + "_" + index.ToString(CultureInfo.InvariantCulture),
                Title = asset.FileName ?? "Asset file",
                Expanded = asset.State != ScenarioAssetInventoryState.Available,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                RendererKind = ScenarioAuthoringInspectorSectionRendererKind.AssetInventoryRow,
                RendererFilter = FilterToken(asset),
                Items = items.ToArray()
            };
        }

        private static bool ShowAdvanced(ScenarioAuthoringState state)
        {
            return state != null
                && state.Settings != null
                && state.Settings.ShowAdvancedDetails;
        }

        private static string FilterToken(ScenarioAssetInventoryItem asset)
        {
            if (asset == null) return "available";
            if (asset.State == ScenarioAssetInventoryState.Missing) return "missing";
            if (asset.State == ScenarioAssetInventoryState.Orphan) return "orphan";
            return asset.IsLarge ? "large" : "available";
        }

        private static int CountState(ScenarioAssetInventory inventory, ScenarioAssetInventoryState state)
        {
            int count = 0;
            for (int i = 0; inventory != null && i < inventory.Items.Count; i++) if (inventory.Items[i].State == state) count++;
            return count;
        }

        private static int CountLarge(ScenarioAssetInventory inventory)
        {
            int count = 0;
            for (int i = 0; inventory != null && i < inventory.Items.Count; i++) if (inventory.Items[i].IsLarge) count++;
            return count;
        }

        private static string FormatWarningSummary(int missing, int orphan, int large)
        {
            List<string> parts = new List<string>();
            if (missing > 0) parts.Add(missing.ToString(CultureInfo.InvariantCulture) + " missing");
            if (orphan > 0) parts.Add(orphan.ToString(CultureInfo.InvariantCulture) + " orphaned");
            if (large > 0) parts.Add(large.ToString(CultureInfo.InvariantCulture) + " large");
            return "Review: " + string.Join(", ", parts.ToArray()) + ".";
        }

        private static string BuildBadge(ScenarioAssetInventoryState state, bool large)
        {
            switch (state)
            {
                case ScenarioAssetInventoryState.Missing: return "MISSING";
                case ScenarioAssetInventoryState.Orphan: return "ORPHAN";
                default: return large ? "LARGE" : "READY";
            }
        }

        private static string FormatSource(ScenarioAssetInventorySource source)
        {
            switch (source)
            {
                case ScenarioAssetInventorySource.VanillaReplacement: return "Vanilla replacement";
                case ScenarioAssetInventorySource.PixelEdited: return "Pixel edited";
                default: return "Imported";
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L) return ((double)bytes / (1024.0 * 1024.0)).ToString("0.##", CultureInfo.InvariantCulture) + " MB";
            if (bytes >= 1024L) return ((double)bytes / 1024.0).ToString("0.##", CultureInfo.InvariantCulture) + " KB";
            return bytes.ToString(CultureInfo.InvariantCulture) + " bytes";
        }
    }
}
