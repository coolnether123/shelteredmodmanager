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
            string existing = TryReadLineageId(path);
            if (!string.IsNullOrEmpty(existing))
                return existing;

            string lineageId = Guid.NewGuid().ToString("N");
            TryWriteLineageId(path, lineageId);
            return lineageId;
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
                if (!ManualJson.TryParseObject(File.ReadAllText(path), out root, out error))
                    return null;

                string lineageId = root.GetString("lineageId", string.Empty);
                return string.IsNullOrEmpty(lineageId) ? null : lineageId;
            }
            catch
            {
                return null;
            }
        }

        private static void TryWriteLineageId(string path, string lineageId)
        {
            try
            {
                ManualJsonObject root = new ManualJsonObject();
                root.Set("schemaVersion", ManualJsonValue.Number(1));
                root.Set("lineageId", ManualJsonValue.String(lineageId));
                root.Set("createdAtUtc", ManualJsonValue.String(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
                File.WriteAllText(path, ManualJson.Serialize(root, true));
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Failed to write backup lineage metadata: " + ex.Message);
            }
        }
    }
}
