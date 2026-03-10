using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal sealed class DocumentLanguageAnalysisService
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

            var restored = CloneLanguageAnalysis(session.LanguageAnalysis);
            restored.DocumentPath = session.FilePath ?? restored.DocumentPath;
            restored.DocumentVersion = session.TextVersion;
            var restoredAny = false;
            if (cached.HasClassifications)
            {
                restored.Classifications = CloneClassifications(cached.Analysis != null ? cached.Analysis.Classifications : null);
                restoredAny = true;
            }

            if (cached.HasDiagnostics)
            {
                restored.Diagnostics = CloneDiagnostics(cached.Analysis != null ? cached.Analysis.Diagnostics : null);
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
                logInfo("[Cortex.Roslyn] Restored cached analysis for " +
                    Path.GetFileName(session.FilePath) +
                    ". Classifications=" + cached.HasClassifications +
                    ", Diagnostics=" + cached.HasDiagnostics + ".");
            }
        }

        public void RememberSnapshot(DocumentSession session)
        {
            if (session == null || session.LanguageAnalysisHistory == null || session.LanguageAnalysis == null)
            {
                return;
            }

            var hasClassifications =
                session.LastLanguageClassificationVersion == session.TextVersion &&
                session.LanguageAnalysis.Classifications != null;
            var hasDiagnostics =
                session.LastLanguageDiagnosticVersion == session.TextVersion &&
                session.LanguageAnalysis.Diagnostics != null;
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
            snapshot.Analysis = CloneLanguageAnalysis(session.LanguageAnalysis);
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

        public void ApplyProvisionalClassificationProjection(DocumentSession session)
        {
            if (session == null ||
                session.PendingLanguageInvalidation == null ||
                session.LanguageAnalysis == null ||
                session.LanguageAnalysis.Classifications == null ||
                session.LastLanguageClassificationVersion == session.TextVersion)
            {
                return;
            }

            var invalidation = session.PendingLanguageInvalidation;
            if (invalidation.CurrentContextLength <= 0 && invalidation.PreviousContextLength <= 0)
            {
                return;
            }

            var projected = CloneLanguageAnalysis(session.LanguageAnalysis);
            projected.DocumentPath = session.FilePath ?? projected.DocumentPath;
            projected.DocumentVersion = session.TextVersion;
            projected.Classifications = ProjectClassifications(projected.Classifications, invalidation);
            session.LanguageAnalysis = projected;
            session.LastLanguageAnalysisUtc = DateTime.UtcNow;
        }

        public IncrementalClassificationRange BuildIncrementalClassificationRange(DocumentSession session)
        {
            if (session == null ||
                session.PendingLanguageInvalidation == null ||
                session.LanguageAnalysis == null ||
                session.LanguageAnalysis.Classifications == null ||
                session.LanguageAnalysis.Classifications.Length == 0)
            {
                return null;
            }

            var invalidation = session.PendingLanguageInvalidation;
            if (!invalidation.CanUseIncrementalLanguageAnalysis ||
                invalidation.OldLength != invalidation.NewLength ||
                invalidation.PreviousContextStart != invalidation.CurrentContextStart ||
                invalidation.PreviousContextLength != invalidation.CurrentContextLength ||
                invalidation.CurrentContextLength <= 0 ||
                invalidation.PreviousContextLength < 0)
            {
                return null;
            }

            return new IncrementalClassificationRange
            {
                RequestStart = invalidation.CurrentContextStart,
                RequestLength = invalidation.CurrentContextLength,
                OldSpanStart = invalidation.PreviousContextStart,
                OldSpanLength = invalidation.PreviousContextLength,
                NewSpanStart = invalidation.CurrentContextStart,
                NewSpanLength = invalidation.CurrentContextLength
            };
        }

        public LanguageServiceDocumentRequest BuildDocumentRequest(
            DocumentSession session,
            CortexSettings settings,
            CortexProjectDefinition project,
            string[] sourceRoots,
            bool includeDiagnostics,
            bool includeClassifications,
            IncrementalClassificationRange classificationRange)
        {
            return new LanguageServiceDocumentRequest
            {
                DocumentPath = session != null ? session.FilePath : string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath : string.Empty,
                SourceRoots = sourceRoots ?? new string[0],
                DocumentText = session != null ? session.Text : string.Empty,
                DocumentVersion = session != null ? session.TextVersion : 0,
                IncludeDiagnostics = includeDiagnostics,
                IncludeClassifications = includeClassifications,
                ClassificationRangeStart = classificationRange != null ? classificationRange.RequestStart : -1,
                ClassificationRangeLength = classificationRange != null ? classificationRange.RequestLength : 0
            };
        }

        public string BuildAnalysisWorkKey(string fingerprint, bool includeDiagnostics, bool includeClassifications)
        {
            return (fingerprint ?? string.Empty) +
                "|diag=" + includeDiagnostics +
                "|class=" + includeClassifications;
        }

        public string BuildAnalysisPhaseLabel(DocumentLanguageAnalysisRequestState pending)
        {
            if (pending == null)
            {
                return "unknown";
            }

            if (pending.IncludeClassifications && pending.IncludeDiagnostics)
            {
                return "full";
            }

            if (pending.IncludeClassifications)
            {
                return pending.IsPartialClassification ? "classifications(partial)" : "classifications";
            }

            if (pending.IncludeDiagnostics)
            {
                return "diagnostics";
            }

            return "empty";
        }

        public LanguageServiceAnalysisResponse MergeAnalysis(
            LanguageServiceAnalysisResponse existing,
            LanguageServiceAnalysisResponse incoming,
            DocumentLanguageAnalysisRequestState pending)
        {
            if (incoming == null)
            {
                return existing;
            }

            return new LanguageServiceAnalysisResponse
            {
                Success = incoming.Success,
                StatusMessage = incoming.StatusMessage ?? string.Empty,
                DocumentPath = !string.IsNullOrEmpty(incoming.DocumentPath)
                    ? incoming.DocumentPath
                    : (existing != null ? existing.DocumentPath ?? string.Empty : string.Empty),
                ProjectFilePath = !string.IsNullOrEmpty(incoming.ProjectFilePath)
                    ? incoming.ProjectFilePath
                    : (existing != null ? existing.ProjectFilePath ?? string.Empty : string.Empty),
                DocumentVersion = incoming.DocumentVersion,
                Diagnostics = pending != null && pending.IncludeDiagnostics && incoming.Success
                    ? CloneDiagnostics(incoming.Diagnostics)
                    : CloneDiagnostics(existing != null ? existing.Diagnostics : null),
                Classifications = pending != null && pending.IncludeClassifications && incoming.Success
                    ? MergeClassifications(existing, incoming, pending)
                    : CloneClassifications(existing != null ? existing.Classifications : null)
            };
        }

        private static LanguageServiceDiagnostic[] CloneDiagnostics(LanguageServiceDiagnostic[] diagnostics)
        {
            if (diagnostics == null || diagnostics.Length == 0)
            {
                return new LanguageServiceDiagnostic[0];
            }

            var clone = new LanguageServiceDiagnostic[diagnostics.Length];
            for (var i = 0; i < diagnostics.Length; i++)
            {
                clone[i] = diagnostics[i] == null
                    ? null
                    : new LanguageServiceDiagnostic
                    {
                        Id = diagnostics[i].Id,
                        Severity = diagnostics[i].Severity,
                        Message = diagnostics[i].Message,
                        Category = diagnostics[i].Category,
                        FilePath = diagnostics[i].FilePath,
                        Line = diagnostics[i].Line,
                        Column = diagnostics[i].Column,
                        EndLine = diagnostics[i].EndLine,
                        EndColumn = diagnostics[i].EndColumn
                    };
            }

            return clone;
        }

        private static LanguageServiceClassifiedSpan[] CloneClassifications(LanguageServiceClassifiedSpan[] classifications)
        {
            if (classifications == null || classifications.Length == 0)
            {
                return new LanguageServiceClassifiedSpan[0];
            }

            var clone = new LanguageServiceClassifiedSpan[classifications.Length];
            for (var i = 0; i < classifications.Length; i++)
            {
                clone[i] = CloneClassification(classifications[i]);
            }

            return clone;
        }

        private static LanguageServiceClassifiedSpan[] MergeClassifications(
            LanguageServiceAnalysisResponse existing,
            LanguageServiceAnalysisResponse incoming,
            DocumentLanguageAnalysisRequestState pending)
        {
            if (incoming == null)
            {
                return CloneClassifications(existing != null ? existing.Classifications : null);
            }

            if (pending == null || !pending.IsPartialClassification)
            {
                return CloneClassifications(incoming.Classifications);
            }

            var merged = new List<LanguageServiceClassifiedSpan>();
            var existingClassifications = existing != null ? existing.Classifications : null;
            var oldRangeStart = pending.OldClassificationStart;
            var oldRangeEnd = pending.OldClassificationStart + Math.Max(0, pending.OldClassificationLength);
            var shift = pending.NewClassificationLength - pending.OldClassificationLength;

            if (existingClassifications != null)
            {
                for (var i = 0; i < existingClassifications.Length; i++)
                {
                    var span = existingClassifications[i];
                    if (span == null || span.Length <= 0)
                    {
                        continue;
                    }

                    var spanStart = span.Start;
                    var spanEnd = span.Start + span.Length;
                    if (spanEnd <= oldRangeStart)
                    {
                        merged.Add(CloneClassification(span));
                        continue;
                    }

                    if (spanStart >= oldRangeEnd)
                    {
                        var shifted = CloneClassification(span);
                        shifted.Start += shift;
                        merged.Add(shifted);
                    }
                }
            }

            var incomingClassifications = incoming.Classifications;
            if (incomingClassifications != null)
            {
                for (var i = 0; i < incomingClassifications.Length; i++)
                {
                    if (incomingClassifications[i] != null)
                    {
                        merged.Add(CloneClassification(incomingClassifications[i]));
                    }
                }
            }

            merged.Sort(delegate(LanguageServiceClassifiedSpan left, LanguageServiceClassifiedSpan right)
            {
                if (left.Start != right.Start)
                {
                    return left.Start.CompareTo(right.Start);
                }

                return right.Length.CompareTo(left.Length);
            });
            return merged.ToArray();
        }

        private static LanguageServiceClassifiedSpan[] ProjectClassifications(
            LanguageServiceClassifiedSpan[] existingClassifications,
            EditorInvalidation invalidation)
        {
            if (existingClassifications == null || existingClassifications.Length == 0 || invalidation == null)
            {
                return new LanguageServiceClassifiedSpan[0];
            }

            var projected = new List<LanguageServiceClassifiedSpan>();
            var oldRangeStart = invalidation.PreviousContextStart;
            var oldRangeEnd = invalidation.PreviousContextStart + Math.Max(0, invalidation.PreviousContextLength);
            var shift = invalidation.CurrentContextLength - invalidation.PreviousContextLength;
            for (var i = 0; i < existingClassifications.Length; i++)
            {
                var span = existingClassifications[i];
                if (span == null || span.Length <= 0)
                {
                    continue;
                }

                var spanStart = span.Start;
                var spanEnd = span.Start + span.Length;
                if (spanEnd <= oldRangeStart)
                {
                    projected.Add(CloneClassification(span));
                    continue;
                }

                if (spanStart >= oldRangeEnd)
                {
                    var shifted = CloneClassification(span);
                    shifted.Start += shift;
                    projected.Add(shifted);
                }
            }

            return projected.ToArray();
        }

        private static LanguageServiceClassifiedSpan CloneClassification(LanguageServiceClassifiedSpan classification)
        {
            return classification == null
                ? null
                : new LanguageServiceClassifiedSpan
                {
                    Classification = classification.Classification,
                    Start = classification.Start,
                    Length = classification.Length,
                    Line = classification.Line,
                    Column = classification.Column
                };
        }

        private static LanguageServiceAnalysisResponse CloneLanguageAnalysis(LanguageServiceAnalysisResponse response)
        {
            if (response == null)
            {
                return new LanguageServiceAnalysisResponse();
            }

            return new LanguageServiceAnalysisResponse
            {
                Success = response.Success,
                StatusMessage = response.StatusMessage ?? string.Empty,
                DocumentPath = response.DocumentPath ?? string.Empty,
                ProjectFilePath = response.ProjectFilePath ?? string.Empty,
                DocumentVersion = response.DocumentVersion,
                Diagnostics = CloneDiagnostics(response.Diagnostics),
                Classifications = CloneClassifications(response.Classifications)
            };
        }
    }

    internal sealed class DocumentLanguageAnalysisRequestState
    {
        public string RequestId;
        public int Generation;
        public string Fingerprint;
        public string DocumentPath;
        public int DocumentVersion;
        public bool IncludeDiagnostics;
        public bool IncludeClassifications;
        public bool IsPartialClassification;
        public int OldClassificationStart;
        public int OldClassificationLength;
        public int NewClassificationStart;
        public int NewClassificationLength;
    }

    internal sealed class IncrementalClassificationRange
    {
        public int RequestStart;
        public int RequestLength;
        public int OldSpanStart;
        public int OldSpanLength;
        public int NewSpanStart;
        public int NewSpanLength;
    }
}
