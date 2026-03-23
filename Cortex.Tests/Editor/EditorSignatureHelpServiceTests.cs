using Cortex;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class EditorSignatureHelpServiceTests
    {
        [Fact]
        public void QueueRequest_CapturesCaretAndRequestMetadata()
        {
            var service = new EditorSignatureHelpService();
            var state = new CortexEditorInteractionState();
            var session = CreateSession("D:\\Temp\\Sample.cs", "Call(value);", 5);

            var queued = service.QueueRequest(session, state, new TestEditorService(), false, "(");

            Assert.True(queued);
            Assert.Equal("D:\\Temp\\Sample.cs", state.RequestedSignatureHelpDocumentPath);
            Assert.Equal(1, state.RequestedSignatureHelpLine);
            Assert.Equal(6, state.RequestedSignatureHelpColumn);
            Assert.Equal(5, state.RequestedSignatureHelpAbsolutePosition);
            Assert.Equal("(", state.RequestedSignatureHelpTriggerCharacter);
        }

        [Fact]
        public void AcceptResponse_StoresActiveSignatureHelpForMatchingRequest()
        {
            var service = new EditorSignatureHelpService();
            var state = new CortexEditorInteractionState
            {
                RequestedSignatureHelpKey = "request-key"
            };
            var session = CreateSession("D:\\Temp\\Sample.cs", "Call(value);", 5);
            var pending = new PendingLanguageSignatureHelpRequest
            {
                RequestKey = "request-key",
                DocumentPath = session.FilePath,
                DocumentVersion = session.TextVersion
            };
            var response = new LanguageServiceSignatureHelpResponse
            {
                Success = true,
                DocumentPath = session.FilePath,
                DocumentVersion = session.TextVersion,
                Items = new[]
                {
                    new LanguageServiceSignatureHelpItem
                    {
                        PrefixDisplay = "void Call(",
                        SuffixDisplay = ")",
                        Parameters = new[]
                        {
                            new LanguageServiceSignatureHelpParameter
                            {
                                Name = "value",
                                Display = "string value"
                            }
                        }
                    }
                }
            };

            var accepted = service.AcceptResponse(state, session, pending, response);

            Assert.True(accepted);
            Assert.Equal("request-key", state.ActiveSignatureHelpKey);
            Assert.NotNull(state.ActiveSignatureHelpResponse);
            Assert.True(service.HasVisibleSignatureHelp(state, session));
        }

        [Fact]
        public void AcceptResponse_RejectsMismatchedRequestKeyWithoutTouchingState()
        {
            var service = new EditorSignatureHelpService();
            var state = new CortexEditorInteractionState
            {
                RequestedSignatureHelpKey = "request-key",
                ActiveSignatureHelpKey = "active-key",
                ActiveSignatureHelpResponse = new LanguageServiceSignatureHelpResponse
                {
                    Success = true,
                    DocumentPath = "D:\\Temp\\Sample.cs",
                    Items = new[] { new LanguageServiceSignatureHelpItem() }
                }
            };
            var session = CreateSession("D:\\Temp\\Sample.cs", "Call(value);", 5);
            var pending = new PendingLanguageSignatureHelpRequest
            {
                RequestKey = "different-key",
                DocumentPath = session.FilePath,
                DocumentVersion = session.TextVersion
            };
            var response = new LanguageServiceSignatureHelpResponse
            {
                Success = true,
                DocumentPath = session.FilePath,
                DocumentVersion = session.TextVersion,
                Items = new[] { new LanguageServiceSignatureHelpItem() }
            };

            var accepted = service.AcceptResponse(state, session, pending, response);

            Assert.False(accepted);
            Assert.Equal("request-key", state.RequestedSignatureHelpKey);
            Assert.Equal("active-key", state.ActiveSignatureHelpKey);
            Assert.NotNull(state.ActiveSignatureHelpResponse);
        }

        [Fact]
        public void BuildWorkerRequest_CarriesQueuedSignatureHelpMetadata()
        {
            var service = new EditorSignatureHelpService();
            var session = CreateSession("D:\\Temp\\Sample.cs", "Call(value);", 5);
            var state = new CortexEditorInteractionState
            {
                RequestedSignatureHelpLine = 2,
                RequestedSignatureHelpColumn = 6,
                RequestedSignatureHelpAbsolutePosition = 5,
                RequestedSignatureHelpTriggerCharacter = "(",
                RequestedSignatureHelpExplicit = true
            };
            var project = new CortexProjectDefinition
            {
                ProjectFilePath = "D:\\Temp\\Sample.csproj"
            };
            var sourceRoots = new[] { "D:\\Temp" };
            var settings = new CortexSettings
            {
                WorkspaceRootPath = "D:\\Temp"
            };

            var request = service.BuildWorkerRequest(session, settings, project, sourceRoots, state);

            Assert.NotNull(request);
            Assert.Equal(session.FilePath, request.DocumentPath);
            Assert.Equal(project.ProjectFilePath, request.ProjectFilePath);
            Assert.Equal(settings.WorkspaceRootPath, request.WorkspaceRootPath);
            Assert.Equal(sourceRoots, request.SourceRoots);
            Assert.Equal(session.Text, request.DocumentText);
            Assert.Equal(session.TextVersion, request.DocumentVersion);
            Assert.Equal(2, request.Line);
            Assert.Equal(6, request.Column);
            Assert.Equal(5, request.AbsolutePosition);
            Assert.True(request.ExplicitInvocation);
            Assert.Equal("(", request.TriggerCharacter);
        }

        [Fact]
        public void Reset_ClearsRequestedAndActiveSignatureHelp()
        {
            var service = new EditorSignatureHelpService();
            var state = new CortexEditorInteractionState
            {
                RequestedSignatureHelpKey = "pending",
                RequestedSignatureHelpDocumentPath = "D:\\Temp\\Sample.cs",
                RequestedSignatureHelpAbsolutePosition = 4,
                ActiveSignatureHelpKey = "active",
                ActiveSignatureHelpResponse = new LanguageServiceSignatureHelpResponse
                {
                    Success = true,
                    Items = new[] { new LanguageServiceSignatureHelpItem() }
                }
            };

            service.Reset(state);

            Assert.Equal(string.Empty, state.RequestedSignatureHelpKey);
            Assert.Equal(string.Empty, state.ActiveSignatureHelpKey);
            Assert.Null(state.ActiveSignatureHelpResponse);
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
