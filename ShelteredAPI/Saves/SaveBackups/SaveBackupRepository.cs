using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ModAPI.Core;
using ModAPI.Util;
using ShelteredAPI.Saves.Runtime;

namespace ShelteredAPI.Saves.Backups
{
    internal sealed class SaveBackupRepository
    {
        private const string BlobCompression = SaveBackupBlobCodec.CompressionName;
        private const string BlobExtension = ".bin.slz";
        private const string RestoreTransactionDirectoryName = "restore-transactions";
        private const string RestoreRepositoryLockFileName = ".repository.lock";
        private const int RestoreRepositoryLockTimeoutMilliseconds = 30000;
        private readonly string _root;

        public SaveBackupRepository(string root)
        {
            _root = root;
            string recoveryError;
            if (!RecoverIncompleteRestoreTransactions(out recoveryError))
                MMLog.WriteWarning("[SaveBackup] Restore recovery remains unresolved: " + recoveryError);
        }

        public string CreateSnapshot(SaveBackupTarget target, SaveBackupReason reason, SaveBackupRetentionPolicy policy)
        {
            if (target == null || string.IsNullOrEmpty(target.TimelineKey))
                return null;
            if (policy == null || !policy.IsEnabled)
                return null;

            try
            {
                using (FileStream repositoryLock = AcquireRestoreRepositoryLock())
                {
                    string recoveryError;
                    if (!RecoverIncompleteRestoreTransactionsUnderLock(out recoveryError))
                        throw new IOException("Restore recovery is unresolved: " + recoveryError);

                    EnsureDirectory(_root);

                    List<SaveBackupFileRecord> files = CaptureFiles(target);
                    if (files.Count == 0)
                        return null;

                    DateTime createdAt = DateTime.UtcNow;
                    string snapshotId = BuildSnapshotId(createdAt);
                    string timelinePath = GetTimelinePath(target.TimelineKey);
                    EnsureDirectory(timelinePath);

                    string manifestPath = Path.Combine(timelinePath, snapshotId + ".json");
                    ManualJsonObject manifest = BuildSnapshotManifest(snapshotId, createdAt, target, reason, files);
                    string manifestJson = ManualJson.Serialize(manifest, true);
                    byte[] manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
                    bool manifestPublished = false;
                    try
                    {
                        PublishDurableFile(manifestPath, manifestBytes, false);
                        manifestPublished = true;

                        ManualJsonObject publishedManifest;
                        string publishedManifestError;
                        if (!ManualJson.TryParseObject(
                            File.ReadAllText(manifestPath),
                            out publishedManifest,
                            out publishedManifestError))
                        {
                            throw new IOException("Published snapshot manifest is invalid: " + publishedManifestError);
                        }
                        if (!string.Equals(
                            publishedManifest.GetString("snapshotId", string.Empty),
                            snapshotId,
                            StringComparison.Ordinal))
                        {
                            throw new IOException("Published snapshot manifest identity validation failed.");
                        }
                    }
                    catch
                    {
                        if (manifestPublished)
                            TryDeleteRestoreTemporaryFile(manifestPath);
                        throw;
                    }

                    WriteIndex();
                    ApplyRetention(target.TimelineKey, policy);

                    MMLog.WriteInfo("[SaveBackup] Created snapshot " + snapshotId
                        + " for " + target.SaveKind + " timeline " + target.TimelineKey + ".");
                    return snapshotId;
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SaveBackup] Snapshot failed: " + ex.Message);
                return null;
            }
        }

        public bool RestoreSnapshot(string manifestPath, out string error)
        {
            return RestoreSnapshot(manifestPath, null, out error);
        }

        public bool RestoreSnapshot(
            string manifestPath,
            SaveBackupRestoreDestination destination,
            out string error)
        {
            error = null;
            try
            {
                using (FileStream repositoryLock = AcquireRestoreRepositoryLock())
                {
                    string recoveryError;
                    if (!RecoverIncompleteRestoreTransactionsUnderLock(out recoveryError))
                        throw new IOException("Restore recovery is unresolved: " + recoveryError);

                    RestorePlan plan = BuildRestorePlan(manifestPath, destination);
                    ExecuteRestorePlan(plan);

                    MMLog.WriteInfo("[SaveBackup] Restored snapshot " + Path.GetFileNameWithoutExtension(manifestPath)
                        + " with " + plan.Files.Count + " file(s).");
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                MMLog.WriteError("[SaveBackup] Restore failed: " + ex.Message);
                return false;
            }
        }

        public bool DeleteSnapshot(string manifestPath, out string error)
        {
            error = null;
            try
            {
                using (FileStream repositoryLock = AcquireRestoreRepositoryLock())
                {
                    string recoveryError;
                    if (!RecoverIncompleteRestoreTransactionsUnderLock(out recoveryError))
                        throw new IOException("Restore recovery is unresolved: " + recoveryError);

                    if (string.IsNullOrEmpty(manifestPath))
                    {
                        error = "Snapshot manifest path was missing.";
                        return false;
                    }

                    string fullManifestPath = Path.GetFullPath(manifestPath);
                    if (!IsPathUnderRoot(_root, fullManifestPath))
                    {
                        error = "Snapshot manifest path is outside backup storage.";
                        return false;
                    }

                    if (!File.Exists(fullManifestPath))
                    {
                        error = "Snapshot manifest was not found.";
                        return false;
                    }

                    SaveBackupSnapshotRef snapshot;
                    TryReadSnapshotRef(fullManifestPath, out snapshot);

                    File.Delete(fullManifestPath);
                    WriteIndex();
                    PruneUnreferencedBlobs();

                    MMLog.WriteInfo("[SaveBackup] Deleted snapshot "
                        + (snapshot != null ? snapshot.SnapshotId : Path.GetFileNameWithoutExtension(fullManifestPath)) + ".");
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                MMLog.WriteError("[SaveBackup] Delete snapshot failed: " + ex.Message);
                return false;
            }
        }

        public List<SaveBackupSnapshotInfo> ListSnapshots(string timelineKey, SaveBackupSnapshotSortOrder sortOrder)
        {
            List<SaveBackupSnapshotInfo> snapshots = new List<SaveBackupSnapshotInfo>();
            if (string.IsNullOrEmpty(timelineKey))
                return snapshots;

            List<SaveBackupSnapshotRef> refs = ReadTimelineSnapshotRefs(GetTimelinePath(timelineKey));
            refs.Sort(CompareSnapshotRefs);
            if (sortOrder == SaveBackupSnapshotSortOrder.NewestFirst)
                refs.Reverse();

            for (int i = 0; i < refs.Count; i++)
            {
                SaveBackupSnapshotInfo snapshot;
                if (TryReadSnapshotInfo(refs[i].ManifestPath, out snapshot))
                    snapshots.Add(snapshot);
            }

            return snapshots;
        }

        public bool TryFindLatestTimelineKey(
            string saveKind,
            string scenarioId,
            int absoluteSlot,
            out string timelineKey)
        {
            timelineKey = null;
            if (string.IsNullOrEmpty(saveKind) || absoluteSlot <= 0)
                return false;

            SaveBackupSnapshotRef latest = null;
            List<SaveBackupSnapshotRef> refs = ReadAllSnapshotRefs();
            for (int i = 0; i < refs.Count; i++)
            {
                try
                {
                    ManualJsonObject root;
                    string parseError;
                    if (!ManualJson.TryParseObject(File.ReadAllText(refs[i].ManifestPath), out root, out parseError)
                        || root == null
                        || !string.Equals(root.GetString("saveKind", string.Empty), saveKind, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(root.GetString("scenarioId", string.Empty), scenarioId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                        || root.GetInt("absoluteSlot", 0) != absoluteSlot
                        || string.IsNullOrEmpty(refs[i].TimelineKey))
                    {
                        continue;
                    }

                    if (latest == null || CompareSnapshotRefs(latest, refs[i]) < 0)
                        latest = refs[i];
                }
                catch
                {
                    // A concurrently removed or malformed manifest is not a usable recovery timeline.
                }
            }

            if (latest == null)
                return false;

            timelineKey = latest.TimelineKey;
            return true;
        }

        public int CountSnapshots(string timelineKey)
        {
            if (string.IsNullOrEmpty(timelineKey))
                return 0;

            return ReadTimelineSnapshotRefs(GetTimelinePath(timelineKey)).Count;
        }

        public int CountSnapshotsAfter(string timelineKey, DateTime createdAtUtc, string snapshotId)
        {
            if (string.IsNullOrEmpty(timelineKey))
                return 0;

            List<SaveBackupSnapshotRef> refs = ReadTimelineSnapshotRefs(GetTimelinePath(timelineKey));
            int count = 0;
            for (int i = 0; i < refs.Count; i++)
            {
                if (IsSnapshotAfter(refs[i], createdAtUtc, snapshotId))
                    count++;
            }

            return count;
        }

        public int PruneSnapshotsAfter(string timelineKey, DateTime createdAtUtc, string snapshotId)
        {
            if (string.IsNullOrEmpty(timelineKey))
                return 0;

            try
            {
                using (FileStream repositoryLock = AcquireRestoreRepositoryLock())
                {
                    string recoveryError;
                    if (!RecoverIncompleteRestoreTransactionsUnderLock(out recoveryError))
                        throw new IOException("Restore recovery is unresolved: " + recoveryError);

                    List<SaveBackupSnapshotRef> refs = ReadTimelineSnapshotRefs(GetTimelinePath(timelineKey));
                    int deleted = 0;
                    for (int i = 0; i < refs.Count; i++)
                    {
                        SaveBackupSnapshotRef snapshot = refs[i];
                        if (snapshot.IsPinned || !IsSnapshotAfter(snapshot, createdAtUtc, snapshotId))
                            continue;

                        try
                        {
                            File.Delete(snapshot.ManifestPath);
                            deleted++;
                            MMLog.WriteDebug("[SaveBackup] Pruned future snapshot " + snapshot.SnapshotId
                                + " from timeline " + timelineKey + ".");
                        }
                        catch (Exception ex)
                        {
                            MMLog.WriteWarning("[SaveBackup] Failed to prune future snapshot "
                                + snapshot.SnapshotId + ": " + ex.Message);
                        }
                    }

                    if (deleted > 0)
                    {
                        WriteIndex();
                        PruneUnreferencedBlobs();
                    }

                    return deleted;
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SaveBackup] Prune snapshots failed closed: " + ex.Message);
                return 0;
            }
        }

        private RestorePlan BuildRestorePlan(string manifestPath)
        {
            return BuildRestorePlan(manifestPath, null);
        }

        private RestorePlan BuildRestorePlan(
            string manifestPath,
            SaveBackupRestoreDestination restoreDestination)
        {
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
                throw new FileNotFoundException("Backup manifest was not found.", manifestPath);

            string fullManifestPath = Path.GetFullPath(manifestPath);
            if (!IsPathUnderRoot(_root, fullManifestPath))
                throw new IOException("Backup manifest path is outside backup storage.");
            EnsureNoReparsePointTraversal(GetDirectoryRoot(_root), fullManifestPath);

            ManualJsonObject root;
            string parseError;
            if (!ManualJson.TryParseObject(File.ReadAllText(fullManifestPath), out root, out parseError))
                throw new IOException("Backup manifest is invalid: " + parseError);

            Dictionary<string, SaveBackupSource> sources = ResolveCurrentRestoreSources(root, restoreDestination);
            ManualJsonArray fileArray = root.GetArray("files");
            if (fileArray == null || fileArray.Items.Count == 0)
                throw new IOException("Backup manifest contains no files.");

            RestorePlan plan = new RestorePlan();
            plan.ManifestPath = fullManifestPath;
            if (restoreDestination != null)
            {
                plan.RestoreDestination = new SaveBackupRestoreDestination
                {
                    ScenarioId = SaveStorageRouter.NormalizeScenarioId(
                        root.GetString("scenarioId", string.Empty)),
                    AbsoluteSlot = restoreDestination.AbsoluteSlot,
                    ExpectedLineageId = GetCustomSnapshotLineageId(root),
                    AllowHistoricalSlotWhenUnoccupied =
                        restoreDestination.AllowHistoricalSlotWhenUnoccupied
                };
            }
            foreach (KeyValuePair<string, SaveBackupSource> pair in sources)
            {
                SaveBackupSource source = pair.Value;
                if (source == null || string.IsNullOrEmpty(source.Path))
                    continue;

                plan.AllowedRoots.Add(new RestoreAllowedRoot
                {
                    Path = Path.GetFullPath(source.Path),
                    Kind = source.Kind
                });
            }
            HashSet<string> destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < fileArray.Items.Count; i++)
            {
                ManualJsonObject file = fileArray.Items[i] != null ? fileArray.Items[i].ObjectValue : null;
                if (file == null)
                    throw new IOException("Backup manifest contains an invalid file entry.");

                RestoreFilePlan item = BuildRestoreFilePlan(file, sources);
                if (!destinations.Add(item.DestinationPath))
                    throw new IOException("Backup manifest contains duplicate restore destinations.");
                EnsureRestoreDestinationHasNoReparseTraversal(item.DestinationPath, plan.AllowedRoots);

                plan.Files.Add(item);
            }

            if (plan.Files.Count == 0)
                throw new IOException("Backup manifest contains no restorable files.");

            AddExactRestoreDeletions(plan, sources, destinations);
            ValidateCustomRestorePlanIdentity(root, plan);
            return plan;
        }

        private bool TryReadSnapshotInfo(string manifestPath, out SaveBackupSnapshotInfo snapshot)
        {
            snapshot = null;
            try
            {
                if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
                    return false;

                ManualJsonObject root;
                string error;
                if (!ManualJson.TryParseObject(File.ReadAllText(manifestPath), out root, out error))
                    return false;

                SaveBackupSnapshotRef snapshotRef = BuildSnapshotRef(root, manifestPath);
                string saveKind = root.GetString("saveKind", string.Empty);
                string scenarioId = root.GetString("scenarioId", string.Empty);
                int absoluteSlot = root.GetInt("absoluteSlot", 0);
                string saveId = root.GetString("saveId", string.Empty);
                SaveManager.SaveType saveType = ReadSaveType(root.GetString("saveType", string.Empty));

                SaveBackupFileRecord saveRecord;
                byte[] saveBytes = ReadSnapshotSaveBytes(root, saveKind, out saveRecord);
                SaveInfo saveInfo = ReadSnapshotSaveInfo(saveKind, saveBytes);
                if (saveInfo == null)
                    saveInfo = new SaveInfo();
                if (string.IsNullOrEmpty(saveInfo.familyName))
                    saveInfo.familyName = "Unknown";
                saveInfo.saveTime = snapshotRef.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture);

                SaveEntry entry = new SaveEntry
                {
                    id = snapshotRef.SnapshotId,
                    absoluteSlot = absoluteSlot,
                    name = saveInfo.familyName,
                    createdAt = snapshotRef.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture),
                    updatedAt = snapshotRef.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture),
                    fileSize = saveRecord != null ? saveRecord.Size : 0,
                    crc32 = saveRecord != null ? saveRecord.Crc32 : 0,
                    scenarioId = scenarioId,
                    saveInfo = saveInfo
                };

                snapshot = new SaveBackupSnapshotInfo
                {
                    Ref = snapshotRef,
                    Entry = entry,
                    SlotManifest = ReadSnapshotSlotManifest(root),
                    SaveKind = saveKind,
                    ScenarioId = scenarioId,
                    AbsoluteSlot = absoluteSlot,
                    SaveId = saveId,
                    SaveType = saveType
                };
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Failed to read snapshot metadata: " + ex.Message);
                return false;
            }
        }

