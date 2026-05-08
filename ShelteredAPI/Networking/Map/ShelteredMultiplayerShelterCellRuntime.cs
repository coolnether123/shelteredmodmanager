using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal static class ShelteredMultiplayerShelterCellRuntime
    {
        private static readonly FieldInfo MapScratchpadField =
            AccessTools.Field(typeof(ExpeditionMap), "m_mapScratchpad");

        public static void ForceActiveBunkerShelterCell(ExpeditionMap map)
        {
            if (map == null)
                return;

            Vector2 worldPosition;
            if (!ShelteredMultiplayerBunkerAnchorRuntime.TryGetActiveBunkerWorldPosition(out worldPosition))
                return;
            if (worldPosition.sqrMagnitude <= 0.0001f)
                return;

            Array scratchpad = MapScratchpadField != null ? MapScratchpadField.GetValue(map) as Array : null;
            if (scratchpad == null)
                return;

            ExpeditionMap.GridRef gridRef = map.WorldPosToGridRef(worldPosition);
            if (gridRef.x < 0 || gridRef.x >= scratchpad.GetLength(0) || gridRef.y < 0 || gridRef.y >= scratchpad.GetLength(1))
                return;

            object cell = scratchpad.GetValue(gridRef.x, gridRef.y);
            if (cell == null)
                return;

            FieldInfo typeField = AccessTools.Field(cell.GetType(), "type");
            FieldInfo categoryField = AccessTools.Field(cell.GetType(), "category");
            FieldInfo alwaysVisibleField = AccessTools.Field(cell.GetType(), "alwaysVisible");

            if (typeField != null)
                typeField.SetValue(cell, MapRegion.Topography.Shelter);
            if (categoryField != null)
                categoryField.SetValue(cell, "Shelter");
            if (alwaysVisibleField != null)
                alwaysVisibleField.SetValue(cell, true);
        }
    }
}
