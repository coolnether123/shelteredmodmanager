using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class FileDocumentService : IDocumentService
    {
        public DocumentSession Open(string filePath)
        {
            var session = new DocumentSession();
            session.FilePath = filePath;
            session.Text = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
            session.OriginalTextSnapshot = session.Text;
            session.IsDirty = false;
            session.LastKnownWriteUtc = File.Exists(filePath) ? File.GetLastWriteTimeUtc(filePath) : DateTime.MinValue;
            session.HasExternalChanges = false;
            return session;
        }

        public bool Save(DocumentSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.FilePath))
            {
                return false;
            }

            var dir = Path.GetDirectoryName(session.FilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var desiredText = session.Text ?? string.Empty;
            var currentDiskText = File.Exists(session.FilePath) ? File.ReadAllText(session.FilePath) : string.Empty;
            var snapshotText = session.OriginalTextSnapshot ?? currentDiskText;
            string updatedDiskText;
            if (!TryBuildScopedSaveText(currentDiskText, snapshotText, desiredText, out updatedDiskText))
            {
                session.HasExternalChanges = true;
                return false;
            }

            File.WriteAllText(session.FilePath, updatedDiskText);
            session.LastKnownWriteUtc = File.GetLastWriteTimeUtc(session.FilePath);
            session.Text = desiredText;
            session.OriginalTextSnapshot = desiredText;
            session.IsDirty = false;
            session.HasExternalChanges = false;
            return true;
        }

        public bool Reload(DocumentSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.FilePath) || !File.Exists(session.FilePath))
            {
                return false;
            }

            session.Text = File.ReadAllText(session.FilePath);
            session.OriginalTextSnapshot = session.Text;
            session.LastKnownWriteUtc = File.GetLastWriteTimeUtc(session.FilePath);
            session.IsDirty = false;
            session.HasExternalChanges = false;
            return true;
        }

        public bool HasExternalChanges(DocumentSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.FilePath) || !File.Exists(session.FilePath))
            {
                return false;
            }

            var changed = File.GetLastWriteTimeUtc(session.FilePath) > session.LastKnownWriteUtc;
            session.HasExternalChanges = changed;
            return changed;
        }

        private static bool TryBuildScopedSaveText(string currentDiskText, string snapshotText, string desiredText, out string updatedDiskText)
        {
            updatedDiskText = desiredText;
            currentDiskText = currentDiskText ?? string.Empty;
            snapshotText = snapshotText ?? string.Empty;
            desiredText = desiredText ?? string.Empty;

            if (string.Equals(currentDiskText, snapshotText, StringComparison.Ordinal))
            {
                updatedDiskText = desiredText;
                return true;
            }

            var prefixLength = 0;
            var prefixMax = Math.Min(snapshotText.Length, desiredText.Length);
            while (prefixLength < prefixMax && snapshotText[prefixLength] == desiredText[prefixLength])
            {
                prefixLength++;
            }

            var snapshotSuffixLength = snapshotText.Length - prefixLength;
            var desiredSuffixLength = desiredText.Length - prefixLength;
            var suffixLength = 0;
            while (suffixLength < snapshotSuffixLength &&
                   suffixLength < desiredSuffixLength &&
                   snapshotText[snapshotText.Length - 1 - suffixLength] == desiredText[desiredText.Length - 1 - suffixLength])
            {
                suffixLength++;
            }

            if (prefixLength + suffixLength > snapshotText.Length)
            {
                suffixLength = snapshotText.Length - prefixLength;
            }

            var prefix = snapshotText.Substring(0, prefixLength);
            var suffix = suffixLength > 0 ? snapshotText.Substring(snapshotText.Length - suffixLength) : string.Empty;
            var replacement = desiredText.Substring(prefixLength, desiredText.Length - prefixLength - suffixLength);

            if (!currentDiskText.StartsWith(prefix, StringComparison.Ordinal) ||
                !currentDiskText.EndsWith(suffix, StringComparison.Ordinal))
            {
                return false;
            }

            updatedDiskText = currentDiskText.Substring(0, prefixLength) +
                replacement +
                (suffixLength > 0 ? currentDiskText.Substring(currentDiskText.Length - suffixLength) : string.Empty);
            return true;
        }
    }
}