        private SaveBackupSnapshotRef BuildSnapshotRef(ManualJsonObject root, string manifestPath)
        {
            DateTime createdAt;
            string created = root.GetString("createdAtUtc", string.Empty);
            if (!DateTime.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out createdAt))
                createdAt = File.GetCreationTimeUtc(manifestPath);

            return new SaveBackupSnapshotRef
            {
                SnapshotId = root.GetString("snapshotId", Path.GetFileNameWithoutExtension(manifestPath)),
                TimelineKey = root.GetString("timelineKey", string.Empty),
                ManifestPath = manifestPath,
                CreatedAtUtc = createdAt.ToUniversalTime(),
                IsPinned = root.GetBool("isPinned", false)
            };
        }

        private SaveInfo ReadSnapshotSaveInfo(string saveKind, byte[] saveBytes)
        {
            if (saveBytes == null || saveBytes.Length == 0)
                return null;

            if (string.Equals(saveKind, "VanillaSlot", StringComparison.OrdinalIgnoreCase))
                return SaveRegistryCore.ReadVanillaSaveInfoFromEncryptedBytes(saveBytes);

            return SaveRegistryCore.ReadSaveInfoFromXml(saveBytes);
        }

        private SlotManifest ReadSnapshotSlotManifest(ManualJsonObject root)
        {
            SaveBackupFileRecord record;
            byte[] manifestBytes = ReadSnapshotFileBytes(root, "slot", "manifest.json", out record);
            if (manifestBytes == null || manifestBytes.Length == 0)
                manifestBytes = ReadSnapshotFileBytes(root, "slotSidecar", "manifest.json", out record);
            if (manifestBytes == null || manifestBytes.Length == 0)
                return null;

            try
            {
                return SaveRegistryCore.DeserializeSlotManifest(Encoding.UTF8.GetString(manifestBytes));
            }
            catch
            {
                return null;
            }
        }

        private byte[] ReadSnapshotSaveBytes(ManualJsonObject root, string saveKind, out SaveBackupFileRecord record)
        {
            if (string.Equals(saveKind, "VanillaSlot", StringComparison.OrdinalIgnoreCase))
                return ReadSnapshotFileBytes(root, "vanillaFile", null, out record);

            return ReadSnapshotFileBytes(root, "slot", "SaveData.xml", out record);
        }

        private byte[] ReadSnapshotFileBytes(ManualJsonObject root, string sourceId, string relativeFileName, out SaveBackupFileRecord record)
        {
            record = null;
            ManualJsonArray files = root.GetArray("files");
            if (files == null)
                return null;

            for (int i = 0; i < files.Items.Count; i++)
            {
                ManualJsonObject file = files.Items[i] != null ? files.Items[i].ObjectValue : null;
                if (file == null || !MatchesSnapshotFile(file, sourceId, relativeFileName))
                    continue;

                string compression = file.GetString("compression", string.Empty);
                if (!string.Equals(compression, BlobCompression, StringComparison.OrdinalIgnoreCase))
                    return null;

                string blobPath = file.GetString("blobPath", string.Empty);
                if (string.IsNullOrEmpty(blobPath))
                    return null;

                record = new SaveBackupFileRecord
                {
                    SourceId = file.GetString("sourceId", string.Empty),
                    RelativePath = file.GetString("relativePath", string.Empty),
                    Hash = file.GetString("hash", string.Empty),
                    Size = GetLong(file, "size", 0),
                    Crc32 = unchecked((uint)GetLong(file, "crc32", 0)),
                    BlobPath = blobPath,
                    Compression = compression
                };

                return SaveBackupBlobCodec.ReadDecompressed(GetPathUnderRoot(_root, blobPath));
            }

            return null;
        }

