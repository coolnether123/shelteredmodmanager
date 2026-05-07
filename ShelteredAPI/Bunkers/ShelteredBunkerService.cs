using System;
using System.Collections.Generic;
using System.Linq;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Bunkers
{
    internal sealed class ShelteredBunkerService : IShelteredBunkerService
    {
        private const float FallbackWorldWidth = 813f;
        private const float FallbackWorldHeight = 327f;
        private const float PlacementMarginScale = 0.35f;

        private readonly Dictionary<int, BunkerDefinition> _bunkers = new Dictionary<int, BunkerDefinition>();
        private BunkerLocationMode _locationMode = BunkerLocationMode.FullyRandom;
        private int _activePlayerId;

        public event Action<BunkerDefinition> OnBunkerChanged;

        public BunkerLocationMode LocationMode
        {
            get { return _locationMode; }
        }

        public int ActivePlayerId
        {
            get { return _activePlayerId; }
        }

        public void SetLocationMode(BunkerLocationMode mode)
        {
            _locationMode = mode;
        }

        public void SetActivePlayerId(int id)
        {
            if (_activePlayerId == id)
                return;

            _activePlayerId = id;
            NotifyChanged(GetBunker(id) ?? GetPrimaryBunker());
        }

        public BunkerDefinition GetPrimaryBunker()
        {
            return GetBunker(0);
        }

        public BunkerDefinition GetBunker(int id)
        {
            BunkerDefinition bunker;
            _bunkers.TryGetValue(id, out bunker);
            return bunker;
        }

        public IEnumerable<BunkerDefinition> GetAllBunkers()
        {
            return _bunkers.Values;
        }

        public BunkerDefinition RequestNewBunker(int userId, string displayName, bool enableStarterHouses, bool force)
        {
            BunkerDefinition existing;
            if (!force && _bunkers.TryGetValue(userId, out existing))
                return existing;

            Vector2 newPosition = userId == 0
                ? CalculatePrimaryPosition(_locationMode)
                : CalculateSecondaryPosition();

            string resolvedName = ResolveDisplayName(userId, displayName);
            BunkerDefinition bunker = new BunkerDefinition(userId, newPosition, resolvedName, enableStarterHouses);
            _bunkers[userId] = bunker;

            LogPosition(userId, newPosition);
            NotifyChanged(bunker);
            return bunker;
        }

        public void SetBunkerPosition(int id, Vector2 position)
        {
            BunkerDefinition bunker;
            if (_bunkers.TryGetValue(id, out bunker))
            {
                bunker.Position = position;
            }
            else
            {
                bunker = new BunkerDefinition(id, position, ResolveDisplayName(id, string.Empty));
                _bunkers[id] = bunker;
            }

            LogPosition(id, position);
            NotifyChanged(bunker);
        }

        public void SetBunkerOnline(int id, bool online)
        {
            BunkerDefinition bunker;
            if (!_bunkers.TryGetValue(id, out bunker))
                return;

            bunker.IsOnline = online;
            NotifyChanged(bunker);
        }

        public bool IsAnyHome(Vector2 worldPos, float tolerance)
        {
            return _bunkers.Values.Any(b => b != null && Vector2.Distance(b.Position, worldPos) < tolerance);
        }

        public Vector2 CalculatePrimaryPosition()
        {
            return CalculatePrimaryPosition(_locationMode);
        }

        public Vector2 CalculatePrimaryPosition(BunkerLocationMode mode)
        {
            float width;
            float height;
            GetWorldSize(out width, out height);

            float marginX = width * PlacementMarginScale;
            float marginY = height * PlacementMarginScale;

            switch (mode)
            {
                case BunkerLocationMode.TopLeft:
                    return new Vector2(-marginX, marginY);
                case BunkerLocationMode.TopRight:
                    return new Vector2(marginX, marginY);
                case BunkerLocationMode.BottomLeft:
                    return new Vector2(-marginX, -marginY);
                case BunkerLocationMode.BottomRight:
                    return new Vector2(marginX, -marginY);
                case BunkerLocationMode.FullyRandom:
                    return new Vector2(ModRandom.Range(-marginX, marginX), ModRandom.Range(-marginY, marginY));
                case BunkerLocationMode.RandomQuadrant:
                    int quadrant = ModRandom.Range(0, 4);
                    float x = (quadrant == 0 || quadrant == 2) ? -marginX : marginX;
                    float y = (quadrant == 0 || quadrant == 1) ? marginY : -marginY;
                    return new Vector2(x, y);
                default:
                    return Vector2.zero;
            }
        }

        public Vector2 CalculateSecondaryPosition()
        {
            float width;
            float height;
            GetWorldSize(out width, out height);

            float marginX = width * PlacementMarginScale;
            float marginY = height * PlacementMarginScale;

            List<Vector2> corners = new List<Vector2>
            {
                new Vector2(-marginX, marginY),
                new Vector2(marginX, marginY),
                new Vector2(-marginX, -marginY),
                new Vector2(marginX, -marginY)
            };

            ModRandom.Shuffle(corners);

            for (int i = 0; i < corners.Count; i++)
            {
                Vector2 candidate = corners[i];
                if (!IsAnyHome(candidate, width * 0.25f))
                    return candidate;
            }

            float minDistance = width * 0.30f;
            for (int radiusStep = 0; radiusStep < 5; radiusStep++)
            {
                for (int attempt = 0; attempt < 15; attempt++)
                {
                    Vector2 candidate = new Vector2(ModRandom.Range(-marginX, marginX), ModRandom.Range(-marginY, marginY));
                    if (!IsAnyHome(candidate, minDistance))
                        return candidate;
                }

                minDistance *= 0.65f;
            }

            for (int i = 0; i < 10; i++)
            {
                Vector2 candidate = new Vector2(ModRandom.Range(-marginX, marginX), ModRandom.Range(-marginY, marginY));
                if (!IsAnyHome(candidate, 10f))
                    return candidate;
            }

            return new Vector2(ModRandom.Range(-marginX, marginX), ModRandom.Range(-marginY, marginY));
        }

        public Vector2 GetActiveBunkerWorldPosition()
        {
            BunkerDefinition bunker = GetBunker(_activePlayerId) ?? GetPrimaryBunker() ?? GetFirstBunker();
            return bunker != null ? bunker.Position : Vector2.zero;
        }

        public Vector2 GetBunkerWorldPosition(int id)
        {
            BunkerDefinition bunker = GetBunker(id);
            return bunker != null ? bunker.Position : Vector2.zero;
        }

        public Vector3 GetActiveBunkerMapPixels()
        {
            BunkerDefinition bunker = GetBunker(_activePlayerId) ?? GetPrimaryBunker() ?? GetFirstBunker();
            return bunker != null ? WorldToMapPixels(bunker.Position) : Vector3.zero;
        }

        public Vector3 GetBunkerMapPixels(int id)
        {
            BunkerDefinition bunker = GetBunker(id);
            return bunker != null ? WorldToMapPixels(bunker.Position) : Vector3.zero;
        }

        public ExpeditionMap.GridRef GetActiveBunkerGridRef()
        {
            BunkerDefinition bunker = GetBunker(_activePlayerId) ?? GetPrimaryBunker() ?? GetFirstBunker();
            return bunker != null ? WorldToGridRef(bunker.Position) : new ExpeditionMap.GridRef(0, 0);
        }

        public ExpeditionMap.GridRef GetBunkerGridRef(int id)
        {
            BunkerDefinition bunker = GetBunker(id);
            return bunker != null ? WorldToGridRef(bunker.Position) : new ExpeditionMap.GridRef(0, 0);
        }

        public List<BunkerDefinition> GetDefinitions()
        {
            return _bunkers.Values
                .Where(d => d != null)
                .Select(d => d.Clone())
                .ToList();
        }

        public void LoadDefinitions(List<BunkerDefinition> bunkers)
        {
            _bunkers.Clear();
            if (bunkers == null)
                return;

            for (int i = 0; i < bunkers.Count; i++)
            {
                BunkerDefinition bunker = bunkers[i];
                if (bunker == null)
                    continue;

                BunkerDefinition clone = bunker.Clone();
                _bunkers[clone.Id] = clone;
                NotifyChanged(clone);
            }
        }

        public void Clear()
        {
            _bunkers.Clear();
        }

        private static void GetWorldSize(out float width, out float height)
        {
            width = FallbackWorldWidth;
            height = FallbackWorldHeight;

            ExplorationManager manager = ExplorationManager.Instance;
            if (manager == null)
                return;

            if (manager.worldWidth > 0f)
                width = manager.worldWidth;
            if (manager.worldHeight > 0f)
                height = manager.worldHeight;
        }

        private static string ResolveDisplayName(int id, string displayName)
        {
            if (!string.IsNullOrEmpty(displayName))
                return displayName;

            return id == 0 ? "Local Shelter" : "Bunker " + id;
        }

        private BunkerDefinition GetFirstBunker()
        {
            foreach (BunkerDefinition bunker in _bunkers.Values)
                return bunker;

            return null;
        }

        private static Vector3 WorldToMapPixels(Vector2 worldPosition)
        {
            ExplorationManager manager = ExplorationManager.Instance;
            if (manager == null || manager.mapSourceSprite == null)
                return Vector3.zero;

            try
            {
                return new Vector3(
                    manager.WorldToMapPixelsX(worldPosition.x),
                    manager.WorldToMapPixelsY(worldPosition.y),
                    0f);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredBunkers.WorldToMapPixels", "Failed to convert bunker world position: " + ex.Message);
                return Vector3.zero;
            }
        }

        private static ExpeditionMap.GridRef WorldToGridRef(Vector2 worldPosition)
        {
            ExpeditionMap map = ExpeditionMap.Instance;
            if (map == null || ExplorationManager.Instance == null)
                return new ExpeditionMap.GridRef(0, 0);

            try
            {
                return map.WorldPosToGridRef(worldPosition);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredBunkers.WorldToGridRef", "Failed to convert bunker world position: " + ex.Message);
                return new ExpeditionMap.GridRef(0, 0);
            }
        }

        private static void LogPosition(int id, Vector2 position)
        {
            MMLog.WriteInfo(string.Format(
                "[ShelteredBunkers] Bunker {0} position set to ({1:F1}, {2:F1}).",
                id,
                position.x,
                position.y));
        }

        private void NotifyChanged(BunkerDefinition bunker)
        {
            Action<BunkerDefinition> handler = OnBunkerChanged;
            if (handler != null && bunker != null)
                handler(bunker);
        }
    }
}
