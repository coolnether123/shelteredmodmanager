using System;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Analysis
{
    internal interface IDocumentLanguageAnalysisService
    {
        void TryRestoreFromRecentCache(DocumentSession session, Action<string> logInfo);
        void RememberSnapshot(DocumentSession session);
        void ApplyProvisionalClassificationProjection(DocumentSession session);
        IncrementalClassificationRange BuildIncrementalClassificationRange(DocumentSession session);
        LanguageServiceDocumentRequest BuildDocumentRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, bool includeDiagnostics, bool includeClassifications, IncrementalClassificationRange classificationRange);
        string BuildAnalysisWorkKey(string fingerprint, bool includeDiagnostics, bool includeClassifications);
        string BuildAnalysisPhaseLabel(DocumentLanguageAnalysisRequestState pending);
        LanguageServiceAnalysisResponse MergeAnalysis(LanguageServiceAnalysisResponse existing, LanguageServiceAnalysisResponse incoming, DocumentLanguageAnalysisRequestState pending);
    }
}
