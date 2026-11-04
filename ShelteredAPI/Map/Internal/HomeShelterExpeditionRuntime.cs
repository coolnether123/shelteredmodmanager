using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Reflection;
using UnityEngine;

namespace ShelteredAPI.Map.Internal
{
    /// <summary>
    /// Points vanilla expedition code at the registered home shelter instead of the vanilla
    /// shelter position. Every method is inert until a mod registers a home shelter position,
    /// so unmodded games keep vanilla behavior.
    /// </summary>
    internal static class HomeShelterExpeditionRuntime
    {
        // Map instance id -> registry version it was last snapped or cleaned at. A new version
        // (the home moved or was re-registered) redoes both.
        private static readonly Dictionary<int, int> SnappedMaps = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> SanitizedMaps = new Dictionary<int, int>();
        private static int _cachedVersion = -1;
        private static bool _hasHome;
        private static Vector2 _homeWorld;

        private static bool IsInactive
        {
            get { return ModRuntime.IsQuitting || (LoadingScreen.Instance != null && LoadingScreen.Instance.isShowing); }
        }

        /// <summary>Registered home shelter world position, cached per registry version.</summary>
        internal static bool TryGetHomeWorld(out Vector2 world)
        {
            int version = HomeShelterPositionRegistry.Version;
            if (version != _cachedVersion)
            {
                HomeShelterPositionSnapshot snapshot;
                bool registered = HomeShelterPositionRegistry.TryGetActive(out snapshot);
                _hasHome = registered && snapshot.HasWorldPosition
                    && IsFinite(snapshot.WorldPosition.X) && IsFinite(snapshot.WorldPosition.Y);
                _homeWorld = _hasHome ? new Vector2(snapshot.WorldPosition.X, snapshot.WorldPosition.Y) : Vector2.zero;
                // A grid- or pixel-only registration can resolve once the map exists, so only
                // cache a miss when nothing is registered at all.
                if (_hasHome || !registered)
                    _cachedVersion = version;
            }

            world = _homeWorld;
            return _hasHome;
        }

        /// <summary>Replacement for GameModeManager.shelterMapWorldPosition reads in expedition code.</summary>
        internal static Vector3 ShelterMapPosition(GameModeManager manager)
        {
            Vector2 world;
            ExplorationManager exploration = ExplorationManager.Instance;
            if (!ModRuntime.IsQuitting && exploration != null && exploration.mapSourceSprite != null && TryGetHomeWorld(out world))
            {
                float x = exploration.WorldToMapPixelsX(world.x);
                float y = exploration.WorldToMapPixelsY(world.y);
                if (IsFinite(x) && IsFinite(y))
                    return new Vector3(x, y, 0f);
            }

            return manager.shelterMapWorldPosition;
        }

        internal static void OnPartyAwake(ExplorationParty party)
        {
            Vector2 home;
            if (IsInactive || party == null || ExplorationManager.Instance == null || !TryGetHomeWorld(out home))
                return;

            SyncPartyToHome(party, home);
        }

        /// <summary>
        /// Moves both route endpoints to the home shelter after SetRoute. Vanilla builds the route
        /// from Vector2.zero, which is too ambiguous to replace safely in IL.
        /// </summary>
        internal static void OnRouteSet(ExplorationParty party)
        {
            Vector2 home;
            if (IsInactive || party == null || !TryGetHomeWorld(out home))
                return;

            try
            {
                List<Vector2> route = Safe.GetField<List<Vector2>>(party, "m_route");
                if (route != null && route.Count > 0)
                {
                    int last = route.Count - 1;
                    bool changed = false;
                    if (Vector2.Distance(route[0], home) > 0.01f)
                    {
                        route[0] = home;
                        changed = true;
                    }
                    if (last > 0 && Vector2.Distance(route[last], home) > 0.01f)
                    {
                        route[last] = home;
                        changed = true;
                    }

                    // An endpoint moved, so rebuild the first leg from the first two waypoints.
                    if (changed && party.hasRoute && route.Count > 1)
                        Safe.SetField(party, "m_currentLeg", route[1] - route[0]);
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredMap] Home shelter route repair failed: " + ex.Message);
            }

            SyncPartyToHome(party, home);
        }

        /// <summary>Vanilla recalls the party nearest the world origin; measure from the home shelter instead.</summary>
        internal static bool TryRecallNearest(ExplorationManager manager, out bool recalled)
        {
            recalled = false;
            Vector2 home;
            if (IsInactive || manager == null || !TryGetHomeWorld(out home))
                return false;

            ExplorationParty nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (ExplorationParty party in manager.GetAllExplorarionParties())
            {
                if (party == null)
                    continue;
                float distance = Vector2.Distance(party.location, home);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = party;
                }
            }

            if (nearest != null)
            {
                nearest.RecallToShelter();
                recalled = true;
            }
            return true;
        }

        internal static void OnMapEnabled(UI_ExpeditionMap map)
        {
            if (map == null)
                return;
            SnappedMaps.Remove(map.GetInstanceID());
            OnMapUpdate(map);
        }

        internal static void OnMapDisabled(UI_ExpeditionMap map)
        {
            if (map != null)
                SnappedMaps.Remove(map.GetInstanceID());
        }

