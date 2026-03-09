using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;

namespace Cortex.Services
{
    internal sealed class CortexNavigationService
    {
        private readonly IDocumentService _documentService;
        private readonly ISourceReferenceService _sourceReferenceService;
        private readonly IRuntimeSourceNavigationService _runtimeSourceNavigationService;

        public CortexNavigationService(
            IDocumentService documentService,
            ISourceReferenceService sourceReferenceService,
            IRuntimeSourceNavigationService runtimeSourceNavigationService)
        {
            _documentService = documentService;
            _sourceReferenceService = sourceReferenceService;
            _runtimeSourceNavigationService = runtimeSourceNavigationService;
        }

        public DocumentSession OpenDocument(CortexShellState state, string filePath, int highlightedLine, string successStatusMessage, string failureStatusMessage)
        {
            var opened = CortexModuleUtil.OpenDocument(_documentService, state, filePath, highlightedLine);
            if (opened != null)
            {
                state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
                if (!string.IsNullOrEmpty(successStatusMessage))
                {
                    state.StatusMessage = successStatusMessage;
                }

                return opened;
            }

            if (!string.IsNullOrEmpty(failureStatusMessage))
            {
                state.StatusMessage = failureStatusMessage;
            }

            return null;
        }

        public DecompilerResponse RequestDecompilerSource(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache)
        {
            return CortexModuleUtil.RequestDecompilerSource(
                _sourceReferenceService,
                state,
                assemblyPath,
                metadataToken,
                entityKind,
                ignoreCache);
        }

        public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, string successStatusMessage, string failureStatusMessage)
        {
            var opened = CortexModuleUtil.OpenDecompilerResult(_documentService, state, response);
            if (opened)
            {
                if (!string.IsNullOrEmpty(successStatusMessage))
                {
                    state.StatusMessage = successStatusMessage;
                }

                return true;
            }

            if (!string.IsNullOrEmpty(failureStatusMessage))
            {
                state.StatusMessage = failureStatusMessage;
            }

            return false;
        }

        public bool DecompileAndOpen(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage)
        {
            var response = RequestDecompilerSource(state, assemblyPath, metadataToken, entityKind, ignoreCache);
            if (response == null)
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            return OpenDecompilerResult(state, response, successStatusMessage, failureStatusMessage);
        }

        public SourceNavigationTarget ResolveRuntimeTarget(RuntimeLogEntry entry, int frameIndex, CortexShellState state)
        {
            return _runtimeSourceNavigationService != null
                ? _runtimeSourceNavigationService.Resolve(entry, frameIndex, state.SelectedProject, state.Settings)
                : null;
        }

        public bool OpenRuntimeTarget(CortexShellState state, SourceNavigationTarget target, string successStatusMessage, string failureStatusMessage)
        {
            if (target == null || !target.Success || string.IsNullOrEmpty(target.FilePath))
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }
                else if (target != null && !string.IsNullOrEmpty(target.StatusMessage))
                {
                    state.StatusMessage = target.StatusMessage;
                }

                return false;
            }

            return OpenDocument(
                state,
                target.FilePath,
                target.LineNumber,
                !string.IsNullOrEmpty(successStatusMessage) ? successStatusMessage : target.StatusMessage,
                failureStatusMessage);
        }
    }
}
