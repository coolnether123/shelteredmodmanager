using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cortex.Core.Diagnostics;
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;
using Cortex.Plugin.Harmony.Services.Resolution;

namespace Cortex.Plugin.Harmony.Services
{
    internal sealed class HarmonyMethodResolver
    {
        private static readonly CortexLogger Log = CortexLog.ForSource("Cortex.Harmony");
        private readonly HarmonySourceSymbolService _sourceSymbolService;
        private readonly IHarmonyRuntimeMethodLookupService _runtimeMethodLookupService;

        public HarmonyMethodResolver()
            : this(new HarmonySourceSymbolService(), new HarmonyRuntimeMethodLookupService())
        {
        }

        internal HarmonyMethodResolver(
            HarmonySourceSymbolService sourceSymbolService,
            IHarmonyRuntimeMethodLookupService runtimeMethodLookupService)
        {
            _sourceSymbolService = sourceSymbolService ?? new HarmonySourceSymbolService();
            _runtimeMethodLookupService = runtimeMethodLookupService ?? new HarmonyRuntimeMethodLookupService();
        }

        public bool TryResolveMethod(IWorkbenchModuleRuntime runtime, EditorCommandTarget target, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = "Select a resolvable method before using Harmony actions.";
            if (runtime == null || target == null)
            {
                return false;
            }

            if (TryResolveFromSourcePatch(runtime, target, out resolvedTarget, out reason))
            {
                return true;
            }

            if (TryResolveFromMetadata(runtime, target, out resolvedTarget, out reason))
            {
                return true;
            }

            if (TryResolveFromProjectOutput(runtime, target, out resolvedTarget, out reason))
            {
                return true;
            }

            return false;
        }

        public bool TryResolveMethod(IWorkbenchModuleRuntime runtime, HarmonyPatchInspectionRequest request, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = "Harmony inspection metadata was incomplete.";
            if (runtime == null || request == null || string.IsNullOrEmpty(request.AssemblyPath))
            {
                return false;
            }

            var assembly = LoadAssembly(request.AssemblyPath, string.Empty, runtime);
            if (assembly == null)
            {
                reason = "The target assembly could not be loaded for Harmony inspection.";
                return false;
            }

            MethodBase method = null;
            if (request.MetadataToken > 0)
            {
                try
                {
                    method = assembly.ManifestModule.ResolveMethod(request.MetadataToken);
                }
                catch
                {
                    method = null;
                }
            }

            if (method == null)
            {
                Type declaringType;
                if (!TryResolveDeclaringType(assembly, request.DeclaringTypeName, out declaringType) || declaringType == null)
                {
                    reason = "The declaring type could not be resolved from the target assembly.";
                    return false;
                }

                var hint = BuildLookupHint(request.DocumentationCommentId, request.MethodName, request.Signature);
                method = _runtimeMethodLookupService.ResolveMethod(declaringType, request.MethodName, hint, out reason);
            }

            if (method == null)
            {
                if (string.IsNullOrEmpty(reason))
                {
                    reason = "The runtime method could not be resolved from the target assembly.";
                }

                return false;
            }

            resolvedTarget = CreateResolvedTarget(runtime, request, method);
            return true;
        }

        public bool TryResolveType(IWorkbenchModuleRuntime runtime, EditorCommandTarget target, out HarmonyResolvedTypeTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = "Select a decompiled type before viewing Harmony patches in that area.";
            if (runtime == null || target == null)
            {
                return false;
            }

            string typeName;
            if (!TryResolveTypeName(target, out typeName))
            {
                reason = "The declaring type could not be inferred for the selected symbol.";
                return false;
            }

            var candidates = ResolveAssemblyCandidates(runtime, target);
            if (candidates.Count == 0)
            {
                reason = "The containing assembly could not be located for the selected symbol.";
                WriteResolutionTrace("Type resolution failed: no assembly candidates. " + BuildTargetDiagnosticText(target, typeName, string.Empty));
                return false;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                Type declaringType;
                if (!TryResolveDeclaringType(candidate.Assembly, typeName, out declaringType) || declaringType == null)
                {
                    WriteResolutionTrace("Type resolution candidate miss. CandidateAssembly='" + (candidate.SimpleName ?? string.Empty) +
                        "', CandidateSource='" + (candidate.Source ?? string.Empty) +
                        "', TypeHint='" + typeName + "'.");
                    continue;
                }

                resolvedTarget = new HarmonyResolvedTypeTarget
                {
                    AssemblyPath = candidate.AssemblyPath ?? string.Empty,
                    DeclaringType = declaringType,
                    Project = FindProject(runtime, target.DocumentPath, candidate.AssemblyPath),
                    DisplayName = declaringType.FullName ?? declaringType.Name ?? string.Empty
                };
                WriteResolutionTrace("Type resolution matched. CandidateAssembly='" + (candidate.SimpleName ?? string.Empty) +
                    "', CandidateSource='" + (candidate.Source ?? string.Empty) +
                    "', ResolvedType='" + resolvedTarget.DisplayName + "'.");
                return true;
            }

            reason = "The declaring type could not be resolved from the target assembly.";
            WriteResolutionTrace("Type resolution failed. " + BuildTargetDiagnosticText(target, typeName, string.Empty));
            return false;
        }

