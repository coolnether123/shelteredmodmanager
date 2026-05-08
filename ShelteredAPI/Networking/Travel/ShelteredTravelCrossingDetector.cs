using System;
using System.Collections.Generic;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Travel
{
    internal delegate bool ShelteredTravelCrossingVisibilityPredicate(
        ShelteredTravelState left,
        ShelteredMapEntity leftEntity,
        ShelteredTravelState right,
        ShelteredMapEntity rightEntity);

    internal sealed class ShelteredTravelCrossingDetectionOptions
    {
        public ShelteredTravelCrossingDetectionOptions()
        {
            CellThreshold = 1;
            VisibilityPredicate = DefaultVisibilityPredicate;
        }

        public int CellThreshold { get; set; }
        public ShelteredTravelCrossingVisibilityPredicate VisibilityPredicate { get; set; }

        internal int NormalizedCellThreshold
        {
            get { return CellThreshold < 0 ? 0 : CellThreshold; }
        }

        private static bool DefaultVisibilityPredicate(
            ShelteredTravelState left,
            ShelteredMapEntity leftEntity,
            ShelteredTravelState right,
            ShelteredMapEntity rightEntity)
        {
            return IsVisible(leftEntity) && IsVisible(rightEntity);
        }

        private static bool IsVisible(ShelteredMapEntity entity)
        {
            return entity == null || entity.IsVisible;
        }
    }

    internal sealed class ShelteredTravelCrossingCandidate
    {
        public ShelteredTravelCrossingCandidate()
        {
            EncounterId = string.Empty;
            FirstTravelId = string.Empty;
            SecondTravelId = string.Empty;
        }

        public string EncounterId { get; set; }
        public string FirstTravelId { get; set; }
        public string SecondTravelId { get; set; }
        public int FirstOwnerPlayerId { get; set; }
        public byte FirstOwnerPeerId { get; set; }
        public int SecondOwnerPlayerId { get; set; }
        public byte SecondOwnerPeerId { get; set; }
        public int FirstGridX { get; set; }
        public int FirstGridY { get; set; }
        public int SecondGridX { get; set; }
        public int SecondGridY { get; set; }
        public int CellDistance { get; set; }
        public int DistanceSquared { get; set; }
        public long WorldTick { get; set; }
    }

    internal sealed class ShelteredTravelCrossingDetector
    {
        private readonly IShelteredTravelStateRegistry _travelStates;
        private readonly IShelteredMapEntityRegistry _mapEntities;
        private readonly ShelteredTravelCrossingDetectionOptions _options;

        public ShelteredTravelCrossingDetector(
            IShelteredTravelStateRegistry travelStates,
            IShelteredMapEntityRegistry mapEntities)
            : this(travelStates, mapEntities, null)
        {
        }

        public ShelteredTravelCrossingDetector(
            IShelteredTravelStateRegistry travelStates,
            IShelteredMapEntityRegistry mapEntities,
            ShelteredTravelCrossingDetectionOptions options)
        {
            if (travelStates == null)
                throw new ArgumentNullException("travelStates");

            _travelStates = travelStates;
            _mapEntities = mapEntities;
            _options = options ?? new ShelteredTravelCrossingDetectionOptions();
        }

        public IList<ShelteredTravelCrossingCandidate> Detect(long worldTick)
        {
            IList<ShelteredTravelState> active = _travelStates.GetActive();
            List<ShelteredTravelCrossingCandidate> candidates = new List<ShelteredTravelCrossingCandidate>();

            for (int i = 0; i < active.Count; i++)
            {
                for (int j = i + 1; j < active.Count; j++)
                {
                    ShelteredTravelCrossingCandidate candidate;
                    if (TryCreateCandidate(active[i], active[j], worldTick, out candidate))
                        candidates.Add(candidate);
                }
            }

            candidates.Sort(CompareCandidates);
            return candidates;
        }

        public bool TryCreateCandidate(
            ShelteredTravelState left,
            ShelteredTravelState right,
            long worldTick,
            out ShelteredTravelCrossingCandidate candidate)
        {
            candidate = null;
            if (left == null || right == null)
                return false;
            if (string.IsNullOrEmpty(left.TravelId) || string.IsNullOrEmpty(right.TravelId))
                return false;
            if (string.Equals(left.TravelId, right.TravelId, StringComparison.Ordinal))
                return false;
            if (IsSameOwner(left, right))
                return false;

            ShelteredMapEntity leftEntity = GetMapEntity(left.TravelId);
            ShelteredMapEntity rightEntity = GetMapEntity(right.TravelId);
            ShelteredTravelCrossingVisibilityPredicate visibility = _options.VisibilityPredicate;
            if (visibility != null && !visibility(left, leftEntity, right, rightEntity))
                return false;

            GridPoint leftPoint = ResolvePoint(left, leftEntity, worldTick);
            GridPoint rightPoint = ResolvePoint(right, rightEntity, worldTick);
            int deltaX = leftPoint.X - rightPoint.X;
            int deltaY = leftPoint.Y - rightPoint.Y;
            int cellDistance = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
            if (cellDistance > _options.NormalizedCellThreshold)
                return false;

            candidate = new ShelteredTravelCrossingCandidate();
            candidate.EncounterId = CreateEncounterId(left.TravelId, right.TravelId);
            candidate.FirstTravelId = left.TravelId;
            candidate.SecondTravelId = right.TravelId;
            candidate.FirstOwnerPlayerId = left.OwnerPlayerId;
            candidate.FirstOwnerPeerId = left.OwnerPeerId;
            candidate.SecondOwnerPlayerId = right.OwnerPlayerId;
            candidate.SecondOwnerPeerId = right.OwnerPeerId;
            candidate.FirstGridX = leftPoint.X;
            candidate.FirstGridY = leftPoint.Y;
            candidate.SecondGridX = rightPoint.X;
            candidate.SecondGridY = rightPoint.Y;
            candidate.CellDistance = cellDistance;
            candidate.DistanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            candidate.WorldTick = worldTick;
            return true;
        }

        public static string CreateEncounterId(string firstTravelId, string secondTravelId)
        {
            string first = firstTravelId ?? string.Empty;
            string second = secondTravelId ?? string.Empty;
            if (string.Compare(first, second, StringComparison.Ordinal) > 0)
            {
                string temp = first;
                first = second;
                second = temp;
            }

            return "encounter:crossing:" + EscapeId(first) + ":" + EscapeId(second);
        }

        private ShelteredMapEntity GetMapEntity(string travelId)
        {
            return _mapEntities != null
                ? _mapEntities.Get(ShelteredTravelStateRegistry.CreateMapEntityId(travelId))
                : null;
        }

        private GridPoint ResolvePoint(ShelteredTravelState state, ShelteredMapEntity entity, long worldTick)
        {
            ShelteredTravelPredictionResult prediction = _travelStates.Predict(state.TravelId, worldTick);
            if (prediction != null)
                return new GridPoint(prediction.GridX, prediction.GridY);

            if (entity != null)
                return new GridPoint(entity.GridX, entity.GridY);

            return new GridPoint(state.LastPredictedGridX, state.LastPredictedGridY);
        }

        private static bool IsSameOwner(ShelteredTravelState left, ShelteredTravelState right)
        {
            if (left.OwnerPlayerId > 0 && left.OwnerPlayerId == right.OwnerPlayerId)
                return true;

            return left.OwnerPlayerId <= 0
                && right.OwnerPlayerId <= 0
                && left.OwnerPeerId == right.OwnerPeerId;
        }

        private static int CompareCandidates(
            ShelteredTravelCrossingCandidate left,
            ShelteredTravelCrossingCandidate right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.Compare(left.EncounterId, right.EncounterId, StringComparison.Ordinal);
        }

        private static string EscapeId(string value)
        {
            return (value ?? string.Empty).Replace("%", "%25").Replace(":", "%3A");
        }

        private struct GridPoint
        {
            public GridPoint(int x, int y)
            {
                X = x;
                Y = y;
            }

            public readonly int X;
            public readonly int Y;
        }
    }
}
