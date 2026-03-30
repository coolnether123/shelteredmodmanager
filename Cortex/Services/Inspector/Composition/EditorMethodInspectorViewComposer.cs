using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Services.Harmony.Editor;
using Cortex.Services.Harmony.Presentation;
using Cortex.Presentation.Models;
using Cortex.Services.Inspector.Actions;
using Cortex.Services.Inspector.Relationships;

namespace Cortex.Services.Inspector.Composition
{
    internal interface IEditorMethodInspectorViewComposer
    {
        MethodInspectorViewModel Compose(
            CortexShellState state,
            DocumentSession session,
            CortexMethodInspectorState inspector,
            EditorCommandTarget target,
            EditorMethodRelationshipsContext relationshipsContext,
            EditorSourceHarmonyContext sourceHarmonyContext,
            HarmonyMethodPatchSummary harmonySummary,
            string harmonyStatusMessage,
            EditorIndirectHarmonyContext indirectHarmonyContext,
            HarmonyPatchDisplayService harmonyDisplayService,
            bool showHarmony,
            bool canCreatePatch,
            bool hasPreparedPatch,
            string patchAvailabilityReason);
    }

    internal sealed class EditorMethodInspectorViewComposer : IEditorMethodInspectorViewComposer
    {
        private const int SnippetContextLineCount = 2;

        private readonly IEditorMethodInspectorNavigationActionFactory _actionFactory;

        public EditorMethodInspectorViewComposer(IEditorMethodInspectorNavigationActionFactory actionFactory)
        {
            _actionFactory = actionFactory;
        }

        public MethodInspectorViewModel Compose(
            CortexShellState state,
            DocumentSession session,
            CortexMethodInspectorState inspector,
            EditorCommandTarget target,
            EditorMethodRelationshipsContext relationshipsContext,
            EditorSourceHarmonyContext sourceHarmonyContext,
            HarmonyMethodPatchSummary harmonySummary,
            string harmonyStatusMessage,
            EditorIndirectHarmonyContext indirectHarmonyContext,
            HarmonyPatchDisplayService harmonyDisplayService,
            bool showHarmony,
            bool canCreatePatch,
            bool hasPreparedPatch,
            string patchAvailabilityReason)
        {
            var viewModel = new MethodInspectorViewModel();
            viewModel.Title = "Method Info: " + (inspector != null ? inspector.Title ?? string.Empty : string.Empty);
            viewModel.Subtitle = BuildHeaderSubtitle(target);

            var sections = new List<MethodInspectorSectionViewModel>();
            sections.Add(BuildStructureSection(inspector, target));
            sections.Add(BuildRelationshipsSection(inspector, relationshipsContext));
            if (showHarmony)
            {
                sections.Add(BuildHarmonySection(
                    inspector,
                    state,
                    sourceHarmonyContext,
                    harmonySummary,
                    harmonyStatusMessage,
                    indirectHarmonyContext,
                    harmonyDisplayService,
                    canCreatePatch,
                    hasPreparedPatch,
                    patchAvailabilityReason));
            }

            sections.Add(BuildSourceSection(inspector, session, target));
            viewModel.Sections = sections.ToArray();
            return viewModel;
        }

        internal static bool ShouldShowHarmony(
            EditorSourceHarmonyContext sourceHarmonyContext,
            HarmonyMethodPatchSummary harmonySummary,
            EditorIndirectHarmonyContext indirectHarmonyContext)
        {
            return (sourceHarmonyContext != null && sourceHarmonyContext.IsPatchMethod) ||
                (harmonySummary != null && harmonySummary.IsPatched) ||
                (indirectHarmonyContext != null && indirectHarmonyContext.PatchedCallerCount > 0);
        }

