using Cortex.Core.Diagnostics;
using Cortex.Presentation.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityCortexHostCompositionRoot : ICortexHostCompositionRoot
    {
        private readonly ICortexLogSink _logSink;
        private readonly ICortexHostServices _hostServices;

        public UnityCortexHostCompositionRoot()
            : this(new MmLogCortexLogSink(), new UnityCortexHostServices())
        {
        }

        public UnityCortexHostCompositionRoot(ICortexLogSink logSink, ICortexHostServices hostServices)
        {
            _logSink = logSink;
            _hostServices = hostServices;
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
