using ModAPI.Core;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesRuntimeBootstrap : IGameRuntimeBootstrap
    {
        public void Initialize()
        {
            if (!ModAPIRegistry.IsAPIRegistered(ParalivesRuntimeInfo.RegistryId))
                ModAPIRegistry.RegisterAPI(ParalivesRuntimeInfo.RegistryId, ParalivesRuntimeInfo.Current);

            if (!ModAPIRegistry.IsAPIRegistered(ParalivesGameLifecycleFacade.RegistryId))
                ModAPIRegistry.RegisterAPI(ParalivesGameLifecycleFacade.RegistryId, ParalivesGameLifecycleFacade.Current);
            if (!ModAPIRegistry.IsAPIRegistered(ParalivesSaveStorageFacade.RegistryId))
                ModAPIRegistry.RegisterAPI(ParalivesSaveStorageFacade.RegistryId, ParalivesSaveStorageFacade.Current);
            if (!ModAPIRegistry.IsAPIRegistered(GameRuntimeApiIds.GameLifecycle))
                ModAPIRegistry.RegisterAPI(GameRuntimeApiIds.GameLifecycle, ParalivesGameLifecycleFacade.Current);
            if (!ModAPIRegistry.IsAPIRegistered(GameRuntimeApiIds.SaveRuntime))
                ModAPIRegistry.RegisterAPI(GameRuntimeApiIds.SaveRuntime, ParalivesSaveStorageFacade.Current);

            ParalivesHarmonyPatcher.EnsurePatched();
            ParalivesRuntimeHost.Start();
            SmmModScreenBridge.Start();
        }
    }
}
