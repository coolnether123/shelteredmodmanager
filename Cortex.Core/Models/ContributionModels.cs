using System;

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

    public enum SettingValueKind
    {
        String,
        Integer,
        Float,
        Boolean
    }

    public enum SettingEditorKind
    {
        Auto,
        Text,
        Path,
        MultilineText,
        Choice,
        Secret
    }

    public enum SettingValidationSeverity
    {
        None,
        Info,
        Warning,
        Error
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
        public string ContextId;
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
        public string Description;
        public bool IsDefault;
        public bool ShowInOnboarding;
        public bool SupportsCustomColors;
        public string BackgroundColor;
        public string SurfaceColor;
        public string HeaderColor;
        public string BorderColor;
        public string AccentColor;
        public string TextColor;
        public string MutedTextColor;
        public string WarningColor;
        public string ErrorColor;
        public string FontRole;
        public int SortOrder;
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
        public SettingValueKind ValueKind;
        public SettingEditorKind EditorKind;
        public string PlaceholderText;
        public string HelpText;
        public string[] Keywords;
        public SettingChoiceOption[] Options;
        public bool IsSecret;
        public bool IsRequired;
        public Func<string> ReadValue;
        public Action<string> WriteValue;
        public Func<string> ReadDefaultValue;
        public Func<string, SettingValidationResult> ValidateValue;
        public SettingActionContribution[] Actions;
        public int SortOrder;
    }

    public sealed class SettingChoiceOption
    {
        public string Value;
        public string DisplayName;
        public string Description;
    }

    public sealed class SettingValidationResult
    {
        public SettingValidationSeverity Severity;
        public string Message;
    }

    public sealed class SettingActionContribution
    {
        public string ActionId;
        public string DisplayName;
        public string Description;
        public Action<SettingActionInvocation> Execute;
    }

    public sealed class SettingActionInvocation
    {
        public string SettingId;
        public string CurrentValue;
        public string DefaultValue;
        public Action<string> SetDraftValue;
        public Action<string> SetStatusMessage;
    }

    public sealed class SettingSectionContribution
    {
        public string Scope;
        public string GroupId;
        public string GroupTitle;
        public string SectionId;
        public string SectionTitle;
        public string Description;
        public string[] Keywords;
        public int SortOrder;
    }
}
