using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services;
using UnityEngine;

namespace Cortex.Modules.Editor
{
    internal sealed class EditorSemanticPopupSurface
    {
        private const float QuickActionsWidth = 360f;
        private const float QuickActionsHeight = 220f;
        private const float RenameWidth = 260f;
        private const float RenameHeight = 65f;
        private const float PeekWidth = 400f;
        private const float PeekHeight = 150f;
        private readonly EditorContextMenuService _contextMenuService = new EditorContextMenuService();
        private readonly IEditorContextService _contextService;
        private readonly EditorSemanticOperationService _semanticOperationService = new EditorSemanticOperationService();

        public EditorSemanticPopupSurface(IEditorContextService contextService)
        {
            _contextService = contextService;
        }

        public void DrawQuickActions(CortexShellState state, Rect anchorRect, Vector2 surfaceSize, ICommandRegistry commandRegistry)
        {
            if (state == null || state.Semantic == null || !state.Semantic.QuickActionsVisible || string.IsNullOrEmpty(state.Semantic.QuickActionsContextKey))
            {
                return;
            }

            var target = _contextService.ResolveTarget(state, state.Semantic.QuickActionsContextKey);
            if (target == null)
            {
                _semanticOperationService.CloseQuickActions(state);
                return;
            }
            var popupRect = BuildPopupRect(anchorRect, surfaceSize, QuickActionsWidth, QuickActionsHeight);
            var current = Event.current;
            GUI.Box(popupRect, GUIContent.none, GUI.skin.window);

            GUILayout.BeginArea(popupRect);
            GUILayout.BeginVertical();
            GUILayout.Label("Quick Actions: " + (state.Semantic.QuickActionsTitle ?? string.Empty), GUI.skin.label);
            GUI.SetNextControlName("Cortex.QuickActionsFilter");
            state.Semantic.QuickActionsFilterText = GUILayout.TextField(state.Semantic.QuickActionsFilterText ?? string.Empty, GUI.skin.textField);

            var actions = state.Semantic.QuickActions ?? new EditorResolvedContextAction[0];
            var previousEnabled = GUI.enabled;
            var renderedCount = 0;
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null || !MatchesQuickActionFilter(action, state.Semantic.QuickActionsFilterText))
                {
                    continue;
                }

                renderedCount++;
                GUI.enabled = action.Enabled;
                var label = action.Title ?? action.CommandId ?? string.Empty;
                if (!string.IsNullOrEmpty(action.ShortcutText))
                {
                    label += "  (" + action.ShortcutText + ")";
                }

                if (GUILayout.Button(label, GUILayout.Height(24f)))
                {
                    _contextMenuService.Execute(state, commandRegistry, target, action.CommandId);
                    _semanticOperationService.CloseQuickActions(state);
                    GUI.enabled = previousEnabled;
                    GUILayout.EndVertical();
                    GUILayout.EndArea();
                    return;
                }

                GUI.enabled = true;
                var detail = action.Enabled ? action.Description : action.DisabledReason;
                if (!string.IsNullOrEmpty(detail))
                {
                    GUILayout.Label(detail, GUI.skin.label);
                }
            }