        private static bool MatchesSnapshotFile(ManualJsonObject file, string sourceId, string relativeFileName)
        {
            string actualSourceId = file.GetString("sourceId", string.Empty);
            if (!string.IsNullOrEmpty(sourceId) && !string.Equals(actualSourceId, sourceId, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrEmpty(relativeFileName))
                return true;

            string relativePath = NormalizeManifestPath(file.GetString("relativePath", string.Empty));
            string expected = NormalizeManifestPath(relativeFileName);
            return string.Equals(relativePath, expected, StringComparison.OrdinalIgnoreCase)
                || relativePath.EndsWith("/" + expected, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(relativePath), expected, StringComparison.OrdinalIgnoreCase);
        }

        private Dictionary<string, SaveBackupSource> ReadRestoreSources(ManualJsonObject root)
        {
            Dictionary<string, SaveBackupSource> sources = new Dictionary<string, SaveBackupSource>(StringComparer.OrdinalIgnoreCase);
            ManualJsonArray sourceArray = root.GetArray("sources");
            if (sourceArray == null)
                return sources;

            for (int i = 0; i < sourceArray.Items.Count; i++)
            {
                ManualJsonObject source = sourceArray.Items[i] != null ? sourceArray.Items[i].ObjectValue : null;
                if (source == null)
                    continue;

                string id = source.GetString("id", string.Empty);
                string path = source.GetString("path", string.Empty);
                if (string.IsNullOrEmpty(id))
                    continue;

                sources[id] = new SaveBackupSource
                {
                    Id = id,
                    Path = path,
                    Kind = ReadSourceKind(source.GetString("kind", string.Empty))
                };
            }

            return sources;
        }

        private Dictionary<string, SaveBackupSource> ResolveCurrentRestoreSources(
            ManualJsonObject root,
            SaveBackupRestoreDestination restoreDestination)
        {
            Dictionary<string, SaveBackupSource> manifestSources = ReadRestoreSources(root);
            Dictionary<string, SaveBackupSource> resolved = new Dictionary<string, SaveBackupSource>(StringComparer.OrdinalIgnoreCase);

            string saveKind = root.GetString("saveKind", string.Empty);
            string scenarioId = SaveStorageRouter.NormalizeScenarioId(root.GetString("scenarioId", string.Empty));
            int absoluteSlot = root.GetInt("absoluteSlot", 0);
            SaveManager.SaveType saveType = ReadSaveType(root.GetString("saveType", string.Empty));

            if (string.Equals(saveKind, "CustomSlot", StringComparison.OrdinalIgnoreCase))
            {
                if (absoluteSlot <= 0)
                    throw new IOException("Backup manifest custom slot number is invalid.");

                string customSlotRoot = restoreDestination == null
                    ? DirectoryProvider.SlotRoot(scenarioId, absoluteSlot, false)
                    : ResolveCustomRestoreDestination(root, restoreDestination);
                AddResolvedSourceIfPresent(
                    manifestSources,
                    resolved,
                    "slot",
                    customSlotRoot,
                    SaveBackupSourceKind.Directory);
                return resolved;
            }

            if (string.Equals(saveKind, "VanillaSlot", StringComparison.OrdinalIgnoreCase))
            {
                VanillaSaveRoute route;
                if (!VanillaSaveRouting.TryGetRoute(saveType, out route))
                    throw new IOException("Backup manifest vanilla save route is invalid.");

                AddResolvedSourceIfPresent(
                    manifestSources,
                    resolved,
                    "vanillaFile",
                    SaveRegistryCore.GetVanillaSavePath(route.VanillaSlotNumber),
                    SaveBackupSourceKind.File);
                AddResolvedSourceIfPresent(
                    manifestSources,
                    resolved,
                    "slotSidecar",
                    DirectoryProvider.SlotRoot(route.StorageScenarioId, route.AbsoluteSlot, false),
                    SaveBackupSourceKind.Directory);
                return resolved;
            }

            throw new IOException("Backup manifest save kind is unsupported: " + saveKind);
        }

        private static string ResolveCustomRestoreDestination(
            ManualJsonObject manifest,
            SaveBackupRestoreDestination destination)
        {
            if (destination == null || destination.AbsoluteSlot <= 0)
                throw new IOException("Custom restore destination is invalid.");

            string manifestScenario = SaveStorageRouter.NormalizeScenarioId(
                manifest.GetString("scenarioId", string.Empty));
            string destinationScenario = SaveStorageRouter.NormalizeScenarioId(destination.ScenarioId);
            if (!string.Equals(manifestScenario, destinationScenario, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Custom restore destination scenario does not match the snapshot.");

            string lineageId = GetCustomSnapshotLineageId(manifest);
            if (!string.IsNullOrEmpty(destination.ExpectedLineageId)
                && !string.Equals(destination.ExpectedLineageId, lineageId, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Custom restore destination lineage proof does not match the snapshot.");
            }

            string targetRoot = Path.GetFullPath(
                DirectoryProvider.SlotRoot(destinationScenario, destination.AbsoluteSlot, false));
            string scenarioRoot = Path.GetDirectoryName(targetRoot);
            if (string.IsNullOrEmpty(scenarioRoot))
                throw new IOException("Custom restore destination scenario root is invalid.");
            scenarioRoot = Path.GetFullPath(scenarioRoot);
            string canonicalTargetRoot = Path.GetFullPath(Path.Combine(
                scenarioRoot,
                "Slot_" + destination.AbsoluteSlot.ToString(CultureInfo.InvariantCulture)));
            if (!IsPathInsideDirectory(GetDirectoryRoot(scenarioRoot), targetRoot)
                || !string.Equals(
                    targetRoot,
                    canonicalTargetRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Custom restore destination is not a canonical slot path.");
            }
            EnsureNoReparsePointTraversal(GetDirectoryRoot(scenarioRoot), targetRoot);

            int matchingLiveSlots = 0;
            int matchingLiveSlot = 0;
            if (Directory.Exists(scenarioRoot))
            {
                string[] slotRoots = Directory.GetDirectories(scenarioRoot, "Slot_*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < slotRoots.Length; i++)
                {
                    string slotRoot = Path.GetFullPath(slotRoots[i]);
                    string slotName = Path.GetFileName(slotRoot);
                    int slotNumber;
                    if (string.IsNullOrEmpty(slotName)
                        || !slotName.StartsWith("Slot_", StringComparison.Ordinal)
                        || !int.TryParse(
                            slotName.Substring("Slot_".Length),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out slotNumber)
                        || slotNumber <= 0)
                    {
                        continue;
                    }

                    string canonicalSlotRoot = Path.GetFullPath(
                        DirectoryProvider.SlotRoot(destinationScenario, slotNumber, false));
                    if (!string.Equals(slotRoot, canonicalSlotRoot, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Custom save repository contains a non-canonical slot path.");
                    EnsureNoReparsePointTraversal(GetDirectoryRoot(scenarioRoot), slotRoot);

                    if (!File.Exists(Path.Combine(slotRoot, "SaveData.xml")))
                        continue;

                    string liveLineageId = ReadCustomLineageIdStrict(slotRoot);
                    if (string.Equals(liveLineageId, lineageId, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingLiveSlots++;
                        matchingLiveSlot = slotNumber;
                    }
                }
            }

            if (matchingLiveSlots > 1)
                throw new IOException("Multiple live custom slots own the snapshot lineage.");
            if (matchingLiveSlots == 1)
            {
                if (matchingLiveSlot != destination.AbsoluteSlot)
                    throw new IOException("Custom restore destination does not own the snapshot lineage.");
                if (!string.Equals(
                    ReadCustomLineageIdStrict(targetRoot),
                    lineageId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("Custom restore destination identity does not match the snapshot lineage.");
                }
                return targetRoot;
            }

            int historicalSlot = manifest.GetInt("absoluteSlot", 0);
            if (!destination.AllowHistoricalSlotWhenUnoccupied
                || destination.AbsoluteSlot != historicalSlot)
            {
                throw new IOException("No live custom slot owns the snapshot lineage.");
            }
            if (Directory.Exists(targetRoot)
                && Directory.GetFileSystemEntries(targetRoot).Length != 0)
            {
                throw new IOException("Historical custom restore destination is occupied.");
            }

            return targetRoot;
        }

        private static string GetCustomSnapshotLineageId(ManualJsonObject manifest)
        {
            string timelineKey = manifest.GetString("timelineKey", string.Empty);
            const string prefix = "custom:";
            if (!timelineKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Custom snapshot timeline does not contain a lineage.");

            string lineageId = timelineKey.Substring(prefix.Length);
            if (!IsHexValue(lineageId, 32))
                throw new IOException("Custom snapshot lineage is invalid.");
            return lineageId;
        }

        private static string ReadCustomLineageIdStrict(string slotRoot)
        {
            string identityPath = Path.Combine(slotRoot, "backup.identity.json");
            if (!File.Exists(identityPath))
                return null;
            EnsureNoReparsePointTraversal(GetDirectoryRoot(slotRoot), identityPath);

            ManualJsonObject identity;
            string error;
            if (!ManualJson.TryParseObject(File.ReadAllText(identityPath), out identity, out error)
                || identity.GetInt("schemaVersion", 0) != 1)
            {
                throw new IOException("Custom slot backup identity is invalid.");
            }

            string lineageId = identity.GetString("lineageId", string.Empty);
            if (!IsHexValue(lineageId, 32))
                throw new IOException("Custom slot backup identity lineage is invalid.");
            return lineageId;
        }

        private static void ValidateCustomRestorePlanIdentity(ManualJsonObject manifest, RestorePlan plan)
        {
            if (!string.Equals(
                manifest.GetString("saveKind", string.Empty),
                "CustomSlot",
                StringComparison.OrdinalIgnoreCase))
            {
                if (plan.RestoreDestination != null)
                    throw new IOException("A custom restore destination cannot be used for a non-custom snapshot.");
                return;
            }

            string expectedLineageId = GetCustomSnapshotLineageId(manifest);
            int identityFiles = 0;
            for (int i = 0; i < plan.Files.Count; i++)
            {
                RestoreFilePlan file = plan.Files[i];
                if (!string.Equals(
                    Path.GetFileName(file.DestinationPath),
                    "backup.identity.json",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                identityFiles++;
                ManualJsonObject identity;
                string error;
                if (!ManualJson.TryParseObject(
                    Encoding.UTF8.GetString(file.Bytes ?? new byte[0]),
                    out identity,
                    out error)
                    || identity.GetInt("schemaVersion", 0) != 1
                    || !string.Equals(
                        identity.GetString("lineageId", string.Empty),
                        expectedLineageId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("Snapshot backup identity does not match its timeline lineage.");
                }
            }

            if (identityFiles != 1)
                throw new IOException("Custom snapshot must contain exactly one backup identity file.");
        }

        private static void AddResolvedSourceIfPresent(
            Dictionary<string, SaveBackupSource> manifestSources,
            Dictionary<string, SaveBackupSource> resolved,
            string id,
            string path,
            SaveBackupSourceKind kind)
        {
            if (!manifestSources.ContainsKey(id))
                return;
            if (string.IsNullOrEmpty(path))
                throw new IOException("Current restore path is missing for backup source: " + id);

            resolved[id] = new SaveBackupSource
            {
                Id = id,
                Path = path,
                Kind = kind
            };
        }

        private RestoreFilePlan BuildRestoreFilePlan(ManualJsonObject file, Dictionary<string, SaveBackupSource> sources)
        {
            string sourceId = file.GetString("sourceId", string.Empty);
            SaveBackupSource source;
            if (string.IsNullOrEmpty(sourceId) || !sources.TryGetValue(sourceId, out source))
                throw new IOException("Backup manifest references an unknown source: " + sourceId);

            string compression = file.GetString("compression", string.Empty);
            if (!string.Equals(compression, BlobCompression, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Backup blob uses unsupported compression: " + compression);

            string blobPath = file.GetString("blobPath", string.Empty);
            string fullBlobPath = GetPathUnderRoot(_root, blobPath);
            byte[] bytes = SaveBackupBlobCodec.ReadDecompressed(fullBlobPath);
            VerifyRestoreBytes(file, bytes);

            return new RestoreFilePlan
            {
                DestinationPath = GetRestoreDestinationPath(source, file.GetString("relativePath", string.Empty)),
                Bytes = bytes
            };
        }

        private static void AddExactRestoreDeletions(
            RestorePlan plan,
            Dictionary<string, SaveBackupSource> sources,
            HashSet<string> snapshotDestinations)
        {
            HashSet<string> plannedDeletions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, SaveBackupSource> pair in sources)
            {
                SaveBackupSource source = pair.Value;
                if (source == null || source.Kind != SaveBackupSourceKind.Directory)
                    continue;

                string root = GetDirectoryRoot(source.Path);
                if (!Directory.Exists(root))
                    continue;

                List<string> existingFiles = GetFilesUnderRoot(root);
                for (int i = 0; i < existingFiles.Count; i++)
                {
                    string existingFile = existingFiles[i];
                    if (IsRestoreTransactionArtifactPath(existingFile))
                        continue;
                    if (!snapshotDestinations.Contains(existingFile) && plannedDeletions.Add(existingFile))
                        plan.Deletions.Add(existingFile);
                }
            }
        }

        private static void AddInterruptedRestoreDeletions(RestorePlan plan, string transactionId)
        {
            HashSet<string> replacements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < plan.Files.Count; i++)
                replacements.Add(plan.Files[i].DestinationPath);

            HashSet<string> deletions = new HashSet<string>(plan.Deletions, StringComparer.OrdinalIgnoreCase);
            for (int rootIndex = 0; rootIndex < plan.AllowedRoots.Count; rootIndex++)
            {
                RestoreAllowedRoot allowedRoot = plan.AllowedRoots[rootIndex];
                if (allowedRoot.Kind != SaveBackupSourceKind.Directory || !Directory.Exists(allowedRoot.Path))
                    continue;

                List<string> files = GetFilesUnderRoot(GetDirectoryRoot(allowedRoot.Path));
                for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
                {
                    string destinationPath;
                    if (!TryGetRestoreArtifactDestination(
                        files[fileIndex],
                        transactionId,
                        "rollback",
                        out destinationPath))
                    {
                        continue;
                    }

                    destinationPath = Path.GetFullPath(destinationPath);
                    if (replacements.Contains(destinationPath)
                        || !IsRestoreDestinationAllowed(destinationPath, plan.AllowedRoots))
                    {
                        continue;
                    }

                    EnsureRestoreDestinationHasNoReparseTraversal(destinationPath, plan.AllowedRoots);
                    if (deletions.Add(destinationPath))
                        plan.Deletions.Add(destinationPath);
                }
            }
        }

        private static bool IsRestoreTransactionArtifactPath(string path)
        {
            string fileName = Path.GetFileName(path);
            int restoreIndex = fileName.LastIndexOf(".restore.", StringComparison.OrdinalIgnoreCase);
            if (restoreIndex <= 33 || !fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                return false;

            int transactionStart = restoreIndex - 32;
            return transactionStart > 0
                && fileName[transactionStart - 1] == '.'
                && IsHexValue(fileName.Substring(transactionStart, 32), 32);
        }

        private static bool TryGetRestoreArtifactDestination(
            string artifactPath,
            string transactionId,
            string purpose,
            out string destinationPath)
        {
            destinationPath = null;
            string suffix = "." + transactionId + ".restore." + purpose + ".tmp";
            if (string.IsNullOrEmpty(artifactPath)
                || !artifactPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                || artifactPath.Length <= suffix.Length)
            {
                return false;
            }

            destinationPath = artifactPath.Substring(0, artifactPath.Length - suffix.Length);
            return true;
        }

        private static void VerifyRestoreBytes(ManualJsonObject file, byte[] bytes)
        {
            long expectedSize = GetLong(file, "size", -1);
            if (expectedSize < 0)
                throw new IOException("Backup blob size metadata is missing.");
            if (bytes.Length != expectedSize)
                throw new IOException("Backup blob size check failed.");

            string expectedHash = file.GetString("hash", string.Empty);
            if (string.IsNullOrEmpty(expectedHash))
                throw new IOException("Backup blob hash metadata is missing.");
            if (!string.Equals(ComputeSha256(bytes), expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Backup blob hash check failed.");

            long expectedCrc = GetLong(file, "crc32", -1);
            if (expectedCrc < 0 || expectedCrc > uint.MaxValue)
                throw new IOException("Backup blob CRC metadata is missing or invalid.");
            if (CRC32.Compute(bytes) != unchecked((uint)expectedCrc))
                throw new IOException("Backup blob CRC check failed.");
        }

        private static string GetRestoreDestinationPath(SaveBackupSource source, string relativePath)
        {
            string root = Path.GetFullPath(source.Path);
            if (source.Kind == SaveBackupSourceKind.File)
                return root;

            return GetPathUnderRoot(root, relativePath);
        }

        private static string GetPathUnderRoot(string root, string relativePath)
        {
            if (string.IsNullOrEmpty(root))
                throw new IOException("Restore root path is missing.");
            if (string.IsNullOrEmpty(relativePath))
                throw new IOException("Restore relative path is missing.");
            if (Path.IsPathRooted(relativePath))
                throw new IOException("Restore relative path must not be rooted.");

            string fullRoot = GetDirectoryRoot(root);
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsPathInsideDirectory(fullRoot, fullPath))
                throw new IOException("Backup manifest path escapes its restore root.");
            EnsureNoReparsePointTraversal(fullRoot, fullPath);

            return fullPath;
        }

        private static string GetDirectoryRoot(string root)
        {
            if (string.IsNullOrEmpty(root))
                throw new IOException("Restore root path is missing.");

            return Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        }

        private static bool IsPathInsideDirectory(string fullRoot, string path)
        {
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureNoReparsePointTraversal(string fullRoot, string fullPath)
        {
            if ((File.Exists(fullPath) || Directory.Exists(fullPath))
                && (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Restore path resolves through a reparse point.");
            }

            string root = Path.GetFullPath(fullRoot);
            string pathRoot = Path.GetPathRoot(root);
            if (!string.Equals(root, pathRoot, StringComparison.OrdinalIgnoreCase))
                root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string directory = Path.GetDirectoryName(fullPath);
            while (!string.IsNullOrEmpty(directory)
                && (string.Equals(directory, root, StringComparison.OrdinalIgnoreCase)
                    || IsPathInsideDirectory(fullRoot, directory)))
            {
                if (Directory.Exists(directory)
                    && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Restore path traverses a reparse point under its resolved root.");

                if (string.Equals(directory, root, StringComparison.OrdinalIgnoreCase))
                    break;
                directory = Path.GetDirectoryName(directory);
            }
        }

        private static List<string> GetFilesUnderRoot(string fullRoot)
        {
            if ((File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Restore directory root cannot be a reparse point.");

            List<string> files = new List<string>();
            List<string> pending = new List<string>();
            pending.Add(fullRoot);

            while (pending.Count > 0)
            {
                int last = pending.Count - 1;
                string directory = pending[last];
                pending.RemoveAt(last);

                string[] childFiles = Directory.GetFiles(directory);
                for (int i = 0; i < childFiles.Length; i++)
                {
                    string fullFile = Path.GetFullPath(childFiles[i]);
                    if (!IsPathInsideDirectory(fullRoot, fullFile))
                        throw new IOException("Existing restore file escapes its resolved root.");
                    if ((File.GetAttributes(fullFile) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Existing restore file is a reparse point.");
                    files.Add(fullFile);
                }

                string[] childDirectories = Directory.GetDirectories(directory);
                for (int i = 0; i < childDirectories.Length; i++)
                {
                    string fullDirectory = Path.GetFullPath(childDirectories[i]);
                    if (!IsPathInsideDirectory(fullRoot, fullDirectory))
                        throw new IOException("Existing restore directory escapes its resolved root.");
                    if ((File.GetAttributes(fullDirectory) & FileAttributes.ReparsePoint) != 0)
                        continue;
                    pending.Add(fullDirectory);
                }
            }

            return files;
        }

        private static SaveBackupSourceKind ReadSourceKind(string value)
        {
            return string.Equals(value, "File", StringComparison.OrdinalIgnoreCase)
                ? SaveBackupSourceKind.File
                : SaveBackupSourceKind.Directory;
        }

        private static long GetLong(ManualJsonObject obj, string name, long fallback)
        {
            ManualJsonValue value = obj != null ? obj.Get(name) : null;
            if (value == null || value.Type != ManualJsonValueType.Number)
                return fallback;

            long parsed;
            return long.TryParse(value.NumberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private void ExecuteRestorePlan(RestorePlan plan)
        {
            string transactionId = Guid.NewGuid().ToString("N");
            string transactionDirectory = Path.Combine(_root, RestoreTransactionDirectoryName);
            string journalPath = Path.Combine(transactionDirectory, transactionId + ".json");
            string committedPath = Path.Combine(transactionDirectory, transactionId + ".committed");
            string committedTemporaryPath = committedPath + ".tmp";
            List<RestoreMutation> mutations = new List<RestoreMutation>();
            bool preserveOriginals = false;
            try
            {
                EnsureDirectory(transactionDirectory);

                for (int i = 0; i < plan.Files.Count; i++)
                {
                    RestoreFilePlan file = plan.Files[i];
                    string directory = Path.GetDirectoryName(file.DestinationPath);
                    EnsureDirectory(directory);

                    RestoreMutation mutation = new RestoreMutation();
                    mutation.DestinationPath = file.DestinationPath;
                    mutation.StagedPath = BuildRestoreTemporaryPath(file.DestinationPath, transactionId, "stage");
                    mutation.RollbackPath = BuildRestoreTemporaryPath(file.DestinationPath, transactionId, "rollback");
                    mutation.AbsentMarkerPath = BuildRestoreTemporaryPath(file.DestinationPath, transactionId, "absent");
                    mutation.DiscardPath = BuildRestoreTemporaryPath(file.DestinationPath, transactionId, "discard");
                    mutation.OriginalExisted = File.Exists(file.DestinationPath);
                    mutation.HasReplacement = true;
                    mutation.ReplacementBytes = file.Bytes ?? new byte[0];
                    mutation.ExpectedSize = mutation.ReplacementBytes.LongLength;
                    mutation.ExpectedHash = ComputeSha256(mutation.ReplacementBytes);
                    mutations.Add(mutation);
                }

                for (int i = 0; i < plan.Deletions.Count; i++)
                {
                    RestoreMutation mutation = new RestoreMutation();
                    mutation.DestinationPath = plan.Deletions[i];
                    mutation.RollbackPath = BuildRestoreTemporaryPath(mutation.DestinationPath, transactionId, "rollback");
                    mutation.AbsentMarkerPath = BuildRestoreTemporaryPath(mutation.DestinationPath, transactionId, "absent");
                    mutation.DiscardPath = BuildRestoreTemporaryPath(mutation.DestinationPath, transactionId, "discard");
                    mutation.OriginalExisted = File.Exists(mutation.DestinationPath);
                    mutations.Add(mutation);
                }

                WriteRestoreTransactionJournal(journalPath, transactionId, plan, mutations);

                for (int i = 0; i < mutations.Count; i++)
                {
                    RestoreMutation mutation = mutations[i];
                    if (!mutation.HasReplacement)
                        continue;

                    DurableFileWriter.WriteNew(mutation.StagedPath, mutation.ReplacementBytes);
                    mutation.ReplacementBytes = null;
                }

                for (int i = 0; i < mutations.Count; i++)
                    CommitRestoreMutation(mutations[i]);

                string validationError = ValidateCommittedRestoreState(mutations);
                if (!string.IsNullOrEmpty(validationError))
                    throw new IOException("Committed restore validation failed: " + validationError);

                WriteDurableTextFile(committedTemporaryPath, transactionId);
                File.Move(committedTemporaryPath, committedPath);
            }
            catch (Exception commitError)
            {
                string rollbackError = RollBackRestoreMutations(mutations);
                if (!string.IsNullOrEmpty(rollbackError))
                {
                    preserveOriginals = true;
                    throw new IOException(commitError.Message + " Rollback also failed: " + rollbackError, commitError);
                }
                if (CleanupRestoreTemporaryFiles(mutations)
                    && TryDeleteRestoreTemporaryFile(journalPath))
                {
                    TryDeleteRestoreTemporaryFile(committedPath);
                    TryDeleteRestoreTemporaryFile(committedTemporaryPath);
                }
                else
                {
                    preserveOriginals = true;
                }
                throw;
            }
            finally
            {
                if (preserveOriginals)
                    MMLog.WriteWarning("[SaveBackup] Preserved incomplete restore transaction " + transactionId + " for later recovery.");
            }
        }

        private static string BuildRestoreTemporaryPath(string destinationPath, string transactionId, string purpose)
        {
            return destinationPath + "." + transactionId + ".restore." + purpose + ".tmp";
        }

        private static void CommitRestoreMutation(RestoreMutation mutation)
        {
            if (File.Exists(mutation.DestinationPath) != mutation.OriginalExisted)
                throw new IOException("A restore destination changed while the snapshot was being staged.");

            if (mutation.OriginalExisted)
            {
                if (mutation.HasReplacement)
                    ReplaceRestoreFile(mutation.StagedPath, mutation.DestinationPath, mutation.RollbackPath);
                else
                    File.Move(mutation.DestinationPath, mutation.RollbackPath);
                return;
            }

            if (mutation.HasReplacement)
            {
                WriteDurableTextFile(mutation.AbsentMarkerPath, string.Empty);
                File.Move(mutation.StagedPath, mutation.DestinationPath);
            }
        }

        private static void ReplaceRestoreFile(string replacementPath, string destinationPath, string rollbackPath)
        {
            try
            {
                File.Replace(replacementPath, destinationPath, rollbackPath, true);
                return;
            }
            catch (PlatformNotSupportedException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (IOException)
            {
                if (File.Exists(rollbackPath))
                    throw;
            }

            File.Move(destinationPath, rollbackPath);
            File.Move(replacementPath, destinationPath);
        }

        private static string RollBackRestoreMutations(List<RestoreMutation> mutations)
        {
            StringBuilder errors = new StringBuilder();
            for (int i = mutations.Count - 1; i >= 0; i--)
            {
                RestoreMutation mutation = mutations[i];
                try
                {
                    if (mutation.OriginalExisted)
                    {
                        if (File.Exists(mutation.RollbackPath))
                        {
                            if (File.Exists(mutation.DestinationPath))
                            {
                                TryDeleteRestoreTemporaryFile(mutation.DiscardPath);
                                ReplaceRestoreFile(
                                    mutation.RollbackPath,
                                    mutation.DestinationPath,
                                    mutation.DiscardPath);
                                TryDeleteRestoreTemporaryFile(mutation.DiscardPath);
                            }
                            else
                            {
                                File.Move(mutation.RollbackPath, mutation.DestinationPath);
                            }
                        }
                        else if (!File.Exists(mutation.DestinationPath))
                        {
                            throw new IOException("The durable original and live destination are both missing.");
                        }
                    }
                    else if (mutation.HasReplacement
                        && File.Exists(mutation.AbsentMarkerPath)
                        && !File.Exists(mutation.StagedPath)
                        && File.Exists(mutation.DestinationPath))
                    {
                        File.Delete(mutation.DestinationPath);
                    }
                }
                catch (Exception ex)
                {
                    if (errors.Length > 0)
                        errors.Append(" ");
                    errors.Append(mutation.DestinationPath);
                    errors.Append(": ");
                    errors.Append(ex.Message);
                }
            }

            return errors.ToString();
        }

        private static bool CleanupRestoreTemporaryFiles(List<RestoreMutation> mutations)
        {
            bool succeeded = true;
            for (int i = 0; i < mutations.Count; i++)
            {
                succeeded = TryDeleteRestoreTemporaryFile(mutations[i].StagedPath) && succeeded;
                succeeded = TryDeleteRestoreTemporaryFile(mutations[i].RollbackPath) && succeeded;
                succeeded = TryDeleteRestoreTemporaryFile(mutations[i].AbsentMarkerPath) && succeeded;
                succeeded = TryDeleteRestoreTemporaryFile(mutations[i].DiscardPath) && succeeded;
            }
            return succeeded;
        }

        private FileStream AcquireRestoreRepositoryLock()
        {
            string transactionDirectory = Path.Combine(_root, RestoreTransactionDirectoryName);
            EnsureDirectory(transactionDirectory);
            string lockPath = Path.Combine(transactionDirectory, RestoreRepositoryLockFileName);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(RestoreRepositoryLockTimeoutMilliseconds);

            while (true)
            {
                try
                {
                    return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    if (DateTime.UtcNow >= deadline)
                        throw new IOException("Timed out waiting for the save restore repository lock.");
                    Thread.Sleep(25);
                }
            }
        }

        private bool RecoverIncompleteRestoreTransactions(out string error)
        {
            error = null;
            try
            {
                using (FileStream repositoryLock = AcquireRestoreRepositoryLock())
                    return RecoverIncompleteRestoreTransactionsUnderLock(out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool RecoverIncompleteRestoreTransactionsUnderLock(out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(_root))
                return true;

            string transactionDirectory = Path.Combine(_root, RestoreTransactionDirectoryName);
            if (!Directory.Exists(transactionDirectory))
                return true;

            string[] unpublishedJournals = Directory.GetFiles(
                transactionDirectory,
                "*.json.tmp",
                SearchOption.TopDirectoryOnly);
            for (int i = 0; i < unpublishedJournals.Length; i++)
                TryDeleteRestoreTemporaryFile(unpublishedJournals[i]);

            StringBuilder unresolved = new StringBuilder();
            string[] journalPaths = Directory.GetFiles(transactionDirectory, "*.json", SearchOption.TopDirectoryOnly);
            for (int journalIndex = 0; journalIndex < journalPaths.Length; journalIndex++)
            {
                string journalPath = journalPaths[journalIndex];
                string transactionId = Path.GetFileNameWithoutExtension(journalPath);
                string committedPath = Path.Combine(transactionDirectory, transactionId + ".committed");
                try
                {
                    if (!IsHexValue(transactionId, 32))
                        throw new IOException("Restore transaction file name is not a 32-character hexadecimal GUID.");

                    ManualJsonObject journal;
                    string parseError;
                    if (!ManualJson.TryParseObject(File.ReadAllText(journalPath), out journal, out parseError))
                        throw new IOException("Restore transaction journal is invalid: " + parseError);
                    if (journal.GetInt("schemaVersion", 0) != 1)
                        throw new IOException("Restore transaction journal schema is unsupported.");
                    if (!string.Equals(journal.GetString("transactionId", string.Empty), transactionId, StringComparison.Ordinal))
                        throw new IOException("Restore transaction journal identity does not match its file name.");

                    string manifestPath = journal.GetString("manifestPath", string.Empty);
                    if (string.IsNullOrEmpty(manifestPath)
                        || !Path.IsPathRooted(manifestPath)
                        || !IsPathUnderRoot(_root, manifestPath))
                    {
                        throw new IOException("Restore transaction manifest path is outside backup storage.");
                    }
                    manifestPath = Path.GetFullPath(manifestPath);
                    EnsureNoReparsePointTraversal(GetDirectoryRoot(_root), manifestPath);

                    SaveBackupRestoreDestination restoreDestination = null;
                    ManualJsonObject destinationJson = journal.GetObject("restoreDestination");
                    if (destinationJson != null)
                    {
                        restoreDestination = new SaveBackupRestoreDestination
                        {
                            ScenarioId = destinationJson.GetString("scenarioId", string.Empty),
                            AbsoluteSlot = destinationJson.GetInt("absoluteSlot", 0),
                            ExpectedLineageId = destinationJson.GetString("expectedLineageId", string.Empty),
                            AllowHistoricalSlotWhenUnoccupied =
                                destinationJson.GetBool("allowHistoricalSlotWhenUnoccupied", false)
                        };
                    }

                    RestorePlan validatedPlan = BuildRestorePlan(manifestPath, restoreDestination);
                    AddInterruptedRestoreDeletions(validatedPlan, transactionId);
                    ManualJsonArray allowedRootArray = journal.GetArray("allowedRoots");
                    if (allowedRootArray == null || allowedRootArray.Items.Count == 0)
                        throw new IOException("Restore transaction journal contains no allowed restore roots.");

                    List<RestoreAllowedRoot> journalRoots = new List<RestoreAllowedRoot>();
                    HashSet<string> rootKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < allowedRootArray.Items.Count; i++)
                    {
                        ManualJsonObject item = allowedRootArray.Items[i] != null
                            ? allowedRootArray.Items[i].ObjectValue
                            : null;
                        if (item == null)
                            throw new IOException("Restore transaction journal contains an invalid allowed root.");

                        string path = item.GetString("path", string.Empty);
                        string kindText = item.GetString("kind", string.Empty);
                        if (string.IsNullOrEmpty(path) || !Path.IsPathRooted(path))
                            throw new IOException("Restore transaction journal contains an invalid allowed root path.");
                        if (!string.Equals(kindText, SaveBackupSourceKind.File.ToString(), StringComparison.Ordinal)
                            && !string.Equals(kindText, SaveBackupSourceKind.Directory.ToString(), StringComparison.Ordinal))
                        {
                            throw new IOException("Restore transaction journal contains an invalid allowed root kind.");
                        }

                        RestoreAllowedRoot allowedRoot = new RestoreAllowedRoot();
                        allowedRoot.Path = Path.GetFullPath(path);
                        allowedRoot.Kind = string.Equals(kindText, SaveBackupSourceKind.File.ToString(), StringComparison.Ordinal)
                            ? SaveBackupSourceKind.File
                            : SaveBackupSourceKind.Directory;
                        string rootKey = allowedRoot.Kind + "|" + allowedRoot.Path;
                        if (!rootKeys.Add(rootKey))
                            throw new IOException("Restore transaction journal contains duplicate allowed roots.");
                        journalRoots.Add(allowedRoot);
                    }

                    if (!RestoreAllowedRootsMatch(journalRoots, validatedPlan.AllowedRoots))
                        throw new IOException("Restore transaction roots do not match the validated snapshot restore plan.");

                    Dictionary<string, RestoreExpectedMutation> expectedMutations =
                        BuildExpectedRestoreMutations(validatedPlan);
                    ManualJsonArray mutationArray = journal.GetArray("mutations");
                    if (mutationArray == null || mutationArray.Items.Count == 0)
                        throw new IOException("Restore transaction journal contains no mutations.");

                    List<RestoreMutation> mutations = new List<RestoreMutation>();
                    HashSet<string> destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < mutationArray.Items.Count; i++)
                    {
                        ManualJsonObject item = mutationArray.Items[i] != null
                            ? mutationArray.Items[i].ObjectValue
                            : null;
                        if (item == null)
                            throw new IOException("Restore transaction journal contains an invalid mutation.");

                        string destinationPath = item.GetString("destinationPath", string.Empty);
                        if (string.IsNullOrEmpty(destinationPath) || !Path.IsPathRooted(destinationPath))
                            throw new IOException("Restore transaction journal contains an invalid destination.");

                        RestoreMutation mutation = new RestoreMutation();
                        mutation.DestinationPath = Path.GetFullPath(destinationPath);
                        if (!destinations.Add(mutation.DestinationPath))
                            throw new IOException("Restore transaction journal contains duplicate destinations.");
                        if (!IsRestoreDestinationAllowed(mutation.DestinationPath, validatedPlan.AllowedRoots))
                            throw new IOException("Restore transaction destination is outside the validated restore roots.");
                        EnsureRestoreDestinationHasNoReparseTraversal(
                            mutation.DestinationPath,
                            validatedPlan.AllowedRoots);

                        mutation.OriginalExisted = item.GetBool("originalExisted", false);
                        mutation.HasReplacement = item.GetBool("hasReplacement", false);
                        mutation.ExpectedSize = GetLong(item, "expectedSize", -1);
                        mutation.ExpectedHash = item.GetString("expectedHash", string.Empty);
                        if (mutation.HasReplacement
                            && (mutation.ExpectedSize < 0 || !IsHexValue(mutation.ExpectedHash, 64)))
                        {
                            throw new IOException("Restore transaction replacement verification metadata is invalid.");
                        }
                        if (!mutation.HasReplacement
                            && (mutation.ExpectedSize != 0 || !string.IsNullOrEmpty(mutation.ExpectedHash)))
                        {
                            throw new IOException("Restore transaction deletion verification metadata is invalid.");
                        }

                        RestoreExpectedMutation expectedMutation;
                        if (!expectedMutations.TryGetValue(mutation.DestinationPath, out expectedMutation)
                            || expectedMutation.HasReplacement != mutation.HasReplacement
                            || expectedMutation.ExpectedSize != mutation.ExpectedSize
                            || !string.Equals(
                                expectedMutation.ExpectedHash,
                                mutation.ExpectedHash,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            throw new IOException("Restore transaction mutation does not exactly match the validated restore plan.");
                        }

                        mutation.StagedPath = mutation.HasReplacement
                            ? BuildRestoreTemporaryPath(mutation.DestinationPath, transactionId, "stage")
                            : null;
                        mutation.RollbackPath = BuildRestoreTemporaryPath(mutation.DestinationPath, transactionId, "rollback");
                        mutation.AbsentMarkerPath = BuildRestoreTemporaryPath(mutation.DestinationPath, transactionId, "absent");
                        mutation.DiscardPath = BuildRestoreTemporaryPath(mutation.DestinationPath, transactionId, "discard");
                        mutations.Add(mutation);
                    }
                    if (destinations.Count != expectedMutations.Count)
                        throw new IOException("Restore transaction mutations do not match the validated restore plan.");

                    bool isCommitted = File.Exists(committedPath)
                        && string.Equals(File.ReadAllText(committedPath), transactionId, StringComparison.Ordinal);
                    if (isCommitted)
                    {
                        string validationError = ValidateCommittedRestoreState(mutations);
                        if (!string.IsNullOrEmpty(validationError))
                            throw new IOException("Committed restore state is invalid: " + validationError);
                    }
                    else
                    {
                        string rollbackError = RollBackRestoreMutations(mutations);
                        if (!string.IsNullOrEmpty(rollbackError))
                            throw new IOException("Restore transaction rollback failed: " + rollbackError);
                    }

                    if (!CleanupRestoreTemporaryFiles(mutations))
                        throw new IOException("Restore transaction cleanup remains incomplete.");
                    if (!TryDeleteRestoreTemporaryFile(journalPath))
                        throw new IOException("Restore transaction journal could not be removed.");
                    TryDeleteRestoreTemporaryFile(committedPath);
                    TryDeleteRestoreTemporaryFile(committedPath + ".tmp");
                    MMLog.WriteInfo("[SaveBackup] Recovered restore transaction " + transactionId + ".");
                }
                catch (Exception ex)
                {
                    if (unresolved.Length > 0)
                        unresolved.Append(" ");
                    unresolved.Append(transactionId);
                    unresolved.Append(": ");
                    unresolved.Append(ex.Message);
                    MMLog.WriteWarning("[SaveBackup] Restore transaction " + transactionId
                        + " remains pending: " + ex.Message);
                }
            }

            if (unresolved.Length == 0)
                return true;

            error = unresolved.ToString();
            return false;
        }

        private static bool RestoreAllowedRootsMatch(
            List<RestoreAllowedRoot> persisted,
            List<RestoreAllowedRoot> validated)
        {
            if (persisted == null || validated == null || persisted.Count != validated.Count)
                return false;

            for (int i = 0; i < persisted.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < validated.Count; j++)
                {
                    if (persisted[i].Kind == validated[j].Kind
                        && string.Equals(
                            Path.GetFullPath(persisted[i].Path),
                            Path.GetFullPath(validated[j].Path),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }

        private static bool IsRestoreDestinationAllowed(
            string destinationPath,
            List<RestoreAllowedRoot> allowedRoots)
        {
            if (string.IsNullOrEmpty(destinationPath) || allowedRoots == null)
                return false;

            string fullDestination = Path.GetFullPath(destinationPath);
            for (int i = 0; i < allowedRoots.Count; i++)
            {
                RestoreAllowedRoot allowedRoot = allowedRoots[i];
                if (allowedRoot == null || string.IsNullOrEmpty(allowedRoot.Path))
                    continue;

                if (allowedRoot.Kind == SaveBackupSourceKind.File)
                {
                    if (string.Equals(
                        fullDestination,
                        Path.GetFullPath(allowedRoot.Path),
                        StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (IsPathInsideDirectory(GetDirectoryRoot(allowedRoot.Path), fullDestination))
                {
                    return true;
                }
            }
            return false;
        }

        private static void EnsureRestoreDestinationHasNoReparseTraversal(
            string destinationPath,
            List<RestoreAllowedRoot> allowedRoots)
        {
            string fullDestination = Path.GetFullPath(destinationPath);
            for (int i = 0; i < allowedRoots.Count; i++)
            {
                RestoreAllowedRoot allowedRoot = allowedRoots[i];
                if (allowedRoot == null || string.IsNullOrEmpty(allowedRoot.Path))
                    continue;

                if (allowedRoot.Kind == SaveBackupSourceKind.File
                    && string.Equals(
                        fullDestination,
                        Path.GetFullPath(allowedRoot.Path),
                        StringComparison.OrdinalIgnoreCase))
                {
                    EnsureNoReparsePointTraversal(
                        GetDirectoryRoot(Path.GetDirectoryName(allowedRoot.Path)),
                        fullDestination);
                    return;
                }

                if (allowedRoot.Kind == SaveBackupSourceKind.Directory
                    && IsPathInsideDirectory(GetDirectoryRoot(allowedRoot.Path), fullDestination))
                {
                    EnsureNoReparsePointTraversal(GetDirectoryRoot(allowedRoot.Path), fullDestination);
                    return;
                }
            }

            throw new IOException("Restore transaction destination is outside the validated restore roots.");
        }

        private static Dictionary<string, RestoreExpectedMutation> BuildExpectedRestoreMutations(RestorePlan plan)
        {
            Dictionary<string, RestoreExpectedMutation> expected =
                new Dictionary<string, RestoreExpectedMutation>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < plan.Files.Count; i++)
            {
                RestoreFilePlan file = plan.Files[i];
                expected.Add(
                    file.DestinationPath,
                    new RestoreExpectedMutation
                    {
                        HasReplacement = true,
                        ExpectedSize = (file.Bytes ?? new byte[0]).LongLength,
                        ExpectedHash = ComputeSha256(file.Bytes ?? new byte[0])
                    });
            }

            for (int i = 0; i < plan.Deletions.Count; i++)
            {
                expected.Add(
                    plan.Deletions[i],
                    new RestoreExpectedMutation
                    {
                        HasReplacement = false,
                        ExpectedSize = 0,
                        ExpectedHash = string.Empty
                    });
            }

            return expected;
        }

        private static string ValidateCommittedRestoreState(List<RestoreMutation> mutations)
        {
            for (int i = 0; i < mutations.Count; i++)
            {
                RestoreMutation mutation = mutations[i];
                if (!mutation.HasReplacement)
                {
                    if (File.Exists(mutation.DestinationPath))
                        return mutation.DestinationPath + " was expected to be deleted.";
                    continue;
                }

                if (!File.Exists(mutation.DestinationPath))
                    return mutation.DestinationPath + " is missing.";

                FileInfo file = new FileInfo(mutation.DestinationPath);
                if (file.Length != mutation.ExpectedSize)
                    return mutation.DestinationPath + " has an unexpected size.";
                if (!string.Equals(
                    ComputeSha256(File.ReadAllBytes(mutation.DestinationPath)),
                    mutation.ExpectedHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return mutation.DestinationPath + " has an unexpected hash.";
                }
            }
            return null;
        }

        private static bool IsHexValue(string value, int requiredLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length != requiredLength)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9')
                    || (c >= 'a' && c <= 'f')
                    || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }

        private static void WriteRestoreTransactionJournal(
            string journalPath,
            string transactionId,
            RestorePlan plan,
            List<RestoreMutation> mutations)
        {
            ManualJsonObject journal = new ManualJsonObject();
            journal.Set("schemaVersion", ManualJsonValue.Number(1));
            journal.Set("transactionId", ManualJsonValue.String(transactionId));
            journal.Set("manifestPath", ManualJsonValue.String(plan.ManifestPath));
            if (plan.RestoreDestination != null)
            {
                ManualJsonObject destination = new ManualJsonObject();
                destination.Set(
                    "scenarioId",
                    ManualJsonValue.String(plan.RestoreDestination.ScenarioId));
                destination.Set(
                    "absoluteSlot",
                    ManualJsonValue.Number(plan.RestoreDestination.AbsoluteSlot));
                destination.Set(
                    "expectedLineageId",
                    ManualJsonValue.String(plan.RestoreDestination.ExpectedLineageId));
                destination.Set(
                    "allowHistoricalSlotWhenUnoccupied",
                    ManualJsonValue.Boolean(plan.RestoreDestination.AllowHistoricalSlotWhenUnoccupied));
                journal.Set("restoreDestination", ManualJsonValue.Object(destination));
            }

            ManualJsonArray allowedRootArray = new ManualJsonArray();
            for (int i = 0; i < plan.AllowedRoots.Count; i++)
            {
                RestoreAllowedRoot allowedRoot = plan.AllowedRoots[i];
                ManualJsonObject item = new ManualJsonObject();
                item.Set("path", ManualJsonValue.String(allowedRoot.Path));
                item.Set("kind", ManualJsonValue.String(allowedRoot.Kind.ToString()));
                allowedRootArray.Add(ManualJsonValue.Object(item));
            }
            journal.Set("allowedRoots", ManualJsonValue.Array(allowedRootArray));

            ManualJsonArray mutationArray = new ManualJsonArray();
            for (int i = 0; i < mutations.Count; i++)
            {
                RestoreMutation mutation = mutations[i];
                ManualJsonObject item = new ManualJsonObject();
                item.Set("destinationPath", ManualJsonValue.String(mutation.DestinationPath));
                item.Set("originalExisted", ManualJsonValue.Boolean(mutation.OriginalExisted));
                item.Set("hasReplacement", ManualJsonValue.Boolean(mutation.HasReplacement));
                item.Set("expectedSize", ManualJsonValue.Number(mutation.ExpectedSize));
                item.Set("expectedHash", ManualJsonValue.String(mutation.ExpectedHash));
                mutationArray.Add(ManualJsonValue.Object(item));
            }
            journal.Set("mutations", ManualJsonValue.Array(mutationArray));

            string temporaryPath = journalPath + ".tmp";
            WriteDurableTextFile(temporaryPath, ManualJson.Serialize(journal, true));
            File.Move(temporaryPath, journalPath);
        }

        private static void WriteDurableTextFile(string path, string text)
        {
            WriteDurableBytesFile(path, Encoding.UTF8.GetBytes(text ?? string.Empty));
        }

        private static void PublishDurableFile(string destinationPath, byte[] bytes, bool allowExisting)
        {
            string temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".publish.tmp";
            try
            {
                WriteDurableBytesFile(temporaryPath, bytes);
                ValidatePublishedBytes(temporaryPath, bytes);

                if (File.Exists(destinationPath))
                {
                    if (!allowExisting)
                        throw new IOException("Durable publication destination already exists.");
                }
                else
                {
                    File.Move(temporaryPath, destinationPath);
                }

                ValidatePublishedBytes(destinationPath, bytes);
            }
            finally
            {
                TryDeleteRestoreTemporaryFile(temporaryPath);
            }
        }

        private static void ValidatePublishedBytes(string path, byte[] expectedBytes)
        {
            if (!File.Exists(path))
                throw new IOException("Durable publication output is missing.");

            FileInfo file = new FileInfo(path);
            if (file.Length != expectedBytes.LongLength
                || !string.Equals(
                    ComputeSha256(File.ReadAllBytes(path)),
                    ComputeSha256(expectedBytes),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Durable publication content validation failed.");
            }
        }

        private static void WriteDurableBytesFile(string path, byte[] bytes)
        {
            DurableFileWriter.WriteNew(path, bytes);
        }

        private static bool TryDeleteRestoreTemporaryFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                return !File.Exists(path);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Failed to clean restore temporary file: " + ex.Message);
                return false;
            }
        }

        private List<SaveBackupFileRecord> CaptureFiles(SaveBackupTarget target)
        {
            List<SaveBackupFileRecord> records = new List<SaveBackupFileRecord>();
            for (int i = 0; i < target.Sources.Count; i++)
            {
                SaveBackupSource source = target.Sources[i];
                if (source == null || string.IsNullOrEmpty(source.Path) || string.IsNullOrEmpty(source.Id))
                    continue;

                if (source.Kind == SaveBackupSourceKind.File)
                {
                    if (File.Exists(source.Path))
                        records.Add(CaptureFile(source.Id, source.Path, Path.GetFileName(source.Path)));
                    continue;
                }

                if (!Directory.Exists(source.Path))
                    continue;

                string[] files = Directory.GetFiles(source.Path, "*", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string file = files[fileIndex];
                    records.Add(CaptureFile(source.Id, file, GetRelativePath(source.Path, file)));
                }
            }

            return records;
        }

        private SaveBackupFileRecord CaptureFile(string sourceId, string path, string relativePath)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string hash = ComputeSha256(bytes);
            string blobRelativePath = GetBlobRelativePath(hash);
            string blobPath = Path.Combine(_root, blobRelativePath);

            if (File.Exists(blobPath))
            {
                byte[] existingBytes = SaveBackupBlobCodec.ReadDecompressed(blobPath);
                if (existingBytes.LongLength != bytes.LongLength
                    || !string.Equals(ComputeSha256(existingBytes), hash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("Existing backup blob failed content validation: " + hash);
                }
            }
            else
            {
                EnsureDirectory(Path.GetDirectoryName(blobPath));
                string codecTemporaryPath = blobPath + "." + Guid.NewGuid().ToString("N") + ".codec.tmp";
                try
                {
                    SaveBackupBlobCodec.WriteCompressed(codecTemporaryPath, bytes);
                    byte[] codecRoundTrip = SaveBackupBlobCodec.ReadDecompressed(codecTemporaryPath);
                    if (codecRoundTrip.LongLength != bytes.LongLength
                        || !string.Equals(ComputeSha256(codecRoundTrip), hash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException("New backup blob failed codec round-trip validation: " + hash);
                    }

                    byte[] compressedBytes = File.ReadAllBytes(codecTemporaryPath);
                    PublishDurableFile(blobPath, compressedBytes, true);

                    byte[] publishedBytes = SaveBackupBlobCodec.ReadDecompressed(blobPath);
                    if (publishedBytes.LongLength != bytes.LongLength
                        || !string.Equals(ComputeSha256(publishedBytes), hash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException("Published backup blob failed content validation: " + hash);
                    }
                }
                finally
                {
                    TryDeleteRestoreTemporaryFile(codecTemporaryPath);
                }
            }

            return new SaveBackupFileRecord
            {
                SourceId = sourceId,
                RelativePath = NormalizeManifestPath(relativePath),
                Hash = hash,
                Size = bytes.Length,
                Crc32 = CRC32.Compute(bytes),
                BlobPath = NormalizeManifestPath(blobRelativePath),
                Compression = BlobCompression
            };
        }

        private ManualJsonObject BuildSnapshotManifest(
            string snapshotId,
            DateTime createdAt,
            SaveBackupTarget target,
            SaveBackupReason reason,
            List<SaveBackupFileRecord> files)
        {
            ManualJsonObject root = new ManualJsonObject();
            root.Set("schemaVersion", ManualJsonValue.Number(1));
            root.Set("snapshotId", ManualJsonValue.String(snapshotId));
            root.Set("createdAtUtc", ManualJsonValue.String(createdAt.ToString("o", CultureInfo.InvariantCulture)));
            root.Set("reason", ManualJsonValue.String(reason.ToString()));
            root.Set("timelineKey", ManualJsonValue.String(target.TimelineKey));
            root.Set("saveKind", ManualJsonValue.String(target.SaveKind));
            root.Set("scenarioId", ManualJsonValue.String(target.ScenarioId));
            root.Set("absoluteSlot", ManualJsonValue.Number(target.AbsoluteSlot));
            root.Set("saveId", ManualJsonValue.String(target.SaveId));
            root.Set("saveType", ManualJsonValue.String(target.SaveType.ToString()));
            bool isSafetySnapshot = reason == SaveBackupReason.BeforeRestore
                || reason == SaveBackupReason.BeforeDelete;
            root.Set("isPinned", ManualJsonValue.Boolean(isSafetySnapshot));
            root.Set(
                "pinnedAtUtc",
                ManualJsonValue.String(
                    isSafetySnapshot
                        ? createdAt.ToString("o", CultureInfo.InvariantCulture)
                        : string.Empty));
            root.Set(
                "pinReason",
                ManualJsonValue.String(
                    isSafetySnapshot
                        ? reason.ToString()
                        : string.Empty));

            ManualJsonArray sources = new ManualJsonArray();
            for (int i = 0; i < target.Sources.Count; i++)
            {
                SaveBackupSource source = target.Sources[i];
                if (source == null)
                    continue;

                ManualJsonObject sourceJson = new ManualJsonObject();
                sourceJson.Set("id", ManualJsonValue.String(source.Id));
                sourceJson.Set("kind", ManualJsonValue.String(source.Kind.ToString()));
                sourceJson.Set("path", ManualJsonValue.String(source.Path));
                sources.Add(ManualJsonValue.Object(sourceJson));
            }
            root.Set("sources", ManualJsonValue.Array(sources));

            ManualJsonArray fileArray = new ManualJsonArray();
            for (int i = 0; i < files.Count; i++)
            {
                SaveBackupFileRecord file = files[i];
                ManualJsonObject fileJson = new ManualJsonObject();
                fileJson.Set("sourceId", ManualJsonValue.String(file.SourceId));
                fileJson.Set("relativePath", ManualJsonValue.String(file.RelativePath));
                fileJson.Set("hash", ManualJsonValue.String(file.Hash));
                fileJson.Set("size", ManualJsonValue.Number(file.Size));
                fileJson.Set("crc32", ManualJsonValue.Number(file.Crc32.ToString(CultureInfo.InvariantCulture)));
                fileJson.Set("blobPath", ManualJsonValue.String(file.BlobPath));
                fileJson.Set("compression", ManualJsonValue.String(file.Compression));
                fileArray.Add(ManualJsonValue.Object(fileJson));
            }
            root.Set("files", ManualJsonValue.Array(fileArray));

            return root;
        }

        private void ApplyRetention(string timelineKey, SaveBackupRetentionPolicy policy)
        {
            if (policy == null || policy.Mode != SaveBackupRetentionMode.Limited)
                return;

            int limit = Math.Max(0, policy.SnapshotLimit);
            string timelinePath = GetTimelinePath(timelineKey);
            List<SaveBackupSnapshotRef> refs = ReadTimelineSnapshotRefs(timelinePath);
            refs.Sort(CompareSnapshotRefs);

            int unpinned = 0;
            for (int i = 0; i < refs.Count; i++)
            {
                if (!refs[i].IsPinned)
                    unpinned++;
            }

            for (int i = 0; i < refs.Count && unpinned > limit; i++)
            {
                SaveBackupSnapshotRef snapshot = refs[i];
                if (snapshot.IsPinned)
                    continue;

                try
                {
                    File.Delete(snapshot.ManifestPath);
                    unpinned--;
                    MMLog.WriteDebug("[SaveBackup] Pruned snapshot " + snapshot.SnapshotId
                        + " from timeline " + timelineKey + ".");
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[SaveBackup] Failed to prune snapshot " + snapshot.SnapshotId + ": " + ex.Message);
                }
            }

            WriteIndex();
            PruneUnreferencedBlobs();
        }

        private List<SaveBackupSnapshotRef> ReadAllSnapshotRefs()
        {
            List<SaveBackupSnapshotRef> refs = new List<SaveBackupSnapshotRef>();
            string timelinesRoot = Path.Combine(_root, "timelines");
            if (!Directory.Exists(timelinesRoot))
                return refs;

            string[] timelineDirs = Directory.GetDirectories(timelinesRoot);
            for (int i = 0; i < timelineDirs.Length; i++)
            {
                refs.AddRange(ReadTimelineSnapshotRefs(timelineDirs[i]));
            }

            return refs;
        }

        private List<SaveBackupSnapshotRef> ReadTimelineSnapshotRefs(string timelinePath)
        {
            List<SaveBackupSnapshotRef> refs = new List<SaveBackupSnapshotRef>();
            if (!Directory.Exists(timelinePath))
                return refs;

            string[] manifests = Directory.GetFiles(timelinePath, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < manifests.Length; i++)
            {
                SaveBackupSnapshotRef snapshot;
                if (TryReadSnapshotRef(manifests[i], out snapshot))
                    refs.Add(snapshot);
            }

            return refs;
        }

        private bool TryReadSnapshotRef(string manifestPath, out SaveBackupSnapshotRef snapshot)
        {
            snapshot = null;
            try
            {
                ManualJsonObject root;
                string error;
                if (!ManualJson.TryParseObject(File.ReadAllText(manifestPath), out root, out error))
                    return false;
                snapshot = BuildSnapshotRef(root, manifestPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void WriteIndex()
        {
            try
            {
                List<SaveBackupSnapshotRef> refs = ReadAllSnapshotRefs();
                refs.Sort(CompareSnapshotRefs);

                ManualJsonObject root = new ManualJsonObject();
                root.Set("schemaVersion", ManualJsonValue.Number(1));
                root.Set("updatedAtUtc", ManualJsonValue.String(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));

                ManualJsonArray snapshots = new ManualJsonArray();
                for (int i = 0; i < refs.Count; i++)
                {
                    SaveBackupSnapshotRef snapshot = refs[i];
                    ManualJsonObject item = new ManualJsonObject();
                    item.Set("snapshotId", ManualJsonValue.String(snapshot.SnapshotId));
                    item.Set("timelineKey", ManualJsonValue.String(snapshot.TimelineKey));
                    item.Set("createdAtUtc", ManualJsonValue.String(snapshot.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture)));
                    item.Set("isPinned", ManualJsonValue.Boolean(snapshot.IsPinned));
                    item.Set("manifestPath", ManualJsonValue.String(NormalizeManifestPath(GetRelativePath(_root, snapshot.ManifestPath))));
                    snapshots.Add(ManualJsonValue.Object(item));
                }
                root.Set("snapshots", ManualJsonValue.Array(snapshots));

                File.WriteAllText(Path.Combine(_root, "index.json"), ManualJson.Serialize(root, true));
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Failed to update backup index: " + ex.Message);
            }
        }

        private void PruneUnreferencedBlobs()
        {
            try
            {
                string blobsRoot = Path.Combine(_root, "blobs");
                if (!Directory.Exists(blobsRoot))
                    return;

                HashSet<string> referencedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                List<SaveBackupSnapshotRef> refs = ReadAllSnapshotRefs();
                for (int i = 0; i < refs.Count; i++)
                {
                    AddReferencedHashes(refs[i].ManifestPath, referencedHashes);
                }

                string[] blobs = Directory.GetFiles(blobsRoot, "*" + BlobExtension, SearchOption.AllDirectories);
                for (int i = 0; i < blobs.Length; i++)
                {
                    string fileName = Path.GetFileName(blobs[i]);
                    string hash = fileName != null && fileName.EndsWith(BlobExtension, StringComparison.OrdinalIgnoreCase)
                        ? fileName.Substring(0, fileName.Length - BlobExtension.Length)
                        : string.Empty;

                    if (hash.Length == 0 || referencedHashes.Contains(hash))
                        continue;

                    try { File.Delete(blobs[i]); }
                    catch (Exception ex) { MMLog.WriteWarning("[SaveBackup] Failed to delete unused blob: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Blob cleanup failed: " + ex.Message);
            }
        }

        private void AddReferencedHashes(string manifestPath, HashSet<string> hashes)
        {
            try
            {
                ManualJsonObject root;
                string error;
                if (!ManualJson.TryParseObject(File.ReadAllText(manifestPath), out root, out error))
                    return;

                ManualJsonArray files = root.GetArray("files");
                if (files == null)
                    return;

                for (int i = 0; i < files.Items.Count; i++)
                {
                    ManualJsonObject file = files.Items[i] != null ? files.Items[i].ObjectValue : null;
                    if (file == null)
                        continue;

                    string hash = file.GetString("hash", string.Empty);
                    if (!string.IsNullOrEmpty(hash))
                        hashes.Add(hash);
                }
            }
            catch
            {
            }
        }

        private string GetTimelinePath(string timelineKey)
        {
            return Path.Combine(Path.Combine(_root, "timelines"), SanitizePathSegment(timelineKey));
        }

        private static int CompareSnapshotRefs(SaveBackupSnapshotRef left, SaveBackupSnapshotRef right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;

            int date = left.CreatedAtUtc.CompareTo(right.CreatedAtUtc);
            return date != 0 ? date : string.Compare(left.SnapshotId, right.SnapshotId, StringComparison.Ordinal);
        }

        private static bool IsSnapshotAfter(SaveBackupSnapshotRef snapshot, DateTime createdAtUtc, string snapshotId)
        {
            if (snapshot == null)
                return false;

            SaveBackupSnapshotRef boundary = new SaveBackupSnapshotRef
            {
                SnapshotId = snapshotId ?? string.Empty,
                CreatedAtUtc = createdAtUtc.ToUniversalTime()
            };

            return CompareSnapshotRefs(snapshot, boundary) > 0;
        }

        private static SaveManager.SaveType ReadSaveType(string value)
        {
            if (string.IsNullOrEmpty(value))
                return SaveManager.SaveType.Invalid;

            try
            {
                return (SaveManager.SaveType)Enum.Parse(typeof(SaveManager.SaveType), value, true);
            }
            catch
            {
                return SaveManager.SaveType.Invalid;
            }
        }

        private static string BuildSnapshotId(DateTime createdAt)
        {
            return createdAt.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture)
                + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static string GetBlobRelativePath(string hash)
        {
            string prefix = !string.IsNullOrEmpty(hash) && hash.Length >= 2 ? hash.Substring(0, 2) : "xx";
            return Path.Combine(Path.Combine("blobs", prefix), hash + BlobExtension);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256Managed sha = new SHA256Managed())
            {
                return ToHex(sha.ComputeHash(bytes ?? new byte[0]));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            char[] chars = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = hex[bytes[i] >> 4];
                chars[i * 2 + 1] = hex[bytes[i] & 15];
            }

            return new string(chars);
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static string GetRelativePath(string root, string path)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(path))
                return path ?? string.Empty;

            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(fullRoot.Length);

            return Path.GetFileName(path);
        }

        private static bool IsPathUnderRoot(string root, string path)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(path))
                return false;

            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeManifestPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
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

        private sealed class RestoreFilePlan
        {
            public string DestinationPath;
            public byte[] Bytes;
        }

        private sealed class RestorePlan
        {
            public string ManifestPath;
            public SaveBackupRestoreDestination RestoreDestination;
            public readonly List<RestoreFilePlan> Files = new List<RestoreFilePlan>();
            public readonly List<string> Deletions = new List<string>();
            public readonly List<RestoreAllowedRoot> AllowedRoots = new List<RestoreAllowedRoot>();
        }

        private sealed class RestoreAllowedRoot
        {
            public string Path;
            public SaveBackupSourceKind Kind;
        }

        private sealed class RestoreExpectedMutation
        {
            public bool HasReplacement;
            public long ExpectedSize;
            public string ExpectedHash;
        }

        private sealed class RestoreMutation
        {
            public string DestinationPath;
            public string StagedPath;
            public string RollbackPath;
            public string AbsentMarkerPath;
            public string DiscardPath;
            public bool OriginalExisted;
            public bool HasReplacement;
            public byte[] ReplacementBytes;
            public long ExpectedSize;
            public string ExpectedHash;
        }
    }
}
