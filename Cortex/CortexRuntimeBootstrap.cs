using System;
using ModAPI.Core;
using UnityEngine;

namespace Cortex
{
    /// <summary>
    /// Boots the Cortex shell into the live Unity runtime when the shared Cortex assembly is present.
    /// </summary>
    public sealed class CortexRuntimeBootstrap : IGameRuntimeBootstrap
    {
        /// <summary>
        /// Ensures a single persistent Cortex shell exists in the active Unity runtime.
        /// </summary>
        public void Initialize()
        {
            try
            {
                var existingShell = UnityEngine.Object.FindObjectOfType<CortexShell>();
                if (existingShell != null)
                {
                    MMLog.WriteInfo("[Cortex] Runtime bootstrap skipped because an existing shell is already active on '" + existingShell.gameObject.name + "'.");
                    return;
                }

                var shellRoot = new GameObject("Cortex.Shell");
                UnityEngine.Object.DontDestroyOnLoad(shellRoot);
                shellRoot.AddComponent<CortexShell>();
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
