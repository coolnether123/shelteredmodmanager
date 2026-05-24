using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ModAPI.Actors;
using ShelteredAPI.Actors;
using ShelteredAPI.Characters;
using ShelteredAPI.Scenarios.Domain.Map;
using UnityEngine;

namespace ShelteredAPI.Map
{
    /// <summary>
    /// Stable facade for mod-owned map markers and read-only vanilla expedition map projections.
    /// Registration is data-only; it does not mutate vanilla map generation or create UI objects.
    /// </summary>
    public static class ShelteredMapMarkers
    {
        private static readonly ModOwnedMarkerRegistry ModOwnedMarkers = new ModOwnedMarkerRegistry();

        /// <summary>
        /// Registers a detached marker owned by <see cref="MapMarkerSnapshot.SourceModId"/>.
        /// Marker IDs are unique within a source mod ID.
        /// </summary>
        public static bool RegisterModOwnedMarker(MapMarkerSnapshot marker)
        {
            return ModOwnedMarkers.Register(marker);
        }

        /// <summary>
        /// Replaces a previously registered marker with matching marker and source mod IDs.
        /// </summary>
        public static bool UpdateModOwnedMarker(MapMarkerSnapshot marker)
        {
            return ModOwnedMarkers.Update(marker);
        }

        /// <summary>
        /// Removes a marker only from the named source mod's marker set.
        /// </summary>
        public static bool RemoveModOwnedMarker(string markerId, string sourceModId)
        {
            return ModOwnedMarkers.Remove(markerId, sourceModId);
        }

        /// <summary>
        /// Returns copied snapshots for every registered mod-owned marker.
        /// </summary>
        public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotModOwnedMarkers()
        {
            return ModOwnedMarkers.Snapshot(null);
        }

        /// <summary>
        /// Returns copied snapshots for markers registered by one source mod.
        /// </summary>
        public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotModOwnedMarkers(string sourceModId)
        {
            return ModOwnedMarkers.Snapshot(sourceModId);
        }

        /// <summary>
        /// Projects the current home shelter location, or null before the expedition runtime is available.
        /// </summary>
        public static MapMarkerSnapshot SnapshotHomeShelter()
        {
            return VanillaExpeditionSnapshotReader.SnapshotHomeShelter();
        }

        /// <summary>
        /// Projects discovered, searchable vanilla locations as detached marker snapshots.
        /// Returns an empty collection before a map is initialized.
        /// </summary>
        public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotDiscoveredLocations()
        {
            return VanillaExpeditionSnapshotReader.SnapshotDiscoveredLocations();
        }

        /// <summary>
        /// Projects vanilla quest-bearing map locations as detached marker snapshots.
        /// Returns an empty collection before a map is initialized.
        /// </summary>
        public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotQuestLocations()
        {
            return VanillaExpeditionSnapshotReader.SnapshotQuestLocations();
        }

        /// <summary>
        /// Projects currently active player expedition parties as moving map markers.
        /// </summary>
        public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotPlayerPartyMarkers()
        {
            return VanillaExpeditionSnapshotReader.SnapshotPlayerPartyMarkers();
        }

        /// <summary>
        /// Projects active player expedition parties with copied route and member actor identity data.
        /// </summary>
        public static ReadOnlyCollection<ExpeditionActorSnapshot> SnapshotActiveExpeditionParties()
        {
            return VanillaExpeditionSnapshotReader.SnapshotActiveExpeditionParties();
        }

        /// <summary>
        /// Vanilla exposes no stable enumerable faction-party runtime collection.
        /// This method is an explicit safe extension point and currently returns no vanilla projections.
        /// </summary>
        public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotFactionPartyMarkers()
        {
            return EmptyMarkers();
        }

        /// <summary>
        /// Vanilla encounter actors are panel/state-machine owned rather than stable mobile map entities.
        /// Mod-owned encounter markers should be registered through this facade; this vanilla projection is empty.
        /// </summary>
        public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotMobileEncounterMarkers()
        {
            return EmptyMarkers();
        }

