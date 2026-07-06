using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Infrastructure.Serialization
{
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

            string key = fullPath + "|" + (ownerModId ?? string.Empty);
            lock (Sync)
            {
                CacheEntry cached;
                if (Entries.TryGetValue(key, out cached)
                    && cached != null
                    && cached.LastWriteTicks == lastWriteTicks
                    && cached.Length == length)
                {
                    metadata = cached.Metadata;
                    return metadata != null;
                }
            }

            ScenarioDefinition definition = loadDefinition();
            ScenarioDefinitionMetadata loaded = BuildMetadata(definition, fullPath, ownerModId);

            lock (Sync)
            {
                Entries[key] = new CacheEntry
                {
                    LastWriteTicks = lastWriteTicks,
                    Length = length,
                    Metadata = loaded
                };
            }

            metadata = loaded;
            return metadata != null;
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
