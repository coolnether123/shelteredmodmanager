using System.Collections.Generic;
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

        ICortexShellHostUi ShellHostUi { get; }

        string PreferredLanguageProviderId { get; }

        IList<ILanguageProviderFactory> LanguageProviderFactories { get; }
    }

    public enum CortexShellInputEventKind
    {
        None = 0,
        MouseDown = 1,
        MouseUp = 2,
        MouseDrag = 3,
        KeyDown = 4,
        Repaint = 5,
        ScrollWheel = 6,
        Other = 7
    }

    public enum CortexShellInputKey
    {
        None = 0,
        Escape = 1,
        Other = 2
    }

    public struct CortexShellPointerPosition
    {
        public float X;
        public float Y;

        public CortexShellPointerPosition(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public interface ICortexShellHostUi
    {
        int ScreenWidth { get; }

        int ScreenHeight { get; }

        int HotControl { get; }

        int KeyboardControl { get; }

        bool HasCurrentEvent { get; }

        CortexShellInputEventKind CurrentEventKind { get; }

        CortexShellInputEventKind CurrentEventRawKind { get; }

        CortexShellInputKey CurrentKey { get; }

        int CurrentMouseButton { get; }

        CortexShellPointerPosition CurrentMousePosition { get; }

        CortexShellPointerPosition PointerPosition { get; }

        void ConsumeCurrentEvent();
    }

    public interface ICortexHostCompositionRoot
    {
        ICortexLogSink LogSink { get; }

        ICortexHostServices HostServices { get; }
    }
}
