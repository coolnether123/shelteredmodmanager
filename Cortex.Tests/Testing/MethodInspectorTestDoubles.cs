using System;
using System.Collections.Generic;
using Cortex;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Editor;
using Cortex.Services.Harmony.Generation;
using Cortex.Services.Harmony.Inspection;
using Cortex.Services.Harmony.Presentation;
using Cortex.Services.Harmony.Resolution;
using Cortex.Services.Navigation;
using Cortex.Services.Semantics.Context;
using GameModding.Shared.Restart;
using UnityEngine;

namespace Cortex.Tests.Testing
{
    internal sealed class TestEditorContextService : IEditorContextService
    {
        private readonly EditorCommandTarget _target;
        private readonly EditorCommandInvocation _invocation;

        public TestEditorContextService(EditorCommandTarget target)
        {
            _target = target;
            _invocation = target != null
                ? new EditorCommandInvocation { Target = target }
                : null;
        }

        public string BuildSurfaceId(string documentPath, EditorSurfaceKind surfaceKind, string paneId) { return string.Empty; }
        public EditorContextSnapshot PublishDocumentContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, bool editingEnabled, int absolutePosition) { return null; }
        public EditorContextSnapshot PublishInvocationContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, EditorCommandInvocation invocation, bool setActive) { return null; }
        public EditorContextSnapshot PublishTargetContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, EditorCommandTarget target, bool setActive) { return null; }
        public EditorContextSnapshot GetActiveContext(CortexShellState state) { return null; }
        public EditorContextSnapshot GetHoveredContext(CortexShellState state) { return null; }
        public EditorContextSnapshot GetContext(CortexShellState state, string contextKey) { return null; }
        public EditorContextSnapshot GetSurfaceContext(CortexShellState state, string surfaceId) { return null; }
        public EditorCommandTarget ResolveTarget(CortexShellState state, string contextKey) { return _target; }
        public EditorCommandInvocation ResolveInvocation(CortexShellState state, string contextKey) { return _invocation; }
        public LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string contextKey, string hoverKey) { return null; }
        public LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string hoverKey) { return null; }
        public EditorResolvedHoverContent ResolveHoverContent(CortexShellState state, string contextKey, string hoverKey) { return null; }
        public EditorResolvedHoverContent ResolveHoverContent(CortexShellState state, string hoverKey) { return null; }
        public string ApplyHoverResponse(CortexShellState state, string contextKey, string hoverKey, LanguageServiceHoverResponse response) { return string.Empty; }
        public void ApplyHoverContent(CortexShellState state, string contextKey, string hoverKey, EditorResolvedHoverContent content) { }
        public void ApplySymbolContext(CortexShellState state, string contextKey, LanguageServiceSymbolContextResponse response) { }
        public void ClearHoverResponse(CortexShellState state, string contextKey) { }
        public void PublishHoveredContext(CortexShellState state, string contextKey, string definitionDocumentPath) { }
        public void ClearHoveredContext(CortexShellState state) { }
        public string BuildContextKey(string surfaceId, string documentPath, int documentVersion, int caretIndex, int selectionStart, int selectionEnd, int targetStart, int targetLength, string symbolText) { return string.Empty; }
    }

    internal sealed class TestLanguageProviderSession : ILanguageProviderSession
    {
        private readonly Queue<LanguageRuntimeMessage> _messages = new Queue<LanguageRuntimeMessage>();
        public int CancelRequestCallCount;
        public string LastCanceledRequestId = string.Empty;
        public string NextCallHierarchyRequestId = string.Empty;

        public LanguageProviderDescriptor Descriptor { get { return new LanguageProviderDescriptor(); } }
        public string ConfigurationFingerprint { get { return string.Empty; } }
        public string LastError { get { return string.Empty; } }
        public bool IsRunning { get { return true; } }

        public void Enqueue(LanguageRuntimeMessage message)
        {
            _messages.Enqueue(message);
        }

        public void Start(LanguageServiceInitializeRequest request) { }
        public void Advance() { }
        public bool TryCancelRequest(string requestId)
        {
            CancelRequestCallCount++;
            LastCanceledRequestId = requestId ?? string.Empty;
            return true;
        }
        public bool TryDequeueMessage(out LanguageRuntimeMessage message)
        {
            if (_messages.Count > 0)
            {
                message = _messages.Dequeue();
                return true;
            }

            message = null;
            return false;
        }

        public string QueueStatus() { return string.Empty; }
        public string QueueAnalyzeDocument(LanguageServiceDocumentRequest request) { return string.Empty; }
        public string QueueHover(LanguageServiceHoverRequest request) { return string.Empty; }
        public string QueueGoToDefinition(LanguageServiceDefinitionRequest request) { return string.Empty; }
        public string QueueCompletion(LanguageServiceCompletionRequest request) { return string.Empty; }
        public string QueueSignatureHelp(LanguageServiceSignatureHelpRequest request) { return string.Empty; }
        public string QueueSymbolContext(LanguageServiceSymbolContextRequest request) { return string.Empty; }
        public string QueueRenamePreview(LanguageServiceRenameRequest request) { return string.Empty; }
        public string QueueReferences(LanguageServiceReferencesRequest request) { return string.Empty; }
        public string QueueGoToBase(LanguageServiceBaseSymbolRequest request) { return string.Empty; }
        public string QueueGoToImplementation(LanguageServiceImplementationRequest request) { return string.Empty; }
        public string QueueCallHierarchy(LanguageServiceCallHierarchyRequest request) { return NextCallHierarchyRequestId; }
        public string QueueValueSource(LanguageServiceValueSourceRequest request) { return string.Empty; }
        public string QueueDocumentTransformPreview(LanguageServiceDocumentTransformRequest request) { return string.Empty; }
        public void Shutdown() { }
        public void Dispose() { }
    }

    internal sealed class TestInspectorOverlayController : IEditorMethodInspectorOverlayController
    {
        public Rect PredictResult = new Rect(1f, 2f, 3f, 4f);
        public Rect DrawResult = new Rect(5f, 6f, 7f, 8f);
        public int PredictCallCount;
        public int InputCallCount;
        public int DrawCallCount;

        public Rect PredictRect(CortexShellState state, DocumentSession session, Rect anchorRect, Vector2 surfaceSize)
        {
            PredictCallCount++;
            return PredictResult;
        }

        public void HandlePreDrawInput(Event current, Rect panelRect, Vector2 localPointer)
        {
            InputCallCount++;
        }

        public Rect Draw(
            CortexShellState state,
            DocumentSession session,
            Rect anchorRect,
            Vector2 surfaceSize,
            ICortexNavigationService navigationService,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            GUIStyle containerStyle,
            GUIStyle buttonStyle,
            GUIStyle headerStyle,
            IDocumentService documentService,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyInspectionService,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchDisplayService harmonyDisplayService,
            HarmonyPatchGenerationService harmonyGenerationService,
            Cortex.Rendering.Abstractions.IPanelRenderer panelRenderer)
        {
            DrawCallCount++;
            return DrawResult;
        }
    }

    internal sealed class TestProjectCatalog : IProjectCatalog
    {
        private readonly IList<CortexProjectDefinition> _projects;

        public TestProjectCatalog(IList<CortexProjectDefinition> projects = null)
        {
            _projects = projects ?? new List<CortexProjectDefinition>();
        }

        public IList<CortexProjectDefinition> GetProjects() { return _projects; }
        public CortexProjectDefinition GetProject(string modId) { return null; }
        public void Upsert(CortexProjectDefinition definition) { }
        public void Remove(string modId) { }
    }

    internal sealed class TestLoadedModCatalog : ILoadedModCatalog
    {
        public IList<LoadedModInfo> GetLoadedMods() { return new List<LoadedModInfo>(); }
        public LoadedModInfo GetMod(string modId) { return null; }
    }
}
