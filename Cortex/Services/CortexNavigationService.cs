using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;

namespace Cortex.Services
{
    public sealed class CortexNavigationService
    {
        private readonly IDocumentService _documentService;
        private readonly ISourceReferenceService _sourceReferenceService;
        private readonly IRuntimeSourceNavigationService _runtimeSourceNavigationService;
        private readonly ISourceLookupIndex _sourceLookupIndex;

        public CortexNavigationService(
            IDocumentService documentService,
            ISourceReferenceService sourceReferenceService,
            IRuntimeSourceNavigationService runtimeSourceNavigationService)
            : this(documentService, sourceReferenceService, runtimeSourceNavigationService, null)
        {
        }

        public CortexNavigationService(
            IDocumentService documentService,
            ISourceReferenceService sourceReferenceService,
            IRuntimeSourceNavigationService runtimeSourceNavigationService,
            ISourceLookupIndex sourceLookupIndex)
        {
            _documentService = documentService;
            _sourceReferenceService = sourceReferenceService;
            _runtimeSourceNavigationService = runtimeSourceNavigationService;
            _sourceLookupIndex = sourceLookupIndex;
        }

        public DocumentSession OpenDocument(CortexShellState state, string filePath, int highlightedLine, string successStatusMessage, string failureStatusMessage)
        {
            var opened = CortexModuleUtil.OpenDocument(_documentService, state, filePath, highlightedLine);
            if (opened != null)
            {
                state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
                if (!string.IsNullOrEmpty(successStatusMessage))
                {
                    state.StatusMessage = successStatusMessage;
                }

                return opened;
            }

            if (!string.IsNullOrEmpty(failureStatusMessage))
            {
                state.StatusMessage = failureStatusMessage;
            }

            return null;
        }

        public DecompilerResponse RequestDecompilerSource(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache)
        {
            return CortexModuleUtil.RequestDecompilerSource(
                _sourceReferenceService,
                state,
                assemblyPath,
                metadataToken,
                entityKind,
                ignoreCache);
        }

        public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, string successStatusMessage, string failureStatusMessage)
        {
            var opened = CortexModuleUtil.OpenDecompilerResult(_documentService, state, response);
            if (opened)
            {
                if (!string.IsNullOrEmpty(successStatusMessage))
                {
                    state.StatusMessage = successStatusMessage;
                }

                return true;
            }

            if (!string.IsNullOrEmpty(failureStatusMessage))
            {
                state.StatusMessage = failureStatusMessage;
            }

            return false;
        }

        public bool DecompileAndOpen(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage)
        {
            var response = RequestDecompilerSource(state, assemblyPath, metadataToken, entityKind, ignoreCache);
            if (response == null)
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            return OpenDecompilerResult(state, response, successStatusMessage, failureStatusMessage);
        }

        public SourceNavigationTarget ResolveRuntimeTarget(RuntimeLogEntry entry, int frameIndex, CortexShellState state)
        {
            return _runtimeSourceNavigationService != null
                ? _runtimeSourceNavigationService.Resolve(entry, frameIndex, state.SelectedProject, state.Settings)
                : null;
        }

        public bool OpenRuntimeTarget(CortexShellState state, SourceNavigationTarget target, string successStatusMessage, string failureStatusMessage)
        {
            if (target == null || !target.Success || string.IsNullOrEmpty(target.FilePath))
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }
                else if (target != null && !string.IsNullOrEmpty(target.StatusMessage))
                {
                    state.StatusMessage = target.StatusMessage;
                }

