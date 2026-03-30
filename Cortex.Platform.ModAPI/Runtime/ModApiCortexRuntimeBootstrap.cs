using Cortex.Host.Unity.Runtime;
using ModAPI.Core;

namespace Cortex.Platform.ModAPI.Runtime
{
    /// <summary>
    /// ModAPI-specific runtime entry point that wires the Unity host to the ModAPI platform module.
    /// Concrete loader composition stays here; the Unity host only consumes the host/platform seam.
    /// </summary>
    public sealed class ModApiCortexRuntimeBootstrap : IGameRuntimeBootstrap
    {
        public void Initialize()
        {
            UnityCortexShellBootstrapper.EnsureShell(new ModApiCortexPlatformModule());
        }
    }
}
