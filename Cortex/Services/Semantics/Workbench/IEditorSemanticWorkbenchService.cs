using Cortex.Core.Models;

namespace Cortex.Services.Semantics.Workbench
{
    internal interface IEditorSemanticWorkbenchService
    {
        UnitTestGenerationPlan BuildUnitTestPlan(CortexShellState state, EditorCommandTarget target);
        void OpenDocumentEditPreview(CortexShellState state, DocumentEditPreviewPlan previewPlan);
    }
}