                return false;
            }

            return OpenDocument(
                state,
                target.FilePath,
                target.LineNumber,
                !string.IsNullOrEmpty(successStatusMessage) ? successStatusMessage : target.StatusMessage,
                failureStatusMessage) != null;
        }

        public bool OpenHoverDisplayPart(CortexShellState state, LanguageServiceHoverDisplayPart part, string successStatusMessage, string failureStatusMessage)
        {
            if (part == null || !part.IsInteractive)
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            return OpenLanguageSymbolTarget(
                state,
                part.SymbolDisplay,
                part.SymbolKind,
                part.MetadataName,
                part.ContainingTypeName,
                part.ContainingAssemblyName,
                part.DocumentationCommentId,
                part.DefinitionDocumentPath,
                part.DefinitionRange,
                successStatusMessage,
                failureStatusMessage);
        }

        public bool OpenLanguageSymbolTarget(
            CortexShellState state,
            string symbolDisplay,
            string symbolKind,
            string metadataName,
            string containingTypeName,
            string containingAssemblyName,
            string documentationCommentId,
            string definitionDocumentPath,
            LanguageServiceRange definitionRange,
            string successStatusMessage,
            string failureStatusMessage)
        {
            var displayName = !string.IsNullOrEmpty(symbolDisplay) ? symbolDisplay : metadataName ?? string.Empty;
            var lineNumber = definitionRange != null ? definitionRange.StartLine : 0;
            if (!string.IsNullOrEmpty(definitionDocumentPath) && File.Exists(definitionDocumentPath))
            {
                return OpenDocument(
                    state,
                    definitionDocumentPath,
                    lineNumber,
                    successStatusMessage,
                    failureStatusMessage) != null;
            }

            string assemblyPath;
            if (!TryResolveAssemblyPath(state, containingAssemblyName, out assemblyPath))
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            int metadataToken;
            DecompilerEntityKind entityKind;
            if (!TryResolveMetadataTarget(assemblyPath, documentationCommentId, containingTypeName, symbolKind, out metadataToken, out entityKind))
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            return DecompileAndOpen(
                state,
                assemblyPath,
                metadataToken,
                entityKind,
                false,
                !string.IsNullOrEmpty(successStatusMessage) ? successStatusMessage : "Opened decompiled definition: " + displayName,
                failureStatusMessage);
        }

        private bool TryResolveAssemblyPath(CortexShellState state, string assemblyName, out string assemblyPath)
        {
            assemblyPath = string.Empty;
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                var assembly = loadedAssemblies[i];
                if (assembly == null || !string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    assemblyPath = assembly.Location;
                }
                catch
                {
                    assemblyPath = string.Empty;
                }

                if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
                {
                    return true;
                }
            }

            var searchRoots = new List<string>();
            AddSearchRoot(searchRoots, AppDomain.CurrentDomain.BaseDirectory);
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
            var smmRoot = Path.Combine(baseDirectory, "SMM");
            var smmBinRoot = Path.Combine(smmRoot, "bin");
            AddSearchRoot(searchRoots, smmRoot);
            AddSearchRoot(searchRoots, smmBinRoot);
            AddSearchRoot(searchRoots, Path.Combine(smmBinRoot, "decompiler"));
            if (state != null && state.Settings != null)
            {
                var configuredRoots = SourceRootSetBuilder.Build(state.SelectedProject, state.Settings, SourceRootSetBuilder.LanguageServiceRoots);
                for (var i = 0; i < configuredRoots.Count; i++)
                {
                    AddSearchRoot(searchRoots, configuredRoots[i]);
                }

                if (!string.IsNullOrEmpty(state.Settings.DecompilerCachePath))
                {
                    AddSearchRoot(searchRoots, state.Settings.DecompilerCachePath);
                }
            }

            assemblyPath = _sourceLookupIndex != null
                ? _sourceLookupIndex.ResolveAssemblyPath(searchRoots, assemblyName)
                : string.Empty;
            return !string.IsNullOrEmpty(assemblyPath);
        }

        private static void AddSearchRoot(List<string> roots, string root)
        {
            if (roots == null || string.IsNullOrEmpty(root))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(root);
                if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
                {
                    return;
                }

                for (var i = 0; i < roots.Count; i++)
                {
                    if (string.Equals(roots[i], fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                roots.Add(fullPath);
            }
            catch
            {
            }
        }

        private static bool TryResolveMetadataTarget(string assemblyPath, string documentationCommentId, string containingTypeName, string symbolKind, out int metadataToken, out DecompilerEntityKind entityKind)
        {
            metadataToken = 0;
            entityKind = DecompilerEntityKind.Type;

            var assembly = LoadAssemblyForMetadata(assemblyPath);
            if (assembly == null)
            {
                return false;
            }

            var normalizedContainingTypeName = NormalizeMetadataTypeName(containingTypeName);
            if (IsTypeLikeSymbol(symbolKind))
            {
                var type = ResolveTypeByName(
                    assembly,
                    !string.IsNullOrEmpty(documentationCommentId) && documentationCommentId.StartsWith("T:", StringComparison.Ordinal)
                        ? documentationCommentId.Substring(2)
                        : normalizedContainingTypeName);
                if (type == null)
                {
                    return false;
                }

                metadataToken = type.MetadataToken;
                entityKind = DecompilerEntityKind.Type;
                return true;
            }

            if (!string.IsNullOrEmpty(documentationCommentId) && documentationCommentId.StartsWith("M:", StringComparison.Ordinal))
            {
                var method = ResolveMethodByDocumentationId(assembly, documentationCommentId);
                if (method != null)
                {
                    metadataToken = method.MetadataToken;
                    entityKind = DecompilerEntityKind.Method;
                    return true;
                }
            }

            var containingType = ResolveTypeByName(assembly, normalizedContainingTypeName);
            if (containingType == null)
            {
                return false;
            }

            metadataToken = containingType.MetadataToken;
            entityKind = DecompilerEntityKind.Type;
            return true;
        }

        private static Assembly LoadAssemblyForMetadata(string assemblyPath)
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

        private static Type ResolveTypeByName(Assembly assembly, string typeName)
        {
            if (assembly == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            var normalized = NormalizeMetadataTypeName(typeName);
            try
            {
                var direct = assembly.GetType(normalized, false);
                if (direct != null)
                {
                    return direct;
                }

                var allTypes = assembly.GetTypes();
                for (var i = 0; i < allTypes.Length; i++)
                {
                    var type = allTypes[i];
                    if (type != null &&
                        string.Equals((type.FullName ?? type.Name).Replace('+', '.'), normalized, StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static MethodBase ResolveMethodByDocumentationId(Assembly assembly, string documentationCommentId)
        {
            if (assembly == null || string.IsNullOrEmpty(documentationCommentId))
            {
                return null;
            }

            try
            {
                var allTypes = assembly.GetTypes();
                for (var typeIndex = 0; typeIndex < allTypes.Length; typeIndex++)
                {
                    var type = allTypes[typeIndex];
                    if (type == null)
                    {
                        continue;
                    }

                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                    var methods = type.GetMethods(flags);
                    for (var methodIndex = 0; methodIndex < methods.Length; methodIndex++)
                    {
                        if (string.Equals(BuildMethodDocumentationId(methods[methodIndex]), documentationCommentId, StringComparison.Ordinal))
                        {
                            return methods[methodIndex];
                        }
                    }

                    var constructors = type.GetConstructors(flags);
                    for (var ctorIndex = 0; ctorIndex < constructors.Length; ctorIndex++)
                    {
                        if (string.Equals(BuildMethodDocumentationId(constructors[ctorIndex]), documentationCommentId, StringComparison.Ordinal))
                        {
                            return constructors[ctorIndex];
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string BuildMethodDocumentationId(MethodBase method)
        {
            if (method == null || method.DeclaringType == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append("M:");
            builder.Append(GetXmlTypeName(method.DeclaringType));
            builder.Append(".");
            builder.Append(method.Name);

            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                builder.Append("(");
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(",");
                    }

                    builder.Append(GetXmlTypeName(parameters[i].ParameterType));
                }
                builder.Append(")");
            }

            return builder.ToString();
        }

        private static string GetXmlTypeName(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (type.IsByRef)
            {
                return GetXmlTypeName(type.GetElementType()) + "@";
            }

            if (type.IsArray)
            {
                return GetXmlTypeName(type.GetElementType()) + "[]";
            }

            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                var baseName = (genericType.FullName ?? genericType.Name).Replace('+', '.');
                var tickIndex = baseName.IndexOf('`');
                if (tickIndex >= 0)
                {
                    baseName = baseName.Substring(0, tickIndex);
                }

                var args = type.GetGenericArguments();
                var builder = new StringBuilder();
                builder.Append(baseName);
                builder.Append("{");
                for (var i = 0; i < args.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(",");
                    }

                    builder.Append(GetXmlTypeName(args[i]));
                }
                builder.Append("}");
                return builder.ToString();
            }

            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        private static string NormalizeMetadataTypeName(string typeName)
        {
            return string.IsNullOrEmpty(typeName)
                ? string.Empty
                : typeName.Replace("global::", string.Empty).Replace('+', '.');
        }

        private static bool IsTypeLikeSymbol(string symbolKind)
        {
            return string.Equals(symbolKind, "NamedType", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Namespace", StringComparison.OrdinalIgnoreCase);
        }
    }
}