        public bool TryResolveSourcePatchContext(IWorkbenchModuleRuntime runtime, EditorCommandTarget target, out HarmonySourcePatchContext context, out string reason)
        {
            context = null;
            reason = "Select a Harmony source method to inspect its patch context.";
            if (runtime == null || target == null || string.IsNullOrEmpty(target.DocumentPath))
            {
                return false;
            }

            var text = _sourceSymbolService.GetDocumentText(runtime, target.DocumentPath);
            if (string.IsNullOrEmpty(text))
            {
                reason = "Source text was not available for Harmony source context.";
                return false;
            }

            string header;
            if (!_sourceSymbolService.TryExtractEnclosingMethodHeader(text, target.AbsolutePosition, out header))
            {
                reason = "The enclosing source method header could not be read for Harmony context.";
                return false;
            }

            HarmonyMethodLookupHint hint;
            if (!_sourceSymbolService.TryBuildLookupHint(runtime, target.DocumentPath, target.AbsolutePosition, target.SymbolText, out hint) || hint == null)
            {
                reason = "The selected source location does not map to a method declaration.";
                return false;
            }

            bool resolvedFromAttribute;
            var patchKind = _sourceSymbolService.ResolveSourcePatchKind(header, hint.Name, out resolvedFromAttribute);
            if (string.IsNullOrEmpty(patchKind))
            {
                reason = "The selected source method is not a Harmony Prefix, Postfix, Transpiler, or Finalizer.";
                return false;
            }

            HarmonyResolvedMethodTarget targetMethod;
            if (!TryResolveFromSourcePatch(runtime, target, out targetMethod, out reason) || targetMethod == null)
            {
                return false;
            }

            context = new HarmonySourcePatchContext
            {
                PatchKind = patchKind,
                SourceMethodName = !string.IsNullOrEmpty(hint.Name) ? hint.Name : target.SymbolText ?? string.Empty,
                ResolutionSource = resolvedFromAttribute ? "attribute" : "convention",
                Target = targetMethod
            };
            reason = string.Empty;
            return true;
        }

        private bool TryResolveFromSourcePatch(IWorkbenchModuleRuntime runtime, EditorCommandTarget target, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (target == null || string.IsNullOrEmpty(target.DocumentPath))
            {
                return false;
            }

            var text = _sourceSymbolService.GetDocumentText(runtime, target.DocumentPath);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            HarmonyPatchAttributeBinding binding;
            if (!_sourceSymbolService.TryBuildHarmonyPatchBinding(text, target.AbsolutePosition, target.SymbolText, out binding) || binding == null)
            {
                return false;
            }

            string assemblyPath;
            Type declaringType;
            if (!_sourceSymbolService.TryResolveRuntimeTypeByName(binding.TypeName, out assemblyPath, out declaringType) || declaringType == null)
            {
                reason = "The HarmonyPatch target type could not be resolved from the current runtime.";
                return false;
            }

            var hint = new HarmonyMethodLookupHint
            {
                Name = binding.MethodName ?? string.Empty,
                ParameterTypeNames = binding.ParameterTypeNames ?? new string[0],
                ParameterCount = binding.ParameterTypeNames != null && binding.ParameterTypeNames.Length > 0
                    ? binding.ParameterTypeNames.Length
                    : -1
            };

            var method = _runtimeMethodLookupService.ResolveMethod(declaringType, binding.MethodName, hint, out reason);
            if (method == null)
            {
                if (string.IsNullOrEmpty(reason))
                {
                    reason = "The HarmonyPatch target method could not be resolved from the current runtime.";
                }

                return false;
            }

            var request = CreateInspectionRequest(method, assemblyPath, target.DocumentPath, string.Empty, string.Empty);
            resolvedTarget = CreateResolvedTarget(runtime, request, method);
            return true;
        }

