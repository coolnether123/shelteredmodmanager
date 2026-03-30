using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Analysis
{
    internal sealed class DocumentLanguageAnalysisMergeService
    {
        public LanguageServiceAnalysisResponse MergeAnalysis(LanguageServiceAnalysisResponse existing, LanguageServiceAnalysisResponse incoming, DocumentLanguageAnalysisRequestState pending)
        {
            if (incoming == null)
            {
                return existing;
            }

            return new LanguageServiceAnalysisResponse
            {
                Success = incoming.Success,
                StatusMessage = incoming.StatusMessage ?? string.Empty,
                DocumentPath = !string.IsNullOrEmpty(incoming.DocumentPath) ? incoming.DocumentPath : (existing != null ? existing.DocumentPath ?? string.Empty : string.Empty),
                ProjectFilePath = !string.IsNullOrEmpty(incoming.ProjectFilePath) ? incoming.ProjectFilePath : (existing != null ? existing.ProjectFilePath ?? string.Empty : string.Empty),
                DocumentVersion = incoming.DocumentVersion,
                Diagnostics = pending != null && pending.IncludeDiagnostics && incoming.Success
                    ? DocumentLanguageAnalysisCloneUtility.CloneDiagnostics(incoming.Diagnostics)
                    : DocumentLanguageAnalysisCloneUtility.CloneDiagnostics(existing != null ? existing.Diagnostics : null),
                Classifications = pending != null && pending.IncludeClassifications && incoming.Success
                    ? MergeClassifications(existing, incoming, pending)
                    : DocumentLanguageAnalysisCloneUtility.CloneClassifications(existing != null ? existing.Classifications : null)
            };
        }

        private static LanguageServiceClassifiedSpan[] MergeClassifications(LanguageServiceAnalysisResponse existing, LanguageServiceAnalysisResponse incoming, DocumentLanguageAnalysisRequestState pending)
        {
            if (incoming == null)
            {
                return DocumentLanguageAnalysisCloneUtility.CloneClassifications(existing != null ? existing.Classifications : null);
            }

            if (pending == null || !pending.IsPartialClassification)
            {
                return DocumentLanguageAnalysisCloneUtility.CloneClassifications(incoming.Classifications);
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
                        merged.Add(DocumentLanguageAnalysisCloneUtility.CloneClassification(span));
                        continue;
                    }

                    if (spanStart >= oldRangeEnd)
                    {
                        var shifted = DocumentLanguageAnalysisCloneUtility.CloneClassification(span);
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
                        merged.Add(DocumentLanguageAnalysisCloneUtility.CloneClassification(incomingClassifications[i]));
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

        internal static LanguageServiceClassifiedSpan[] ProjectClassifications(LanguageServiceClassifiedSpan[] existingClassifications, EditorInvalidation invalidation)
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
                    projected.Add(DocumentLanguageAnalysisCloneUtility.CloneClassification(span));
                    continue;
                }

                if (spanStart >= oldRangeEnd)
                {
                    var shifted = DocumentLanguageAnalysisCloneUtility.CloneClassification(span);
                    shifted.Start += shift;
                    projected.Add(shifted);
                }
            }

            return projected.ToArray();
        }
    }
}
