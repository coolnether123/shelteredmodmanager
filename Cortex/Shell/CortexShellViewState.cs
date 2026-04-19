using Cortex.Rendering.Models;

namespace Cortex.Shell
{
    internal sealed class CortexShellWindowViewState
    {
        public RenderRect CurrentRect;
        public RenderRect ExpandedRect;
        public RenderRect CollapsedRect;
        public bool IsCollapsed;
        public readonly float CollapsedWidth;
        public readonly float CollapsedHeight;
        public readonly float MinWidth;
        public readonly float MinHeight;

        public CortexShellWindowViewState(float collapsedWidth, float collapsedHeight, float minWidth, float minHeight)
        {
            CurrentRect = new RenderRect(0f, 0f, 0f, 0f);
            ExpandedRect = new RenderRect(0f, 0f, 0f, 0f);
            CollapsedRect = new RenderRect(0f, 0f, 0f, 0f);
            CollapsedWidth = collapsedWidth;
            CollapsedHeight = collapsedHeight;
            MinWidth = minWidth;
            MinHeight = minHeight;
        }

        public static RenderRect BuildCollapsedRect(RenderRect expandedRect, float width, float height)
        {
            return new RenderRect(expandedRect.X, expandedRect.Y, width, height);
        }

        public static RenderRect ToggleCollapsed(CortexShellWindowViewState state, RenderRect currentRect, float collapsedWidth, float collapsedHeight)
        {
            if (state == null)
            {
                return currentRect;
            }

            if (!state.IsCollapsed)
            {
                state.ExpandedRect = currentRect;
                state.CollapsedRect = BuildCollapsedRect(currentRect, collapsedWidth, collapsedHeight);
                state.IsCollapsed = true;
                return state.CollapsedRect;
            }

            state.IsCollapsed = false;
            if (state.ExpandedRect.Width <= 0f || state.ExpandedRect.Height <= 0f)
            {
                state.ExpandedRect = currentRect;
            }

            return state.ExpandedRect;
        }
    }

    internal sealed class CortexShellViewState
    {
        public readonly CortexShellWindowViewState MainWindow = new CortexShellWindowViewState(126f, 28f, 920f, 580f);
        public readonly CortexShellWindowViewState LogsWindow = new CortexShellWindowViewState(110f, 26f, 760f, 420f);
        public readonly CortexShellWindowViewState OverlayControlWindow = new CortexShellWindowViewState(148f, 28f, 640f, 92f);
        public readonly CortexShellWindowViewState OverlayPrimaryWindow = new CortexShellWindowViewState(148f, 28f, 260f, 280f);
        public readonly CortexShellWindowViewState OverlayDocumentWindow = new CortexShellWindowViewState(148f, 28f, 520f, 360f);
        public readonly CortexShellWindowViewState OverlaySecondaryWindow = new CortexShellWindowViewState(148f, 28f, 260f, 280f);
        public readonly CortexShellWindowViewState OverlayPanelWindow = new CortexShellWindowViewState(148f, 28f, 420f, 180f);
        public bool ShowDetachedLogsWindow;
        public CortexLayoutNode LayoutRoot;
    }
}
