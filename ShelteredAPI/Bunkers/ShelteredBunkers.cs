using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Bunkers
{
    /// <summary>
    /// Stable mod-facing facade for shared Sheltered bunker placement and ownership state.
    /// </summary>
    public static class ShelteredBunkers
    {
        private static readonly IShelteredBunkerService _service = new ShelteredBunkerService();

        /// <summary>
        /// Gets the shared bunker placement and ownership service.
        /// </summary>
        public static IShelteredBunkerService Service
        {
            get { return _service; }
        }

        /// <summary>
        /// Gets whether the shared bunker service is available.
        /// </summary>
        public static bool IsReady
        {
            get { return Service != null; }
        }

        /// <inheritdoc cref="IShelteredBunkerService.GetPrimaryBunker" />
        public static BunkerDefinition GetPrimaryBunker()
        {
            return Service.GetPrimaryBunker();
        }

        /// <inheritdoc cref="IShelteredBunkerService.GetBunker" />
        public static BunkerDefinition GetBunker(int id)
        {
            return Service.GetBunker(id);
        }

        /// <inheritdoc cref="IShelteredBunkerService.GetAllBunkers" />
        public static IEnumerable<BunkerDefinition> GetAllBunkers()
        {
            return Service.GetAllBunkers();
        }

        /// <inheritdoc cref="IShelteredBunkerService.GetBunkerMapRecord" />
        public static BunkerMapRecord GetBunkerMapRecord(int id)
        {
            return Service.GetBunkerMapRecord(id);
        }

        /// <inheritdoc cref="IShelteredBunkerService.GetAllBunkerMapRecords" />
        public static IEnumerable<BunkerMapRecord> GetAllBunkerMapRecords()
        {
            return Service.GetAllBunkerMapRecords();
        }

        /// <summary>Creates or returns a bunker for the supplied owner ID.</summary>
        public static BunkerDefinition RequestNewBunker(int userId)
        {
            return RequestNewBunker(userId, string.Empty, true, false);
        }

        /// <summary>Creates or returns a bunker for the supplied owner ID and display name.</summary>
        public static BunkerDefinition RequestNewBunker(int userId, string displayName)
        {
            return RequestNewBunker(userId, displayName, true, false);
        }

        /// <summary>Creates or returns a bunker for the supplied owner ID, display name, and starter-house setting.</summary>
        public static BunkerDefinition RequestNewBunker(int userId, string displayName, bool enableStarterHouses)
        {
            return RequestNewBunker(userId, displayName, enableStarterHouses, false);
        }

        /// <inheritdoc cref="IShelteredBunkerService.RequestNewBunker" />
        public static BunkerDefinition RequestNewBunker(int userId, string displayName, bool enableStarterHouses, bool force)
        {
            return Service.RequestNewBunker(userId, displayName, enableStarterHouses, force);
        }

        /// <inheritdoc cref="IShelteredBunkerService.RegisterBunker" />
        public static void RegisterBunker(BunkerDefinition bunker)
        {
            Service.RegisterBunker(bunker);
        }

        /// <inheritdoc cref="IShelteredBunkerService.SetBunkerPosition" />
        public static void SetBunkerPosition(int id, Vector2 position)
        {
            Service.SetBunkerPosition(id, position);
        }

        /// <inheritdoc cref="IShelteredBunkerService.SetBunkerOnline" />
        public static void SetBunkerOnline(int id, bool online)
        {
            Service.SetBunkerOnline(id, online);
        }

        /// <inheritdoc cref="IShelteredBunkerService.SetActivePlayerId" />
        public static void SetActivePlayerId(int id)
        {
            Service.SetActivePlayerId(id);
        }

        /// <inheritdoc cref="IShelteredBunkerService.SetLocationMode" />
        public static void SetLocationMode(BunkerLocationMode mode)
        {
            Service.SetLocationMode(mode);
        }

        /// <summary>Checks whether a world position is home for any registered bunker.</summary>
        public static bool IsAnyHome(Vector2 worldPos)
        {
            return IsAnyHome(worldPos, 0.1f);
        }

        /// <inheritdoc cref="IShelteredBunkerService.IsAnyHome" />
        public static bool IsAnyHome(Vector2 worldPos, float tolerance)
        {
            return Service.IsAnyHome(worldPos, tolerance);
        }

        /// <inheritdoc cref="IShelteredBunkerService.CalculatePrimaryPosition(BunkerLocationMode)" />
        public static Vector2 CalculatePrimaryPosition(BunkerLocationMode mode)
        {
            return Service.CalculatePrimaryPosition(mode);
        }

        /// <inheritdoc cref="IShelteredBunkerService.CalculatePrimaryPosition()" />
        public static Vector2 CalculatePrimaryPosition()
        {
            return Service.CalculatePrimaryPosition();
        }

        /// <inheritdoc cref="IShelteredBunkerService.CalculateSecondaryPosition" />
        public static Vector2 CalculateSecondaryPosition()
        {
            return Service.CalculateSecondaryPosition();
        }

        /// <inheritdoc cref="IShelteredBunkerService.GetActiveBunkerWorldPosition" />
        public static Vector2 GetActiveBunkerWorldPosition()
        {
            return Service.GetActiveBunkerWorldPosition();
        }

        /// <inheritdoc cref="IShelteredBunkerService.GetBunkerWorldPosition" />
        public static Vector2 GetBunkerWorldPosition(int id)
        {
            return Service.GetBunkerWorldPosition(id);
        }

        /// <inheritdoc cref="IShelteredBunkerService.GetActiveBunkerMapPixels" />
        public static Vector3 GetActiveBunkerMapPixels()
        {
            return Service.GetActiveBunkerMapPixels();
        }

        /// <inheritdoc cref="IShelteredBunkerService.GetBunkerMapPixels" />
        public static Vector3 GetBunkerMapPixels(int id)
        {
            return Service.GetBunkerMapPixels(id);
        }

        /// <inheritdoc cref="IShelteredBunkerService.GetActiveBunkerGridRef" />
        public static ExpeditionMap.GridRef GetActiveBunkerGridRef()
        {
            return Service.GetActiveBunkerGridRef();
        }

        /// <inheritdoc cref="IShelteredBunkerService.GetBunkerGridRef" />
        public static ExpeditionMap.GridRef GetBunkerGridRef(int id)
        {
            return Service.GetBunkerGridRef(id);
        }
    }
}
