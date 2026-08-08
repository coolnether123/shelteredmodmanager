using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Core;
using UnityEngine;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Application.Map;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Composition;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Public;

namespace ShelteredScenarioEditor.Infrastructure.Unity{
    internal sealed class ScenarioMapAuthoringRuntimeService : IScenarioMapAuthoringRuntime
    {
        private readonly ScenarioMapDraftService _draftService;
        private readonly ScenarioPreviewSessionHost _previewSession;
        private UI_ExpeditionMap _hoveredMap;
        private int _hoveredGridX;
        private int _hoveredGridY;
        private bool _hasHoveredGrid;
        private readonly Dictionary<string, GameObject> _markers = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        public ScenarioMapAuthoringRuntimeService(
            ScenarioMapDraftService draftService,
            ScenarioPreviewSessionHost previewSession)
        {
            _draftService = draftService ?? throw new ArgumentNullException("draftService");
            _previewSession = previewSession ?? throw new ArgumentNullException("previewSession");
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

        public void ObserveHoveredRegion(UI_ExpeditionMap map, MapRegion region, Vector2? worldPosition)
        {
            if (!ScenarioAuthoringRuntimeGuards.IsMapAuthoringActive())
                return;

            _hoveredMap = map;
            if (region != null)
            {
                _hoveredGridX = region.gridReference.x;
                _hoveredGridY = region.gridReference.y;
                _hasHoveredGrid = true;
                return;
            }

            int gridX = 0;
            int gridY = 0;
            float centreX;
            float centreY;
            _hasHoveredGrid = worldPosition.HasValue
                && TryResolveGrid(worldPosition.Value.x, worldPosition.Value.y, out gridX, out gridY, out centreX, out centreY);
            if (_hasHoveredGrid)
            {
                _hoveredGridX = gridX;
                _hoveredGridY = gridY;
            }
        }

        public void ClickMap(UI_ExpeditionMap map, Vector2 worldPosition, string source)
        {
            if (!ScenarioAuthoringRuntimeGuards.IsMapAuthoringActive())
                return;
            if (_hoveredMap != null && map != null && !ReferenceEquals(_hoveredMap, map))
                return;

            ScenarioAuthoringBackendService.Instance.ExecuteCommand(
                MapAuthoringCommand.ClickWorldPosition(worldPosition.x, worldPosition.y));
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

        public bool TryResolveGrid(float worldX, float worldY, out int gridX, out int gridY, out float centreWorldX, out float centreWorldY)
        {
            gridX = 0;
            gridY = 0;
            centreWorldX = worldX;
            centreWorldY = worldY;
            if (ExpeditionMap.Instance == null)
                return false;

            ExpeditionMap.GridRef grid = ExpeditionMap.Instance.WorldPosToGridRef(new Vector2(worldX, worldY));
            if (grid.x < 0 || grid.y < 0 || grid.x >= ExpeditionMap.Instance.width || grid.y >= ExpeditionMap.Instance.height)
                return false;

            gridX = grid.x;
            gridY = grid.y;
            Vector2 centre = ExpeditionMap.Instance.GetGridRefCentreWorldPos(grid);
            centreWorldX = centre.x;
            centreWorldY = centre.y;
            return true;
        }

        public bool CanAuthorLocationAtGrid(int gridX, int gridY, out string reason)
        {
            reason = null;
            if (ExpeditionMap.Instance == null)
            {
                reason = "The expedition map is not ready.";
                return false;
            }

            if (gridX < 0 || gridY < 0 || gridX >= ExpeditionMap.Instance.width || gridY >= ExpeditionMap.Instance.height)
            {
                reason = "Grid " + gridX.ToString(CultureInfo.InvariantCulture) + "," + gridY.ToString(CultureInfo.InvariantCulture)
                    + " is outside the generated expedition map.";
                return false;
            }

            MapRegion region = ExpeditionMap.Instance.GetRegionOnMap(new ExpeditionMap.GridRef(gridX, gridY));
            if (region == null)
            {
                reason = "Grid " + gridX.ToString(CultureInfo.InvariantCulture) + "," + gridY.ToString(CultureInfo.InvariantCulture)
                    + " has no generated MapRegion. Empty-cell region creation is blocked because ExpeditionMap.CreateRegion is private and depends on private prefab/fog scratchpad setup.";
                return false;
            }

            if (region.topography == MapRegion.Topography.NowhereSpecial)
            {
                reason = "Grid " + gridX.ToString(CultureInfo.InvariantCulture) + "," + gridY.ToString(CultureInfo.InvariantCulture)
                    + " is an empty NowhereSpecial cell. Move the location onto a generated vanilla region; creating a new region there would bypass ExpeditionMap.CreateRegion setup.";
                return false;
            }

            return true;
        }

        public bool CanPaintTerrainAtGrid(int gridX, int gridY, string terrainId, out string reason)
        {
            reason = null;
            if (!string.Equals(terrainId, ShelteredScenarioAuthoring.GeneratedBlendTerrainId, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(terrainId, MapRegion.Topography.NowhereSpecial.ToString(), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(terrainId, MapRegion.Topography.Woodland.ToString(), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(terrainId, MapRegion.Topography.Mountains.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                reason = "Unknown map terrain '" + (terrainId ?? string.Empty) + "'.";
                return false;
            }

            if (ExpeditionMap.Instance == null)
            {
                reason = "The expedition map is not ready.";
                return false;
            }

            if (gridX < 0 || gridY < 0 || gridX >= ExpeditionMap.Instance.width || gridY >= ExpeditionMap.Instance.height)
            {
                reason = "The selected cell is outside the expedition map.";
                return false;
            }

            if (ExpeditionMap.Instance.GetRegionOnMap(new ExpeditionMap.GridRef(gridX, gridY)) == null)
            {
                reason = "The selected cell has no live map region to paint.";
                return false;
            }

            return true;
        }

        public bool PreviewTerrainDraft(ScenarioEditorSession session, out string reason)
        {
            reason = null;
            if (ExpeditionMap.Instance == null)
            {
                reason = "The expedition map is not ready.";
                return false;
            }
            if (session == null || session.WorkingDefinition == null || session.WorkingDefinition.Map == null)
            {
                reason = "The scenario map draft is not available.";
                return false;
            }

            ScenarioPreviewResult result = _previewSession.Refresh(
                session.WorkingDefinition,
                ScenarioPreviewRefreshScope.MapProjection);
            if (result == null || !result.Started)
            {
                reason = result != null && result.Messages != null && result.Messages.Length > 0
                    ? result.Messages[result.Messages.Length - 1]
                    : "Map terrain preview requires an active scenario preview session.";
                return false;
            }
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
                    CleanupMarkers();
                    state.MapAuthoringActive = false;
                    state.ShellVisible = state.MapAuthoringPreviousShellVisible || !state.ShellVisible;
                    state.MapAuthoringPreviousShellVisible = false;
                    state.StatusMessage = "Map authoring closed. Map workspace active.";
                    changed = true;
                }
                else
                {
                    RefreshMarkers(state, session);
                }
            }
            else
            {
                CleanupMarkers();
            }

            if (state.MapSelection != null && RefreshCapturedStatus(state.MapSelection, session))
                changed = true;

            return changed;
        }

        public void RefreshMarkers(ScenarioAuthoringState state, ScenarioEditorSession session)
        {
            if (!ScenarioAuthoringRuntimeGuards.IsMapAuthoringActive() || state == null || session == null || session.WorkingDefinition == null)
            {
                CleanupMarkers();
                return;
            }

            MapAuthoringDefinition map = session.WorkingDefinition.Map;
            if (map == null || map.Locations == null || ExplorationManager.Instance == null || ExplorationManager.Instance.mapSourceSprite == null)
            {
                CleanupMarkers();
                return;
            }

            Dictionary<string, bool> live = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            ScenarioMapAuthoringMarkerFilter.ApplyVanillaRegionFilter();
            ScenarioMapAuthoringMarkerFilter.ApplyTerrainBrushPreview(state, _hoveredGridX, _hoveredGridY, _hasHoveredGrid);
            for (int i = 0; i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location == null || string.IsNullOrEmpty(location.Id))
                    continue;

                live[location.Id] = true;
                GameObject marker = EnsureMarker(location.Id);
                if (marker == null)
                    continue;

                marker.transform.localPosition = BuildMarkerPosition(location);
                marker.SetActive(true);
                UISprite sprite = marker.GetComponent<UISprite>();
                if (sprite != null)
                {
                    bool selected = string.Equals(state.MapSelectedLocationId, location.Id, StringComparison.OrdinalIgnoreCase);
                    string placementReason;
                    bool placementBlocked = !CanAuthorLocationAtGrid(location.GridX, location.GridY, out placementReason);
                    float alpha = ScenarioMapAuthoringFilterState.ResolveAuthoredMarkerAlpha(map, location, placementBlocked);
                    sprite.color = selected ? new Color(1f, 0.86f, 0.15f, alpha) : new Color(0.25f, 0.95f, 1f, alpha);
                    sprite.depth = Math.Max(sprite.depth, 25);
                }
            }

            CleanupMissingMarkers(live);
        }

        public void CleanupMarkers()
        {
            ScenarioMapAuthoringMarkerFilter.RestoreVanillaRegionColors();
            foreach (GameObject marker in _markers.Values)
            {
                if (marker != null)
                    UnityEngine.Object.Destroy(marker);
            }

            _markers.Clear();
            _hasHoveredGrid = false;
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
            selection.SelectionKind = "Vanilla";
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
            selection.IconId = ResolveIconId(region);
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
            selection.Authored = false;
            if (_draftService != null)
                selection.LocationId = _draftService.BuildLocationId(selection);
            return selection;
        }

        private GameObject EnsureMarker(string id)
        {
            GameObject marker;
            if (_markers.TryGetValue(id, out marker) && marker != null)
                return marker;

            GameObject prefab = _hoveredMap != null ? _hoveredMap.waypointPrefab : null;
            if (prefab == null)
                return null;

            marker = UnityEngine.Object.Instantiate<GameObject>(prefab);
            marker.name = "ScenarioAuthoringMapMarker_" + id;
            marker.transform.parent = ExplorationManager.Instance.mapSourceSprite.gameObject.transform;
            marker.transform.localScale = !((UnityEngine.Object)DifficultyManager.instance != (UnityEngine.Object)null)
                ? new Vector3(1.2f, 1.2f, 1f)
                : DifficultyManager.instance.GetPartyMapSymbolScale() * 1.2f;
            _markers[id] = marker;
            return marker;
        }

        private static Vector3 BuildMarkerPosition(MapLocationDefinition location)
        {
            Vector2 world = ResolveLocationWorldPosition(location);
            return new Vector3(
                (float)ExplorationManager.Instance.WorldToMapPixelsX(world.x),
                (float)ExplorationManager.Instance.WorldToMapPixelsY(world.y),
                0f);
        }

        private static Vector2 ResolveLocationWorldPosition(MapLocationDefinition location)
        {
            if (ExpeditionMap.Instance != null)
                return ExpeditionMap.Instance.GetGridRefCentreWorldPos(new ExpeditionMap.GridRef(location.GridX, location.GridY));

            return new Vector2(location.X, location.Y);
        }

        private void CleanupMissingMarkers(Dictionary<string, bool> live)
        {
            List<string> remove = new List<string>();
            foreach (string id in _markers.Keys)
            {
                if (live == null || !live.ContainsKey(id))
                    remove.Add(id);
            }

            for (int i = 0; i < remove.Count; i++)
            {
                GameObject marker;
                if (_markers.TryGetValue(remove[i], out marker) && marker != null)
                    UnityEngine.Object.Destroy(marker);
                _markers.Remove(remove[i]);
            }
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

        private static string ResolveIconId(MapRegion region)
        {
            UISprite sprite = region != null ? region.GetComponent<UISprite>() : null;
            return sprite != null ? sprite.spriteName : null;
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