        /// <summary>Centers the map camera on the home shelter each time the map opens or the home moves.</summary>
        internal static void OnMapUpdate(UI_ExpeditionMap map)
        {
            int snappedVersion;
            if (IsInactive || map == null
                || (SnappedMaps.TryGetValue(map.GetInstanceID(), out snappedVersion) && snappedVersion == HomeShelterPositionRegistry.Version))
                return;

            Vector2 home;
            ExplorationManager exploration = ExplorationManager.Instance;
            if (exploration == null || !TryGetHomeWorld(out home))
                return;

            try
            {
                Vector3 pixels = new Vector3(exploration.WorldToMapPixelsX(home.x), exploration.WorldToMapPixelsY(home.y), 0f);
                if (!IsFinite(pixels.x) || !IsFinite(pixels.y))
                    return;

                Camera mapCamera = Safe.GetField<Camera>(map, "m_mapCamera");
                if (mapCamera != null)
                {
                    float zoom = Safe.GetField<float>(map, "m_mapZoom");
                    if (zoom <= 0f)
                        zoom = mapCamera.orthographicSize;

                    // The map UI is authored at 1080 px tall.
                    float halfViewHeight = 540f;
                    float halfViewWidth = halfViewHeight * mapCamera.aspect;
                    float clampX = Mathf.Max(exploration.mapImageWidth * 0.5f - halfViewWidth * zoom, 0f);
                    float clampY = Mathf.Max(exploration.mapImageHeight * 0.5f - halfViewHeight * zoom, 0f);

                    Vector3 position = mapCamera.transform.localPosition;
                    position.x = Mathf.Clamp(pixels.x, -clampX, clampX);
                    position.y = Mathf.Clamp(pixels.y, -clampY, clampY);
                    mapCamera.transform.localPosition = position;
                }

                // Keep the cursor centered so it resolves to the snapped camera point.
                if (map.cursor != null)
                    map.cursor.transform.localPosition = Vector3.zero;

                HideVanillaShelterIcon(home);
                SnappedMaps[map.GetInstanceID()] = HomeShelterPositionRegistry.Version;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredMap] Home shelter map snap failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Replaces vanilla UpdateRubberBand while a home shelter is registered. Vanilla has no guard
        /// for non-finite or off-map endpoints, which a relocated home can produce.
        /// </summary>
        internal static bool TryUpdateRubberBand(GameObject rubberBand, Vector3 start, Vector3 end)
        {
            Vector2 home;
            if (!TryGetHomeWorld(out home))
                return false;
            if (rubberBand == null)
                return true;

            if (!IsFinite(start) || !IsFinite(end))
            {
                rubberBand.SetActive(false);
                return true;
            }

            float maxDistance = GetMaxRubberBandLength();
            if (maxDistance > 0f && Vector3.Distance(start, end) > maxDistance)
            {
                Vector3 offset = end - start;
                if (offset.sqrMagnitude <= Mathf.Epsilon)
                {
                    rubberBand.SetActive(false);
                    return true;
                }
                end = start + offset.normalized * maxDistance;
            }

            if (!rubberBand.activeSelf)
                rubberBand.SetActive(true);

            Vector3 direction = end - start;
            direction = direction.sqrMagnitude <= Mathf.Epsilon ? Vector3.right : direction.normalized;
            rubberBand.transform.localPosition = (start + end) / 2f;
            UISprite sprite = rubberBand.GetComponent<UISprite>();
            if (sprite != null)
            {
                sprite.transform.right = direction;
                sprite.width = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(start, end)));
            }
            return true;
        }

        internal static void ResetSession()
        {
            SnappedMaps.Clear();
            SanitizedMaps.Clear();
            _cachedVersion = -1;
        }

        private static void SyncPartyToHome(ExplorationParty party, Vector2 home)
        {
            Vector2 location = party.location;
            if (location.sqrMagnitude >= 0.01f && Vector2.Distance(location, home) <= 0.1f)
                return;

            Safe.SetField(party, "m_location", home);
            if (ExpeditionMap.Instance != null)
                Safe.SetField(party, "m_currentRegion", ExpeditionMap.Instance.GetRegionInWorld(home));
        }

        /// <summary>Hides the vanilla center shelter icon once per map when the home is elsewhere.</summary>
        private static void HideVanillaShelterIcon(Vector2 home)
        {
            ExpeditionMap map = ExpeditionMap.Instance;
            int sanitizedVersion;
            if (map == null
                || (SanitizedMaps.TryGetValue(map.GetInstanceID(), out sanitizedVersion) && sanitizedVersion == HomeShelterPositionRegistry.Version))
                return;

            MapRegion[,] regions = Safe.GetField<MapRegion[,]>(map, "m_mapRegions");
            if (regions == null)
                return;

            ExpeditionMap.GridRef homeGrid = map.WorldPosToGridRef(home);
            int centerX = map.width / 2;
            int centerY = map.height / 2;
            for (int x = Mathf.Max(0, centerX - 1); x <= Mathf.Min(map.width - 1, centerX + 1); x++)
            {
                for (int y = Mathf.Max(0, centerY - 1); y <= Mathf.Min(map.height - 1, centerY + 1); y++)
                {
                    if (x == homeGrid.x && y == homeGrid.y)
                        continue;
                    MapRegion region = regions[x, y];
                    if (region != null && region.topography == MapRegion.Topography.Shelter)
                        region.SetShownOnMap(false);
                }
            }

            if (homeGrid.x >= 0 && homeGrid.x < map.width && homeGrid.y >= 0 && homeGrid.y < map.height)
            {
                MapRegion homeRegion = regions[homeGrid.x, homeGrid.y];
                if (homeRegion != null && homeRegion.topography == MapRegion.Topography.Shelter)
                    homeRegion.SetShownOnMap(true);
            }

            SanitizedMaps[map.GetInstanceID()] = HomeShelterPositionRegistry.Version;
        }

        private static float GetMaxRubberBandLength()
        {
            ExplorationManager exploration = ExplorationManager.Instance;
            if (exploration == null)
                return 0f;

            float width = Mathf.Max(1f, exploration.mapImageWidth);
            float height = Mathf.Max(1f, exploration.mapImageHeight);
            return Mathf.Sqrt(width * width + height * height) * 1.25f + 128f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
