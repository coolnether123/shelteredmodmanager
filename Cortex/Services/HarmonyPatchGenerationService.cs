using System;
using System.IO;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;

namespace Cortex.Services
{
    internal sealed class HarmonyPatchGenerationService
    {
        private readonly HarmonyPatchTemplateService _templateService;
        private readonly HarmonyPatchInsertionService _insertionService;

        public HarmonyPatchGenerationService(HarmonyPatchTemplateService templateService, HarmonyPatchInsertionService insertionService)
        {
            _templateService = templateService;
            _insertionService = insertionService;
        }

        public HarmonyPatchGenerationRequest CreateDefaultRequest(HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationKind generationKind)
        {
            var method = resolvedTarget != null ? resolvedTarget.Method : null;
            var request = new HarmonyPatchGenerationRequest();
            request.GenerationKind = generationKind;
            request.TargetAssemblyPath = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.AssemblyPath : string.Empty;
            request.TargetMetadataToken = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.MetadataToken : 0;
            request.TargetDeclaringTypeName = method != null && method.DeclaringType != null ? method.DeclaringType.FullName ?? string.Empty : string.Empty;
            request.TargetMethodName = method != null ? method.Name : string.Empty;
            request.TargetSignature = HarmonyPatchResolutionService.BuildMethodSignature(method);
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

        public HarmonyPatchInsertionTarget[] BuildInsertionTargets(CortexShellState state, IProjectCatalog projectCatalog, HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationRequest request)
        {
            return _insertionService.BuildInsertionTargets(state, projectCatalog, resolvedTarget, request);
        }

        public bool TryValidateGenerationTarget(CortexShellState state, HarmonyResolvedMethodTarget resolvedTarget, out string reason)
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

            if (IsWorkspaceOwnedDocument(state, request.DocumentPath) &&
                !Cortex.Modules.Shared.CortexModuleUtil.IsDecompilerDocumentPath(state, request.DocumentPath))
            {
                reason = "Harmony patch generation is not available for methods from your workspace source files.";
                return false;
            }

            var project = resolvedTarget.Project;
            if (project == null)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(request.DocumentPath) &&
                !Cortex.Modules.Shared.CortexModuleUtil.IsDecompilerDocumentPath(null, request.DocumentPath) &&
                PathStartsWith(request.DocumentPath, project.SourceRootPath))
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

        public HarmonyPatchGenerationPreview BuildPreview(CortexShellState state, HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationRequest request)
        {
            var snippetPreview = _templateService.BuildSnippet(resolvedTarget, request);
            if (!snippetPreview.CanApply)
            {
                return snippetPreview;
            }

            return _insertionService.BuildPreview(state, request, snippetPreview);
        }

        public bool Apply(CortexShellState state, IDocumentService documentService, HarmonyPatchGenerationRequest request, HarmonyPatchGenerationPreview preview, out DocumentSession session, out string statusMessage)
        {
            return _insertionService.ApplyPreview(state, documentService, request, preview, out session, out statusMessage);
        }

        public void ArmEditorInsertionPick(CortexShellState state)
        {
            if (state == null || state.Harmony == null || state.Harmony.GenerationRequest == null)
            {
                return;
            }

            state.Harmony.IsInsertionPickActive = true;
            state.Harmony.GenerationStatusMessage = "Click a writable source editor line to choose where the Harmony patch should be inserted.";
            state.StatusMessage = state.Harmony.GenerationStatusMessage;
        }

        public void ClearEditorInsertionPick(CortexShellState state)
        {
            if (state == null || state.Harmony == null)
            {
                return;
            }

            state.Harmony.IsInsertionPickActive = false;
        }

        public bool TryApplyEditorInsertionSelection(CortexShellState state, DocumentSession session, int lineNumber, int absolutePosition, out string statusMessage)
        {
            statusMessage = "Harmony patch generation is not active.";
            if (state == null || state.Harmony == null || state.Harmony.GenerationRequest == null)
            {
                return false;
            }

            if (session == null || string.IsNullOrEmpty(session.FilePath))
            {
                statusMessage = "Open a writable source editor before choosing a Harmony insertion point.";
                return false;
            }

            if (!session.SupportsSaving || session.IsReadOnly || CortexModuleUtil.IsDecompilerDocumentPath(state, session.FilePath))
            {
                statusMessage = "Select the Harmony insertion point from a writable source editor, not decompiled output.";
                state.Harmony.GenerationStatusMessage = statusMessage;
                state.StatusMessage = statusMessage;
                return false;
            }

            var normalizedPath = NormalizePath(session.FilePath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                statusMessage = "The selected editor path was invalid for Harmony insertion.";
                state.Harmony.GenerationStatusMessage = statusMessage;
                state.StatusMessage = statusMessage;
                return false;
            }

            var request = state.Harmony.GenerationRequest;
            request.DestinationFilePath = normalizedPath;
            request.InsertionAnchorKind = HarmonyPatchInsertionAnchorKind.SelectedContext;
            request.InsertionLine = Math.Max(1, lineNumber);
            request.InsertionAbsolutePosition = Math.Max(0, absolutePosition);
            request.InsertionContextLabel = "Selected editor slot";
            UpsertEditorInsertionTarget(state, normalizedPath, request.InsertionLine, request.InsertionAbsolutePosition, request.InsertionContextLabel);
            state.Harmony.IsInsertionPickActive = false;
            state.Harmony.GenerationPreview = null;
            statusMessage = "Selected " + Path.GetFileName(normalizedPath) + ":" + request.InsertionLine + " for Harmony patch insertion.";
            state.Harmony.GenerationStatusMessage = statusMessage;
            state.StatusMessage = statusMessage;
            return true;
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

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool PathStartsWith(string path, string rootPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(rootPath))
            {
                return false;
            }

            try
            {
                var normalizedPath = Path.GetFullPath(path);
                var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWorkspaceOwnedDocument(CortexShellState state, string documentPath)
        {
            if (string.IsNullOrEmpty(documentPath))
            {
                return false;
            }

            var workspaceRoot = state != null && state.Settings != null
                ? state.Settings.WorkspaceRootPath ?? string.Empty
                : string.Empty;
            return PathStartsWith(documentPath, workspaceRoot);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void UpsertEditorInsertionTarget(CortexShellState state, string filePath, int lineNumber, int absolutePosition, string contextLabel)
        {
            if (state == null || state.Harmony == null || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var targets = state.Harmony.InsertionTargets;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !PathsEqual(target.FilePath, filePath))
                {
                    continue;
                }

                target.IsWritable = true;
                target.DefaultAnchorKind = HarmonyPatchInsertionAnchorKind.SelectedContext;
                target.SuggestedLine = Math.Max(1, lineNumber);
                target.SuggestedAbsolutePosition = Math.Max(0, absolutePosition);
                target.SuggestedContextLabel = contextLabel ?? string.Empty;
                target.Reason = "Selected editor insertion point";
                return;
            }

            targets.Insert(0, new HarmonyPatchInsertionTarget
            {
                FilePath = filePath,
                DisplayName = Path.GetFileName(filePath),
                IsNewFile = !File.Exists(filePath),
                IsWritable = true,
                DefaultAnchorKind = HarmonyPatchInsertionAnchorKind.SelectedContext,
                SuggestedLine = Math.Max(1, lineNumber),
                SuggestedAbsolutePosition = Math.Max(0, absolutePosition),
                SuggestedContextLabel = contextLabel ?? string.Empty,
                Reason = "Selected editor insertion point"
            });
        }
    }
}
