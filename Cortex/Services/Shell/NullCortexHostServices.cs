using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Presentation.Abstractions;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Shell
{
    internal sealed class NullCortexHostServices : ICortexHostServices
    {
        public static readonly NullCortexHostServices Instance = new NullCortexHostServices();

        private readonly ICortexHostEnvironment _environment = new NullCortexHostEnvironment();
        private readonly IWorkbenchFrameContext _frameContext = NullWorkbenchFrameContext.Instance;

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

        public IWorkbenchFrameContext FrameContext
        {
            get { return _frameContext; }
        }

        public string PreferredLanguageProviderId
        {
            get { return string.Empty; }
        }

        public IList<ILanguageProviderFactory> LanguageProviderFactories
        {
            get { return new List<ILanguageProviderFactory>(); }
        }

        private sealed class NullCortexHostEnvironment : ICortexHostEnvironment
        {
            public string ApplicationRootPath
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

            public string BundledPluginSearchRoots
            {
                get { return string.Empty; }
            }

            public string ConfiguredPluginSearchRoots
            {
                get { return string.Empty; }
            }

            public string ReferenceAssemblyRootPath
            {
                get { return string.Empty; }
            }

            public string RuntimeContentRootPath
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
