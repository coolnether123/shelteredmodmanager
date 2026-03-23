using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Services;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class EditorMutationExecutionServiceTests
    {
        [Fact]
        public void TryExecuteClipboardCommand_Cut_EntersEditModeAndRemovesSelection()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var filePath = CreateTempFile("hello world");
                try
                {
                    var session = CreateSourceSession(filePath, "hello world");
                    var state = new CortexShellState
                    {
                        Settings = new CortexSettings
                        {
                            EnableFileEditing = true
                        }
                    };
                    state.Documents.OpenDocuments.Add(session);

                    var clipboard = new TestClipboardService();
                    var service = new EditorMutationExecutionService(
                        new TestEditorService(),
                        new EditorDocumentModeService(),
                        new EditorLogicalDocumentTargetResolutionService(),
                        clipboard);

                    string statusMessage;
                    var executed = service.TryExecuteClipboardCommand(
                        "cortex.editor.cut",
                        state,
                        new EditorCommandTarget
                        {
                            DocumentKind = DocumentKind.SourceCode,
                            DocumentPath = filePath,
                            SupportsEditing = true,
                            IsEditModeEnabled = false,
                            CanToggleEditMode = true,
                            HasSelection = true,
                            SelectionStart = 0,
                            SelectionEnd = 5
                        },
                        out statusMessage);

                    Assert.True(executed);
                    Assert.Equal(" world", session.Text);
                    Assert.Equal("hello", clipboard.GetText());
                    Assert.True(session.EditorState.EditModeEnabled);
                    Assert.Equal("Cut selection.", statusMessage);
                }
                finally
                {
                    File.Delete(filePath);
                }
            });
        }

        [Fact]
        public void TryExecuteClipboardCommand_Paste_EntersEditModeAndInsertsClipboardText()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var filePath = CreateTempFile("hello");
                try
                {
                    var session = CreateSourceSession(filePath, "hello");
                    var state = new CortexShellState
                    {
                        Settings = new CortexSettings
                        {
                            EnableFileEditing = true
                        }
                    };
                    state.Documents.OpenDocuments.Add(session);

                    var service = new EditorMutationExecutionService(
                        new TestEditorService(),
                        new EditorDocumentModeService(),
                        new EditorLogicalDocumentTargetResolutionService(),
                        new TestClipboardService(" world"));

                    string statusMessage;
                    var executed = service.TryExecuteClipboardCommand(
                        "cortex.editor.paste",
                        state,
                        new EditorCommandTarget
                        {
                            DocumentKind = DocumentKind.SourceCode,
                            DocumentPath = filePath,
                            SupportsEditing = true,
                            IsEditModeEnabled = false,
                            CanToggleEditMode = true,
                            CaretIndex = 5
                        },
                        out statusMessage);

                    Assert.True(executed);
                    Assert.Equal("hello world", session.Text);
                    Assert.True(session.EditorState.EditModeEnabled);
                    Assert.Equal("Pasted clipboard contents.", statusMessage);
                }
                finally
                {
                    File.Delete(filePath);
                }
            });
        }

        private static DocumentSession CreateSourceSession(string filePath, string text)
        {
            return new DocumentSession
            {
                FilePath = filePath,
                Kind = DocumentKind.SourceCode,
                IsReadOnly = false,
                Text = text,
                OriginalTextSnapshot = text
            };
        }

        private static string CreateTempFile(string text)
        {
            var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cs");
            File.WriteAllText(filePath, text);
            return filePath;
        }
    }
}
