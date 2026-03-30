using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Inspector.Actions;
using Cortex.Services.Inspector.Relationships;
using Cortex.Services.Navigation;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class EditorMethodInspectorNavigationActionHandlerTests
    {
        [Fact]
        public void Handle_OpensRelationshipTarget_WithEncodedNavigationPayload()
        {
            var factory = new EditorMethodInspectorNavigationActionFactory();
            var handler = new EditorMethodInspectorNavigationActionHandler();
            var navigationService = new RecordingNavigationService();
            var action = factory.CreateRelationshipActions(new EditorMethodRelationshipItem
            {
                SymbolKind = "Method",
                MetadataName = "Execute",
                ContainingTypeName = "Sample.TargetType",
                ContainingAssemblyName = "Sample.Assembly",
                DocumentationCommentId = "M:Sample.TargetType.Execute"
            })[0];

            var handled = handler.TryHandle(new CortexShellState(), navigationService, action.Id);

            Assert.True(handled);
            Assert.Equal("Method", navigationService.SymbolKind);
            Assert.Equal("Execute", navigationService.MetadataName);
            Assert.Equal("Sample.TargetType", navigationService.ContainingTypeName);
            Assert.Equal("Sample.Assembly", navigationService.ContainingAssemblyName);
            Assert.Equal("M:Sample.TargetType.Execute", navigationService.DocumentationCommentId);
        }

        private sealed class RecordingNavigationService : ICortexNavigationService
        {
            public string SymbolKind = string.Empty;
            public string MetadataName = string.Empty;
            public string ContainingTypeName = string.Empty;
            public string ContainingAssemblyName = string.Empty;
            public string DocumentationCommentId = string.Empty;

            public DocumentSession OpenDocument(CortexShellState state, string filePath, int highlightedLine, string successStatusMessage, string failureStatusMessage) { return null; }
            public void PreloadDocument(CortexShellState state, string filePath) { }
            public void PreloadHoverResponseTarget(CortexShellState state, LanguageServiceHoverResponse response) { }
            public void PreloadHoverDisplayPartTarget(CortexShellState state, LanguageServiceHoverDisplayPart part) { }
            public void PreloadHoverNavigationTarget(CortexShellState state, EditorHoverNavigationTarget target) { }
            public DecompilerResponse RequestDecompilerSource(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache) { return null; }
            public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, string successStatusMessage, string failureStatusMessage) { return false; }
            public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, int highlightedLine, string successStatusMessage, string failureStatusMessage) { return false; }
            public bool DecompileAndOpen(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage) { return false; }
            public bool OpenDecompilerMethodTarget(CortexShellState state, string assemblyPath, int methodMetadataToken, string metadataName, string containingTypeName, string symbolKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage) { return false; }
            public DecompilerResponse RequestDecompilerMethodView(CortexShellState state, string assemblyPath, int methodMetadataToken, string metadataName, string containingTypeName, string symbolKind, bool ignoreCache, out int highlightedLine) { highlightedLine = 0; return null; }
            public SourceNavigationTarget ResolveRuntimeTarget(RuntimeLogEntry entry, int frameIndex, CortexShellState state) { return null; }
            public bool OpenRuntimeTarget(CortexShellState state, SourceNavigationTarget target, string successStatusMessage, string failureStatusMessage) { return false; }
            public bool OpenHoverDisplayPart(CortexShellState state, LanguageServiceHoverDisplayPart part, string successStatusMessage, string failureStatusMessage) { return false; }
            public bool OpenHoverNavigationTarget(CortexShellState state, EditorHoverNavigationTarget target, string successStatusMessage, string failureStatusMessage) { return false; }

            public bool OpenLanguageSymbolTarget(
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
                string failureStatusMessage)
            {
                SymbolKind = symbolKind ?? string.Empty;
                MetadataName = metadataName ?? string.Empty;
                ContainingTypeName = containingTypeName ?? string.Empty;
                ContainingAssemblyName = containingAssemblyName ?? string.Empty;
                DocumentationCommentId = documentationCommentId ?? string.Empty;
                return true;
            }
        }
    }
}
