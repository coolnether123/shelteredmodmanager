using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Assets;

namespace ShelteredAPI.Scenarios.Application.Assets
{
    internal sealed class ScenarioAssetInventoryMutationService
    {
        private readonly ScenarioAuthoringHistoryService _history;
        private string _pendingRemovalPath;

        public ScenarioAssetInventoryMutationService(ScenarioAuthoringHistoryService history)
        {
            _history = history;
        }

        public bool RelinkMissing(ScenarioEditorSession session, string scenarioFilePath, string missingPath, out string message)
        {
            message = null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null || string.IsNullOrEmpty(missingPath))
            {
                message = "No missing asset is available to relink.";
                return false;
            }

            string importFolder = ScenarioPngImportService.GetImportFolderPath(scenarioFilePath, true);
            string[] files = Directory.GetFiles(importFolder, "*", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                message = "No replacement files were found in the scenario import folder: " + importFolder;
                return false;
            }
            Array.Sort(files, CompareNewestFirst);
            string source = files[0];
            string packRoot = ScenarioAssetInventoryService.GetPackRoot(scenarioFilePath);
            string extension = Path.GetExtension(source);
            string fileName = Sanitize(Path.GetFileNameWithoutExtension(source)) + "_relinked_" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + extension;
            string newRelativePath = ScenarioAssetInventoryService.NormalizeRelativePath(Path.Combine("Assets", Path.Combine("Relinked", fileName)));
            string destination = ScenarioAssetInventoryService.ResolvePackPath(packRoot, newRelativePath);
            string destinationDirectory = Path.GetDirectoryName(destination);
            if (!Directory.Exists(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);
            File.Copy(source, destination, false);

            if (_history != null) _history.RecordVisualChange(definition, "Relink missing asset " + missingPath);
            int updates = ReplaceAllReferences(definition, missingPath, newRelativePath);
            if (updates == 0)
            {
                File.Delete(destination);
                message = "The missing path is no longer referenced by this draft.";
                return false;
            }
            MoveCredit(definition, missingPath, newRelativePath);
            session.MarkDraftChanged(ScenarioDirtySection.Assets, ScenarioEditCategory.Assets);
            message = "Relinked " + updates.ToString(CultureInfo.InvariantCulture) + " reference(s) to '" + Path.GetFileName(source) + "'. Undo restores every previous reference.";
            return true;
        }

        public bool RemoveOrphan(ScenarioEditorSession session, string scenarioFilePath, string relativePath, out string message)
        {
            message = null;
            if (!ScenarioAssetInventoryService.PathEquals(_pendingRemovalPath, relativePath))
            {
                _pendingRemovalPath = relativePath;
                message = "Confirm removal by choosing Remove file again. This deletes the unreferenced file from the draft asset folder.";
                return true;
            }

            _pendingRemovalPath = null;
            string path = ScenarioAssetInventoryService.ResolvePackPath(ScenarioAssetInventoryService.GetPackRoot(scenarioFilePath), relativePath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                message = "The orphan file is already absent.";
                return true;
            }
            File.Delete(path);
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition != null && definition.AssetReferences != null)
            {
                List<ScenarioAssetCreditDefinition> credits = definition.AssetReferences.AssetCredits;
                for (int i = credits.Count - 1; i >= 0; i--)
                    if (credits[i] != null && ScenarioAssetInventoryService.PathEquals(credits[i].RelativePath, relativePath)) credits.RemoveAt(i);
                session.MarkDraftChanged(ScenarioDirtySection.Assets, ScenarioEditCategory.Assets);
            }
            message = "Removed orphan file '" + Path.GetFileName(path) + "'.";
            return true;
        }

        public bool KeepOrphan(string relativePath, out string message)
        {
            if (ScenarioAssetInventoryService.PathEquals(_pendingRemovalPath, relativePath)) _pendingRemovalPath = null;
            message = "Kept '" + Path.GetFileName(relativePath) + "'. It remains unreferenced and will not be included in export.";
            return true;
        }

