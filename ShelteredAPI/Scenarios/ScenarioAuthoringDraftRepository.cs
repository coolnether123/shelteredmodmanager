using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ModAPI.Saves;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
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
        private static readonly ScenarioAuthoringDraftRepository _instance = new ScenarioAuthoringDraftRepository();
        private readonly object _sync = new object();
        private readonly ScenarioDefinitionSerializer _serializer = new ScenarioDefinitionSerializer();

        public static ScenarioAuthoringDraftRepository Instance
        {
            get { return _instance; }
        }

        private ScenarioAuthoringDraftRepository()
        {
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
                int nextSaveSlot = ScenarioSaves.GetNextAvailableSlot(DraftStorageScenarioId);
                if (nextSaveSlot > slot)
                    slot = nextSaveSlot;

                string draftRoot = EnsureSlotRoot(slot);
                while (Directory.Exists(draftRoot) && File.Exists(Path.Combine(draftRoot, ScenarioDefinitionSerializer.DefaultFileName)))
                {
                    scenarioId = CreateDraftId();
                    slot++;
                    draftRoot = EnsureSlotRoot(slot);
                }

                SaveEntry startupSave = ScenarioSaves.CreateNext(DraftStorageScenarioId, new SaveCreateOptions
                {
                    name = scenarioId,
                    absoluteSlot = slot
                });
                if (startupSave == null)
                    throw new InvalidOperationException("Could not allocate the draft startup save entry.");

                ScenarioDefinition definition = new ScenarioDefinition();
                definition.Id = scenarioId;
                definition.DisplayName = "New Custom Scenario";
                definition.Description = "Local scenario authoring draft.";
                definition.Author = "SMM Authoring";
                definition.Version = "0.1.0";
                definition.BaseGameMode = baseMode;

                string scenarioFilePath = Path.Combine(draftRoot, ScenarioDefinitionSerializer.DefaultFileName);
                _serializer.Save(definition, scenarioFilePath);
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
                        ScenarioInfo info = _serializer.LoadInfo(files[i], DraftOwnerId);
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
                        ScenarioInfo loaded = _serializer.LoadInfo(files[i], DraftOwnerId);
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
                        ScenarioInfo loaded = _serializer.LoadInfo(files[i], DraftOwnerId);
                        if (loaded == null || !string.Equals(loaded.Id, draftId, StringComparison.OrdinalIgnoreCase))
                            continue;

                        int slot = TryParseSlotNumber(files[i]);
                        if (slot <= 0)
                            return false;

                        string saveId = DraftStorageScenarioId + "_" + slot;
                        entry = ScenarioSaves.Get(DraftStorageScenarioId, saveId);
                        return entry != null;
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteWarning("[ScenarioAuthoringDraftRepository] Failed to resolve draft save entry for '" + draftId + "': " + ex.Message);
                    }
                }
            }

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
                        ScenarioInfo loaded = _serializer.LoadInfo(files[i], DraftOwnerId);
                        if (loaded == null || !string.Equals(loaded.Id, draftId, StringComparison.OrdinalIgnoreCase))
                            continue;

                        int slot = TryParseSlotNumber(files[i]);
                        bool saveDeleted = false;
                        if (slot > 0)
                        {
                            string saveId = DraftStorageScenarioId + "_" + slot;
                            saveDeleted = ScenarioSaves.Delete(DraftStorageScenarioId, saveId);
                        }

                        string draftRoot = Path.GetDirectoryName(files[i]);
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
                gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
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