            GUI.enabled = previousEnabled;
            if (renderedCount == 0)
            {
                GUILayout.Label("No quick actions matched the current filter.", GUI.skin.label);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(72f)) || (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape))
            {
                _semanticOperationService.CloseQuickActions(state);
                if (current != null)
                {
                    current.Use();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        public void DrawRename(CortexShellState state, Rect anchorRect, Vector2 surfaceSize, ICommandRegistry commandRegistry)
        {
            if (state == null || state.Editor == null || string.IsNullOrEmpty(state.Editor.ActiveRenameContextKey))
            {
                return;
            }

            var target = _contextService.ResolveTarget(state, state.Editor.ActiveRenameContextKey);
            if (target == null)
            {
                state.Editor.ActiveRenameContextKey = string.Empty;
                return;
            }
            var popupRect = BuildPopupRect(anchorRect, surfaceSize, RenameWidth, RenameHeight);
            var current = Event.current;

            GUI.Box(popupRect, GUIContent.none, GUI.skin.window);
            GUILayout.BeginArea(popupRect);
            GUILayout.BeginVertical();
            GUILayout.Label("Rename '" + (target.SymbolText ?? string.Empty) + "' to:", GUI.skin.label);

            GUI.SetNextControlName("Cortex.RenameInput");
            state.Editor.ActiveRenameText = GUILayout.TextField(state.Editor.ActiveRenameText, GUI.skin.textField);

            if (current != null && current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "Cortex.RenameInput")
            {
                GUI.FocusControl("Cortex.RenameInput");
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(60f)) || (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape))
            {
                state.Editor.ActiveRenameContextKey = string.Empty;
                if (current != null)
                {
                    current.Use();
                }
            }

            if (GUILayout.Button("Apply", GUILayout.Width(60f)) || (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Return))
            {
                _semanticOperationService.QueueRequest(state, target, SemanticRequestKind.RenamePreview, state.Editor.ActiveRenameText);
                state.Semantic.ActiveView = SemanticWorkbenchViewKind.RenamePreview;
                OpenSearchContainer(state, commandRegistry);
                state.StatusMessage = "Semantic rename preview requested for " + (target.SymbolText ?? string.Empty) + ".";
                state.Editor.ActiveRenameContextKey = string.Empty;
                if (current != null)
                {
                    current.Use();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        public void DrawPeek(CortexShellState state, Rect anchorRect, Vector2 surfaceSize)
        {
            if (state == null || state.Editor == null || string.IsNullOrEmpty(state.Editor.ActivePeekContextKey))
            {
                return;
            }

            var target = _contextService.ResolveTarget(state, state.Editor.ActivePeekContextKey);
            if (target == null)
            {
                state.Editor.ActivePeekContextKey = string.Empty;
                return;
            }
            var popupRect = BuildPopupRect(anchorRect, surfaceSize, PeekWidth, PeekHeight);
            var current = Event.current;

            GUI.Box(popupRect, GUIContent.none, GUI.skin.window);
            GUILayout.BeginArea(popupRect);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Peek Definition: " + (target.SymbolText ?? string.Empty), GUI.skin.label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", GUILayout.Width(24f)) || (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape))
            {
                state.Editor.ActivePeekContextKey = string.Empty;
                if (current != null)
                {
                    current.Use();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2f);

            var innerStyle = new GUIStyle(GUI.skin.box);
            innerStyle.alignment = TextAnchor.UpperLeft;
            innerStyle.wordWrap = true;

            var contentRect = new Rect(4f, 26f, PeekWidth - 8f, PeekHeight - 30f);
            var peekDefinition = state.Semantic != null ? state.Semantic.PeekDefinition : null;
            if (peekDefinition == null || !peekDefinition.Success || string.IsNullOrEmpty(peekDefinition.PreviewText))
            {
                GUI.Label(contentRect, "No semantic definition preview is available yet.", innerStyle);
            }
            else
            {
                GUI.Label(contentRect, peekDefinition.PreviewText, innerStyle);
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private static Rect BuildPopupRect(Rect anchorRect, Vector2 surfaceSize, float width, float height)
        {
            return ClampPopupRect(new Rect(anchorRect.x, anchorRect.yMax + 4f, width, height), surfaceSize);
        }

        private static Rect ClampPopupRect(Rect popupRect, Vector2 viewportSize)
        {
            if (viewportSize.x <= 0f || viewportSize.y <= 0f)
            {
                return popupRect;
            }

            if (popupRect.xMax > viewportSize.x - 8f)
            {
                popupRect.x = viewportSize.x - popupRect.width - 8f;
            }

            if (popupRect.yMax > viewportSize.y - 8f)
            {
                popupRect.y = viewportSize.y - popupRect.height - 8f;
            }

            popupRect.x = Mathf.Max(8f, popupRect.x);
            popupRect.y = Mathf.Max(8f, popupRect.y);
            return popupRect;
        }

        private static bool MatchesQuickActionFilter(EditorResolvedContextAction action, string filterText)
        {
            if (action == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(filterText))
            {
                return true;
            }

            return (action.Title ?? string.Empty).IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (action.Description ?? string.Empty).IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void OpenSearchContainer(CortexShellState state, ICommandRegistry commandRegistry)
        {
            if (state == null || commandRegistry == null || state.Workbench == null || state.Documents == null)
            {
                return;
            }

            commandRegistry.Execute("cortex.window.search", new CommandExecutionContext
            {
                ActiveContainerId = state.Workbench.FocusedContainerId,
                ActiveDocumentId = state.Documents.ActiveDocumentPath,
                FocusedRegionId = state.Workbench.FocusedContainerId
            });
        }
    }
}
