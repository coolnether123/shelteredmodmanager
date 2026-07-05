using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
namespace ShelteredAPI.Scenarios.Definitions{
    /// <summary>
    /// Indexes Sheltered scenario.xml files from each loaded mod's Scenarios folder.
    /// </summary>
    internal sealed class ScenarioCatalog
    {
        private readonly IScenarioModFolderSource _modFolderSource;
        private readonly ScenarioDefinitionSerializer _serializer;
        private readonly object _sync = new object();
        private Dictionary<string, ScenarioInfo> _byId = new Dictionary<string, ScenarioInfo>(StringComparer.OrdinalIgnoreCase);
        private bool _scanned;

        public ScenarioCatalog()
            : this(new ModRegistryScenarioModFolderSource(), new ScenarioDefinitionSerializer())
        {
        }

        public ScenarioCatalog(IScenarioModFolderSource modFolderSource, ScenarioDefinitionSerializer serializer)
        {
            _modFolderSource = modFolderSource;
            _serializer = serializer ?? new ScenarioDefinitionSerializer();
        }

        public void Refresh()
        {
            Dictionary<string, ScenarioInfo> next = new Dictionary<string, ScenarioInfo>(StringComparer.OrdinalIgnoreCase);
            ScenarioModFolder[] folders = _modFolderSource != null ? _modFolderSource.GetLoadedModFolders() : new ScenarioModFolder[0];

            for (int i = 0; i < folders.Length; i++)
            {
                ScenarioModFolder folder = folders[i];
                if (folder == null || string.IsNullOrEmpty(folder.RootPath))
                    continue;

                string scenariosRoot = Path.Combine(folder.RootPath, "Scenarios");
                if (!Directory.Exists(scenariosRoot))
                    continue;

                string[] files;
                try { files = Directory.GetFiles(scenariosRoot, ScenarioDefinitionSerializer.DefaultFileName, SearchOption.AllDirectories); }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[ScenarioCatalog] Failed to scan '" + scenariosRoot + "': " + ex.Message);
                    continue;
                }

                for (int j = 0; j < files.Length; j++)
                {
                    if (IsAuthoringDraftScenarioFile(files[j]))
                    {
                        MMLog.WriteInfo("[ScenarioCatalog] Skipping authoring draft scenario file outside playable catalog: " + files[j]);
                        continue;
                    }

                    TryAddScenario(next, files[j], folder.ModId);
                }
            }

            lock (_sync)
            {
                _byId = next;
                _scanned = true;
            }
        }

        public ScenarioInfo[] ListAll()
        {
            EnsureScanned();
            List<ScenarioInfo> items = new List<ScenarioInfo>();
            lock (_sync)
            {
                foreach (KeyValuePair<string, ScenarioInfo> pair in _byId)
                    items.Add(pair.Value);
            }

            items.Sort(CompareInfo);
            return items.ToArray();
        }

        public bool TryGet(string scenarioId, out ScenarioInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(scenarioId))
                return false;

            EnsureScanned();
            lock (_sync)
            {
                return _byId.TryGetValue(scenarioId, out info);
            }
        }

        private void TryAddScenario(Dictionary<string, ScenarioInfo> target, string filePath, string ownerModId)
        {
            try
            {
                ScenarioInfo info = _serializer.LoadInfo(filePath, ownerModId);
                if (info == null || string.IsNullOrEmpty(info.Id))
                {
                    MMLog.WriteWarning("[ScenarioCatalog] Skipping scenario without an Id: " + filePath);
                    return;
                }

                if (target.ContainsKey(info.Id))
                {
                    MMLog.WriteWarning("[ScenarioCatalog] Duplicate scenario id '" + info.Id + "' at " + filePath + ". Keeping first occurrence.");
                    return;
                }

                target.Add(info.Id, info);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioCatalog] Skipping invalid scenario file '" + filePath + "': " + ex.Message);
            }
        }

        private void EnsureScanned()
        {
            lock (_sync)
            {
                if (_scanned)
                    return;
            }

            Refresh();
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

        private static bool IsAuthoringDraftScenarioFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            try
            {
                string fullPath = Path.GetFullPath(filePath);
                string marker = Path.DirectorySeparatorChar
                    + ScenarioAuthoringDraftRepository.DraftStorageScenarioId
                    + Path.DirectorySeparatorChar;
                if (fullPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                string parent = Path.GetFileName(Path.GetDirectoryName(fullPath));
                return !string.IsNullOrEmpty(parent)
                    && parent.StartsWith("Slot_", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return filePath.IndexOf(ScenarioAuthoringDraftRepository.DraftStorageScenarioId, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
