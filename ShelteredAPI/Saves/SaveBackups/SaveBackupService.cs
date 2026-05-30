using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Harmony;
using ModAPI.Util;
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
                    CreateSnapshot(target, SaveBackupReason.BeforeOverwrite, false, true);
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Pre-save backup skipped: " + ex.Message);
            }
        }

        internal static void BackupCustomEntryBeforeOverwrite(SaveEntry entry)
        {
            try
            {
                SaveBackupTarget target;
                if (!TryCreateCustomTarget(entry, out target))
                {
                    MMLog.WriteInfo("[SaveBackup] Custom overwrite snapshot skipped. " + DescribeCustomTargetFailure(entry));
                    return;
                }

                string snapshotId = CreateSnapshot(target, SaveBackupReason.BeforeOverwrite, true, false);
                MMLog.WriteInfo("[SaveBackup] Custom overwrite snapshot request. scenario="
                    + target.ScenarioId + ", absoluteSlot=" + target.AbsoluteSlot
                    + ", saveId=" + target.SaveId + ", timeline=" + target.TimelineKey
                    + ", created=" + (!string.IsNullOrEmpty(snapshotId) ? snapshotId : "<none>") + ".");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Custom overwrite backup skipped: " + ex.Message);
            }
        }

        internal static void BackupVanillaBeforeOverwrite(SaveManager.SaveType type)
        {
            try
            {
                SaveBackupTarget target;
                if (TryCreateVanillaTarget(type, out target))
                    CreateSnapshot(target, SaveBackupReason.BeforeOverwrite, true, false);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Vanilla overwrite backup skipped: " + ex.Message);
            }
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
            return repository.RestoreSnapshot(snapshot.Ref.ManifestPath, out error);
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
                ClearBranchMarkerIfTarget(snapshot.Ref);

            return deleted;
        }

        internal static void ArmBranchTruncation(SaveBackupSnapshotInfo snapshot)
        {
            if (snapshot == null || snapshot.Ref == null || string.IsNullOrEmpty(snapshot.Ref.TimelineKey))
                return;

            try
            {
                if (CountSnapshotsAfter(snapshot) <= 0)
                {
                    ClearBranchMarker(snapshot.Ref.TimelineKey);
                    return;
                }

                string markerPath = GetBranchMarkerPath(snapshot.Ref.TimelineKey);
                Directory.CreateDirectory(Path.GetDirectoryName(markerPath));

                ManualJsonObject root = new ManualJsonObject();
                root.Set("schemaVersion", ManualJsonValue.Number(1));
                root.Set("timelineKey", ManualJsonValue.String(snapshot.Ref.TimelineKey));
                root.Set("snapshotId", ManualJsonValue.String(snapshot.Ref.SnapshotId));
                root.Set("snapshotCreatedAtUtc", ManualJsonValue.String(snapshot.Ref.CreatedAtUtc.ToString("o")));
                root.Set("armedAtUtc", ManualJsonValue.String(DateTime.UtcNow.ToString("o")));
                File.WriteAllText(markerPath, ManualJson.Serialize(root, true));

                MMLog.WriteInfo("[SaveBackup] Armed branch truncation for timeline "
                    + snapshot.Ref.TimelineKey + " at snapshot " + snapshot.Ref.SnapshotId + ".");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Failed to arm branch truncation: " + ex.Message);
            }
        }

        private static void ClearBranchMarkerIfTarget(SaveBackupSnapshotRef snapshotRef)
        {
            if (snapshotRef == null || string.IsNullOrEmpty(snapshotRef.TimelineKey))
                return;

            string armedSnapshotId;
            DateTime armedSnapshotCreatedAtUtc;
            if (!TryReadBranchMarker(snapshotRef.TimelineKey, out armedSnapshotId, out armedSnapshotCreatedAtUtc))
                return;

            if (string.Equals(armedSnapshotId, snapshotRef.SnapshotId, StringComparison.OrdinalIgnoreCase))
                ClearBranchMarker(snapshotRef.TimelineKey);
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

            ApplyPendingBranchTruncation(target.TimelineKey);

            SaveBackupRetentionPolicy policy = ReadRetentionPolicy();
            if (policy == null || !policy.IsEnabled)
                return null;

            SaveBackupRepository repository = new SaveBackupRepository(DirectoryProvider.SaveBackupsRoot);
            string snapshotId = repository.CreateSnapshot(target, reason, policy);
            if (markForCurrentSavePass && !string.IsNullOrEmpty(snapshotId))
                MarkCapturedInCurrentSavePass(target.TimelineKey);

            return snapshotId;
        }

        private static void ApplyPendingBranchTruncation(string timelineKey)
        {
            if (string.IsNullOrEmpty(timelineKey))
                return;

            string snapshotId;
            DateTime snapshotCreatedAtUtc;
            if (!TryReadBranchMarker(timelineKey, out snapshotId, out snapshotCreatedAtUtc))
                return;

            try
            {
                SaveBackupRepository repository = new SaveBackupRepository(DirectoryProvider.SaveBackupsRoot);
                int deleted = repository.PruneSnapshotsAfter(timelineKey, snapshotCreatedAtUtc, snapshotId);
                MMLog.WriteInfo("[SaveBackup] Applied branch truncation for timeline "
                    + timelineKey + ". Deleted future snapshots: " + deleted + ".");
            }
            finally
            {
                ClearBranchMarker(timelineKey);
            }
        }

        private static bool TryReadBranchMarker(string timelineKey, out string snapshotId, out DateTime snapshotCreatedAtUtc)
        {
            snapshotId = null;
            snapshotCreatedAtUtc = DateTime.MinValue;

            string markerPath = GetBranchMarkerPath(timelineKey);
            if (!File.Exists(markerPath))
                return false;

            try
            {
                ManualJsonObject root;
                string error;
                if (!ManualJson.TryParseObject(File.ReadAllText(markerPath), out root, out error))
                {
                    ClearBranchMarker(timelineKey);
                    return false;
                }

                string markerTimeline = root.GetString("timelineKey", string.Empty);
                if (!string.Equals(markerTimeline, timelineKey, StringComparison.OrdinalIgnoreCase))
                {
                    ClearBranchMarker(timelineKey);
                    return false;
                }

                DateTime parsed;
                if (!DateTime.TryParse(
                    root.GetString("snapshotCreatedAtUtc", string.Empty),
                    null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out parsed))
                {
                    ClearBranchMarker(timelineKey);
                    return false;
                }

                snapshotId = root.GetString("snapshotId", string.Empty);
                snapshotCreatedAtUtc = parsed.ToUniversalTime();
                return !string.IsNullOrEmpty(snapshotId);
            }
            catch
            {
                ClearBranchMarker(timelineKey);
                return false;
            }
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
