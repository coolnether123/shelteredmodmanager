namespace Cortex.Core.Models
{
    public enum ModuleActivationKind
    {
        Immediate,
        OnContainerOpen,
        OnCommand,
        OnWorkspaceAvailable,
        OnDocumentRestore
    }

    public enum MenuProjectionLocation
    {
        None,
        MainMenu,
        ContextMenu,
        Toolbar,
        CommandPalette,
        StatusStrip
    }

    public enum StatusItemAlignment
    {
        Left,
        Right
    }

    public sealed class ViewContainerContribution
    {
        public string ContainerId;
        public string Title;
        public string IconId;
        public WorkbenchHostLocation DefaultHostLocation;
        public int SortOrder;
        public bool PinnedByDefault;
        public ModuleActivationKind ActivationKind;
        public string ActivationTarget;
    }

    public sealed class ViewContribution
    {
        public string ViewId;
        public string ContainerId;
        public string Title;
        public string PersistenceId;
        public int SortOrder;
        public bool VisibleByDefault;
    }

    public sealed class EditorContribution
    {
        public string EditorId;
        public string DisplayName;
        public string ResourceExtension;
        public string ContentType;
        public int SortOrder;
    }

    public sealed class MenuContribution
    {
        public string CommandId;
        public MenuProjectionLocation Location;
        public string Group;
        public int SortOrder;
    }

    public sealed class StatusItemContribution
    {
        public string ItemId;
        public string Text;
        public string ToolTip;
        public string CommandId;
        public string Severity;
        public StatusItemAlignment Alignment;
        public int Priority;
    }

    public sealed class ThemeContribution
    {
        public string ThemeId;
        public string DisplayName;
    }

    public sealed class IconContribution
    {
        public string IconId;
        public string Alias;
    }

    public sealed class SettingContribution
    {
        public string SettingId;
        public string DisplayName;
        public string Description;
        public string Scope;
        public string DefaultValue;
    }
}