        private bool TryResolveFromMetadata(IWorkbenchModuleRuntime runtime, EditorCommandTarget target, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (target == null)
            {
                return false;
            }

            string typeName;
            if (!TryResolveTypeName(target, out typeName))
            {
                reason = "The declaring type could not be inferred for the selected symbol.";
                return false;
            }

            var candidates = ResolveAssemblyCandidates(runtime, target);
            if (candidates.Count == 0)
            {
                reason = "The containing assembly could not be located for the selected symbol.";
                WriteResolutionTrace("Metadata resolution failed: no assembly candidates. " + BuildTargetDiagnosticText(target, typeName, string.Empty));
                return false;
            }

            var hint = BuildLookupHint(
                target.DocumentationCommentId,
                !string.IsNullOrEmpty(target.SymbolText) ? target.SymbolText : target.MetadataName,
                target.QualifiedSymbolDisplay);
            var methodName = !string.IsNullOrEmpty(hint.Name)
                ? hint.Name
                : (!string.IsNullOrEmpty(target.SymbolText) ? target.SymbolText : target.MetadataName);

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                Type declaringType;
                if (!TryResolveDeclaringType(candidate.Assembly, typeName, out declaringType) || declaringType == null)
                {
                    WriteResolutionTrace("Metadata resolution type miss. CandidateAssembly='" + (candidate.SimpleName ?? string.Empty) +
                        "', CandidateSource='" + (candidate.Source ?? string.Empty) +
                        "', TypeHint='" + typeName + "'.");
                    continue;
                }

                var method = _runtimeMethodLookupService.ResolveMethod(declaringType, methodName, hint, out reason);
                if (method == null)
                {
                    WriteResolutionTrace("Metadata resolution method miss. CandidateAssembly='" + (candidate.SimpleName ?? string.Empty) +
                        "', CandidateSource='" + (candidate.Source ?? string.Empty) +
                        "', TypeHint='" + typeName +
                        "', MethodHint='" + methodName +
                        "', Reason='" + (reason ?? string.Empty) + "'.");
                    continue;
                }

                var request = CreateInspectionRequest(
                    method,
                    candidate.AssemblyPath ?? string.Empty,
                    target.DefinitionDocumentPath ?? target.DocumentPath ?? string.Empty,
                    string.Empty,
                    target.DocumentationCommentId);
                resolvedTarget = CreateResolvedTarget(runtime, request, method);
                WriteResolutionTrace("Metadata resolution matched. CandidateAssembly='" + (candidate.SimpleName ?? string.Empty) +
                    "', CandidateSource='" + (candidate.Source ?? string.Empty) +
                    "', Resolved='" + (request.DisplayName ?? string.Empty) + "'.");
                return true;
            }

            if (string.IsNullOrEmpty(reason))
            {
                reason = "Metadata navigation could not resolve the selected method.";
            }

            WriteResolutionTrace("Metadata resolution failed. " + BuildTargetDiagnosticText(target, typeName, methodName));
            return false;
        }

