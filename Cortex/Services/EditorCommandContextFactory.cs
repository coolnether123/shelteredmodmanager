using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class EditorCommandContextFactory
    {
        public CommandExecutionContext Build(CortexShellState state, EditorCommandTarget target)
        {
            return new CommandExecutionContext
            {
                ActiveContainerId = state != null ? state.Workbench.FocusedContainerId : string.Empty,
                ActiveDocumentId = state != null ? state.Documents.ActiveDocumentPath : string.Empty,
                FocusedRegionId = state != null ? state.Workbench.FocusedContainerId : string.Empty,
                Parameter = target
            };
        }
    }
}
