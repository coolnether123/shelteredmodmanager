using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.Saves.Runtime;

namespace ShelteredAPI.Saves.Backups
{
    internal static class SaveBackupService
    {
        private const string RetentionKey = "SaveBackupRetention";
        private const string LegacyRetentionModeKey = "SaveBackupRetentionMode";
        private const string LegacyRetentionCountKey = "SaveBackupRetentionCount";
        private const string BranchMarkerDirectoryName = "branches";
        private static readonly object Sync = new object();
        private static readonly HashSet<string> CurrentSavePassSnapshots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void ClearCurrentSavePass()
        {
            lock (Sync)
            {
                CurrentSavePassSnapshots.Clear();
            }
        }

        internal static void BackupCurrentSlotBeforeSave(SaveManager manager)
        {
            try
            {
                if (SaveRuntimeState.HasActiveCustomSave || SaveRuntimeState.HasAnyPendingSave())
                {
                    MMLog.WriteDebug("[SaveBackup] Manager pre-save backup skipped for custom save context; custom router will capture the overwrite.");
                    return;
                }

                SaveManager.SaveType type = ResolveCurrentSaveType(manager);
                if (IsReservedSaveType(type))
                    return;

                SaveBackupTarget target;
                if (TryCreateVanillaTarget(type, out target))
                {
                    BackupTargetBeforeOverwrite(target, false);
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Pre-save backup skipped: " + ex.Message);
            }
        }

        internal static bool BackupCustomEntryBeforeOverwrite(SaveEntry entry)
        {
            try
            {
                if (!ReadRetentionPolicy().IsEnabled)
                    return true;

                SaveBackupTarget target;
                if (!TryCreateCustomTarget(entry, out target))
                {
                    string scenarioId = entry != null
                        ? SaveStorageRouter.NormalizeScenarioId(entry.scenarioId)
                        : string.Empty;
                    string savePath = entry != null && entry.absoluteSlot > 0
                        ? DirectoryProvider.EntryPath(scenarioId, entry.absoluteSlot, false)
                        : string.Empty;
                    MMLog.WriteInfo("[SaveBackup] Custom overwrite snapshot skipped. " + DescribeCustomTargetFailure(entry));
                    return string.IsNullOrEmpty(savePath) || !File.Exists(savePath);
                }

                if (WasCapturedInCurrentSavePass(target.TimelineKey))
                    return true;

                string snapshotId = BackupTargetBeforeOverwrite(target, true);
                MMLog.WriteInfo("[SaveBackup] Custom overwrite snapshot request. scenario="
                    + target.ScenarioId + ", absoluteSlot=" + target.AbsoluteSlot
                    + ", saveId=" + target.SaveId + ", timeline=" + target.TimelineKey
                    + ", created=" + (!string.IsNullOrEmpty(snapshotId) ? snapshotId : "<none>") + ".");
                return !string.IsNullOrEmpty(snapshotId);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Custom overwrite backup skipped: " + ex.Message);
                return false;
            }
        }

        internal static bool BackupVanillaBeforeOverwrite(SaveManager.SaveType type)
        {
            try
            {
                if (!ReadRetentionPolicy().IsEnabled)
                    return true;

                SaveBackupTarget target;
                if (TryCreateVanillaTarget(type, out target))
                {
                    if (WasCapturedInCurrentSavePass(target.TimelineKey))
                        return true;
                    return !string.IsNullOrEmpty(BackupTargetBeforeOverwrite(target, true));
                }

                VanillaSaveRoute route;
                if (!VanillaSaveRouting.TryGetRoute(type, out route))
                    return false;

                string vanillaPath = SaveRegistryCore.GetVanillaSavePath(route.VanillaSlotNumber);
                return string.IsNullOrEmpty(vanillaPath) || !File.Exists(vanillaPath);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Vanilla overwrite backup skipped: " + ex.Message);
                return false;
            }
        }

        internal static bool BackupBeforeDelete(string scenarioId, int absoluteSlot, out string error)
        {
            error = null;
            if (absoluteSlot <= 0)
            {
                error = "The save slot is invalid.";
                return false;
            }

            string storageScenarioId = SaveStorageRouter.NormalizeScenarioId(scenarioId);
            SaveBackupTarget target;
            SaveManager.SaveType vanillaSaveType = SaveManager.SaveType.Invalid;
            if (SaveStorageRouter.IsStandardScenario(storageScenarioId))
            {
                switch (absoluteSlot)
                {
                    case 1: vanillaSaveType = SaveManager.SaveType.Slot1; break;
                    case 2: vanillaSaveType = SaveManager.SaveType.Slot2; break;
                    case 3: vanillaSaveType = SaveManager.SaveType.Slot3; break;
                }
            }
            else
            {
                VanillaSaveRoute specialRoute;
                if (VanillaSaveRouting.TryGetRouteByStorageScenarioId(storageScenarioId, out specialRoute)
                    && specialRoute.AbsoluteSlot == absoluteSlot)
                {
                    vanillaSaveType = specialRoute.SaveType;
                }
            }

            if (vanillaSaveType != SaveManager.SaveType.Invalid)
            {
                VanillaSaveRoute route;
                if (!VanillaSaveRouting.TryGetRoute(vanillaSaveType, out route))
                {
                    error = "The vanilla save route could not be resolved.";
                    return false;
                }

                string vanillaPath = SaveRegistryCore.GetVanillaSavePath(route.VanillaSlotNumber);
                if (string.IsNullOrEmpty(vanillaPath) || !File.Exists(vanillaPath))
                    return true;

                if (!TryCreateVanillaTarget(vanillaSaveType, out target))
                {
                    error = "The current vanilla save could not be captured before deletion.";
                    return false;
                }
            }
            else
            {
                string savePath = DirectoryProvider.EntryPath(storageScenarioId, absoluteSlot, false);
                if (!File.Exists(savePath))
                    return true;

                SaveEntry entry = SaveStorageRouter.GetRegistry(storageScenarioId).GetSaveBySlot(absoluteSlot);
                if (entry == null || !TryCreateCustomTarget(entry, out target))
                {
                    error = "The current custom save could not be captured before deletion.";
                    return false;
                }
            }

            SaveBackupRetentionPolicy safetySnapshotPolicy = new SaveBackupRetentionPolicy
            {
                Mode = SaveBackupRetentionMode.Forever,
                SnapshotLimit = 0
            };
            SaveBackupRepository repository = new SaveBackupRepository(DirectoryProvider.SaveBackupsRoot);
            string snapshotId = repository.CreateSnapshot(
                target,
                SaveBackupReason.BeforeDelete,
                safetySnapshotPolicy);
            if (string.IsNullOrEmpty(snapshotId))
            {
                error = "The current save could not be preserved, so deletion was cancelled.";
                return false;
            }

            MMLog.WriteInfo("[SaveBackup] Preserved current head before deletion as " + snapshotId + ".");
            return true;
        }

        internal static bool TryGetCustomTimelineKey(SaveEntry entry, out string timelineKey)
        {
            timelineKey = null;
            if (entry == null || entry.absoluteSlot <= 0)
                return false;

            string scenarioId = SaveStorageRouter.NormalizeScenarioId(entry.scenarioId);
            string slotRoot = DirectoryProvider.SlotRoot(scenarioId, entry.absoluteSlot, false);
            string lineageId = SaveBackupLineageStore.TryReadCustomLineageId(slotRoot);
            if (string.IsNullOrEmpty(lineageId))
                return false;

            timelineKey = "custom:" + lineageId;
            return true;
        }

        internal static bool TryGetVanillaTimelineKey(int vanillaSlotNumber, out string timelineKey, out SaveManager.SaveType saveType)
        {
            timelineKey = null;
            saveType = SaveManager.SaveType.Invalid;

            switch (vanillaSlotNumber)
            {
                case 1: saveType = SaveManager.SaveType.Slot1; break;
                case 2: saveType = SaveManager.SaveType.Slot2; break;
                case 3: saveType = SaveManager.SaveType.Slot3; break;
                case 4: saveType = SaveManager.SaveType.SlotSurrounded; break;
                case 5: saveType = SaveManager.SaveType.SlotStasis; break;
                default: return false;
            }

            timelineKey = "vanilla:" + saveType;
            return true;
        }

        internal static List<SaveBackupSnapshotInfo> ListSnapshots(string timelineKey, SaveBackupSnapshotSortOrder sortOrder)
        {
            SaveBackupRepository repository = new SaveBackupRepository(DirectoryProvider.SaveBackupsRoot);
            return repository.ListSnapshots(timelineKey, sortOrder);
        }

        internal static bool TryFindTimelineKey(
            string saveKind,
            string scenarioId,
            int absoluteSlot,
            out string timelineKey)
        {
            SaveBackupRepository repository = new SaveBackupRepository(DirectoryProvider.SaveBackupsRoot);
            return repository.TryFindLatestTimelineKey(saveKind, scenarioId, absoluteSlot, out timelineKey);
        }

        internal static int CountSnapshots(string timelineKey)
        {
            SaveBackupRepository repository = new SaveBackupRepository(DirectoryProvider.SaveBackupsRoot);
            return repository.CountSnapshots(timelineKey);
        }

        internal static int CountSnapshotsAfter(SaveBackupSnapshotInfo snapshot)
        {
            if (snapshot == null || snapshot.Ref == null)
                return 0;

            SaveBackupRepository repository = new SaveBackupRepository(DirectoryProvider.SaveBackupsRoot);
            return repository.CountSnapshotsAfter(
                snapshot.Ref.TimelineKey,
                snapshot.Ref.CreatedAtUtc,
                snapshot.Ref.SnapshotId);
        }

        internal static bool RestoreSnapshot(SaveBackupSnapshotInfo snapshot, out string error)
        {
            error = null;
            if (snapshot == null || snapshot.Ref == null || string.IsNullOrEmpty(snapshot.Ref.ManifestPath))
            {
                error = "Snapshot metadata was missing.";
                return false;
            }

            SaveBackupRepository repository = new SaveBackupRepository(DirectoryProvider.SaveBackupsRoot);
            if (snapshot.IsVanilla)
            {
                if (!CreatePreRestoreSafetySnapshot(snapshot, null, out error))
                    return false;

                return repository.RestoreSnapshot(snapshot.Ref.ManifestPath, out error);
            }

            SaveEntry currentEntry;
            SaveBackupRestoreDestination destination;
            if (!TryResolveCustomRestoreDestination(snapshot, out currentEntry, out destination, out error))
                return false;

            if (currentEntry != null
                && !CreatePreRestoreSafetySnapshot(snapshot, currentEntry, out error))
            {
                return false;
            }

            if (!repository.RestoreSnapshot(snapshot.Ref.ManifestPath, destination, out error))
                return false;

            snapshot.ScenarioId = destination.ScenarioId;
            snapshot.AbsoluteSlot = destination.AbsoluteSlot;
            if (currentEntry != null)
            {
                if (snapshot.Entry != null)
                    currentEntry.saveInfo = snapshot.Entry.saveInfo;
                snapshot.SaveId = currentEntry.id;
                snapshot.Entry = currentEntry;
            }
            else if (snapshot.Entry != null)
            {
                snapshot.Entry.absoluteSlot = destination.AbsoluteSlot;
                snapshot.Entry.scenarioId = destination.ScenarioId;
            }

            return true;
        }

        internal static bool DeleteSnapshot(SaveBackupSnapshotInfo snapshot, out string error)
        {
            error = null;
            if (snapshot == null || snapshot.Ref == null || string.IsNullOrEmpty(snapshot.Ref.ManifestPath))
            {
                error = "Snapshot metadata was missing.";
                return false;
            }

            SaveBackupRepository repository = new SaveBackupRepository(DirectoryProvider.SaveBackupsRoot);
            bool deleted = repository.DeleteSnapshot(snapshot.Ref.ManifestPath, out error);
            if (deleted)
                ClearBranchMarker(snapshot.Ref.TimelineKey);

            return deleted;
        }

        private static bool TryCreateCustomTarget(SaveEntry entry, out SaveBackupTarget target)
        {
            target = null;
            if (entry == null || entry.absoluteSlot <= 0)
                return false;

            string scenarioId = SaveStorageRouter.NormalizeScenarioId(entry.scenarioId);
            string savePath = DirectoryProvider.EntryPath(scenarioId, entry.absoluteSlot);
            if (!File.Exists(savePath))
                return false;

            string slotRoot = DirectoryProvider.SlotRoot(scenarioId, entry.absoluteSlot, false);
            if (!Directory.Exists(slotRoot))
                return false;

            string lineageId = SaveBackupLineageStore.EnsureCustomLineageId(slotRoot);
            if (string.IsNullOrEmpty(lineageId))
                return false;

            target = new SaveBackupTarget
            {
                TimelineKey = "custom:" + lineageId,
                SaveKind = "CustomSlot",
                ScenarioId = scenarioId,
                AbsoluteSlot = entry.absoluteSlot,
                SaveId = entry.id,
                SaveType = SaveManager.SaveType.Invalid
            };
            target.Sources.Add(new SaveBackupSource
            {
                Id = "slot",
                Path = slotRoot,
                Kind = SaveBackupSourceKind.Directory
            });
            return true;
        }

        private static string DescribeCustomTargetFailure(SaveEntry entry)
        {
            if (entry == null)
                return "entry=<null>.";
            if (entry.absoluteSlot <= 0)
                return "slot is invalid for saveId=" + (entry.id ?? "<null>") + ".";

            string scenarioId = SaveStorageRouter.NormalizeScenarioId(entry.scenarioId);
            string savePath = DirectoryProvider.EntryPath(scenarioId, entry.absoluteSlot);
            string slotRoot = DirectoryProvider.SlotRoot(scenarioId, entry.absoluteSlot, false);

            return "scenario=" + scenarioId
                + ", absoluteSlot=" + entry.absoluteSlot
                + ", saveId=" + (entry.id ?? "<null>")
                + ", saveExists=" + File.Exists(savePath)
                + ", slotRootExists=" + Directory.Exists(slotRoot) + ".";
        }

        private static bool TryCreateVanillaTarget(SaveManager.SaveType type, out SaveBackupTarget target)
        {
            target = null;

            VanillaSaveRoute route;
            if (!VanillaSaveRouting.TryGetRoute(type, out route))
                return false;

            string vanillaPath = SaveRegistryCore.GetVanillaSavePath(route.VanillaSlotNumber);
            if (string.IsNullOrEmpty(vanillaPath) || !File.Exists(vanillaPath))
                return false;

            SaveInfo saveInfo = SaveRegistryCore.ReadVanillaSaveInfo(route.VanillaSlotNumber);
            SaveBackupSidecarCapture.EnsureVanillaSidecar(route, saveInfo);

            target = new SaveBackupTarget
            {
                TimelineKey = "vanilla:" + route.SaveType,
                SaveKind = "VanillaSlot",
                ScenarioId = route.StorageScenarioId,
                AbsoluteSlot = route.AbsoluteSlot,
                SaveId = route.SaveId,
                SaveType = route.SaveType
            };
            target.Sources.Add(new SaveBackupSource
            {
                Id = "vanillaFile",
                Path = vanillaPath,
                Kind = SaveBackupSourceKind.File
            });

            string sidecarRoot = DirectoryProvider.SlotRoot(route.StorageScenarioId, route.AbsoluteSlot, false);
            if (Directory.Exists(sidecarRoot))
            {
                target.Sources.Add(new SaveBackupSource
                {
                    Id = "slotSidecar",
                    Path = sidecarRoot,
                    Kind = SaveBackupSourceKind.Directory
                });
            }

            return true;
        }

        private static string CreateSnapshot(
            SaveBackupTarget target,
            SaveBackupReason reason,
            bool skipIfAlreadyCaptured,
            bool markForCurrentSavePass)
        {
            if (target == null || string.IsNullOrEmpty(target.TimelineKey))
                return null;

            if (skipIfAlreadyCaptured && WasCapturedInCurrentSavePass(target.TimelineKey))
                return null;

            // Older builds armed destructive branch truncation after restoring an
            // earlier snapshot. Backups are recovery points, so retain both the
            // older and newer history instead of deleting either branch.
            ClearBranchMarker(target.TimelineKey);

            SaveBackupRetentionPolicy policy = ReadRetentionPolicy();
            if (policy == null || !policy.IsEnabled)
                return null;

            SaveBackupRepository repository = new SaveBackupRepository(DirectoryProvider.SaveBackupsRoot);
            string snapshotId = repository.CreateSnapshot(target, reason, policy);
            if (markForCurrentSavePass && !string.IsNullOrEmpty(snapshotId))
                MarkCapturedInCurrentSavePass(target.TimelineKey);

            return snapshotId;
        }

        private static string BackupTargetBeforeOverwrite(SaveBackupTarget target, bool skipIfAlreadyCaptured)
        {
            return CreateSnapshot(
                target,
                SaveBackupReason.BeforeOverwrite,
                skipIfAlreadyCaptured,
                true);
        }

        private static bool TryResolveCustomRestoreDestination(
            SaveBackupSnapshotInfo snapshot,
            out SaveEntry currentEntry,
            out SaveBackupRestoreDestination destination,
            out string error)
        {
            currentEntry = null;
            destination = null;
            error = null;

            const string timelinePrefix = "custom:";
            string timelineKey = snapshot.Ref != null ? snapshot.Ref.TimelineKey : string.Empty;
            if (string.IsNullOrEmpty(timelineKey)
                || !timelineKey.StartsWith(timelinePrefix, StringComparison.OrdinalIgnoreCase)
                || timelineKey.Length <= timelinePrefix.Length)
            {
                error = "The custom snapshot lineage is invalid.";
                return false;
            }

            string lineageId = timelineKey.Substring(timelinePrefix.Length);
            string scenarioId = SaveStorageRouter.NormalizeScenarioId(snapshot.ScenarioId);
            SaveEntry[] entries = SaveStorageRouter.GetRegistry(scenarioId).ListSaves();
            int matchCount = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                SaveEntry candidate = entries[i];
                if (candidate == null || candidate.absoluteSlot <= 0)
                    continue;

                string slotRoot = DirectoryProvider.SlotRoot(scenarioId, candidate.absoluteSlot, false);
                string candidateLineageId = SaveBackupLineageStore.TryReadCustomLineageId(slotRoot);
                if (!string.Equals(candidateLineageId, lineageId, StringComparison.OrdinalIgnoreCase))
                    continue;

                matchCount++;
                currentEntry = candidate;
            }

            if (matchCount > 1)
            {
                error = "Multiple live custom saves own this snapshot lineage.";
                currentEntry = null;
                return false;
            }

            destination = new SaveBackupRestoreDestination
            {
                ScenarioId = scenarioId,
                AbsoluteSlot = currentEntry != null ? currentEntry.absoluteSlot : snapshot.AbsoluteSlot,
                ExpectedLineageId = lineageId,
                AllowHistoricalSlotWhenUnoccupied = currentEntry == null
            };
            return true;
        }

