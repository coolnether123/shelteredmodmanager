using System;
using System.IO;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;
using Cortex.Services.Navigation.Metadata;

namespace Cortex.Services.Harmony.Resolution
{
    internal interface IHarmonyMetadataTargetResolver
    {
        bool TryResolveFromInspectionRequest(IProjectCatalog projectCatalog, HarmonyPatchInspectionRequest request, out HarmonyResolvedMethodTarget resolvedTarget, out string reason);
        bool TryResolveTypeFromEditorTarget(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonyResolvedTypeTarget resolvedTarget, out string reason);
        bool TryResolveFromCallHierarchyItem(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, LanguageServiceCallHierarchyItem item, out HarmonyResolvedMethodTarget resolvedTarget, out string reason);
        bool TryResolveFromDecompilerDocument(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, string documentPath, string symbolText, int absolutePosition, out HarmonyResolvedMethodTarget resolvedTarget, out string reason);
        bool TryResolveFromMetadataSymbol(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, string containingAssemblyName, string documentationCommentId, string containingTypeName, string symbolKind, string displayName, string documentPath, out HarmonyResolvedMethodTarget resolvedTarget, out string reason);
    }

    internal sealed class HarmonyMetadataTargetResolver : IHarmonyMetadataTargetResolver
    {
        private readonly IAssemblyMetadataNavigationService _metadataNavigationService;
        private readonly IHarmonyMethodIdentityService _methodIdentityService;
        private readonly IHarmonyRuntimeMethodLookupService _methodLookupService;

        public HarmonyMetadataTargetResolver(IAssemblyMetadataNavigationService metadataNavigationService, IHarmonyMethodIdentityService methodIdentityService, IHarmonyRuntimeMethodLookupService methodLookupService)
        {
            _metadataNavigationService = metadataNavigationService;
            _methodIdentityService = methodIdentityService;
            _methodLookupService = methodLookupService;
        }

        public bool TryResolveFromInspectionRequest(IProjectCatalog projectCatalog, HarmonyPatchInspectionRequest request, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (request == null || string.IsNullOrEmpty(request.AssemblyPath) || request.MetadataToken <= 0)
            {
                reason = "Harmony inspection metadata was incomplete.";
                return false;
            }

            MethodBase method;
            if (!_methodIdentityService.TryResolveMethod(request.AssemblyPath, request.MetadataToken, out method))
            {
                reason = "The runtime method could not be loaded from the target assembly.";
                return false;
            }

            resolvedTarget = _methodIdentityService.CreateResolvedTarget(request, method, projectCatalog);
            return true;
        }

        public bool TryResolveTypeFromEditorTarget(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonyResolvedTypeTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (target == null || string.IsNullOrEmpty(target.DocumentPath))
            {
                reason = "Select a decompiled type before viewing Harmony patches in that area.";
                return false;
            }

            if (!CortexModuleUtil.IsDecompilerDocumentPath(state, target.DocumentPath))
            {
                reason = "Type-level Harmony inspection is only available from decompiled runtime documents.";
                return false;
            }

            string assemblyPath;
            if (!TryResolveAssemblyFromCachePath(state, sourceLookupIndex, target.DocumentPath, out assemblyPath))
            {
                reason = "The decompiled cache path could not be mapped back to an assembly.";
                return false;
            }

            Type declaringType;
            if (!TryResolveTypeFromCachePath(assemblyPath, target.DocumentPath, out declaringType) || declaringType == null)
            {
                reason = "The decompiled file could not be mapped back to a declaring type.";
                return false;
            }

            if (!string.IsNullOrEmpty(target.SymbolText) && !IsDeclaringTypeSymbol(declaringType, target.SymbolText))
            {
                reason = "The selected symbol is not the decompiled type for this document.";
                return false;
            }

            resolvedTarget = new HarmonyResolvedTypeTarget
            {
                AssemblyPath = assemblyPath,
                DeclaringType = declaringType,
                Project = _methodIdentityService.FindProjectForDocument(projectCatalog, string.Empty, assemblyPath),
                DisplayName = declaringType.FullName ?? declaringType.Name ?? string.Empty
            };
            return true;
        }

        public bool TryResolveFromCallHierarchyItem(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, LanguageServiceCallHierarchyItem item, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (item == null)
            {
                reason = "Incoming caller metadata was incomplete.";
                return false;
            }

            return TryResolveFromMetadataSymbol(
                state,
                sourceLookupIndex,
                projectCatalog,
                item.ContainingAssemblyName,
                item.DocumentationCommentId,
                item.ContainingTypeName,
                item.SymbolKind,
                !string.IsNullOrEmpty(item.QualifiedSymbolDisplay) ? item.QualifiedSymbolDisplay : item.SymbolDisplay,
                string.Empty,
                out resolvedTarget,
                out reason);
        }

