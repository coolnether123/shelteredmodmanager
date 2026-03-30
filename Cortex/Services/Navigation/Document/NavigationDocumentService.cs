using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;

namespace Cortex.Services.Navigation.Document
{
    internal interface INavigationDocumentService
    {
        DocumentSession OpenDocument(CortexShellState state, string filePath, int highlightedLine, string successStatusMessage, string failureStatusMessage);
        void PreloadDocument(CortexShellState state, string filePath);
        bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, int highlightedLine, string successStatusMessage, string failureStatusMessage);
    }

    internal sealed class NavigationDocumentService : INavigationDocumentService
    {
        private readonly IDocumentService _documentService;

        public NavigationDocumentService(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        public DocumentSession OpenDocument(CortexShellState state, string filePath, int highlightedLine, string successStatusMessage, string failureStatusMessage)
        {
            var kind = CortexModuleUtil.IsDecompilerDocumentPath(state, filePath)
                ? DocumentKind.DecompiledCode
                : DocumentKind.Unknown;
            var opened = CortexModuleUtil.OpenDocument(_documentService, state, filePath, highlightedLine, kind);
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

        public void PreloadDocument(CortexShellState state, string filePath)
        {
            if (_documentService == null || state == null || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            if (CortexModuleUtil.FindOpenDocument(state, filePath) != null)
            {
                return;
            }

            _documentService.Preload(filePath);
        }

        public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, int highlightedLine, string successStatusMessage, string failureStatusMessage)
        {
            var lineNumber = highlightedLine > 0 ? highlightedLine : 1;
            var opened = response != null && _documentService != null && state != null
                ? CortexModuleUtil.OpenDocument(_documentService, state, response.CachePath, lineNumber, DocumentKind.DecompiledCode)
                : null;
            if (opened != null)
            {
                state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
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
    }
}
