using Cortex.Core.Abstractions;
using Cortex.Core.Diagnostics;
using Cortex.Presentation.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityCortexHostCompositionRoot : ICortexHostCompositionRoot
    {
        private readonly ICortexPlatformModule _platformModule;
        private readonly ICortexLogSink _logSink;
        private readonly ICortexHostServices _hostServices;

        public UnityCortexHostCompositionRoot(ICortexPlatformModule platformModule)
            : this(
                platformModule,
                new UnityCortexHostServices(
                    new UnityCortexHostEnvironment(),
                    new WindowsPathInteractionService(),
                    new UnityWorkbenchRuntimeFactory(),
                    platformModule,
                    new UnityCortexShellHostUi()))
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
    }
}
