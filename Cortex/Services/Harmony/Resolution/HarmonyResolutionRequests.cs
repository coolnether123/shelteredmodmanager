using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services.Harmony.Resolution
{
    internal enum HarmonyResolutionTargetKind
    {
        Source,
        Decompiled
    }

    internal sealed class HarmonyResolutionTargetRequest
    {
        public HarmonyResolutionTargetKind Kind;
        public EditorCommandTarget Target;
    }

    internal sealed class HarmonySourceResolutionRequest
    {
        public CortexShellState State;
        public ISourceLookupIndex SourceLookupIndex;
        public IProjectCatalog ProjectCatalog;
        public EditorCommandTarget Target;
    }
}
