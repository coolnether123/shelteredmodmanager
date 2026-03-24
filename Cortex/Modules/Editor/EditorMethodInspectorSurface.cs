using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Rendering.Models;
using Cortex.Renderers.Imgui;
using Cortex.Services;
using UnityEngine;

namespace Cortex.Modules.Editor
{
    internal sealed class EditorMethodInspectorSurface
    {
        private const float PreferredPanelWidth = 430f;
        private const float MinimumPanelWidth = 320f;
        private const float PreferredPanelHeight = 520f;
        private const float MinimumPanelHeight = 260f;
        private const float PopupMargin = 8f;
        private const float PopupGap = 12f;
        private const int SnippetContextLineCount = 2;

        private readonly EditorMethodInspectorService _inspectorService = new EditorMethodInspectorService();
        private readonly EditorMethodHarmonyContextService _harmonyContextService = new EditorMethodHarmonyContextService();
        private readonly EditorMethodTargetMetadataService _targetMetadataService = new EditorMethodTargetMetadataService();
        private readonly EditorMethodPatchCreationService _patchCreationService = new EditorMethodPatchCreationService();
        private readonly HarmonyPatchDisplayService _fallbackHarmonyDisplayService = new HarmonyPatchDisplayService();
        private readonly ImguiPanelRenderer _panelRenderer = new ImguiPanelRenderer();
        private const float ScrollWheelStep = 28f;

        private Vector2 _scroll = Vector2.zero;

        public Rect Draw(
            CortexShellState state,
            DocumentSession session,
            string activeDocumentPath,
            Rect anchorRect,
            Vector2 surfaceSize,
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
            HarmonyPatchGenerationService harmonyGenerationService)
        {
            if (!_inspectorService.IsVisibleForDocument(state, activeDocumentPath))
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            var inspector = state != null && state.Editor != null ? state.Editor.MethodInspector : null;
            var invocation = inspector != null ? inspector.Invocation : null;
            var target = invocation != null ? invocation.Target : null;
            if (target == null)
            {
                _inspectorService.Close(state);
                return new Rect(0f, 0f, 0f, 0f);
            }

            var popupRect = ResolvePanelRect(anchorRect, surfaceSize);
            _inspectorService.EnsureCallHierarchyRequest(state);
            _targetMetadataService.EnsureSymbolContextRequest(state, target);
            _targetMetadataService.Enrich(target, session, state);

            var sourceHarmonyContext = _harmonyContextService.BuildSourcePatchContext(
                state,
                target,
                projectCatalog,
                harmonyResolutionService);

            HarmonyMethodPatchSummary harmonySummary;
            string harmonyStatusMessage;
            TryLoadHarmonySummary(
                state,
                target,
                sourceHarmonyContext,
                projectCatalog,
                loadedModCatalog,
                sourceLookupIndex,
                harmonyInspectionService,
                harmonyResolutionService,
                out harmonySummary,
                out harmonyStatusMessage);

            var indirectHarmonyContext = _harmonyContextService.BuildIndirectContext(
                state,
                inspector,
                loadedModCatalog,
                projectCatalog,
                sourceLookupIndex,
                harmonyInspectionService,
                harmonyResolutionService);

            string patchAvailabilityReason;
            var canCreatePatch = _patchCreationService.CanPreparePatch(
                state,
                target,
                projectCatalog,
                sourceLookupIndex,
                harmonyResolutionService,
                harmonyGenerationService,
                out patchAvailabilityReason);
            var hasPreparedPatch = _patchCreationService.IsPreparedForTarget(
                state,
                target,
                projectCatalog,
                sourceLookupIndex,
                harmonyResolutionService);

            var document = BuildDocument(
                state,
                session,
                inspector,
                target,
                sourceHarmonyContext,
                harmonySummary,
                harmonyStatusMessage,
                indirectHarmonyContext,
                harmonyDisplayService ?? _fallbackHarmonyDisplayService,
                canCreatePatch,
                hasPreparedPatch,
                patchAvailabilityReason);
            var renderResult = _panelRenderer.Draw(popupRect, document, _scroll, BuildTheme());
            _scroll = renderResult != null ? renderResult.Scroll : _scroll;
            HandleActivation(
                renderResult != null ? renderResult.ActivatedId : string.Empty,
                state,
                inspector,
                invocation,
                commandRegistry,
                documentService,
                projectCatalog,
                sourceLookupIndex,
                harmonyResolutionService,
                harmonyGenerationService);
            return popupRect;
        }

