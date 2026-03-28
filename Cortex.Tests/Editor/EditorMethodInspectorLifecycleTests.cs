using Cortex;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class EditorMethodInspectorLifecycleTests
    {
        [Fact]
        public void ToggleRelationships_ClearsCachedState_AndReopenRequeuesWithoutDuplicates()
        {
            var target = new EditorCommandTarget
            {
                ContextKey = "ctx",
                DocumentPath = @"D:\workspace\Sample.cs",
                SymbolText = "Sample",
                AbsolutePosition = 12
            };
            var state = new CortexShellState();
            state.Editor.MethodInspector.IsVisible = true;
            state.Editor.MethodInspector.ContextKey = "ctx";
            state.Editor.MethodInspector.RelationshipsExpanded = true;
            state.Editor.MethodInspector.RelationshipsRequested = true;
            state.Editor.MethodInspector.RelationshipsTargetKey = EditorMethodInspectorService.BuildTargetKey(target);
            state.Editor.MethodInspector.RelationshipsRequestKey = "pending";
            state.Editor.MethodInspector.RelationshipsStatusMessage = "Loading";
            state.Editor.MethodInspector.RelationshipsCallHierarchy = new LanguageServiceCallHierarchyResponse
            {
                Success = true
            };

            var service = new EditorMethodInspectorService(new TestEditorContextService(target));

            service.ToggleSection(state, "relationships");

            Assert.False(state.Editor.MethodInspector.RelationshipsExpanded);
            Assert.False(state.Editor.MethodInspector.RelationshipsRequested);
            Assert.Equal(string.Empty, state.Editor.MethodInspector.RelationshipsRequestKey);
            Assert.Null(state.Editor.MethodInspector.RelationshipsCallHierarchy);

            service.ToggleSection(state, "relationships");

            Assert.True(state.Editor.MethodInspector.RelationshipsExpanded);
            Assert.True(service.EnsureRelationshipsRequest(state));
            Assert.False(service.EnsureRelationshipsRequest(state));
        }

        [Fact]
        public void ProcessLanguageResponses_IgnoresRelationshipResponse_WhenSectionWasCollapsed()
        {
            var target = new EditorCommandTarget
            {
                ContextKey = "ctx",
                DocumentPath = @"D:\workspace\Sample.cs",
                SymbolText = "Sample",
                AbsolutePosition = 12
            };
            var state = new CortexShellState();
            state.Editor.MethodInspector.IsVisible = true;
            state.Editor.MethodInspector.ContextKey = "ctx";
            state.Editor.MethodInspector.RelationshipsExpanded = false;
            state.Editor.MethodInspector.RelationshipsTargetKey = EditorMethodInspectorService.BuildTargetKey(target);

            var runtimeState = new CortexShellLanguageRuntimeState
            {
                ServiceGeneration = 3,
                PendingMethodInspectorRelationships = new PendingMethodInspectorRelationshipsRequest
                {
                    RequestId = "req-1",
                    Generation = 3,
                    TargetKey = state.Editor.MethodInspector.RelationshipsTargetKey,
                    DocumentPath = target.DocumentPath,
                    DocumentVersion = 1
                },
                MethodInspectorRelationshipsInFlight = true
            };

            var session = new TestLanguageProviderSession();
            session.Enqueue(new LanguageRuntimeMessage
            {
                Kind = LanguageRuntimeMessageKind.RequestResult,
                Generation = 3,
                Envelope = new LanguageServiceEnvelope
                {
                    RequestId = "req-1",
                    Success = true,
                    Command = LanguageServiceCommands.CallHierarchy
                }
            });

            var context = new CortexShellLanguageRuntimeContext(
                state,
                runtimeState,
                new DocumentLanguageAnalysisService(),
                new DocumentLanguageInteractionService(),
                new EditorCompletionService(),
                new EditorSignatureHelpService(),
                new TestEditorContextService(target),
                delegate { return session; },
                delegate { return null; },
                delegate { return false; },
                0d,
                delegate { },
                delegate { },
                delegate { },
                delegate { },
                delegate(string path) { return new DocumentSession { FilePath = path, TextVersion = 1 }; },
                delegate(string path) { return null; },
                delegate(CortexSettings settings, CortexProjectDefinition project) { return new string[0]; },
                delegate(DocumentSession document) { return string.Empty; },
                delegate(DocumentSession document, DocumentLanguageCompletionRequestState pending) { return null; },
                delegate(DocumentSession document, DocumentLanguageCompletionRequestState pending, CompletionAugmentationRequest request, LanguageServiceCompletionResponse response) { return false; },
                delegate { });

            var processor = new CortexShellLanguageResponseProcessor();
            processor.ProcessLanguageResponses(context);

            Assert.False(runtimeState.MethodInspectorRelationshipsInFlight);
            Assert.Null(state.Editor.MethodInspector.RelationshipsCallHierarchy);
        }
    }
}
