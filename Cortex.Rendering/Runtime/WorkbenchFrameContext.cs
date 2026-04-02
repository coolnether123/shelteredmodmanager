using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi
{
    // The frame/input contract now lives in Cortex.Rendering to keep the dependency
    // direction low-level and break the portable project cycle. The namespace stays
    // stable so existing shell/host code does not need a broad rename pass.
    public enum WorkbenchInputEventKind
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

    public enum WorkbenchInputKey
    {
        None = 0,
        Escape = 1,
        Other = 2
    }

    public struct WorkbenchFrameInputSnapshot
    {
        public RenderSize ViewportSize;
        public bool AllowsVisualRefresh;
        public int HotControl;
        public int KeyboardControl;
        public bool HasCurrentEvent;
        public WorkbenchInputEventKind CurrentEventKind;
        public WorkbenchInputEventKind CurrentRawEventKind;
        public WorkbenchInputKey CurrentKey;
        public int CurrentMouseButton;
        public float WheelScrollDelta;
        public float AnalogScrollDelta;
        public int FrameId;
        public RenderPoint CurrentMousePosition;
        public RenderPoint PointerPosition;
    }

    public interface IWorkbenchFrameContext
    {
        WorkbenchFrameInputSnapshot Snapshot { get; }

        void ConsumeCurrentInput();
    }

    public sealed class NullWorkbenchFrameContext : IWorkbenchFrameContext
    {
        public static readonly IWorkbenchFrameContext Instance = new NullWorkbenchFrameContext();

        private static readonly WorkbenchFrameInputSnapshot SnapshotInstance = new WorkbenchFrameInputSnapshot
        {
            ViewportSize = new RenderSize(1920f, 1080f),
            AllowsVisualRefresh = true,
            CurrentMousePosition = RenderPoint.Zero,
            PointerPosition = RenderPoint.Zero,
            CurrentEventKind = WorkbenchInputEventKind.None,
            CurrentRawEventKind = WorkbenchInputEventKind.None,
            CurrentKey = WorkbenchInputKey.None,
            CurrentMouseButton = -1
        };

        private NullWorkbenchFrameContext()
        {
        }

        public WorkbenchFrameInputSnapshot Snapshot
        {
            get { return SnapshotInstance; }
        }

        public void ConsumeCurrentInput()
        {
        }
    }
}
