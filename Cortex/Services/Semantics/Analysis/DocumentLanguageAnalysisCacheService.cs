using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Analysis
{
    internal sealed class DocumentLanguageAnalysisCacheService
    {
        public void TryRestoreFromRecentCache(DocumentSession session, Action<string> logInfo)
        {
            if (session == null || session.LanguageAnalysisHistory == null || session.LanguageAnalysisHistory.Count == 0)
            {
                return;
            }

            if (session.LastLanguageCacheRestoreVersion == session.TextVersion)
            {
                return;
            }

            DocumentLanguageAnalysisSnapshot cached = null;
            var currentText = session.Text ?? string.Empty;
            for (var i = 0; i < session.LanguageAnalysisHistory.Count; i++)
            {
                var candidate = session.LanguageAnalysisHistory[i];
                if (candidate != null && string.Equals(candidate.TextSnapshot ?? string.Empty, currentText, StringComparison.Ordinal))
                {
                    cached = candidate;
                    break;
                }
            }

            if (cached == null)
            {
                return;
            }

            var restored = DocumentLanguageAnalysisCloneUtility.CloneLanguageAnalysis(session.LanguageAnalysis);
            restored.DocumentPath = session.FilePath ?? restored.DocumentPath;
            restored.DocumentVersion = session.TextVersion;
            var restoredAny = false;
            if (cached.HasClassifications)
            {
                restored.Classifications = DocumentLanguageAnalysisCloneUtility.CloneClassifications(cached.Analysis != null ? cached.Analysis.Classifications : null);
                restoredAny = true;
            }

            if (cached.HasDiagnostics)
            {
                restored.Diagnostics = DocumentLanguageAnalysisCloneUtility.CloneDiagnostics(cached.Analysis != null ? cached.Analysis.Diagnostics : null);
                restoredAny = true;
            }

            if (!restoredAny)
            {
                return;
            }

            session.LanguageAnalysis = restored;
            session.LastLanguageAnalysisUtc = DateTime.UtcNow;
            session.LastLanguageCacheRestoreVersion = session.TextVersion;
            session.PendingLanguageInvalidation = new EditorInvalidation();
            if (logInfo != null)
            {
                logInfo("[Cortex.Roslyn] Restored cached analysis for " + Path.GetFileName(session.FilePath) + ". Classifications=" + cached.HasClassifications + ", Diagnostics=" + cached.HasDiagnostics + ".");
            }
        }

        public void RememberSnapshot(DocumentSession session)
        {
            if (session == null || session.LanguageAnalysisHistory == null || session.LanguageAnalysis == null)
            {
                return;
            }

            var hasClassifications = session.LastLanguageClassificationVersion == session.TextVersion && session.LanguageAnalysis.Classifications != null;
            var hasDiagnostics = session.LastLanguageDiagnosticVersion == session.TextVersion && session.LanguageAnalysis.Diagnostics != null;
            if (!hasClassifications && !hasDiagnostics)
            {
                return;
            }

            var textSnapshot = session.Text ?? string.Empty;
            DocumentLanguageAnalysisSnapshot existing = null;
            for (var i = 0; i < session.LanguageAnalysisHistory.Count; i++)
            {
                var candidate = session.LanguageAnalysisHistory[i];
                if (candidate != null && string.Equals(candidate.TextSnapshot ?? string.Empty, textSnapshot, StringComparison.Ordinal))
                {
                    existing = candidate;
                    session.LanguageAnalysisHistory.RemoveAt(i);
                    break;
                }
            }

            var snapshot = existing ?? new DocumentLanguageAnalysisSnapshot();
            snapshot.TextSnapshot = textSnapshot;
            snapshot.Analysis = DocumentLanguageAnalysisCloneUtility.CloneLanguageAnalysis(session.LanguageAnalysis);
            snapshot.Analysis.DocumentVersion = session.TextVersion;
            if (!hasClassifications)
            {
                snapshot.Analysis.Classifications = new LanguageServiceClassifiedSpan[0];
            }

            if (!hasDiagnostics)
            {
                snapshot.Analysis.Diagnostics = new LanguageServiceDiagnostic[0];
            }

            snapshot.HasClassifications = hasClassifications;
            snapshot.HasDiagnostics = hasDiagnostics;
            snapshot.CachedUtc = DateTime.UtcNow;
            session.LanguageAnalysisHistory.Insert(0, snapshot);
            while (session.LanguageAnalysisHistory.Count > 3)
            {
                session.LanguageAnalysisHistory.RemoveAt(session.LanguageAnalysisHistory.Count - 1);
            }
        }
    }
}
