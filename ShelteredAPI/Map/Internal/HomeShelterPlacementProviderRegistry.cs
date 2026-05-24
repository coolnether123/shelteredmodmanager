using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Domain.Map;
using UnityEngine;

namespace ShelteredAPI.Map.Internal
{
    internal static class HomeShelterPlacementProviderRegistry
    {
        private const int DefaultMapWidth = 40;
        private const int DefaultMapHeight = 16;
        private const float DefaultWorldWidth = 813f;
        private const float DefaultWorldHeight = 327f;

        private static readonly object Sync = new object();
        private static readonly List<HomeShelterPlacementProviderRegistration> Registrations =
            new List<HomeShelterPlacementProviderRegistration>();

        internal static MapPolicyRegistrationResult Register(HomeShelterPlacementProviderRegistration registration)
        {
            string error = Validate(registration);
            if (error != null)
                return MapPolicyRegistrationResult.Failed(error);

            HomeShelterPlacementProviderRegistration copy = Copy(registration);
            lock (Sync)
            {
                bool replaced = false;
                for (int i = Registrations.Count - 1; i >= 0; i--)
                {
                    HomeShelterPlacementProviderRegistration existing = Registrations[i];
                    if (existing != null
                        && string.Equals(existing.SourceId, copy.SourceId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.ProviderId, copy.ProviderId, StringComparison.OrdinalIgnoreCase))
                    {
                        Registrations.RemoveAt(i);
                        replaced = true;
                    }
                }

                Registrations.Add(copy);
                return MapPolicyRegistrationResult.Ok(replaced);
            }
        }

        internal static int Unregister(string sourceId, string providerId)
        {
            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(providerId))
                return 0;