        private static ReadOnlyCollection<MapMarkerSnapshot> EmptyMarkers()
        {
            return new List<MapMarkerSnapshot>(0).AsReadOnly();
        }
    }

    internal sealed class ModOwnedMarkerRegistry
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, MapMarkerSnapshot> _markers =
            new Dictionary<string, MapMarkerSnapshot>(StringComparer.OrdinalIgnoreCase);

        public bool Register(MapMarkerSnapshot marker)
        {
            if (!IsValid(marker))
                return false;

            string key = BuildKey(marker.SourceModId, marker.MarkerId);
            lock (_sync)
            {
                if (_markers.ContainsKey(key))
                    return false;

                _markers.Add(key, marker.Clone());
                return true;
            }
        }

        public bool Update(MapMarkerSnapshot marker)
        {
            if (!IsValid(marker))
                return false;

            string key = BuildKey(marker.SourceModId, marker.MarkerId);
            lock (_sync)
            {
                if (!_markers.ContainsKey(key))
                    return false;

                _markers[key] = marker.Clone();
                return true;
            }
        }

        public bool Remove(string markerId, string sourceModId)
        {
            if (string.IsNullOrEmpty(markerId) || string.IsNullOrEmpty(sourceModId))
                return false;

            lock (_sync)
            {
                return _markers.Remove(BuildKey(sourceModId, markerId));
            }
        }

