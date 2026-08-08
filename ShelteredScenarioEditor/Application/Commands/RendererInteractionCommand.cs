using System;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Map;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal static class RendererInteractionAutomationIds
    {
        public const string ActionRendererMapFilterTogglePrefix = "shell.renderer.map_filter.toggle.";
        public const string ActionRendererPixelGroupTogglePrefix = "shell.renderer.pixel_group.toggle.";
        public const string ActionRendererHomeGroupTogglePrefix = "shell.renderer.home_group.toggle.";
        public const string ActionRendererTimelineGroupTogglePrefix = "shell.renderer.timeline_group.toggle.";
        public const string ActionRendererWorkshopGroupTogglePrefix = "shell.renderer.workshop_group.toggle.";
        public const string ActionRendererAssetFavoriteTogglePrefix = "shell.renderer.asset_favorite.toggle.";
        public const string ActionRendererAssetCategorySelectPrefix = "shell.renderer.asset_category.select.";
        public const string ActionRendererAssetSearchPrefix = "shell.renderer.asset_search.";
        public const string ActionRendererAssetSearchClear = "shell.renderer.asset_search.clear";
        public const string ActionRendererAssetInventoryFilterPrefix = "shell.renderer.asset_inventory_filter.select.";
        public const string ActionRendererCandidateSearchPrefix = "shell.renderer.candidate_search.set.";
        public const string ActionRendererCandidateFilterPrefix = "shell.renderer.candidate_filter.select.";
        public const string ActionRendererGlobalSearchQueryPrefix = "shell.renderer.global_search.query.";
        public const string ActionRendererWorkspaceSubtabSelectPrefix = "shell.renderer.workspace.subtab.select.";
        public const string ActionRendererWorkspaceEntitySelectPrefix = "shell.renderer.workspace.entity.select.";
        public const string ActionRendererWorkspaceWarningOpenPrefix = "shell.renderer.workspace.warning.open.";
        public const string ActionRendererWorkspaceGroupTogglePrefix = "shell.renderer.workspace.group.toggle.";
        public const string ActionRendererWorkspaceRowTogglePrefix = "shell.renderer.workspace.row.toggle.";
        public const string ActionRendererWorkspaceSearchSetPrefix = "shell.renderer.workspace.search.set.";
        public const string ActionRendererWorkspaceBreadcrumbSelectPrefix = "shell.renderer.workspace.breadcrumb.select.";
        public const string ActionRendererWorkspaceBackPrefix = "shell.renderer.workspace.back.";
        public const string ActionRendererTopBarMoreToggle = "shell.renderer.top_bar_more.toggle";
    }

    internal enum RendererInteractionCommandKind
    {
        ToggleMapFilter,
        SelectWorkspaceSubtab,
        SelectWorkspaceEntity,
        OpenWorkspaceWarning,
        SelectWorkspaceBreadcrumb,
        ToggleWorkspaceGroup,
        ToggleWorkspaceRow,
        SetWorkspaceSearch,
        ShowWorkspaceNavigator,
        ToggleDisclosure,
        ToggleAssetFavorite,
        SelectAssetCategory,
        SelectAssetInventoryFilter,
        SetCandidateSearch,
        SetCandidateFilter,
        SetAssetSearch,
        SetGlobalSearchQuery,
        ClearAssetSearch,
        ToggleTopBarMore
    }

    internal sealed class RendererInteractionCommand : ScenarioAuthoringCommand, IScenarioTextValueCommand
    {
        private RendererInteractionCommand(
            RendererInteractionCommandKind kind,
            string automationId,
            string key,
            string value,
            string workspaceId,
            string subtabId,
            ScenarioMapAuthoringFilter mapFilter)
            : base(automationId, ScenarioAuthoringCommandPolicy.Default)
        {
            Kind = kind;
            Key = key;
            Value = value;
            WorkspaceId = workspaceId;
            SubtabId = subtabId;
            MapFilter = mapFilter;
        }

        public RendererInteractionCommandKind Kind { get; private set; }
        public string Key { get; private set; }
        public string Value { get; private set; }
        public string WorkspaceId { get; private set; }
        public string SubtabId { get; private set; }
        public ScenarioMapAuthoringFilter MapFilter { get; private set; }

        public ScenarioAuthoringCommand WithTextValue(string value)
        {
            switch (Kind)
            {
                case RendererInteractionCommandKind.SetAssetSearch:
                    return ForValue(Kind, RendererInteractionAutomationIds.ActionRendererAssetSearchPrefix, value);
                case RendererInteractionCommandKind.SetGlobalSearchQuery:
                    return ForValue(Kind, RendererInteractionAutomationIds.ActionRendererGlobalSearchQueryPrefix, value);
                case RendererInteractionCommandKind.SetCandidateSearch:
                    return ForControlValue(Kind, RendererInteractionAutomationIds.ActionRendererCandidateSearchPrefix, Key, value);
                case RendererInteractionCommandKind.SetWorkspaceSearch:
                    return ForWorkspace(Kind, RendererInteractionAutomationIds.ActionRendererWorkspaceSearchSetPrefix, WorkspaceId, SubtabId, value);
                default:
                    return this;
            }
        }

        public static RendererInteractionCommand ForMapFilter(ScenarioMapAuthoringFilter filter)
        {
            return new RendererInteractionCommand(
                RendererInteractionCommandKind.ToggleMapFilter,
                RendererInteractionAutomationIds.ActionRendererMapFilterTogglePrefix + filter,
                null, null, null, null, filter);
        }

        public static RendererInteractionCommand ForKey(RendererInteractionCommandKind kind, string prefix, string key)
        {
            return new RendererInteractionCommand(kind, TokenId(prefix, key), key, null, null, null, default(ScenarioMapAuthoringFilter));
        }

        public static RendererInteractionCommand ForValue(RendererInteractionCommandKind kind, string prefix, string value)
        {
            return new RendererInteractionCommand(kind, TokenId(prefix, value), null, value, null, null, default(ScenarioMapAuthoringFilter));
        }

        public static RendererInteractionCommand ForControlValue(RendererInteractionCommandKind kind, string prefix, string key, string value)
        {
            return new RendererInteractionCommand(kind, TokenId(prefix, (key ?? string.Empty) + "\n" + (value ?? string.Empty)), key, value, null, null, default(ScenarioMapAuthoringFilter));
        }

        public static RendererInteractionCommand ForWorkspace(RendererInteractionCommandKind kind, string prefix, string workspaceId, string subtabId, string value)
        {
            string payload = (workspaceId ?? string.Empty) + "\n" + (subtabId ?? string.Empty) + "\n" + (value ?? string.Empty);
            return new RendererInteractionCommand(kind, TokenId(prefix, payload), null, value, workspaceId, subtabId, default(ScenarioMapAuthoringFilter));
        }

        public static RendererInteractionCommand ForSimple(RendererInteractionCommandKind kind, string automationId)
        {
            return new RendererInteractionCommand(kind, automationId, null, null, null, null, default(ScenarioMapAuthoringFilter));
        }

        private static string TokenId(string prefix, string value)
        {
            return (prefix ?? string.Empty) + ScenarioAutomationIdCodec.EncodeToken(value);
        }
    }

    internal static class RendererInteractionActionFactory
    {
        public static ScenarioAuthoringInspectorAction Create(
            RendererInteractionCommand command,
            string label,
            bool emphasized,
            bool enabled = true)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Id = command != null ? command.AutomationId : string.Empty,
                Command = command,
                Label = label,
                Enabled = enabled,
                Emphasized = emphasized
            };
        }
    }

    internal sealed class TypedRendererInteractionCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioAuthoringRendererInteractionState _rendererInteraction;
        private readonly ScenarioAuthoringSettingsService _settingsService;

        internal TypedRendererInteractionCommandHandler(
            ScenarioAuthoringRendererInteractionState rendererInteraction,
            ScenarioAuthoringSettingsService settingsService = null)
        {
            _rendererInteraction = rendererInteraction;
            _settingsService = settingsService;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is RendererInteractionCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            RendererInteractionCommand renderer = command as RendererInteractionCommand;
            if (state == null || renderer == null)
                return Result(false, false, "Renderer interaction was unavailable.");

            switch (renderer.Kind)
            {
                case RendererInteractionCommandKind.ToggleMapFilter:
                    ScenarioMapAuthoringFilterState.Toggle(renderer.MapFilter);
                    return Result(true, true, "Map filter toggled: " + renderer.MapFilter + ".");
                case RendererInteractionCommandKind.SelectWorkspaceSubtab:
                    _rendererInteraction.SetWorkspaceSubtab(renderer.WorkspaceId, renderer.SubtabId);
                    return Result(true, true, null);
                case RendererInteractionCommandKind.SelectWorkspaceEntity:
                case RendererInteractionCommandKind.OpenWorkspaceWarning:
                case RendererInteractionCommandKind.SelectWorkspaceBreadcrumb:
                    _rendererInteraction.SetWorkspaceSubtab(renderer.WorkspaceId, renderer.SubtabId);
                    _rendererInteraction.SetWorkspaceSelection(renderer.WorkspaceId, renderer.SubtabId, renderer.Value);
                    _rendererInteraction.SetWorkspaceNarrowPane(renderer.WorkspaceId, renderer.SubtabId, true);
                    return Result(true, true, null);
                case RendererInteractionCommandKind.ToggleWorkspaceGroup:
                case RendererInteractionCommandKind.ToggleWorkspaceRow:
                    bool expanded = _rendererInteraction.GetWorkspaceExpanded(renderer.WorkspaceId, renderer.SubtabId, renderer.Value, false);
                    _rendererInteraction.SetWorkspaceExpanded(renderer.WorkspaceId, renderer.SubtabId, renderer.Value, !expanded);
                    return Result(true, true, null);
                case RendererInteractionCommandKind.SetWorkspaceSearch:
                    _rendererInteraction.SetWorkspaceSearch(renderer.WorkspaceId, renderer.SubtabId, renderer.Value);
                    return Result(true, true, null);
                case RendererInteractionCommandKind.ShowWorkspaceNavigator:
                    _rendererInteraction.SetWorkspaceNarrowPane(renderer.WorkspaceId, renderer.SubtabId, false);
                    return Result(true, true, null);
                case RendererInteractionCommandKind.ToggleDisclosure:
                    _rendererInteraction.ToggleDisclosure(renderer.Key);
                    return Result(true, true, "Disclosure toggled: " + renderer.Key + ".");
                case RendererInteractionCommandKind.ToggleAssetFavorite:
                    ScenarioAssetBrowserUx.ToggleFavorite(state, renderer.Key, _settingsService);
                    return Result(true, true, "Asset favorite toggled.");
                case RendererInteractionCommandKind.SelectAssetCategory:
                    _rendererInteraction.AssetBrowserCategory = renderer.Key;
                    return Result(true, true, "Asset category selected: " + renderer.Key + ".");
                case RendererInteractionCommandKind.SelectAssetInventoryFilter:
                    _rendererInteraction.AssetInventoryFilter = renderer.Key;
                    return Result(true, true, "Asset inventory filter selected: " + renderer.Key + ".");
                case RendererInteractionCommandKind.SetCandidateSearch:
                    _rendererInteraction.SetCandidateSearch(renderer.Key, renderer.Value);
                    return Result(true, true, "Candidate search updated: " + renderer.Key + ".");
                case RendererInteractionCommandKind.SetCandidateFilter:
                    _rendererInteraction.SetCandidateFilter(renderer.Key, renderer.Value);
                    return Result(true, true, "Candidate filter updated: " + renderer.Key + ".");
                case RendererInteractionCommandKind.SetAssetSearch:
                    _rendererInteraction.AssetBrowserSearch = renderer.Value ?? string.Empty;
                    return Result(true, true, "Asset search updated.");
                case RendererInteractionCommandKind.SetGlobalSearchQuery:
                    _rendererInteraction.GlobalSearchQuery = renderer.Value ?? string.Empty;
                    return Result(true, true, "Global search query updated.");
                case RendererInteractionCommandKind.ClearAssetSearch:
                    _rendererInteraction.AssetBrowserSearch = string.Empty;
                    return Result(true, true, "Asset search cleared.");
                case RendererInteractionCommandKind.ToggleTopBarMore:
                    _rendererInteraction.TopBarMoreOpen = !_rendererInteraction.TopBarMoreOpen;
                    return Result(true, true, _rendererInteraction.TopBarMoreOpen ? "Stage overflow opened." : "Stage overflow closed.");
                default:
                    return Result(false, false, "Renderer command was not recognized.");
            }
        }

        private static ScenarioCommandDispatchResult Result(bool handled, bool changed, string message)
        {
            return new ScenarioCommandDispatchResult
            {
                Handled = handled,
                Changed = changed,
                Message = message
            };
        }
    }
}
