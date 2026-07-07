using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ShelteredAPI.Saves;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioAuthoringDraftRepository
    {
        internal sealed class DraftRecord
        {
            public ScenarioInfo Info;
            public SaveEntry StartupSave;
            public int Slot;
        }

        internal const string DraftOwnerId = "smm.authoring";
        internal const string DraftStorageScenarioId = "ScenarioAuthoringDrafts";
        private readonly object _sync = new object();
        private readonly ScenarioDefinitionSerializer _serializer = new ScenarioDefinitionSerializer();
        private readonly IScenarioSaveLibrary _saveLibrary;

        public static ScenarioAuthoringDraftRepository Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioAuthoringDraftRepository>(); }
        }

        internal ScenarioAuthoringDraftRepository(IScenarioSaveLibrary saveLibrary)
        {
            if (saveLibrary == null)
                throw new ArgumentNullException("saveLibrary");

            _saveLibrary = saveLibrary;
            ScenarioRegistry.RegisterScenario(new ScenarioDescriptor
            {
                id = DraftStorageScenarioId,
                displayName = "Scenario Authoring Drafts",
                description = "Scenario authoring draft storage.",
                version = "1.0"
            });
        }

        public DraftRecord CreateDraft(ScenarioBaseGameMode baseMode)
        {
            lock (_sync)
            {
                string draftsRoot = EnsureDraftsRoot();
                string scenarioId = CreateDraftId();
                int slot = GetNextDraftSlot(draftsRoot);
                int nextSaveSlot = _saveLibrary.GetNextAvailableSlot(DraftStorageScenarioId);
                if (nextSaveSlot > slot)
                    slot = nextSaveSlot;

                string draftRoot = EnsureSlotRoot(slot);
                while (Directory.Exists(draftRoot) && File.Exists(Path.Combine(draftRoot, ScenarioDefinitionSerializer.DefaultFileName)))
                {
                    scenarioId = CreateDraftId();
                    slot++;
                    draftRoot = EnsureSlotRoot(slot);
                }

                SaveEntry startupSave = _saveLibrary.CreateNext(DraftStorageScenarioId, new SaveCreateOptions
                {
                    name = scenarioId,
                    absoluteSlot = slot
                });
                if (startupSave == null)
                    throw new InvalidOperationException("Could not allocate the draft startup save entry.");

                ScenarioDefinition definition = new ScenarioDefinition();
                definition.Id = scenarioId;
                definition.DisplayName = "Untitled Scenario";
                definition.Description = "Local scenario authoring draft.";
                definition.Author = "SMM Authoring";
                definition.Version = "0.1.0";
                definition.BaseGameMode = baseMode;
                definition.SelectionRules = ScenarioSelectionRulesDefinition.ForBaseMode(baseMode);

                string scenarioFilePath = Path.Combine(draftRoot, ScenarioDefinitionSerializer.DefaultFileName);
                _serializer.Save(definition, scenarioFilePath);
                ScenarioDefinitionMetadataCache.Invalidate(scenarioFilePath);
                new ScenarioAuthoringSetupStateService().CreateInitialForScenarioFile(scenarioFilePath);
                MMLog.WriteInfo("[ScenarioAuthoringDraftRepository] Created draft '" + scenarioId + "' in save-system slot " + slot
                    + " at " + scenarioFilePath + ".");
                return new DraftRecord
                {
                    Info = _serializer.LoadInfo(scenarioFilePath, DraftOwnerId),
                    StartupSave = startupSave,
                    Slot = slot
                };
            }
        }

        public ScenarioInfo[] ListAll()
        {
            lock (_sync)
            {
                string draftsRoot = GetDraftsRootPath();
                if (!Directory.Exists(draftsRoot))
                    return new ScenarioInfo[0];

                string[] files;
                try
                {
                    files = EnumerateDraftScenarioFiles(draftsRoot);
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] Failed to scan draft scenarios: " + ex.Message);
                    return new ScenarioInfo[0];
                }

                Dictionary<string, ScenarioInfo> byId = new Dictionary<string, ScenarioInfo>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        ScenarioInfo info = LoadInfoWithRecovery(files[i]);
                        if (info == null || string.IsNullOrEmpty(info.Id) || byId.ContainsKey(info.Id))
                            continue;

                        byId[info.Id] = info;
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] Skipping invalid draft scenario '" + files[i] + "': " + ex.Message);
                    }
                }

                List<ScenarioInfo> results = new List<ScenarioInfo>();
                foreach (KeyValuePair<string, ScenarioInfo> pair in byId)
                    results.Add(pair.Value);

                results.Sort(CompareInfo);
                return results.ToArray();
            }
        }

        public bool TryGet(string scenarioId, out ScenarioInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(scenarioId))
                return false;

            lock (_sync)
            {
                string[] files = EnumerateDraftScenarioFiles(GetDraftsRootPath());
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        ScenarioInfo loaded = LoadInfoWithRecovery(files[i]);
                        if (loaded != null && string.Equals(loaded.Id, scenarioId, StringComparison.OrdinalIgnoreCase))
                        {
                            info = loaded;
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] Failed while resolving draft '" + scenarioId + "': " + ex.Message);
                    }
                }

                return false;
            }
        }

        public bool TryGetDraftSaveEntry(string draftId, out SaveEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(draftId))
                return false;

            lock (_sync)
            {
                string[] files = EnumerateDraftScenarioFiles(GetDraftsRootPath());
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        ScenarioInfo loaded = LoadInfoWithRecovery(files[i]);
                        if (loaded == null || !string.Equals(loaded.Id, draftId, StringComparison.OrdinalIgnoreCase))
                            continue;

                        int slot = TryParseSlotNumber(files[i]);
                        if (slot <= 0)
                            return false;

                        string saveId = DraftStorageScenarioId + "_" + slot;
                        entry = _saveLibrary.Get(DraftStorageScenarioId, saveId);
                        if (entry != null)
                            return true;

                        entry = new SaveEntry
                        {
                            id = saveId,
                            absoluteSlot = slot,
                            name = string.IsNullOrEmpty(loaded.DisplayName) ? loaded.Id : loaded.DisplayName,
                            scenarioId = DraftStorageScenarioId,
                            scenarioVersion = loaded.Version,
                            createdAt = DateTime.UtcNow.ToString("o"),
                            updatedAt = File.GetLastWriteTimeUtc(files[i]).ToString("o")
                        };
                        MMLog.WriteInfo("[ScenarioAuthoringDraftRepository] Reconstructed draft save entry for '"
                            + draftId + "' from slot " + slot + " because no SaveData.xml entry exists yet.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] Failed to resolve draft save entry for '" + draftId + "': " + ex.Message);
                    }
                }
            }

            return false;
        }

        public bool TryUpdateMetadata(string draftId, string displayName, string description, out ScenarioInfo updatedInfo, out string error)
        {
            updatedInfo = null;
            error = null;

            if (string.IsNullOrEmpty(draftId))
            {
                error = "Draft id is required.";
                return false;
            }

            if (string.IsNullOrEmpty(displayName))
            {
                error = "Scenario name is required.";
                return false;
            }

            lock (_sync)
            {
                string[] files = EnumerateDraftScenarioFiles(GetDraftsRootPath());
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        ScenarioDefinition definition = _serializer.Load(files[i]);
                        if (definition == null || !string.Equals(definition.Id, draftId, StringComparison.OrdinalIgnoreCase))
                            continue;

                        definition.DisplayName = displayName;
                        definition.Description = description ?? string.Empty;
                        _serializer.Save(definition, files[i]);
                        ScenarioDefinitionMetadataCache.Invalidate(files[i]);
                        updatedInfo = _serializer.LoadInfo(files[i], DraftOwnerId);
                        MMLog.WriteInfo("[ScenarioAuthoringDraftRepository] Updated draft metadata for '" + draftId + "'.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] Failed to update draft metadata for '" + draftId + "': " + ex.Message);
                        return false;
                    }
                }
            }

            error = "Draft '" + draftId + "' was not found.";
            return false;
        }

        public bool TryRenameDraft(string draftId, string newDraftId, string displayName, string description, out ScenarioInfo updatedInfo, out string error)
        {
            updatedInfo = null;
            error = null;

            if (string.IsNullOrEmpty(draftId))
            {
                error = "Draft id is required.";
                return false;
            }

            string normalizedId = NormalizeDraftId(newDraftId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                error = "Draft file name is required.";
                return false;
            }

            lock (_sync)
            {
                string[] files = EnumerateDraftScenarioFiles(GetDraftsRootPath());
                if (ScenarioIdExistsOutsideDrafts(normalizedId))
                {
                    error = "A published or exported scenario already uses file name '" + normalizedId + "'.";
                    return false;
                }

                for (int i = 0; i < files.Length; i++)
                {
                    ScenarioDefinition existing = SafeLoadDefinition(files[i]);
                    if (existing != null
                        && !string.Equals(existing.Id, draftId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.Id, normalizedId, StringComparison.OrdinalIgnoreCase))
                    {
                        error = "A draft already uses file name '" + normalizedId + "'.";
                        return false;
                    }
                }

                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        ScenarioDefinition definition = _serializer.Load(files[i]);
                        if (definition == null || !string.Equals(definition.Id, draftId, StringComparison.OrdinalIgnoreCase))
                            continue;

                        definition.Id = normalizedId;
                        definition.DisplayName = string.IsNullOrEmpty(displayName) ? definition.DisplayName : displayName;
                        definition.Description = description ?? string.Empty;
                        _serializer.Save(definition, files[i]);
                        ScenarioDefinitionMetadataCache.Invalidate(files[i]);
                        updatedInfo = _serializer.LoadInfo(files[i], DraftOwnerId);
                        MMLog.WriteInfo("[ScenarioAuthoringDraftRepository] Renamed draft '" + draftId + "' to '" + normalizedId + "'.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] Failed to rename draft '" + draftId + "': " + ex.Message);
                        return false;
                    }
                }
            }

            error = "Draft '" + draftId + "' was not found.";
            return false;
        }

        public bool TryDuplicateDraft(string draftId, out ScenarioInfo duplicateInfo, out string error)
        {
            duplicateInfo = null;
            error = null;
            if (string.IsNullOrEmpty(draftId))
            {
                error = "Draft id is required.";
                return false;
            }

            lock (_sync)
            {
                string[] files = EnumerateDraftScenarioFiles(GetDraftsRootPath());
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        ScenarioDefinition source = _serializer.Load(files[i]);
                        if (source == null || !string.Equals(source.Id, draftId, StringComparison.OrdinalIgnoreCase))
                            continue;

                        DraftRecord duplicate = CreateDraft(source.BaseGameMode);
                        if (duplicate == null || duplicate.Info == null || string.IsNullOrEmpty(duplicate.Info.FilePath))
                        {
                            error = "Could not allocate duplicate draft storage.";
                            return false;
                        }

                        string duplicateId = duplicate.Info.Id;
                        CopyDraftFolder(Path.GetDirectoryName(files[i]), Path.GetDirectoryName(duplicate.Info.FilePath));
                        ScenarioDefinitionMetadataCache.InvalidateUnder(Path.GetDirectoryName(duplicate.Info.FilePath));
                        source.Id = duplicateId;
                        source.DisplayName = BuildDuplicateDisplayName(source.DisplayName);
                        _serializer.Save(source, duplicate.Info.FilePath);
                        ScenarioDefinitionMetadataCache.Invalidate(duplicate.Info.FilePath);
                        duplicateInfo = _serializer.LoadInfo(duplicate.Info.FilePath, DraftOwnerId);
                        MMLog.WriteInfo("[ScenarioAuthoringDraftRepository] Duplicated draft '" + draftId + "' as '" + duplicateId + "'.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] Failed to duplicate draft '" + draftId + "': " + ex.Message);
                        return false;
                    }
                }
            }

            error = "Draft '" + draftId + "' was not found.";
            return false;
        }

        public bool DeleteDraft(string draftId, string reason)
        {
            if (string.IsNullOrEmpty(draftId))
                return false;

            lock (_sync)
            {
                string[] files = EnumerateDraftScenarioFiles(GetDraftsRootPath());
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        ScenarioInfo loaded = LoadInfoWithRecovery(files[i]);
                        if (loaded == null || !string.Equals(loaded.Id, draftId, StringComparison.OrdinalIgnoreCase))
                            continue;

                        int slot = TryParseSlotNumber(files[i]);
                        bool saveDeleted = false;
                        if (slot > 0)
                        {
                            string saveId = DraftStorageScenarioId + "_" + slot;
                            saveDeleted = _saveLibrary.Delete(DraftStorageScenarioId, saveId);
                        }

                        string draftRoot = Path.GetDirectoryName(files[i]);
                        ScenarioDefinitionMetadataCache.InvalidateUnder(draftRoot);
                        bool draftDeleted = DeleteDraftDirectory(draftRoot);
                        MMLog.WriteInfo("[ScenarioAuthoringDraftRepository] Deleted pending draft '" + draftId + "'. slot=" + slot
                            + " saveDeleted=" + saveDeleted + " draftDeleted=" + draftDeleted
                            + " reason=" + (reason ?? "unspecified") + ".");
                        return saveDeleted || draftDeleted;
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] Failed to delete draft '" + draftId + "': " + ex.Message);
                    }
                }
            }

            return false;
        }

        private static int CompareInfo(ScenarioInfo left, ScenarioInfo right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (name != 0) return name;

            return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        }

        private ScenarioDefinition SafeLoadDefinition(string filePath)
        {
            try
            {
                ScenarioDefinition definition;
                string recoveryMessage;
                bool recovered;
                if (!_serializer.TryLoadWithRecovery(filePath, out definition, out recoveryMessage, out recovered))
                    return null;

                if (recovered)
                    MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] " + recoveryMessage);
                return definition;
            }
            catch { return null; }
        }

        private ScenarioInfo LoadInfoWithRecovery(string filePath)
        {
            ScenarioDefinition definition;
            string recoveryMessage;
            bool recovered;
            if (!_serializer.TryLoadWithRecovery(filePath, out definition, out recoveryMessage, out recovered))
                throw new IOException(string.IsNullOrEmpty(recoveryMessage) ? "Scenario XML could not be loaded." : recoveryMessage);

            if (recovered)
                MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] " + recoveryMessage);

            return new ScenarioInfo(
                definition.Id,
                definition.DisplayName,
                definition.Author,
                definition.Version,
                filePath,
                DraftOwnerId);
        }

        private static bool ScenarioIdExistsOutsideDrafts(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return false;

            try
            {
                IScenarioDefinitionCatalogService catalog = ScenarioCompositionRoot.Resolve<IScenarioDefinitionCatalogService>();
                if (catalog == null)
                    return false;

                ScenarioInfo[] infos = catalog.ListDefinitions();
                for (int i = 0; infos != null && i < infos.Length; i++)
                {
                    ScenarioInfo info = infos[i];
                    if (info != null && string.Equals(info.Id, scenarioId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] Published scenario collision check failed: " + ex.Message);
            }

            return false;
        }

        private static void CopyDraftFolder(string sourceRoot, string destinationRoot)
        {
            if (string.IsNullOrEmpty(sourceRoot) || string.IsNullOrEmpty(destinationRoot) || !Directory.Exists(sourceRoot))
                return;

            string sourceFull = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destinationFull = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(sourceFull, destinationFull, StringComparison.OrdinalIgnoreCase))
                return;

            string[] directories = Directory.GetDirectories(sourceFull, "*", SearchOption.AllDirectories);
            for (int i = 0; i < directories.Length; i++)
            {
                string relative = directories[i].Substring(sourceFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destinationFull, relative));
            }

            string[] files = Directory.GetFiles(sourceFull, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(sourceFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destination = Path.Combine(destinationFull, relative);
                string directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.Copy(files[i], destination, true);
            }
        }

        private static string BuildDuplicateDisplayName(string displayName)
        {
            return (string.IsNullOrEmpty(displayName) ? "Custom Scenario" : displayName) + " Copy";
        }

        private static string NormalizeDraftId(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string trimmed = value.Trim();
            if (trimmed.Length == 0)
                return null;

            char[] invalid = Path.GetInvalidFileNameChars();
            List<char> chars = new List<char>();
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                bool bad = char.IsWhiteSpace(c);
                for (int j = 0; !bad && j < invalid.Length; j++)
                    bad = c == invalid[j];
                chars.Add(bad ? '_' : c);
            }

            return new string(chars.ToArray()).Trim('_', '.');
        }

        private static string CreateDraftId()
        {
            return "smm.authoring." + DateTime.UtcNow.ToString("yyyyMMddHHmmss")
                + "." + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static int GetNextDraftSlot(string draftsRoot)
        {
            int maxSlot = 0;
            if (!Directory.Exists(draftsRoot))
                return 1;

            string[] directories = Directory.GetDirectories(draftsRoot, "Slot_*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < directories.Length; i++)
            {
                string name = Path.GetFileName(directories[i]);
                if (string.IsNullOrEmpty(name) || name.Length <= 5)
                    continue;

                int slot;
                if (int.TryParse(name.Substring(5), out slot) && slot > maxSlot)
                    maxSlot = slot;
            }

            return maxSlot + 1;
        }

        private static string[] EnumerateDraftScenarioFiles(string draftsRoot)
        {
            if (!Directory.Exists(draftsRoot))
                return new string[0];

            List<string> files = new List<string>();
            string[] directories = Directory.GetDirectories(draftsRoot, "Slot_*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < directories.Length; i++)
            {
                string path = Path.Combine(directories[i], ScenarioDefinitionSerializer.DefaultFileName);
                if (File.Exists(path))
                    files.Add(path);
            }

            return files.ToArray();
        }

        private static int TryParseSlotNumber(string scenarioFilePath)
        {
            try
            {
                string slotDirectory = Path.GetFileName(Path.GetDirectoryName(scenarioFilePath));
                if (string.IsNullOrEmpty(slotDirectory) || slotDirectory.Length <= 5 || !slotDirectory.StartsWith("Slot_", StringComparison.OrdinalIgnoreCase))
                    return 0;

                int slot;
                return int.TryParse(slotDirectory.Substring(5), out slot) ? slot : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static string GetDraftsRootPath()
        {
            return GetScenarioRootPath(false);
        }

        private static string EnsureDraftsRoot()
        {
            return GetScenarioRootPath(true);
        }

        private static string EnsureSlotRoot(int slot)
        {
            string path = Path.Combine(EnsureDraftsRoot(), "Slot_" + slot);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        private static bool DeleteDraftDirectory(string draftRoot)
        {
            if (string.IsNullOrEmpty(draftRoot) || !Directory.Exists(draftRoot))
                return false;

            try
            {
                string parent = Path.GetDirectoryName(draftRoot);
                if (string.IsNullOrEmpty(parent))
                    return false;

                string trashRoot = Path.Combine(parent, "_trash");
                if (!Directory.Exists(trashRoot))
                    Directory.CreateDirectory(trashRoot);

                string name = Path.GetFileName(draftRoot) + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string deletedPath = Path.Combine(trashRoot, name);
                while (Directory.Exists(deletedPath))
                    deletedPath = Path.Combine(trashRoot, name + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));

                Directory.Move(draftRoot, deletedPath);
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] Failed to quarantine draft directory '" + draftRoot + "': " + ex.Message);
                return false;
            }
        }

        private static string GetScenarioRootPath(bool create)
        {
            string gameRoot;
            try
            {
                gameRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            }
            catch
            {
                gameRoot = Directory.GetCurrentDirectory();
            }

            string modsRoot = Path.Combine(gameRoot, "mods");
            if (!Directory.Exists(modsRoot))
            {
                string legacyModsRoot = Path.Combine(gameRoot, "Mods");
                modsRoot = Directory.Exists(legacyModsRoot) ? legacyModsRoot : modsRoot;
            }

            string path = Path.Combine(Path.Combine(Path.Combine(Path.Combine(modsRoot, "ModAPI"), "User"), "Saves"), DraftStorageScenarioId);
            if (create && !Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }
    }
}