        public bool TryResolveFromDecompilerDocument(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, string documentPath, string symbolText, int absolutePosition, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (string.IsNullOrEmpty(documentPath) || !CortexModuleUtil.IsDecompilerDocumentPath(state, documentPath))
            {
                return false;
            }

            string assemblyPath;
            if (!TryResolveAssemblyFromCachePath(state, sourceLookupIndex, documentPath, out assemblyPath))
            {
                reason = "The decompiled cache path could not be mapped back to an assembly.";
                return false;
            }

            var metadataToken = ExtractMetadataToken(documentPath);
            if (metadataToken > 0)
            {
                MethodBase method;
                if (_methodIdentityService.TryResolveMethod(assemblyPath, metadataToken, out method) && method != null)
                {
                    var request = _methodIdentityService.CreateInspectionRequest(
                        method,
                        assemblyPath,
                        string.Empty,
                        string.Empty,
                        documentPath,
                        string.Empty);
                    resolvedTarget = _methodIdentityService.CreateResolvedTarget(request, method, projectCatalog);
                    return true;
                }
            }

            Type declaringType;
            if (!TryResolveTypeFromCachePath(assemblyPath, documentPath, out declaringType) || declaringType == null)
            {
                reason = "The decompiled file could not be mapped back to a declaring type.";
                return false;
            }

            HarmonyMethodLookupHint hint = null;
            if (!string.IsNullOrEmpty(symbolText))
            {
                hint = new HarmonyMethodLookupHint
                {
                    Name = _methodLookupService.NormalizeMethodName(symbolText),
                    ParameterCount = -1
                };
            }

            string resolveReason;
            var methodFromType = _methodLookupService.ResolveMethod(declaringType, symbolText, hint, out resolveReason);
            if (methodFromType == null)
            {
                reason = !string.IsNullOrEmpty(resolveReason)
                    ? resolveReason
                    : "The selected decompiled member could not be resolved to a unique runtime method.";
                return false;
            }

            var inspectionRequest = _methodIdentityService.CreateInspectionRequest(
                methodFromType,
                assemblyPath,
                string.Empty,
                string.Empty,
                documentPath,
                string.Empty);
            resolvedTarget = _methodIdentityService.CreateResolvedTarget(inspectionRequest, methodFromType, projectCatalog);
            return true;
        }

        public bool TryResolveFromMetadataSymbol(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, string containingAssemblyName, string documentationCommentId, string containingTypeName, string symbolKind, string displayName, string documentPath, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;

            string assemblyPath;
            if (!_metadataNavigationService.TryResolveAssemblyPath(state, sourceLookupIndex, containingAssemblyName, out assemblyPath))
            {
                reason = "The containing assembly could not be located for the selected symbol.";
                return false;
            }

            MetadataNavigationTarget metadataTarget;
            if (!_metadataNavigationService.TryResolveMetadataTarget(
                assemblyPath,
                documentationCommentId,
                containingTypeName,
                symbolKind,
                out metadataTarget) ||
                metadataTarget == null ||
                metadataTarget.EntityKind != DecompilerEntityKind.Method)
            {
                reason = metadataTarget != null && metadataTarget.EntityKind == DecompilerEntityKind.Type
                    ? "The selected symbol resolved to a type, not a method."
                    : "Metadata navigation could not resolve the selected method.";
                return false;
            }

            MethodBase method;
            if (!_methodIdentityService.TryResolveMethod(assemblyPath, metadataTarget.MetadataToken, out method))
            {
                reason = "The selected method metadata token could not be resolved from the target assembly.";
                return false;
            }

            var request = _methodIdentityService.CreateInspectionRequest(
                method,
                assemblyPath,
                displayName,
                documentPath,
                string.Empty,
                documentationCommentId);
            resolvedTarget = _methodIdentityService.CreateResolvedTarget(request, method, projectCatalog);
            return true;
        }

        private bool TryResolveAssemblyFromCachePath(CortexShellState state, ISourceLookupIndex sourceLookupIndex, string cachePath, out string assemblyPath)
        {
            assemblyPath = string.Empty;
            var relativePath = GetCacheRelativePath(state, cachePath);
            if (string.IsNullOrEmpty(relativePath))
            {
                return false;
            }

            var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            var segments = relativePath.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }

            var assemblyName = segments[0];
            return _metadataNavigationService.TryResolveAssemblyPath(state, sourceLookupIndex, assemblyName, out assemblyPath);
        }

        private static string GetCacheRelativePath(CortexShellState state, string filePath)
        {
            var cacheRoot = state != null && state.Settings != null
                ? state.Settings.DecompilerCachePath ?? string.Empty
                : string.Empty;
            if (string.IsNullOrEmpty(cacheRoot) || string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            try
            {
                var root = Path.GetFullPath(cacheRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var path = Path.GetFullPath(filePath);
                return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int ExtractMetadataToken(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return 0;
            }

            var name = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            var markerIndex = name.LastIndexOf("_0x", StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0 || markerIndex + 3 >= name.Length)
            {
                return 0;
            }

            var tokenText = name.Substring(markerIndex + 3);
            int metadataToken;
            return int.TryParse(tokenText, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out metadataToken)
                ? metadataToken
                : 0;
        }

        private bool TryResolveTypeFromCachePath(string assemblyPath, string filePath, out Type declaringType)
        {
            declaringType = null;
            var metadataToken = ExtractMetadataToken(filePath);
            if (metadataToken > 0)
            {
                MethodBase method;
                if (_methodIdentityService.TryResolveMethod(assemblyPath, metadataToken, out method) &&
                    method != null &&
                    method.DeclaringType != null)
                {
                    declaringType = method.DeclaringType;
                    return true;
                }
            }

            string fullTypeName;
            if (_metadataNavigationService.TryResolveTypeNavigationTarget(assemblyPath, metadataToken, out fullTypeName) && !string.IsNullOrEmpty(fullTypeName))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    declaringType = assembly.GetType(fullTypeName, false);
                    return declaringType != null;
                }
                catch
                {
                }
            }

            return false;
        }

        private static bool IsDeclaringTypeSymbol(Type declaringType, string symbolText)
        {
            return declaringType != null &&
                !string.IsNullOrEmpty(symbolText) &&
                (string.Equals(symbolText, declaringType.Name, StringComparison.Ordinal) ||
                 string.Equals(symbolText, declaringType.FullName ?? string.Empty, StringComparison.Ordinal));
        }

        private static string NormalizeTypeName(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace('+', '.').Replace("global::", string.Empty).Trim();
        }

    }
}
