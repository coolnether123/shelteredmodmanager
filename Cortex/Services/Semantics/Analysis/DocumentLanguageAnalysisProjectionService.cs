using Cortex.Core.Models;

namespace Cortex.Services.Semantics.Analysis
{
    internal sealed class DocumentLanguageAnalysisProjectionService
    {
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

            var projected = DocumentLanguageAnalysisCloneUtility.CloneLanguageAnalysis(session.LanguageAnalysis);
            projected.DocumentPath = session.FilePath ?? projected.DocumentPath;
            projected.DocumentVersion = session.TextVersion;
            projected.Classifications = DocumentLanguageAnalysisMergeService.ProjectClassifications(projected.Classifications, invalidation);
            session.LanguageAnalysis = projected;
            session.LastLanguageAnalysisUtc = System.DateTime.UtcNow;
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
    }
}
