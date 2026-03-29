using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services.Harmony.Generation;
using Cortex.Services.Harmony.Inspection;
using Cortex.Services.Harmony.Presentation;
using Cortex.Services.Harmony.Resolution;
using Cortex.Services.Navigation;
using UnityEngine;
using Cortex.Services.Harmony.Workflow;

namespace Cortex.Modules.Harmony
{
    public sealed class HarmonyModule
    {
        private readonly HarmonyPatchWorkspaceService _workspaceService;
        private Vector2 _summaryScroll = Vector2.zero;
        private Vector2 _patchScroll = Vector2.zero;
        private Vector2 _previewScroll = Vector2.zero;

        public HarmonyModule()
            : this(null)
        {
        }

        internal HarmonyModule(HarmonyPatchWorkspaceService workspaceService)
        {
            _workspaceService = workspaceService ?? new HarmonyPatchWorkspaceService();
        }

        internal void Draw(
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            ICortexNavigationService navigationService,
            IDocumentService documentService,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            IPathInteractionService pathInteractionService,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService inspectionService,
            HarmonyPatchResolutionService resolutionService,
            HarmonyPatchDisplayService displayService,
            HarmonyPatchGenerationService generationService,
            GeneratedTemplateNavigationService templateNavigationService,
            CortexShellState state)
        {
            if (state == null || state.Harmony == null)
            {
                GUILayout.Label("Harmony state is not available.");
                return;
            }

            _workspaceService.EnsureSummary(state, loadedModCatalog, projectCatalog, inspectionService);

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            DrawHeader(navigationService, pathInteractionService, projectCatalog, loadedModCatalog, inspectionService, resolutionService, displayService, generationService, state);
            GUILayout.Space(6f);

            _summaryScroll = GUILayout.BeginScrollView(_summaryScroll, GUILayout.ExpandHeight(true));
            DrawSummary(displayService, state);
            GUILayout.Space(6f);
            DrawPatchList(navigationService, pathInteractionService, displayService, state);
            GUILayout.Space(6f);
            DrawOrderExplanation(state);
            GUILayout.Space(6f);
            DrawGeneration(
                documentService,
                projectCatalog,
                pathInteractionService,
                resolutionService,
                generationService,
                templateNavigationService,
                state);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawHeader(
            ICortexNavigationService navigationService,
            IPathInteractionService pathInteractionService,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            HarmonyPatchInspectionService inspectionService,
            HarmonyPatchResolutionService resolutionService,
            HarmonyPatchDisplayService displayService,
            HarmonyPatchGenerationService generationService,
            CortexShellState state)
        {
            var summary = state.Harmony.ActiveSummary;
            var runtimeLabel = inspectionService != null && inspectionService.IsAvailable ? "Runtime connected" : "Runtime unavailable";
            var hasTypeScope = HasActiveTypeScope(state);

            CortexIdeLayout.DrawGroup("Harmony", delegate
            {
                GUILayout.Label(runtimeLabel + "  |  " + (state.Harmony.SnapshotStatusMessage ?? "No Harmony snapshot loaded."));
                if (!string.IsNullOrEmpty(state.Harmony.ResolutionFailureReason))
                {
                    GUILayout.Label(state.Harmony.ResolutionFailureReason);
                }

                GUILayout.Space(4f);
                if (hasTypeScope)
                {
                    GUILayout.Label("Type scope: " + GetTypeScopeLabel(state) + ". Select a patched method below to inspect ordering or generate a patch.");
                }
                else
                {
                    var generationAvailabilityReason = _workspaceService.GetGenerationAvailabilityReason(state, projectCatalog, resolutionService, generationService);
                    if (!string.IsNullOrEmpty(generationAvailabilityReason))
                    {
                        GUILayout.Label(generationAvailabilityReason);
                    }
                }

                GUILayout.Space(4f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh", GUILayout.Width(88f)))
                {
                    _workspaceService.RefreshSummary(state, loadedModCatalog, projectCatalog, inspectionService);
                }

                GUI.enabled = summary != null && displayService != null;
                if (GUILayout.Button("Copy Summary", GUILayout.Width(118f)))
                {
                    GUIUtility.systemCopyBuffer = displayService.BuildPatchSummaryClipboardText(summary);
                    state.StatusMessage = "Copied Harmony patch summary.";
                }

                GUI.enabled = summary != null;
                if (GUILayout.Button("Navigate To Target", GUILayout.Width(136f)))
                {
                    OpenNavigationTarget(navigationService, state, summary != null ? summary.Target : null, "Opened Harmony target method.", "Could not open the Harmony target method.");
                }

                GUI.enabled = hasTypeScope && summary != null;
                if (GUILayout.Button("Back To Type", GUILayout.Width(118f)))
                {
                    _workspaceService.ReturnToTypeScope(state);
                }

                GUI.enabled = _workspaceService.CanBeginGeneration(state, projectCatalog, resolutionService, generationService);
                if (GUILayout.Button("Generate Prefix", GUILayout.Width(126f)))
                {
                    _workspaceService.BeginGeneration(state, projectCatalog, resolutionService, generationService, HarmonyPatchGenerationKind.Prefix);
                }

                if (GUILayout.Button("Generate Postfix", GUILayout.Width(132f)))
                {
                    _workspaceService.BeginGeneration(state, projectCatalog, resolutionService, generationService, HarmonyPatchGenerationKind.Postfix);
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            });
        }

        private void DrawSummary(HarmonyPatchDisplayService displayService, CortexShellState state)
        {
            var summary = state.Harmony.ActiveSummary;
            CortexIdeLayout.DrawGroup("Target Summary", delegate
            {
                if (summary == null && HasActiveTypeScope(state))
                {
                    DrawMetadataRow("Type", GetTypeScopeLabel(state));
                    DrawMetadataRow("Assembly", state.Harmony.ActiveTypeAssemblyPath);
                    DrawMetadataRow("Patched Methods", (state.Harmony.ActiveTypeSummaries != null ? state.Harmony.ActiveTypeSummaries.Length : 0).ToString());
                    DrawMetadataRow("Counts", BuildTypeCountBreakdown(displayService, state.Harmony.ActiveTypeSummaries));
                    return;
                }

                if (summary == null)
                {
                    GUILayout.Label("Select a resolvable method or click a Harmony badge to inspect live patch data.");
                    return;
                }

                DrawMetadataRow("Target", displayService != null ? displayService.BuildTargetDisplayName(summary) : summary.MethodName);
                DrawMetadataRow("Declaring Type", summary.DeclaringType);
                DrawMetadataRow("Signature", summary.Signature);
                DrawMetadataRow("Assembly", summary.AssemblyPath);
                DrawMetadataRow("Project/Mod", displayService != null ? displayService.BuildTargetOrigin(summary) : "Unknown");
                DrawMetadataRow("Document", !string.IsNullOrEmpty(summary.DocumentPath) ? summary.DocumentPath : summary.CachePath);
                DrawMetadataRow("Counts", displayService != null ? displayService.BuildCountBreakdown(summary.Counts) : (summary.Counts != null ? summary.Counts.TotalCount.ToString() : "0"));
                DrawMetadataRow("Owners", displayService != null ? displayService.BuildOwnerSummary(summary) : string.Join(", ", summary.Owners ?? new string[0]));
                DrawMetadataRow("Conflict Hint", !string.IsNullOrEmpty(summary.ConflictHint) ? summary.ConflictHint : "None");
                DrawMetadataRow("Captured", summary.CapturedUtc != DateTime.MinValue ? summary.CapturedUtc.ToLocalTime().ToString("g") : "Unknown");
            });
        }

        private void DrawPatchList(ICortexNavigationService navigationService, IPathInteractionService pathInteractionService, HarmonyPatchDisplayService displayService, CortexShellState state)
        {
            var summary = state.Harmony.ActiveSummary;
            CortexIdeLayout.DrawGroup("Patch List", delegate
            {
                if (summary == null && HasActiveTypeScope(state))
                {
                    DrawTypePatchList(state, displayService);
                    return;
                }

                if (summary == null || summary.Entries == null || summary.Entries.Length == 0)
                {
                    GUILayout.Label("No Harmony patches are registered for the active method.");
                    return;
                }

                _patchScroll = GUILayout.BeginScrollView(_patchScroll, GUI.skin.box, GUILayout.MinHeight(180f), GUILayout.Height(280f));
                foreach (HarmonyPatchKind patchKind in Enum.GetValues(typeof(HarmonyPatchKind)))
                {
                    var wroteHeader = false;
                    for (var i = 0; i < summary.Entries.Length; i++)
                    {
                        var entry = summary.Entries[i];
                        if (entry == null || entry.PatchKind != patchKind)
                        {
                            continue;
                        }

                        if (!wroteHeader)
                        {
                            GUILayout.Label((displayService != null ? displayService.GetPatchKindLabel(patchKind) : patchKind.ToString()) + " Patches", GUI.skin.box);
                            wroteHeader = true;
                        }

                        GUILayout.BeginVertical(GUI.skin.box);
                        DrawMetadataRow("Owner", !string.IsNullOrEmpty(entry.OwnerDisplayName) ? entry.OwnerDisplayName : entry.OwnerId);
                        DrawMetadataRow("Patch Method", BuildPatchMethodLabel(entry));
                        DrawMetadataRow("Priority", entry.Priority.ToString());
                        DrawMetadataRow("Before", JoinValues(entry.Before));
                        DrawMetadataRow("After", JoinValues(entry.After));
                        DrawMetadataRow("Index", entry.Index.ToString());
                        DrawMetadataRow("Association", BuildAssociationLabel(entry.OwnerAssociation));
                        if (entry.OwnerAssociation != null && !string.IsNullOrEmpty(entry.OwnerAssociation.MatchReason))
                        {
                            DrawMetadataRow("Match", entry.OwnerAssociation.MatchReason);
                        }

                        GUILayout.BeginHorizontal();
                        GUI.enabled = entry.NavigationTarget != null;
                        if (GUILayout.Button("Navigate To Patch Method", GUILayout.Width(172f)))
                        {
                            OpenNavigationTarget(navigationService, state, entry.NavigationTarget, "Opened Harmony patch method.", "Could not open the Harmony patch method.");
                        }

                        GUI.enabled = HasOwnerPath(entry.OwnerAssociation);
                        if (GUILayout.Button("Open Owning Mod/Project", GUILayout.Width(172f)))
                        {
                            OpenOwnerAssociation(pathInteractionService, entry.OwnerAssociation, state);
                        }

                        GUI.enabled = true;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                        GUILayout.Space(4f);
                    }
                }

                GUILayout.EndScrollView();
            });
        }

        private void DrawOrderExplanation(CortexShellState state)
        {
            var summary = state.Harmony.ActiveSummary;
            CortexIdeLayout.DrawGroup("Order Explanation", delegate
            {
                if (summary == null && HasActiveTypeScope(state))
                {
                    GUILayout.Label("Select a patched method from the type list to inspect execution ordering.");
                    return;
                }

                if (summary == null || summary.Order == null || summary.Order.Length == 0)
                {
                    GUILayout.Label("No ordering explanation is available for the active method.");
                    return;
                }

                for (var i = 0; i < summary.Order.Length; i++)
                {
                    var order = summary.Order[i];
                    if (order == null)
                    {
                        continue;
                    }

                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label(order.PatchKind.ToString());
                    if (!string.IsNullOrEmpty(order.Disclaimer))
                    {
                        GUILayout.Label(order.Disclaimer);
                    }

                    if (order.Items != null)
                    {
                        for (var itemIndex = 0; itemIndex < order.Items.Length; itemIndex++)
                        {
                            var item = order.Items[itemIndex];
                            if (item == null)
                            {
                                continue;
                            }

                            GUILayout.Label(
                                "#" + item.Position +
                                "  " + (item.OwnerId ?? string.Empty) +
                                "  P=" + item.Priority +
                                "  Index=" + item.Index +
                                "  " + (item.PatchMethodName ?? string.Empty));
                            if (!string.IsNullOrEmpty(item.Explanation))
                            {
                                GUILayout.Label(item.Explanation);
                            }
                            if ((item.Before != null && item.Before.Length > 0) || (item.After != null && item.After.Length > 0))
                            {
                                GUILayout.Label("Before: " + JoinValues(item.Before) + "  |  After: " + JoinValues(item.After));
                            }
                        }
                    }

                    GUILayout.EndVertical();
                    GUILayout.Space(4f);
                }
            });
        }

        private void DrawGeneration(
            IDocumentService documentService,
            IProjectCatalog projectCatalog,
            IPathInteractionService pathInteractionService,
            HarmonyPatchResolutionService resolutionService,
            HarmonyPatchGenerationService generationService,
            GeneratedTemplateNavigationService templateNavigationService,
            CortexShellState state)
        {
            CortexIdeLayout.DrawGroup("Generation", delegate
            {
                var request = state != null && state.Harmony != null
                    ? state.Harmony.GenerationRequest
                    : null;
                if (request == null && HasActiveTypeScope(state) && state.Harmony.ActiveSummary == null)
                {
                    GUILayout.Label("Select a specific patched method from the type list before generating a Harmony patch.");
                    return;
                }

                var generationAvailabilityReason = _workspaceService.GetGenerationAvailabilityReason(state, projectCatalog, resolutionService, generationService);
                request = state.Harmony.GenerationRequest;
                if (!string.IsNullOrEmpty(generationAvailabilityReason))
                {
                    GUILayout.Label(generationAvailabilityReason);
                    return;
                }

                if (request == null)
                {
                    GUILayout.Label("Choose Generate Prefix or Generate Postfix to build a Harmony patch scaffold.");
                    return;
                }

                var changed = false;
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(request.GenerationKind == HarmonyPatchGenerationKind.Prefix, "Prefix", GUI.skin.button, GUILayout.Width(92f)) &&
                    request.GenerationKind != HarmonyPatchGenerationKind.Prefix)
                {
                    _workspaceService.BeginGeneration(state, projectCatalog, resolutionService, generationService, HarmonyPatchGenerationKind.Prefix);
                    request = state.Harmony.GenerationRequest;
                    changed = true;
                }

                if (GUILayout.Toggle(request.GenerationKind == HarmonyPatchGenerationKind.Postfix, "Postfix", GUI.skin.button, GUILayout.Width(92f)) &&
                    request.GenerationKind != HarmonyPatchGenerationKind.Postfix)
                {
                    _workspaceService.BeginGeneration(state, projectCatalog, resolutionService, generationService, HarmonyPatchGenerationKind.Postfix);
                    request = state.Harmony.GenerationRequest;
                    changed = true;
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (request == null)
                {
                    return;
                }

                changed |= DrawInsertionTargets(state, request);
                changed |= DrawTextField("Namespace", ref request.NamespaceName);
                changed |= DrawTextField("Patch Class", ref request.PatchClassName);
                changed |= DrawTextField("Patch Method", ref request.PatchMethodName);
                changed |= DrawTextField("Harmony ID", ref request.HarmonyIdMetadata);

                GUILayout.Label("Destination File");
                var nextDestination = CortexPathField.DrawValueEditor(
                    "cortex.harmony.destination",
                    request.DestinationFilePath,
                    pathInteractionService,
                    new CortexPathFieldOptions
                    {
                        AllowBrowse = true,
                        AllowOpen = true,
                        AllowReveal = true,
                        AllowPaste = true,
                        AllowClear = true,
                        BrowseRequest = new PathSelectionRequest
                        {
                            SelectionKind = PathSelectionKind.OpenFile,
                            Title = "Select Harmony patch destination",
                            InitialPath = request.DestinationFilePath ?? string.Empty,
                            Filter = "C# files (*.cs)|*.cs|All files (*.*)|*.*"
                        }
                    },
                    GUILayout.ExpandWidth(true));
                if (!string.Equals(nextDestination, request.DestinationFilePath ?? string.Empty, StringComparison.Ordinal))
                {
                    request.DestinationFilePath = nextDestination ?? string.Empty;
                    changed = true;
                }

                GUILayout.Space(4f);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Editor Placement", GUILayout.Width(120f));
                if (GUILayout.Button(state.Harmony.IsInsertionPickActive ? "Cancel Pick" : "Pick In Editor", GUILayout.Width(120f)))
                {
                    if (state.Harmony.IsInsertionPickActive)
                    {
                        generationService.ClearEditorInsertionPick(state);
                        state.Harmony.GenerationStatusMessage = "Harmony editor insertion pick cancelled.";
                        state.StatusMessage = state.Harmony.GenerationStatusMessage;
                        MMLog.WriteInfo("[Cortex.Harmony] Editor insertion pick cancelled from the Harmony panel.");
                    }
                    else
                    {
                        generationService.ArmEditorInsertionPick(state);
                        MMLog.WriteInfo("[Cortex.Harmony] Editor insertion pick armed from the Harmony panel.");
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                if (state.Harmony.IsInsertionPickActive)
                {
                    GUILayout.Label("Click a writable source editor line to choose where the generated patch should be inserted.");
                }

                GUILayout.Space(4f);
                GUILayout.Label("Insertion Anchor");
                GUILayout.BeginHorizontal();
                changed |= DrawAnchorToggle(request, HarmonyPatchInsertionAnchorKind.EndOfFile, "End Of File");
                changed |= DrawAnchorToggle(request, HarmonyPatchInsertionAnchorKind.NamespaceOrClass, "Namespace/Class");
                changed |= DrawAnchorToggle(request, HarmonyPatchInsertionAnchorKind.ExplicitLine, "Explicit Line");
                if (request.InsertionAnchorKind == HarmonyPatchInsertionAnchorKind.SelectedContext || request.InsertionAbsolutePosition > 0)
                {
                    changed |= DrawAnchorToggle(request, HarmonyPatchInsertionAnchorKind.SelectedContext, "Picked Slot");
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (request.InsertionAnchorKind == HarmonyPatchInsertionAnchorKind.SelectedContext)
                {
                    GUILayout.Label(!string.IsNullOrEmpty(request.InsertionContextLabel)
                        ? request.InsertionContextLabel
                        : "The selected editor slot will place the generated patch after the enclosing type when clicked inside a class.");
                }

                if (request.InsertionAnchorKind == HarmonyPatchInsertionAnchorKind.ExplicitLine)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Line", GUILayout.Width(64f));
                    var lineText = GUILayout.TextField(Math.Max(1, request.InsertionLine).ToString(), GUILayout.Width(88f));
                    int parsedLine;
                    if (int.TryParse(lineText, out parsedLine))
                    {
                        parsedLine = Math.Max(1, parsedLine);
                        if (parsedLine != request.InsertionLine)
                        {
                            request.InsertionLine = parsedLine;
                            changed = true;
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(4f);
                GUILayout.Label("Optional Parameters");
                changed |= DrawToggle("Include __instance", ref request.IncludeInstanceParameter);
                changed |= DrawToggle("Include arguments", ref request.IncludeArgumentParameters);
                changed |= DrawToggle("Include __state", ref request.IncludeStateParameter);
                if (request.GenerationKind == HarmonyPatchGenerationKind.Prefix)
                {
                    changed |= DrawToggle("Use skip-original bool return", ref request.UseSkipOriginalPattern);
                }
                else
                {
                    changed |= DrawToggle("Include __result", ref request.IncludeResultParameter);
                }

                if (changed || state.Harmony.GenerationPreview == null)
                {
                    _workspaceService.RefreshGenerationPreview(state, projectCatalog, resolutionService, generationService);
                }

                GUILayout.Space(4f);
                GUILayout.Label(state.Harmony.GenerationStatusMessage ?? string.Empty);

                var preview = state.Harmony.GenerationPreview;
                GUILayout.Space(4f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh Preview", GUILayout.Width(120f)))
                {
                    _workspaceService.RefreshGenerationPreview(state, projectCatalog, resolutionService, generationService);
                    preview = state.Harmony.GenerationPreview;
                }

                GUI.enabled = preview != null && preview.CanApply;
                if (GUILayout.Button("Insert Patch", GUILayout.Width(108f)))
                {
                    string applyStatusMessage;
                    if (_workspaceService.ApplyGeneration(
                        state,
                        documentService,
                        projectCatalog,
                        resolutionService,
                        generationService,
                        templateNavigationService,
                        out applyStatusMessage))
                    {
                        state.StatusMessage = applyStatusMessage;
                    }
                    else if (!string.IsNullOrEmpty(applyStatusMessage))
                    {
                        state.StatusMessage = applyStatusMessage;
                    }
                    preview = state.Harmony.GenerationPreview;
                }
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(4f);
                _previewScroll = GUILayout.BeginScrollView(_previewScroll, GUI.skin.box, GUILayout.MinHeight(200f), GUILayout.Height(260f));
                GUILayout.TextArea(preview != null ? preview.PreviewText ?? string.Empty : string.Empty, GUILayout.ExpandHeight(true));
                GUILayout.EndScrollView();
            });
        }

        private bool DrawInsertionTargets(CortexShellState state, HarmonyPatchGenerationRequest request)
        {
            if (state == null || state.Harmony == null || state.Harmony.InsertionTargets == null || state.Harmony.InsertionTargets.Count == 0)
            {
                return false;
            }

            var changed = false;
            GUILayout.Label("Suggested Destinations");
            for (var i = 0; i < state.Harmony.InsertionTargets.Count; i++)
            {
                var target = state.Harmony.InsertionTargets[i];
                if (target == null)
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(target.DisplayName ?? target.FilePath ?? string.Empty, GUILayout.Width(220f)))
                {
                    request.DestinationFilePath = target.FilePath ?? string.Empty;
                    request.InsertionAnchorKind = target.DefaultAnchorKind;
                    request.InsertionLine = target.SuggestedLine;
                    request.InsertionAbsolutePosition = target.SuggestedAbsolutePosition;
                    request.InsertionContextLabel = target.SuggestedContextLabel ?? string.Empty;
                    changed = true;
                }

                GUILayout.Label(target.Reason ?? string.Empty);
                GUILayout.EndHorizontal();
            }

            return changed;
        }

        private void DrawTypePatchList(CortexShellState state, HarmonyPatchDisplayService displayService)
        {
            var summaries = state != null && state.Harmony != null
                ? state.Harmony.ActiveTypeSummaries ?? new HarmonyMethodPatchSummary[0]
                : new HarmonyMethodPatchSummary[0];
            if (summaries.Length == 0)
            {
                GUILayout.Label("No live Harmony patches are registered for methods in this type.");
                return;
            }

            _patchScroll = GUILayout.BeginScrollView(_patchScroll, GUI.skin.box, GUILayout.MinHeight(180f), GUILayout.Height(280f));
            for (var i = 0; i < summaries.Length; i++)
            {
                var current = summaries[i];
                if (current == null)
                {
                    continue;
                }

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(displayService != null ? displayService.BuildTargetDisplayName(current) : (current.MethodName ?? string.Empty));
                DrawMetadataRow("Counts", displayService != null ? displayService.BuildCountBreakdown(current.Counts) : (current.Counts != null ? current.Counts.TotalCount.ToString() : "0"));
                DrawMetadataRow("Owners", displayService != null ? displayService.BuildOwnerSummary(current) : string.Join(", ", current.Owners ?? new string[0]));
                DrawMetadataRow("Conflict Hint", !string.IsNullOrEmpty(current.ConflictHint) ? current.ConflictHint : "None");
                if (GUILayout.Button("Inspect Method", GUILayout.Width(132f)))
                {
                    _workspaceService.ActivateMethodSummary(state, current);
                }
                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }

            GUILayout.EndScrollView();
        }

        private static bool HasActiveTypeScope(CortexShellState state)
        {
            return state != null &&
                state.Harmony != null &&
                !string.IsNullOrEmpty(state.Harmony.ActiveTypeName);
        }

        private static string GetTypeScopeLabel(CortexShellState state)
        {
            if (!HasActiveTypeScope(state))
            {
                return string.Empty;
            }

            return !string.IsNullOrEmpty(state.Harmony.ActiveTypeDisplayName)
                ? state.Harmony.ActiveTypeDisplayName
                : state.Harmony.ActiveTypeName;
        }

        private static string BuildTypeCountBreakdown(HarmonyPatchDisplayService displayService, HarmonyMethodPatchSummary[] summaries)
        {
            var counts = new HarmonyPatchCounts();
            if (summaries != null)
            {
                for (var i = 0; i < summaries.Length; i++)
                {
                    var current = summaries[i];
                    if (current == null || current.Counts == null)
                    {
                        continue;
                    }

                    counts.PrefixCount += current.Counts.PrefixCount;
                    counts.PostfixCount += current.Counts.PostfixCount;
                    counts.TranspilerCount += current.Counts.TranspilerCount;
                    counts.FinalizerCount += current.Counts.FinalizerCount;
                    counts.InnerPrefixCount += current.Counts.InnerPrefixCount;
                    counts.InnerPostfixCount += current.Counts.InnerPostfixCount;
                    counts.TotalCount += current.Counts.TotalCount;
                }
            }

            return displayService != null
                ? displayService.BuildCountBreakdown(counts)
                : counts.TotalCount.ToString();
        }

        private static void OpenNavigationTarget(ICortexNavigationService navigationService, CortexShellState state, HarmonyPatchNavigationTarget target, string successMessage, string failureMessage)
        {
            if (navigationService == null || state == null || target == null)
            {
                if (state != null)
                {
                    state.StatusMessage = failureMessage;
                }
                return;
            }

            if (!string.IsNullOrEmpty(target.DocumentPath) && File.Exists(target.DocumentPath))
            {
                if (!CortexModuleUtil.IsDecompilerDocumentPath(state, target.DocumentPath))
                {
                    if (navigationService.OpenDocument(state, target.DocumentPath, target.Line > 0 ? target.Line : 1, successMessage, failureMessage) != null)
                    {
                        return;
                    }
                }
            }

            if (!string.IsNullOrEmpty(target.AssemblyPath) && target.MetadataToken > 0)
            {
                if (navigationService.DecompileAndOpen(state, target.AssemblyPath, target.MetadataToken, DecompilerEntityKind.Method, false, successMessage, failureMessage))
                {
                    return;
                }
            }

            if (!string.IsNullOrEmpty(target.CachePath) && File.Exists(target.CachePath))
            {
                if (navigationService.OpenDocument(state, target.CachePath, target.Line > 0 ? target.Line : 1, successMessage, failureMessage) != null)
                {
                    return;
                }
            }

            if (!string.IsNullOrEmpty(target.DocumentPath) && File.Exists(target.DocumentPath))
            {
                if (navigationService.OpenDocument(state, target.DocumentPath, target.Line > 0 ? target.Line : 1, successMessage, failureMessage) != null)
                {
                    return;
                }
            }

            state.StatusMessage = failureMessage;
        }

        private static void OpenOwnerAssociation(IPathInteractionService pathInteractionService, HarmonyPatchOwnerAssociation association, CortexShellState state)
        {
            var path = association != null && !string.IsNullOrEmpty(association.ProjectSourceRootPath)
                ? association.ProjectSourceRootPath
                : (association != null ? association.LoadedModRootPath : string.Empty);
            if (pathInteractionService == null || string.IsNullOrEmpty(path) || !pathInteractionService.TryOpenPath(path))
            {
                if (state != null)
                {
                    state.StatusMessage = "Could not open the owning mod or project path.";
                }
                return;
            }

            if (state != null)
            {
                state.StatusMessage = "Opened owning mod/project path.";
            }
        }

        private static bool HasOwnerPath(HarmonyPatchOwnerAssociation association)
        {
            return association != null &&
                (!string.IsNullOrEmpty(association.ProjectSourceRootPath) || !string.IsNullOrEmpty(association.LoadedModRootPath));
        }

        private static bool DrawTextField(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100f));
            var nextValue = GUILayout.TextField(value ?? string.Empty, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            if (string.Equals(nextValue, value ?? string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            value = nextValue;
            return true;
        }

        private static bool DrawToggle(string label, ref bool value)
        {
            var nextValue = GUILayout.Toggle(value, label);
            if (nextValue == value)
            {
                return false;
            }

            value = nextValue;
            return true;
        }

        private static bool DrawAnchorToggle(HarmonyPatchGenerationRequest request, HarmonyPatchInsertionAnchorKind anchorKind, string label)
        {
            var selected = request != null && request.InsertionAnchorKind == anchorKind;
            if (!GUILayout.Toggle(selected, label, GUI.skin.button, GUILayout.Width(138f)) || selected)
            {
                return false;
            }

            request.InsertionAnchorKind = anchorKind;
            return true;
        }

        private static void DrawMetadataRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(112f));
            GUILayout.Label(string.IsNullOrEmpty(value) ? "Unknown" : value, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        private static string JoinValues(string[] values)
        {
            return values != null && values.Length > 0 ? string.Join(", ", values) : "None";
        }

        private static string BuildPatchMethodLabel(HarmonyPatchEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return (entry.PatchMethodDeclaringType ?? string.Empty) + "." + (entry.PatchMethodName ?? string.Empty) + (entry.PatchMethodSignature ?? string.Empty);
        }

        private static string BuildAssociationLabel(HarmonyPatchOwnerAssociation association)
        {
            if (association == null || !association.HasMatch)
            {
                return "No resolved mod/project association";
            }

            if (!string.IsNullOrEmpty(association.ProjectModId))
            {
                return "Project: " + association.ProjectModId;
            }

            if (!string.IsNullOrEmpty(association.LoadedModId))
            {
                return "Loaded Mod: " + association.LoadedModId;
            }

            return association.DisplayName ?? association.OwnerId ?? "Resolved owner";
        }
    }
}
