using System;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Analysis
{
    internal sealed class DocumentLanguageAnalysisService : IDocumentLanguageAnalysisService
    {
        private readonly DocumentLanguageAnalysisCacheService _cacheService;
        private readonly DocumentLanguageAnalysisProjectionService _projectionService;
        private readonly DocumentLanguageAnalysisRequestFactory _requestFactory;
        private readonly DocumentLanguageAnalysisMergeService _mergeService;

        public DocumentLanguageAnalysisService()
            : this(
                new DocumentLanguageAnalysisCacheService(),
                new DocumentLanguageAnalysisProjectionService(),
                new DocumentLanguageAnalysisRequestFactory(),
                new DocumentLanguageAnalysisMergeService())
        {
        }

        internal DocumentLanguageAnalysisService(DocumentLanguageAnalysisCacheService cacheService, DocumentLanguageAnalysisProjectionService projectionService, DocumentLanguageAnalysisRequestFactory requestFactory, DocumentLanguageAnalysisMergeService mergeService)
        {
            _cacheService = cacheService;
            _projectionService = projectionService;
            _requestFactory = requestFactory;
            _mergeService = mergeService;
        }

        public void TryRestoreFromRecentCache(DocumentSession session, Action<string> logInfo) { _cacheService.TryRestoreFromRecentCache(session, logInfo); }
        public void RememberSnapshot(DocumentSession session) { _cacheService.RememberSnapshot(session); }
        public void ApplyProvisionalClassificationProjection(DocumentSession session) { _projectionService.ApplyProvisionalClassificationProjection(session); }
        public IncrementalClassificationRange BuildIncrementalClassificationRange(DocumentSession session) { return _projectionService.BuildIncrementalClassificationRange(session); }
        public LanguageServiceDocumentRequest BuildDocumentRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, bool includeDiagnostics, bool includeClassifications, IncrementalClassificationRange classificationRange) { return _requestFactory.BuildDocumentRequest(session, settings, project, sourceRoots, includeDiagnostics, includeClassifications, classificationRange); }
        public string BuildAnalysisWorkKey(string fingerprint, bool includeDiagnostics, bool includeClassifications) { return _requestFactory.BuildAnalysisWorkKey(fingerprint, includeDiagnostics, includeClassifications); }
        public string BuildAnalysisPhaseLabel(DocumentLanguageAnalysisRequestState pending) { return _requestFactory.BuildAnalysisPhaseLabel(pending); }
        public LanguageServiceAnalysisResponse MergeAnalysis(LanguageServiceAnalysisResponse existing, LanguageServiceAnalysisResponse incoming, DocumentLanguageAnalysisRequestState pending) { return _mergeService.MergeAnalysis(existing, incoming, pending); }
    }
}
