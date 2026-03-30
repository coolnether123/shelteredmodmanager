using System;
using System.IO;
using System.Reflection;

namespace Cortex.Services.Harmony.Resolution
{
    internal sealed class HarmonySourceFallbackTargetResolver : IHarmonySourceTargetResolutionStep
    {
        private readonly HarmonySourceSymbolService _symbolService;
        private readonly IHarmonyMethodIdentityService _methodIdentityService;
        private readonly IHarmonyRuntimeMethodLookupService _methodLookupService;

        public HarmonySourceFallbackTargetResolver(
            HarmonySourceSymbolService symbolService,
            IHarmonyMethodIdentityService methodIdentityService,
            IHarmonyRuntimeMethodLookupService methodLookupService)
        {
            _symbolService = symbolService ?? new HarmonySourceSymbolService();
            _methodIdentityService = methodIdentityService ?? new HarmonyMethodIdentityService();
            _methodLookupService = methodLookupService ?? new HarmonyRuntimeMethodLookupService();
        }

        public bool TryResolve(HarmonySourceResolutionRequest request, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (request == null || request.Target == null || string.IsNullOrEmpty(request.Target.SymbolText))
            {
                reason = "No method symbol was selected.";
                return false;
            }

            var project = _methodIdentityService.FindProjectForDocument(request.ProjectCatalog, request.Target.DocumentPath, string.Empty);
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

            HarmonyMethodLookupHint hint;
            _symbolService.TryBuildLookupHint(request.State, request.Target.DocumentPath, request.Target.AbsolutePosition, request.Target.SymbolText, out hint);

            MethodBase unique = null;
            var matchCount = 0;
            try
            {
                var types = assembly.GetTypes();
                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    string candidateReason;
                    var candidate = _methodLookupService.ResolveMethod(types[typeIndex], request.Target.SymbolText, hint, out candidateReason);
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

            var inspectionRequest = _methodIdentityService.CreateInspectionRequest(
                unique,
                assemblyPath,
                string.Empty,
                request.Target.DocumentPath,
                string.Empty,
                string.Empty);
            resolvedTarget = _methodIdentityService.CreateResolvedTarget(inspectionRequest, unique, request.ProjectCatalog);
            return true;
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
    }
}
