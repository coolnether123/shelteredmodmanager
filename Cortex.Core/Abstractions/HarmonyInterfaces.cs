using Cortex.Core.Models;

namespace Cortex.Core.Abstractions
{
    public interface IHarmonyRuntimeInspectionService
    {
        bool IsAvailable { get; }

        HarmonyPatchSnapshot CaptureSnapshot();

        HarmonyMethodPatchSummary Inspect(HarmonyPatchInspectionRequest request);
    }
}