            int removed = 0;
            lock (Sync)
            {
                for (int i = Registrations.Count - 1; i >= 0; i--)
                {
                    HomeShelterPlacementProviderRegistration registration = Registrations[i];
                    if (registration != null
                        && string.Equals(registration.SourceId, sourceId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(registration.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
                    {
                        Registrations.RemoveAt(i);
                        removed++;
                    }
                }
            }

            return removed;
        }

        internal static int Clear(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
                return 0;

            int removed = 0;
            lock (Sync)
            {
                for (int i = Registrations.Count - 1; i >= 0; i--)
                {
                    HomeShelterPlacementProviderRegistration registration = Registrations[i];
                    if (registration != null
                        && string.Equals(registration.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))
                    {
                        Registrations.RemoveAt(i);
                        removed++;
                    }
                }
            }

            return removed;
        }

        internal static bool TryResolve(string reason, out HomeShelterPositionSnapshot snapshot)
        {
            snapshot = null;

            HomeShelterPlacementProviderRegistration[] providers = SnapshotProviders();
            if (providers.Length == 0)
                return false;

            HomeShelterPlacementContext context = BuildContext();
            Array.Sort(providers, CompareDescending);
            for (int i = 0; i < providers.Length; i++)
            {
                HomeShelterPlacementProviderRegistration provider = providers[i];
                HomeShelterPlacementResult result;
                try
                {
                    if (provider.Provider == null || !provider.Provider.TryResolve(context, out result))
                        continue;
                }
                catch (Exception ex)
                {
                    MMLog.WarnOnce(
                        "HomeShelterPlacementProvider." + provider.SourceId + "." + provider.ProviderId,
                        "Home shelter placement provider failed: " + ex.Message);
                    continue;
                }

                if (!TryPublish(provider, context, result, reason, out snapshot))
                    continue;

                return true;
            }

            return false;
        }

        private static HomeShelterPlacementProviderRegistration[] SnapshotProviders()
        {
            lock (Sync)
            {
                return Registrations.ToArray();
            }
        }

        private static HomeShelterPlacementContext BuildContext()
        {
            ExpeditionMap map = ExpeditionMap.Instance;
            ExplorationManager exploration = ExplorationManager.Instance;

            int width = map != null && map.width > 0 ? map.width : DefaultMapWidth;
            int height = map != null && map.height > 0 ? map.height : DefaultMapHeight;
            float worldWidth = exploration != null && exploration.worldWidth > 0f
                ? exploration.worldWidth
                : DefaultWorldWidth;
            float worldHeight = exploration != null && exploration.worldHeight > 0f
                ? exploration.worldHeight
                : DefaultWorldHeight;
            bool fromLiveMap = map != null
                && exploration != null
                && map.width > 0
                && map.height > 0
                && exploration.worldWidth > 0f
                && exploration.worldHeight > 0f;

            return new HomeShelterPlacementContext(
                width,
                height,
                worldWidth,
                worldHeight,
                fromLiveMap,
                MapGenerationPolicyRegistry.Resolve());
        }

        private static bool TryPublish(
            HomeShelterPlacementProviderRegistration provider,
            HomeShelterPlacementContext context,
            HomeShelterPlacementResult result,
            string reason,
            out HomeShelterPositionSnapshot snapshot)
        {
            snapshot = null;
            if (result == null)
                return false;

            ExpeditionMapWorldPosition? world = result.WorldPosition;
            ExpeditionMapGridPosition? grid = result.GridPosition;
            ExpeditionMapPixelPosition? map = result.MapPosition;

            if (!world.HasValue && grid.HasValue)
            {
                ExpeditionMapWorldPosition resolvedWorld;
                if (context.TryGridToWorldCenter(grid.Value, out resolvedWorld))
                    world = resolvedWorld;
                else if (ExpeditionMapCoordinateConverter.TryGridToWorldCenter(grid.Value, out resolvedWorld))
                    world = resolvedWorld;
            }

            if (!grid.HasValue && world.HasValue)
            {
                ExpeditionMapGridPosition resolvedGrid;
                if (context.TryWorldToGrid(world.Value, out resolvedGrid))
                    grid = resolvedGrid;
                else if (ExpeditionMapCoordinateConverter.TryWorldToGrid(world.Value, out resolvedGrid))
                    grid = resolvedGrid;
            }

            if (!world.HasValue && map.HasValue)
            {
                ExpeditionMapWorldPosition resolvedWorld;
                if (ExpeditionMapCoordinateConverter.TryMapPixelsToWorld(map.Value, out resolvedWorld))
                    world = resolvedWorld;
            }

            if (!map.HasValue && world.HasValue)
            {
                ExpeditionMapPixelPosition resolvedMap;
                if (ExpeditionMapCoordinateConverter.TryWorldToMapPixels(world.Value, out resolvedMap))
                    map = resolvedMap;
            }

            if (!world.HasValue && !grid.HasValue && !map.HasValue)
                return false;

            string homeId = string.IsNullOrEmpty(result.HomeId) ? "home-shelter" : result.HomeId;
            string displayName = string.IsNullOrEmpty(result.DisplayName) ? "Home Shelter" : result.DisplayName;
            string sourceReason = BuildSourceReason(provider, result, reason);

            MapGenerationPolicyRegistry.Register(new HomeShelterPlacementPolicy(
                provider.SourceId,
                provider.ProviderId,
                grid,
                Math.Max(0, result.MinimumEdgeDistanceInCells),
                provider.Priority));

            HomeShelterPositionRegistry.Register(new HomeShelterPositionRegistration
            {
                SourceId = provider.SourceId,
                HomeId = homeId,
                DisplayName = displayName,
                OwnerId = result.OwnerId,
                IsPrimary = result.IsPrimary,
                IsActive = result.IsActive,
                IsVisible = result.IsVisible,
                IsOnline = result.IsOnline,
                GenerateStartingLocations = result.GenerateStartingLocations,
                MinimumEdgeDistanceInCells = Math.Max(0, result.MinimumEdgeDistanceInCells),
                Priority = provider.Priority,
                WorldPosition = world,
                GridPosition = grid,
                MapPosition = map,
                SourceReason = sourceReason
            });

            if (result.IsVisible && world.HasValue)
                PublishMarker(provider.SourceId, homeId, displayName, world.Value, grid);

            bool resolved = result.IsPrimary
                ? HomeShelterPositionRegistry.TryGetPrimary(out snapshot)
                : HomeShelterPositionRegistry.TryGetActive(out snapshot);
            if (!resolved)
                resolved = HomeShelterPositionRegistry.TryGetActive(out snapshot);

            if (resolved)
                NotifyResolved(provider, reason, snapshot);

            return resolved;
        }

        private static void NotifyResolved(
            HomeShelterPlacementProviderRegistration provider,
            string reason,
            HomeShelterPositionSnapshot snapshot)
        {
            if (provider == null || provider.ResolutionListener == null || snapshot == null)
                return;

            try
            {
                provider.ResolutionListener.OnHomeShelterPlacementResolved(
                    new HomeShelterPlacementResolution(
                        provider.SourceId,
                        provider.ProviderId,
                        reason,
                        snapshot.Clone()));
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce(
                    "HomeShelterPlacementProvider.ResolutionListener." + provider.SourceId + "." + provider.ProviderId,
                    "Home shelter placement resolution listener failed: " + ex.Message);
            }
        }

        private static void PublishMarker(
            string sourceId,
            string homeId,
            string displayName,
            ExpeditionMapWorldPosition world,
            ExpeditionMapGridPosition? grid)
        {
            MapMarkerSnapshot marker = new MapMarkerSnapshot
            {
                MarkerId = homeId,
                DisplayName = displayName,
                Kind = MapMarkerKind.Shelter,
                IsVisible = true,
                IsDiscovered = true,
                SourceModId = sourceId,
                WorldPosition = world
            };
            if (grid.HasValue)
                marker.GridPosition = grid.Value;

            if (!ShelteredMapMarkers.UpdateModOwnedMarker(marker))
                ShelteredMapMarkers.RegisterModOwnedMarker(marker);
        }

        private static string BuildSourceReason(
            HomeShelterPlacementProviderRegistration provider,
            HomeShelterPlacementResult result,
            string reason)
        {
            string resultReason = result.SourceReason ?? string.Empty;
            string requestReason = reason ?? string.Empty;
            if (resultReason.Length == 0)
                return provider.ProviderId + ": " + requestReason;
            if (requestReason.Length == 0)
                return provider.ProviderId + ": " + resultReason;
            return provider.ProviderId + ": " + resultReason + " (" + requestReason + ")";
        }

        private static int CompareDescending(
            HomeShelterPlacementProviderRegistration left,
            HomeShelterPlacementProviderRegistration right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0)
                return priority;

            int source = string.Compare(left.SourceId, right.SourceId, StringComparison.OrdinalIgnoreCase);
            if (source != 0)
                return source;

            return string.Compare(left.ProviderId, right.ProviderId, StringComparison.OrdinalIgnoreCase);
        }

        private static string Validate(HomeShelterPlacementProviderRegistration registration)
        {
            if (registration == null)
                return "Home shelter placement provider registration cannot be null.";
            if (string.IsNullOrEmpty(registration.SourceId))
                return "SourceId is required.";
            if (string.IsNullOrEmpty(registration.ProviderId))
                return "ProviderId is required.";
            if (registration.Provider == null)
                return "Provider is required.";
            return null;
        }

        private static HomeShelterPlacementProviderRegistration Copy(HomeShelterPlacementProviderRegistration source)
        {
            return new HomeShelterPlacementProviderRegistration
            {
                SourceId = source.SourceId,
                ProviderId = source.ProviderId,
                Priority = source.Priority,
                Provider = source.Provider,
                ResolutionListener = source.ResolutionListener
            };
        }
    }
}
