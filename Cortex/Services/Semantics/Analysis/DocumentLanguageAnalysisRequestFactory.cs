using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Analysis
{
    internal sealed class DocumentLanguageAnalysisRequestFactory
    {
        public LanguageServiceDocumentRequest BuildDocumentRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, bool includeDiagnostics, bool includeClassifications, IncrementalClassificationRange classificationRange)
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
            return (fingerprint ?? string.Empty) + "|diag=" + includeDiagnostics + "|class=" + includeClassifications;
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
    }
}
