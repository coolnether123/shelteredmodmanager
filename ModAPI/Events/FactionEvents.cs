using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Reflection;

namespace ModAPI.Events
{
    /// <summary>
    /// Events related to the game's Faction system.
    /// Allows mods to react to faction behavior without individual Harmony patches.
    /// </summary>
    public static class FactionEvents
    {
        /// <summary>
        /// Fired when a new faction has been spawned.
        /// Parameter: factionId
        /// </summary>
        public static event Action<int> OnFactionSpawned;

        /// <summary>
        /// Fired when a faction zone expands into a new area.
        /// Parameter 1: factionId
        /// Parameter 2: zoneId
        /// </summary>
        public static event Action<int, int> OnFactionZoneGrow;

        /// <summary>
        /// Fired when territory changes ownership or visibility.
        /// </summary>
        public static event Action<int, int> OnFactionTerritoryChanged;

        internal static void RaiseFactionSpawned(int factionId)
        {
            try { OnFactionSpawned?.Invoke(factionId); }
            catch (Exception ex) { ModLog.Error($"OnFactionSpawned handler failed: {ex.Message}"); }
        }

        internal static void RaiseFactionZoneGrow(int factionId, int zoneId)
        {
            try
            {
                OnFactionZoneGrow?.Invoke(factionId, zoneId);
                OnFactionTerritoryChanged?.Invoke(factionId, zoneId);
            }
            catch (Exception ex) { ModLog.Error($"OnFactionZoneGrow handler failed: {ex.Message}"); }
        }

        // Harmony patches ---------------------------------------------------

        [HarmonyPatch(typeof(FactionMan), "SpawnNewFaction")]
        private static class FactionMan_SpawnNewFaction_Patch
        {
            private static void Postfix(bool __result, int factionSpawnIndex)
            {
                if (__result)
                {
                    RaiseFactionSpawned(factionSpawnIndex);
                }
            }
        }

        [HarmonyPatch(typeof(FactionMan), "GrowFactionZone")]
        private static class FactionMan_GrowFactionZone_Patch
        {
            private static void Postfix(bool __result, object zone)
            {
                if (__result && zone != null)
                {
                    // FactionZone is a private class inside FactionMan.
                    // We use Safe to get its fields.
                    if (Safe.TryGetField<int>(zone, "m_factionId", out int factionId) &&
                        Safe.TryGetField<int>(zone, "m_zoneId", out int zoneId))
                    {
                        RaiseFactionZoneGrow(factionId, zoneId);
                    }
                }
            }
        }
    }
}
