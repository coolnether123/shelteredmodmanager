using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Core;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Map;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Map;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ScenarioMapAuthoringRuntimeService
    {
        private readonly ScenarioMapDraftService _draftService;
        private UI_ExpeditionMap _hoveredMap;
        private MapRegion _hoveredRegion;
        private readonly Dictionary<string, GameObject> _markers = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

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

        public void ClickMap(UI_ExpeditionMap map, Vector2 worldPosition, string source)
        {
            if (!ScenarioAuthoringRuntimeGuards.IsMapAuthoringActive())
                return;
            if (_hoveredMap != null && map != null && !ReferenceEquals(_hoveredMap, map))
                return;

            string token = worldPosition.x.ToString(CultureInfo.InvariantCulture) + "," + worldPosition.y.ToString(CultureInfo.InvariantCulture);
            string actionId = ScenarioAuthoringActionCodec.BuildTokenActionId(ScenarioAuthoringActionIds.ActionMapAuthoringClickWorldPrefix, token);
            ScenarioAuthoringBackendService.Instance.ExecuteAction(actionId);
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

        public bool TryResolveGrid(float worldX, float worldY, out int gridX, out int gridY, out float centreWorldX, out float centreWorldY)
        {
            gridX = 0;
            gridY = 0;
            centreWorldX = worldX;
            centreWorldY = worldY;
            if (ExpeditionMap.Instance == null)
                return false;

            ExpeditionMap.GridRef grid = ExpeditionMap.Instance.WorldPosToGridRef(new Vector2(worldX, worldY));
            if (grid.x < 0 || grid.y < 0 || grid.x > ExpeditionMap.Instance.width || grid.y > ExpeditionMap.Instance.height)
                return false;

            gridX = grid.x;
            gridY = grid.y;
            Vector2 centre = ExpeditionMap.Instance.GetGridRefCentreWorldPos(grid);
            centreWorldX = centre.x;
            centreWorldY = centre.y;
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
                    sprite.color = selected ? new Color(1f, 0.86f, 0.15f, 1f) : new Color(0.25f, 0.95f, 1f, 1f);
                    sprite.depth = Math.Max(sprite.depth, 25);
                }
            }

            CleanupMissingMarkers(live);
        }

        public void CleanupMarkers()
        {
            foreach (GameObject marker in _markers.Values)
            {
                if (marker != null)
                    UnityEngine.Object.Destroy(marker);
            }

            _markers.Clear();
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
