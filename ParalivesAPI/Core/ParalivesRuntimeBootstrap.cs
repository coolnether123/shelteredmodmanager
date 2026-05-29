using ModAPI.Core;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesRuntimeBootstrap : IGameRuntimeBootstrap
    {
        public void Initialize()
        {
            if (!ModAPIRegistry.IsAPIRegistered(ParalivesRuntimeInfo.RegistryId))
                ModAPIRegistry.RegisterAPI(ParalivesRuntimeInfo.RegistryId, ParalivesRuntimeInfo.Current);

            ParalivesHarmonyPatcher.EnsurePatched();
            ParalivesRuntimeHost.Start();
            SmmModScreenBridge.Start();
        }
    }
}
