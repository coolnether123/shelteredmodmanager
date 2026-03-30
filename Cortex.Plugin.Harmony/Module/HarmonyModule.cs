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
            var activeTarget = editorContext != null ? editorContext.Target : null;
            var ui = context.Ui;

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            DrawHeader(
                ui,
                "Harmony",
                !string.IsNullOrEmpty(workflow != null ? workflow.SnapshotStatusMessage : string.Empty)
                    ? workflow.SnapshotStatusMessage
                    : "Select a method or use the Harmony actions to inspect live patch data.");

            GUILayout.BeginHorizontal();
            GUI.enabled = runtime.Commands != null && runtime.Commands.CanExecute(HarmonyPluginIds.RefreshCommandId, null);
            if (GUILayout.Button("Refresh", GUILayout.Width(100f)))
            {
                runtime.Commands.Execute(HarmonyPluginIds.RefreshCommandId, null);
            }

            GUI.enabled = runtime.Commands != null && runtime.Commands.CanExecute(HarmonyPluginIds.CopySummaryCommandId, null);
            if (GUILayout.Button("Copy Summary", GUILayout.Width(120f)))
            {
                runtime.Commands.Execute(HarmonyPluginIds.CopySummaryCommandId, null);
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
            DrawSectionPanel(ui, "Runtime", delegate
            {
                DrawRow(ui, "Target", workflow != null ? workflow.ActiveSymbolDisplay : string.Empty);
                DrawRow(ui, "Preferred Generation", persistent != null ? persistent.PreferredGenerationKind : string.Empty);
                DrawRow(ui, "Last Document", persistent != null ? persistent.LastDocumentPath : string.Empty);
                DrawRow(ui, "Type Context", workflow != null ? workflow.ActiveContainingTypeName : string.Empty);
                DrawRow(ui, "Assembly Context", workflow != null ? workflow.ActiveAssemblyName : string.Empty);
                DrawRow(ui, "Runtime Snapshot", workflow != null ? workflow.SnapshotStatusMessage : string.Empty);
                DrawRow(ui, "Insertion Workflow", workflow != null && workflow.IsInsertionSelectionActive ? "Awaiting editor selection" : "Idle");
                DrawRow(ui, "Document Scope", documentState != null ? documentState.LastInspectedSymbol : string.Empty);
                DrawRow(ui, "Editor Scope", editorState != null && editorState.SelectedLineNumber > 0
                    ? "Line " + editorState.SelectedLineNumber.ToString() + " at " + editorState.SelectedAbsolutePosition.ToString()
                    : string.Empty);
                if (workflow != null && workflow.ActiveSummary != null)
                {
                    DrawRow(ui, "Counts", _workflowController.DisplayService.BuildCountBreakdown(workflow.ActiveSummary.Counts));
                    DrawRow(ui, "Owners", _workflowController.DisplayService.BuildOwnerSummary(workflow.ActiveSummary));
                    DrawRow(ui, "Conflict Hint", workflow.ActiveSummary.ConflictHint);
                }
            });

            GUILayout.BeginHorizontal();
            GUI.enabled = runtime.Commands != null && runtime.Commands.CanExecute(HarmonyPluginIds.GeneratePrefixCommandId, activeTarget);
            if (GUILayout.Button("Generate Prefix", GUILayout.Width(140f)))
            {
                runtime.Commands.Execute(HarmonyPluginIds.GeneratePrefixCommandId, activeTarget);
            }
            GUI.enabled = runtime.Commands != null && runtime.Commands.CanExecute(HarmonyPluginIds.GeneratePostfixCommandId, activeTarget);
            if (GUILayout.Button("Generate Postfix", GUILayout.Width(140f)))
            {
                runtime.Commands.Execute(HarmonyPluginIds.GeneratePostfixCommandId, activeTarget);
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
            DrawSectionPanel(ui, "Generation", delegate
            {
                DrawRow(ui, "Status", workflow != null ? workflow.GenerationStatusMessage : string.Empty);
            });

            if (workflow != null && workflow.GenerationPreview != null)
            {
                DrawSectionPanel(ui, "Preview", delegate
                {
                    _previewScroll = GUILayout.BeginScrollView(_previewScroll, GUI.skin.box, GUILayout.Height(220f));
                    GUILayout.TextArea(workflow.GenerationPreview.PreviewText ?? string.Empty, GUILayout.ExpandHeight(true));
                    GUILayout.EndScrollView();
                });
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static void DrawHeader(IWorkbenchUiSurface ui, string title, string description)
        {
            if (ui != null)
            {
                ui.DrawSectionHeader(title, description);
                return;
            }

            GUILayout.Label(title);
            GUILayout.Label(description ?? string.Empty);
        }

        private static void DrawSectionPanel(IWorkbenchUiSurface ui, string title, System.Action drawBody)
        {
            if (ui != null)
            {
                ui.DrawSectionPanel(title, drawBody);
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label(title);
            if (drawBody != null)
            {
                drawBody();
            }
        }

        private static void DrawRow(IWorkbenchUiSurface ui, string label, string value)
        {
            if (ui != null)
            {
                ui.BeginPropertyRow();
                ui.DrawPropertyLabelColumn(label, string.Empty);
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                GUILayout.Label(string.IsNullOrEmpty(value) ? "None" : value, GUILayout.ExpandWidth(true));
                GUILayout.EndVertical();
                ui.EndPropertyRow();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140f));
            GUILayout.Label(string.IsNullOrEmpty(value) ? "None" : value, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }
    }
}
