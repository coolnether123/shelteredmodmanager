using System;
using System.Collections.Generic;

namespace ShelteredAPI.Networking.Locations
{
    internal sealed class ShelteredLocationStateRegistry
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, LocationState> _locations =
            new Dictionary<string, LocationState>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<LootItemRecord>> _loot =
            new Dictionary<string, List<LootItemRecord>>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _takenEventIds =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Func<long> _worldTickSource;

        public ShelteredLocationStateRegistry()
            : this(ResolveCoordinatorWorldTick)
        {
        }

        internal ShelteredLocationStateRegistry(Func<long> worldTickSource)
        {
            _worldTickSource = worldTickSource ?? ResolveCoordinatorWorldTick;
        }

        public LocationState Upsert(LocationState state)
        {
            if (state == null)
                throw new ArgumentNullException("state");

            LocationState copy = state.Copy();
            copy.LocationId = ResolveLocationId(copy);
            if (copy.LastUpdatedTick <= 0)
                copy.LastUpdatedTick = ResolveWorldTick();

            lock (_sync)
            {
                _locations[copy.LocationId] = copy;
            }

            return copy.Copy();
        }

        public bool TryGet(string locationId, out LocationState state)
        {
            state = null;
            string key = NormalizeId(locationId);
            if (key.Length == 0)
                return false;

            lock (_sync)
            {
                LocationState existing;
                if (!_locations.TryGetValue(key, out existing))
                    return false;

                state = existing.Copy();
                return true;
            }
        }

        public IList<LocationState> GetAll()
        {
            lock (_sync)
            {
                List<LocationState> result = new List<LocationState>();
                foreach (LocationState state in _locations.Values)
                    result.Add(state.Copy());
                result.Sort(CompareLocations);
                return result;
            }
        }

        public void SetLoot(string locationId, IList<LootItemRecord> loot)
        {
            string key = NormalizeId(locationId);
            if (key.Length == 0)
                return;

            lock (_sync)
            {
                _loot[key] = CloneLoot(loot);
            }
        }

        public IList<LootItemRecord> GetLoot(string locationId)
        {
            string key = NormalizeId(locationId);
            lock (_sync)
            {
                List<LootItemRecord> existing;
                return key.Length > 0 && _loot.TryGetValue(key, out existing)
                    ? CloneLoot(existing)
                    : new List<LootItemRecord>();
            }
        }

        public bool ApplyLootTaken(string locationId, string eventCorrelationId, IList<LootItemRecord> taken, int playerId, long takenTick)
        {
            string key = NormalizeId(locationId);
            if (key.Length == 0)
                return false;

            string eventKey = NormalizeId(eventCorrelationId);
            if (eventKey.Length == 0)
                eventKey = BuildTakenEventKey(key, taken, playerId, takenTick);

            lock (_sync)
            {
                if (_takenEventIds.ContainsKey(eventKey))
                    return false;

                _takenEventIds[eventKey] = true;
                List<LootItemRecord> existing;
                if (!_loot.TryGetValue(key, out existing))
                {
                    existing = new List<LootItemRecord>();
                    _loot[key] = existing;
                }

                for (int i = 0; taken != null && i < taken.Count; i++)
                {
                    LootItemRecord request = taken[i];
                    if (request == null || request.Count <= 0)
                        continue;

                    int remaining = request.Count;
                    for (int j = 0; j < existing.Count && remaining > 0; j++)
                    {
                        LootItemRecord record = existing[j];
                        if (record == null || record.Count <= 0 || !SameItem(record, request))
                            continue;

                        int moved = Math.Min(record.Count, remaining);
                        record.Count -= moved;
                        remaining -= moved;
                    }
                }

                return true;
            }
        }

        public bool IsDepleted(string locationId)
        {
            IList<LootItemRecord> loot = GetLoot(locationId);
            for (int i = 0; i < loot.Count; i++)
            {
                if (loot[i] != null && loot[i].Count > 0)
                    return false;
            }

            return true;
        }

        public void Clear(string reason)
        {
            lock (_sync)
            {
                _locations.Clear();
                _loot.Clear();
                _takenEventIds.Clear();
            }
        }

        internal static string ResolveLocationId(LocationState state)
        {
            string explicitId = NormalizeId(state != null ? state.LocationId : string.Empty);
            if (explicitId.Length > 0)
                return explicitId;

            if (state == null)
                throw new ArgumentNullException("state");

            return BuildLocationId(state.GridX, state.GridY, state.LocationKind);
        }

        internal static string BuildLocationId(int gridX, int gridY, string locationKind)
        {
            return "location:" + gridX.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + gridY.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + NormalizeId(locationKind);
        }

        internal static bool SameItem(LootItemRecord left, LootItemRecord right)
        {
            if (left == null || right == null)
                return false;

            if (left.VanillaItemTypeInt.HasValue || right.VanillaItemTypeInt.HasValue)
                return left.VanillaItemTypeInt.HasValue
                    && right.VanillaItemTypeInt.HasValue
                    && left.VanillaItemTypeInt.Value == right.VanillaItemTypeInt.Value;

            return string.Equals(left.CustomItemId ?? string.Empty, right.CustomItemId ?? string.Empty, StringComparison.Ordinal);
        }

        private long ResolveWorldTick()
        {
            long tick = _worldTickSource();
            return tick > 0 ? tick : 0;
        }

        private static long ResolveCoordinatorWorldTick()
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            return context != null ? context.WorldTick : 0;
        }

        private static List<LootItemRecord> CloneLoot(IList<LootItemRecord> source)
        {
            List<LootItemRecord> result = new List<LootItemRecord>();
            for (int i = 0; source != null && i < source.Count; i++)
            {
                if (source[i] != null)
                    result.Add(source[i].Copy());
            }

            return result;
        }

        private static string BuildTakenEventKey(string locationId, IList<LootItemRecord> taken, int playerId, long tick)
        {
            return "loottaken:" + locationId + ":" + playerId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + tick.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + (taken != null ? taken.Count : 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int CompareLocations(LocationState left, LocationState right)
        {
            return string.Compare(left.LocationId, right.LocationId, StringComparison.Ordinal);
        }

        private static string NormalizeId(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
