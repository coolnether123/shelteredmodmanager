using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;

namespace Cortex.Services
{
    internal sealed class HarmonyResolvedMethodTarget
    {
        public HarmonyPatchInspectionRequest InspectionRequest;
        public MethodBase Method;
        public CortexProjectDefinition Project;
        public string DisplayName = string.Empty;
    }

    internal sealed class HarmonyResolvedTypeTarget
    {
        public string AssemblyPath = string.Empty;
        public Type DeclaringType;
        public CortexProjectDefinition Project;
        public string DisplayName = string.Empty;
    }

    internal sealed class HarmonyPatchAttributeBinding
    {
        public string TypeName = string.Empty;
        public string MethodName = string.Empty;
        public string[] ParameterTypeNames = new string[0];
    }

    internal sealed class HarmonyPatchResolutionService
    {
        private readonly EditorSymbolInteractionService _symbolInteractionService = new EditorSymbolInteractionService();

        public bool TryResolveFromEditorTarget(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (target == null || string.IsNullOrEmpty(target.DocumentPath))
            {
                reason = "Select a resolvable method before using Harmony actions.";
                return false;
            }

            if (CortexModuleUtil.IsDecompilerDocumentPath(state, target.DocumentPath))
            {
                if (TryResolveFromDecompilerDocument(
                    state,
                    sourceLookupIndex,
                    projectCatalog,
                    target.DocumentPath,
                    target.SymbolText,
                    target.AbsolutePosition,
                    out resolvedTarget,
                    out reason))
                {
                    return true;
                }

                if (string.IsNullOrEmpty(reason))
                {
                    reason = "The selected decompiled member could not be resolved to a unique runtime method.";
                }

                return false;
            }

            string hoverReason;
            if (TryResolveFromSourceHover(state, sourceLookupIndex, projectCatalog, target, out resolvedTarget, out hoverReason))
            {
                reason = string.Empty;
                return true;
            }

            string attributeReason;
            if (TryResolveFromSourceHarmonyPatchAttribute(state, projectCatalog, target, out resolvedTarget, out attributeReason))
            {
                reason = string.Empty;
                return true;
            }

            string fallbackReason;
            if (TryResolveFromSourceFallback(state, projectCatalog, target, out resolvedTarget, out fallbackReason))
            {
                reason = string.Empty;
                return true;
            }

            reason = !string.IsNullOrEmpty(hoverReason)
                ? hoverReason
                : !string.IsNullOrEmpty(attributeReason)
                    ? attributeReason
                : fallbackReason;
            if (string.IsNullOrEmpty(reason))
            {
                reason = "Cortex could not map the current editor context to a runtime method.";
            }
            return false;
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
            if (!TryResolveMethod(request.AssemblyPath, request.MetadataToken, out method))
            {
                reason = "The runtime method could not be loaded from the target assembly.";
                return false;
            }

            resolvedTarget = BuildResolvedTarget(
                request,
                method,
                FindProjectForRequest(projectCatalog, request, method),
                !string.IsNullOrEmpty(request.DisplayName) ? request.DisplayName : BuildMethodDisplayName(method));
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
                Project = FindProjectForDocument(projectCatalog, string.Empty, assemblyPath),
                DisplayName = declaringType.FullName ?? declaringType.Name ?? string.Empty
            };
            return true;
        }

        public bool TryResolveFromDocument(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, DocumentSession session, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (session == null || string.IsNullOrEmpty(session.FilePath))
            {
                reason = "Open a source or decompiled method first.";
                return false;
            }

            EditorCommandTarget documentTarget;
            TryBuildDocumentTarget(state, session, out documentTarget);

            if (TryResolveFromDecompilerDocument(
                state,
                sourceLookupIndex,
                projectCatalog,
                session.FilePath,
                documentTarget != null ? documentTarget.SymbolText : string.Empty,
                documentTarget != null ? documentTarget.AbsolutePosition : 0,
                out resolvedTarget,
                out reason))
            {
                return true;
            }

            if (documentTarget != null &&
                TryResolveFromSourceHover(state, sourceLookupIndex, projectCatalog, documentTarget, out resolvedTarget, out reason))
            {
                return true;
            }

            if (string.IsNullOrEmpty(reason))
            {
                reason = "The active document does not resolve to a specific Harmony target yet.";
            }
            return false;
        }

        public static string BuildMethodDisplayName(MethodBase method)
        {
            if (method == null)
            {
                return string.Empty;
            }

            var declaringType = method.DeclaringType != null ? method.DeclaringType.FullName ?? method.DeclaringType.Name ?? string.Empty : string.Empty;
            return declaringType + "." + method.Name + BuildMethodSignature(method);
        }

