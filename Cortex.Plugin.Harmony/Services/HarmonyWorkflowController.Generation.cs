using System;
using System.IO;
using System.Reflection;
using Cortex.Core.Models;
using Cortex.Plugin.Harmony.Services.Resolution;
using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    internal sealed partial class HarmonyWorkflowController
    {
        public bool PrepareGeneration(
            IWorkbenchModuleRuntime runtime,
            EditorCommandTarget target,
            HarmonyPatchGenerationKind generationKind,
            out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            if (!IsRuntimeAvailable || runtime == null)
            {
                return false;
            }

            statusMessage = "Harmony patch generation is not available for the selected method.";

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            if (!_resolver.TryResolveMethod(runtime, target, out resolvedTarget, out reason) ||
                resolvedTarget == null ||
                !TryValidateGenerationTarget(resolvedTarget, out reason))
            {
                statusMessage = reason ?? statusMessage;
                return false;
            }

            LoadMethodSummary(runtime, resolvedTarget, false, out reason);
            return BeginGeneration(runtime, resolvedTarget, generationKind, out statusMessage);
        }

        public bool BeginGeneration(IWorkbenchModuleRuntime runtime, HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationKind generationKind, out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            var workflow = _stateStore.GetWorkflow(runtime);
            if (!IsRuntimeAvailable || runtime == null || workflow == null || resolvedTarget == null)
            {
                return false;
            }

            statusMessage = "Harmony patch generation is not available for the selected method.";

            var request = CreateDefaultRequest(resolvedTarget, generationKind);
            var insertionTargets = _insertionService.BuildInsertionTargets(runtime, resolvedTarget, request);
            workflow.GenerationRequest = request;
            workflow.GenerationPreview = null;
            workflow.GenerationStatusMessage = string.Empty;
            workflow.InsertionTargets.Clear();
            for (var i = 0; i < insertionTargets.Length; i++)
            {
                workflow.InsertionTargets.Add(insertionTargets[i]);
            }

            if (insertionTargets.Length > 0)
            {
                request.DestinationFilePath = insertionTargets[0].FilePath ?? string.Empty;
                request.InsertionAnchorKind = insertionTargets[0].DefaultAnchorKind;
                request.InsertionLine = insertionTargets[0].SuggestedLine;
                request.InsertionAbsolutePosition = insertionTargets[0].SuggestedAbsolutePosition;
                request.InsertionContextLabel = insertionTargets[0].SuggestedContextLabel ?? string.Empty;
            }

            workflow.IsInsertionSelectionActive = true;
            return RefreshGenerationPreview(runtime, out statusMessage);
        }

        public bool RefreshGenerationPreview(IWorkbenchModuleRuntime runtime, out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            var workflow = _stateStore.GetWorkflow(runtime);
            if (!IsRuntimeAvailable || runtime == null || workflow == null || workflow.GenerationRequest == null)
            {
                return false;
            }

            statusMessage = "Harmony patch preview is not available.";

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            if (!_resolver.TryResolveMethod(runtime, workflow.ActiveInspectionRequest, out resolvedTarget, out reason) ||
                resolvedTarget == null ||
                !TryValidateGenerationTarget(resolvedTarget, out reason))
            {
                workflow.GenerationStatusMessage = reason ?? statusMessage;
                statusMessage = workflow.GenerationStatusMessage;
                return false;
            }

            var snippetPreview = _templateService.BuildSnippet(resolvedTarget, workflow.GenerationRequest);
            workflow.GenerationPreview = _insertionService.BuildPreview(runtime, workflow.GenerationRequest, snippetPreview);
            workflow.GenerationStatusMessage = workflow.GenerationPreview != null
                ? workflow.GenerationPreview.StatusMessage ?? string.Empty
                : statusMessage;
            statusMessage = workflow.GenerationStatusMessage;
            return workflow.GenerationPreview != null && workflow.GenerationPreview.CanApply;
        }

        public bool ApplyGeneration(IWorkbenchModuleRuntime runtime, out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            var workflow = _stateStore.GetWorkflow(runtime);
            if (!IsRuntimeAvailable || runtime == null || workflow == null || workflow.GenerationRequest == null)
            {
                return false;
            }

            statusMessage = "Harmony patch preview is not ready to apply.";

            RefreshGenerationPreview(runtime, out statusMessage);
            if (workflow.GenerationPreview == null || !workflow.GenerationPreview.CanApply)
            {
                return false;
            }

            DocumentSession session;
            if (!_insertionService.ApplyPreview(runtime, workflow.GenerationRequest, workflow.GenerationPreview, out session, out statusMessage))
            {
                return false;
            }

            if (session != null)
            {
                _templateNavigationService.StartSession(
                    runtime,
                    session,
                    workflow.GenerationPreview.Placeholders,
                    workflow.GenerationPreview.InsertionOffset,
                    workflow.GenerationPreview.InsertionOffset + ((workflow.GenerationPreview.SnippetText ?? string.Empty).Length));
            }

            workflow.IsInsertionSelectionActive = false;
            return true;
        }

        public bool IsInsertionSelectionActive(IWorkbenchModuleRuntime runtime)
        {
            if (!IsRuntimeAvailable)
            {
                return false;
            }

            var workflow = _stateStore.GetWorkflow(runtime);
            return workflow != null && workflow.IsInsertionSelectionActive;
        }

        public bool HasTemplateSession(IWorkbenchModuleRuntime runtime, DocumentSession session)
        {
            return IsRuntimeAvailable && _templateNavigationService.HasActiveSession(runtime, session);
        }

        public void SynchronizeInsertionSelection(WorkbenchEditorWorkflowContext context)
        {
            if (context != null && context.Runtime != null && context.Runtime.Feedback != null)
            {
                context.Runtime.Feedback.SetStatusMessage(
                    IsRuntimeAvailable
                        ? "Click a writable editor line to place the Harmony patch. Press Escape to cancel."
                        : GetUnavailableMessage());
            }
        }

        public void SynchronizeTemplateSession(WorkbenchEditorWorkflowContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return;
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            if (runtime != null)
            {
                _templateNavigationService.SyncSession(runtime, context != null ? context.Session : null);
            }
        }

        public WorkbenchEditorWorkflowResult HandleTemplateKeyboard(WorkbenchEditorKeyboardContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new WorkbenchEditorWorkflowResult();
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            if (runtime == null ||
                context == null ||
                context.Session == null ||
                context.Key != WorkbenchEditorInteractionKey.Tab ||
                context.Control ||
                context.Alt ||
                !_templateNavigationService.TryHandleNavigation(runtime, context.Session, context.Shift))
            {
                return new WorkbenchEditorWorkflowResult();
            }

            return new WorkbenchEditorWorkflowResult
            {
                Handled = true,
                ConsumeInput = true
            };
        }

        public WorkbenchEditorWorkflowResult HandleInsertionPointer(WorkbenchEditorPointerContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new WorkbenchEditorWorkflowResult();
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            var workflow = _stateStore.GetWorkflow(runtime);
            if (context == null || runtime == null || workflow == null || !workflow.IsInsertionSelectionActive)
            {
                return new WorkbenchEditorWorkflowResult();
            }

            string statusMessage;
            if (!_insertionService.TryApplyEditorInsertionSelection(
                runtime,
                context.EditorContext,
                context.Session,
                context.LineNumber,
                context.AbsolutePosition,
                out statusMessage))
            {
                SetStatus(context.Runtime, statusMessage);
            }
            else
            {
                RefreshGenerationPreview(runtime, out statusMessage);
                SetStatus(context.Runtime, statusMessage);
            }

            return new WorkbenchEditorWorkflowResult
            {
                Handled = true,
                ConsumeInput = true
            };
        }

        public WorkbenchEditorWorkflowResult HandleInsertionKeyboard(WorkbenchEditorKeyboardContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new WorkbenchEditorWorkflowResult();
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            var workflow = _stateStore.GetWorkflow(runtime);
            if (context == null || runtime == null || workflow == null || !workflow.IsInsertionSelectionActive)
            {
                return new WorkbenchEditorWorkflowResult();
            }

            if (context.Key == WorkbenchEditorInteractionKey.Escape)
            {
                workflow.IsInsertionSelectionActive = false;
                workflow.GenerationStatusMessage = "Harmony insertion selection cancelled.";
                SetStatus(context.Runtime, workflow.GenerationStatusMessage);
                return new WorkbenchEditorWorkflowResult
                {
                    Handled = true,
                    ConsumeInput = true
                };
            }

            if (context.Key != WorkbenchEditorInteractionKey.Tab || context.Shift || context.Control || context.Alt || !context.EditingEnabled || context.Session == null)
            {
                return new WorkbenchEditorWorkflowResult();
            }

            var caretIndex = context.Session.EditorState != null ? context.Session.EditorState.CaretIndex : 0;
            var lineNumber = context.Session.HighlightedLine > 0 ? context.Session.HighlightedLine : 1;
            string statusMessage;
            if (!_insertionService.TryApplyEditorInsertionSelection(runtime, context.EditorContext, context.Session, lineNumber, caretIndex, out statusMessage))
            {
                SetStatus(context.Runtime, statusMessage);
                return new WorkbenchEditorWorkflowResult
                {
                    Handled = true,
                    ConsumeInput = true
                };
            }

            ApplyGeneration(runtime, out statusMessage);
            SetStatus(context.Runtime, statusMessage);
            return new WorkbenchEditorWorkflowResult
            {
                Handled = true,
                ConsumeInput = true
            };
        }

        private bool TryValidateGenerationTarget(HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            reason = string.Empty;
            if (resolvedTarget == null || resolvedTarget.Method == null || resolvedTarget.InspectionRequest == null)
            {
                reason = "Select a resolvable external runtime method before generating a Harmony patch.";
                return false;
            }

            var request = resolvedTarget.InspectionRequest;
            if (string.IsNullOrEmpty(request.AssemblyPath) || request.MetadataToken <= 0)
            {
                reason = "Harmony patch generation requires a resolved external runtime method.";
                return false;
            }

            var project = resolvedTarget.Project;
            if (project == null)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(request.DocumentPath) && PathStartsWith(request.DocumentPath, project.SourceRootPath))
            {
                reason = "Harmony patch generation is only available for external patch targets, not methods from your own source project.";
                return false;
            }

            if (!string.IsNullOrEmpty(request.AssemblyPath) &&
                !string.IsNullOrEmpty(project.OutputAssemblyPath) &&
                PathsEqual(request.AssemblyPath, project.OutputAssemblyPath))
            {
                reason = "Harmony patch generation is only available for external patch targets, not methods from your own built assembly.";
                return false;
            }

            return true;
        }

        private HarmonyPatchGenerationRequest CreateDefaultRequest(HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationKind generationKind)
        {
            var method = resolvedTarget != null ? resolvedTarget.Method : null;
            var request = new HarmonyPatchGenerationRequest();
            request.GenerationKind = generationKind;
            request.TargetAssemblyPath = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.AssemblyPath : string.Empty;
            request.TargetMetadataToken = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.MetadataToken : 0;
            request.TargetDeclaringTypeName = method != null && method.DeclaringType != null ? method.DeclaringType.FullName ?? string.Empty : string.Empty;
            request.TargetMethodName = method != null ? method.Name : string.Empty;
            request.TargetSignature = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.Signature ?? string.Empty : string.Empty;
            request.TargetDocumentPath = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.DocumentPath ?? string.Empty : string.Empty;
            request.TargetCachePath = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.CachePath ?? string.Empty : string.Empty;
            request.NamespaceName = resolvedTarget != null && resolvedTarget.Project != null && !string.IsNullOrEmpty(resolvedTarget.Project.ModId)
                ? SanitizeIdentifier(resolvedTarget.Project.ModId) + ".Harmony"
                : "GeneratedHarmonyPatches";
            request.PatchClassName = BuildPatchClassName(method, generationKind);
            request.PatchMethodName = generationKind == HarmonyPatchGenerationKind.Prefix ? "Prefix" : "Postfix";
            request.InsertionAnchorKind = HarmonyPatchInsertionAnchorKind.EndOfFile;
            request.IncludeInstanceParameter = method != null && !method.IsStatic && !method.IsConstructor;
            request.IncludeArgumentParameters = method != null && method.GetParameters().Length > 0;
            request.IncludeStateParameter = generationKind == HarmonyPatchGenerationKind.Prefix;
            request.IncludeResultParameter = generationKind == HarmonyPatchGenerationKind.Postfix && method is MethodInfo && ((MethodInfo)method).ReturnType != typeof(void);
            request.UseSkipOriginalPattern = false;
            return request;
        }

        private static string BuildPatchClassName(MethodBase method, HarmonyPatchGenerationKind generationKind)
        {
            var typeName = method != null && method.DeclaringType != null ? method.DeclaringType.Name ?? "Target" : "Target";
            var methodName = method != null ? method.Name ?? "Method" : "Method";
            return SanitizeIdentifier(typeName) + "_" + SanitizeIdentifier(methodName) + "_" + (generationKind == HarmonyPatchGenerationKind.Prefix ? "PrefixPatch" : "PostfixPatch");
        }

        private static string SanitizeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Generated";
            }

            var characters = value.ToCharArray();
            for (var i = 0; i < characters.Length; i++)
            {
                if (!char.IsLetterOrDigit(characters[i]) && characters[i] != '_')
                {
                    characters[i] = '_';
                }
            }

            var result = new string(characters).Trim('_');
            if (string.IsNullOrEmpty(result))
            {
                result = "Generated";
            }

            if (!char.IsLetter(result[0]) && result[0] != '_')
            {
                result = "_" + result;
            }

            return result;
        }
    }
}
