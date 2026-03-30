using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services.Navigation.Document;

namespace Cortex.Services.Navigation.Runtime
{
    internal interface IRuntimeNavigationTargetService
    {
        SourceNavigationTarget ResolveTarget(RuntimeLogEntry entry, int frameIndex, CortexShellState state);
        bool OpenTarget(CortexShellState state, SourceNavigationTarget target, string successStatusMessage, string failureStatusMessage);
    }

    internal sealed class RuntimeNavigationTargetService : IRuntimeNavigationTargetService
    {
        private readonly IRuntimeSourceNavigationService _runtimeSourceNavigationService;
        private readonly INavigationDocumentService _documentService;

        public RuntimeNavigationTargetService(IRuntimeSourceNavigationService runtimeSourceNavigationService, INavigationDocumentService documentService)
        {
            _runtimeSourceNavigationService = runtimeSourceNavigationService;
            _documentService = documentService;
        }

        public SourceNavigationTarget ResolveTarget(RuntimeLogEntry entry, int frameIndex, CortexShellState state)
        {
            return _runtimeSourceNavigationService != null
                ? _runtimeSourceNavigationService.Resolve(entry, frameIndex, state.SelectedProject, state.Settings)
                : null;
        }

        public bool OpenTarget(CortexShellState state, SourceNavigationTarget target, string successStatusMessage, string failureStatusMessage)
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

            return _documentService.OpenDocument(
                state,
                target.FilePath,
                target.LineNumber,
                !string.IsNullOrEmpty(successStatusMessage) ? successStatusMessage : target.StatusMessage,
                failureStatusMessage) != null;
        }
    }
}
