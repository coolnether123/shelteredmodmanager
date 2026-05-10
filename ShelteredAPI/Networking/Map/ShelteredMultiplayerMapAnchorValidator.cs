using System;
using System.Reflection;
using HarmonyLib;
using ShelteredAPI.Bunkers;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal interface IShelteredMultiplayerMapRegionSource
    {
        int Width { get; }
        int Height { get; }
        bool HasRegion(int gridX, int gridY);
        bool IsShelterRegion(int gridX, int gridY);
    }

    internal sealed class ShelteredMultiplayerMapAnchorGridResult
    {
        public bool HasMap;
        public bool HasValidRegion;
        public bool RequestedInBounds;
        public bool RequestedRegionValid;
        public bool IsFallback;
        public int RequestedGridX;
        public int RequestedGridY;
        public int ChosenGridX;
        public int ChosenGridY;
        public int MapWidth;
        public int MapHeight;
        public int ValidRegionCount;
        public string Reason = string.Empty;
    }

    internal sealed class ShelteredMultiplayerMapAnchorValidationResult
    {
        public int BunkerOwnerId;
        public Vector2 AssignedWorldPosition;
        public Vector2 ChosenWorldPosition;
        public Vector3 ChosenMapPixels;
        public int RequestedGridX;
        public int RequestedGridY;
        public int ChosenGridX;
        public int ChosenGridY;
        public int MapWidth;
        public int MapHeight;
        public int ValidRegionCount;
        public float WorldMinX;
        public float WorldMaxX;
        public float WorldMinY;
        public float WorldMaxY;
        public bool HasExplorationManager;
        public bool HasExpeditionMap;
        public bool HasMapRegionSource;
        public bool IsFallback;
        public bool IsValid;
        public bool ChosenRegionIsShelter;
        public string Reason = string.Empty;
    }

    internal static class ShelteredMultiplayerMapAnchorFallback
    {
        public static ShelteredMultiplayerMapAnchorGridResult ValidateGrid(
            int requestedGridX,
            int requestedGridY,
            IShelteredMultiplayerMapRegionSource regions)
        {
            ShelteredMultiplayerMapAnchorGridResult result = new ShelteredMultiplayerMapAnchorGridResult();
            result.RequestedGridX = requestedGridX;
            result.RequestedGridY = requestedGridY;
            result.ChosenGridX = requestedGridX;
            result.ChosenGridY = requestedGridY;

            if (regions == null || regions.Width <= 0 || regions.Height <= 0)
            {
                result.Reason = "MapUnavailable";
                return result;
            }

            result.HasMap = true;
            result.MapWidth = regions.Width;
            result.MapHeight = regions.Height;
            result.RequestedInBounds = IsInBounds(requestedGridX, requestedGridY, regions.Width, regions.Height);

            if (result.RequestedInBounds && regions.HasRegion(requestedGridX, requestedGridY))
            {
                result.HasValidRegion = true;
                result.RequestedRegionValid = true;
                result.ValidRegionCount = CountRegions(regions);
                result.Reason = "RequestedGridValid";
                return result;
            }

            bool found = false;
            long bestDistance = long.MaxValue;
            int bestX = requestedGridX;
            int bestY = requestedGridY;
            int validRegionCount = 0;

            for (int y = 0; y < regions.Height; y++)
            {
                for (int x = 0; x < regions.Width; x++)
                {
                    if (!regions.HasRegion(x, y))
                        continue;

                    validRegionCount++;
                    long distance = SquaredDistance(requestedGridX, requestedGridY, x, y);
                    if (!found
                        || distance < bestDistance
                        || distance == bestDistance && CompareGrid(x, y, bestX, bestY) < 0)
                    {
                        found = true;
                        bestDistance = distance;
                        bestX = x;
                        bestY = y;
                    }
                }
            }

            result.ValidRegionCount = validRegionCount;
            if (!found)
            {
                result.Reason = "NoValidMapRegions";
                return result;
            }

            result.HasValidRegion = true;
            result.IsFallback = true;
            result.ChosenGridX = bestX;
            result.ChosenGridY = bestY;
            result.Reason = result.RequestedInBounds
                ? "RequestedRegionMissing;FallbackNearestValidRegion"
                : "RequestedGridOutOfBounds;FallbackNearestValidRegion";
            return result;
        }

        private static int CountRegions(IShelteredMultiplayerMapRegionSource regions)
        {
            int count = 0;
            for (int y = 0; y < regions.Height; y++)
            {
                for (int x = 0; x < regions.Width; x++)
                {
                    if (regions.HasRegion(x, y))
                        count++;
                }
            }

            return count;
        }

        private static bool IsInBounds(int x, int y, int width, int height)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }

        private static long SquaredDistance(int requestedX, int requestedY, int candidateX, int candidateY)
        {
            long dx = (long)candidateX - requestedX;
            long dy = (long)candidateY - requestedY;
            return dx * dx + dy * dy;
        }

        private static int CompareGrid(int leftX, int leftY, int rightX, int rightY)
        {
            int xCompare = leftX.CompareTo(rightX);
            return xCompare != 0 ? xCompare : leftY.CompareTo(rightY);
        }
    }

    internal static class ShelteredMultiplayerMapAnchorValidator
    {
        public static ShelteredMultiplayerMapAnchorValidationResult ValidateActiveBunker(string reason)
        {
            ShelteredMultiplayerMapAnchorValidationResult result =
                CreateBaseResult("No active multiplayer bunker assignment.", reason);

            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive)
            {
                result.Reason = "MultiplayerInactive";
                return result;
            }

            ShelteredMultiplayerBunkerAssignmentRecord assignment = ResolveActiveAssignment(context);
            int bunkerOwnerId = ShelteredMultiplayerBunkerAssignments.ResolveBunkerOwnerId(
                context.BunkerAssignments,
                context.LocalPlayerId);

            Vector2 worldPosition = assignment != null
                ? assignment.Position
                : ShelteredBunkers.GetBunkerWorldPosition(bunkerOwnerId);

            result = ValidateWorldPosition(bunkerOwnerId, worldPosition, reason);
            if (assignment == null && result.Reason.Length == 0)
                result.Reason = "Active assignment missing; validated ShelteredBunkers position.";
            return result;
        }

        public static ShelteredMultiplayerMapAnchorValidationResult ValidateWorldPosition(
            int bunkerOwnerId,
            Vector2 assignedWorldPosition,
            string reason)
        {
            ShelteredMultiplayerMapAnchorValidationResult result =
                CreateBaseResult(string.Empty, reason);
            result.BunkerOwnerId = bunkerOwnerId;
            result.AssignedWorldPosition = assignedWorldPosition;
            result.ChosenWorldPosition = assignedWorldPosition;

            ExpeditionMap map = ExpeditionMap.Instance;
            result.HasExpeditionMap = map != null;
            if (map == null)
            {
                result.Reason = "NoExpeditionMap";
                return result;
            }

            ExplorationManager manager = ExplorationManager.Instance;
            result.HasExplorationManager = manager != null;
            PopulateWorldBounds(result, manager);

            int requestedGridX;
            int requestedGridY;
            if (!TryWorldToGrid(map, manager, assignedWorldPosition, out requestedGridX, out requestedGridY))
            {
                result.MapWidth = map.width;
                result.MapHeight = map.height;
                result.RequestedGridX = 0;
                result.RequestedGridY = 0;
                result.ChosenGridX = 0;
                result.ChosenGridY = 0;
                result.Reason = manager == null ? "NoExplorationManager" : "CouldNotConvertWorldToGrid";
                return result;
            }

            result.RequestedGridX = requestedGridX;
            result.RequestedGridY = requestedGridY;
            result.ChosenGridX = requestedGridX;
            result.ChosenGridY = requestedGridY;

            IShelteredMultiplayerMapRegionSource regions = new ExpeditionMapRegionSource(map);
            result.HasMapRegionSource = true;
            ShelteredMultiplayerMapAnchorGridResult gridResult =
                ShelteredMultiplayerMapAnchorFallback.ValidateGrid(requestedGridX, requestedGridY, regions);

            result.MapWidth = gridResult.MapWidth;
            result.MapHeight = gridResult.MapHeight;
            result.ValidRegionCount = gridResult.ValidRegionCount;
            result.IsFallback = gridResult.IsFallback;
            result.IsValid = gridResult.HasValidRegion;
            result.ChosenGridX = gridResult.ChosenGridX;
            result.ChosenGridY = gridResult.ChosenGridY;
            result.Reason = gridResult.Reason;

            if (!gridResult.HasValidRegion)
                return result;

            if (gridResult.IsFallback)
                result.ChosenWorldPosition = map.GetGridRefCentreWorldPos(
                    new ExpeditionMap.GridRef(gridResult.ChosenGridX, gridResult.ChosenGridY));

            result.ChosenMapPixels = TryWorldToMapPixels(manager, result.ChosenWorldPosition);
            result.ChosenRegionIsShelter = regions.IsShelterRegion(result.ChosenGridX, result.ChosenGridY);
            return result;
        }

        private static ShelteredMultiplayerMapAnchorValidationResult CreateBaseResult(
            string reason,
            string callReason)
        {
            ShelteredMultiplayerMapAnchorValidationResult result =
                new ShelteredMultiplayerMapAnchorValidationResult();
            result.BunkerOwnerId = -1;
            result.Reason = reason ?? string.Empty;
            return result;
        }

        private static ShelteredMultiplayerBunkerAssignmentRecord ResolveActiveAssignment(
            ShelteredMultiplayerSessionContext context)
        {
            if (context == null || context.BunkerAssignments == null)
                return null;

            for (int i = 0; i < context.BunkerAssignments.Length; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord record = context.BunkerAssignments[i];
                if (record != null && record.PlayerId == context.LocalPlayerId)
                    return record;
            }

            return null;
        }

        private static bool TryWorldToGrid(
            ExpeditionMap map,
            ExplorationManager manager,
            Vector2 worldPosition,
            out int gridX,
            out int gridY)
        {
            gridX = 0;
            gridY = 0;

            if (map == null || manager == null)
                return false;

            float worldWidth = (float)manager.worldWidth;
            float worldHeight = (float)manager.worldHeight;
            if (worldWidth <= 0f || worldHeight <= 0f || map.width <= 0 || map.height <= 0)
                return false;

            gridX = (int)((worldPosition.x + worldWidth * 0.5f) * map.width / worldWidth);
            gridY = (int)((worldPosition.y + worldHeight * 0.5f) * map.height / worldHeight);
            gridX = Mathf.Min(gridX, map.width - 1);
            gridY = Mathf.Min(gridY, map.height - 1);
            return true;
        }

        private static Vector3 TryWorldToMapPixels(ExplorationManager manager, Vector2 worldPosition)
        {
            if (manager == null)
                return Vector3.zero;

            try
            {
                return new Vector3(
                    manager.WorldToMapPixelsX(worldPosition.x),
                    manager.WorldToMapPixelsY(worldPosition.y),
                    0f);
            }
            catch
            {
                return Vector3.zero;
            }
        }

        private static void PopulateWorldBounds(
            ShelteredMultiplayerMapAnchorValidationResult result,
            ExplorationManager manager)
        {
            if (result == null || manager == null)
                return;

            result.WorldMinX = (float)manager.worldWidth * -0.5f;
            result.WorldMaxX = (float)manager.worldWidth * 0.5f;
            result.WorldMinY = (float)manager.worldHeight * -0.5f;
            result.WorldMaxY = (float)manager.worldHeight * 0.5f;
        }

        private sealed class ExpeditionMapRegionSource : IShelteredMultiplayerMapRegionSource
        {
            private static readonly FieldInfo MapRegionsField =
                AccessTools.Field(typeof(ExpeditionMap), "m_mapRegions");

            private readonly ExpeditionMap _map;
            private readonly MapRegion[,] _regions;

            public ExpeditionMapRegionSource(ExpeditionMap map)
            {
                _map = map;
                _regions = MapRegionsField != null ? MapRegionsField.GetValue(map) as MapRegion[,] : null;
            }

            public int Width
            {
                get { return _regions != null ? _regions.GetLength(0) : (_map != null ? _map.width : 0); }
            }

            public int Height
            {
                get { return _regions != null ? _regions.GetLength(1) : (_map != null ? _map.height : 0); }
            }

            public bool HasRegion(int gridX, int gridY)
            {
                if (_map == null || gridX < 0 || gridX >= Width || gridY < 0 || gridY >= Height)
                    return false;

                if (_regions != null)
                    return _regions[gridX, gridY] != null;

                return _map.GetRegionOnMap(new ExpeditionMap.GridRef(gridX, gridY)) != null;
            }

            public bool IsShelterRegion(int gridX, int gridY)
            {
                if (_map == null || gridX < 0 || gridX >= Width || gridY < 0 || gridY >= Height)
                    return false;

                MapRegion region = _regions != null
                    ? _regions[gridX, gridY]
                    : _map.GetRegionOnMap(new ExpeditionMap.GridRef(gridX, gridY));
                return region != null && region.topography == MapRegion.Topography.Shelter;
            }
        }
    }
}
