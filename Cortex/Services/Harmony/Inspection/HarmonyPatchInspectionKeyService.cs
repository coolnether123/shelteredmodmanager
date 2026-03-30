using Cortex.Core.Models;

namespace Cortex.Services.Harmony.Inspection
{
    internal interface IHarmonyPatchInspectionKeyService
    {
        string BuildKey(HarmonyPatchInspectionRequest request);
    }

    internal sealed class HarmonyPatchInspectionKeyService : IHarmonyPatchInspectionKeyService
    {
        public string BuildKey(HarmonyPatchInspectionRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(request.AssemblyPath) && request.MetadataToken > 0)
            {
                return request.AssemblyPath + "|0x" + request.MetadataToken.ToString("X8");
            }

            return (request.AssemblyPath ?? string.Empty) + "|" +
                (request.DeclaringTypeName ?? string.Empty) + "|" +
                (request.MethodName ?? string.Empty) + "|" +
                (request.Signature ?? string.Empty);
        }
    }
}
