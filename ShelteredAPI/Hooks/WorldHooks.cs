using System;
using UnityEngine;
using ModAPI.Core;
using ShelteredAPI.Map.Internal;

namespace ShelteredAPI.Hooks
{
    /// <summary>
    /// Metadata-focused API for world information.
    /// Provides access to the current shelter position as known by the game.
    /// When Bunker Random Location is active, its read-only authority is preferred
    /// over the vanilla GameModeManager shelter field.
    /// </summary>
    internal static class WorldHooks
    {
        /// <summary>
        /// Gets the absolute world position of the primary shelter.
        /// Returns (0,0) if no authoritative shelter position is available.
        /// </summary>
        public static Vector2 ShelterPosition
        {
            get
            {
                try
                {
                    Vector2 worldPosition;
                    if (HomeShelterPositionResolver.TryResolveWorldPosition(
                        ExplorationManager.Instance,
                        out worldPosition))
                        return worldPosition;
                }
                catch (Exception ex)
                {
                    MMLog.WarnOnce("WorldHooks.ShelterPosition", "Failed to get shelter position: " + ex.Message);
                }
                return Vector2.zero;
            }
        }

        /// <summary>
        /// Gets the grid reference for the absolute shelter position.
        /// Uses the current ExpeditionMap instance to convert the world position to grid coordinates.
        /// </summary>
        public static ExpeditionMap.GridRef ShelterGridRef
        {
            get
            {
                try
                {
                    var map = ExpeditionMap.Instance;
                    if (map != null)
                    {
                        return map.WorldPosToGridRef(ShelterPosition);
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WarnOnce("WorldHooks.ShelterGridRef", "Failed to get shelter grid ref: " + ex.Message);
                }
                return new ExpeditionMap.GridRef(0, 0);
            }
        }
    }
}
