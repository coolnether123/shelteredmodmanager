using System;
using System.IO;
using System.Reflection;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services.Harmony.Resolution;

namespace Cortex.Services.Harmony.Generation
{
    internal interface IHarmonyPatchGenerationRequestFactory
    {
        HarmonyPatchGenerationRequest CreateDefaultRequest(HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationKind generationKind);
        bool TryValidateGenerationTarget(CortexShellState state, HarmonyResolvedMethodTarget resolvedTarget, out string reason);
    }

    internal sealed class HarmonyPatchGenerationRequestFactory : IHarmonyPatchGenerationRequestFactory
    {
        private readonly HarmonyMethodIdentityService _methodIdentityService = new HarmonyMethodIdentityService();

        public HarmonyPatchGenerationRequest CreateDefaultRequest(HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationKind generationKind)
        {
            var method = resolvedTarget != null ? resolvedTarget.Method : null;
            var request = new HarmonyPatchGenerationRequest();
            request.GenerationKind = generationKind;
            request.TargetAssemblyPath = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.AssemblyPath : string.Empty;
            request.TargetMetadataToken = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.MetadataToken : 0;
            request.TargetDeclaringTypeName = method != null && method.DeclaringType != null ? method.DeclaringType.FullName ?? string.Empty : string.Empty;
            request.TargetMethodName = method != null ? method.Name : string.Empty;
            request.TargetSignature = _methodIdentityService.BuildMethodSignature(method);
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
                !CortexModuleUtil.IsDecompilerDocumentPath(state, request.DocumentPath))
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
                !CortexModuleUtil.IsDecompilerDocumentPath(null, request.DocumentPath) &&
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
    }
}
