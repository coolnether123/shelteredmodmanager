using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal static class ScenarioAssetInventoryActionIds
    {
        public const string Prefix = "asset_browser.inventory.";
        public const string RelinkPrefix = Prefix + "relink.";
        public const string RemovePrefix = Prefix + "remove.";
        public const string KeepPrefix = Prefix + "keep.";
        public const string CreditPrefix = Prefix + "credit.";
        public const string NavigatePrefix = Prefix + "navigate.";
    }

    internal sealed class ScenarioAssetInventoryContentBuilder
    {
        private readonly ScenarioAssetInventoryService _inventoryService;

        public ScenarioAssetInventoryContentBuilder(ScenarioAssetInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        public List<ScenarioAuthoringInspectorSection> Build(ScenarioAuthoringState state, ScenarioEditorSession editorSession)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            ScenarioAssetInventory inventory = _inventoryService.Build(
                editorSession != null ? editorSession.WorkingDefinition : null,
                state != null ? state.ActiveScenarioFilePath : null);

            List<ScenarioAuthoringInspectorItem> summary = new List<ScenarioAuthoringInspectorItem>();
            summary.Add(ScenarioInspectorItemFactory.Property("Referenced files", CountState(inventory, ScenarioAssetInventoryState.Available, ScenarioAssetInventoryState.Missing).ToString(CultureInfo.InvariantCulture)));
            summary.Add(ScenarioInspectorItemFactory.Property("Total asset payload", FormatBytes(inventory.TotalPayloadSize)));
            summary.Add(ScenarioInspectorItemFactory.Property("Payload warning threshold", "25 MB"));
            if (inventory.PayloadWarning)
                summary.Add(ScenarioInspectorItemFactory.Text("Large asset payload: packages above 25 MB take longer to export, install, and load."));
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

            for (int i = 0; i < inventory.Items.Count; i++)
                sections.Add(BuildItemSection(inventory.Items[i], i));
            return sections;
        }

        private static ScenarioAuthoringInspectorSection BuildItemSection(ScenarioAssetInventoryItem asset, int index)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioAuthoringInspectorItem file = ScenarioInspectorItemFactory.Property("File name", asset.FileName ?? asset.RelativePath);
            file.Detail = asset.RelativePath;
            file.PreviewSprite = asset.Thumbnail;
            file.Badge = BuildBadge(asset.State);
            file.Emphasized = asset.State != ScenarioAssetInventoryState.Available;
            items.Add(file);
            items.Add(ScenarioInspectorItemFactory.Property("Dimensions", asset.Width > 0 && asset.Height > 0 ? asset.Width.ToString(CultureInfo.InvariantCulture) + " x " + asset.Height.ToString(CultureInfo.InvariantCulture) + " px" : "Unknown"));
            items.Add(ScenarioInspectorItemFactory.Property("File size", asset.State == ScenarioAssetInventoryState.Missing ? "Missing" : FormatBytes(asset.Size)));
            items.Add(ScenarioInspectorItemFactory.Property("Source", FormatSource(asset.Source)));

            if (asset.State == ScenarioAssetInventoryState.Missing)
            {
                items.Add(ScenarioInspectorItemFactory.Text("This sprite will show as missing in game."));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAssetInventoryActionIds.RelinkPrefix + ScenarioAuthoringActionCodec.EncodeToken(asset.RelativePath),
                    "Relink...",
                    "Use the newest replacement file from the scenario Imports folder and update every matching reference atomically.",
                    true, false, "LINK")));
            }
            else if (asset.State == ScenarioAssetInventoryState.Orphan)
            {
                items.Add(ScenarioInspectorItemFactory.Text("This file is unused and will not be included in export."));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAssetInventoryActionIds.RemovePrefix + ScenarioAuthoringActionCodec.EncodeToken(asset.RelativePath),
                    "Remove file",
                    "Choose twice to confirm permanent deletion from the draft asset folder.",
                    true, false, "DEL")));
                items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                    ScenarioAssetInventoryActionIds.KeepPrefix + ScenarioAuthoringActionCodec.EncodeToken(asset.RelativePath),
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
                            ScenarioAssetInventoryActionIds.NavigatePrefix + ScenarioAuthoringActionCodec.EncodeToken(reference.NavigationToken),
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
                ScenarioAssetInventoryActionIds.CreditPrefix + ScenarioAuthoringActionCodec.EncodeToken(asset.RelativePath) + ".",
                "Save asset credit", credit.HoverHint, true, false, "CR");
            items.Add(credit);

            return new ScenarioAuthoringInspectorSection
            {
                Id = "asset_inventory_file_" + index.ToString(CultureInfo.InvariantCulture),
                Title = asset.FileName ?? "Asset file",
                Expanded = asset.State != ScenarioAssetInventoryState.Available,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static int CountState(ScenarioAssetInventory inventory, ScenarioAssetInventoryState first, ScenarioAssetInventoryState second)
        {
            int count = 0;
            for (int i = 0; inventory != null && i < inventory.Items.Count; i++) if (inventory.Items[i].State == first || inventory.Items[i].State == second) count++;
            return count;
        }

        private static string BuildBadge(ScenarioAssetInventoryState state)
        {
            switch (state)
            {
                case ScenarioAssetInventoryState.Missing: return "MISSING";
                case ScenarioAssetInventoryState.Orphan: return "ORPHAN";
                default: return "READY";
            }
        }

        private static string FormatSource(ScenarioAssetInventorySource source)
        {
            switch (source)
            {
                case ScenarioAssetInventorySource.VanillaReplacement: return "vanilla-replacement";
                case ScenarioAssetInventorySource.PixelEdited: return "pixel-edited";
                default: return "imported";
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
