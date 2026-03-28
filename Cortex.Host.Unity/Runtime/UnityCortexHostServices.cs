using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Presentation.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityCortexHostServices : ICortexHostServices
    {
        private readonly ICortexHostEnvironment _environment;
        private readonly IPathInteractionService _pathInteractionService;
        private readonly IWorkbenchRuntimeFactory _workbenchRuntimeFactory;
        private readonly ICortexPlatformModule _platformModule;
        private readonly ICortexShellHostUi _shellHostUi;

        public UnityCortexHostServices()
            : this(
                new UnityCortexHostEnvironment(),
                new WindowsPathInteractionService(),
                new UnityWorkbenchRuntimeFactory(),
                null,
                new UnityCortexShellHostUi())
        {
        }

        public UnityCortexHostServices(
            ICortexHostEnvironment environment,
            IPathInteractionService pathInteractionService,
            IWorkbenchRuntimeFactory workbenchRuntimeFactory,
            ICortexPlatformModule platformModule,
            ICortexShellHostUi shellHostUi)
        {
            _environment = environment;
            _pathInteractionService = pathInteractionService;
            _workbenchRuntimeFactory = workbenchRuntimeFactory;
            _platformModule = platformModule;
            _shellHostUi = shellHostUi;
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

        public ICortexShellHostUi ShellHostUi
        {
            get { return _shellHostUi; }
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