        private static bool CreatePreRestoreSafetySnapshot(
            SaveBackupSnapshotInfo snapshot,
            SaveEntry resolvedCustomEntry,
            out string error)
        {
            error = null;
            SaveBackupTarget target;

            if (snapshot.IsVanilla)
            {
                VanillaSaveRoute route;
                if (!VanillaSaveRouting.TryGetRoute(snapshot.SaveType, out route))
                {
                    error = "The current vanilla save route could not be resolved.";
                    return false;
                }

                string vanillaPath = SaveRegistryCore.GetVanillaSavePath(route.VanillaSlotNumber);
                if (string.IsNullOrEmpty(vanillaPath) || !File.Exists(vanillaPath))
                    return true;

                if (!TryCreateVanillaTarget(snapshot.SaveType, out target))
                {
                    error = "The current vanilla save could not be captured before restore.";
                    return false;
                }
            }
            else
            {
                if (resolvedCustomEntry == null)
                    return true;

                if (!TryCreateCustomTarget(resolvedCustomEntry, out target))
                {
                    error = "The current custom save could not be captured before restore.";
                    return false;
                }
            }

            SaveBackupRetentionPolicy safetySnapshotPolicy = new SaveBackupRetentionPolicy
            {
                Mode = SaveBackupRetentionMode.Forever,
                SnapshotLimit = 0
            };
            SaveBackupRepository repository = new SaveBackupRepository(DirectoryProvider.SaveBackupsRoot);
            string safetySnapshotId = repository.CreateSnapshot(
                target,
                SaveBackupReason.BeforeRestore,
                safetySnapshotPolicy);
            if (string.IsNullOrEmpty(safetySnapshotId))
            {
                error = "The current save could not be preserved, so the restore was cancelled.";
                return false;
            }

            MMLog.WriteInfo("[SaveBackup] Preserved current head before restore as " + safetySnapshotId + ".");
            return true;
        }

