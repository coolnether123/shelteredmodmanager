using Cortex.Core.Abstractions;
using Cortex.Core.Diagnostics;

namespace Cortex.Presentation.Abstractions
{
    public interface ICortexHostEnvironment
    {
        string GameRootPath { get; }

        string HostRootPath { get; }

        string HostBinPath { get; }

        string ManagedAssemblyRootPath { get; }

        string ModsRootPath { get; }

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

        ICortexPlatformModule PlatformModule { get; }
    }

    public interface ICortexHostCompositionRoot
    {
        ICortexLogSink LogSink { get; }

        ICortexHostServices HostServices { get; }
    }
}
