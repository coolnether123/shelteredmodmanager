using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Diagnostics;
using UnityEngine;

namespace Cortex.Host.Unity.Runtime
{
    /// <summary>
    /// Applies the Unity host with a single platform-module attachment point.
    /// Loader-specific assemblies provide their <see cref="ICortexPlatformModule"/> here and the
    /// host performs the default host-only composition internally.
    /// </summary>
    public static class UnityCortexShellBootstrapper
    {
        private static readonly CortexLogger Log = CortexLog.ForSource("Cortex.Host.Unity");

        public static void EnsureShell(ICortexPlatformModule platformModule)
        {
            if (platformModule == null)
            {
                throw new ArgumentNullException("platformModule");
            }

            EnsureShell(UnityCortexHostCompositionRoot.CreateDefault(platformModule));
        }

        private static void EnsureShell(UnityCortexHostCompositionRoot compositionRoot)
        {
            try
            {
                CortexLog.Configure(compositionRoot.LogSink);

                var platformModule = compositionRoot.HostServices.PlatformModule;
                CortexDiagnostics.Configure(platformModule != null ? platformModule.DiagnosticConfiguration : null);

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
