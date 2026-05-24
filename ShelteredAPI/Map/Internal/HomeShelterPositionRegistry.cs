using System;
using System.Collections.Generic;

namespace ShelteredAPI.Map.Internal
{
    internal static class HomeShelterPositionRegistry
    {
        private static readonly object Sync = new object();
        private static readonly List<HomeShelterPositionRegistration> Registrations =
            new List<HomeShelterPositionRegistration>();

        internal static MapPolicyRegistrationResult Register(HomeShelterPositionRegistration registration)
        {
            string error = Validate(registration);
            if (error != null)
                return MapPolicyRegistrationResult.Failed(error);

            HomeShelterPositionRegistration copy = Copy(registration);
            lock (Sync)
            {
                bool replaced = false;
                for (int i = Registrations.Count - 1; i >= 0; i--)
                {
                    HomeShelterPositionRegistration existing = Registrations[i];
                    if (existing != null
                        && string.Equals(existing.SourceId, copy.SourceId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.HomeId, copy.HomeId, StringComparison.OrdinalIgnoreCase))
                    {
                        Registrations.RemoveAt(i);
                        replaced = true;
                    }
                }

                Registrations.Add(copy);
                return MapPolicyRegistrationResult.Ok(replaced);
            }
        }

        internal static int Unregister(string sourceId, string homeId)
        {
            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(homeId))
                return 0;