        private static MethodInspectorSectionViewModel BuildStructureSection(CortexMethodInspectorState inspector, EditorCommandTarget target)
        {
            var section = new MethodInspectorSectionViewModel();
            section.Id = "structure";
            section.Title = "Structure";
            section.Expanded = inspector != null ? inspector.OverviewExpanded : true;

            var elements = new List<MethodInspectorElementViewModel>();
            if (target == null)
            {
                elements.Add(CreateTextElement(string.Empty, "No method target is selected.", false));
            }
            else
            {
                elements.Add(CreateMetadataElement("Namespace", ExtractNamespace(target)));
                elements.Add(CreateMetadataElement("Type", target.ContainingTypeName));
                elements.Add(CreateMetadataElement("Member", target.SymbolText));
                elements.Add(CreateMetadataElement("Kind", !string.IsNullOrEmpty(target.SymbolKind) ? target.SymbolKind : "Method"));
                elements.Add(CreateMetadataElement("Classification", inspector != null ? inspector.Classification : string.Empty));
                elements.Add(CreateMetadataElement("Assembly", target.ContainingAssemblyName));
                elements.Add(CreateMetadataElement("Document", target.DocumentPath));
                elements.Add(CreateMetadataElement("Position", "Line " + Math.Max(1, target.Line) + ", Column " + Math.Max(1, target.Column)));

                var signature = BuildSignature(target);
                if (!string.IsNullOrEmpty(signature))
                {
                    elements.Add(new MethodInspectorSpacerViewModel { Height = 2f });
                    elements.Add(CreateTextElement("Signature", signature, true));
                }

                if (!string.IsNullOrEmpty(target.HoverText))
                {
                    elements.Add(CreateTextElement("Inspector Notes", target.HoverText, true));
                }
            }

            section.Elements = elements.ToArray();
            return section;
        }

        private MethodInspectorSectionViewModel BuildRelationshipsSection(CortexMethodInspectorState inspector, EditorMethodRelationshipsContext relationshipsContext)
        {
            var section = new MethodInspectorSectionViewModel();
            section.Id = "relationships";
            section.Title = "Relationships";
            section.Expanded = inspector != null ? inspector.RelationshipsExpanded : false;

            var elements = new List<MethodInspectorElementViewModel>();
            if (relationshipsContext == null)
            {
                elements.Add(CreateTextElement(string.Empty, "Method relationships are not available.", false));
            }
            else if (!relationshipsContext.IsExpanded)
            {
                elements.Add(CreateTextElement(string.Empty, relationshipsContext.StatusMessage, false));
            }
            else if (relationshipsContext.IsLoading || !relationshipsContext.HasResponse)
            {
                elements.Add(CreateTextElement(string.Empty, relationshipsContext.StatusMessage, false));
            }
            else
            {
                elements.Add(CreateMetadataElement("Depends On", relationshipsContext.OutgoingCallCount.ToString()));
                elements.Add(CreateMetadataElement("Used By", relationshipsContext.IncomingCallCount.ToString()));
                AppendRelationshipGroup(elements, "Depends On", relationshipsContext.OutgoingCalls, "This method does not call any resolved symbols.");
                elements.Add(new MethodInspectorSpacerViewModel { Height = 4f });
                AppendRelationshipGroup(elements, "Used By", relationshipsContext.IncomingCalls, "No incoming callers were found for this method.");
            }

            section.Elements = elements.ToArray();
            return section;
        }

        private void AppendRelationshipGroup(
            List<MethodInspectorElementViewModel> elements,
            string title,
            EditorMethodRelationshipItem[] items,
            string emptyMessage)
        {
            elements.Add(CreateTextElement(title, string.Empty, false));
            var safeItems = items ?? new EditorMethodRelationshipItem[0];
            if (safeItems.Length == 0)
            {
                elements.Add(CreateTextElement(string.Empty, emptyMessage, false));
                return;
            }

            var rendered = 0;
            for (var i = 0; i < safeItems.Length && rendered < 4; i++)
            {
                var item = safeItems[i];
                if (item == null)
                {
                    continue;
                }

                rendered++;
                var rows = new List<MethodInspectorMetadataViewModel>();
                if (!string.IsNullOrEmpty(item.Detail))
                {
                    rows.Add(CreateMetadataElement("Context", item.Detail));
                }

                if (!string.IsNullOrEmpty(item.ContainingTypeName))
                {
                    rows.Add(CreateMetadataElement("Type", item.ContainingTypeName));
                }

                rows.Add(CreateMetadataElement("Relationship", (item.Relationship ?? "Call") + " (" + item.CallCount + ")"));
                if (!string.IsNullOrEmpty(item.ContainingAssemblyName))
                {
                    rows.Add(CreateMetadataElement("Assembly", item.ContainingAssemblyName));
                }

                elements.Add(CreateCardElement(
                    item.Title ?? "Unknown",
                    rows.ToArray(),
                    string.Empty,
                    _actionFactory != null ? _actionFactory.CreateRelationshipActions(item) : new MethodInspectorActionViewModel[0]));
            }

            if (safeItems.Length > rendered)
            {
                elements.Add(CreateTextElement(string.Empty, "Additional relationship entries are available beyond this preview.", false));
            }
        }

