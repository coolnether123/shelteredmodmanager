using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Presentation.Abstractions;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityCortexHostServices : ICortexHostServices
    {
        private readonly ICortexHostEnvironment _environment;
        private readonly IPathInteractionService _pathInteractionService;
        private readonly IWorkbenchRuntimeFactory _workbenchRuntimeFactory;
        private readonly ICortexPlatformModule _platformModule;
        private readonly IWorkbenchFrameContext _frameContext;

        public UnityCortexHostServices(
            ICortexHostEnvironment environment,
            IPathInteractionService pathInteractionService,
            IWorkbenchRuntimeFactory workbenchRuntimeFactory,
            ICortexPlatformModule platformModule,
            IWorkbenchFrameContext frameContext)
        {
            _environment = environment;
            _pathInteractionService = pathInteractionService;
            _workbenchRuntimeFactory = workbenchRuntimeFactory;
            _platformModule = platformModule;
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
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

        public IWorkbenchFrameContext FrameContext
        {
            get { return _frameContext; }
        }

        public string PreferredLanguageProviderId
        {
            get { return Cortex.Shell.RoslynLanguageProviderFactory.ProviderId; }
        }

        public IList<ILanguageProviderFactory> LanguageProviderFactories
        {
            get { return new List<ILanguageProviderFactory>(); }
        }
    }
}
