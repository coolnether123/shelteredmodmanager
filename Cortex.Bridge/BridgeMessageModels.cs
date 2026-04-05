using System.Collections.Generic;
using Cortex.Shell.Shared.Models;

namespace Cortex.Bridge
{
    public static class DesktopBridgeProtocol
    {
        public const int Version = 2;
        public const string DefaultPipeName = "cortex.desktop.bridge";
        public const string PipeNameEnvironmentVariable = "CORTEX_DESKTOP_BRIDGE_PIPE_NAME";
        public const string DefaultClientDisplayName = "Cortex Desktop Host";
        public const string DefaultRuntimeDisplayName = "Cortex Runtime";
    }

    public enum BridgeMessageType
    {
        OpenSessionRequest = 0,
        SessionOpened = 1,
        WorkbenchSnapshot = 2,
        UserIntent = 3,
        OperationResult = 4,
        Diagnostic = 5,
        OverlayPresentationSnapshot = 6,
        OverlayInputIntent = 7,
        OverlayHostLifecycle = 8,
        OverlayWindowStateChanged = 9,
        Heartbeat = 10
    }

    public enum BridgeIntentType
    {
        SelectOnboardingProfile = 0,
        SelectOnboardingLayout = 1,
        SelectOnboardingTheme = 2,
        SetOnboardingWorkspaceRoot = 3,
        ApplyOnboarding = 4,
        SetWorkspaceRoot = 5,
        SaveSettings = 6,
        SelectSettingsSection = 7,
        SelectSetting = 8,
        SetSettingValue = 9,
        SetSettingsSearchQuery = 10,
        AnalyzeWorkspace = 11,
        ImportWorkspace = 12,
        SelectProject = 13,
        OpenFilePreview = 14,
        UpdateSearch = 15,
        OpenSearchResult = 16
    }

    public enum BridgeOperationStatus
    {
        Accepted = 0,
        Completed = 1,
        Rejected = 2
    }

    public sealed class BridgeMessageEnvelope
    {
        public int ProtocolVersion { get; set; } = DesktopBridgeProtocol.Version;
        public string MessageId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public BridgeMessageType MessageType { get; set; }
        public OpenSessionRequestMessage OpenSessionRequest { get; set; }
        public SessionOpenedMessage SessionOpened { get; set; }
        public WorkbenchSnapshotMessage WorkbenchSnapshot { get; set; }
        public BridgeIntentMessage Intent { get; set; }
        public BridgeOperationResultMessage OperationResult { get; set; }
        public BridgeDiagnosticMessage Diagnostic { get; set; }
        public OverlayPresentationSnapshotMessage OverlayPresentationSnapshot { get; set; }
        public OverlayInputIntentMessage OverlayInputIntent { get; set; }
        public OverlayHostLifecycleMessage OverlayHostLifecycle { get; set; }
        public OverlayWindowStateChangedMessage OverlayWindowStateChanged { get; set; }
        public BridgeHeartbeatMessage Heartbeat { get; set; }
    }

    public sealed class OpenSessionRequestMessage
    {
        public string ClientName { get; set; } = DesktopBridgeProtocol.DefaultClientDisplayName;
        public int RequestedProtocolVersion { get; set; } = DesktopBridgeProtocol.Version;
        public string LaunchToken { get; set; } = string.Empty;
        public BridgeCapabilitySet Capabilities { get; set; } = new BridgeCapabilitySet();
    }

    public sealed class SessionOpenedMessage
    {
        public string RuntimeDisplayName { get; set; } = DesktopBridgeProtocol.DefaultRuntimeDisplayName;
        public string PipeName { get; set; } = DesktopBridgeProtocol.DefaultPipeName;
        public int AcceptedProtocolVersion { get; set; } = DesktopBridgeProtocol.Version;
        public string StatusMessage { get; set; } = string.Empty;
        public BridgeCapabilitySet Capabilities { get; set; } = new BridgeCapabilitySet();
        public string LaunchToken { get; set; } = string.Empty;
    }

