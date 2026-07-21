using System;
using System.Collections.Generic;

namespace ShelteredAPI.Saves.Backups
{
    internal enum SaveBackupRetentionMode
    {
        Disabled = 0,
        Limited = 1,
        Forever = 2
    }

    internal enum SaveBackupReason
    {
        BeforeOverwrite = 0,
        Manual = 1,
        BeforeRestore = 2,
        BeforeDelete = 3
    }

    internal enum SaveBackupSourceKind
    {
        File = 0,
        Directory = 1
    }

    internal enum SaveBackupSnapshotSortOrder
    {
        NewestFirst = 0,
        OldestFirst = 1
    }

    internal sealed class SaveBackupRetentionPolicy
    {
        public SaveBackupRetentionMode Mode;
        public int SnapshotLimit;

        public bool IsEnabled
        {
            get { return Mode != SaveBackupRetentionMode.Disabled; }
        }

        public static SaveBackupRetentionPolicy Default()
        {
            return new SaveBackupRetentionPolicy
            {
                Mode = SaveBackupRetentionMode.Limited,
                SnapshotLimit = 10
            };
        }
    }

    internal sealed class SaveBackupTarget
    {
        public string TimelineKey;
        public string SaveKind;
        public string ScenarioId;
        public int AbsoluteSlot;
        public string SaveId;
        public SaveManager.SaveType SaveType;
        public readonly List<SaveBackupSource> Sources = new List<SaveBackupSource>();
    }

    internal sealed class SaveBackupSource
    {
        public string Id;
        public string Path;
        public SaveBackupSourceKind Kind;
    }

    internal sealed class SaveBackupRestoreDestination
    {
        public string ScenarioId;
        public int AbsoluteSlot;
        public string ExpectedLineageId;
        public bool AllowHistoricalSlotWhenUnoccupied;
    }

    internal sealed class SaveBackupFileRecord
    {
        public string SourceId;
        public string RelativePath;
        public string Hash;
        public long Size;
        public uint Crc32;
        public string BlobPath;
        public string Compression;
    }

    internal sealed class SaveBackupSnapshotRef
    {
        public string SnapshotId;
        public string TimelineKey;
        public string ManifestPath;
        public DateTime CreatedAtUtc;
        public bool IsPinned;
    }

    internal sealed class SaveBackupSnapshotInfo
    {
        public SaveBackupSnapshotRef Ref;
        public SaveEntry Entry;
        public SlotManifest SlotManifest;
        public string SaveKind;
        public string ScenarioId;
        public int AbsoluteSlot;
        public string SaveId;
        public SaveManager.SaveType SaveType;

        public bool IsVanilla
        {
            get { return string.Equals(SaveKind, "VanillaSlot", StringComparison.OrdinalIgnoreCase); }
        }
    }
}
