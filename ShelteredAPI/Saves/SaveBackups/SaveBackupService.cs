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
                SaveManager.SaveType type = ResolveCurrentSaveType(manager);
                if (IsReservedSaveType(type))
                    return;

                SaveBackupTarget target;
                if (TryCreateCustomTargetForSaveType(type, out target)
                    || TryCreateVanillaTarget(type, out target))
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
                if (TryCreateCustomTarget(entry, out target))
                    CreateSnapshot(target, SaveBackupReason.BeforeOverwrite, true, false);
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

        private static bool TryCreateCustomTargetForSaveType(SaveManager.SaveType type, out SaveBackupTarget target)
        {
            target = null;

            PlatformSaveProxy.Target pending;
            if (SaveRuntimeState.TryGetPendingSave(type, out pending) && pending != null)
            {
                string scenarioId = SaveStorageRouter.NormalizeScenarioId(pending.scenarioId);
                SaveEntry pendingEntry = SaveStorageRouter.Get(scenarioId, pending.saveId);
                return TryCreateCustomTarget(pendingEntry, out target);
            }

            if (SaveRuntimeState.HasActiveCustomSessionFor(type))
                return TryCreateCustomTarget(SaveRuntimeState.ActiveCustomSave, out target);

            return false;
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

            SaveBackupRetentionPolicy policy = ReadRetentionPolicy();
            if (policy == null || !policy.IsEnabled)
                return null;

            SaveBackupRepository repository = new SaveBackupRepository(DirectoryProvider.SaveBackupsRoot);
            string snapshotId = repository.CreateSnapshot(target, reason, policy);
            if (markForCurrentSavePass && !string.IsNullOrEmpty(snapshotId))
                MarkCapturedInCurrentSavePass(target.TimelineKey);

            return snapshotId;
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