        public ReadOnlyCollection<MapMarkerSnapshot> Snapshot(string sourceModId)
        {
            List<MapMarkerSnapshot> result = new List<MapMarkerSnapshot>();
            lock (_sync)
            {
                foreach (MapMarkerSnapshot marker in _markers.Values)
                {
                    if (!string.IsNullOrEmpty(sourceModId)
                        && !string.Equals(marker.SourceModId, sourceModId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    result.Add(marker.Clone());
                }
            }

            result.Sort(delegate(MapMarkerSnapshot left, MapMarkerSnapshot right)
            {
                int sourceCompare = string.Compare(left.SourceModId, right.SourceModId, StringComparison.OrdinalIgnoreCase);
                return sourceCompare != 0
                    ? sourceCompare
                    : string.Compare(left.MarkerId, right.MarkerId, StringComparison.OrdinalIgnoreCase);
            });
            return result.AsReadOnly();
        }

        private static bool IsValid(MapMarkerSnapshot marker)
        {
            return marker != null
                && !string.IsNullOrEmpty(marker.MarkerId)
                && !string.IsNullOrEmpty(marker.SourceModId);
        }

        private static string BuildKey(string sourceModId, string markerId)
        {
            return (sourceModId ?? string.Empty) + "\n" + (markerId ?? string.Empty);
        }
    }

    internal static class VanillaExpeditionSnapshotReader
    {
        private const string VanillaSourceId = "vanilla";

        public static MapMarkerSnapshot SnapshotHomeShelter()
        {
            ExpeditionMapContext context = ShelteredMap.Current;
            if (context == null || !context.IsValid || !context.HasHomeShelterPosition)
                return null;

            MapMarkerSnapshot marker = new MapMarkerSnapshot
            {
                MarkerId = "vanilla.home_shelter",
                DisplayName = "Home Shelter",
                Kind = MapMarkerKind.Shelter,
                IsVisible = true,
                IsDiscovered = true,
                SourceModId = VanillaSourceId
            };
            MarkerCoordinateAdapter.ApplyWorldPosition(marker, context.HomeShelterWorldPosition);
            return marker;
        }

        public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotDiscoveredLocations()
        {
            return SnapshotRegions(delegate(MapRegion region)
            {
                return region.discovered && (region.isSearchable || region.hasQuest);
            });
        }

        public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotQuestLocations()
        {
            return SnapshotRegions(delegate(MapRegion region)
            {
                return region.hasQuest && (region.discovered || region.isVisibleOnMap);
            });
        }

        public static ReadOnlyCollection<MapMarkerSnapshot> SnapshotPlayerPartyMarkers()
        {
            ReadOnlyCollection<ExpeditionActorSnapshot> parties = SnapshotActiveExpeditionParties();
            List<MapMarkerSnapshot> markers = new List<MapMarkerSnapshot>();
            for (int i = 0; i < parties.Count; i++)
            {
                if (parties[i].Marker != null)
                    markers.Add(parties[i].Marker.Clone());
            }

            return markers.AsReadOnly();
        }

        public static ReadOnlyCollection<ExpeditionActorSnapshot> SnapshotActiveExpeditionParties()
        {
            List<ExpeditionActorSnapshot> result = new List<ExpeditionActorSnapshot>();
            ExplorationManager manager = ExplorationManager.Instance;
            if (manager == null)
                return result.AsReadOnly();

            List<ExplorationParty> parties = manager.GetAllExplorarionParties();
            if (parties == null)
                return result.AsReadOnly();

            for (int i = 0; i < parties.Count; i++)
            {
                ExplorationParty party = parties[i];
                if (party == null)
                    continue;

                ExpeditionRouteSnapshot route = SnapshotRoute(party);
                MapMarkerSnapshot marker = new MapMarkerSnapshot
                {
                    MarkerId = "vanilla.party." + party.id,
                    DisplayName = "Expedition Party " + party.id,
                    Kind = MapMarkerKind.Custom,
                    IsVisible = true,
                    IsDiscovered = true,
                    SourceModId = VanillaSourceId,
                    Route = route
                };
                MarkerCoordinateAdapter.ApplyWorldPosition(
                    marker,
                    new ExpeditionMapWorldPosition(party.location.x, party.location.y));

                List<ActorId> memberActorIds = new List<ActorId>();
                for (int memberIndex = 0; memberIndex < party.membersCount; memberIndex++)
                {
                    PartyMember member = party.GetMember(memberIndex);
                    if (member != null && member.person != null)
                        memberActorIds.Add(ShelteredActors.FamilyMemberActorId(member.person.GetId()));
                }

                result.Add(new ExpeditionActorSnapshot(new ExpeditionPartyInfo(party), memberActorIds, marker));
            }

            result.Sort(delegate(ExpeditionActorSnapshot left, ExpeditionActorSnapshot right)
            {
                return left.PartyInfo.Id.CompareTo(right.PartyInfo.Id);
            });
            return result.AsReadOnly();
        }

        private static ReadOnlyCollection<MapMarkerSnapshot> SnapshotRegions(Predicate<MapRegion> include)
        {
            List<MapMarkerSnapshot> result = new List<MapMarkerSnapshot>();
            ExpeditionMap map = ExpeditionMap.Instance;
            ExplorationManager manager = ExplorationManager.Instance;
            if (map == null || manager == null || !map.initialised)
                return result.AsReadOnly();

            for (int y = 0; y < map.height; y++)
            {
                for (int x = 0; x < map.width; x++)
                {
                    MapRegion region = map.GetRegionOnMap(new ExpeditionMap.GridRef(x, y));
                    if (region == null || include == null || !include(region))
                        continue;

                    result.Add(SnapshotRegion(region));
                }
            }

            return result.AsReadOnly();
        }

        private static MapMarkerSnapshot SnapshotRegion(MapRegion region)
        {
            MapMarkerSnapshot marker = new MapMarkerSnapshot
            {
                MarkerId = "vanilla.location." + region.gridReference.x + "." + region.gridReference.y,
                DisplayName = ResolveRegionDisplayName(region),
                Kind = ResolveRegionKind(region),
                IsVisible = region.isVisibleOnMap,
                IsDiscovered = region.discovered,
                SourceModId = VanillaSourceId
            };
            MarkerCoordinateAdapter.ApplyRegionPosition(marker, region);
            return marker;
        }

        private static ExpeditionRouteSnapshot SnapshotRoute(ExplorationParty party)
        {
            IList<Vector2> route = party.GetRoute();
            if (route == null || route.Count == 0)
                return null;

            List<ExpeditionMapWorldPosition> copy = new List<ExpeditionMapWorldPosition>();
            for (int i = 0; i < route.Count; i++)
                copy.Add(new ExpeditionMapWorldPosition(route[i].x, route[i].y));
            return new ExpeditionRouteSnapshot(copy);
        }

        private static string ResolveRegionDisplayName(MapRegion region)
        {
            if (!string.IsNullOrEmpty(region.regionName))
                return region.regionName;
            if (!string.IsNullOrEmpty(region.townName))
                return region.townName;
            return region.topography.ToString();
        }

        private static MapMarkerKind ResolveRegionKind(MapRegion region)
        {
            if (region.hasQuest)
                return MapMarkerKind.Quest;

            if (string.Equals(region.category, "City", StringComparison.OrdinalIgnoreCase))
                return MapMarkerKind.City;
            if (string.Equals(region.category, "Town", StringComparison.OrdinalIgnoreCase)
                || string.Equals(region.category, "Village", StringComparison.OrdinalIgnoreCase))
                return MapMarkerKind.Town;

            switch (region.topography)
            {
                case MapRegion.Topography.Shelter:
                    return MapMarkerKind.Shelter;
                case MapRegion.Topography.SmallHouse:
                case MapRegion.Topography.MediumHouse:
                case MapRegion.Topography.LargeHouse:
                case MapRegion.Topography.House_Stasis:
                    return MapMarkerKind.House;
                case MapRegion.Topography.SmallReservoir:
                case MapRegion.Topography.LargeReservoir:
                case MapRegion.Topography.RecyclingCentre:
                case MapRegion.Topography.LumberYard:
                case MapRegion.Topography.Lumberyard_Stasis:
                    return MapMarkerKind.Resource;
                default:
                    return MapMarkerKind.PointOfInterest;
            }
        }
    }

    /// <summary>
    /// Joins map-context grid/world projections with vanilla map-pixel projection.
    /// The direct pixel conversion can move behind map context if that facade later exposes pixels.
    /// </summary>
    internal static class MarkerCoordinateAdapter
    {
        public static void ApplyWorldPosition(MapMarkerSnapshot marker, ExpeditionMapWorldPosition worldPosition)
        {
            if (marker == null)
                return;

            marker.WorldPosition = worldPosition;
            ExplorationManager manager = ExplorationManager.Instance;
            if (manager != null)
            {
                Vector2 mapPixels = manager.WorldToMapPixels(new Vector2(worldPosition.X, worldPosition.Y));
                marker.MapPosition = new ExpeditionMapPixelPosition(mapPixels.x, mapPixels.y);
            }

            ExpeditionMapContext context = ShelteredMap.Current;
            ExpeditionMapGridPosition gridPosition;
            if (context != null && context.TryWorldToGrid(worldPosition, out gridPosition))
            {
                marker.GridPosition = gridPosition;
            }
        }

        public static void ApplyRegionPosition(MapMarkerSnapshot marker, MapRegion region)
        {
            if (marker == null || region == null)
                return;

            ExpeditionMapGridPosition gridPosition =
                new ExpeditionMapGridPosition(region.gridReference.x, region.gridReference.y);
            marker.GridPosition = gridPosition;

            ExplorationManager manager = ExplorationManager.Instance;
            ExpeditionMapContext context = ShelteredMap.Current;
            ExpeditionMapWorldPosition worldPosition;
            if (manager == null
                || context == null
                || !context.TryGridToWorldCenter(gridPosition, out worldPosition))
            {
                return;
            }

            marker.WorldPosition = worldPosition;
            Vector2 mapPixels = manager.WorldToMapPixels(new Vector2(worldPosition.X, worldPosition.Y));
            marker.MapPosition = new ExpeditionMapPixelPosition(mapPixels.x, mapPixels.y);
        }
    }
}
