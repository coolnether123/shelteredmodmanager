using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Rendering.Models;

namespace Cortex.Services
{
    internal sealed class EditorMethodInspectorPreparedView
    {
        public CortexMethodInspectorState Inspector;
        public EditorCommandInvocation Invocation;
        public EditorCommandTarget Target;
        public PanelDocument Document;
    }

    internal sealed class EditorMethodInspectorPresentationService
    {
        private const int SnippetContextLineCount = 2;

        private readonly IEditorContextService _contextService;
        private readonly EditorMethodInspectorService _inspectorService;
        private readonly EditorMethodHarmonyContextService _harmonyContextService = new EditorMethodHarmonyContextService();
        private readonly EditorMethodRelationshipsContextService _relationshipsContextService = new EditorMethodRelationshipsContextService();
        private readonly EditorMethodTargetMetadataService _targetMetadataService;
        private readonly EditorMethodPatchCreationService _patchCreationService = new EditorMethodPatchCreationService();
        private readonly HarmonyPatchDisplayService _fallbackHarmonyDisplayService = new HarmonyPatchDisplayService();

        public EditorMethodInspectorPresentationService(IEditorContextService contextService)
        {
            _contextService = contextService;
            _inspectorService = new EditorMethodInspectorService(contextService);
            _targetMetadataService = new EditorMethodTargetMetadataService(contextService);
        }

