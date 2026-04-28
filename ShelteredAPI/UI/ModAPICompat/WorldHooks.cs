using System;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Hooks
{
    /// <summary>
    /// Metadata-focused API for world information.
    /// Provides access to the current shelter position as known by the game.
    /// Mods are responsible for patching GameModeManager.shelterMapWorldPosition
    /// if they wish to relocate the bunker.
    /// </summary>
    public static class WorldHooks
    {
        /// <summary>
        /// Gets the absolute world position of the primary shelter.
        /// Returns (0,0) if GameModeManager is not available or if the shelter is at the center.
        /// If a mod patches GameModeManager.shelterMapWorldPosition, this property reflects that change.
        /// </summary>
        public static Vector2 ShelterPosition
        {
            get
            {
                try
                {
                    var instance = GameModeManager.instance;
                    if (instance != null)
                    {
                        return (Vector2)instance.shelterMapWorldPosition;
                    }
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
