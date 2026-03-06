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

            File.WriteAllText(session.FilePath, session.Text ?? string.Empty);
            session.LastKnownWriteUtc = File.GetLastWriteTimeUtc(session.FilePath);
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
    }
}