        public EditorMethodInspectorPreparedView Prepare(
            CortexShellState state,
            DocumentSession session,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyInspectionService,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchDisplayService harmonyDisplayService,
            HarmonyPatchGenerationService harmonyGenerationService)
        {
            var inspector = state != null && state.Editor != null ? state.Editor.MethodInspector : null;
            var invocation = _contextService != null ? _contextService.ResolveInvocation(state, inspector != null ? inspector.ContextKey : string.Empty) : null;
            var target = invocation != null ? invocation.Target : null;
            if (target == null)
            {
                _inspectorService.Close(state);
                return null;
            }

            _targetMetadataService.EnsureSymbolContextRequest(state, target);
            _targetMetadataService.Enrich(target, session, state);

            if (inspector != null && inspector.RelationshipsExpanded)
            {
                _inspectorService.EnsureRelationshipsRequest(state);
            }

            var relationshipsContext = _relationshipsContextService.BuildContext(inspector);
            var sourceHarmonyContext = _harmonyContextService.BuildSourcePatchContext(
                state,
                target,
                projectCatalog,
                harmonyResolutionService);

            string harmonyStatusMessage;
            var harmonySummary = TryLoadConditionalHarmonySummary(
                state,
                target,
                sourceHarmonyContext,
                projectCatalog,
                loadedModCatalog,
                sourceLookupIndex,
                harmonyInspectionService,
                harmonyResolutionService,
                out harmonyStatusMessage);

            var indirectHarmonyContext = _harmonyContextService.BuildIndirectContext(
                state,
                relationshipsContext,
                loadedModCatalog,
                projectCatalog,
                sourceLookupIndex,
                harmonyInspectionService,
                harmonyResolutionService);

            var showHarmony = ShouldShowHarmony(sourceHarmonyContext, harmonySummary, indirectHarmonyContext);

            string patchAvailabilityReason = string.Empty;
            var canCreatePatch = false;
            var hasPreparedPatch = false;
            if (showHarmony)
            {
                canCreatePatch = _patchCreationService.CanPreparePatch(
                    state,
                    target,
                    projectCatalog,
                    sourceLookupIndex,
                    harmonyResolutionService,
                    harmonyGenerationService,
                    out patchAvailabilityReason);
                hasPreparedPatch = _patchCreationService.IsPreparedForTarget(
                    state,
                    target,
                    projectCatalog,
                    sourceLookupIndex,
                    harmonyResolutionService);
            }

            return new EditorMethodInspectorPreparedView
            {
                Inspector = inspector,
                Invocation = invocation,
                Target = target,
                Document = BuildDocument(
                    state,
                    session,
                    inspector,
                    target,
                    relationshipsContext,
                    sourceHarmonyContext,
                    harmonySummary,
                    harmonyStatusMessage,
                    indirectHarmonyContext,
                    harmonyDisplayService ?? _fallbackHarmonyDisplayService,
                    showHarmony,
                    canCreatePatch,
                    hasPreparedPatch,
                    patchAvailabilityReason)
            };
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

        private HarmonyMethodPatchSummary TryLoadConditionalHarmonySummary(
            CortexShellState state,
            EditorCommandTarget target,
            EditorSourceHarmonyContext sourceHarmonyContext,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyInspectionService,
            HarmonyPatchResolutionService harmonyResolutionService,
            out string statusMessage)
        {
            statusMessage = string.Empty;
            if (state == null || harmonyInspectionService == null || projectCatalog == null)
            {
                return null;
            }

            HarmonyPatchInspectionRequest inspectionRequest = null;
            if (sourceHarmonyContext != null &&
                sourceHarmonyContext.IsPatchMethod &&
                sourceHarmonyContext.TargetInspectionRequest != null)
            {
                inspectionRequest = sourceHarmonyContext.TargetInspectionRequest;
            }
            else
            {
                if (target == null || harmonyResolutionService == null)
                {
                    return null;
                }

                HarmonyResolvedMethodTarget resolvedTarget;
                string resolutionReason;
                if (!harmonyResolutionService.TryResolveFromEditorTarget(state, sourceLookupIndex, projectCatalog, target, out resolvedTarget, out resolutionReason) ||
                    resolvedTarget == null ||
                    resolvedTarget.InspectionRequest == null)
                {
                    statusMessage = resolutionReason ?? string.Empty;
                    return null;
                }

                inspectionRequest = resolvedTarget.InspectionRequest;
            }

            string snapshotStatus;
            var summary = harmonyInspectionService.GetCachedSummary(
                state,
                inspectionRequest,
                loadedModCatalog,
                projectCatalog,
                true,
                out snapshotStatus);
            if (summary != null && summary.IsPatched)
            {
                statusMessage = !string.IsNullOrEmpty(snapshotStatus)
                    ? snapshotStatus
                    : "Loaded Harmony patch details for the resolved runtime method.";
                return summary;
            }

            statusMessage = sourceHarmonyContext != null && sourceHarmonyContext.IsPatchMethod
                ? "No live Harmony patches are registered for the patched runtime target."
                : "No live Harmony patches are registered for this method.";
            return null;
        }

        internal PanelDocument BuildDocument(
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
            var document = new PanelDocument();
            document.Title = "Method Info: " + (inspector != null ? inspector.Title ?? string.Empty : string.Empty);
            document.Subtitle = BuildHeaderSubtitle(target);

            var sections = new List<PanelSection>();
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
            document.Sections = sections.ToArray();
            return document;
        }

        private static PanelSection BuildStructureSection(CortexMethodInspectorState inspector, EditorCommandTarget target)
        {
            var section = new PanelSection();
            section.Id = "structure";
            section.Title = "Structure";
            section.Expanded = inspector != null ? inspector.OverviewExpanded : true;

            var elements = new List<PanelElement>();
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
                    elements.Add(new PanelSpacerElement { Height = 2f });
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

        private static PanelSection BuildRelationshipsSection(CortexMethodInspectorState inspector, EditorMethodRelationshipsContext relationshipsContext)
        {
            var section = new PanelSection();
            section.Id = "relationships";
            section.Title = "Relationships";
            section.Expanded = inspector != null ? inspector.RelationshipsExpanded : false;

            var elements = new List<PanelElement>();
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
                elements.Add(new PanelSpacerElement { Height = 4f });
                AppendRelationshipGroup(elements, "Used By", relationshipsContext.IncomingCalls, "No incoming callers were found for this method.");
            }

            section.Elements = elements.ToArray();
            return section;
        }

        private static void AppendRelationshipGroup(
            List<PanelElement> elements,
            string title,
            LanguageServiceCallHierarchyItem[] items,
            string emptyMessage)
        {
            elements.Add(CreateTextElement(title, string.Empty, false));
            var safeItems = items ?? new LanguageServiceCallHierarchyItem[0];
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
                var rows = new List<PanelMetadataElement>();
                rows.Add(CreateMetadataElement("Type", item.ContainingTypeName));
                rows.Add(CreateMetadataElement("Relationship", (item.Relationship ?? "Call") + " (" + item.CallCount + ")"));
                if (!string.IsNullOrEmpty(item.ContainingAssemblyName))
                {
                    rows.Add(CreateMetadataElement("Assembly", item.ContainingAssemblyName));
                }

                elements.Add(CreateCardElement(
                    item.SymbolDisplay ?? "Unknown",
                    rows.ToArray(),
                    string.Empty,
                    new PanelAction[0]));
            }

            if (safeItems.Length > rendered)
            {
                elements.Add(CreateTextElement(string.Empty, "Additional relationship entries are available beyond this preview.", false));
            }
        }