        private static MethodInspectorSectionViewModel BuildSourceSection(CortexMethodInspectorState inspector, DocumentSession session, EditorCommandTarget target)
        {
            var section = new MethodInspectorSectionViewModel();
            section.Id = "source";
            section.Title = "Source Context";
            section.Expanded = inspector != null ? inspector.NavigationExpanded : true;

            var elements = new List<MethodInspectorElementViewModel>();
            if (session == null || target == null || string.IsNullOrEmpty(session.Text))
            {
                elements.Add(CreateTextElement(string.Empty, "Source context is not available for the selected method.", false));
            }
            else
            {
                elements.Add(CreateMetadataElement("Definition", BuildDefinitionText(target)));
                elements.Add(CreateMetadataElement("Fit", BuildFitText(session, target)));
                var snippet = BuildSnippet(session, target.Line, SnippetContextLineCount);
                if (string.IsNullOrEmpty(snippet))
                {
                    elements.Add(CreateTextElement(string.Empty, "No local source snippet could be built for this method.", false));
                }
                else
                {
                    elements.Add(CreateTextElement("Nearby Code", snippet, true));
                }
            }

            section.Elements = elements.ToArray();
            return section;
        }

        private MethodInspectorSectionViewModel BuildHarmonySection(
            CortexMethodInspectorState inspector,
            CortexShellState state,
            EditorSourceHarmonyContext sourceHarmonyContext,
            HarmonyMethodPatchSummary harmonySummary,
            string harmonyStatusMessage,
            EditorIndirectHarmonyContext indirectHarmonyContext,
            HarmonyPatchDisplayService harmonyDisplayService,
            bool canCreatePatch,
            bool hasPreparedPatch,
            string patchAvailabilityReason)
        {
            var section = new MethodInspectorSectionViewModel();
            section.Id = "harmony";
            section.Title = "Harmony Context";
            section.Expanded = inspector != null ? inspector.HarmonyExpanded : true;

            var elements = new List<MethodInspectorElementViewModel>();
            var hasSourceHarmonyContext = AppendSourceHarmonyElements(elements, sourceHarmonyContext);
            if (hasSourceHarmonyContext)
            {
                elements.Add(new MethodInspectorSpacerViewModel { Height = 4f });
            }

            if (harmonySummary != null)
            {
                if (hasSourceHarmonyContext)
                {
                    elements.Add(CreateTextElement("Target Runtime Patch State", "Live Harmony data for the patched runtime method.", false));
                }

                elements.Add(CreateMetadataElement("Status", harmonySummary.IsPatched ? "Patched at runtime" : "No active patches"));
                elements.Add(CreateMetadataElement("Counts", harmonyDisplayService.BuildCountBreakdown(harmonySummary.Counts)));
                elements.Add(CreateMetadataElement("Owners", harmonyDisplayService.BuildOwnerSummary(harmonySummary)));

                var entries = harmonySummary.Entries ?? new HarmonyPatchEntry[0];
                if (entries.Length == 0)
                {
                    elements.Add(CreateTextElement(string.Empty, "No live Harmony patches are registered for this function.", false));
                }
                else
                {
                    var rendered = 0;
                    for (var i = 0; i < entries.Length && rendered < 6; i++)
                    {
                        var entry = entries[i];
                        if (entry == null)
                        {
                            continue;
                        }

                        rendered++;
                        var rows = new List<MethodInspectorMetadataViewModel>();
                        rows.Add(CreateMetadataElement("Owner", !string.IsNullOrEmpty(entry.OwnerDisplayName) ? entry.OwnerDisplayName : entry.OwnerId));
                        rows.Add(CreateMetadataElement("Priority", entry.Priority.ToString()));
                        rows.Add(CreateMetadataElement("Patch Method", BuildPatchMethodSignature(entry), (entry.Before == null || entry.Before.Length == 0) && (entry.After == null || entry.After.Length == 0)));
                        if ((entry.Before != null && entry.Before.Length > 0) || (entry.After != null && entry.After.Length > 0))
                        {
                            rows.Add(CreateMetadataElement("Order", "Before: " + Join(entry.Before) + " | After: " + Join(entry.After)));
                        }

                        elements.Add(CreateCardElement(
                            harmonyDisplayService.GetPatchKindLabel(entry.PatchKind) + ": " + BuildPatchTitle(entry),
                            rows.ToArray(),
                            string.Empty,
                            new MethodInspectorActionViewModel[0]));
                    }

                    if (entries.Length > rendered)
                    {
                        elements.Add(CreateTextElement(string.Empty, "Additional Harmony entries are available in the full patch view.", false));
                    }
                }
            }
            else if (!hasSourceHarmonyContext && !string.IsNullOrEmpty(harmonyStatusMessage))
            {
                elements.Add(CreateTextElement(string.Empty, harmonyStatusMessage, false));
            }

            if (indirectHarmonyContext != null && indirectHarmonyContext.PatchedCallerCount > 0)
            {
                elements.Add(new MethodInspectorSpacerViewModel { Height = 4f });
                elements.Add(CreateTextElement("Indirect Harmony", indirectHarmonyContext.StatusMessage, false));
                AppendIndirectHarmonyElements(elements, indirectHarmonyContext, harmonyDisplayService);
            }

            if (canCreatePatch || hasPreparedPatch)
            {
                elements.Add(new MethodInspectorSpacerViewModel { Height = 4f });
                elements.Add(CreateTextElement("Patch Creation", BuildPatchCreationStatus(state, canCreatePatch, hasPreparedPatch, patchAvailabilityReason), false));
                AppendPatchCreationElements(elements, state, canCreatePatch, hasPreparedPatch);
            }

            section.Elements = elements.ToArray();
            return section;
        }