        private static void ClearBranchMarker(string timelineKey)
        {
            try
            {
                string markerPath = GetBranchMarkerPath(timelineKey);
                if (File.Exists(markerPath))
                    File.Delete(markerPath);
            }
            catch
            {
            }
        }

        private static string GetBranchMarkerPath(string timelineKey)
        {
            string branchRoot = Path.Combine(DirectoryProvider.SaveBackupsRoot, BranchMarkerDirectoryName);
            return Path.Combine(branchRoot, SanitizePathSegment(timelineKey) + ".json");
        }

        private static string SanitizePathSegment(string value)
        {
            string safe = string.IsNullOrEmpty(value) ? "unknown" : value;
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                safe = safe.Replace(invalid[i], '_');

            safe = safe.Replace('\\', '_').Replace('/', '_').Replace(':', '_').Replace('|', '_');
            return safe.Length > 96 ? safe.Substring(0, 96) : safe;
        }

        private static SaveBackupRetentionPolicy ReadRetentionPolicy()
        {
            string raw = HarmonyBootstrap.ReadManagerString(RetentionKey, null);
            if (!string.IsNullOrEmpty(raw))
                return ParseRetentionPolicy(raw);

            string legacyMode = HarmonyBootstrap.ReadManagerString(LegacyRetentionModeKey, null);
            if (!string.IsNullOrEmpty(legacyMode))
            {
                SaveBackupRetentionPolicy legacyPolicy = ParseRetentionPolicy(legacyMode);
                if (legacyPolicy.Mode == SaveBackupRetentionMode.Limited)
                    legacyPolicy.SnapshotLimit = Math.Max(0, HarmonyBootstrap.ReadManagerInt(LegacyRetentionCountKey, legacyPolicy.SnapshotLimit));
                return legacyPolicy;
            }

            return SaveBackupRetentionPolicy.Default();
        }

