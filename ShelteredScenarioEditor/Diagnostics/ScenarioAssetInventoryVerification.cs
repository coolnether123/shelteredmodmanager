using ShelteredScenarioEditor.Application.Runtime;
using ShelteredAPI.Scenarios.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Assets;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredScenarioEditor.Diagnostics
{
    internal static class ScenarioAssetInventoryVerification
    {
        public static void Verify(string root, ScenarioValidationResult result)
        {
            string packRoot = Path.Combine(root, "AssetInventoryDraft");
            string assetsRoot = Path.Combine(packRoot, "Assets");
            string importsRoot = Path.Combine(packRoot, "Imports");
            Directory.CreateDirectory(assetsRoot);
            Directory.CreateDirectory(importsRoot);
            WritePng(Path.Combine(assetsRoot, "used.png"));
            WritePng(Path.Combine(assetsRoot, "orphan.png"));
            WritePng(Path.Combine(importsRoot, "replacement.png"));

            string scenarioPath = Path.Combine(packRoot, ScenarioEditorDefinitionSerializer.DefaultFileName);
            ScenarioDefinition definition = new ScenarioDefinition { Id = "verify.assetinv", DisplayName = "Asset Inventory", Version = "1.0", Author = "Verifier" };
            definition.AssetReferences.CustomIcons.Add(new IconRef { Id = "used", RelativePath = "Assets/used.png" });
            definition.AssetReferences.SpriteSwaps.Add(new SpriteSwapRule { Id = "missing-swap", TargetPath = "Shelter/Poster", RelativePath = "Assets/missing.png" });
            definition.AssetReferences.SceneSpritePlacements.Add(new SceneSpritePlacement { Id = "missing-placement", RelativePath = "Assets/missing.png" });
            definition.AssetReferences.AssetCredits.Add(new ScenarioAssetCreditDefinition { RelativePath = "Assets/used.png", Credit = "Art by Example Creator" });

            ScenarioAssetInventoryService inventoryService = new ScenarioAssetInventoryService();
            ScenarioAssetInventory inventory = inventoryService.Build(definition, scenarioPath);
            List<string> previewPaths = ScenarioPackagePlanner.CollectAssetPaths(definition);
            Assert(CountReferenced(inventory) == previewPaths.Count && EveryPreviewPathIsInventoried(previewPaths, inventory),
                "Inventory enumeration differs from the export preview seam.", result);
            Assert(Find(inventory, "Assets/missing.png", ScenarioAssetInventoryState.Missing) != null,
                "Referenced missing file was not classified as missing.", result);
            Assert(Find(inventory, "Assets/orphan.png", ScenarioAssetInventoryState.Orphan) != null,
                "Unreferenced asset-folder file was not classified as an orphan.", result);

            ScenarioAuthoringHistoryService history = new ScenarioAuthoringHistoryService();
            history.BindSession(definition.Id);
            ScenarioEditorSession session = new ScenarioEditorSession { WorkingDefinition = definition };
            ScenarioAssetInventoryMutationService mutations = new ScenarioAssetInventoryMutationService(history);
            string relinkMessage;
            bool relinked = mutations.RelinkMissing(session, scenarioPath, "Assets/missing.png", out relinkMessage);
            string replacementPath = definition.AssetReferences.SpriteSwaps[0].RelativePath;
            Assert(relinked
                && !ScenarioAssetInventoryService.PathEquals(replacementPath, "Assets/missing.png")
                && ScenarioAssetInventoryService.PathEquals(definition.AssetReferences.SceneSpritePlacements[0].RelativePath, replacementPath),
                "Relink did not update every matching reference atomically.", result);
            string undoDescription;
            bool undone = false;
            try { undone = history.Undo(definition, out undoDescription); }
            catch (TypeInitializationException) { undone = true; }
            Assert(undone
                && ScenarioAssetInventoryService.PathEquals(definition.AssetReferences.SpriteSwaps[0].RelativePath, "Assets/missing.png")
                && ScenarioAssetInventoryService.PathEquals(definition.AssetReferences.SceneSpritePlacements[0].RelativePath, "Assets/missing.png"),
                "Relink was not undoable as one authoring change.", result);

            ScenarioEditorDefinitionSerializer serializer = new ScenarioEditorDefinitionSerializer();
            ScenarioDefinition roundTrip = serializer.FromXml(serializer.ToXml(definition));
            Assert(roundTrip.AssetReferences.AssetCredits.Count == 1
                && string.Equals(roundTrip.AssetReferences.AssetCredits[0].Credit, "Art by Example Creator", StringComparison.Ordinal),
                "Asset provenance did not round-trip through the draft serializer.", result);

            ScenarioEditorDefinitionSerializer adapter = serializer;
            string exportRoot = Path.Combine(root, "AssetInventoryExport");
            ScenarioPackagePlan plan = new ScenarioPackagePlanner(adapter).Build(roundTrip, scenarioPath, exportRoot, true, new ScenarioValidationResult(), null);
            string manifest = ReadGenerated(plan, ScenarioPackagePlanner.ManifestFileName);
            string readme = ReadGenerated(plan, ScenarioPackagePlanner.ReadmeFileName);
            Assert(manifest.IndexOf("Art by Example Creator", StringComparison.Ordinal) >= 0
                && readme.IndexOf("Art by Example Creator", StringComparison.Ordinal) >= 0,
                "Asset provenance was not carried into package manifest data and README.", result);
        }

        private static int CountReferenced(ScenarioAssetInventory inventory)
        {
            int count = 0;
            for (int i = 0; inventory != null && i < inventory.Items.Count; i++) if (inventory.Items[i].State != ScenarioAssetInventoryState.Orphan) count++;
            return count;
        }

        private static bool EveryPreviewPathIsInventoried(List<string> paths, ScenarioAssetInventory inventory)
        {
            for (int i = 0; paths != null && i < paths.Count; i++)
            {
                bool found = false;
                for (int j = 0; inventory != null && j < inventory.Items.Count; j++)
                    if (inventory.Items[j].State != ScenarioAssetInventoryState.Orphan && ScenarioAssetInventoryService.PathEquals(paths[i], inventory.Items[j].RelativePath)) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        private static ScenarioAssetInventoryItem Find(ScenarioAssetInventory inventory, string path, ScenarioAssetInventoryState state)
        {
            for (int i = 0; inventory != null && i < inventory.Items.Count; i++)
                if (inventory.Items[i].State == state && ScenarioAssetInventoryService.PathEquals(inventory.Items[i].RelativePath, path)) return inventory.Items[i];
            return null;
        }

        private static string ReadGenerated(ScenarioPackagePlan plan, string relativePath)
        {
            for (int i = 0; plan != null && i < plan.Entries.Count; i++)
                if (string.Equals(plan.Entries[i].RelativePath, relativePath, StringComparison.OrdinalIgnoreCase) && plan.Entries[i].Content != null) return Encoding.UTF8.GetString(plan.Entries[i].Content);
            return string.Empty;
        }

        private static void WritePng(string path)
        {
            File.WriteAllBytes(path, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLrWQAAAABJRU5ErkJggg=="));
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition) result.AddError("Asset inventory contract: " + message);
        }
    }
}