        public Rect PredictRect(CortexShellState state, string activeDocumentPath, Rect anchorRect, Vector2 surfaceSize)
        {
            if (!_inspectorService.IsVisibleForDocument(state, activeDocumentPath))
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            return ResolvePanelRect(anchorRect, surfaceSize);
        }

        public bool TryHandlePreDrawInput(Event current, Rect panelRect, Vector2 localPointer)
        {
            if (current == null ||
                panelRect.width <= 0f ||
                panelRect.height <= 0f ||
                !panelRect.Contains(localPointer))
            {
                return false;
            }

            if (current.type == EventType.ScrollWheel)
            {
                _scroll.y = Mathf.Max(0f, _scroll.y + (current.delta.y * ScrollWheelStep));
                current.Use();
            }

            return current.type == EventType.MouseDown ||
                current.type == EventType.MouseUp ||
                current.type == EventType.MouseDrag ||
                current.type == EventType.ScrollWheel ||
                current.type == EventType.ContextClick;
        }

        private PanelDocument BuildDocument(
            CortexShellState state,
            DocumentSession session,
            CortexMethodInspectorState inspector,
            EditorCommandTarget target,
            EditorSourceHarmonyContext sourceHarmonyContext,
            HarmonyMethodPatchSummary harmonySummary,
            string harmonyStatusMessage,
            EditorIndirectHarmonyContext indirectHarmonyContext,
            HarmonyPatchDisplayService harmonyDisplayService,
            bool canCreatePatch,
            bool hasPreparedPatch,
            string patchAvailabilityReason)
        {
            var document = new PanelDocument();
            document.Title = "Method Info: " + (inspector != null ? inspector.Title ?? string.Empty : string.Empty);
            document.Subtitle = BuildHeaderSubtitle(target);

            var sections = new List<PanelSection>();
            sections.Add(BuildStructureSection(inspector, target));
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
            sections.Add(BuildSourceSection(inspector, session, target));
            document.Sections = sections.ToArray();
            return document;
        }

        private PanelSection BuildStructureSection(CortexMethodInspectorState inspector, EditorCommandTarget target)
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

        private PanelSection BuildSourceSection(CortexMethodInspectorState inspector, DocumentSession session, EditorCommandTarget target)
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
            else if (!hasSourceHarmonyContext)
            {
                elements.Add(CreateTextElement(string.Empty, !string.IsNullOrEmpty(harmonyStatusMessage) ? harmonyStatusMessage : "No Harmony patch details are available for this method.", false));
            }

            elements.Add(new PanelSpacerElement { Height = 4f });
            elements.Add(CreateTextElement("Indirect Harmony Context", BuildIndirectStatus(indirectHarmonyContext), false));
            AppendIndirectHarmonyElements(elements, indirectHarmonyContext, harmonyDisplayService);

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

        private static string BuildIndirectStatus(EditorIndirectHarmonyContext indirectContext)
        {
            return indirectContext != null
                ? indirectContext.StatusMessage
                : "Indirect Harmony context is not available for this method.";
        }