        private static bool AppendSourceHarmonyElements(List<MethodInspectorElementViewModel> elements, EditorSourceHarmonyContext sourceHarmonyContext)
        {
            if (elements == null || sourceHarmonyContext == null || !sourceHarmonyContext.IsPatchMethod)
            {
                return false;
            }

            elements.Add(CreateTextElement("Current Method", sourceHarmonyContext.StatusMessage, false));
            elements.Add(CreateMetadataElement("Patch Kind", sourceHarmonyContext.PatchKind));
            elements.Add(CreateMetadataElement("Source Method", sourceHarmonyContext.SourceMethodName));
            elements.Add(CreateMetadataElement("Patches Into", sourceHarmonyContext.TargetDisplayName));
            elements.Add(CreateMetadataElement("Target Type", sourceHarmonyContext.TargetTypeName));
            elements.Add(CreateMetadataElement("Target Method", sourceHarmonyContext.TargetMethodName + sourceHarmonyContext.TargetSignature));
            elements.Add(CreateMetadataElement("Resolved Via", string.Equals(sourceHarmonyContext.ResolutionSource, "attribute", StringComparison.OrdinalIgnoreCase)
                ? "Harmony method attribute"
                : "Harmony naming convention"));
            return true;
        }

        private void AppendIndirectHarmonyElements(List<MethodInspectorElementViewModel> elements, EditorIndirectHarmonyContext indirectContext, HarmonyPatchDisplayService harmonyDisplayService)
        {
            if (elements == null || indirectContext == null || indirectContext.IsLoading || indirectContext.PatchedCallerCount <= 0)
            {
                return;
            }

            elements.Add(CreateMetadataElement("Incoming Callers", indirectContext.IncomingCallerCount.ToString()));
            elements.Add(CreateMetadataElement("Patched Callers", indirectContext.PatchedCallerCount.ToString(), indirectContext.UnresolvedCallerCount <= 0));
            if (indirectContext.UnresolvedCallerCount > 0)
            {
                elements.Add(CreateMetadataElement("Unresolved", indirectContext.UnresolvedCallerCount.ToString()));
            }

            var callers = indirectContext.PatchedCallers ?? new EditorIndirectHarmonyCallerContext[0];
            for (var i = 0; i < callers.Length && i < 4; i++)
            {
                var caller = callers[i];
                if (caller == null || caller.Caller == null || caller.Summary == null)
                {
                    continue;
                }

                var rows = new List<MethodInspectorMetadataViewModel>();
                rows.Add(CreateMetadataElement("Type", caller.Caller.ContainingTypeName));
                rows.Add(CreateMetadataElement("Relationship", (caller.Caller.Relationship ?? "Incoming Call") + " (" + caller.Caller.CallCount + ")"));
                rows.Add(CreateMetadataElement("Patch Counts", harmonyDisplayService.BuildCountBreakdown(caller.Summary.Counts)));
                rows.Add(CreateMetadataElement("Owners", harmonyDisplayService.BuildOwnerSummary(caller.Summary), false));
                elements.Add(CreateCardElement(caller.Caller.SymbolDisplay ?? "Patched Caller", rows.ToArray(), string.Empty, new MethodInspectorActionViewModel[0]));
            }
        }

