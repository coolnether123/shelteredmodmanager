using Cortex.Core.Models;
using Cortex.Services.Editor.Presentation;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class EditorPresentationServiceTests
    {
        [Fact]
        public void ResolveSearchShortcutCommand_MapsFindAndSearchNavigation()
        {
            var service = new EditorPresentationService();
            var state = CreateActiveSourceState();
            state.Search.IsVisible = true;

            var findCommand = service.ResolveSearchShortcutCommand(
                new EditorSearchShortcutInput { Control = true, KeyCode = "F" },
                state);
            var nextCommand = service.ResolveSearchShortcutCommand(
                new EditorSearchShortcutInput { KeyCode = "F3" },
                state);
            var closeCommand = service.ResolveSearchShortcutCommand(
                new EditorSearchShortcutInput { KeyCode = "Escape" },
                state);

            Assert.Equal("cortex.editor.find", findCommand);
            Assert.Equal("cortex.search.next", nextCommand);
            Assert.Equal("cortex.search.close", closeCommand);
        }

        [Fact]
        public void BuildStatusBarPresentation_ReportsDocumentStateAndAllowsToggle()
        {
            var service = new EditorPresentationService();
            var state = CreateActiveSourceState();

            var presentation = service.BuildStatusBarPresentation(state);

            Assert.True(presentation.CanToggleEditMode);
            Assert.False(presentation.IsEditing);
            Assert.Equal(2, presentation.LineCount);
            Assert.Equal("Language: offline", presentation.LanguageStatusLabel);

            string statusMessage;
            var toggled = service.TryToggleEditMode(state, out statusMessage);

            Assert.True(toggled);
            Assert.True(state.Documents.ActiveDocument.EditorState.EditModeEnabled);
            Assert.Equal("Edit mode enabled for Example.cs.", statusMessage);
        }

        private static CortexShellState CreateActiveSourceState()
        {
            var session = new DocumentSession
            {
                FilePath = @"C:\temp\Example.cs",
                Kind = DocumentKind.SourceCode,
                IsReadOnly = false,
                Text = "line1\nline2",
                OriginalTextSnapshot = "line1\nline2"
            };

            var state = new CortexShellState
            {
                Settings = new CortexSettings
                {
                    EnableFileEditing = true
                }
            };
            state.Documents.OpenDocuments.Add(session);
            state.Documents.ActiveDocument = session;
            state.Documents.ActiveDocumentPath = session.FilePath;
            return state;
        }
    }
}