        public static string BuildMethodSignature(MethodBase method)
        {
            if (method == null)
            {
                return "()";
            }

            var parameters = method.GetParameters();
            var builder = new StringBuilder();
            builder.Append("(");
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                var parameterType = parameters[i].ParameterType;
                if (parameterType != null && parameterType.IsByRef)
                {
                    builder.Append(parameters[i].IsOut ? "out " : "ref ");
                    parameterType = parameterType.GetElementType();
                }

                builder.Append(parameterType != null ? parameterType.Name : "object");
                builder.Append(" ");
                builder.Append(parameters[i].Name ?? ("arg" + i));
            }
            builder.Append(")");
            return builder.ToString();
        }

        private bool TryResolveFromSourceHover(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            var hoverResponse = GetMatchingHoverResponse(state, target);
            if (hoverResponse == null || !hoverResponse.Success)
            {
                reason = "Hover metadata was not available for the selected symbol.";
                return false;
            }

            string assemblyPath;
            if (!MetadataNavigationResolver.TryResolveAssemblyPath(state, sourceLookupIndex, hoverResponse.ContainingAssemblyName, out assemblyPath))
            {
                reason = "The containing assembly could not be located for the selected symbol.";
                return false;
            }

            int metadataToken;
            DecompilerEntityKind entityKind;
            if (!MetadataNavigationResolver.TryResolveMetadataTarget(
                assemblyPath,
                hoverResponse.DocumentationCommentId,
                hoverResponse.ContainingTypeName,
                hoverResponse.SymbolKind,
                out metadataToken,
                out entityKind) ||
                entityKind != DecompilerEntityKind.Method)
            {
                reason = entityKind == DecompilerEntityKind.Type
                    ? "The selected symbol resolved to a type, not a method."
                    : "Metadata navigation could not resolve the selected method.";
                return false;
            }

            MethodBase method;
            if (!TryResolveMethod(assemblyPath, metadataToken, out method))
            {
                reason = "The selected method metadata token could not be resolved from the target assembly.";
                return false;
            }

            var request = new HarmonyPatchInspectionRequest
            {
                AssemblyPath = assemblyPath,
                MetadataToken = metadataToken,
                DeclaringTypeName = hoverResponse.ContainingTypeName ?? (method.DeclaringType != null ? method.DeclaringType.FullName : string.Empty),
                MethodName = method.Name,
                Signature = BuildMethodSignature(method),
                DisplayName = hoverResponse.SymbolDisplay ?? BuildMethodDisplayName(method),
                DocumentPath = hoverResponse.DefinitionDocumentPath ?? target.DocumentPath ?? string.Empty,
                DocumentationCommentId = hoverResponse.DocumentationCommentId ?? string.Empty
            };

            resolvedTarget = BuildResolvedTarget(request, method, FindProjectForDocument(projectCatalog, request.DocumentPath, assemblyPath), request.DisplayName);
            return true;
        }

        private bool TryResolveFromSourceHarmonyPatchAttribute(CortexShellState state, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (target == null || string.IsNullOrEmpty(target.DocumentPath) || CortexModuleUtil.IsDecompilerDocumentPath(state, target.DocumentPath))
            {
                reason = "Harmony attribute source resolution is only available for writable source documents.";
                return false;
            }

            var text = GetDocumentText(state, target.DocumentPath);
            if (string.IsNullOrEmpty(text))
            {
                reason = "Source text was not available for Harmony attribute resolution.";
                return false;
            }

            HarmonyPatchAttributeBinding binding;
            if (!TryBuildHarmonyPatchBinding(text, target.AbsolutePosition, target.SymbolText, out binding))
            {
                reason = "No enclosing HarmonyPatch attribute could be resolved from the current source context.";
                return false;
            }

            Type declaringType;
            string assemblyPath;
            if (!TryResolveRuntimeTypeByName(binding.TypeName, out assemblyPath, out declaringType))
            {
                reason = "The HarmonyPatch target type could not be resolved from the current runtime.";
                return false;
            }

            MethodLookupHint hint = null;
            if (!string.IsNullOrEmpty(binding.MethodName))
            {
                hint = new MethodLookupHint
                {
                    Name = binding.MethodName,
                    ParameterTypeNames = binding.ParameterTypeNames ?? new string[0],
                    ParameterCount = binding.ParameterTypeNames != null && binding.ParameterTypeNames.Length > 0
                        ? binding.ParameterTypeNames.Length
                        : -1
                };
            }

            string resolveReason;
            var method = ResolveMethod(declaringType, binding.MethodName, hint, out resolveReason);
            if (method == null)
            {
                reason = !string.IsNullOrEmpty(resolveReason)
                    ? resolveReason
                    : "The HarmonyPatch target method could not be resolved from the current runtime.";
                return false;
            }

            var request = new HarmonyPatchInspectionRequest
            {
                AssemblyPath = assemblyPath,
                MetadataToken = method.MetadataToken,
                DeclaringTypeName = declaringType.FullName ?? string.Empty,
                MethodName = method.Name,
                Signature = BuildMethodSignature(method),
                DisplayName = BuildMethodDisplayName(method),
                DocumentPath = target.DocumentPath ?? string.Empty
            };

            resolvedTarget = BuildResolvedTarget(request, method, FindProjectForDocument(projectCatalog, request.DocumentPath, assemblyPath), request.DisplayName);
            return true;
        }

        private bool TryResolveFromDecompilerDocument(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, string documentPath, string symbolText, int absolutePosition, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (string.IsNullOrEmpty(documentPath) || !CortexModuleUtil.IsDecompilerDocumentPath(state, documentPath))
            {
                reason = "The current document is not a decompiled method view.";
                return false;
            }

            string assemblyPath;
            if (!TryResolveAssemblyFromCachePath(state, sourceLookupIndex, documentPath, out assemblyPath))
            {
                reason = "The decompiled cache path could not be mapped back to an assembly.";
                return false;
            }

            var metadataToken = ExtractMetadataToken(documentPath);
            MethodBase method;
            if (metadataToken > 0 && TryResolveMethod(assemblyPath, metadataToken, out method))
            {
                var request = new HarmonyPatchInspectionRequest
                {
                    AssemblyPath = assemblyPath,
                    MetadataToken = metadataToken,
                    DeclaringTypeName = method.DeclaringType != null ? method.DeclaringType.FullName ?? string.Empty : string.Empty,
                    MethodName = method.Name,
                    Signature = BuildMethodSignature(method),
                    DisplayName = BuildMethodDisplayName(method),
                    CachePath = documentPath,
                    DocumentPath = documentPath
                };
                resolvedTarget = BuildResolvedTarget(request, method, FindProjectForDocument(projectCatalog, string.Empty, assemblyPath), request.DisplayName);
                return true;
            }

            Type declaringType;
            if (!TryResolveTypeFromCachePath(assemblyPath, documentPath, out declaringType))
            {
                reason = "The decompiled file could not be mapped back to a declaring type.";
                return false;
            }

            MethodLookupHint hint;
            var hasHint = TryBuildLookupHint(state, documentPath, absolutePosition, symbolText, out hint);
            if (!hasHint && string.IsNullOrEmpty(NormalizeMethodName(symbolText)))
            {
                reason = "Right-click inside a specific method header or body to inspect Harmony patches.";
                return false;
            }

            if (!hasHint && IsDeclaringTypeSymbol(declaringType, symbolText))
            {
                reason = "The selected symbol resolved to a type, not a method. Right-click a specific method header or body instead.";
                return false;
            }

            var effectiveSymbolText = hasHint && hint != null && !string.IsNullOrEmpty(hint.Name)
                ? string.Empty
                : symbolText;
            method = ResolveMethod(declaringType, effectiveSymbolText, hint, out reason);
            if (method == null)
            {
                if (string.IsNullOrEmpty(reason))
                {
                    reason = "The selected decompiled member could not be resolved to a unique runtime method.";
                }
                return false;
            }

            var inspectionRequest = new HarmonyPatchInspectionRequest
            {
                AssemblyPath = assemblyPath,
                MetadataToken = method.MetadataToken,
                DeclaringTypeName = declaringType.FullName ?? string.Empty,
                MethodName = method.Name,
                Signature = BuildMethodSignature(method),
                DisplayName = BuildMethodDisplayName(method),
                CachePath = documentPath,
                DocumentPath = documentPath
            };
            resolvedTarget = BuildResolvedTarget(inspectionRequest, method, FindProjectForDocument(projectCatalog, string.Empty, assemblyPath), inspectionRequest.DisplayName);
            return true;
        }

        private bool TryResolveFromSourceFallback(CortexShellState state, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (target == null || string.IsNullOrEmpty(target.SymbolText))
            {
                reason = "No method symbol was selected.";
                return false;
            }

            var project = FindProjectForDocument(projectCatalog, target.DocumentPath, string.Empty);
            var assemblyPath = project != null ? project.OutputAssemblyPath ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
            {
                reason = "The source file is not associated with a build output assembly.";
                return false;
            }

            var assembly = LoadAssembly(assemblyPath);
            if (assembly == null)
            {
                reason = "The project output assembly could not be loaded for fallback resolution.";
                return false;
            }

            MethodBase unique = null;
            MethodLookupHint hint;
            TryBuildLookupHint(state, target.DocumentPath, target.AbsolutePosition, target.SymbolText, out hint);
            var matchCount = 0;
            try
            {
                var types = assembly.GetTypes();
                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    string candidateReason;
                    var candidate = ResolveMethod(types[typeIndex], target.SymbolText, hint, out candidateReason);
                    if (candidate == null)
                    {
                        continue;
                    }

                    matchCount++;
                    if (unique != null && unique.MetadataToken != candidate.MetadataToken)
                    {
                        reason = "Fallback source resolution was ambiguous across multiple runtime methods.";
                        return false;
                    }

                    unique = candidate;
                }
            }
            catch
            {
                reason = "Fallback source resolution failed while scanning the output assembly.";
                return false;
            }

            if (unique == null)
            {
                reason = "Fallback source resolution could not find a matching runtime method.";
                return false;
            }

            if (matchCount > 1)
            {
                reason = "Fallback source resolution matched multiple runtime methods.";
                return false;
            }

            var request = new HarmonyPatchInspectionRequest
            {
                AssemblyPath = assemblyPath,
                MetadataToken = unique.MetadataToken,
                DeclaringTypeName = unique.DeclaringType != null ? unique.DeclaringType.FullName ?? string.Empty : string.Empty,
                MethodName = unique.Name,
                Signature = BuildMethodSignature(unique),
                DisplayName = BuildMethodDisplayName(unique),
                DocumentPath = target.DocumentPath ?? string.Empty
            };
            resolvedTarget = BuildResolvedTarget(request, unique, project, request.DisplayName);
            return true;
        }

        private static HarmonyResolvedMethodTarget BuildResolvedTarget(HarmonyPatchInspectionRequest request, MethodBase method, CortexProjectDefinition project, string displayName)
        {
            return new HarmonyResolvedMethodTarget
            {
                InspectionRequest = request,
                Method = method,
                Project = project,
                DisplayName = displayName ?? string.Empty
            };
        }

        private static LanguageServiceHoverResponse GetMatchingHoverResponse(CortexShellState state, EditorCommandTarget target)
        {
            if (state == null || state.Editor == null || target == null)
            {
                return null;
            }

            var key = (target.DocumentPath ?? string.Empty) + "|" + target.AbsolutePosition;
            return string.Equals(state.Editor.ActiveHoverKey, key, StringComparison.Ordinal)
                ? state.Editor.ActiveHoverResponse
                : null;
        }

        private bool TryBuildDocumentTarget(CortexShellState state, DocumentSession session, out EditorCommandTarget target)
        {
            target = null;
            if (session == null)
            {
                return false;
            }

            var absolutePosition = session.EditorState != null ? session.EditorState.CaretIndex : 0;
            return _symbolInteractionService.TryCreateTargetFromPosition(
                session,
                absolutePosition,
                GetMatchingHoverResponse(state, new EditorCommandTarget
                {
                    DocumentPath = session.FilePath ?? string.Empty,
                    AbsolutePosition = absolutePosition
                }),
                out target);
        }

        private static bool TryResolveAssemblyFromCachePath(CortexShellState state, ISourceLookupIndex sourceLookupIndex, string cachePath, out string assemblyPath)
        {
            assemblyPath = string.Empty;
            var relative = GetCacheRelativePath(state, cachePath);
            if (string.IsNullOrEmpty(relative))
            {
                return false;
            }

            var segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }

            return MetadataNavigationResolver.TryResolveAssemblyPath(state, sourceLookupIndex, segments[0], out assemblyPath);
        }

        private static string GetCacheRelativePath(CortexShellState state, string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            var fullPath = Path.GetFullPath(filePath);
            var cacheRoot = state != null && state.Settings != null ? state.Settings.DecompilerCachePath ?? string.Empty : string.Empty;
            if (!string.IsNullOrEmpty(cacheRoot))
            {
                var normalizedRoot = Path.GetFullPath(cacheRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return fullPath.Substring(normalizedRoot.Length);
                }
            }

            var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            var fileName = Path.GetFileName(fullPath) ?? string.Empty;
            var parent = Path.GetFileName(directory) ?? string.Empty;
            return !string.IsNullOrEmpty(parent) ? Path.Combine(parent, fileName) : fileName;
        }

        private static int ExtractMetadataToken(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            const string marker = "_0x";
            var markerIndex = fileName.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return 0;
            }

            var hexStart = markerIndex + marker.Length;
            if (hexStart + 8 != fileName.Length)
            {
                return 0;
            }

            var hexValue = fileName.Substring(hexStart, 8);
            try
            {
                return int.Parse(hexValue, System.Globalization.NumberStyles.HexNumber);
            }
            catch
            {
                return 0;
            }
        }

        private static bool TryResolveTypeFromCachePath(string assemblyPath, string filePath, out Type declaringType)
        {
            declaringType = null;
            var assembly = LoadAssembly(assemblyPath);
            if (assembly == null)
            {
                return false;
            }

            var fileStem = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            var tokenIndex = fileStem.IndexOf("_0x", StringComparison.OrdinalIgnoreCase);
            if (tokenIndex >= 0)
            {
                fileStem = fileStem.Substring(0, tokenIndex);
            }

            var lastDot = fileStem.LastIndexOf('.');
            if (lastDot >= 0)
            {
                fileStem = fileStem.Substring(0, lastDot);
            }

            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(directory))
            {
                var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath) ?? string.Empty;
                var pieces = directory.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                var namespaceStartIndex = pieces.Length;
                if (!string.IsNullOrEmpty(assemblyName))
                {
                    for (var i = 0; i < pieces.Length; i++)
                    {
                        if (string.Equals(pieces[i], assemblyName, StringComparison.OrdinalIgnoreCase))
                        {
                            namespaceStartIndex = i + 1;
                            break;
                        }
                    }
                }

                for (var i = namespaceStartIndex; i < pieces.Length; i++)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(".");
                    }
                    builder.Append(pieces[i]);
                }
            }

            if (builder.Length > 0)
            {
                builder.Append(".");
            }
            builder.Append(fileStem);
            var normalizedTypeName = builder.ToString().Replace('+', '.');

            declaringType = assembly.GetType(normalizedTypeName, false);
            if (declaringType != null)
            {
                return true;
            }

            try
            {
                var types = assembly.GetTypes();
                for (var i = 0; i < types.Length; i++)
                {
                    var current = types[i];
                    if (current == null)
                    {
                        continue;
                    }

                    var fullName = (current.FullName ?? current.Name ?? string.Empty).Replace('+', '.');
                    if (string.Equals(fullName, normalizedTypeName, StringComparison.OrdinalIgnoreCase))
                    {
                        declaringType = current;
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static MethodBase ResolveMethod(Type declaringType, string symbolText, MethodLookupHint hint, out string reason)
        {
            reason = string.Empty;
            if (declaringType == null)
            {
                return null;
            }

            var matches = new MethodBase[64];
            var matchCount = 0;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            try
            {
                var methods = declaringType.GetMethods(flags);
                for (var i = 0; i < methods.Length; i++)
                {
                    if (!IsMethodMatch(methods[i], declaringType, symbolText, hint))
                    {
                        continue;
                    }

                    matchCount = AddMatch(matches, matchCount, methods[i]);
                }

                var constructors = declaringType.GetConstructors(flags);
                for (var i = 0; i < constructors.Length; i++)
                {
                    if (!IsMethodMatch(constructors[i], declaringType, symbolText, hint))
                    {
                        continue;
                    }

                    matchCount = AddMatch(matches, matchCount, constructors[i]);
                }

                if (declaringType.TypeInitializer != null && IsMethodMatch(declaringType.TypeInitializer, declaringType, symbolText, hint))
                {
                    matchCount = AddMatch(matches, matchCount, declaringType.TypeInitializer);
                }
            }
            catch
            {
                reason = "Runtime reflection failed while resolving the selected method.";
                return null;
            }

            if (matchCount == 0)
            {
                reason = "No runtime method matched the selected symbol.";
                return null;
            }

            if (matchCount > 1)
            {
                reason = "Multiple overloads or accessors matched the selected symbol. Move the caret to a specific method header or use a source hover result.";
                return null;
            }

            return matches[0];
        }

        private static bool TryResolveMethod(string assemblyPath, int metadataToken, out MethodBase method)
        {
            method = null;
            if (string.IsNullOrEmpty(assemblyPath) || metadataToken <= 0)
            {
                return false;
            }

            var assembly = LoadAssembly(assemblyPath);
            if (assembly == null)
            {
                return false;
            }

            try
            {
                method = assembly.ManifestModule.ResolveMethod(metadataToken);
            }
            catch
            {
                method = null;
            }

            return method != null;
        }

        private static Assembly LoadAssembly(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                return null;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                try
                {
                    if (string.Equals(loadedAssemblies[i].Location, assemblyPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return loadedAssemblies[i];
                    }
                }
                catch
                {
                }
            }

            try
            {
                return File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null;
            }
            catch
            {
                return null;
            }
        }

        private static CortexProjectDefinition FindProjectForRequest(IProjectCatalog projectCatalog, HarmonyPatchInspectionRequest request, MethodBase method)
        {
            return FindProjectForDocument(projectCatalog, request != null ? request.DocumentPath : string.Empty, request != null ? request.AssemblyPath : string.Empty) ??
                FindProjectByAssembly(projectCatalog, method != null ? method.Module.Assembly.Location : string.Empty);
        }

        private static CortexProjectDefinition FindProjectForDocument(IProjectCatalog projectCatalog, string documentPath, string assemblyPath)
        {
            if (projectCatalog == null)
            {
                return null;
            }

            var projects = projectCatalog.GetProjects();
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(documentPath) && PathStartsWith(documentPath, project.SourceRootPath))
                {
                    return project;
                }

                if (!string.IsNullOrEmpty(assemblyPath) &&
                    !string.IsNullOrEmpty(project.OutputAssemblyPath) &&
                    string.Equals(Path.GetFullPath(project.OutputAssemblyPath), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase))
                {
                    return project;
                }
            }

            return null;
        }

        private static CortexProjectDefinition FindProjectByAssembly(IProjectCatalog projectCatalog, string assemblyPath)
        {
            return FindProjectForDocument(projectCatalog, string.Empty, assemblyPath);
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

        private static int AddMatch(MethodBase[] matches, int count, MethodBase method)
        {
            if (method == null || matches == null)
            {
                return count;
            }

            for (var i = 0; i < count; i++)
            {
                if (matches[i] != null && matches[i].MetadataToken == method.MetadataToken)
                {
                    return count;
                }
            }

            if (count < matches.Length)
            {
                matches[count] = method;
                return count + 1;
            }

            return count;
        }

        private static bool IsMethodMatch(MethodBase method, Type declaringType, string symbolText, MethodLookupHint hint)
        {
            if (method == null)
            {
                return false;
            }

            if (hint != null)
            {
                if (hint.IsStaticConstructor && method.Name != ".cctor")
                {
                    return false;
                }

                if (hint.IsConstructor && !method.IsConstructor && method.Name != ".ctor")
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(hint.Name) && !NameMatches(method, declaringType, hint.Name))
                {
                    return false;
                }

                if (hint.ParameterCount >= 0 && method.GetParameters().Length != hint.ParameterCount)
                {
                    return false;
                }

                if (hint.ParameterTypeNames != null && hint.ParameterTypeNames.Length > 0 && !ParameterTypesMatch(method, hint.ParameterTypeNames))
                {
                    return false;
                }
            }

            return string.IsNullOrEmpty(symbolText) || NameMatches(method, declaringType, symbolText);
        }

        private static bool ParameterTypesMatch(MethodBase method, string[] parameterTypeNames)
        {
            var parameters = method != null ? method.GetParameters() : null;
            if (parameters == null || parameterTypeNames == null || parameters.Length != parameterTypeNames.Length)
            {
                return false;
            }

            for (var i = 0; i < parameters.Length; i++)
            {
                var type = parameters[i].ParameterType;
                if (type != null && type.IsByRef)
                {
                    type = type.GetElementType();
                }

                if (!TypeNameMatches(type, parameterTypeNames[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool NameMatches(MethodBase method, Type declaringType, string symbolText)
        {
            var normalizedSymbol = NormalizeMethodName(symbolText);
            if (string.IsNullOrEmpty(normalizedSymbol) || method == null)
            {
                return false;
            }

            var normalizedSymbolSuffix = normalizedSymbol;
            var normalizedSymbolDot = normalizedSymbol.LastIndexOf('.');
            if (normalizedSymbolDot >= 0 && normalizedSymbolDot + 1 < normalizedSymbol.Length)
            {
                normalizedSymbolSuffix = normalizedSymbol.Substring(normalizedSymbolDot + 1);
            }

            var methodName = method.Name ?? string.Empty;
            if (string.Equals(methodName, normalizedSymbol, StringComparison.Ordinal) ||
                string.Equals(NormalizeMethodName(methodName), normalizedSymbol, StringComparison.Ordinal) ||
                string.Equals(methodName, normalizedSymbolSuffix, StringComparison.Ordinal) ||
                string.Equals(NormalizeMethodName(methodName), normalizedSymbolSuffix, StringComparison.Ordinal))
            {
                return true;
            }

            if (method.IsConstructor)
            {
                return string.Equals(normalizedSymbol, ".ctor", StringComparison.Ordinal) ||
                    string.Equals(normalizedSymbol, ".cctor", StringComparison.Ordinal) ||
                    string.Equals(normalizedSymbol, NormalizeMethodName(declaringType != null ? declaringType.Name : string.Empty), StringComparison.Ordinal) ||
                    string.Equals(normalizedSymbolSuffix, NormalizeMethodName(declaringType != null ? declaringType.Name : string.Empty), StringComparison.Ordinal);
            }

            var explicitIndex = methodName.LastIndexOf('.');
            if (explicitIndex >= 0)
            {
                var suffix = methodName.Substring(explicitIndex + 1);
                if (string.Equals(NormalizeMethodName(suffix), normalizedSymbol, StringComparison.Ordinal))
                {
                    return true;
                }

                if (string.Equals(NormalizeMethodName(suffix), normalizedSymbolSuffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            var accessorBase = GetAccessorBaseName(methodName);
            if (!string.IsNullOrEmpty(accessorBase) &&
                (string.Equals(NormalizeMethodName(accessorBase), normalizedSymbol, StringComparison.Ordinal) ||
                string.Equals(NormalizeMethodName(accessorBase), normalizedSymbolSuffix, StringComparison.Ordinal)))
            {
                return true;
            }

            var operatorName = GetOperatorMethodName(symbolText);
            return !string.IsNullOrEmpty(operatorName) && string.Equals(methodName, operatorName, StringComparison.Ordinal);
        }

        private static string GetAccessorBaseName(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                return string.Empty;
            }

            if (methodName.StartsWith("get_", StringComparison.Ordinal) ||
                methodName.StartsWith("set_", StringComparison.Ordinal) ||
                methodName.StartsWith("add_", StringComparison.Ordinal) ||
                methodName.StartsWith("remove_", StringComparison.Ordinal))
            {
                return methodName.Substring(4);
            }

            var explicitIndex = methodName.LastIndexOf('.');
            if (explicitIndex > 0)
            {
                var suffix = methodName.Substring(explicitIndex + 1);
                if (suffix.StartsWith("get_", StringComparison.Ordinal) ||
                    suffix.StartsWith("set_", StringComparison.Ordinal) ||
                    suffix.StartsWith("add_", StringComparison.Ordinal) ||
                    suffix.StartsWith("remove_", StringComparison.Ordinal))
                {
                    return suffix.Substring(4);
                }
            }

            return string.Empty;
        }

        private static string NormalizeMethodName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            value = value.Trim();
            var parenIndex = value.IndexOf('(');
            if (parenIndex >= 0)
            {
                value = value.Substring(0, parenIndex);
            }

            var whitespaceIndex = value.LastIndexOf(' ');
            if (whitespaceIndex >= 0)
            {
                value = value.Substring(whitespaceIndex + 1);
            }

            return value.Trim();
        }

        private static bool TypeNameMatches(Type runtimeType, string requestedTypeName)
        {
            if (runtimeType == null || string.IsNullOrEmpty(requestedTypeName))
            {
                return false;
            }

            var normalizedRequested = NormalizeTypeName(requestedTypeName);
            var runtimeFullName = NormalizeTypeName(runtimeType.FullName ?? runtimeType.Name ?? string.Empty);
            var runtimeName = NormalizeTypeName(runtimeType.Name ?? string.Empty);
            return string.Equals(runtimeFullName, normalizedRequested, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(runtimeName, normalizedRequested, StringComparison.OrdinalIgnoreCase) ||
                runtimeFullName.EndsWith("." + normalizedRequested, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDeclaringTypeSymbol(Type declaringType, string symbolText)
        {
            if (declaringType == null || string.IsNullOrEmpty(symbolText))
            {
                return false;
            }

            var normalizedSymbol = NormalizeTypeName(symbolText);
            if (string.IsNullOrEmpty(normalizedSymbol))
            {
                return false;
            }

            var declaringTypeName = NormalizeTypeName(declaringType.Name ?? string.Empty);
            var declaringTypeFullName = NormalizeTypeName(declaringType.FullName ?? string.Empty);
            return string.Equals(normalizedSymbol, declaringTypeName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedSymbol, declaringTypeFullName, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(declaringTypeFullName) &&
                declaringTypeFullName.EndsWith("." + normalizedSymbol, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeTypeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            value = value.Replace("global::", string.Empty).Replace('+', '.').Trim();
            var tickIndex = value.IndexOf('`');
            if (tickIndex >= 0)
            {
                value = value.Substring(0, tickIndex);
            }

            var genericIndex = value.IndexOf('<');
            if (genericIndex >= 0)
            {
                value = value.Substring(0, genericIndex);
            }

            return value;
        }

        private static string GetOperatorMethodName(string symbolText)
        {
            switch (NormalizeMethodName(symbolText))
            {
                case "+":
                    return "op_Addition";
                case "-":
                    return "op_Subtraction";
                case "*":
                    return "op_Multiply";
                case "/":
                    return "op_Division";
                case "%":
                    return "op_Modulus";
                case "==":
                    return "op_Equality";
                case "!=":
                    return "op_Inequality";
                case ">":
                    return "op_GreaterThan";
                case "<":
                    return "op_LessThan";
                case ">=":
                    return "op_GreaterThanOrEqual";
                case "<=":
                    return "op_LessThanOrEqual";
                case "!":
                    return "op_LogicalNot";
                case "true":
                    return "op_True";
                case "false":
                    return "op_False";
                default:
                    return string.Empty;
            }
        }

        private static bool TryBuildHarmonyPatchBinding(string text, int absolutePosition, string symbolText, out HarmonyPatchAttributeBinding binding)
        {
            binding = null;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var typeDeclarationStart = FindEnclosingTypeDeclarationStart(text, absolutePosition);
            if (typeDeclarationStart < 0)
            {
                return false;
            }

            var attributes = CollectPrecedingHarmonyPatchAttributes(text, typeDeclarationStart);
            if (attributes == null || attributes.Count == 0)
            {
                return false;
            }

            binding = new HarmonyPatchAttributeBinding();
            for (var i = 0; i < attributes.Count; i++)
            {
                MergeHarmonyPatchAttribute(attributes[i], binding);
            }

            if (string.IsNullOrEmpty(binding.TypeName) || string.IsNullOrEmpty(binding.MethodName))
            {
                return false;
            }

            var normalizedSymbol = NormalizeMethodName(symbolText);
            if (!string.IsNullOrEmpty(normalizedSymbol) &&
                !string.Equals(normalizedSymbol, binding.MethodName, StringComparison.Ordinal) &&
                !string.Equals(normalizedSymbol, NormalizeTypeName(binding.TypeName), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalizedSymbol, NormalizeTypeName(GetSimpleTypeName(binding.TypeName)), StringComparison.OrdinalIgnoreCase))
            {
                var typeName = FindTypeDeclarationName(text, typeDeclarationStart);
                if (!string.IsNullOrEmpty(typeName) &&
                    !string.Equals(normalizedSymbol, typeName, StringComparison.Ordinal) &&
                    !string.Equals(normalizedSymbol, NormalizeMethodName(typeName), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<string> CollectPrecedingHarmonyPatchAttributes(string text, int declarationStart)
        {
            var attributes = new List<string>();
            if (string.IsNullOrEmpty(text) || declarationStart <= 0)
            {
                return attributes;
            }

            var scan = declarationStart;
            while (scan > 0)
            {
                var end = scan - 1;
                while (end >= 0 && char.IsWhiteSpace(text[end]))
                {
                    end--;
                }

                if (end < 0 || text[end] != ']')
                {
                    break;
                }

                var start = FindMatchingAttributeOpen(text, end);
                if (start < 0)
                {
                    break;
                }

                var attributeText = text.Substring(start, end - start + 1);
                if (attributeText.IndexOf("HarmonyPatch", StringComparison.Ordinal) >= 0)
                {
                    attributes.Insert(0, attributeText);
                }

                scan = start;
            }

            return attributes;
        }

        private static int FindMatchingAttributeOpen(string text, int closeBracketIndex)
        {
            if (string.IsNullOrEmpty(text) || closeBracketIndex < 0 || closeBracketIndex >= text.Length || text[closeBracketIndex] != ']')
            {
                return -1;
            }

            var depth = 0;
            for (var i = closeBracketIndex; i >= 0; i--)
            {
                if (text[i] == ']')
                {
                    depth++;
                }
                else if (text[i] == '[')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static int FindEnclosingTypeDeclarationStart(string text, int absolutePosition)
        {
            if (string.IsNullOrEmpty(text))
            {
                return -1;
            }

            var safePosition = Math.Max(0, Math.Min(absolutePosition, text.Length));
            var lineStart = FindLineStart(text, safePosition);
            while (lineStart >= 0)
            {
                var lineEnd = FindLineEnd(text, lineStart);
                var line = text.Substring(lineStart, lineEnd - lineStart);
                if (IsTypeDeclarationLine(line))
                {
                    return lineStart;
                }

                if (lineStart == 0)
                {
                    break;
                }

                lineStart = FindPreviousLineStart(text, lineStart);
            }

            return -1;
        }

        private static bool IsTypeDeclarationLine(string line)
        {
            var normalized = NormalizeHeaderText(line);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            return normalized.IndexOf(" class ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf(" struct ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf(" interface ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.StartsWith("class ", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("struct ", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("interface ", StringComparison.OrdinalIgnoreCase);
        }

        private static int FindLineStart(string text, int position)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var index = Math.Max(0, Math.Min(position, text.Length));
            while (index > 0 && text[index - 1] != '\n')
            {
                index--;
            }

            return index;
        }

        private static int FindPreviousLineStart(string text, int currentLineStart)
        {
            if (string.IsNullOrEmpty(text) || currentLineStart <= 0)
            {
                return -1;
            }

            var index = Math.Max(0, currentLineStart - 1);
            if (index > 0 && text[index - 1] == '\r')
            {
                index--;
            }

            return FindLineStart(text, index);
        }

        private static int FindLineEnd(string text, int lineStart)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var index = Math.Max(0, Math.Min(lineStart, text.Length));
            while (index < text.Length && text[index] != '\n')
            {
                index++;
            }

            return index;
        }

        private static string FindTypeDeclarationName(string text, int declarationStart)
        {
            if (string.IsNullOrEmpty(text) || declarationStart < 0 || declarationStart >= text.Length)
            {
                return string.Empty;
            }

            var lineEnd = FindLineEnd(text, declarationStart);
            var line = NormalizeHeaderText(text.Substring(declarationStart, lineEnd - declarationStart));
            return ExtractTypeNameFromDeclaration(line);
        }

        private static string ExtractTypeNameFromDeclaration(string declarationLine)
        {
            if (string.IsNullOrEmpty(declarationLine))
            {
                return string.Empty;
            }

            var keywords = new[] { "class", "struct", "interface" };
            for (var i = 0; i < keywords.Length; i++)
            {
                var keyword = keywords[i];
                var index = declarationLine.IndexOf(keyword + " ", StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    continue;
                }

                var start = index + keyword.Length + 1;
                var end = start;
                while (end < declarationLine.Length &&
                    (char.IsLetterOrDigit(declarationLine[end]) || declarationLine[end] == '_' || declarationLine[end] == '@'))
                {
                    end++;
                }

                return end > start ? declarationLine.Substring(start, end - start).Trim() : string.Empty;
            }

            return string.Empty;
        }

        private static void MergeHarmonyPatchAttribute(string attributeText, HarmonyPatchAttributeBinding binding)
        {
            if (string.IsNullOrEmpty(attributeText) || binding == null)
            {
                return;
            }

            var searchIndex = 0;
            while (searchIndex < attributeText.Length)
            {
                var patchIndex = attributeText.IndexOf("HarmonyPatch", searchIndex, StringComparison.Ordinal);
                if (patchIndex < 0)
                {
                    break;
                }

                var openParen = attributeText.IndexOf('(', patchIndex);
                if (openParen < 0)
                {
                    break;
                }

                var closeParen = FindClosingParenthesis(attributeText, openParen);
                if (closeParen <= openParen)
                {
                    break;
                }

                ApplyHarmonyPatchArguments(attributeText.Substring(openParen + 1, closeParen - openParen - 1), binding);
                searchIndex = closeParen + 1;
            }
        }

        private static void ApplyHarmonyPatchArguments(string argumentsText, HarmonyPatchAttributeBinding binding)
        {
            if (string.IsNullOrEmpty(argumentsText) || binding == null)
            {
                return;
            }

            var arguments = SplitTopLevel(argumentsText);
            for (var i = 0; i < arguments.Length; i++)
            {
                var current = arguments[i] != null ? arguments[i].Trim() : string.Empty;
                if (string.IsNullOrEmpty(current))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(binding.TypeName))
                {
                    var typeName = ExtractTypeNameArgument(current);
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        binding.TypeName = typeName;
                        continue;
                    }
                }

                if (string.IsNullOrEmpty(binding.MethodName))
                {
                    var methodName = ExtractMethodNameArgument(current);
                    if (!string.IsNullOrEmpty(methodName))
                    {
                        binding.MethodName = methodName;
                        continue;
                    }
                }

                if ((binding.ParameterTypeNames == null || binding.ParameterTypeNames.Length == 0) &&
                    current.IndexOf("new Type", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var parameterTypeNames = ExtractParameterTypeArray(current);
                    if (parameterTypeNames.Length > 0)
                    {
                        binding.ParameterTypeNames = parameterTypeNames;
                    }
                }
            }
        }

        private static string ExtractTypeNameArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return string.Empty;
            }

            var trimmed = argument.Trim();
            if (!trimmed.StartsWith("typeof(", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var openParen = trimmed.IndexOf('(');
            var closeParen = FindClosingParenthesis(trimmed, openParen);
            if (closeParen <= openParen)
            {
                return string.Empty;
            }

            return NormalizeTypeName(trimmed.Substring(openParen + 1, closeParen - openParen - 1));
        }

        private static string ExtractMethodNameArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return string.Empty;
            }

            var trimmed = argument.Trim();
            if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal) && trimmed.Length >= 2)
            {
                return trimmed.Substring(1, trimmed.Length - 2);
            }

            if (!trimmed.StartsWith("nameof(", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var openParen = trimmed.IndexOf('(');
            var closeParen = FindClosingParenthesis(trimmed, openParen);
            if (closeParen <= openParen)
            {
                return string.Empty;
            }

            var value = trimmed.Substring(openParen + 1, closeParen - openParen - 1).Trim();
            var lastDot = value.LastIndexOf('.');
            if (lastDot >= 0 && lastDot + 1 < value.Length)
            {
                value = value.Substring(lastDot + 1);
            }

            return NormalizeMethodName(value);
        }

        private static string[] ExtractParameterTypeArray(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return new string[0];
            }

            var results = new List<string>();
            var searchIndex = 0;
            while (searchIndex < argument.Length)
            {
                var typeofIndex = argument.IndexOf("typeof(", searchIndex, StringComparison.Ordinal);
                if (typeofIndex < 0)
                {
                    break;
                }

                var openParen = argument.IndexOf('(', typeofIndex);
                var closeParen = FindClosingParenthesis(argument, openParen);
                if (closeParen <= openParen)
                {
                    break;
                }

                var typeName = NormalizeTypeName(argument.Substring(openParen + 1, closeParen - openParen - 1));
                if (!string.IsNullOrEmpty(typeName))
                {
                    results.Add(typeName);
                }

                searchIndex = closeParen + 1;
            }

            return results.ToArray();
        }

        private static bool TryResolveRuntimeTypeByName(string typeName, out string assemblyPath, out Type runtimeType)
        {
            assemblyPath = string.Empty;
            runtimeType = null;
            var normalizedTypeName = NormalizeTypeName(typeName);
            if (string.IsNullOrEmpty(normalizedTypeName))
            {
                return false;
            }

            var bestScore = -1;
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var assemblyIndex = 0; assemblyIndex < loadedAssemblies.Length; assemblyIndex++)
            {
                var assembly = loadedAssemblies[assemblyIndex];
                if (assembly == null)
                {
                    continue;
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }

                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    var current = types[typeIndex];
                    if (current == null)
                    {
                        continue;
                    }

                    var score = ScoreRuntimeTypeMatch(current, normalizedTypeName);
                    if (score < 0)
                    {
                        continue;
                    }

                    if (score > bestScore || (score == bestScore && string.Equals(assembly.GetName().Name, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase)))
                    {
                        bestScore = score;
                        runtimeType = current;
                    }
                }
            }

            if (runtimeType == null)
            {
                return false;
            }

            try
            {
                assemblyPath = runtimeType.Assembly.Location;
            }
            catch
            {
                assemblyPath = string.Empty;
            }

            return !string.IsNullOrEmpty(assemblyPath);
        }

        private static int ScoreRuntimeTypeMatch(Type runtimeType, string normalizedTypeName)
        {
            if (runtimeType == null || string.IsNullOrEmpty(normalizedTypeName))
            {
                return -1;
            }

            var fullName = NormalizeTypeName(runtimeType.FullName ?? runtimeType.Name ?? string.Empty);
            var name = NormalizeTypeName(runtimeType.Name ?? string.Empty);
            if (string.Equals(fullName, normalizedTypeName, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (string.Equals(name, normalizedTypeName, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return fullName.EndsWith("." + normalizedTypeName, StringComparison.OrdinalIgnoreCase)
                ? 1
                : -1;
        }

        private static string GetSimpleTypeName(string typeName)
        {
            var normalized = NormalizeTypeName(typeName);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            var lastDot = normalized.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < normalized.Length
                ? normalized.Substring(lastDot + 1)
                : normalized;
        }

        private static bool TryBuildLookupHint(CortexShellState state, string documentPath, int absolutePosition, string symbolText, out MethodLookupHint hint)
        {
            hint = null;
            var text = GetDocumentText(state, documentPath);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var safePosition = Math.Max(0, Math.Min(absolutePosition, Math.Max(0, text.Length - 1)));
            if (TryBuildEnclosingMethodLookupHint(text, safePosition, out hint) ||
                TryBuildForwardMethodLookupHint(text, safePosition, out hint) ||
                TryBuildNearestMethodLookupHint(text, safePosition, symbolText, out hint))
            {
                return hint != null;
            }

            return false;
        }

        private static bool TryBuildNearestMethodLookupHint(string text, int safePosition, string symbolText, out MethodLookupHint hint)
        {
            hint = null;
            var openParen = FindNearestCharacter(text, safePosition, '(', 256);
            if (openParen < 0)
            {
                return false;
            }

            var closeParen = FindClosingParenthesis(text, openParen);
            if (closeParen <= openParen)
            {
                return false;
            }

            hint = new MethodLookupHint();
            hint.Name = ExtractMemberName(text, openParen);
            if (string.IsNullOrEmpty(hint.Name))
            {
                hint.Name = NormalizeMethodName(symbolText);
            }

            hint.ParameterTypeNames = ParseParameterTypeNames(text.Substring(openParen + 1, closeParen - openParen - 1));
            hint.ParameterCount = hint.ParameterTypeNames != null ? hint.ParameterTypeNames.Length : 0;
            if (string.Equals(symbolText, ".cctor", StringComparison.Ordinal) || string.Equals(hint.Name, ".cctor", StringComparison.Ordinal))
            {
                hint.IsStaticConstructor = true;
                hint.Name = ".cctor";
            }
            else if (string.Equals(symbolText, ".ctor", StringComparison.Ordinal) || string.Equals(hint.Name, ".ctor", StringComparison.Ordinal))
            {
                hint.IsConstructor = true;
                hint.Name = ".ctor";
            }

            return true;
        }

        private static bool TryBuildEnclosingMethodLookupHint(string text, int safePosition, out MethodLookupHint hint)
        {
            hint = null;
            var openBraces = BuildOpenBraceStack(text, safePosition);
            for (var i = openBraces.Count - 1; i >= 0; i--)
            {
                if (TryBuildLookupHintFromOpenBrace(text, openBraces[i], openBraces, i, out hint))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildForwardMethodLookupHint(string text, int safePosition, out MethodLookupHint hint)
        {
            hint = null;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var searchEnd = Math.Min(text.Length, safePosition + 320);
            for (var i = safePosition; i < searchEnd; i++)
            {
                if (text[i] != '{')
                {
                    continue;
                }

                var between = text.Substring(safePosition, i - safePosition);
                if (between.IndexOf(';') >= 0 || between.IndexOf('}') >= 0)
                {
                    return false;
                }

                string header;
                if (!TryExtractDeclarationHeader(text, i, out header))
                {
                    continue;
                }

                var normalizedHeader = NormalizeHeaderText(header);
                if (IsTypeDeclarationHeader(normalizedHeader) || IsControlBlockHeader(normalizedHeader))
                {
                    return false;
                }

                if (TryBuildLookupHintFromOpenBrace(text, i, null, -1, out hint))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<int> BuildOpenBraceStack(string text, int position)
        {
            var stack = new List<int>();
            if (string.IsNullOrEmpty(text))
            {
                return stack;
            }

            var end = Math.Max(0, Math.Min(position, text.Length - 1));
            for (var i = 0; i <= end; i++)
            {
                if (text[i] == '{')
                {
                    stack.Add(i);
                }
                else if (text[i] == '}' && stack.Count > 0)
                {
                    stack.RemoveAt(stack.Count - 1);
                }
            }

            return stack;
        }

        private static bool TryBuildLookupHintFromOpenBrace(string text, int openBraceIndex, IList<int> openBraces, int braceStackIndex, out MethodLookupHint hint)
        {
            hint = null;
            string header;
            if (!TryExtractDeclarationHeader(text, openBraceIndex, out header))
            {
                return false;
            }

            var normalizedHeader = NormalizeHeaderText(header);
            if (string.IsNullOrEmpty(normalizedHeader) ||
                IsTypeDeclarationHeader(normalizedHeader) ||
                IsControlBlockHeader(normalizedHeader))
            {
                return false;
            }

            var accessorKind = GetAccessorKind(normalizedHeader);
            if (!string.IsNullOrEmpty(accessorKind))
            {
                return TryBuildAccessorLookupHint(text, openBraces, braceStackIndex, accessorKind, out hint);
            }

            return TryCreateMethodLookupHint(normalizedHeader, out hint);
        }

        private static bool TryBuildAccessorLookupHint(string text, IList<int> openBraces, int braceStackIndex, string accessorKind, out MethodLookupHint hint)
        {
            hint = null;
            if (openBraces == null || braceStackIndex <= 0)
            {
                return false;
            }

            for (var i = braceStackIndex - 1; i >= 0; i--)
            {
                string propertyHeader;
                if (!TryExtractDeclarationHeader(text, openBraces[i], out propertyHeader))
                {
                    continue;
                }

                propertyHeader = NormalizeHeaderText(propertyHeader);
                if (string.IsNullOrEmpty(propertyHeader) ||
                    IsTypeDeclarationHeader(propertyHeader) ||
                    IsControlBlockHeader(propertyHeader) ||
                    propertyHeader.IndexOf('(') >= 0)
                {
                    continue;
                }

                var propertyName = ExtractPropertyLikeMemberName(propertyHeader);
                if (string.IsNullOrEmpty(propertyName))
                {
                    continue;
                }

                hint = new MethodLookupHint
                {
                    Name = propertyName,
                    ParameterCount = string.Equals(accessorKind, "set", StringComparison.Ordinal) ||
                        string.Equals(accessorKind, "add", StringComparison.Ordinal) ||
                        string.Equals(accessorKind, "remove", StringComparison.Ordinal)
                        ? 1
                        : 0
                };
                return true;
            }

            return false;
        }

        private static bool TryCreateMethodLookupHint(string header, out MethodLookupHint hint)
        {
            hint = null;
            if (string.IsNullOrEmpty(header))
            {
                return false;
            }

            var openParen = header.IndexOf('(');
            if (openParen < 0)
            {
                return false;
            }

            var closeParen = FindClosingParenthesis(header, openParen);
            if (closeParen <= openParen)
            {
                return false;
            }

            var methodName = ExtractHeaderMemberName(header, openParen);
            if (string.IsNullOrEmpty(methodName))
            {
                return false;
            }

            hint = new MethodLookupHint();
            hint.Name = methodName;
            hint.ParameterTypeNames = ParseParameterTypeNames(header.Substring(openParen + 1, closeParen - openParen - 1));
            hint.ParameterCount = hint.ParameterTypeNames != null ? hint.ParameterTypeNames.Length : 0;

            if (string.Equals(methodName, ".cctor", StringComparison.Ordinal))
            {
                hint.IsStaticConstructor = true;
            }
            else if (string.Equals(methodName, ".ctor", StringComparison.Ordinal))
            {
                hint.IsConstructor = true;
            }

            return true;
        }

        private static bool TryExtractDeclarationHeader(string text, int openBraceIndex, out string header)
        {
            header = string.Empty;
            if (string.IsNullOrEmpty(text) || openBraceIndex <= 0 || openBraceIndex > text.Length)
            {
                return false;
            }

            var start = Math.Max(0, openBraceIndex - 512);
            var window = text.Substring(start, openBraceIndex - start);
            var lastDelimiter = FindLastHeaderDelimiter(window);
            if (lastDelimiter >= 0 && lastDelimiter + 1 < window.Length)
            {
                window = window.Substring(lastDelimiter + 1);
            }

            header = window.Trim();
            return !string.IsNullOrEmpty(header);
        }

        private static int FindLastHeaderDelimiter(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return -1;
            }

            for (var i = text.Length - 1; i >= 0; i--)
            {
                if (text[i] == ';' || text[i] == '{' || text[i] == '}')
                {
                    return i;
                }
            }

            return -1;
        }

        private static string NormalizeHeaderText(string header)
        {
            if (string.IsNullOrEmpty(header))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(header.Length);
            var lastWasWhitespace = false;
            for (var i = 0; i < header.Length; i++)
            {
                var current = header[i];
                if (char.IsWhiteSpace(current))
                {
                    if (!lastWasWhitespace)
                    {
                        builder.Append(' ');
                        lastWasWhitespace = true;
                    }

                    continue;
                }

                builder.Append(current);
                lastWasWhitespace = false;
            }

            return builder.ToString().Trim();
        }

        private static bool IsTypeDeclarationHeader(string header)
        {
            return StartsWithWord(header, "namespace") ||
                StartsWithWord(header, "class") ||
                StartsWithWord(header, "struct") ||
                StartsWithWord(header, "interface") ||
                StartsWithWord(header, "enum") ||
                StartsWithWord(header, "record") ||
                header.IndexOf(" class ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                header.IndexOf(" struct ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                header.IndexOf(" interface ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                header.IndexOf(" enum ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                header.IndexOf(" record ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsControlBlockHeader(string header)
        {
            return StartsWithWord(header, "if") ||
                StartsWithWord(header, "else") ||
                StartsWithWord(header, "for") ||
                StartsWithWord(header, "foreach") ||
                StartsWithWord(header, "while") ||
                StartsWithWord(header, "switch") ||
                StartsWithWord(header, "lock") ||
                StartsWithWord(header, "using") ||
                StartsWithWord(header, "catch") ||
                StartsWithWord(header, "try") ||
                StartsWithWord(header, "finally") ||
                StartsWithWord(header, "do");
        }

        private static string GetAccessorKind(string header)
        {
            if (string.IsNullOrEmpty(header))
            {
                return string.Empty;
            }

            if (string.Equals(header, "get", StringComparison.OrdinalIgnoreCase))
            {
                return "get";
            }

            if (string.Equals(header, "set", StringComparison.OrdinalIgnoreCase))
            {
                return "set";
            }

            if (string.Equals(header, "add", StringComparison.OrdinalIgnoreCase))
            {
                return "add";
            }

            if (string.Equals(header, "remove", StringComparison.OrdinalIgnoreCase))
            {
                return "remove";
            }

            return string.Empty;
        }

        private static string ExtractPropertyLikeMemberName(string header)
        {
            if (string.IsNullOrEmpty(header))
            {
                return string.Empty;
            }

            var normalized = NormalizeHeaderText(header);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            var lastSpace = normalized.LastIndexOf(' ');
            var candidate = lastSpace >= 0 ? normalized.Substring(lastSpace + 1) : normalized;
            var genericIndex = candidate.IndexOf('<');
            if (genericIndex >= 0)
            {
                candidate = candidate.Substring(0, genericIndex);
            }

            return candidate.Trim();
        }

        private static string ExtractHeaderMemberName(string header, int openParen)
        {
            if (string.IsNullOrEmpty(header) || openParen <= 0)
            {
                return string.Empty;
            }

            var prefix = header.Substring(0, openParen).Trim();
            if (string.IsNullOrEmpty(prefix))
            {
                return string.Empty;
            }

            var operatorIndex = prefix.LastIndexOf("operator ", StringComparison.Ordinal);
            if (operatorIndex >= 0)
            {
                return MapOperatorMethodName(prefix.Substring(operatorIndex + "operator ".Length).Trim());
            }

            if (prefix.EndsWith(".this", StringComparison.Ordinal) || prefix.EndsWith(" this", StringComparison.Ordinal))
            {
                return "Item";
            }

            var lastSpace = prefix.LastIndexOf(' ');
            var candidate = lastSpace >= 0 ? prefix.Substring(lastSpace + 1) : prefix;
            var genericIndex = candidate.IndexOf('<');
            if (genericIndex >= 0)
            {
                candidate = candidate.Substring(0, genericIndex);
            }

            if (string.Equals(candidate, "this", StringComparison.Ordinal))
            {
                return "Item";
            }

            return candidate.Trim();
        }

        private static string MapOperatorMethodName(string operatorToken)
        {
            switch ((operatorToken ?? string.Empty).Trim())
            {
                case "+":
                    return "op_Addition";
                case "-":
                    return "op_Subtraction";
                case "*":
                    return "op_Multiply";
                case "/":
                    return "op_Division";
                case "%":
                    return "op_Modulus";
                case "==":
                    return "op_Equality";
                case "!=":
                    return "op_Inequality";
                case ">":
                    return "op_GreaterThan";
                case "<":
                    return "op_LessThan";
                case ">=":
                    return "op_GreaterThanOrEqual";
                case "<=":
                    return "op_LessThanOrEqual";
                case "!":
                    return "op_LogicalNot";
                case "true":
                    return "op_True";
                case "false":
                    return "op_False";
                case "implicit":
                    return "op_Implicit";
                case "explicit":
                    return "op_Explicit";
                default:
                    return string.Empty;
            }
        }

        private static bool StartsWithWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word) ||
                !text.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return text.Length == word.Length || char.IsWhiteSpace(text[word.Length]) || text[word.Length] == '(';
        }

        private static string[] ParseParameterTypeNames(string parameterList)
        {
            if (string.IsNullOrEmpty(parameterList))
            {
                return new string[0];
            }

            var items = SplitTopLevel(parameterList);
            var results = new string[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                results[i] = ExtractParameterTypeName(items[i]);
            }

            return results;
        }

        private static string[] SplitTopLevel(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new string[0];
            }

            var values = new System.Collections.Generic.List<string>();
            var depth = 0;
            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var current = text[i];
                if (current == '<' || current == '[' || current == '(')
                {
                    depth++;
                }
                else if (current == '>' || current == ']' || current == ')')
                {
                    depth = Math.Max(0, depth - 1);
                }
                else if (current == ',' && depth == 0)
                {
                    values.Add(text.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }

            values.Add(text.Substring(start).Trim());
            return values.ToArray();
        }

        private static string ExtractParameterTypeName(string parameterText)
        {
            if (string.IsNullOrEmpty(parameterText))
            {
                return string.Empty;
            }

            var text = parameterText;
            var equalsIndex = text.IndexOf('=');
            if (equalsIndex >= 0)
            {
                text = text.Substring(0, equalsIndex).Trim();
            }

            text = text.Replace("ref ", string.Empty)
                .Replace("out ", string.Empty)
                .Replace("params ", string.Empty)
                .Replace("this ", string.Empty)
                .Trim();

            var lastSpace = text.LastIndexOf(' ');
            return lastSpace > 0 ? NormalizeTypeName(text.Substring(0, lastSpace)) : NormalizeTypeName(text);
        }

        private static int FindNearestCharacter(string text, int position, char value, int searchWindow)
        {
            var start = Math.Max(0, position - searchWindow);
            var end = Math.Min(text.Length - 1, position + searchWindow);
            for (var i = position; i <= end; i++)
            {
                if (text[i] == value)
                {
                    return i;
                }
            }

            for (var i = position; i >= start; i--)
            {
                if (text[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindClosingParenthesis(string text, int openParen)
        {
            if (string.IsNullOrEmpty(text) || openParen < 0 || openParen >= text.Length || text[openParen] != '(')
            {
                return -1;
            }

            var depth = 0;
            for (var i = openParen; i < text.Length; i++)
            {
                if (text[i] == '(')
                {
                    depth++;
                }
                else if (text[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static string ExtractMemberName(string text, int openParen)
        {
            if (string.IsNullOrEmpty(text) || openParen <= 0)
            {
                return string.Empty;
            }

            var end = openParen - 1;
            while (end >= 0 && char.IsWhiteSpace(text[end]))
            {
                end--;
            }

            if (end < 0)
            {
                return string.Empty;
            }

            var start = end;
            while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '_' || text[start] == '.' || text[start] == '@'))
            {
                start--;
            }

            return text.Substring(start + 1, end - start).Trim();
        }

        private static string GetDocumentText(CortexShellState state, string documentPath)
        {
            var session = CortexModuleUtil.FindOpenDocument(state, documentPath);
            if (session != null)
            {
                return session.Text ?? string.Empty;
            }

            try
            {
                return !string.IsNullOrEmpty(documentPath) && File.Exists(documentPath)
                    ? File.ReadAllText(documentPath)
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class MethodLookupHint
        {
            public string Name = string.Empty;
            public int ParameterCount = -1;
            public string[] ParameterTypeNames = new string[0];
            public bool IsConstructor;
            public bool IsStaticConstructor;
        }
    }
}
