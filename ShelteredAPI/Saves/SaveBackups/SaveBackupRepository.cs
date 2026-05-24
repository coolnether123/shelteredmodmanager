using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using ModAPI.Core;
using ModAPI.Util;

namespace ShelteredAPI.Saves.Backups
{
    internal sealed class SaveBackupRepository
    {
        private const string BlobCompression = SaveBackupBlobCodec.CompressionName;
        private const string BlobExtension = ".bin.slz";
        private readonly string _root;

        public SaveBackupRepository(string root)
        {
            _root = root;
        }

        public string CreateSnapshot(SaveBackupTarget target, SaveBackupReason reason, SaveBackupRetentionPolicy policy)
        {
            if (target == null || string.IsNullOrEmpty(target.TimelineKey))
                return null;
            if (policy == null || !policy.IsEnabled)
                return null;

            try
            {
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
                File.WriteAllText(manifestPath, ManualJson.Serialize(manifest, true));

                WriteIndex();
                ApplyRetention(target.TimelineKey, policy);

                MMLog.WriteInfo("[SaveBackup] Created snapshot " + snapshotId
                    + " for " + target.SaveKind + " timeline " + target.TimelineKey + ".");
                return snapshotId;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SaveBackup] Snapshot failed: " + ex.Message);
                return null;
            }
        }

        public bool RestoreSnapshot(string manifestPath, out string error)
        {
            error = null;
            try
            {
                List<RestoreFilePlan> files = BuildRestorePlan(manifestPath);
                for (int i = 0; i < files.Count; i++)
                {
                    RestoreFilePlan file = files[i];
                    EnsureDirectory(Path.GetDirectoryName(file.DestinationPath));
                    WriteFileAtomically(file.DestinationPath, file.Bytes);
                }

                MMLog.WriteInfo("[SaveBackup] Restored snapshot " + Path.GetFileNameWithoutExtension(manifestPath)
                    + " with " + files.Count + " file(s).");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                MMLog.WriteError("[SaveBackup] Restore failed: " + ex.Message);
                return false;
            }
        }

        private List<RestoreFilePlan> BuildRestorePlan(string manifestPath)
        {
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
                throw new FileNotFoundException("Backup manifest was not found.", manifestPath);

            ManualJsonObject root;
            string parseError;
            if (!ManualJson.TryParseObject(File.ReadAllText(manifestPath), out root, out parseError))
                throw new InvalidDataException("Backup manifest is invalid: " + parseError);

            Dictionary<string, SaveBackupSource> sources = ReadRestoreSources(root);
            ManualJsonArray fileArray = root.GetArray("files");
            if (fileArray == null || fileArray.Items.Count == 0)
                throw new InvalidDataException("Backup manifest contains no files.");

            List<RestoreFilePlan> plan = new List<RestoreFilePlan>();
            for (int i = 0; i < fileArray.Items.Count; i++)
            {
                ManualJsonObject file = fileArray.Items[i] != null ? fileArray.Items[i].ObjectValue : null;
                if (file == null)
                    continue;

                RestoreFilePlan item = BuildRestoreFilePlan(file, sources);
                plan.Add(item);
            }

            if (plan.Count == 0)
                throw new InvalidDataException("Backup manifest contains no restorable files.");

            return plan;
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
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(path))
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

        private RestoreFilePlan BuildRestoreFilePlan(ManualJsonObject file, Dictionary<string, SaveBackupSource> sources)
        {
            string sourceId = file.GetString("sourceId", string.Empty);
            SaveBackupSource source;
            if (string.IsNullOrEmpty(sourceId) || !sources.TryGetValue(sourceId, out source))
                throw new InvalidDataException("Backup manifest references an unknown source: " + sourceId);

            string compression = file.GetString("compression", string.Empty);
            if (!string.Equals(compression, BlobCompression, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Backup blob uses unsupported compression: " + compression);

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

        private static void VerifyRestoreBytes(ManualJsonObject file, byte[] bytes)
        {
            long expectedSize = GetLong(file, "size", -1);
            if (expectedSize >= 0 && bytes.Length != expectedSize)
                throw new InvalidDataException("Backup blob size check failed.");

            string expectedHash = file.GetString("hash", string.Empty);
            if (!string.IsNullOrEmpty(expectedHash) && !string.Equals(ComputeSha256(bytes), expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Backup blob hash check failed.");

            long expectedCrc = GetLong(file, "crc32", -1);
            if (expectedCrc >= 0 && CRC32.Compute(bytes) != unchecked((uint)expectedCrc))
                throw new InvalidDataException("Backup blob CRC check failed.");
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
                throw new InvalidDataException("Restore root path is missing.");
            if (string.IsNullOrEmpty(relativePath))
                throw new InvalidDataException("Restore relative path is missing.");

            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Backup manifest path escapes its restore root.");

            return fullPath;
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

        private static void WriteFileAtomically(string path, byte[] bytes)
        {
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".restore.tmp";
            try
            {
                File.WriteAllBytes(tmp, bytes ?? new byte[0]);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
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

            if (!File.Exists(blobPath))
            {
                EnsureDirectory(Path.GetDirectoryName(blobPath));
                string tmp = blobPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    SaveBackupBlobCodec.WriteCompressed(tmp, bytes);
                    if (File.Exists(blobPath))
                        File.Delete(tmp);
                    else
                        File.Move(tmp, blobPath);
                }
                finally
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
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
            root.Set("isPinned", ManualJsonValue.Boolean(false));
            root.Set("pinnedAtUtc", ManualJsonValue.String(string.Empty));
            root.Set("pinReason", ManualJsonValue.String(string.Empty));

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

                DateTime createdAt;
                string created = root.GetString("createdAtUtc", string.Empty);
                if (!DateTime.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out createdAt))
                    createdAt = File.GetCreationTimeUtc(manifestPath);

                snapshot = new SaveBackupSnapshotRef
                {
                    SnapshotId = root.GetString("snapshotId", Path.GetFileNameWithoutExtension(manifestPath)),
                    TimelineKey = root.GetString("timelineKey", string.Empty),
                    ManifestPath = manifestPath,
                    CreatedAtUtc = createdAt.ToUniversalTime(),
                    IsPinned = root.GetBool("isPinned", false)
                };
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
    }
}
