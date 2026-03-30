using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;
using UnityEngine;

namespace Cortex.Plugin.Harmony
{
    internal sealed class HarmonyModule : IWorkbenchModule
    {
        private readonly HarmonyModuleStateStore _stateStore;
        private readonly HarmonyWorkflowController _workflowController;
        private Vector2 _scrollPosition = Vector2.zero;
        private Vector2 _previewScroll = Vector2.zero;

        public HarmonyModule(HarmonyModuleStateStore stateStore, HarmonyWorkflowController workflowController)
        {
            _stateStore = stateStore ?? new HarmonyModuleStateStore();
            _workflowController = workflowController ?? new HarmonyWorkflowController(_stateStore);
        }

        public string GetUnavailableMessage()
        {
            return _workflowController.GetUnavailableMessage();
        }

        public void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
        {
            if (context == null || context.Runtime == null)
            {
                GUILayout.Label("Harmony runtime is not available.");
                return;
            }

            var runtime = context.Runtime;
            var persistent = _stateStore.ReadPersistent(runtime);
            var workflow = _stateStore.GetWorkflow(runtime);
            var editorContext = runtime.Editor != null ? runtime.Editor.GetActiveContext() : null;
            var documentState = _stateStore.GetDocument(runtime, editorContext, false);
            var editorState = _stateStore.GetEditor(runtime, editorContext, false);

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            GUILayout.Label("Harmony");
            GUILayout.Label(!string.IsNullOrEmpty(workflow != null ? workflow.SnapshotStatusMessage : string.Empty)
                ? workflow.SnapshotStatusMessage
                : "Select a method or use the Harmony actions to inspect live patch data.");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(100f)))
            {
                string statusMessage;
                _workflowController.Refresh(runtime, out statusMessage);
            }

            GUI.enabled = workflow != null && workflow.ActiveSummary != null;
            if (GUILayout.Button("Copy Summary", GUILayout.Width(120f)))
            {
                GUIUtility.systemCopyBuffer = _workflowController.BuildSummaryText(runtime);
            }
            GUI.enabled = true;
            GUI.enabled = workflow != null && workflow.ActiveSummary != null && workflow.ActiveSummary.Target != null;
            if (GUILayout.Button("Navigate To Target", GUILayout.Width(160f)))
            {
                string statusMessage;
                _workflowController.NavigateToTarget(runtime, out statusMessage);
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUI.skin.box, GUILayout.ExpandHeight(true));
            DrawRow("Target", workflow != null ? workflow.ActiveSymbolDisplay : string.Empty);
            DrawRow("Preferred Generation", persistent != null ? persistent.PreferredGenerationKind : string.Empty);
            DrawRow("Last Document", persistent != null ? persistent.LastDocumentPath : string.Empty);
            DrawRow("Type Context", workflow != null ? workflow.ActiveContainingTypeName : string.Empty);
            DrawRow("Assembly Context", workflow != null ? workflow.ActiveAssemblyName : string.Empty);
            DrawRow("Runtime Snapshot", workflow != null ? workflow.SnapshotStatusMessage : string.Empty);
            DrawRow("Insertion Workflow", workflow != null && workflow.IsInsertionSelectionActive ? "Awaiting editor selection" : "Idle");
            DrawRow("Document Scope", documentState != null ? documentState.LastInspectedSymbol : string.Empty);
            DrawRow("Editor Scope", editorState != null && editorState.SelectedLineNumber > 0
                ? "Line " + editorState.SelectedLineNumber.ToString() + " at " + editorState.SelectedAbsolutePosition.ToString()
                : string.Empty);
            if (workflow != null && workflow.ActiveSummary != null)
            {
                GUILayout.Space(6f);
                DrawRow("Counts", _workflowController.DisplayService.BuildCountBreakdown(workflow.ActiveSummary.Counts));
                DrawRow("Owners", _workflowController.DisplayService.BuildOwnerSummary(workflow.ActiveSummary));
                DrawRow("Conflict Hint", workflow.ActiveSummary.ConflictHint);
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Prefix", GUILayout.Width(140f)))
            {
                string statusMessage;
                _workflowController.PrepareGeneration(runtime, runtime.Editor != null && runtime.Editor.GetActiveContext() != null ? runtime.Editor.GetActiveContext().Target : null, HarmonyPatchGenerationKind.Prefix, out statusMessage);
            }
            if (GUILayout.Button("Generate Postfix", GUILayout.Width(140f)))
            {
                string statusMessage;
                _workflowController.PrepareGeneration(runtime, runtime.Editor != null && runtime.Editor.GetActiveContext() != null ? runtime.Editor.GetActiveContext().Target : null, HarmonyPatchGenerationKind.Postfix, out statusMessage);
            }
            GUI.enabled = workflow != null && workflow.GenerationPreview != null && workflow.GenerationPreview.CanApply;
            if (GUILayout.Button("Insert Patch", GUILayout.Width(120f)))
            {
                string statusMessage;
                _workflowController.ApplyGeneration(runtime, out statusMessage);
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            DrawRow("Generation", workflow != null ? workflow.GenerationStatusMessage : string.Empty);

            if (workflow != null && workflow.GenerationPreview != null)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Preview");
                _previewScroll = GUILayout.BeginScrollView(_previewScroll, GUI.skin.box, GUILayout.Height(220f));
                GUILayout.TextArea(workflow.GenerationPreview.PreviewText ?? string.Empty, GUILayout.ExpandHeight(true));
                GUILayout.EndScrollView();
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static void DrawRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140f));
            GUILayout.Label(string.IsNullOrEmpty(value) ? "None" : value, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }
    }
}