    public sealed class WorkbenchSnapshotMessage
    {
        public long Revision { get; set; }
        public WorkbenchBridgeSnapshot Snapshot { get; set; } = new WorkbenchBridgeSnapshot();
    }

    public sealed class BridgeIntentMessage
    {
        public string RequestId { get; set; } = string.Empty;
        public BridgeIntentType IntentType { get; set; }
        public string ProfileId { get; set; } = string.Empty;
        public string LayoutPresetId { get; set; } = string.Empty;
        public string ThemeId { get; set; } = string.Empty;
        public string WorkspaceRootPath { get; set; } = string.Empty;
        public string SectionId { get; set; } = string.Empty;
        public string SettingId { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
        public string SearchQuery { get; set; } = string.Empty;
        public WorkbenchSearchScope SearchScope { get; set; } = WorkbenchSearchScope.CurrentDocument;
        public bool MatchCase { get; set; }
        public bool WholeWord { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int ResultIndex { get; set; } = -1;
    }

    public sealed class BridgeOperationResultMessage
    {
        public string RequestId { get; set; } = string.Empty;
        public BridgeIntentType IntentType { get; set; }
        public BridgeOperationStatus Status { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }

    public sealed class BridgeDiagnosticMessage
    {
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string UtcTimestamp { get; set; } = string.Empty;
    }

    public sealed class BridgeHeartbeatMessage
    {
        public long WorkbenchRevision { get; set; }
        public long OverlayRevision { get; set; }
        public string UtcTimestamp { get; set; } = string.Empty;
    }

    public sealed class WorkbenchBridgeSnapshot
    {
        public string WorkbenchId { get; set; } = "default";
        public string ActiveLayoutPresetId { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public string RuntimeConnectionState { get; set; } = "ready";
        public WorkbenchCatalogSnapshot Catalog { get; set; } = new WorkbenchCatalogSnapshot();
        public OnboardingState Onboarding { get; set; } = new OnboardingState();
        public OnboardingFlowModel OnboardingFlow { get; set; } = new OnboardingFlowModel();
        public string ThemePreviewSummary { get; set; } = string.Empty;
        public SettingsBridgeSnapshot Settings { get; set; } = new SettingsBridgeSnapshot();
        public WorkspaceBridgeSnapshot Workspace { get; set; } = new WorkspaceBridgeSnapshot();
        public EditorWorkbenchModel Editor { get; set; } = new EditorWorkbenchModel();
        public SearchWorkbenchModel Search { get; set; } = new SearchWorkbenchModel();
        public ReferenceWorkbenchModel Reference { get; set; } = new ReferenceWorkbenchModel();
        public OverlayPresentationSnapshot Overlay { get; set; } = new OverlayPresentationSnapshot();
    }

    public sealed class SettingsBridgeSnapshot
    {
        public ShellSettings CurrentSettings { get; set; } = new ShellSettings();
        public SettingsDocumentModel Document { get; set; } = new SettingsDocumentModel();
        public List<SettingsSectionModel> VisibleSections { get; set; } = new List<SettingsSectionModel>();
        public List<SettingDescriptor> ActiveSettings { get; set; } = new List<SettingDescriptor>();
        public string SelectedSectionId { get; set; } = string.Empty;
        public string SelectedSettingId { get; set; } = string.Empty;
        public string SearchQuery { get; set; } = string.Empty;
        public bool ShowModifiedOnly { get; set; }
        public List<BridgeSettingValueEntry> DraftValues { get; set; } = new List<BridgeSettingValueEntry>();
    }

    public sealed class WorkspaceBridgeSnapshot
    {
        public string WorkspaceRootPath { get; set; } = string.Empty;
        public List<WorkspaceProjectDefinition> Projects { get; set; } = new List<WorkspaceProjectDefinition>();
        public string SelectedProjectId { get; set; } = string.Empty;
        public WorkspaceFileNode WorkspaceTreeRoot { get; set; }
        public string PreviewFilePath { get; set; } = string.Empty;
        public string PreviewText { get; set; } = string.Empty;
    }

    public sealed class BridgeSettingValueEntry
    {
        public string SettingId { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
