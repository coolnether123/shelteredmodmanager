using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Rendering.Models;
using Cortex.Services.Semantics.Context;
using Cortex.Services.Semantics.Hover;
using Xunit;
using Cortex.Services.Editor.Context;

namespace Cortex.Tests.Editor
{
    public sealed class EditorHoverServiceTests
    {
        [Fact]
        public void RequestHoverNow_QueuesHoverRequestForPublishedSourceTarget()
        {
            var state = new CortexShellState();
            var session = CreateSession("D:\\Temp\\Hover.cs", "Sample();", 0);
            state.Documents.ActiveDocument = session;
            state.Documents.ActiveDocumentPath = session.FilePath;
            var contextService = new EditorContextService(
                new EditorService(),
                new EditorCommandContextFactory(),
                new EditorSymbolInteractionService());
            var hoverService = new EditorHoverService(contextService);

            EditorHoverTarget hoverTarget;
            var created = hoverService.TryCreateSourceHoverTarget(
                session,
                state,
                true,
                "surface-1",
                "pane-1",
                EditorSurfaceKind.Source,
                0,
                new RenderRect(2f, 4f, 20f, 18f),
                "method",
                out hoverTarget);

            Assert.True(created);
            Assert.NotNull(hoverTarget);

            hoverService.RequestHoverNow(state, "surface-1", hoverTarget);

            Assert.Equal(hoverTarget.HoverKey, state.Editor.Hover.RequestedKey);
            Assert.Equal(hoverTarget.Target.ContextKey, state.Editor.Hover.RequestedContextKey);
            Assert.Equal(session.FilePath, state.Editor.Hover.RequestedDocumentPath);
            Assert.Equal(hoverTarget.Target.AbsolutePosition, state.Editor.Hover.RequestedAbsolutePosition);
            Assert.Equal(hoverTarget.Target.SymbolText, state.Editor.Hover.RequestedTokenText);
        }

        [Fact]
        public void TryCreateSourceHoverTarget_PublishesSharedContextForSurface()
        {
            var state = new CortexShellState();
            var session = CreateSession("D:\\Temp\\Hover.cs", "Sample();", 0);
            var contextService = new EditorContextService(
                new EditorService(),
                new EditorCommandContextFactory(),
                new EditorSymbolInteractionService());
            var hoverService = new EditorHoverService(contextService);

            EditorHoverTarget hoverTarget;
            var created = hoverService.TryCreateSourceHoverTarget(
                session,
                state,
                true,
                "surface-1",
                "pane-1",
                EditorSurfaceKind.Source,
                0,
                new RenderRect(0f, 0f, 12f, 12f),
                "method",
                out hoverTarget);

            Assert.True(created);
            Assert.NotNull(hoverTarget);
            Assert.Equal(hoverTarget.Target.ContextKey, state.EditorContext.SurfaceContextKeys["surface-1"]);
            Assert.Equal("Sample", hoverTarget.Target.SymbolText);
        }

        private static DocumentSession CreateSession(string filePath, string text, int caretIndex)
        {
            var session = new DocumentSession
            {
                FilePath = filePath,
                Kind = DocumentKind.SourceCode,
                Text = text,
                OriginalTextSnapshot = text,
                TextVersion = 3
            };
            session.EditorState.SelectionAnchorIndex = caretIndex;
            session.EditorState.CaretIndex = caretIndex;
            return session;
        }
    }
}