        private void AppendIndirectHarmonyElements(List<PanelElement> elements, EditorIndirectHarmonyContext indirectContext, HarmonyPatchDisplayService harmonyDisplayService)
        {
            if (elements == null || indirectContext == null || indirectContext.IsLoading)
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

        private void AppendPatchCreationElements(List<PanelElement> elements, CortexShellState state, bool canCreatePatch, bool hasPreparedPatch)
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

        private void HandleActivation(
            string activatedId,
            CortexShellState state,
            CortexMethodInspectorState inspector,
            EditorCommandInvocation invocation,
            ICommandRegistry commandRegistry,
            IDocumentService documentService,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchGenerationService harmonyGenerationService)
        {
            if (string.IsNullOrEmpty(activatedId))
            {
                return;
            }

            if (string.Equals(activatedId, "close", StringComparison.Ordinal))
            {
                MMLog.WriteInfo("[Cortex.Overlay] Method inspector closed. Reason='close-button'.");
                _inspectorService.Close(state);
                return;
            }

            if (activatedId.StartsWith("section:", StringComparison.Ordinal))
            {
                ToggleSection(inspector, activatedId.Substring("section:".Length));
                return;
            }

            if (invocation == null || invocation.Target == null)
            {
                return;
            }

            if (string.Equals(activatedId, "patch:create:prefix", StringComparison.Ordinal))
            {
                PreparePatch(state, invocation.Target, projectCatalog, sourceLookupIndex, harmonyResolutionService, harmonyGenerationService, HarmonyPatchGenerationKind.Prefix);
                return;
            }

            if (string.Equals(activatedId, "patch:create:postfix", StringComparison.Ordinal))
            {
                PreparePatch(state, invocation.Target, projectCatalog, sourceLookupIndex, harmonyResolutionService, harmonyGenerationService, HarmonyPatchGenerationKind.Postfix);
                return;
            }

            if (activatedId.StartsWith("patch:open:", StringComparison.Ordinal))
            {
                var indexText = activatedId.Substring("patch:open:".Length);
                int index;
                if (int.TryParse(indexText, out index))
                {
                    OpenInsertionTarget(state, documentService, harmonyGenerationService, index);
                }
            }
        }

        private void PreparePatch(
            CortexShellState state,
            EditorCommandTarget target,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchGenerationService harmonyGenerationService,
            HarmonyPatchGenerationKind kind)
        {
            string statusMessage;
            if (_patchCreationService.PreparePatch(
                state,
                target,
                projectCatalog,
                sourceLookupIndex,
                harmonyResolutionService,
                harmonyGenerationService,
                kind,
                out statusMessage))
            {
                state.StatusMessage = statusMessage;
            }
            else if (!string.IsNullOrEmpty(statusMessage))
            {
                state.StatusMessage = statusMessage;
            }
        }

        private void OpenInsertionTarget(CortexShellState state, IDocumentService documentService, HarmonyPatchGenerationService harmonyGenerationService, int index)
        {
            if (state == null || state.Harmony == null || state.Harmony.InsertionTargets == null || index < 0 || index >= state.Harmony.InsertionTargets.Count)
            {
                return;
            }

            var insertionTarget = state.Harmony.InsertionTargets[index];
            if (insertionTarget == null)
            {
                return;
            }

            string statusMessage;
            if (_patchCreationService.OpenInsertionTarget(state, documentService, harmonyGenerationService, insertionTarget, out statusMessage))
            {
                _inspectorService.Close(state);
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                state.StatusMessage = statusMessage;
            }
        }

        private static void ToggleSection(CortexMethodInspectorState inspector, string sectionId)
        {
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
                    inspector.ReferencesExpanded = !inspector.ReferencesExpanded;
                    break;
                case "harmony":
                    inspector.HarmonyExpanded = !inspector.HarmonyExpanded;
                    break;
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

        private static Rect ResolvePanelRect(Rect anchorRect, Vector2 viewportSize)
        {
            var panelSize = ResolvePanelSize(viewportSize);
            var panelRect = new Rect(
                anchorRect.xMax + PopupGap,
                anchorRect.y - 2f,
                panelSize.x,
                panelSize.y);

            if (viewportSize.x <= 0f || viewportSize.y <= 0f)
            {
                return panelRect;
            }

            var maxX = Mathf.Max(PopupMargin, viewportSize.x - panelRect.width - PopupMargin);
            var maxY = Mathf.Max(PopupMargin, viewportSize.y - panelRect.height - PopupMargin);
            if (panelRect.x > maxX)
            {
                var leftX = anchorRect.x - panelRect.width - PopupGap;
                panelRect.x = leftX >= PopupMargin ? leftX : maxX;
            }

            panelRect.x = Mathf.Max(PopupMargin, panelRect.x);
            panelRect.y = Mathf.Clamp(panelRect.y, PopupMargin, maxY);
            return panelRect;
        }

        private static Vector2 ResolvePanelSize(Vector2 viewportSize)
        {
            if (viewportSize.x <= 0f || viewportSize.y <= 0f)
            {
                return new Vector2(PreferredPanelWidth, PreferredPanelHeight);
            }

            var availableWidth = Mathf.Max(180f, viewportSize.x - (PopupMargin * 2f));
            var availableHeight = Mathf.Max(MinimumPanelHeight, viewportSize.y - (PopupMargin * 2f));
            var width = availableWidth < MinimumPanelWidth
                ? availableWidth
                : Mathf.Min(PreferredPanelWidth, availableWidth);
            var height = availableHeight < MinimumPanelHeight
                ? availableHeight
                : Mathf.Min(PreferredPanelHeight, availableHeight);
            return new Vector2(width, height);
        }

        private static ImguiPanelTheme BuildTheme()
        {
            var borderColor = CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetBorderColor(), 0.38f);
            return new ImguiPanelTheme
            {
                BackgroundColor = CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetBackgroundColor(), 0.22f),
                HeaderColor = CortexIdeLayout.Blend(CortexIdeLayout.GetHeaderColor(), CortexIdeLayout.GetSurfaceColor(), 0.18f),
                BorderColor = borderColor,
                DividerColor = CortexIdeLayout.WithAlpha(CortexIdeLayout.Blend(borderColor, CortexIdeLayout.GetTextColor(), 0.1f), 0.46f),
                ActionFillColor = CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.72f),
                ActionActiveFillColor = CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetHeaderColor(), 0.34f),
                CardFillColor = CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.52f),
                TextColor = CortexIdeLayout.GetTextColor(),
                MutedTextColor = CortexIdeLayout.GetMutedTextColor(),
                AccentColor = CortexIdeLayout.GetAccentColor(),
                WarningColor = CortexIdeLayout.Blend(CortexIdeLayout.GetWarningColor(), CortexIdeLayout.GetTextColor(), 0.42f)
            };
        }

        private void TryLoadHarmonySummary(
            CortexShellState state,
            EditorCommandTarget target,
            EditorSourceHarmonyContext sourceHarmonyContext,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyInspectionService,
            HarmonyPatchResolutionService harmonyResolutionService,
            out HarmonyMethodPatchSummary summary,
            out string statusMessage)
        {
            summary = null;
            statusMessage = string.Empty;
            if (state == null ||
                harmonyInspectionService == null ||
                projectCatalog == null)
            {
                return;
            }

            HarmonyPatchInspectionRequest inspectionRequest = null;
            if (sourceHarmonyContext != null &&
                sourceHarmonyContext.IsPatchMethod &&
                sourceHarmonyContext.TargetInspectionRequest != null)
            {
                inspectionRequest = sourceHarmonyContext.TargetInspectionRequest;
                statusMessage = "Loaded Harmony target context for the patched runtime method.";
            }
            else
            {
                if (target == null || harmonyResolutionService == null)
                {
                    return;
                }

                HarmonyResolvedMethodTarget resolvedTarget;
                string resolutionReason;
                if (!harmonyResolutionService.TryResolveFromEditorTarget(state, sourceLookupIndex, projectCatalog, target, out resolvedTarget, out resolutionReason) ||
                    resolvedTarget == null ||
                    resolvedTarget.InspectionRequest == null)
                {
                    statusMessage = resolutionReason;
                    return;
                }

                inspectionRequest = resolvedTarget.InspectionRequest;
            }

            summary = harmonyInspectionService.GetSummary(
                state,
                inspectionRequest,
                loadedModCatalog,
                projectCatalog,
                false,
                out statusMessage);
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
