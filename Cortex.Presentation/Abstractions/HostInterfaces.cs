using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Diagnostics;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Presentation.Abstractions
{
    public interface ICortexHostEnvironment
    {
        string ApplicationRootPath { get; }

        string HostRootPath { get; }

        string HostBinPath { get; }

        string BundledPluginSearchRoots { get; }

        string ConfiguredPluginSearchRoots { get; }

        string ReferenceAssemblyRootPath { get; }

        string RuntimeContentRootPath { get; }

        string SettingsFilePath { get; }

        string WorkbenchPersistenceFilePath { get; }

        string LogFilePath { get; }

        string ProjectCatalogPath { get; }

        string DecompilerCachePath { get; }
    }

    public interface ICortexHostServices
    {
        ICortexHostEnvironment Environment { get; }

        IPathInteractionService PathInteractionService { get; }

        IWorkbenchRuntimeFactory WorkbenchRuntimeFactory { get; }

        /// <summary>
        /// The single loader/platform attachment point consumed by the Cortex host.
        /// Host code must use this abstraction rather than reference loader-specific types.
        /// </summary>
        ICortexPlatformModule PlatformModule { get; }

        IWorkbenchFrameContext FrameContext { get; }

        string PreferredLanguageProviderId { get; }

        IList<ILanguageProviderFactory> LanguageProviderFactories { get; }
    }

    public interface ICortexHostCompositionRoot
    {
        ICortexLogSink LogSink { get; }

        ICortexHostServices HostServices { get; }
    }
}
