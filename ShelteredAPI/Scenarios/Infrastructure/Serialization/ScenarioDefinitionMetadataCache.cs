using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using System.Threading;
using ModAPI.Scenarios;
using ModAPI.Util;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Infrastructure.Serialization
{
    internal delegate bool ScenarioDefinitionRecoveryLoader(
        string filePath,
        out ScenarioDefinition definition,
        out string recoveryMessage,
        out bool recovered);

    internal sealed class ScenarioDefinitionMetadata
    {
        public ScenarioInfo Info;
        public ScenarioBaseGameMode BaseGameMode;
        public string Description;
    }

    internal static class ScenarioDefinitionMetadataCache
    {
        private sealed class CacheEntry
        {
            public long LastWriteTicks;
            public long Length;
            public ScenarioDefinitionMetadata Metadata;
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, CacheEntry> Entries = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static string _persistentStorePath;
        private static string _persistentOwnerModId;
        private static int _persistenceGeneration;
        private static bool _persistenceWriterRunning;

        internal static void ConfigurePersistentStore(string filePath, string ownerModId)
        {
            string fullPath;
            try { fullPath = string.IsNullOrEmpty(filePath) ? null : Path.GetFullPath(filePath); }
            catch { fullPath = null; }

            lock (Sync)
            {
                if (string.Equals(_persistentStorePath, fullPath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(_persistentOwnerModId, ownerModId, StringComparison.Ordinal))
                    return;

                Entries.Clear();
                _persistentStorePath = fullPath;
                _persistentOwnerModId = ownerModId ?? string.Empty;
                _persistenceGeneration++;
            }

            LoadPersistentEntries(fullPath, ownerModId);
        }

        internal static void GetPersistentStoreConfiguration(out string filePath, out string ownerModId)
        {
            lock (Sync)
            {
                filePath = _persistentStorePath;
                ownerModId = _persistentOwnerModId;
            }
        }

        public static void Invalidate(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            string fullPath;
            try { fullPath = Path.GetFullPath(filePath); }
            catch { return; }

            lock (Sync)
            {
                List<string> remove = new List<string>();
                foreach (KeyValuePair<string, CacheEntry> pair in Entries)
                {
                    if (pair.Key != null && pair.Key.StartsWith(fullPath + "|", StringComparison.OrdinalIgnoreCase))
                        remove.Add(pair.Key);
                }

                for (int i = 0; i < remove.Count; i++)
                    Entries.Remove(remove[i]);

                if (remove.Count > 0)
                    SchedulePersistenceLocked();
            }
        }

        public static void InvalidateUnder(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;

            string fullPath;
            try { fullPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { return; }

            lock (Sync)
            {
                List<string> remove = new List<string>();
                foreach (KeyValuePair<string, CacheEntry> pair in Entries)
                {
                    string key = pair.Key;
                    if (!string.IsNullOrEmpty(key)
                        && (key.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                            || key.StartsWith(fullPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    {
                        remove.Add(key);
                    }
                }

                for (int i = 0; i < remove.Count; i++)
                    Entries.Remove(remove[i]);

                if (remove.Count > 0)
                    SchedulePersistenceLocked();
            }
        }

        internal static bool TryGetByScenarioId(string scenarioId, string ownerModId, out ScenarioDefinitionMetadata metadata)
        {
            metadata = null;
            if (string.IsNullOrEmpty(scenarioId))
                return false;

            List<KeyValuePair<string, CacheEntry>> candidates = new List<KeyValuePair<string, CacheEntry>>();
            string ownerSuffix = "|" + (ownerModId ?? string.Empty);
            lock (Sync)
            {
                foreach (KeyValuePair<string, CacheEntry> pair in Entries)
                {
                    CacheEntry entry = pair.Value;
                    if (entry != null && entry.Metadata != null && entry.Metadata.Info != null
                        && pair.Key.EndsWith(ownerSuffix, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(entry.Metadata.Info.Id, scenarioId, StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(pair);
                    }
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                KeyValuePair<string, CacheEntry> candidate = candidates[i];
                string path = candidate.Value.Metadata.Info.FilePath;
                string fullPath;
                long lastWriteTicks;
                long length;
                if (!TryGetFileStamp(path, out fullPath, out lastWriteTicks, out length)
                    || candidate.Value.LastWriteTicks != lastWriteTicks
                    || candidate.Value.Length != length)
                {
                    RemoveEntry(candidate.Key);
                    continue;
                }

                metadata = candidate.Value.Metadata;
                return true;
            }

            return false;
        }

        internal static bool TryGetCached(string filePath, string ownerModId, out ScenarioDefinitionMetadata metadata)
        {
            metadata = null;
            string fullPath;
            long lastWriteTicks;
            long length;
            if (!TryGetFileStamp(filePath, out fullPath, out lastWriteTicks, out length))
                return false;

            string key;
            return TryGetCachedMetadata(fullPath, ownerModId, lastWriteTicks, length, out key, out metadata);
        }

        public static bool TryLoad(
            ScenarioDefinitionSerializer serializer,
            string filePath,
            string ownerModId,
            out ScenarioDefinitionMetadata metadata)
        {
            if (serializer == null)
                serializer = new ScenarioDefinitionSerializer();

            return TryLoad(
                delegate { return serializer.LoadUncached(filePath); },
                filePath,
                ownerModId,
                out metadata);
        }

        public static bool TryLoad(
            ScenarioDefinitionSerializer serializer,
            string filePath,
            string ownerModId,
            ScenarioDefinitionRecoveryLoader recoveryLoader,
            out ScenarioDefinitionMetadata metadata,
            out string recoveryMessage,
            out bool recovered)
        {
            if (serializer == null)
                serializer = new ScenarioDefinitionSerializer();

            metadata = null;
            recoveryMessage = null;
            recovered = false;
            if (recoveryLoader == null || string.IsNullOrEmpty(filePath))
                return false;

            string fullPath;
            long lastWriteTicks;
            long length;
            if (!TryGetFileStamp(filePath, out fullPath, out lastWriteTicks, out length))
                return false;

            string key;
            if (TryGetCachedMetadata(fullPath, ownerModId, lastWriteTicks, length, out key, out metadata))
                return true;

            ScenarioDefinition definition;
            try
            {
                definition = serializer.LoadUncached(filePath);
            }
            catch
            {
                if (!recoveryLoader(filePath, out definition, out recoveryMessage, out recovered) || definition == null)
                    return false;

                // Recovery may replace the primary XML. Never publish its metadata
                // under the stamp captured for the unreadable pre-recovery file.
                Invalidate(fullPath);
                if (!TryGetFileStamp(fullPath, out fullPath, out lastWriteTicks, out length))
                {
                    metadata = BuildMetadata(definition, filePath, ownerModId);
                    return metadata != null;
                }

                key = fullPath + "|" + (ownerModId ?? string.Empty);
            }

            ScenarioDefinitionMetadata loaded = BuildMetadata(definition, fullPath, ownerModId);
            Publish(key, lastWriteTicks, length, loaded);
            metadata = loaded;
            return metadata != null;
        }

        public static bool TryLoad(
            IScenarioDefinitionSerializer serializer,
            string filePath,
            string ownerModId,
            out ScenarioDefinitionMetadata metadata)
        {
            if (serializer == null)
            {
                metadata = null;
                return false;
            }

            return TryLoad(
                delegate { return serializer.Load(filePath); },
                filePath,
                ownerModId,
                out metadata);
        }

        private static bool TryLoad(
            Func<ScenarioDefinition> loadDefinition,
            string filePath,
            string ownerModId,
            out ScenarioDefinitionMetadata metadata)
        {
            metadata = null;
            if (loadDefinition == null || string.IsNullOrEmpty(filePath))
                return false;

            string fullPath;
            long lastWriteTicks;
            long length;
            if (!TryGetFileStamp(filePath, out fullPath, out lastWriteTicks, out length))
                return false;

            string key;
            if (TryGetCachedMetadata(fullPath, ownerModId, lastWriteTicks, length, out key, out metadata))
                return true;

            ScenarioDefinition definition = loadDefinition();
            ScenarioDefinitionMetadata loaded = BuildMetadata(definition, fullPath, ownerModId);
            Publish(key, lastWriteTicks, length, loaded);

            metadata = loaded;
            return metadata != null;
        }

        private static bool TryGetCachedMetadata(
            string fullPath,
            string ownerModId,
            long lastWriteTicks,
            long length,
            out string key,
            out ScenarioDefinitionMetadata metadata)
        {
            key = fullPath + "|" + (ownerModId ?? string.Empty);
            metadata = null;
            lock (Sync)
            {
                CacheEntry cached;
                if (!Entries.TryGetValue(key, out cached)
                    || cached == null
                    || cached.LastWriteTicks != lastWriteTicks
                    || cached.Length != length)
                {
                    return false;
                }

                metadata = cached.Metadata;
                return metadata != null;
            }
        }

        private static void Publish(string key, long lastWriteTicks, long length, ScenarioDefinitionMetadata metadata)
        {
            lock (Sync)
            {
                Entries[key] = new CacheEntry
                {
                    LastWriteTicks = lastWriteTicks,
                    Length = length,
                    Metadata = metadata
                };
                SchedulePersistenceLocked();
            }
        }

        private static void RemoveEntry(string key)
        {
            lock (Sync)
            {
                if (Entries.Remove(key))
                    SchedulePersistenceLocked();
            }
        }

        private static void SchedulePersistenceLocked()
        {
            if (string.IsNullOrEmpty(_persistentStorePath))
                return;

            _persistenceGeneration++;
            if (_persistenceWriterRunning)
                return;

            _persistenceWriterRunning = true;
            ThreadPool.QueueUserWorkItem(delegate { RunPersistenceWriter(); });
        }

        private static void RunPersistenceWriter()
        {
            while (true)
            {
                int observed;
                lock (Sync) { observed = _persistenceGeneration; }
                Thread.Sleep(400);

                lock (Sync)
                {
                    if (observed != _persistenceGeneration)
                        continue;
                }

                WritePersistentSnapshot();
                lock (Sync)
                {
                    if (observed == _persistenceGeneration)
                    {
                        _persistenceWriterRunning = false;
                        return;
                    }
                }
            }
        }

        internal static void FlushPersistentStoreForVerification()
        {
            WritePersistentSnapshot();
        }

        private static void LoadPersistentEntries(string filePath, string ownerModId)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            Dictionary<string, CacheEntry> loaded = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
            try
            {
                ManualJsonObject root;
                string error;
                if (!ManualJson.TryParseObject(File.ReadAllText(filePath), out root, out error))
                    return;

                ManualJsonArray entries = root.GetArray("entries");
                if (entries == null)
                    return;

                for (int i = 0; i < entries.Items.Count; i++)
                {
                    ManualJsonValue value = entries.Items[i];
                    ManualJsonObject item = value != null && value.Type == ManualJsonValueType.Object ? value.ObjectValue : null;
                    CacheEntry entry;
                    string path;
                    if (!TryReadPersistentEntry(item, ownerModId, out path, out entry))
                        continue;

                    loaded[path + "|" + (ownerModId ?? string.Empty)] = entry;
                }
            }
            catch
            {
                return;
            }

            lock (Sync)
            {
                if (!string.Equals(_persistentStorePath, filePath, StringComparison.OrdinalIgnoreCase))
                    return;

                foreach (KeyValuePair<string, CacheEntry> pair in loaded)
                    Entries[pair.Key] = pair.Value;
            }
        }

        private static bool TryReadPersistentEntry(ManualJsonObject item, string ownerModId, out string path, out CacheEntry entry)
        {
            path = null;
            entry = null;
            if (item == null)
                return false;

            path = item.GetString("path", null);
            string id = item.GetString("id", null);
            long ticks;
            long length;
            int baseGameMode;
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(id)
                || !TryGetLong(item, "lastWriteTicks", out ticks)
                || !TryGetLong(item, "length", out length)
                || !TryGetInt(item, "baseGameMode", out baseGameMode))
                return false;

            try { path = Path.GetFullPath(path); }
            catch { return false; }

            ScenarioBaseGameMode mode = Enum.IsDefined(typeof(ScenarioBaseGameMode), baseGameMode)
                ? (ScenarioBaseGameMode)baseGameMode
                : ScenarioBaseGameMode.Survival;
            entry = new CacheEntry
            {
                LastWriteTicks = ticks,
                Length = length,
                Metadata = new ScenarioDefinitionMetadata
                {
                    Info = new ScenarioInfo(
                        id,
                        item.GetString("displayName", null),
                        item.GetString("author", null),
                        item.GetString("version", null),
                        path,
                        ownerModId),
                    BaseGameMode = mode,
                    Description = item.GetString("description", null)
                }
            };
            return true;
        }

        private static bool TryGetLong(ManualJsonObject item, string name, out long value)
        {
            value = 0L;
            ManualJsonValue raw = item.Get(name);
            return raw != null && raw.Type == ManualJsonValueType.Number
                && long.TryParse(raw.NumberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetInt(ManualJsonObject item, string name, out int value)
        {
            value = 0;
            ManualJsonValue raw = item.Get(name);
            return raw != null && raw.Type == ManualJsonValueType.Number
                && int.TryParse(raw.NumberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static void WritePersistentSnapshot()
        {
            string path;
            string ownerModId;
            List<KeyValuePair<string, CacheEntry>> snapshot = new List<KeyValuePair<string, CacheEntry>>();
            lock (Sync)
            {
                path = _persistentStorePath;
                ownerModId = _persistentOwnerModId;
                if (string.IsNullOrEmpty(path))
                    return;

                string suffix = "|" + (ownerModId ?? string.Empty);
                foreach (KeyValuePair<string, CacheEntry> pair in Entries)
                {
                    if (pair.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                        && pair.Value != null && pair.Value.Metadata != null && pair.Value.Metadata.Info != null)
                        snapshot.Add(pair);
                }
            }

            ManualJsonObject root = new ManualJsonObject();
            root.Set("version", ManualJsonValue.Number(1));
            ManualJsonArray items = new ManualJsonArray();
            for (int i = 0; i < snapshot.Count; i++)
            {
                CacheEntry cache = snapshot[i].Value;
                ScenarioDefinitionMetadata metadata = cache.Metadata;
                ScenarioInfo info = metadata.Info;
                ManualJsonObject item = new ManualJsonObject();
                item.Set("path", ManualJsonValue.String(info.FilePath));
                item.Set("lastWriteTicks", ManualJsonValue.Number(cache.LastWriteTicks));
                item.Set("length", ManualJsonValue.Number(cache.Length));
                item.Set("id", ManualJsonValue.String(info.Id));
                item.Set("displayName", ManualJsonValue.String(info.DisplayName));
                item.Set("author", ManualJsonValue.String(info.Author));
                item.Set("version", ManualJsonValue.String(info.Version));
                item.Set("baseGameMode", ManualJsonValue.Number((int)metadata.BaseGameMode));
                item.Set("description", ManualJsonValue.String(metadata.Description));
                items.Add(ManualJsonValue.Object(item));
            }
            root.Set("entries", ManualJsonValue.Array(items));

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string temporary = path + ".tmp";
                File.WriteAllText(temporary, ManualJson.Serialize(root, false), Encoding.UTF8);
                if (File.Exists(path))
                {
                    string backup = path + ".bak";
                    try
                    {
                        File.Replace(temporary, path, backup);
                        if (File.Exists(backup)) File.Delete(backup);
                    }
                    catch
                    {
                        File.Delete(path);
                        File.Move(temporary, path);
                    }
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            catch
            {
                // The cache is an optimization. Persistence failures must never
                // prevent scenario discovery or authoring startup.
            }
        }

        private static bool TryGetFileStamp(string filePath, out string fullPath, out long lastWriteTicks, out long length)
        {
            fullPath = null;
            lastWriteTicks = 0L;
            length = 0L;

            try
            {
                fullPath = Path.GetFullPath(filePath);
                FileInfo info = new FileInfo(fullPath);
                if (!info.Exists)
                    return false;

                lastWriteTicks = info.LastWriteTimeUtc.Ticks;
                length = info.Length;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static ScenarioDefinitionMetadata BuildMetadata(ScenarioDefinition definition, string filePath, string ownerModId)
        {
            if (definition == null)
                return null;

            return new ScenarioDefinitionMetadata
            {
                Info = new ScenarioInfo(
                    definition.Id,
                    definition.DisplayName,
                    definition.Author,
                    definition.Version,
                    filePath,
                    ownerModId),
                BaseGameMode = Enum.IsDefined(typeof(ScenarioBaseGameMode), definition.BaseGameMode)
                    ? definition.BaseGameMode
                    : ScenarioBaseGameMode.Survival,
                Description = definition.Description
            };
        }
    }
}
