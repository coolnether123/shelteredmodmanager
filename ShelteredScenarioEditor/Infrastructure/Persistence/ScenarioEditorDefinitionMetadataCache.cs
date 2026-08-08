using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Application.Runtime;

namespace ShelteredScenarioEditor.Infrastructure.Persistence
{
    internal delegate bool ScenarioEditorDefinitionRecoveryLoader(
        string filePath,
        out ScenarioDefinition definition,
        out string recoveryMessage,
        out bool recovered);

    internal sealed class ScenarioEditorDefinitionMetadata
    {
        public ScenarioInfo Info;
        public ScenarioBaseGameMode BaseGameMode;
        public string Description;
    }

    /// <summary>Process-local draft/package metadata index owned by the optional editor.</summary>
    internal static class ScenarioEditorDefinitionMetadataCache
    {
        private sealed class Entry
        {
            public long LastWriteTicks;
            public long Length;
            public string OwnerModId;
            public ScenarioEditorDefinitionMetadata Metadata;
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public static void Invalidate(string filePath)
        {
            string fullPath = Normalize(filePath);
            if (fullPath == null) return;
            lock (Sync) { Entries.Remove(fullPath); }
        }

        public static void InvalidateUnder(string directoryPath)
        {
            string fullPath = Normalize(directoryPath);
            if (fullPath == null) return;
            lock (Sync)
            {
                List<string> remove = new List<string>();
                foreach (string key in Entries.Keys)
                    if (key.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) remove.Add(key);
                for (int i = 0; i < remove.Count; i++) Entries.Remove(remove[i]);
            }
        }

        public static bool TryGetCached(string filePath, string ownerModId, out ScenarioEditorDefinitionMetadata metadata)
        {
            metadata = null;
            string fullPath;
            long ticks;
            long length;
            if (!TryStamp(filePath, out fullPath, out ticks, out length)) return false;
            lock (Sync)
            {
                Entry entry;
                if (!Entries.TryGetValue(fullPath, out entry)
                    || entry.LastWriteTicks != ticks
                    || entry.Length != length
                    || !string.Equals(entry.OwnerModId, ownerModId ?? string.Empty, StringComparison.Ordinal)) return false;
                metadata = entry.Metadata;
                return metadata != null;
            }
        }

        public static bool TryGetByScenarioId(string scenarioId, string ownerModId, out ScenarioEditorDefinitionMetadata metadata)
        {
            metadata = null;
            if (string.IsNullOrEmpty(scenarioId)) return false;
            lock (Sync)
            {
                foreach (KeyValuePair<string, Entry> pair in Entries)
                {
                    Entry entry = pair.Value;
                    if (entry != null && entry.Metadata != null && entry.Metadata.Info != null
                        && string.Equals(entry.OwnerModId, ownerModId ?? string.Empty, StringComparison.Ordinal)
                        && string.Equals(entry.Metadata.Info.Id, scenarioId, StringComparison.OrdinalIgnoreCase))
                    {
                        string stampedPath;
                        long ticks;
                        long length;
                        if (!TryStamp(pair.Key, out stampedPath, out ticks, out length)
                            || entry.LastWriteTicks != ticks || entry.Length != length)
                            continue;
                        metadata = entry.Metadata;
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool TryLoad(IScenarioDefinitionSerializer serializer, string filePath, string ownerModId, out ScenarioEditorDefinitionMetadata metadata)
        {
            string ignored;
            bool recovered;
            return TryLoad(serializer, filePath, ownerModId, serializer.TryLoadWithRecovery, out metadata, out ignored, out recovered);
        }

        public static bool TryLoad(
            IScenarioDefinitionSerializer serializer,
            string filePath,
            string ownerModId,
            ScenarioEditorDefinitionRecoveryLoader recoveryLoader,
            out ScenarioEditorDefinitionMetadata metadata,
            out string recoveryMessage,
            out bool recovered)
        {
            metadata = null;
            recoveryMessage = null;
            recovered = false;
            if (serializer == null || recoveryLoader == null) return false;
            if (TryGetCached(filePath, ownerModId, out metadata)) return true;

            ScenarioDefinition definition;
            if (!recoveryLoader(filePath, out definition, out recoveryMessage, out recovered) || definition == null) return false;
            ScenarioInfo info = new ScenarioInfo(
                definition.Id,
                definition.DisplayName,
                definition.Author,
                definition.Version,
                filePath,
                ownerModId);
            metadata = new ScenarioEditorDefinitionMetadata
            {
                Info = info,
                BaseGameMode = definition.BaseGameMode,
                Description = definition.Description
            };
            string fullPath;
            long ticks;
            long length;
            if (TryStamp(filePath, out fullPath, out ticks, out length))
            {
                lock (Sync)
                {
                    Entries[fullPath] = new Entry
                    {
                        LastWriteTicks = ticks,
                        Length = length,
                        OwnerModId = ownerModId ?? string.Empty,
                        Metadata = metadata
                    };
                }
            }
            return info != null;
        }

        private static bool TryStamp(string filePath, out string fullPath, out long ticks, out long length)
        {
            fullPath = Normalize(filePath);
            ticks = 0;
            length = 0;
            if (fullPath == null || !File.Exists(fullPath)) return false;
            FileInfo info = new FileInfo(fullPath);
            ticks = info.LastWriteTimeUtc.Ticks;
            length = info.Length;
            return true;
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { return null; }
        }
    }
}
