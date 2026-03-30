using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Services.Semantics.Context;
using Cortex.Services.Editor.Presentation;

namespace Cortex.Services.Inspector.Lifecycle
{
    internal sealed class EditorMethodInspectorService
    {
        private readonly EditorClassificationPresentationService _classificationPresentationService = new EditorClassificationPresentationService();
        private readonly IEditorContextService _contextService;

        public EditorMethodInspectorService(IEditorContextService contextService)
        {
            _contextService = contextService;
        }

        public bool TryOpen(CortexShellState state, EditorCommandInvocation invocation, string classification)
        {
            var inspector = state != null && state.Editor != null ? state.Editor.MethodInspector : null;
            var target = invocation != null ? invocation.Target : null;
            if (inspector == null || target == null || !CanInspect(target, classification))
            {
                return false;
            }

            var existingKey = inspector.ContextKey ?? string.Empty;
            var nextKey = target.ContextKey ?? string.Empty;
            var preserveSections = string.Equals(existingKey, nextKey, StringComparison.Ordinal);

            inspector.IsVisible = true;
            inspector.Title = !string.IsNullOrEmpty(target.SymbolText) ? target.SymbolText : "Method";
            inspector.Classification = _classificationPresentationService.NormalizeClassification(classification);
            inspector.ContextKey = target.ContextKey ?? string.Empty;
            if (!preserveSections)
            {
                inspector.OverviewExpanded = true;
                inspector.NavigationExpanded = true;
                inspector.RelationshipsExpanded = false;
                inspector.ExtensionsExpanded = true;
                inspector.SectionExpansionStates.Clear();
            }

            if (!preserveSections)
            {
                ResetRelationships(inspector, target);
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
            inspector.ContextKey = string.Empty;
            inspector.RelationshipsRequested = false;
            inspector.RelationshipsCycle++;
            inspector.RelationshipsTargetKey = string.Empty;
            inspector.RelationshipsRequestKey = string.Empty;
            inspector.RelationshipsStatusMessage = string.Empty;
            inspector.RelationshipsCallHierarchy = null;
        }

        public bool IsVisibleForDocument(CortexShellState state, string documentPath)
        {
            var inspector = state != null && state.Editor != null ? state.Editor.MethodInspector : null;
            var target = _contextService != null ? _contextService.ResolveTarget(state, inspector != null ? inspector.ContextKey : string.Empty) : null;
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

        public bool EnsureRelationshipsRequest(CortexShellState state)
        {
            var inspector = state != null && state.Editor != null ? state.Editor.MethodInspector : null;
            var target = _contextService != null ? _contextService.ResolveTarget(state, inspector != null ? inspector.ContextKey : string.Empty) : null;
            if (inspector == null || !inspector.IsVisible || !inspector.RelationshipsExpanded || target == null)
            {
                return false;
            }

            var targetKey = BuildTargetKey(target);
            if (inspector.RelationshipsRequested &&
                string.Equals(inspector.RelationshipsTargetKey, targetKey, StringComparison.Ordinal))
            {
                return false;
            }

            ResetRelationships(inspector, target);
            inspector.RelationshipsRequested = true;
            inspector.RelationshipsRequestKey = targetKey + "|relationships";
            inspector.RelationshipsStatusMessage = "Analyzing method relationships.";
            return true;
        }

        public void ToggleSection(CortexShellState state, string sectionId)
        {
            var inspector = state != null && state.Editor != null ? state.Editor.MethodInspector : null;
            if (inspector == null)
            {
                return;
            }

            switch (sectionId ?? string.Empty)
            {
                case "structure":
                    inspector.OverviewExpanded = !inspector.OverviewExpanded;
                    break;
                case "source":
                    inspector.NavigationExpanded = !inspector.NavigationExpanded;
                    break;
                case "relationships":
                    inspector.RelationshipsExpanded = !inspector.RelationshipsExpanded;
                    if (!inspector.RelationshipsExpanded)
                    {
                        var target = _contextService != null ? _contextService.ResolveTarget(state, inspector.ContextKey ?? string.Empty) : null;
                        ResetRelationships(inspector, target);
                    }
                    break;
                case "extensions":
                    inspector.ExtensionsExpanded = !inspector.ExtensionsExpanded;
                    break;
                default:
                    var current = ResolveSectionExpansion(inspector, sectionId, true);
                    inspector.SectionExpansionStates[sectionId ?? string.Empty] = !current;
                    break;
            }
        }

        public static bool ResolveSectionExpansion(CortexMethodInspectorState inspector, string sectionId, bool defaultExpanded)
        {
            if (inspector == null || string.IsNullOrEmpty(sectionId))
            {
                return defaultExpanded;
            }

            bool expanded;
            return inspector.SectionExpansionStates.TryGetValue(sectionId, out expanded)
                ? expanded
                : defaultExpanded;
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

        private static void ResetRelationships(CortexMethodInspectorState inspector, EditorCommandTarget target)
        {
            if (inspector == null)
            {
                return;
            }

            inspector.RelationshipsRequested = false;
            inspector.RelationshipsCycle++;
            inspector.RelationshipsTargetKey = BuildTargetKey(target);
            inspector.RelationshipsRequestKey = string.Empty;
            inspector.RelationshipsStatusMessage = string.Empty;
            inspector.RelationshipsCallHierarchy = null;
        }
    }
}
