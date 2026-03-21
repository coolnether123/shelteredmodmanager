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
        /// <summary>
        /// Ensures a single persistent Cortex shell exists in the active Unity runtime.
        /// </summary>
        public void Initialize()
        {
            try
            {
                CortexLog.Configure(new MmLogCortexLogSink());

                var existingShell = UnityEngine.Object.FindObjectOfType<CortexShell>();
                if (existingShell != null)
                {
                    MMLog.WriteInfo("[Cortex] Runtime bootstrap skipped because an existing shell is already active on '" + existingShell.gameObject.name + "'.");
                    return;
                }

                var shellRoot = new GameObject("Cortex.Shell");
                UnityEngine.Object.DontDestroyOnLoad(shellRoot);
                var shell = shellRoot.AddComponent<CortexShell>();
                shell.ConfigureHostServices(new WindowsPathInteractionService(), new UnityWorkbenchRuntimeFactory(), new ModApiCortexPlatformModule());
                MMLog.WriteInfo("[Cortex] Runtime bootstrap created shell root '" + shellRoot.name + "'.");
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[Cortex] Runtime bootstrap failed: " + ex);
                throw;
            }
        }
    }
}