        public bool SetCredit(ScenarioEditorSession session, string relativePath, string credit, out string message)
        {
            message = null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null || definition.AssetReferences == null)
            {
                message = "No active asset draft is available.";
                return false;
            }
            if (_history != null) _history.RecordVisualChange(definition, "Edit asset credit for " + Path.GetFileName(relativePath));
            UpsertCredit(definition, relativePath, credit);
            session.MarkDraftChanged(ScenarioDirtySection.Assets, ScenarioEditCategory.Assets);
            message = string.IsNullOrEmpty(credit) ? "Asset credit cleared." : "Asset credit updated.";
            return true;
        }

        internal static int ReplaceAllReferences(ScenarioDefinition definition, string oldPath, string newPath)
        {
            if (definition == null) return 0;
            int count = 0;
            AssetReferencesDefinition assets = definition.AssetReferences;
            for (int i = 0; assets != null && assets.CustomSprites != null && i < assets.CustomSprites.Count; i++)
            {
                SpriteRef item = assets.CustomSprites[i];
                if (item != null && ShouldReplace(item.RelativePath, oldPath)) { item.RelativePath = newPath; count++; }
            }
            for (int i = 0; assets != null && assets.CustomIcons != null && i < assets.CustomIcons.Count; i++)
            {
                IconRef item = assets.CustomIcons[i];
                if (item != null && ShouldReplace(item.RelativePath, oldPath)) { item.RelativePath = newPath; count++; }
            }
            for (int i = 0; assets != null && assets.SpriteSwaps != null && i < assets.SpriteSwaps.Count; i++)
            {
                SpriteSwapRule item = assets.SpriteSwaps[i];
                if (item != null && ShouldReplace(item.RelativePath, oldPath)) { item.RelativePath = newPath; count++; }
            }
            for (int i = 0; assets != null && assets.SceneSpritePlacements != null && i < assets.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement item = assets.SceneSpritePlacements[i];
                if (item != null && ShouldReplace(item.RelativePath, oldPath)) { item.RelativePath = newPath; count++; }
            }
            for (int i = 0; assets != null && assets.SpritePatches != null && i < assets.SpritePatches.Count; i++)
            {
                SpritePatchDefinition item = assets.SpritePatches[i];
                if (item != null && ShouldReplace(item.BaseRelativePath, oldPath)) { item.BaseRelativePath = newPath; count++; }
            }
            List<FamilyMemberConfig> members = definition.FamilySetup != null ? definition.FamilySetup.Members : null;
            for (int i = 0; members != null && i < members.Count; i++)
            {
                FamilyMemberAppearanceConfig appearance = members[i] != null ? members[i].Appearance : null;
                if (appearance == null) continue;
                if (ShouldReplace(appearance.HeadTexturePath, oldPath)) { appearance.HeadTexturePath = newPath; count++; }
                if (ShouldReplace(appearance.TorsoTexturePath, oldPath)) { appearance.TorsoTexturePath = newPath; count++; }
                if (ShouldReplace(appearance.LegTexturePath, oldPath)) { appearance.LegTexturePath = newPath; count++; }
            }
            return count;
        }

        private static bool ShouldReplace(string value, string oldPath)
        {
            return !string.IsNullOrEmpty(value) && ScenarioAssetInventoryService.PathEquals(value, oldPath);
        }

        private static void MoveCredit(ScenarioDefinition definition, string oldPath, string newPath)
        {
            List<ScenarioAssetCreditDefinition> credits = definition.AssetReferences.AssetCredits;
            for (int i = 0; i < credits.Count; i++) if (credits[i] != null && ScenarioAssetInventoryService.PathEquals(credits[i].RelativePath, oldPath)) credits[i].RelativePath = newPath;
        }

        private static void UpsertCredit(ScenarioDefinition definition, string path, string credit)
        {
            List<ScenarioAssetCreditDefinition> credits = definition.AssetReferences.AssetCredits;
            for (int i = credits.Count - 1; i >= 0; i--)
            {
                if (credits[i] == null || !ScenarioAssetInventoryService.PathEquals(credits[i].RelativePath, path)) continue;
                if (string.IsNullOrEmpty(credit)) credits.RemoveAt(i);
                else credits[i].Credit = credit.Trim();
                return;
            }
            if (!string.IsNullOrEmpty(credit)) credits.Add(new ScenarioAssetCreditDefinition { RelativePath = path, Credit = credit.Trim() });
        }

        private static int CompareNewestFirst(string left, string right)
        {
            int time = File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left));
            return time != 0 ? time : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "asset";
            char[] result = value.ToCharArray();
            for (int i = 0; i < result.Length; i++) if (!char.IsLetterOrDigit(result[i]) && result[i] != '-' && result[i] != '_') result[i] = '_';
            return new string(result);
        }
    }
}