        private static string BuildPatchCreationStatus(CortexShellState state, bool canCreatePatch, bool hasPreparedPatch, string patchAvailabilityReason)
        {
            if (!canCreatePatch && !hasPreparedPatch)
            {
                return !string.IsNullOrEmpty(patchAvailabilityReason)
                    ? patchAvailabilityReason
                    : "Patch generation is not available for the selected method.";
            }

            if (hasPreparedPatch && state != null && state.Harmony != null && state.Harmony.GenerationRequest != null)
            {
                return state.Harmony.GenerationStatusMessage ?? "Choose a destination file for the generated patch.";
            }

            return "Choose Prefix or Postfix, then pick where the generated patch should go.";
        }

        private static void AppendPatchCreationElements(List<MethodInspectorElementViewModel> elements, CortexShellState state, bool canCreatePatch, bool hasPreparedPatch)
        {
            if (elements == null)
            {
                return;
            }

            elements.Add(CreateActionElement(
                new MethodInspectorActionViewModel
                {
                    Id = "patch:create:prefix",
                    Label = "Create Prefix Patch",
                    Enabled = canCreatePatch
                },
                "Generate a Prefix scaffold for the selected runtime method."));
            elements.Add(CreateActionElement(
                new MethodInspectorActionViewModel
                {
                    Id = "patch:create:postfix",
                    Label = "Create Postfix Patch",
                    Enabled = canCreatePatch
                },
                "Generate a Postfix scaffold for the selected runtime method."));

            if (!hasPreparedPatch || state == null || state.Harmony == null || state.Harmony.GenerationRequest == null)
            {
                return;
            }

            var insertionTargets = state.Harmony.InsertionTargets;
            if (insertionTargets == null || insertionTargets.Count == 0)
            {
                elements.Add(CreateTextElement(string.Empty, "No writable patch destinations were suggested for the active workspace.", false));
                return;
            }

            for (var i = 0; i < insertionTargets.Count && i < 4; i++)
            {
                var insertionTarget = insertionTargets[i];
                if (insertionTarget == null)
                {
                    continue;
                }

                elements.Add(CreateCardElement(
                    insertionTarget.DisplayName ?? insertionTarget.FilePath ?? string.Empty,
                    new MethodInspectorMetadataViewModel[0],
                    insertionTarget.Reason ?? string.Empty,
                    new[]
                    {
                        new MethodInspectorActionViewModel
                        {
                            Id = "patch:open:" + i,
                            Label = "Open And Place Here",
                            Enabled = true
                        }
                    }));
            }
        }

        private static MethodInspectorMetadataViewModel CreateMetadataElement(string label, string value)
        {
            return CreateMetadataElement(label, value, true);
        }

        private static MethodInspectorMetadataViewModel CreateMetadataElement(string label, string value, bool drawDivider)
        {
            return new MethodInspectorMetadataViewModel
            {
                Label = label ?? string.Empty,
                Value = !string.IsNullOrEmpty(value) ? value : "Unknown",
                DrawDivider = drawDivider
            };
        }

        private static MethodInspectorTextViewModel CreateTextElement(string label, string value, bool monospace)
        {
            return new MethodInspectorTextViewModel
            {
                Label = label ?? string.Empty,
                Value = value ?? string.Empty,
                Monospace = monospace
            };
        }

        private static MethodInspectorActionElementViewModel CreateActionElement(MethodInspectorActionViewModel action, string hint)
        {
            return new MethodInspectorActionElementViewModel
            {
                Action = action ?? new MethodInspectorActionViewModel(),
                Hint = hint ?? string.Empty
            };
        }

        private static MethodInspectorCardViewModel CreateCardElement(string title, MethodInspectorMetadataViewModel[] rows, string body, MethodInspectorActionViewModel[] actions)
        {
            return new MethodInspectorCardViewModel
            {
                Title = title ?? string.Empty,
                Rows = rows ?? new MethodInspectorMetadataViewModel[0],
                Body = body ?? string.Empty,
                Actions = actions ?? new MethodInspectorActionViewModel[0]
            };
        }

