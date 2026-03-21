using Cortex.Core.Abstractions;
using Cortex.Presentation.Abstractions;

namespace Cortex.Services
{
    internal sealed class NullCortexHostServices : ICortexHostServices
    {
        public static readonly NullCortexHostServices Instance = new NullCortexHostServices();

        private readonly ICortexHostEnvironment _environment = new NullCortexHostEnvironment();

        private NullCortexHostServices()
        {
        }

        public ICortexHostEnvironment Environment
        {
            get { return _environment; }
        }

        public IPathInteractionService PathInteractionService
        {
            get { return null; }
        }

        public IWorkbenchRuntimeFactory WorkbenchRuntimeFactory
        {
            get { return null; }
        }

        public ICortexPlatformModule PlatformModule
        {
            get { return NullCortexPlatformModule.Instance; }
        }

        private sealed class NullCortexHostEnvironment : ICortexHostEnvironment
        {
            public string GameRootPath
            {
                get { return string.Empty; }
            }

            public string HostRootPath
            {
                get { return string.Empty; }
            }

            public string HostBinPath
            {
                get { return string.Empty; }
            }

            public string ManagedAssemblyRootPath
            {
                get { return string.Empty; }
            }

            public string ModsRootPath
            {
                get { return string.Empty; }
            }

            public string SettingsFilePath
            {
                get { return string.Empty; }
            }

            public string WorkbenchPersistenceFilePath
            {
                get { return string.Empty; }
            }

            public string LogFilePath
            {
                get { return string.Empty; }
            }

            public string ProjectCatalogPath
            {
                get { return string.Empty; }
            }

            public string DecompilerCachePath
            {
                get { return string.Empty; }
            }
        }
    }
}
