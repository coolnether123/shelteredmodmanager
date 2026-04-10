namespace Cortex.Shell.Shared.Models
{
    public sealed class RendererShellWindowModel
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public bool IsCollapsed { get; set; }
        public string Title { get; set; }

        public RendererShellWindowModel()
        {
            Title = string.Empty;
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
        public RendererLayoutNodeModel LayoutRoot { get; set; }

        public RendererShellFrameModel()
        {
            MainWindow = new RendererShellWindowModel();
            LogsWindow = new RendererShellWindowModel();
        }
    }
}
