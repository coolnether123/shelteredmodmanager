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
            TrySetLoot(locationId, loot);
        }

        public bool TrySetLoot(string locationId, IList<LootItemRecord> loot)
        {
            string key = NormalizeId(locationId);
            if (key.Length == 0)
                return false;

            lock (_sync)
            {
                if (!_locations.ContainsKey(key))
                    return false;

                _loot[key] = CloneLoot(loot);
                MarkLocationLootMutationLocked(key, ResolveWorldTick());
                return true;
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
            string errorMessage;
            return TryApplyLootTaken(locationId, eventCorrelationId, taken, playerId, takenTick, out errorMessage);
        }

        public bool TryApplyLootTaken(
            string locationId,
            string eventCorrelationId,
            IList<LootItemRecord> taken,
            int playerId,
            long takenTick,
            out string errorMessage)
        {
            string key = NormalizeId(locationId);
            if (key.Length == 0)
            {
                errorMessage = "Location id is required.";
                return false;
            }

            string eventKey = NormalizeId(eventCorrelationId);
            if (eventKey.Length == 0)
                eventKey = BuildTakenEventKey(key, taken, playerId, takenTick);

            lock (_sync)
            {
                if (_takenEventIds.ContainsKey(eventKey))
                {
                    errorMessage = "Loot-taken event already applied.";
                    return false;
                }

                List<LootItemRecord> existing;
                if (!CanApplyLootTakenLocked(key, taken, out existing, out errorMessage))
                    return false;

                for (int i = 0; taken != null && i < taken.Count; i++)
                {
                    LootItemRecord request = taken[i];
                    if (!IsValidLootRequest(request))
                        continue;

                    ConsumeFromLoot(existing, request);
                }

                _takenEventIds[eventKey] = true;
                MarkLocationLootMutationLocked(key, takenTick);
                errorMessage = string.Empty;
                return true;
            }
        }

        public bool CanApplyLootTaken(string locationId, IList<LootItemRecord> taken, out string errorMessage)
        {
            string key = NormalizeId(locationId);
            if (key.Length == 0)
            {
                errorMessage = "Location id is required.";
                return false;
            }

            lock (_sync)
            {
                List<LootItemRecord> existing;
                return CanApplyLootTakenLocked(key, taken, out existing, out errorMessage);
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

            return BuildLocationId(state.MapIdentity, state.GridX, state.GridY, state.LocationKind);
        }

        internal static string BuildLocationId(int gridX, int gridY, string locationKind)
        {
            return BuildLocationId(string.Empty, gridX, gridY, locationKind);
        }

        internal static string BuildLocationId(string mapIdentity, int gridX, int gridY, string locationKind)
        {
            string normalizedMap = NormalizeId(mapIdentity);
            if (normalizedMap.Length == 0)
            {
                return "location:" + gridX.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ":" + gridY.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ":" + NormalizeId(locationKind);
            }

            return "location:" + NormalizeIdPart(normalizedMap)
                + ":" + gridX.ToString(System.Globalization.CultureInfo.InvariantCulture)
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

        internal static string BuildTakenEventKey(string locationId, IList<LootItemRecord> taken, int playerId, long tick)
        {
            return "loottaken:" + locationId + ":" + playerId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + tick.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + BuildLootSignature(taken);
        }

        internal static string BuildLootSetEventKey(string locationId, IList<LootItemRecord> loot, long tick)
        {
            return "lootset:" + locationId + ":"
                + tick.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + BuildLootSignature(loot);
        }

        internal static string BuildLootSignature(IList<LootItemRecord> loot)
        {
            List<string> parts = new List<string>();
            for (int i = 0; loot != null && i < loot.Count; i++)
            {
                LootItemRecord record = loot[i];
                if (!IsValidLootRequest(record))
                    continue;

                string itemId = record.VanillaItemTypeInt.HasValue
                    ? "v:" + record.VanillaItemTypeInt.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : "c:" + NormalizeId(record.CustomItemId);
                parts.Add(itemId + "x" + record.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            parts.Sort(StringComparer.Ordinal);
            return string.Join("|", parts.ToArray());
        }

        private static int CompareLocations(LocationState left, LocationState right)
        {
            return string.Compare(left.LocationId, right.LocationId, StringComparison.Ordinal);
        }

        private static string NormalizeId(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private bool CanApplyLootTakenLocked(
            string locationId,
            IList<LootItemRecord> taken,
            out List<LootItemRecord> existing,
            out string errorMessage)
        {
            existing = null;
            if (!_locations.ContainsKey(locationId))
            {
                errorMessage = "Location is not registered.";
                return false;
            }

            if (!_loot.TryGetValue(locationId, out existing))
            {
                errorMessage = "Location loot is not registered.";
                return false;
            }

            if (!HasAnyValidLootRequest(taken))
            {
                errorMessage = "Loot-taken request must include at least one positive item count.";
                return false;
            }

            List<LootItemRecord> remaining = CloneLoot(existing);
            for (int i = 0; taken != null && i < taken.Count; i++)
            {
                LootItemRecord request = taken[i];
                if (!IsValidLootRequest(request))
                    continue;

                if (!ConsumeFromLoot(remaining, request))
                {
                    errorMessage = "Requested loot is no longer available.";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool ConsumeFromLoot(IList<LootItemRecord> loot, LootItemRecord request)
        {
            int remaining = request != null ? request.Count : 0;
            if (remaining <= 0)
                return true;

            for (int i = 0; loot != null && i < loot.Count && remaining > 0; i++)
            {
                LootItemRecord record = loot[i];
                if (record == null || record.Count <= 0 || !SameItem(record, request))
                    continue;

                int moved = Math.Min(record.Count, remaining);
                record.Count -= moved;
                remaining -= moved;
            }

            return remaining <= 0;
        }

        private static bool HasAnyValidLootRequest(IList<LootItemRecord> taken)
        {
            for (int i = 0; taken != null && i < taken.Count; i++)
            {
                if (IsValidLootRequest(taken[i]))
                    return true;
            }

            return false;
        }

        private static bool IsValidLootRequest(LootItemRecord record)
        {
            if (record == null || record.Count <= 0)
                return false;

            if (record.VanillaItemTypeInt.HasValue)
                return record.VanillaItemTypeInt.Value >= 0;

            return NormalizeId(record.CustomItemId).Length > 0;
        }

        private void MarkLocationLootMutationLocked(string locationId, long tick)
        {
            LocationState state;
            if (!_locations.TryGetValue(locationId, out state) || state == null)
                return;

            state.LastUpdatedTick = tick > 0 ? tick : ResolveWorldTick();
            state.IsDepleted = IsDepletedLocked(locationId);
        }

        private bool IsDepletedLocked(string locationId)
        {
            List<LootItemRecord> loot;
            if (!_loot.TryGetValue(locationId, out loot))
                return false;

            for (int i = 0; i < loot.Count; i++)
            {
                if (loot[i] != null && loot[i].Count > 0)
                    return false;
            }

            return true;
        }

        private static string NormalizeIdPart(string value)
        {
            string normalized = NormalizeId(value);
            if (normalized.Length == 0)
                return "none";

            char[] chars = normalized.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')
                    continue;
                chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
