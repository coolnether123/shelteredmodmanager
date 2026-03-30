using Cortex.Core.Abstractions;
using Cortex.Core.Diagnostics;
using Cortex.Presentation.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    /// <summary>
    /// Host-only composition seam for the Unity Cortex shell.
    /// Loader-specific bootstraps attach their <see cref="ICortexPlatformModule"/> here and keep
    /// concrete loader dependencies outside the Unity host assembly.
    /// </summary>
    public sealed class UnityCortexHostCompositionRoot : ICortexHostCompositionRoot
    {
        private readonly ICortexPlatformModule _platformModule;
        private readonly ICortexLogSink _logSink;
        private readonly ICortexHostServices _hostServices;

        internal static UnityCortexHostCompositionRoot CreateDefault(ICortexPlatformModule platformModule)
        {
            return new UnityCortexHostCompositionRoot(platformModule, CreateHostServices(platformModule));
        }

        public UnityCortexHostCompositionRoot(ICortexPlatformModule platformModule)
            : this(platformModule, CreateHostServices(platformModule))
        {
        }

        public UnityCortexHostCompositionRoot(ICortexPlatformModule platformModule, ICortexHostServices hostServices)
        {
            _platformModule = platformModule;
            _logSink = platformModule != null ? platformModule.LogSink : null;
            _hostServices = hostServices;
        }

        public ICortexPlatformModule PlatformModule
        {
            get { return _platformModule; }
        }

        public ICortexLogSink LogSink
        {
            get { return _logSink; }
        }

        public ICortexHostServices HostServices
        {
            get { return _hostServices; }
        }

        private static ICortexHostServices CreateHostServices(ICortexPlatformModule platformModule)
        {
            return new UnityCortexHostServices(
                new UnityCortexHostEnvironment(),
                new WindowsPathInteractionService(),
                new UnityWorkbenchRuntimeFactory(),
                platformModule,
                new UnityCortexShellHostUi());
        }
    }
}
