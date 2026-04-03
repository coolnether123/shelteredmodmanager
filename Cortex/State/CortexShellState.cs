using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex
{
    public sealed class CortexWorkbenchSelectionState
    {
        public string FocusedContainerId = CortexWorkbenchIds.EditorContainer;
        public string SideContainerId = string.Empty;
        public string SecondarySideContainerId = string.Empty;
        public string EditorContainerId = CortexWorkbenchIds.EditorContainer;
        public string PanelContainerId = CortexWorkbenchIds.LogsContainer;
        public string RequestedContainerId = string.Empty;
        public int RequestedTabIndex = -1;
        public readonly Dictionary<string, WorkbenchHostLocation> HostOverrides = new Dictionary<string, WorkbenchHostLocation>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> HiddenContainerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public WorkbenchHostLocation GetAssignedHost(string containerId, WorkbenchHostLocation fallback)
        {
            WorkbenchHostLocation hostLocation;
            return !string.IsNullOrEmpty(containerId) && HostOverrides.TryGetValue(containerId, out hostLocation)
                ? hostLocation
                : fallback;
        }

        public void AssignHost(string containerId, WorkbenchHostLocation hostLocation)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return;
            }

            HostOverrides[containerId] = hostLocation;
        }

        public bool IsHidden(string containerId)
        {
            return !string.IsNullOrEmpty(containerId) && HiddenContainerIds.Contains(containerId);
        }
    }

    public enum CortexLayoutSplitDirection
    {
        None,
        Horizontal,
        Vertical
    }

    public sealed class CortexLayoutNode
    {
        public string NodeId;
        public CortexLayoutSplitDirection Split;
        public float SplitRatio;
        public WorkbenchHostLocation HostLocation;
        public CortexLayoutNode ChildA;
        public CortexLayoutNode ChildB;
        public string ActiveModuleId;
        public readonly List<string> ContainedModuleIds = new List<string>();

        public CortexLayoutNode()
        {
            NodeId = string.Empty;
            Split = CortexLayoutSplitDirection.None;
            SplitRatio = 0.5f;
            HostLocation = WorkbenchHostLocation.DocumentHost;
            ActiveModuleId = string.Empty;
        }
    }

    public sealed class CortexDocumentWorkspaceState
    {
        public DocumentSession ActiveDocument;
        public string ActiveDocumentPath;
        public readonly List<DocumentSession> OpenDocuments = new List<DocumentSession>();
    }

    public sealed class CortexEditorInteractionState
    {
        public readonly CortexHoverInteractionState Hover = new CortexHoverInteractionState();
        public readonly CortexDefinitionInteractionState Definition = new CortexDefinitionInteractionState();
        public readonly CortexCompletionInteractionState Completion = new CortexCompletionInteractionState();
        public readonly CortexSignatureHelpInteractionState SignatureHelp = new CortexSignatureHelpInteractionState();
        public readonly CortexRenameInteractionState Rename = new CortexRenameInteractionState();
        public readonly CortexPeekInteractionState Peek = new CortexPeekInteractionState();
        public readonly CortexMethodInspectorState MethodInspector = new CortexMethodInspectorState();
    }

    public sealed class CortexHoverInteractionState
    {
        public string RequestedContextKey = string.Empty;
        public string RequestedKey = string.Empty;
        public string RequestedDocumentPath = string.Empty;
        public int RequestedLine;
        public int RequestedColumn;
        public int RequestedAbsolutePosition = -1;
        public string RequestedTokenText = string.Empty;
        public string ActiveContextKey = string.Empty;
        public string VisualRefreshHoverKey = string.Empty;
        public DateTime VisualRefreshRequestedUtc = DateTime.MinValue;
    }

    public sealed class CortexDefinitionInteractionState
    {
        public string RequestedContextKey = string.Empty;
        public string RequestedKey = string.Empty;
        public string RequestedDocumentPath = string.Empty;
        public int RequestedLine;
        public int RequestedColumn;
        public int RequestedAbsolutePosition = -1;
        public string RequestedTokenText = string.Empty;
    }

    public sealed class CortexCompletionInteractionState
    {
        public readonly List<CortexAcceptedCompletionEntry> RecentAcceptedCompletions = new List<CortexAcceptedCompletionEntry>();
        public int AcceptanceSequence;
        public string RequestedContextKey = string.Empty;
        public string RequestedKey = string.Empty;
        public string RequestedDocumentPath = string.Empty;
        public int RequestedLine;
        public int RequestedColumn;
        public int RequestedAbsolutePosition = -1;
        public string RequestedTriggerCharacter = string.Empty;
        public bool RequestedExplicit;
        public string ActiveContextKey = string.Empty;
        public LanguageServiceCompletionResponse Response;
        public string PopupStateKey = string.Empty;
        public int SelectedIndex = -1;
        public string InlineContextKey = string.Empty;
        public LanguageServiceCompletionResponse InlineResponse;
        public string InlineProviderId = string.Empty;
        public string AugmentationStatus = string.Empty;
        public string AugmentationProviderId = string.Empty;
        public string AugmentationStatusMessage = string.Empty;
    }

    public sealed class CortexSignatureHelpInteractionState
    {
        public string RequestedContextKey = string.Empty;
        public string RequestedKey = string.Empty;
        public string RequestedDocumentPath = string.Empty;
        public int RequestedLine;
        public int RequestedColumn;
        public int RequestedAbsolutePosition = -1;
        public string RequestedTriggerCharacter = string.Empty;
        public bool RequestedExplicit;
        public string ActiveContextKey = string.Empty;
        public LanguageServiceSignatureHelpResponse Response;
    }

    public sealed class CortexRenameInteractionState
    {
        public string ActiveText = string.Empty;
        public string ContextKey = string.Empty;
    }

    public sealed class CortexPeekInteractionState
    {
        public string ContextKey = string.Empty;
    }

    public sealed class CortexMethodInspectorState
    {
        public bool IsVisible;
        public string Title = string.Empty;
        public string Classification = string.Empty;
        public string ContextKey = string.Empty;
        public bool OverviewExpanded = true;
        public bool NavigationExpanded = true;
        public bool RelationshipsExpanded;
        public bool ExtensionsExpanded = true;
        public readonly Dictionary<string, bool> SectionExpansionStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public bool RelationshipsRequested;
        public int RelationshipsCycle;
        public string RelationshipsTargetKey = string.Empty;
        public string RelationshipsRequestKey = string.Empty;
        public string RelationshipsStatusMessage = string.Empty;
        public LanguageServiceCallHierarchyResponse RelationshipsCallHierarchy;
    }

    public sealed class CortexSearchInteractionState
    {
        public bool IsVisible;
        public bool FocusQueryRequested;
        public bool ScopeMenuOpen;
        public bool PendingRefresh = true;
        public int ActiveMatchIndex = -1;
        public string QueryText = string.Empty;
        public string LastExecutedFingerprint = string.Empty;
        public TextSearchQuery Query = new TextSearchQuery();
        public TextSearchResultSet Results;
    }

    public enum CortexExplorerScopeMode
    {
        CurrentMod = 0,
        AllRuntime = 1
    }

    public sealed class CortexExplorerInteractionState
    {
        public bool FiltersVisible;
        public bool AdvancedFiltersVisible;
        public string FilterText = string.Empty;
        public CortexExplorerScopeMode ScopeMode = CortexExplorerScopeMode.CurrentMod;
        public readonly HashSet<string> ActiveFilterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class CortexSemanticInteractionState
    {
        public readonly CortexSemanticRequestState Request = new CortexSemanticRequestState();
        public readonly CortexQuickActionsInteractionState QuickActions = new CortexQuickActionsInteractionState();
        public readonly CortexSemanticWorkbenchState Workbench = new CortexSemanticWorkbenchState();
    }

    public sealed class CortexSemanticRequestState
    {
        public string RequestedKey = string.Empty;
        public string RequestedContextKey = string.Empty;
        public SemanticRequestKind RequestedKind = SemanticRequestKind.None;
        public string RequestedDocumentPath = string.Empty;
        public int RequestedLine;
        public int RequestedColumn;
        public int RequestedAbsolutePosition = -1;
        public string RequestedSymbolText = string.Empty;
        public string RequestedNewName = string.Empty;
        public string RequestedCommandId = string.Empty;
        public string RequestedTitle = string.Empty;
        public string RequestedApplyLabel = string.Empty;
        public bool RequestedOrganizeImports;
        public bool RequestedSimplifyNames;
        public bool RequestedFormatDocument;
    }

    public sealed class CortexQuickActionsInteractionState
    {
        public bool Visible;
        public string Title = string.Empty;
        public string FilterText = string.Empty;
        public int SelectedIndex = -1;
        public string ContextKey = string.Empty;
        public EditorResolvedContextAction[] Actions = new EditorResolvedContextAction[0];
    }

    public sealed class CortexSemanticWorkbenchState
    {
        public SemanticWorkbenchViewKind ActiveView = SemanticWorkbenchViewKind.None;
        public LanguageServiceRenameResponse RenamePreview;
        public LanguageServiceReferencesResponse References;
        public LanguageServiceDefinitionResponse PeekDefinition;
        public LanguageServiceBaseSymbolResponse BaseSymbols;
        public LanguageServiceImplementationResponse Implementations;
        public LanguageServiceCallHierarchyResponse CallHierarchy;
        public LanguageServiceValueSourceResponse ValueSource;
        public UnitTestGenerationPlan UnitTestGeneration;
        public DocumentEditPreviewPlan DocumentEditPreview;
    }

    public sealed class CortexAcceptedCompletionEntry
    {
        public string DocumentPath = string.Empty;
        public string CompletionText = string.Empty;
        public int Sequence;
    }

    public sealed class CortexLogSelectionState
    {
        public RuntimeLogEntry SelectedEntry;
        public int SelectedFrameIndex = -1;
    }

    public sealed class CortexInterfaceDiagnosticState
    {
        public readonly List<string> Entries = new List<string>();

        public void Add(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            Entries.Add(System.DateTime.Now.ToString("HH:mm:ss") + "  " + message);
            if (Entries.Count > 40)
            {
                Entries.RemoveAt(0);
            }
        }
    }

    public sealed class CortexEditorContextState
    {
        public string ActiveSurfaceId = string.Empty;
        public string ActiveContextKey = string.Empty;
        public string HoveredContextKey = string.Empty;
        public string HoveredDefinitionDocumentPath = string.Empty;
        public readonly Dictionary<string, string> SurfaceContextKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, EditorContextSnapshot> ContextsByKey = new Dictionary<string, EditorContextSnapshot>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class CortexShellState
    {
        public readonly CortexWorkbenchSelectionState Workbench = new CortexWorkbenchSelectionState();
        public readonly CortexOnboardingState Onboarding = new CortexOnboardingState();
        public readonly CortexDocumentWorkspaceState Documents = new CortexDocumentWorkspaceState();
        public readonly CortexEditorContextState EditorContext = new CortexEditorContextState();
        public readonly CortexEditorInteractionState Editor = new CortexEditorInteractionState();
        public readonly CortexExplorerInteractionState Explorer = new CortexExplorerInteractionState();
        public readonly CortexSearchInteractionState Search = new CortexSearchInteractionState();
        public readonly CortexSemanticInteractionState Semantic = new CortexSemanticInteractionState();
        public readonly CortexModuleRuntimeState Modules = new CortexModuleRuntimeState();
        public readonly CortexLogSelectionState Logs = new CortexLogSelectionState();
        public readonly CortexInterfaceDiagnosticState Diagnostics = new CortexInterfaceDiagnosticState();
        public CortexProjectDefinition SelectedProject;
        public BuildResult LastBuildResult;
        public DecompilerResponse LastReferenceResult;
        public LanguageRuntimeSnapshot LanguageRuntime = new LanguageRuntimeSnapshot();
        public CortexSettings Settings;
        public string StatusMessage;
        public bool ReloadSettingsRequested;
        public bool OpenOnboardingRequested;
    }
}
