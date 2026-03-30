using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Semantics.Context;
using Xunit;
using Cortex.Services.Editor.Context;

namespace Cortex.Tests.Editor
{
    public sealed class EditorContextServiceTests
    {
        [Fact]
        public void PublishTargetContext_StoresActiveContextAndSurfaceMapping()
        {
            var service = CreateService();
            var state = new CortexShellState();
            var session = CreateSession("D:\\Temp\\Sample.cs", "Sample();", 0);
            var target = new EditorCommandTarget
            {
                DocumentPath = session.FilePath,
                SymbolText = "Sample",
                Line = 1,
                Column = 1,
                AbsolutePosition = 0
            };

            var snapshot = service.PublishTargetContext(state, session, "surface-1", "pane-1", EditorSurfaceKind.Source, target, true);
            var resolved = service.ResolveTarget(state, snapshot.ContextKey);

            Assert.NotNull(snapshot);
            Assert.Equal(snapshot.ContextKey, state.EditorContext.ActiveContextKey);
            Assert.Equal("surface-1", state.EditorContext.ActiveSurfaceId);
            Assert.Equal(snapshot.ContextKey, state.EditorContext.SurfaceContextKeys["surface-1"]);
            Assert.Equal(snapshot.ContextKey, target.ContextKey);
            Assert.NotNull(resolved);
            Assert.Equal("Sample", resolved.SymbolText);
            Assert.Equal(session.FilePath, resolved.DocumentPath);
        }

        [Fact]
        public void ApplySymbolContext_ProjectsSemanticMetadataIntoResolvedTarget()
        {
            var service = CreateService();
            var state = new CortexShellState();
            var session = CreateSession("D:\\Temp\\Sample.cs", "Sample();", 0);
            var target = new EditorCommandTarget
            {
                DocumentPath = session.FilePath,
                SymbolText = "Sample",
                Line = 1,
                Column = 1,
                AbsolutePosition = 0
            };
            var snapshot = service.PublishTargetContext(state, session, "surface-1", "pane-1", EditorSurfaceKind.Source, target, true);

            service.ApplySymbolContext(state, snapshot.ContextKey, new LanguageServiceSymbolContextResponse
            {
                Success = true,
                QualifiedSymbolDisplay = "Demo.Sample",
                SymbolKind = "Method",
                MetadataName = "Sample",
                ContainingTypeName = "Demo.Program",
                ContainingAssemblyName = "DemoAssembly",
                DefinitionDocumentPath = session.FilePath,
                DefinitionRange = new LanguageServiceRange
                {
                    Start = 0,
                    Length = 6,
                    StartLine = 1,
                    StartColumn = 1
                }
            });

            var resolved = service.ResolveTarget(state, snapshot.ContextKey);

            Assert.NotNull(resolved);
            Assert.Equal("Demo.Sample", resolved.QualifiedSymbolDisplay);
            Assert.Equal("Method", resolved.SymbolKind);
            Assert.Equal("Sample", resolved.MetadataName);
            Assert.Equal("Demo.Program", resolved.ContainingTypeName);
            Assert.Equal("DemoAssembly", resolved.ContainingAssemblyName);
            Assert.Equal(session.FilePath, resolved.DefinitionDocumentPath);
            Assert.Equal(0, resolved.DefinitionStart);
            Assert.Equal(6, resolved.DefinitionLength);
        }

        private static EditorContextService CreateService()
        {
            return new EditorContextService(
                new EditorService(),
                new EditorCommandContextFactory(),
                new EditorSymbolInteractionService());
        }

        private static DocumentSession CreateSession(string filePath, string text, int caretIndex)
        {
            var session = new DocumentSession
            {
                FilePath = filePath,
                Kind = DocumentKind.SourceCode,
                Text = text,
                OriginalTextSnapshot = text,
                TextVersion = 2
            };
            session.EditorState.SelectionAnchorIndex = caretIndex;
            session.EditorState.CaretIndex = caretIndex;
            return session;
        }
    }
}
