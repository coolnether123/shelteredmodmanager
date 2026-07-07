using System;
using System.Globalization;
using ModAPI.Core;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Map;
using ShelteredAPI.Scenarios.Composition;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ScenarioMapAuthoringRuntimeService
    {
        private readonly ScenarioMapDraftService _draftService;
        private UI_ExpeditionMap _hoveredMap;
        private MapRegion _hoveredRegion;

        public ScenarioMapAuthoringRuntimeService(ScenarioMapDraftService draftService)
        {
            _draftService = draftService;
        }

        public bool OpenVanillaMap()
        {
            if (UI_PanelContainer.Instance == null || UI_PanelContainer.Instance.MapPanel == null)
                return false;
            if (UIPanelManager.Instance() == null)
                return false;

            BasePanel mapPanel = UI_PanelContainer.Instance.MapPanel;
            if (!UIPanelManager.Instance().IsPanelOnStack(mapPanel))
                UIPanelManager.Instance().PushPanel(mapPanel);
            return true;
        }

        public bool CloseVanillaMap()
        {
            if (UI_PanelContainer.Instance == null || UI_PanelContainer.Instance.MapPanel == null)
                return false;
            if (UIPanelManager.Instance() == null)
                return false;

            BasePanel mapPanel = UI_PanelContainer.Instance.MapPanel;
            if (UIPanelManager.Instance().IsPanelOnStack(mapPanel))
                UIPanelManager.Instance().PopPanel(mapPanel);
            return true;
        }

        public bool IsVanillaMapOpen()
        {
            if (UI_PanelContainer.Instance == null || UI_PanelContainer.Instance.MapPanel == null)
                return false;
            if (UIPanelManager.Instance() == null)
                return false;

            return UIPanelManager.Instance().IsPanelOnStack(UI_PanelContainer.Instance.MapPanel);
        }

        public void ObserveHoveredRegion(UI_ExpeditionMap map, MapRegion region)
        {
            if (!ScenarioAuthoringRuntimeGuards.IsMapAuthoringActive())
                return;

            _hoveredMap = map;
            _hoveredRegion = region;
        }

        public void SelectHoveredRegion(UI_ExpeditionMap map, string source)
        {
            if (!ScenarioAuthoringRuntimeGuards.IsMapAuthoringActive())
                return;
            if (_hoveredMap != null && map != null && !ReferenceEquals(_hoveredMap, map))
                return;

            SelectRegion(_hoveredRegion, null, source);
        }

        public bool SelectWorldPosition(float worldX, float worldY, string source)
        {
            if (!ScenarioAuthoringRuntimeGuards.IsMapAuthoringActive())
                return false;
            ScenarioMapRegionSelection selection;
            if (!TryCreateSelectionFromWorldPosition(worldX, worldY, ScenarioEditorController.Instance.CurrentSession, source, out selection))
                return false;

            return ApplySelection(selection);
        }

        public bool TryCreateSelectionFromWorldPosition(
            float worldX,
            float worldY,
            ScenarioEditorSession session,
            string source,
            out ScenarioMapRegionSelection selection)
        {
            selection = null;
            if (ExpeditionMap.Instance == null)
                return false;

            Vector2 worldPosition = new Vector2(worldX, worldY);
            MapRegion region = ExpeditionMap.Instance.GetRegionInWorld(worldPosition);
            if (region == null)
                return false;

            selection = BuildSelection(region, worldPosition, source);
            RefreshCapturedStatus(selection, session);
            return true;
        }

        public bool Synchronize(ScenarioAuthoringState state, ScenarioEditorSession session)
        {
            if (state == null)
                return false;

            bool changed = false;
            if (state.MapAuthoringActive)
            {
                if (ScenarioAuthoringRuntimeGuards.IsPlaytesting() || !IsVanillaMapOpen())
                {
                    state.MapAuthoringActive = false;
                    state.ShellVisible = state.MapAuthoringPreviousShellVisible || !state.ShellVisible;
                    state.MapAuthoringPreviousShellVisible = false;
                    state.StatusMessage = "Map authoring closed. Map workspace active.";
                    changed = true;
                }
            }

            if (state.MapSelection != null && RefreshCapturedStatus(state.MapSelection, session))
                changed = true;

            return changed;
        }

        private bool SelectRegion(MapRegion region, Vector2? requestedWorldPosition, string source)
        {
            if (region == null || ExpeditionMap.Instance == null)
                return false;

            ScenarioMapRegionSelection selection = BuildSelection(region, requestedWorldPosition, source);
            RefreshCapturedStatus(selection, ScenarioEditorController.Instance.CurrentSession);
            return ApplySelection(selection);
        }

        private bool ApplySelection(ScenarioMapRegionSelection selection)
        {
            if (selection == null)
                return false;

            try
            {
                ScenarioAuthoringBackendService.Instance.ApplyMapSelection(selection);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioMapAuthoring] Failed to apply map selection: " + ex.Message);
                return false;
            }

            return true;
        }

        private ScenarioMapRegionSelection BuildSelection(MapRegion region, Vector2? requestedWorldPosition, string source)
        {
            ExpeditionMap.GridRef grid = region.gridReference;
            Vector2 worldPosition = requestedWorldPosition.HasValue
                ? requestedWorldPosition.Value
                : ExpeditionMap.Instance.GetGridRefCentreWorldPos(grid);

            ScenarioMapRegionSelection selection = new ScenarioMapRegionSelection();
            selection.SelectionId = "vanilla:" + grid.x.ToString(CultureInfo.InvariantCulture) + ":" + grid.y.ToString(CultureInfo.InvariantCulture);
            selection.RegionName = region.regionName;
            selection.TownName = region.townName;
            selection.Category = region.category;
            selection.Topography = region.topography.ToString();
            selection.GridX = grid.x;
            selection.GridY = grid.y;
            selection.WorldX = worldPosition.x;
            selection.WorldY = worldPosition.y;
            selection.DisplayName = ResolveDisplayName(region, selection);
            selection.Searchable = region.isSearchable;
            selection.VisibleOnMap = region.isVisibleOnMap;
            selection.Discovered = region.discovered;
            selection.HiddenUntilDiscovered = region.isHiddenUntilDiscovered;
            selection.HasItems = region.hasItems;
            selection.HasQuest = region.hasQuest;
            selection.HasHiddenItems = SafeHasHiddenItems(region);
            selection.MaxItems = region.maxItems;
            selection.LocationSpecificLootTypeCount = region.locationSpecificItemTypes != null ? region.locationSpecificItemTypes.Count : 0;
            selection.MinSearchTime = region.minSearchTime;
            selection.MaxSearchTime = region.maxSearchTime;
            selection.SearchNpcRevealChance = region.chanceThatSearchRevealsNpcs;
            selection.OpenGroundEncounterChance = region.chanceOfOpenGroundEncounter;
            selection.OpenGroundFactionEncounterChance = region.chanceOfOpenGroundFactionEncounter;
            selection.AnimalEncounterChance = region.chanceThatEncounterIsAnimal;
            selection.Source = source ?? "map";
            if (_draftService != null)
                selection.LocationId = _draftService.BuildLocationId(selection);
            return selection;
        }

        private bool RefreshCapturedStatus(ScenarioMapRegionSelection selection, ScenarioEditorSession session)
        {
            if (selection == null || _draftService == null)
                return false;

            string id = _draftService.BuildLocationId(selection);
            bool captured = _draftService.HasLocation(session, id);
            bool changed = selection.Captured != captured
                || !string.Equals(selection.CapturedLocationId, captured ? id : null, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(selection.LocationId, id, StringComparison.OrdinalIgnoreCase);
            selection.LocationId = id;
            selection.Captured = captured;
            selection.CapturedLocationId = captured ? id : null;
            return changed;
        }

        private static bool SafeHasHiddenItems(MapRegion region)
        {
            try { return region != null && region.AreThereHiddenItems(); }
            catch { return false; }
        }

        private static string ResolveDisplayName(MapRegion region, ScenarioMapRegionSelection selection)
        {
            if (!string.IsNullOrEmpty(selection.TownName))
                return selection.TownName;

            try
            {
                string localised = region.GetLocalisedName();
                if (!string.IsNullOrEmpty(localised) && !localised.StartsWith("Region.Name.", StringComparison.Ordinal))
                    return localised;
            }
            catch
            {
            }

            if (!string.IsNullOrEmpty(selection.RegionName))
                return selection.RegionName;
            if (!string.IsNullOrEmpty(selection.Topography))
                return selection.Topography;
            return selection.SelectionId;
        }
    }
}