        private bool TryResolveFromProjectOutput(IWorkbenchModuleRuntime runtime, EditorCommandTarget target, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (runtime == null || target == null)
            {
                return false;
            }

            var project = FindProject(runtime, target.DocumentPath, string.Empty);
            if (project == null || string.IsNullOrEmpty(project.OutputAssemblyPath))
            {
                return false;
            }

            var assembly = LoadAssembly(project.OutputAssemblyPath, string.Empty, runtime);
            if (assembly == null)
            {
                reason = "The project output assembly could not be loaded for source resolution.";
                return false;
            }

            string typeName;
            if (!TryResolveTypeName(target, out typeName))
            {
                reason = "The declaring type could not be inferred for the selected source method.";
                return false;
            }

            Type declaringType;
            if (!TryResolveDeclaringType(assembly, typeName, out declaringType) || declaringType == null)
            {
                reason = "The source method type could not be resolved from the project output assembly.";
                return false;
            }

            HarmonyMethodLookupHint hint;
            _sourceSymbolService.TryBuildLookupHint(runtime, target.DocumentPath, target.AbsolutePosition, target.SymbolText, out hint);
            var method = _runtimeMethodLookupService.ResolveMethod(declaringType, target.SymbolText, hint, out reason);
            if (method == null)
            {
                if (string.IsNullOrEmpty(reason))
                {
                    reason = "The source method could not be mapped to a runtime method.";
                }

                return false;
            }

            var request = CreateInspectionRequest(method, SafeGetLocation(assembly), target.DocumentPath, string.Empty, string.Empty);
            resolvedTarget = CreateResolvedTarget(runtime, request, method);
            return true;
        }

        private static HarmonyPatchInspectionRequest CreateInspectionRequest(MethodBase method, string assemblyPath, string documentPath, string cachePath, string documentationCommentId)
        {
            return new HarmonyPatchInspectionRequest
            {
                AssemblyPath = assemblyPath ?? string.Empty,
                MetadataToken = method != null ? method.MetadataToken : 0,
                DeclaringTypeName = method != null && method.DeclaringType != null ? method.DeclaringType.FullName ?? method.DeclaringType.Name ?? string.Empty : string.Empty,
                MethodName = method != null ? method.Name ?? string.Empty : string.Empty,
                Signature = BuildMethodSignature(method),
                DisplayName = BuildMethodDisplayName(method),
                DocumentPath = documentPath ?? string.Empty,
                CachePath = cachePath ?? string.Empty,
                DocumentationCommentId = documentationCommentId ?? string.Empty
            };
        }

        private HarmonyResolvedMethodTarget CreateResolvedTarget(IWorkbenchModuleRuntime runtime, HarmonyPatchInspectionRequest request, MethodBase method)
        {
            return new HarmonyResolvedMethodTarget
            {
                InspectionRequest = request,
                Method = method,
                Project = FindProject(runtime, request != null ? request.DocumentPath : string.Empty, request != null ? request.AssemblyPath : string.Empty),
                DisplayName = request != null && !string.IsNullOrEmpty(request.DisplayName)
                    ? request.DisplayName
                    : BuildMethodDisplayName(method)
            };
        }

