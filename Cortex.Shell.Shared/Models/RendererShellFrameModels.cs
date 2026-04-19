namespace Cortex.Shell.Shared.Models
{
    public static class RendererShellChromeDefaults
    {
        public const string BrandText = "Cortex";
        public const string CollapseActionId = "cortex.shell.mainWindow.toggleCollapsed";
        public const string CloseActionId = "cortex.shell.close";
        public const string CollapseActionLabel = "_";
        public const string CloseActionLabel = "X";
        public const float MainWindowCollapsedWidth = 126f;
        public const float MainWindowCollapsedHeight = 28f;
        public const float LogsWindowCollapsedWidth = 110f;
        public const float LogsWindowCollapsedHeight = 26f;
        public const float WindowPadding = 6f;
        public const float TitleBarHeight = 24f;
        public const float HeaderHeight = 30f;
        public const float StatusHeight = 24f;
        public const float HeaderWorkbenchGap = 2f;
        public const float WorkbenchStatusGap = 3f;
    }

    public enum RendererShellChromeMode
    {
        IntegratedWindowHeader = 0,
        RendererDefault = 1
    }

    public sealed class RendererShellWindowModel
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float CollapsedWidth { get; set; }
        public float CollapsedHeight { get; set; }
        public bool IsCollapsed { get; set; }
        public string Title { get; set; }

        public RendererShellWindowModel()
        {
            Title = string.Empty;
            CollapsedWidth = RendererShellChromeDefaults.MainWindowCollapsedWidth;
            CollapsedHeight = RendererShellChromeDefaults.MainWindowCollapsedHeight;
        }
    }

    public sealed class RendererShellChromeModel
    {
        public RendererShellChromeMode ChromeMode { get; set; }
        public string BrandText { get; set; }
        public string ContextText { get; set; }
        public float WindowPadding { get; set; }
        public float TitleBarHeight { get; set; }
        public float HeaderHeight { get; set; }
        public float StatusHeight { get; set; }
        public float HeaderWorkbenchGap { get; set; }
        public float WorkbenchStatusGap { get; set; }
        public bool UseGlobalMainMenu { get; set; }
        public bool ShowWindowActions { get; set; }
        public bool ShowToolbarItems { get; set; }
        public string CollapseActionId { get; set; }
        public string CloseActionId { get; set; }
        public string CollapseActionLabel { get; set; }
        public string CloseActionLabel { get; set; }

        public RendererShellChromeModel()
        {
            ChromeMode = RendererShellChromeMode.IntegratedWindowHeader;
            BrandText = RendererShellChromeDefaults.BrandText;
            ContextText = string.Empty;
            WindowPadding = RendererShellChromeDefaults.WindowPadding;
            TitleBarHeight = RendererShellChromeDefaults.TitleBarHeight;
            HeaderHeight = RendererShellChromeDefaults.HeaderHeight;
            StatusHeight = RendererShellChromeDefaults.StatusHeight;
            HeaderWorkbenchGap = RendererShellChromeDefaults.HeaderWorkbenchGap;
            WorkbenchStatusGap = RendererShellChromeDefaults.WorkbenchStatusGap;
            UseGlobalMainMenu = false;
            ShowWindowActions = true;
            ShowToolbarItems = false;
            CollapseActionId = RendererShellChromeDefaults.CollapseActionId;
            CloseActionId = RendererShellChromeDefaults.CloseActionId;
            CollapseActionLabel = RendererShellChromeDefaults.CollapseActionLabel;
            CloseActionLabel = RendererShellChromeDefaults.CloseActionLabel;
        }
    }

    public sealed class RendererLayoutNodeModel
    {
        public string NodeId { get; set; }
        public string SplitDirection { get; set; }
        public float SplitRatio { get; set; }
        public string HostLocationId { get; set; }
        public string ActiveContainerId { get; set; }
        public string[] ContainedContainerIds { get; set; }
        public RendererLayoutNodeModel FirstChild { get; set; }
        public RendererLayoutNodeModel SecondChild { get; set; }

        public RendererLayoutNodeModel()
        {
            NodeId = string.Empty;
            SplitDirection = string.Empty;
            HostLocationId = string.Empty;
            ActiveContainerId = string.Empty;
            ContainedContainerIds = new string[0];
        }
    }

    public sealed class RendererShellFrameModel
    {
        public bool IsVisible { get; set; }
        public bool ShowDetachedLogsWindow { get; set; }
        public bool OnboardingActive { get; set; }
        public RendererShellWindowModel MainWindow { get; set; }
        public RendererShellWindowModel LogsWindow { get; set; }
        public RendererShellChromeModel Chrome { get; set; }
        public RendererLayoutNodeModel LayoutRoot { get; set; }

        public RendererShellFrameModel()
        {
            MainWindow = new RendererShellWindowModel();
            LogsWindow = new RendererShellWindowModel();
            Chrome = new RendererShellChromeModel();
        }
    }
}
