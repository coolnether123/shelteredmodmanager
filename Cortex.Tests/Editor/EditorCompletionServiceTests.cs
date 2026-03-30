using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Semantics.Completion;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class EditorCompletionServiceTests
    {
        [Fact]
        public void QueueRequest_CapturesCaretAndResetsVisibleCompletionState()
        {
            var service = new EditorCompletionService();
            var state = new CortexCompletionInteractionState
            {
                ActiveContextKey = "active",
                Response = new LanguageServiceCompletionResponse
                {
                    Success = true,
                    Items = new[] { new LanguageServiceCompletionItem { DisplayText = "Old" } }
                },
                PopupStateKey = "popup",
                SelectedIndex = 3
            };
            var session = CreateSession("D:\\Temp\\Sample.cs", "Console.Wri", 11);

            var queued = service.QueueRequest(session, state, new TestEditorService(), false, ".");

            Assert.True(queued);
            Assert.Equal("D:\\Temp\\Sample.cs", state.RequestedDocumentPath);
            Assert.Equal(1, state.RequestedLine);
            Assert.Equal(12, state.RequestedColumn);
            Assert.Equal(11, state.RequestedAbsolutePosition);
            Assert.Equal(".", state.RequestedTriggerCharacter);
            Assert.Equal(string.Empty, state.ActiveContextKey);
            Assert.Null(state.Response);
            Assert.Equal(string.Empty, state.PopupStateKey);
            Assert.Equal(-1, state.SelectedIndex);
        }

        [Fact]
        public void AcceptResponse_StoresActiveCompletionForMatchingRequest()
        {
            var service = new EditorCompletionService();
            var state = new CortexCompletionInteractionState
            {
                RequestedKey = "request-key"
            };
            var session = CreateSession("D:\\Temp\\Sample.cs", "Console.Wri", 11);
            var pending = new DocumentLanguageCompletionRequestState
            {
                RequestKey = "request-key",
                ContextKey = "context-key",
                DocumentPath = session.FilePath,
                DocumentVersion = session.TextVersion,
                AbsolutePosition = 11
            };
            var response = new LanguageServiceCompletionResponse
            {
                Success = true,
                DocumentPath = session.FilePath,
                DocumentVersion = session.TextVersion,
                ReplacementRange = new LanguageServiceRange
                {
                    Start = 8,
                    Length = 3
                },
                Items = new[]
                {
                    new LanguageServiceCompletionItem
                    {
                        DisplayText = "WriteLine",
                        InsertText = "WriteLine",
                        IsPreselected = true
                    }
                }
            };

            var accepted = service.AcceptResponse(state, session, pending, response);

            Assert.True(accepted);
            Assert.Equal(string.Empty, state.RequestedKey);
            Assert.Equal("context-key", state.ActiveContextKey);
            Assert.NotNull(state.Response);
            Assert.Equal(0, state.SelectedIndex);
            Assert.True(service.HasVisibleCompletion(state));
        }

        [Fact]
        public void ApplySelectedCompletion_ReplacesResponseRange_AndClearsPopupState()
        {
            var service = new EditorCompletionService();
            var session = CreateSession("D:\\Temp\\Sample.cs", "Console.Wri", 11);
            var state = new CortexCompletionInteractionState
            {
                ActiveContextKey = "context-key",
                Response = new LanguageServiceCompletionResponse
                {
                    Success = true,
                    DocumentPath = session.FilePath,
                    DocumentVersion = session.TextVersion,
                    ReplacementRange = new LanguageServiceRange
                    {
                        Start = 8,
                        Length = 3
                    },
                    Items = new[]
                    {
                        new LanguageServiceCompletionItem
                        {
                            DisplayText = "WriteLine",
                            InsertText = "WriteLine",
                            IsPreselected = true
                        }
                    }
                }
            };

            var applied = service.ApplySelectedCompletion(session, state, new TestEditorService());

            Assert.True(applied);
            Assert.Equal("Console.WriteLine", session.Text);
            Assert.Null(state.Response);
            Assert.Equal(string.Empty, state.ActiveContextKey);
            Assert.Equal(-1, state.SelectedIndex);
        }

        private static DocumentSession CreateSession(string filePath, string text, int caretIndex)
        {
            var session = new DocumentSession
            {
                FilePath = filePath,
                Kind = DocumentKind.SourceCode,
                Text = text,
                OriginalTextSnapshot = text,
                TextVersion = 5
            };
            session.EditorState.SelectionAnchorIndex = caretIndex;
            session.EditorState.CaretIndex = caretIndex;
            return session;
        }
    }
}