        private static SaveBackupRetentionPolicy ParseRetentionPolicy(string raw)
        {
            SaveBackupRetentionPolicy policy = SaveBackupRetentionPolicy.Default();
            string value = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Length == 0)
                return policy;

            if (value == "none" || value == "disabled" || value == "disable" || value == "off" || value == "false" || value == "0")
            {
                policy.Mode = SaveBackupRetentionMode.Disabled;
                policy.SnapshotLimit = 0;
                return policy;
            }

            if (value == "always" || value == "forever" || value == "all" || value == "unlimited")
            {
                policy.Mode = SaveBackupRetentionMode.Forever;
                policy.SnapshotLimit = 0;
                return policy;
            }

            int count;
            if (int.TryParse(value, out count))
            {
                policy.Mode = count <= 0 ? SaveBackupRetentionMode.Disabled : SaveBackupRetentionMode.Limited;
                policy.SnapshotLimit = Math.Max(0, count);
            }

            return policy;
        }

        private static SaveManager.SaveType ResolveCurrentSaveType(SaveManager manager)
        {
            SaveManager.SaveType currentType = ReadSaveTypeField(manager, "m_currentType");
            if (currentType != SaveManager.SaveType.Invalid)
                return currentType;

            return ReadSaveTypeField(manager, "m_slotInUse");
        }

        private static SaveManager.SaveType ReadSaveTypeField(SaveManager manager, string fieldName)
        {
            if (manager == null || string.IsNullOrEmpty(fieldName))
                return SaveManager.SaveType.Invalid;

            try
            {
                FieldInfo field = manager.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null)
                    return SaveManager.SaveType.Invalid;

                object raw = field.GetValue(manager);
                if (raw is SaveManager.SaveType)
                    return (SaveManager.SaveType)raw;

                return (SaveManager.SaveType)raw;
            }
            catch
            {
                return SaveManager.SaveType.Invalid;
            }
        }

        private static bool IsReservedSaveType(SaveManager.SaveType type)
        {
            return type == SaveManager.SaveType.Invalid || type == SaveManager.SaveType.GlobalData;
        }

        private static bool WasCapturedInCurrentSavePass(string timelineKey)
        {
            lock (Sync)
            {
                return CurrentSavePassSnapshots.Contains(timelineKey);
            }
        }

        private static void MarkCapturedInCurrentSavePass(string timelineKey)
        {
            lock (Sync)
            {
                CurrentSavePassSnapshots.Add(timelineKey);
            }
        }
    }
}
