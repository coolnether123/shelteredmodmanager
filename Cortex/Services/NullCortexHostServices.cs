using Cortex.Core.Abstractions;
using Cortex.Presentation.Abstractions;

namespace Cortex.Services
{
    internal sealed class NullCortexHostServices : ICortexHostServices
    {
        public static readonly NullCortexHostServices Instance = new NullCortexHostServices();

        private readonly ICortexHostEnvironment _environment = new NullCortexHostEnvironment();
        private readonly ICortexShellHostUi _shellHostUi = new NullCortexShellHostUi();

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

        public ICortexShellHostUi ShellHostUi
        {
            get { return _shellHostUi; }
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

        private sealed class NullCortexShellHostUi : ICortexShellHostUi
        {
            public int ScreenWidth
            {
                get { return 1920; }
            }

            public int ScreenHeight
            {
                get { return 1080; }
            }

            public int HotControl
            {
                get { return 0; }
            }

            public int KeyboardControl
            {
                get { return 0; }
            }

            public bool HasCurrentEvent
            {
                get { return false; }
            }

            public CortexShellInputEventKind CurrentEventKind
            {
                get { return CortexShellInputEventKind.None; }
            }

            public CortexShellInputEventKind CurrentEventRawKind
            {
                get { return CortexShellInputEventKind.None; }
            }

            public CortexShellInputKey CurrentKey
            {
                get { return CortexShellInputKey.None; }
            }

            public int CurrentMouseButton
            {
                get { return -1; }
            }

            public CortexShellPointerPosition CurrentMousePosition
            {
                get { return new CortexShellPointerPosition(0f, 0f); }
            }

            public CortexShellPointerPosition PointerPosition
            {
                get { return new CortexShellPointerPosition(0f, 0f); }
            }

            public void ConsumeCurrentEvent()
            {
            }
        }
    }
}
