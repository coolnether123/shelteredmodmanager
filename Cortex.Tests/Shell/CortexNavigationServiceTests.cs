using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Modules.Shared;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class CortexNavigationServiceTests
    {
        [Fact]
        public void OpenDocument_ReusesExistingSession_AndMovesCaretToRequestedLine()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "CortexNavigationTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                var filePath = Path.Combine(tempRoot, "NavigationTarget.cs");
                File.WriteAllText(filePath, "line1\r\nline2\r\nline3\r\nline4");

                try
                {
                    var state = new CortexShellState();
                    var documentService = new FileDocumentService();
                    var editorService = new EditorService();

                    var opened = CortexModuleUtil.OpenDocument(documentService, state, filePath, 1, DocumentKind.SourceCode);
                    Assert.NotNull(opened);

                    editorService.SetCaret(opened, opened.Text.Length, false, false);
                    var moved = CortexModuleUtil.OpenDocument(documentService, state, filePath, 3, DocumentKind.SourceCode);

                    Assert.Same(opened, moved);
                    Assert.Equal(3, moved.HighlightedLine);
                    Assert.True(moved.EditorState.ScrollToCaretPending);

                    var caret = editorService.GetCaretPosition(moved, moved.EditorState.CaretIndex);
                    Assert.Equal(2, caret.Line);
                    Assert.Equal(0, caret.Column);
                }
                finally
                {
                    Directory.Delete(tempRoot, true);
                }
            });
        }
    }
}
