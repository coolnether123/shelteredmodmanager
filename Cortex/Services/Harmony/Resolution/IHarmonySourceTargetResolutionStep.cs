namespace Cortex.Services.Harmony.Resolution
{
    internal interface IHarmonySourceTargetResolutionStep
    {
        bool TryResolve(HarmonySourceResolutionRequest request, out HarmonyResolvedMethodTarget resolvedTarget, out string reason);
    }
}
