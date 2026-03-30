using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Navigation
{
    public interface ICortexNavigationService
    {
        DocumentSession OpenDocument(CortexShellState state, string filePath, int highlightedLine, string successStatusMessage, string failureStatusMessage);
        void PreloadDocument(CortexShellState state, string filePath);
        void PreloadHoverResponseTarget(CortexShellState state, LanguageServiceHoverResponse response);
        void PreloadHoverDisplayPartTarget(CortexShellState state, LanguageServiceHoverDisplayPart part);
        void PreloadHoverNavigationTarget(CortexShellState state, EditorHoverNavigationTarget target);
        DecompilerResponse RequestDecompilerSource(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache);
        bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, string successStatusMessage, string failureStatusMessage);
        bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, int highlightedLine, string successStatusMessage, string failureStatusMessage);
        bool DecompileAndOpen(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage);
        bool OpenDecompilerMethodTarget(
            CortexShellState state,
            string assemblyPath,
            int methodMetadataToken,
            string metadataName,
            string containingTypeName,
            string symbolKind,
            bool ignoreCache,
            string successStatusMessage,
            string failureStatusMessage);
        DecompilerResponse RequestDecompilerMethodView(
            CortexShellState state,
            string assemblyPath,
            int methodMetadataToken,
            string metadataName,
            string containingTypeName,
            string symbolKind,
            bool ignoreCache,
            out int highlightedLine);
        SourceNavigationTarget ResolveRuntimeTarget(RuntimeLogEntry entry, int frameIndex, CortexShellState state);
        bool OpenRuntimeTarget(CortexShellState state, SourceNavigationTarget target, string successStatusMessage, string failureStatusMessage);
        bool OpenHoverDisplayPart(CortexShellState state, LanguageServiceHoverDisplayPart part, string successStatusMessage, string failureStatusMessage);
        bool OpenHoverNavigationTarget(CortexShellState state, EditorHoverNavigationTarget target, string successStatusMessage, string failureStatusMessage);
        bool OpenLanguageSymbolTarget(
            CortexShellState state,
            string symbolDisplay,
            string symbolKind,
            string metadataName,
            string containingTypeName,
            string containingAssemblyName,
            string documentationCommentId,
            string definitionDocumentPath,
            LanguageServiceRange definitionRange,
            string successStatusMessage,
            string failureStatusMessage);
    }
}
