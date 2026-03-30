using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services.Harmony.Resolution
{
    internal interface IHarmonySourceTargetResolver
    {
        bool TryResolveFromSourceTarget(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonyResolvedMethodTarget resolvedTarget, out string reason);
        bool TryResolveSourcePatchContext(CortexShellState state, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonySourcePatchContext context, out string reason);
    }
}
