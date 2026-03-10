using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class FileDocumentService : IDocumentService
    {
        private readonly object _preloadSync = new object();
        private readonly Dictionary<string, PreloadedDocumentSnapshot> _preloadedSnapshots = new Dictionary<string, PreloadedDocumentSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _preloadsInFlight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public DocumentSession Open(string filePath)
        {
            var snapshot = LoadSnapshot(filePath, true);
            return CreateSession(snapshot);
        }

        public void Preload(string filePath)
        {
            var fullPath = NormalizePath(filePath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                return;
            }

            var lastWriteUtc = File.GetLastWriteTimeUtc(fullPath);
            lock (_preloadSync)
            {
                PreloadedDocumentSnapshot cached;
                if (_preloadedSnapshots.TryGetValue(fullPath, out cached) &&
                    cached != null &&
                    cached.LastWriteUtc == lastWriteUtc)
                {
                    return;
                }

                if (_preloadsInFlight.Contains(fullPath))
                {
                    return;
                }

                _preloadsInFlight.Add(fullPath);
            }

            ThreadPool.QueueUserWorkItem(PreloadWorker, fullPath);
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
            session.TextVersion++;
            session.HasExternalChanges = false;
            session.LastTextMutationUtc = DateTime.UtcNow;
            StoreSnapshot(session.FilePath, desiredText, session.LastKnownWriteUtc);
            return true;
        }

        public bool Reload(DocumentSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.FilePath) || !File.Exists(session.FilePath))
            {
                return false;
            }

            var snapshot = LoadSnapshot(session.FilePath, false);
            session.Text = snapshot.Text;
            session.OriginalTextSnapshot = snapshot.Text;
            session.LastKnownWriteUtc = snapshot.LastWriteUtc;
            session.IsDirty = false;
            session.TextVersion++;
            session.HasExternalChanges = false;
            session.LastTextMutationUtc = DateTime.UtcNow;
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

        private void PreloadWorker(object state)
        {
            var fullPath = state as string;
            try
            {
                if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                {
                    var snapshot = ReadSnapshot(fullPath);
                    StoreSnapshot(snapshot.FilePath, snapshot.Text, snapshot.LastWriteUtc);
                }
            }
            catch
            {
            }
            finally
            {
                lock (_preloadSync)
                {
                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        _preloadsInFlight.Remove(fullPath);
                    }
                }
            }
        }

        private DocumentSession CreateSession(PreloadedDocumentSnapshot snapshot)
        {
            var session = new DocumentSession();
            session.FilePath = snapshot.FilePath;
            session.Text = snapshot.Text;
            session.OriginalTextSnapshot = snapshot.Text;
            session.IsDirty = false;
            session.TextVersion = 1;
            session.LastLanguageAnalysisVersion = 0;
            session.LastKnownWriteUtc = snapshot.LastWriteUtc;
            session.LastTextMutationUtc = DateTime.UtcNow;
            session.HasExternalChanges = false;
            return session;
        }

        private PreloadedDocumentSnapshot LoadSnapshot(string filePath, bool preferCache)
        {
            var fullPath = NormalizePath(filePath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                return new PreloadedDocumentSnapshot(fullPath, string.Empty, DateTime.MinValue);
            }

            var lastWriteUtc = File.GetLastWriteTimeUtc(fullPath);
            if (preferCache)
            {
                lock (_preloadSync)
                {
                    PreloadedDocumentSnapshot cached;
                    if (_preloadedSnapshots.TryGetValue(fullPath, out cached) &&
                        cached != null &&
                        cached.LastWriteUtc == lastWriteUtc)
                    {
                        return cached;
                    }
                }
            }

            return ReadSnapshot(fullPath, lastWriteUtc);
        }

        private PreloadedDocumentSnapshot ReadSnapshot(string filePath)
        {
            return ReadSnapshot(filePath, File.Exists(filePath) ? File.GetLastWriteTimeUtc(filePath) : DateTime.MinValue);
        }

        private PreloadedDocumentSnapshot ReadSnapshot(string filePath, DateTime lastWriteUtc)
        {
            var fullPath = NormalizePath(filePath);
            var text = !string.IsNullOrEmpty(fullPath) && File.Exists(fullPath)
                ? File.ReadAllText(fullPath)
                : string.Empty;
            StoreSnapshot(fullPath, text, lastWriteUtc);
            return new PreloadedDocumentSnapshot(fullPath, text, lastWriteUtc);
        }

        private void StoreSnapshot(string filePath, string text, DateTime lastWriteUtc)
        {
            var fullPath = NormalizePath(filePath);
            if (string.IsNullOrEmpty(fullPath))
            {
                return;
            }

            lock (_preloadSync)
            {
                _preloadedSnapshots[fullPath] = new PreloadedDocumentSnapshot(fullPath, text ?? string.Empty, lastWriteUtc);
            }
        }

        private static string NormalizePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(filePath);
            }
            catch
            {
                return string.Empty;
            }
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

        private sealed class PreloadedDocumentSnapshot
        {
            public PreloadedDocumentSnapshot(string filePath, string text, DateTime lastWriteUtc)
            {
                FilePath = filePath ?? string.Empty;
                Text = text ?? string.Empty;
                LastWriteUtc = lastWriteUtc;
            }

            public string FilePath;
            public string Text;
            public DateTime LastWriteUtc;
        }
    }
}
