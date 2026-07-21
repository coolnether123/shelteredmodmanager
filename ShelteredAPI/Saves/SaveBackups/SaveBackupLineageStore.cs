using System;
using System.Globalization;
using System.IO;
using ModAPI.Core;
using ModAPI.Util;

namespace ShelteredAPI.Saves.Backups
{
    internal static class SaveBackupLineageStore
    {
        private const string IdentityFileName = "backup.identity.json";

        internal static string EnsureCustomLineageId(string slotRoot)
        {
            if (string.IsNullOrEmpty(slotRoot) || !Directory.Exists(slotRoot))
                return null;

            string path = Path.Combine(slotRoot, IdentityFileName);
            if (File.Exists(path))
            {
                string existing = TryReadLineageId(path);
                if (!string.IsNullOrEmpty(existing))
                    return existing;

                MMLog.WriteWarning("[SaveBackup] Existing backup lineage metadata is malformed; refusing to replace it.");
                return null;
            }

            string lineageId = Guid.NewGuid().ToString("N");
            return TryWriteLineageId(path, lineageId) ? TryReadLineageId(path) : null;
        }

        internal static string TryReadCustomLineageId(string slotRoot)
        {
            if (string.IsNullOrEmpty(slotRoot) || !Directory.Exists(slotRoot))
                return null;

            return TryReadLineageId(Path.Combine(slotRoot, IdentityFileName));
        }

        private static string TryReadLineageId(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return null;

                ManualJsonObject root;
                string error;
                if (!ManualJson.TryParseObject(File.ReadAllText(path), out root, out error)
                    || root.GetInt("schemaVersion", 0) != 1)
                    return null;

                string lineageId = root.GetString("lineageId", string.Empty);
                return IsValidLineageId(lineageId) ? lineageId : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsValidLineageId(string lineageId)
        {
            if (string.IsNullOrEmpty(lineageId) || lineageId.Length != 32)
                return false;

            for (int i = 0; i < lineageId.Length; i++)
            {
                char value = lineageId[i];
                if (!((value >= '0' && value <= '9')
                    || (value >= 'a' && value <= 'f')
                    || (value >= 'A' && value <= 'F')))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryWriteLineageId(string path, string lineageId)
        {
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                ManualJsonObject root = new ManualJsonObject();
                root.Set("schemaVersion", ManualJsonValue.Number(1));
                root.Set("lineageId", ManualJsonValue.String(lineageId));
                root.Set("createdAtUtc", ManualJsonValue.String(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));

                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(ManualJson.Serialize(root, true));
                DurableFileWriter.WriteNew(temporaryPath, bytes);

                if (File.Exists(path))
                {
                    string racedLineageId = TryReadLineageId(path);
                    return string.Equals(racedLineageId, lineageId, StringComparison.OrdinalIgnoreCase)
                        || !string.IsNullOrEmpty(racedLineageId);
                }

                File.Move(temporaryPath, path);
                string publishedLineageId = TryReadLineageId(path);
                if (!string.Equals(publishedLineageId, lineageId, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Published backup lineage metadata failed readback validation.");

                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Failed to write backup lineage metadata: " + ex.Message);
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch
                {
                }
            }
        }
    }
}
