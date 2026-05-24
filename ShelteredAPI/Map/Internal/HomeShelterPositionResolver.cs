using System;
using System.Reflection;
using UnityEngine;

namespace ShelteredAPI.Map.Internal
{
    internal static class HomeShelterPositionResolver
    {
        private const string BunkerApiTypeName = "BunkerRandomLocation.API.BunkerAPI, BunkerRandomLocation";

        public static bool TryResolveWorldPosition(ExplorationManager exploration, out Vector2 worldPosition)
        {
            if (TryResolveRegisteredWorldPosition(out worldPosition))
                return true;

            if (TryResolveBunkerRandomLocationWorldPosition(out worldPosition))
                return true;

            return TryResolveVanillaWorldPosition(exploration, out worldPosition);
        }

        public static bool TryResolveMapPixels(ExplorationManager exploration, out Vector2 mapPixels)
        {
            mapPixels = Vector2.zero;

            HomeShelterPositionSnapshot snapshot;
            if (HomeShelterPositionRegistry.TryGetActive(out snapshot) && snapshot.HasMapPosition)
            {
                mapPixels = new Vector2(snapshot.MapPosition.X, snapshot.MapPosition.Y);
                return IsUsable(mapPixels);
            }

            Vector2 worldPosition;
            if (TryResolveBunkerRandomLocationWorldPosition(out worldPosition))
            {
                if (exploration == null)
                    return false;

                mapPixels = new Vector2(
                    exploration.WorldToMapPixelsX(worldPosition.x),
                    exploration.WorldToMapPixelsY(worldPosition.y));
                return IsUsable(mapPixels);
            }

            if (GameModeManager.instance == null)
                return false;

            Vector3 vanilla = GameModeManager.instance.shelterMapWorldPosition;
            mapPixels = new Vector2(vanilla.x, vanilla.y);
            return IsUsable(mapPixels);
        }

        private static bool TryResolveRegisteredWorldPosition(out Vector2 worldPosition)
        {
            worldPosition = Vector2.zero;

            HomeShelterPositionSnapshot snapshot;
            if (!HomeShelterPositionRegistry.TryGetActive(out snapshot) || !snapshot.HasWorldPosition)
                return false;

            worldPosition = new Vector2(snapshot.WorldPosition.X, snapshot.WorldPosition.Y);
            return IsUsable(worldPosition);
        }

        private static bool TryResolveBunkerRandomLocationWorldPosition(out Vector2 worldPosition)
        {
            worldPosition = Vector2.zero;

            Type apiType = Type.GetType(BunkerApiTypeName);
            if (apiType == null)
                return false;

            if (TryInvokeWorldPositionMethod(apiType, "TryGetActiveWorldPosition", out worldPosition))
                return true;

            return TryInvokeWorldPositionMethod(apiType, "TryGetPrimaryWorldPosition", out worldPosition);
        }

        private static bool TryInvokeWorldPositionMethod(Type apiType, string methodName, out Vector2 worldPosition)
        {
            worldPosition = Vector2.zero;

            MethodInfo method = apiType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                return false;

            object[] args = { Vector2.zero };
            object result = method.Invoke(null, args);
            if (!(result is bool) || !(bool)result)
                return false;

            if (!(args[0] is Vector2))
                return false;

            worldPosition = (Vector2)args[0];
            return IsUsable(worldPosition);
        }

        private static bool TryResolveVanillaWorldPosition(ExplorationManager exploration, out Vector2 worldPosition)
        {
            worldPosition = Vector2.zero;
            if (GameModeManager.instance == null || exploration == null)
                return false;

            Vector3 mapPosition = GameModeManager.instance.shelterMapWorldPosition;
            worldPosition = exploration.MapPixelsToWorld(new Vector2(mapPosition.x, mapPosition.y));
            return IsUsable(worldPosition);
        }

        private static bool IsUsable(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.x)
                && !float.IsInfinity(value.y);
        }
    }
}