            int removed = 0;
            lock (Sync)
            {
                for (int i = Registrations.Count - 1; i >= 0; i--)
                {
                    HomeShelterPositionRegistration registration = Registrations[i];
                    if (registration != null
                        && string.Equals(registration.SourceId, sourceId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(registration.HomeId, homeId, StringComparison.OrdinalIgnoreCase))
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
                    HomeShelterPositionRegistration registration = Registrations[i];
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

        internal static bool TryGetPrimary(out HomeShelterPositionSnapshot snapshot)
        {
            return TryResolve(delegate(HomeShelterPositionRegistration item) { return item.IsPrimary; }, out snapshot)
                || TryResolve(null, out snapshot);
        }

        internal static bool TryGetActive(out HomeShelterPositionSnapshot snapshot)
        {
            return TryResolve(delegate(HomeShelterPositionRegistration item) { return item.IsActive; }, out snapshot)
                || TryGetPrimary(out snapshot);
        }

        private static bool TryResolve(
            Predicate<HomeShelterPositionRegistration> predicate,
            out HomeShelterPositionSnapshot snapshot)
        {
            snapshot = null;
            HomeShelterPositionRegistration best = null;
            lock (Sync)
            {
                for (int i = 0; i < Registrations.Count; i++)
                {
                    HomeShelterPositionRegistration candidate = Registrations[i];
                    if (candidate == null)
                        continue;
                    if (predicate != null && !predicate(candidate))
                        continue;
                    if (best == null || Compare(candidate, best) > 0)
                        best = candidate;
                }
            }

            if (best == null)
                return false;

            snapshot = BuildSnapshot(best);
            return snapshot.HasWorldPosition || snapshot.HasGridPosition || snapshot.HasMapPosition;
        }

        private static HomeShelterPositionSnapshot BuildSnapshot(HomeShelterPositionRegistration registration)
        {
            ExpeditionMapWorldPosition? world = registration.WorldPosition;
            ExpeditionMapGridPosition? grid = registration.GridPosition;
            ExpeditionMapPixelPosition? map = registration.MapPosition;

            if (!world.HasValue && map.HasValue)
            {
                ExpeditionMapWorldPosition resolvedWorld;
                if (ExpeditionMapCoordinateConverter.TryMapPixelsToWorld(map.Value, out resolvedWorld))
                    world = resolvedWorld;
            }

            if (!world.HasValue && grid.HasValue)
            {
                ExpeditionMapWorldPosition resolvedWorld;
                if (ExpeditionMapCoordinateConverter.TryGridToWorldCenter(grid.Value, out resolvedWorld))
                    world = resolvedWorld;
            }

            if (!grid.HasValue && world.HasValue)
            {
                ExpeditionMapGridPosition resolvedGrid;
                if (ExpeditionMapCoordinateConverter.TryWorldToGrid(world.Value, out resolvedGrid))
                    grid = resolvedGrid;
            }

            if (!map.HasValue && world.HasValue)
            {
                ExpeditionMapPixelPosition resolvedMap;
                if (ExpeditionMapCoordinateConverter.TryWorldToMapPixels(world.Value, out resolvedMap))
                    map = resolvedMap;
            }

            return new HomeShelterPositionSnapshot
            {
                SourceId = registration.SourceId ?? string.Empty,
                HomeId = registration.HomeId ?? string.Empty,
                DisplayName = registration.DisplayName ?? string.Empty,
                OwnerId = registration.OwnerId,
                IsPrimary = registration.IsPrimary,
                IsActive = registration.IsActive,
                IsVisible = registration.IsVisible,
                IsOnline = registration.IsOnline,
                GenerateStartingLocations = registration.GenerateStartingLocations,
                MinimumEdgeDistanceInCells = Math.Max(0, registration.MinimumEdgeDistanceInCells),
                Priority = registration.Priority,
                HasWorldPosition = world.HasValue,
                WorldPosition = world.HasValue ? world.Value : new ExpeditionMapWorldPosition(),
                HasGridPosition = grid.HasValue,
                GridPosition = grid.HasValue ? grid.Value : new ExpeditionMapGridPosition(),
                HasMapPosition = map.HasValue,
                MapPosition = map.HasValue ? map.Value : new ExpeditionMapPixelPosition(),
                SourceReason = registration.SourceReason ?? string.Empty
            };
        }

        private static int Compare(HomeShelterPositionRegistration left, HomeShelterPositionRegistration right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
                return priority;

            if (left.IsActive != right.IsActive)
                return left.IsActive ? 1 : -1;
            if (left.IsPrimary != right.IsPrimary)
                return left.IsPrimary ? 1 : -1;

            int source = string.Compare(left.SourceId, right.SourceId, StringComparison.OrdinalIgnoreCase);
            if (source != 0)
                return -source;

            int home = string.Compare(left.HomeId, right.HomeId, StringComparison.OrdinalIgnoreCase);
            return -home;
        }

        private static string Validate(HomeShelterPositionRegistration registration)
        {
            if (registration == null)
                return "Home shelter registration cannot be null.";
            if (string.IsNullOrEmpty(registration.SourceId))
                return "SourceId is required.";
            if (string.IsNullOrEmpty(registration.HomeId))
                return "HomeId is required.";
            if (!registration.WorldPosition.HasValue
                && !registration.GridPosition.HasValue
                && !registration.MapPosition.HasValue)
            {
                return "At least one home shelter coordinate is required.";
            }
            if (registration.WorldPosition.HasValue
                && (!ExpeditionMapCoordinateConverter.IsFinite(registration.WorldPosition.Value.X)
                    || !ExpeditionMapCoordinateConverter.IsFinite(registration.WorldPosition.Value.Y)))
            {
                return "WorldPosition must be finite.";
            }
            if (registration.MapPosition.HasValue
                && (!ExpeditionMapCoordinateConverter.IsFinite(registration.MapPosition.Value.X)
                    || !ExpeditionMapCoordinateConverter.IsFinite(registration.MapPosition.Value.Y)))
            {
                return "MapPosition must be finite.";
            }

            return null;
        }

        private static HomeShelterPositionRegistration Copy(HomeShelterPositionRegistration source)
        {
            return new HomeShelterPositionRegistration
            {
                SourceId = source.SourceId,
                HomeId = source.HomeId,
                DisplayName = source.DisplayName,
                OwnerId = source.OwnerId,
                IsPrimary = source.IsPrimary,
                IsActive = source.IsActive,
                IsVisible = source.IsVisible,
                IsOnline = source.IsOnline,
                GenerateStartingLocations = source.GenerateStartingLocations,
                MinimumEdgeDistanceInCells = Math.Max(0, source.MinimumEdgeDistanceInCells),
                Priority = source.Priority,
                WorldPosition = source.WorldPosition,
                GridPosition = source.GridPosition,
                MapPosition = source.MapPosition,
                SourceReason = source.SourceReason
            };
        }
    }
}
