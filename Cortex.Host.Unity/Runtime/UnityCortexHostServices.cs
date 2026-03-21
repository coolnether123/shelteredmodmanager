using Cortex.Core.Abstractions;
using Cortex.Platform.ModAPI.Runtime;
using Cortex.Presentation.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityCortexHostServices : ICortexHostServices
    {
        private readonly ICortexHostEnvironment _environment;
        private readonly IPathInteractionService _pathInteractionService;
        private readonly IWorkbenchRuntimeFactory _workbenchRuntimeFactory;
        private readonly ICortexPlatformModule _platformModule;

        public UnityCortexHostServices()
            : this(
                new UnityCortexHostEnvironment(),
                new WindowsPathInteractionService(),
                new UnityWorkbenchRuntimeFactory(),
                new ModApiCortexPlatformModule())
        {
        }

        public UnityCortexHostServices(
            ICortexHostEnvironment environment,
            IPathInteractionService pathInteractionService,
            IWorkbenchRuntimeFactory workbenchRuntimeFactory,
            ICortexPlatformModule platformModule)
        {
            _environment = environment;
            _pathInteractionService = pathInteractionService;
            _workbenchRuntimeFactory = workbenchRuntimeFactory;
            _platformModule = platformModule;
        }

        public ICortexHostEnvironment Environment
        {
            get { return _environment; }
        }

        public IPathInteractionService PathInteractionService
        {
            get { return _pathInteractionService; }
        }

        public IWorkbenchRuntimeFactory WorkbenchRuntimeFactory
        {
            get { return _workbenchRuntimeFactory; }
        }

        public ICortexPlatformModule PlatformModule
        {
            get { return _platformModule; }
        }
    }
}
