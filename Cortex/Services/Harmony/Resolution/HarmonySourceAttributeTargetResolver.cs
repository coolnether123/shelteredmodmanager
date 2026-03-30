using Cortex.Core.Models;

namespace Cortex.Services.Harmony.Resolution
{
    internal sealed class HarmonySourceAttributeTargetResolver : IHarmonySourceTargetResolutionStep
    {
        private readonly HarmonySourceSymbolService _symbolService;
        private readonly IHarmonyMethodIdentityService _methodIdentityService;
        private readonly IHarmonyRuntimeMethodLookupService _methodLookupService;

        public HarmonySourceAttributeTargetResolver(
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
            if (request == null || request.Target == null || string.IsNullOrEmpty(request.Target.DocumentPath))
            {
                reason = "Harmony attribute source resolution is only available for writable source documents.";
                return false;
            }

            var text = _symbolService.GetDocumentText(request.State, request.Target.DocumentPath);
            if (string.IsNullOrEmpty(text))
            {
                reason = "Source text was not available for Harmony attribute resolution.";
                return false;
            }

            HarmonyPatchAttributeBinding binding;
            if (!_symbolService.TryBuildHarmonyPatchBinding(text, request.Target.AbsolutePosition, request.Target.SymbolText, out binding))
            {
                reason = "No enclosing HarmonyPatch attribute could be resolved from the current source context.";
                return false;
            }

            System.Type declaringType;
            string assemblyPath;
            if (!_symbolService.TryResolveRuntimeTypeByName(binding.TypeName, out assemblyPath, out declaringType))
            {
                reason = "The HarmonyPatch target type could not be resolved from the current runtime.";
                return false;
            }

            HarmonyMethodLookupHint hint = null;
            if (!string.IsNullOrEmpty(binding.MethodName))
            {
                hint = new HarmonyMethodLookupHint
                {
                    Name = binding.MethodName,
                    ParameterTypeNames = binding.ParameterTypeNames ?? new string[0],
                    ParameterCount = binding.ParameterTypeNames != null && binding.ParameterTypeNames.Length > 0
                        ? binding.ParameterTypeNames.Length
                        : -1
                };
            }

            string resolveReason;
            var method = _methodLookupService.ResolveMethod(declaringType, binding.MethodName, hint, out resolveReason);
            if (method == null)
            {
                reason = !string.IsNullOrEmpty(resolveReason)
                    ? resolveReason
                    : "The HarmonyPatch target method could not be resolved from the current runtime.";
                return false;
            }

            var inspectionRequest = _methodIdentityService.CreateInspectionRequest(
                method,
                assemblyPath,
                string.Empty,
                request.Target.DocumentPath,
                string.Empty,
                string.Empty);
            resolvedTarget = _methodIdentityService.CreateResolvedTarget(inspectionRequest, method, request.ProjectCatalog);
            return true;
        }
    }
}
