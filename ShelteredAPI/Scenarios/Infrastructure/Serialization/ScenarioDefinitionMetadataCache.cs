using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Scenarios;
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
            }
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
