using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    internal sealed class HarmonyModuleStateStore
    {
        private const string WorkflowStateKey = "workflow";
        private const string DocumentStateKey = "document";
        private const string EditorStateKey = "editor";

        private const string PreferredGenerationKindKey = "persistent.preferredGenerationKind";
        private const string LastInspectedSymbolKey = "persistent.lastInspectedSymbol";
        private const string LastDocumentPathKey = "persistent.lastDocumentPath";

        public HarmonyModulePersistentState ReadPersistent(IWorkbenchModuleRuntime runtime)
        {
            var store = runtime != null && runtime.State != null ? runtime.State.Persistent : null;
            var state = new HarmonyModulePersistentState();
            if (store == null)
            {
                return state;
            }

            state.PreferredGenerationKind = store.GetValue(PreferredGenerationKindKey, string.Empty);
            state.LastInspectedSymbol = store.GetValue(LastInspectedSymbolKey, string.Empty);
            state.LastDocumentPath = store.GetValue(LastDocumentPathKey, string.Empty);
            return state;
        }

        public void WritePersistent(IWorkbenchModuleRuntime runtime, HarmonyModulePersistentState state)
        {
            var store = runtime != null && runtime.State != null ? runtime.State.Persistent : null;
            if (store == null || state == null)
            {
                return;
            }

            WritePersistentValue(store, PreferredGenerationKindKey, state.PreferredGenerationKind);
            WritePersistentValue(store, LastInspectedSymbolKey, state.LastInspectedSymbol);
            WritePersistentValue(store, LastDocumentPathKey, state.LastDocumentPath);
        }

        public HarmonyModuleWorkflowState GetWorkflow(IWorkbenchModuleRuntime runtime)
        {
            var store = runtime != null && runtime.State != null ? runtime.State.Workflow : null;
            return store != null
                ? store.GetOrCreate<HarmonyModuleWorkflowState>(WorkflowStateKey, delegate { return new HarmonyModuleWorkflowState(); })
                : null;
        }

        public HarmonyDocumentState GetDocument(IWorkbenchModuleRuntime runtime, EditorContextSnapshot context, bool create)
        {
            var store = runtime != null && runtime.State != null ? runtime.State.Contexts : null;
            var scope = runtime != null && runtime.Editor != null && context != null ? runtime.Editor.CreateDocumentScope(context) : null;
            if (store == null || scope == null)
            {
                return GetDocument(runtime, context != null ? context.DocumentPath : string.Empty, create);
            }

            return create
                ? store.GetOrCreate<HarmonyDocumentState>(scope, DocumentStateKey, delegate { return new HarmonyDocumentState(); })
                : store.Get<HarmonyDocumentState>(scope, DocumentStateKey);
        }

        public HarmonyDocumentState GetDocument(IWorkbenchModuleRuntime runtime, string documentPath, bool create)
        {
            var store = runtime != null && runtime.State != null ? runtime.State.Contexts : null;
            if (store == null || string.IsNullOrEmpty(documentPath))
            {
                return null;
            }

            var scope = new WorkbenchContextStateScope(
                WorkbenchContextStateScopeKind.Document,
                documentPath,
                documentPath,
                string.Empty,
                string.Empty,
                EditorSurfaceKind.Unknown);
            return create
                ? store.GetOrCreate<HarmonyDocumentState>(scope, DocumentStateKey, delegate { return new HarmonyDocumentState(); })
                : store.Get<HarmonyDocumentState>(scope, DocumentStateKey);
        }

        public HarmonyEditorState GetEditor(IWorkbenchModuleRuntime runtime, EditorContextSnapshot context, bool create)
        {
            var scope = runtime != null && runtime.Editor != null && context != null ? runtime.Editor.CreateEditorScope(context) : null;
            var store = runtime != null && runtime.State != null ? runtime.State.Contexts : null;
            if (store == null || scope == null)
            {
                return null;
            }

            return create
                ? store.GetOrCreate<HarmonyEditorState>(scope, EditorStateKey, delegate { return new HarmonyEditorState(); })
                : store.Get<HarmonyEditorState>(scope, EditorStateKey);
        }

        private static void WritePersistentValue(IWorkbenchPersistentStateStore store, string key, string value)
        {
            if (store == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(value))
            {
                store.Remove(key);
            }
            else
            {
                store.SetValue(key, value);
            }
        }
    }

    internal sealed class HarmonyModulePersistentState
    {
        public string PreferredGenerationKind = string.Empty;
        public string LastInspectedSymbol = string.Empty;
        public string LastDocumentPath = string.Empty;
    }

    internal sealed class HarmonyModuleWorkflowState
    {
        public bool RefreshRequested = true;
        public bool RuntimeAvailable;
        public string SnapshotStatusMessage = string.Empty;
        public DateTime SnapshotUtc = DateTime.MinValue;
        public string ActiveSummaryKey = string.Empty;
        public string ResolutionFailureReason = string.Empty;
        public string ActiveTypeAssemblyPath = string.Empty;
        public string ActiveTypeName = string.Empty;
        public string ActiveTypeDisplayName = string.Empty;
        public string ActiveSymbolDisplay = string.Empty;
        public string ActiveDocumentPath = string.Empty;
        public string ActiveContainingTypeName = string.Empty;
        public string ActiveAssemblyName = string.Empty;
        public string ActiveMetadataName = string.Empty;
        public HarmonyPatchInspectionRequest ActiveInspectionRequest;
        public HarmonyMethodPatchSummary ActiveSummary;
        public HarmonyMethodPatchSummary[] ActiveTypeSummaries = new HarmonyMethodPatchSummary[0];
        public HarmonyMethodPatchSummary[] SnapshotMethods = new HarmonyMethodPatchSummary[0];
        public HarmonyPatchGenerationRequest GenerationRequest;
        public HarmonyPatchGenerationPreview GenerationPreview;
        public string GenerationStatusMessage = string.Empty;
        public bool IsInsertionSelectionActive;
        public readonly List<HarmonyPatchInsertionTarget> InsertionTargets = new List<HarmonyPatchInsertionTarget>();
        public readonly Dictionary<string, HarmonyMethodPatchSummary> SummaryCache = new Dictionary<string, HarmonyMethodPatchSummary>(StringComparer.OrdinalIgnoreCase);
        public DateTime LastUpdatedUtc = DateTime.MinValue;
    }

    internal sealed class HarmonyDocumentState
    {
        public string LastInspectedSymbol = string.Empty;
        public string LastDocumentPath = string.Empty;
        public GeneratedTemplateSession ActiveTemplateSession;
        public DateTime LastUpdatedUtc = DateTime.MinValue;
    }

    internal sealed class HarmonyEditorState
    {
        public int SelectedLineNumber = -1;
        public int SelectedAbsolutePosition = -1;
        public string SelectionLabel = string.Empty;
        public DateTime LastUpdatedUtc = DateTime.MinValue;
    }
}