        private static PanelSection BuildSourceSection(CortexMethodInspectorState inspector, DocumentSession session, EditorCommandTarget target)
        {
            var section = new PanelSection();
            section.Id = "source";
            section.Title = "Source Context";
            section.Expanded = inspector != null ? inspector.NavigationExpanded : true;

            var elements = new List<PanelElement>();
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

        private PanelSection BuildHarmonySection(
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
            var section = new PanelSection();
            section.Id = "harmony";
            section.Title = "Harmony Context";
            section.Expanded = inspector != null ? inspector.HarmonyExpanded : true;

            var elements = new List<PanelElement>();
            var hasSourceHarmonyContext = AppendSourceHarmonyElements(elements, sourceHarmonyContext);
            if (hasSourceHarmonyContext)
            {
                elements.Add(new PanelSpacerElement { Height = 4f });
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
                        var rows = new List<PanelMetadataElement>();
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
                            new PanelAction[0]));
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
                elements.Add(new PanelSpacerElement { Height = 4f });
                elements.Add(CreateTextElement("Indirect Harmony", indirectHarmonyContext.StatusMessage, false));
                AppendIndirectHarmonyElements(elements, indirectHarmonyContext, harmonyDisplayService);
            }

            if (canCreatePatch || hasPreparedPatch)
            {
                elements.Add(new PanelSpacerElement { Height = 4f });
                elements.Add(CreateTextElement("Patch Creation", BuildPatchCreationStatus(state, canCreatePatch, hasPreparedPatch, patchAvailabilityReason), false));
                AppendPatchCreationElements(elements, state, canCreatePatch, hasPreparedPatch);
            }

            section.Elements = elements.ToArray();
            return section;
        }

        private static bool AppendSourceHarmonyElements(List<PanelElement> elements, EditorSourceHarmonyContext sourceHarmonyContext)
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

        private void AppendIndirectHarmonyElements(List<PanelElement> elements, EditorIndirectHarmonyContext indirectContext, HarmonyPatchDisplayService harmonyDisplayService)
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

                var rows = new List<PanelMetadataElement>();
                rows.Add(CreateMetadataElement("Type", caller.Caller.ContainingTypeName));
                rows.Add(CreateMetadataElement("Relationship", (caller.Caller.Relationship ?? "Incoming Call") + " (" + caller.Caller.CallCount + ")"));
                rows.Add(CreateMetadataElement("Patch Counts", harmonyDisplayService.BuildCountBreakdown(caller.Summary.Counts)));
                rows.Add(CreateMetadataElement("Owners", harmonyDisplayService.BuildOwnerSummary(caller.Summary), false));
                elements.Add(CreateCardElement(caller.Caller.SymbolDisplay ?? "Patched Caller", rows.ToArray(), string.Empty, new PanelAction[0]));
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

        private static void AppendPatchCreationElements(List<PanelElement> elements, CortexShellState state, bool canCreatePatch, bool hasPreparedPatch)
        {
            if (elements == null)
            {
                return;
            }

            elements.Add(CreateActionElement(
                new PanelAction
                {
                    Id = "patch:create:prefix",
                    Label = "Create Prefix Patch",
                    Enabled = canCreatePatch
                },
                "Generate a Prefix scaffold for the selected runtime method."));
            elements.Add(CreateActionElement(
                new PanelAction
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
                    new PanelMetadataElement[0],
                    insertionTarget.Reason ?? string.Empty,
                    new[]
                    {
                        new PanelAction
                        {
                            Id = "patch:open:" + i,
                            Label = "Open And Place Here",
                            Enabled = true
                        }
                    }));
            }
        }

        private static PanelMetadataElement CreateMetadataElement(string label, string value)
        {
            return CreateMetadataElement(label, value, true);
        }

        private static PanelMetadataElement CreateMetadataElement(string label, string value, bool drawDivider)
        {
            return new PanelMetadataElement
            {
                Label = label ?? string.Empty,
                Value = !string.IsNullOrEmpty(value) ? value : "Unknown",
                DrawDivider = drawDivider
            };
        }

        private static PanelTextElement CreateTextElement(string label, string value, bool monospace)
        {
            return new PanelTextElement
            {
                Label = label ?? string.Empty,
                Value = value ?? string.Empty,
                Monospace = monospace
            };
        }

        private static PanelActionElement CreateActionElement(PanelAction action, string hint)
        {
            return new PanelActionElement
            {
                Action = action ?? new PanelAction(),
                Hint = hint ?? string.Empty
            };
        }

        private static PanelCardElement CreateCardElement(string title, PanelMetadataElement[] rows, string body, PanelAction[] actions)
        {
            return new PanelCardElement
            {
                Title = title ?? string.Empty,
                Rows = rows ?? new PanelMetadataElement[0],
                Body = body ?? string.Empty,
                Actions = actions ?? new PanelAction[0]
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
