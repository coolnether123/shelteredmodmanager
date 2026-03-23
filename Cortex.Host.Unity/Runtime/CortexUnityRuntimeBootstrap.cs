using System;
using Cortex.Core.Diagnostics;
using Cortex.Platform.ModAPI.Runtime;
using ModAPI.Core;
using UnityEngine;

namespace Cortex.Host.Unity.Runtime
{
    /// <summary>
    /// Boots the Cortex shell into the live Unity runtime when the Unity host assembly is present.
    /// </summary>
    public sealed class CortexUnityRuntimeBootstrap : IGameRuntimeBootstrap
    {
        private static readonly CortexLogger Log = CortexLog.ForSource("Cortex.Host.Unity");

        /// <summary>
        /// Ensures a single persistent Cortex shell exists in the active Unity runtime.
        /// </summary>
        public void Initialize()
        {
            try
            {
                var compositionRoot = new UnityCortexHostCompositionRoot(new ModApiCortexPlatformModule());
                CortexLog.Configure(compositionRoot.LogSink);

                var existingShell = UnityEngine.Object.FindObjectOfType<UnityCortexShellBehaviour>();
                if (existingShell != null)
                {
                    Log.WriteInfo("Runtime bootstrap skipped because an existing shell is already active on '" + existingShell.gameObject.name + "'.");
                    return;
                }

                var shellRoot = new GameObject("Cortex.Shell");
                var shell = shellRoot.AddComponent<UnityCortexShellBehaviour>();
                shell.ConfigureHostServices(compositionRoot.HostServices);
                Log.WriteInfo("Runtime bootstrap created shell root '" + shellRoot.name + "'.");
            }
            catch (Exception ex)
            {
                Log.WriteError("Runtime bootstrap failed: " + ex);
                throw;
            }
        }
    }
}
