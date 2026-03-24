using System;
using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class EditorMethodInspectorService
    {
        private readonly EditorClassificationPresentationService _classificationPresentationService = new EditorClassificationPresentationService();

        public bool TryOpen(CortexShellState state, EditorCommandInvocation invocation, string classification)
        {
            var inspector = state != null && state.Editor != null ? state.Editor.MethodInspector : null;
            var target = invocation != null ? invocation.Target : null;
            if (inspector == null || target == null || !CanInspect(target, classification))
            {
                return false;
            }

            var existingKey = BuildKey(inspector.Invocation);
            var nextKey = BuildKey(invocation);
            var preserveSections = string.Equals(existingKey, nextKey, StringComparison.Ordinal);

            inspector.IsVisible = true;
            inspector.Title = !string.IsNullOrEmpty(target.SymbolText) ? target.SymbolText : "Method";
            inspector.Classification = _classificationPresentationService.NormalizeClassification(classification);
            inspector.Invocation = invocation;
            if (!preserveSections)
            {
                inspector.OverviewExpanded = true;
                inspector.NavigationExpanded = true;
                inspector.ReferencesExpanded = true;
                inspector.HarmonyExpanded = true;
                inspector.IndirectHarmonyExpanded = true;
            }

            if (!preserveSections)
            {
                ResetCallHierarchy(inspector, target);
            }

            return true;
        }

        public void Close(CortexShellState state)
        {
            var inspector = state != null && state.Editor != null ? state.Editor.MethodInspector : null;
            if (inspector == null)
            {
                return;
            }

            inspector.IsVisible = false;
            inspector.Title = string.Empty;
            inspector.Classification = string.Empty;
            inspector.Invocation = null;
            inspector.CallHierarchyRequested = false;
            inspector.CallHierarchyTargetKey = string.Empty;
            inspector.CallHierarchyRequestKey = string.Empty;
            inspector.CallHierarchyStatusMessage = string.Empty;
            inspector.CallHierarchy = null;
        }

        public bool IsVisibleForDocument(CortexShellState state, string documentPath)
        {
            var inspector = state != null && state.Editor != null ? state.Editor.MethodInspector : null;
            var target = inspector != null && inspector.Invocation != null ? inspector.Invocation.Target : null;
            return inspector != null &&
                inspector.IsVisible &&
                target != null &&
                string.Equals(target.DocumentPath ?? string.Empty, documentPath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        public bool CanInspect(EditorCommandTarget target, string classification)
        {
            if (target == null || string.IsNullOrEmpty(target.SymbolText))
            {
                return false;
            }

            return CanInspectSymbol(target.SymbolText, classification, target.SymbolKind);
        }

        public bool CanInspectSymbol(string symbolText, string classification, string symbolKind)
        {
            if (string.IsNullOrEmpty(symbolText))
            {
                return false;
            }

            var normalizedClassification = _classificationPresentationService.NormalizeClassification(classification);
            if (IsMethodLike(normalizedClassification))
            {
                return true;
            }

            return IsMethodLike(_classificationPresentationService.NormalizeClassification(symbolKind));
        }

        public bool EnsureCallHierarchyRequest(CortexShellState state)
        {
            var inspector = state != null && state.Editor != null ? state.Editor.MethodInspector : null;
            var target = inspector != null && inspector.Invocation != null ? inspector.Invocation.Target : null;
            if (inspector == null || !inspector.IsVisible || target == null)
            {
                return false;
            }

            var targetKey = BuildTargetKey(target);
            if (inspector.CallHierarchyRequested &&
                string.Equals(inspector.CallHierarchyTargetKey, targetKey, StringComparison.Ordinal))
            {
                return false;
            }

            ResetCallHierarchy(inspector, target);
            inspector.CallHierarchyRequested = true;
            inspector.CallHierarchyRequestKey = targetKey + "|call-hierarchy";
            inspector.CallHierarchyStatusMessage = "Analyzing incoming callers for Harmony context.";
            return true;
        }

        public static string BuildTargetKey(EditorCommandTarget target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            return (target.DocumentPath ?? string.Empty) + "|" +
                target.AbsolutePosition + "|" +
                (target.SymbolText ?? string.Empty);
        }

        private static bool IsMethodLike(string classification)
        {
            return !string.IsNullOrEmpty(classification) &&
                (classification.IndexOf("method", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 classification.IndexOf("property", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 classification.IndexOf("event", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildKey(EditorCommandInvocation invocation)
        {
            var target = invocation != null ? invocation.Target : null;
            return BuildTargetKey(target);
        }

        private static void ResetCallHierarchy(CortexMethodInspectorState inspector, EditorCommandTarget target)
        {
            if (inspector == null)
            {
                return;
            }

            inspector.CallHierarchyRequested = false;
            inspector.CallHierarchyTargetKey = BuildTargetKey(target);
            inspector.CallHierarchyRequestKey = string.Empty;
            inspector.CallHierarchyStatusMessage = string.Empty;
            inspector.CallHierarchy = null;
        }
    }
}
