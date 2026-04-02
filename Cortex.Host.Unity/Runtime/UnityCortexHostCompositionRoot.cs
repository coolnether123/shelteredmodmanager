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
        private readonly ICortexLogSink _logSink;
        private readonly ICortexHostServices _hostServices;

        public UnityCortexHostCompositionRoot(ICortexHostServices hostServices)
        {
            _hostServices = hostServices;
            _logSink = hostServices != null && hostServices.PlatformModule != null
                ? hostServices.PlatformModule.LogSink
                : null;
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