        private static CortexProjectDefinition FindProject(IWorkbenchModuleRuntime runtime, string documentPath, string assemblyPath)
        {
            var projectsRuntime = runtime != null ? runtime.Projects : null;
            var projects = projectsRuntime != null ? projectsRuntime.GetProjects() : null;
            if (projects == null)
            {
                return null;
            }

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
                    PathsEqual(assemblyPath, project.OutputAssemblyPath))
                {
                    return project;
                }
            }

            return null;
        }

        private Assembly ResolveAssembly(IWorkbenchModuleRuntime runtime, string assemblyName, string documentPath)
        {
            var project = FindProject(runtime, documentPath, string.Empty);
            if (project != null && !string.IsNullOrEmpty(project.OutputAssemblyPath))
            {
                var projectAssembly = LoadAssembly(project.OutputAssemblyPath, string.Empty, runtime);
                if (projectAssembly != null)
                {
                    return projectAssembly;
                }
            }

            return LoadAssembly(string.Empty, assemblyName, runtime);
        }

        private List<AssemblyResolutionCandidate> ResolveAssemblyCandidates(IWorkbenchModuleRuntime runtime, EditorCommandTarget target)
        {
            var results = new List<AssemblyResolutionCandidate>();
            if (target == null)
            {
                return results;
            }

            var project = FindProject(runtime, target.DocumentPath, string.Empty);
            if (project != null && !string.IsNullOrEmpty(project.OutputAssemblyPath))
            {
                var projectAssembly = LoadAssembly(project.OutputAssemblyPath, string.Empty, runtime);
                AddAssemblyCandidate(results, projectAssembly, "project-output");
            }

            AddAssemblyCandidate(results, LoadAssembly(string.Empty, target.ContainingAssemblyName, runtime), "target-assembly");

            var inferredAssemblyName = InferDecompilerAssemblyName(target.DefinitionDocumentPath);
            if (string.IsNullOrEmpty(inferredAssemblyName))
            {
                inferredAssemblyName = InferDecompilerAssemblyName(target.DocumentPath);
            }

            if (!string.IsNullOrEmpty(inferredAssemblyName) &&
                !string.Equals(inferredAssemblyName, target.ContainingAssemblyName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                AddAssemblyCandidate(results, LoadAssembly(string.Empty, inferredAssemblyName, runtime), "decompiler-cache");
            }

            return results;
        }

        private static void AddAssemblyCandidate(List<AssemblyResolutionCandidate> candidates, Assembly assembly, string source)
        {
            if (candidates == null || assembly == null)
            {
                return;
            }

            var assemblyPath = SafeGetLocation(assembly);
            var simpleName = string.Empty;
            try
            {
                var name = assembly.GetName();
                simpleName = name != null ? name.Name ?? string.Empty : string.Empty;
            }
            catch
            {
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (PathsEqual(candidates[i].AssemblyPath, assemblyPath) ||
                    (!string.IsNullOrEmpty(simpleName) &&
                     string.Equals(candidates[i].SimpleName ?? string.Empty, simpleName, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }
            }

            candidates.Add(new AssemblyResolutionCandidate
            {
                Assembly = assembly,
                AssemblyPath = assemblyPath,
                SimpleName = simpleName,
                Source = source ?? string.Empty
            });
        }

        private static string InferDecompilerAssemblyName(string documentPath)
        {
            if (string.IsNullOrEmpty(documentPath))
            {
                return string.Empty;
            }

            try
            {
                var fullPath = Path.GetFullPath(documentPath);
                var cacheSegment = Path.DirectorySeparatorChar + "cortex_cache" + Path.DirectorySeparatorChar;
                var index = fullPath.IndexOf(cacheSegment, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    cacheSegment = Path.AltDirectorySeparatorChar + "cortex_cache" + Path.AltDirectorySeparatorChar;
                    index = fullPath.IndexOf(cacheSegment, StringComparison.OrdinalIgnoreCase);
                }

                if (index < 0)
                {
                    return string.Empty;
                }

                var remainder = fullPath.Substring(index + cacheSegment.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.IsNullOrEmpty(remainder))
                {
                    return string.Empty;
                }

                var separatorIndex = remainder.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                return separatorIndex > 0
                    ? remainder.Substring(0, separatorIndex)
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static Assembly LoadAssembly(string assemblyPath, string assemblyName, IWorkbenchModuleRuntime runtime)
        {
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                try
                {
                    var fullPath = Path.GetFullPath(assemblyPath);
                    var loaded = AppDomain.CurrentDomain.GetAssemblies();
                    for (var i = 0; i < loaded.Length; i++)
                    {
                        if (PathsEqual(SafeGetLocation(loaded[i]), fullPath))
                        {
                            return loaded[i];
                        }
                    }

                    return File.Exists(fullPath) ? Assembly.LoadFrom(fullPath) : null;
                }
                catch
                {
                }
            }

            if (!string.IsNullOrEmpty(assemblyName))
            {
                var simpleName = Path.GetFileNameWithoutExtension(assemblyName) ?? assemblyName;
                var loaded = AppDomain.CurrentDomain.GetAssemblies();
                for (var i = 0; i < loaded.Length; i++)
                {
                    var currentName = loaded[i].GetName();
                    if (currentName != null &&
                        string.Equals(currentName.Name ?? string.Empty, simpleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return loaded[i];
                    }
                }

                var projects = runtime != null && runtime.Projects != null ? runtime.Projects.GetProjects() : null;
                if (projects != null)
                {
                    for (var i = 0; i < projects.Count; i++)
                    {
                        var project = projects[i];
                        if (project == null || string.IsNullOrEmpty(project.OutputAssemblyPath))
                        {
                            continue;
                        }

                        if (string.Equals(Path.GetFileNameWithoutExtension(project.OutputAssemblyPath) ?? string.Empty, simpleName, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                return Assembly.LoadFrom(project.OutputAssemblyPath);
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static bool TryResolveTypeName(EditorCommandTarget target, out string typeName)
        {
            typeName = target != null ? target.ContainingTypeName ?? string.Empty : string.Empty;
            if (!string.IsNullOrEmpty(typeName))
            {
                return true;
            }

            var qualified = target != null ? target.QualifiedSymbolDisplay ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(qualified))
            {
                return false;
            }

            var symbolName = !string.IsNullOrEmpty(target.SymbolText) ? target.SymbolText : target.MetadataName;
            if (!string.IsNullOrEmpty(symbolName))
            {
                var marker = "." + symbolName;
                var markerIndex = qualified.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex > 0)
                {
                    typeName = qualified.Substring(0, markerIndex);
                }
            }

            return !string.IsNullOrEmpty(typeName);
        }

        private static bool TryResolveDeclaringType(Assembly assembly, string typeName, out Type declaringType)
        {
            declaringType = null;
            if (assembly == null || string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            try
            {
                declaringType = assembly.GetType(typeName, false);
                if (declaringType != null)
                {
                    return true;
                }

                var types = assembly.GetTypes();
                for (var i = 0; i < types.Length; i++)
                {
                    if (HarmonySymbolNameUtility.TypeNameMatches(types[i], typeName))
                    {
                        declaringType = types[i];
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static HarmonyMethodLookupHint BuildLookupHint(string documentationCommentId, string symbolText, string signatureText)
        {
            ParsedDocumentationCommentId parsed;
            if (TryParseDocumentationCommentId(documentationCommentId, out parsed))
            {
                return new HarmonyMethodLookupHint
                {
                    Name = parsed.MethodName,
                    ParameterCount = parsed.ParameterTypeNames.Length,
                    ParameterTypeNames = parsed.ParameterTypeNames,
                    IsConstructor = parsed.IsConstructor,
                    IsStaticConstructor = parsed.IsStaticConstructor
                };
            }

            var parameterTypeNames = ParseSignatureParameterTypes(signatureText);
            return new HarmonyMethodLookupHint
            {
                Name = HarmonySymbolNameUtility.NormalizeMethodName(symbolText),
                ParameterCount = parameterTypeNames.Length > 0 ? parameterTypeNames.Length : -1,
                ParameterTypeNames = parameterTypeNames
            };
        }

        private static bool TryParseDocumentationCommentId(string documentationCommentId, out ParsedDocumentationCommentId parsed)
        {
            parsed = null;
            if (string.IsNullOrEmpty(documentationCommentId) || documentationCommentId.Length < 3 || documentationCommentId[1] != ':')
            {
                return false;
            }

            var body = documentationCommentId.Substring(2);
            if (string.IsNullOrEmpty(body))
            {
                return false;
            }

            var openParen = body.IndexOf('(');
            var memberPath = openParen >= 0 ? body.Substring(0, openParen) : body;
            var lastDot = memberPath.LastIndexOf('.');
            if (lastDot <= 0 || lastDot + 1 >= memberPath.Length)
            {
                return false;
            }

            var methodName = memberPath.Substring(lastDot + 1);
            var parameterTypeNames = openParen >= 0
                ? SplitParameterTypes(body.Substring(openParen + 1, body.Length - openParen - 2))
                : new string[0];
            parsed = new ParsedDocumentationCommentId
            {
                MethodName = methodName == "#ctor" ? ".ctor" : methodName == "#cctor" ? ".cctor" : methodName,
                ParameterTypeNames = parameterTypeNames,
                IsConstructor = string.Equals(methodName, "#ctor", StringComparison.Ordinal) || string.Equals(methodName, ".ctor", StringComparison.Ordinal),
                IsStaticConstructor = string.Equals(methodName, "#cctor", StringComparison.Ordinal) || string.Equals(methodName, ".cctor", StringComparison.Ordinal)
            };
            return true;
        }

        private static string[] ParseSignatureParameterTypes(string signatureText)
        {
            if (string.IsNullOrEmpty(signatureText))
            {
                return new string[0];
            }

            var openParen = signatureText.IndexOf('(');
            var closeParen = signatureText.LastIndexOf(')');
            if (openParen < 0 || closeParen <= openParen)
            {
                return new string[0];
            }

            return SplitParameterTypes(signatureText.Substring(openParen + 1, closeParen - openParen - 1));
        }

        private static string[] SplitParameterTypes(string parameterList)
        {
            if (string.IsNullOrEmpty(parameterList))
            {
                return new string[0];
            }

            var results = new List<string>();
            var current = string.Empty;
            var depth = 0;
            for (var i = 0; i < parameterList.Length; i++)
            {
                var value = parameterList[i];
                if (value == '<' || value == '[' || value == '(')
                {
                    depth++;
                }
                else if (value == '>' || value == ']' || value == ')')
                {
                    depth = Math.Max(0, depth - 1);
                }
                else if (value == ',' && depth == 0)
                {
                    AppendParameterType(results, current);
                    current = string.Empty;
                    continue;
                }

                current += value;
            }

            AppendParameterType(results, current);
            return results.ToArray();
        }

        private static void AppendParameterType(List<string> results, string value)
        {
            var candidate = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(candidate))
            {
                return;
            }

            candidate = candidate.Replace("ref ", string.Empty).Replace("out ", string.Empty).Replace("params ", string.Empty).Trim();
            var lastSpace = candidate.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                candidate = candidate.Substring(0, lastSpace);
            }

            results.Add(HarmonySymbolNameUtility.NormalizeTypeName(candidate));
        }

        private static string BuildMethodDisplayName(MethodBase method)
        {
            if (method == null)
            {
                return string.Empty;
            }

            var declaringType = method.DeclaringType != null ? method.DeclaringType.FullName ?? method.DeclaringType.Name ?? string.Empty : string.Empty;
            return declaringType + "." + (method.Name ?? string.Empty) + BuildMethodSignature(method);
        }

        private static string BuildMethodSignature(MethodBase method)
        {
            if (method == null)
            {
                return "()";
            }

            var parameters = method.GetParameters();
            var text = "(";
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    text += ", ";
                }

                var parameterType = parameters[i].ParameterType;
                if (parameterType != null && parameterType.IsByRef)
                {
                    text += parameters[i].IsOut ? "out " : "ref ";
                    parameterType = parameterType.GetElementType();
                }

                text += parameterType != null ? parameterType.Name : "object";
                text += " ";
                text += parameters[i].Name ?? ("arg" + i.ToString());
            }

            return text + ")";
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

        private static string SafeGetLocation(Assembly assembly)
        {
            try
            {
                return assembly != null ? assembly.Location ?? string.Empty : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void WriteResolutionTrace(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Log.WriteInfo(message);
            }
        }

        private static string BuildTargetDiagnosticText(EditorCommandTarget target, string typeName, string methodName)
        {
            var inferredAssembly = InferDecompilerAssemblyName(target != null ? target.DefinitionDocumentPath : string.Empty);
            if (string.IsNullOrEmpty(inferredAssembly))
            {
                inferredAssembly = InferDecompilerAssemblyName(target != null ? target.DocumentPath : string.Empty);
            }

            return "Document='" + (target != null ? target.DocumentPath ?? string.Empty : string.Empty) +
                "', DefinitionDocument='" + (target != null ? target.DefinitionDocumentPath ?? string.Empty : string.Empty) +
                "', AssemblyHint='" + (target != null ? target.ContainingAssemblyName ?? string.Empty : string.Empty) +
                "', DecompiledAssemblyHint='" + inferredAssembly +
                "', TypeHint='" + (typeName ?? string.Empty) +
                "', MethodHint='" + (methodName ?? string.Empty) +
                "', SymbolText='" + (target != null ? target.SymbolText ?? string.Empty : string.Empty) +
                "', MetadataName='" + (target != null ? target.MetadataName ?? string.Empty : string.Empty) +
                "', DocumentationId='" + (target != null ? target.DocumentationCommentId ?? string.Empty : string.Empty) +
                "', QualifiedDisplay='" + (target != null ? target.QualifiedSymbolDisplay ?? string.Empty : string.Empty) + "'.";
        }

        private sealed class ParsedDocumentationCommentId
        {
            public string MethodName = string.Empty;
            public string[] ParameterTypeNames = new string[0];
            public bool IsConstructor;
            public bool IsStaticConstructor;
        }

        private sealed class AssemblyResolutionCandidate
        {
            public Assembly Assembly;
            public string AssemblyPath = string.Empty;
            public string SimpleName = string.Empty;
            public string Source = string.Empty;
        }
    }
}
