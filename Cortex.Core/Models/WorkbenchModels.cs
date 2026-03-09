using System;
using System.Collections.Generic;

namespace Cortex.Core.Models
{
    public enum WorkbenchHostLocation
    {
        ToolRail,
        PrimarySideHost,
        DocumentHost,
        PanelHost,
        StatusStrip,
        SecondarySideHost,
        CommandSurface
    }

    public sealed class WorkbenchState
    {
        public string ActiveWorkspaceId;
        public string ActiveContainerId;
        public string ActivePanelId;
        public string ActiveEditorGroupId;
        public bool PrimarySideHostVisible;
        public bool PanelHostVisible;
        public bool SecondarySideHostVisible;
        public bool CommandSurfaceVisible;

        public WorkbenchState()
        {
            ActiveWorkspaceId = "default";
            ActiveContainerId = string.Empty;
            ActivePanelId = string.Empty;
            ActiveEditorGroupId = "main";
            PrimarySideHostVisible = true;
            PanelHostVisible = true;
            SecondarySideHostVisible = false;
            CommandSurfaceVisible = false;
        }
    }

    public sealed class LayoutState
    {
        public float PrimarySideWidth;
        public float SecondarySideWidth;
        public float PanelSize;
        public bool PanelMaximized;
        public readonly Dictionary<string, float> HostDimensions;

        public LayoutState()
        {
            PrimarySideWidth = 360f;
            SecondarySideWidth = 320f;
            PanelSize = 280f;
            PanelMaximized = false;
            HostDimensions = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed class EditorState
    {
        public string DocumentId;
        public int CaretLine;
        public int CaretColumn;
        public int SelectionStart;
        public int SelectionLength;
        public float VerticalScroll;
        public float HorizontalScroll;
        public bool ReadOnly;

        public EditorState()
        {
            DocumentId = string.Empty;
        }
    }

    public sealed class EditorGroupState
    {
        public string GroupId;
        public string ActiveDocumentId;
        public string PreviewDocumentId;
        public readonly List<string> OpenDocumentIds;
        public readonly List<string> PinnedDocumentIds;
        public readonly List<string> StickyDocumentIds;

        public EditorGroupState()
        {
            GroupId = "main";
            ActiveDocumentId = string.Empty;
            PreviewDocumentId = string.Empty;
            OpenDocumentIds = new List<string>();
            PinnedDocumentIds = new List<string>();
            StickyDocumentIds = new List<string>();
        }
    }

    public sealed class PanelState
    {
        public string ActivePanelId;
        public bool Visible;
        public bool Maximized;
        public float Size;

        public PanelState()
        {
            ActivePanelId = string.Empty;
            Visible = true;
            Maximized = false;
            Size = 280f;
        }
    }

    public sealed class CommandPaletteState
    {
        public string ProviderId;
        public string QueryText;
        public int SelectionIndex;
        public readonly List<string> RecentCommandIds;

        public CommandPaletteState()
        {
            ProviderId = string.Empty;
            QueryText = string.Empty;
            RecentCommandIds = new List<string>();
        }
    }

    public sealed class StatusState
    {
        public readonly List<string> LeftItemIds;
        public readonly List<string> RightItemIds;
        public string TransientMessage;

        public StatusState()
        {
            LeftItemIds = new List<string>();
            RightItemIds = new List<string>();
            TransientMessage = string.Empty;
        }
    }

    public sealed class ThemeState
    {
        public string ThemeId;
        public string IconThemeId;
        public string DensityProfileId;

        public ThemeState()
        {
            ThemeId = "cortex.default";
            IconThemeId = "cortex.default";
            DensityProfileId = "compact";
        }
    }
}