        private static string BuildHeaderSubtitle(EditorCommandTarget target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            var namespaceName = ExtractNamespace(target);
            var typeName = target.ContainingTypeName ?? string.Empty;
            if (string.IsNullOrEmpty(namespaceName))
            {
                return typeName;
            }

            return string.IsNullOrEmpty(typeName)
                ? namespaceName
                : namespaceName + "  |  " + typeName;
        }

        private static string BuildSignature(EditorCommandTarget target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(target.QualifiedSymbolDisplay))
            {
                return target.QualifiedSymbolDisplay;
            }

            return target.SymbolText ?? string.Empty;
        }

        private static string BuildFitText(DocumentSession session, EditorCommandTarget target)
        {
            if (session == null || target == null)
            {
                return string.Empty;
            }

            var lines = SplitLines(session.Text);
            if (target.Line <= 0 || target.Line > lines.Length)
            {
                return "Method line is outside the loaded text snapshot.";
            }

            var previous = target.Line > 1 ? TrimForSummary(lines[target.Line - 2]) : string.Empty;
            var current = TrimForSummary(lines[target.Line - 1]);
            var next = target.Line < lines.Length ? TrimForSummary(lines[target.Line]) : string.Empty;
            var summary = "Declaration line: " + current;
            if (!string.IsNullOrEmpty(previous))
            {
                summary += " | Prev: " + previous;
            }

            if (!string.IsNullOrEmpty(next))
            {
                summary += " | Next: " + next;
            }

            return summary;
        }

        private static string BuildSnippet(DocumentSession session, int lineNumber, int contextLineCount)
        {
            if (session == null || string.IsNullOrEmpty(session.Text))
            {
                return string.Empty;
            }

            var lines = SplitLines(session.Text);
            if (lines.Length == 0)
            {
                return string.Empty;
            }

            var safeLine = Math.Max(1, Math.Min(lineNumber, lines.Length));
            var start = Math.Max(1, safeLine - Math.Max(0, contextLineCount));
            var end = Math.Min(lines.Length, safeLine + Math.Max(0, contextLineCount));
            var snippet = string.Empty;
            for (var i = start; i <= end; i++)
            {
                if (snippet.Length > 0)
                {
                    snippet += Environment.NewLine;
                }

                snippet += i.ToString().PadLeft(4) + "  " + lines[i - 1];
            }

            return snippet;
        }

        private static string BuildDefinitionText(EditorCommandTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.DefinitionDocumentPath))
            {
                return "Unavailable";
            }

            return target.DefinitionDocumentPath + ":" + Math.Max(1, target.DefinitionLine) + ":" + Math.Max(1, target.DefinitionColumn);
        }

        private static string ExtractNamespace(EditorCommandTarget target)
        {
            var qualified = target != null ? target.QualifiedSymbolDisplay ?? string.Empty : string.Empty;
            var containingType = target != null ? target.ContainingTypeName ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(qualified))
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(containingType))
            {
                var marker = "." + containingType;
                var markerIndex = qualified.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex > 0)
                {
                    return qualified.Substring(0, markerIndex);
                }
            }

            var lastDot = qualified.LastIndexOf('.');
            return lastDot > 0 ? qualified.Substring(0, lastDot) : string.Empty;
        }

        private static string BuildPatchTitle(HarmonyPatchEntry entry)
        {
            return !string.IsNullOrEmpty(entry.PatchMethodName)
                ? entry.PatchMethodName
                : !string.IsNullOrEmpty(entry.OwnerDisplayName)
                    ? entry.OwnerDisplayName
                    : entry.OwnerId ?? string.Empty;
        }

        private static string BuildPatchMethodSignature(HarmonyPatchEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(entry.PatchMethodSignature))
            {
                return entry.PatchMethodSignature;
            }

            return !string.IsNullOrEmpty(entry.PatchMethodName) ? entry.PatchMethodName : "Unknown";
        }

        private static string Join(string[] values)
        {
            return values != null && values.Length > 0
                ? string.Join(", ", values)
                : "-";
        }

        private static string[] SplitLines(string text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');
        }

        private static string TrimForSummary(string value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length <= 72)
            {
                return trimmed;
            }

            return trimmed.Substring(0, 72) + "...";
        }
    }
}
