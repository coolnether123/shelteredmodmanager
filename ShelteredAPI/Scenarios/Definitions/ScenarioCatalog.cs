using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
namespace ShelteredAPI.Scenarios.Definitions{
    internal sealed class ScenarioCatalogPathStamp
    {
        public bool Exists;
        public long LastWriteTicks;
        public long Length;
    }

    internal static class ScenarioCatalogDiskStamp
    {
        public static ScenarioCatalogPathStamp ReadDirectory(string path)
        {
            try
            {
                DirectoryInfo info = new DirectoryInfo(path);
                return new ScenarioCatalogPathStamp
                {
                    Exists = info.Exists,
                    LastWriteTicks = info.Exists ? info.LastWriteTimeUtc.Ticks : 0L
                };
            }
            catch
            {
                return new ScenarioCatalogPathStamp();
            }
        }

        public static ScenarioCatalogPathStamp ReadFile(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                return new ScenarioCatalogPathStamp
                {
                    Exists = info.Exists,
                    LastWriteTicks = info.Exists ? info.LastWriteTimeUtc.Ticks : 0L,
                    Length = info.Exists ? info.Length : 0L
                };
            }
            catch
            {
                return new ScenarioCatalogPathStamp();
            }
        }

        public static bool Equal(ScenarioCatalogPathStamp left, ScenarioCatalogPathStamp right)
        {
            return left != null
                && right != null
                && left.Exists == right.Exists
                && left.LastWriteTicks == right.LastWriteTicks
                && left.Length == right.Length;
        }

        public static string NormalizePath(string path)
        {
            try { return Path.GetFullPath(path); }
            catch { return path ?? string.Empty; }
        }
    }

    /// <summary>
    /// Indexes Sheltered scenario.xml files from each loaded mod's Scenarios folder.
    /// </summary>
    internal sealed class ScenarioCatalog
    {
        private readonly IScenarioModFolderSource _modFolderSource;
        private readonly ScenarioDefinitionSerializer _serializer;
        private readonly object _sync = new object();
        private Dictionary<string, ScenarioInfo> _byId = new Dictionary<string, ScenarioInfo>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, ScenarioCatalogPathStamp> _watchedDirectories = new Dictionary<string, ScenarioCatalogPathStamp>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, ScenarioCatalogPathStamp> _watchedFiles = new Dictionary<string, ScenarioCatalogPathStamp>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _scenarioRoots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
            ScenarioModFolder[] folders = _modFolderSource != null ? _modFolderSource.GetLoadedModFolders() : new ScenarioModFolder[0];
            Dictionary<string, string> roots = BuildScenarioRoots(folders);

            lock (_sync)
            {
                if (IsSnapshotCurrent(roots))
                    return;

                Dictionary<string, ScenarioInfo> next = new Dictionary<string, ScenarioInfo>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, ScenarioCatalogPathStamp> directories = new Dictionary<string, ScenarioCatalogPathStamp>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, ScenarioCatalogPathStamp> files = new Dictionary<string, ScenarioCatalogPathStamp>(StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, string> root in roots)
                {
                    string scenariosRoot = root.Key;
                    directories[scenariosRoot] = ScenarioCatalogDiskStamp.ReadDirectory(scenariosRoot);
                    if (!Directory.Exists(scenariosRoot))
                        continue;

                    string[] discoveredDirectories;
                    string[] discoveredFiles;
                    try
                    {
                        discoveredDirectories = Directory.GetDirectories(scenariosRoot, "*", SearchOption.AllDirectories);
                        discoveredFiles = Directory.GetFiles(scenariosRoot, "*", SearchOption.AllDirectories);
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteWarning("[ScenarioCatalog] Failed to scan '" + scenariosRoot + "': " + ex.Message);
                        continue;
                    }

                    for (int i = 0; i < discoveredDirectories.Length; i++)
                        directories[ScenarioCatalogDiskStamp.NormalizePath(discoveredDirectories[i])] = ScenarioCatalogDiskStamp.ReadDirectory(discoveredDirectories[i]);

                    for (int i = 0; i < discoveredFiles.Length; i++)
                    {
                        string filePath = ScenarioCatalogDiskStamp.NormalizePath(discoveredFiles[i]);
                        files[filePath] = ScenarioCatalogDiskStamp.ReadFile(filePath);
                        if (!string.Equals(Path.GetFileName(filePath), ScenarioDefinitionSerializer.DefaultFileName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (IsAuthoringDraftScenarioFile(filePath))
                        {
                            MMLog.WriteInfo("[ScenarioCatalog] Skipping authoring draft scenario file outside playable catalog: " + filePath);
                            continue;
                        }

                        TryAddScenario(next, filePath, root.Value);
                    }
                }

                _byId = next;
                _scenarioRoots = roots;
                _watchedDirectories = directories;
                _watchedFiles = files;
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

        private bool IsSnapshotCurrent(Dictionary<string, string> roots)
        {
            if (!_scanned || roots == null || roots.Count != _scenarioRoots.Count)
                return false;

            foreach (KeyValuePair<string, string> root in roots)
            {
                string ownerModId;
                if (!_scenarioRoots.TryGetValue(root.Key, out ownerModId)
                    || !string.Equals(ownerModId, root.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return StampsMatch(_watchedDirectories, true) && StampsMatch(_watchedFiles, false);
        }

        private static bool StampsMatch(Dictionary<string, ScenarioCatalogPathStamp> expected, bool directory)
        {
            foreach (KeyValuePair<string, ScenarioCatalogPathStamp> pair in expected)
            {
                ScenarioCatalogPathStamp current = directory
                    ? ScenarioCatalogDiskStamp.ReadDirectory(pair.Key)
                    : ScenarioCatalogDiskStamp.ReadFile(pair.Key);
                if (!ScenarioCatalogDiskStamp.Equal(pair.Value, current))
                    return false;
            }

            return true;
        }

        private static Dictionary<string, string> BuildScenarioRoots(ScenarioModFolder[] folders)
        {
            Dictionary<string, string> roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; folders != null && i < folders.Length; i++)
            {
                ScenarioModFolder folder = folders[i];
                if (folder == null || string.IsNullOrEmpty(folder.RootPath))
                    continue;

                roots[ScenarioCatalogDiskStamp.NormalizePath(Path.Combine(folder.RootPath, "Scenarios"))] = folder.ModId;
            }

            return roots;
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
