using System;
using System.Collections.Generic;

namespace ShelteredAPI.Networking.Locations
{
    [Serializable]
    internal sealed class LocationState
    {
        public LocationState()
        {
            LocationId = string.Empty;
            LocationKind = string.Empty;
            GeneratedSeedStream = string.Empty;
            RemainingLootSummaryJson = string.Empty;
        }

        public string LocationId { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public string LocationKind { get; set; }
        public string GeneratedSeedStream { get; set; }
        public long GeneratedWorldTick { get; set; }
        public int DiscoveredByPlayerId { get; set; }
        public bool IsGenerated { get; set; }
        public bool IsSearched { get; set; }
        public bool IsDepleted { get; set; }
        public string RemainingLootSummaryJson { get; set; }
        public long LastUpdatedTick { get; set; }

        public LocationState Copy()
        {
            return new LocationState
            {
                LocationId = LocationId ?? string.Empty,
                GridX = GridX,
                GridY = GridY,
                LocationKind = LocationKind ?? string.Empty,
                GeneratedSeedStream = GeneratedSeedStream ?? string.Empty,
                GeneratedWorldTick = GeneratedWorldTick,
                DiscoveredByPlayerId = DiscoveredByPlayerId,
                IsGenerated = IsGenerated,
                IsSearched = IsSearched,
                IsDepleted = IsDepleted,
                RemainingLootSummaryJson = RemainingLootSummaryJson ?? string.Empty,
                LastUpdatedTick = LastUpdatedTick
            };
        }
    }

    [Serializable]
    internal sealed class LootItemRecord
    {
        public LootItemRecord()
        {
            CustomItemId = string.Empty;
            Source = string.Empty;
        }

        public int? VanillaItemTypeInt { get; set; }
        public string CustomItemId { get; set; }
        public int Count { get; set; }
        public string Source { get; set; }
        public int TakenByPlayerId { get; set; }
        public long TakenTick { get; set; }

        public LootItemRecord Copy()
        {
            return new LootItemRecord
            {
                VanillaItemTypeInt = VanillaItemTypeInt,
                CustomItemId = CustomItemId ?? string.Empty,
                Count = Count,
                Source = Source ?? string.Empty,
                TakenByPlayerId = TakenByPlayerId,
                TakenTick = TakenTick
            };
        }
    }

    [Serializable]
    internal sealed class ShelteredLocationEvent
    {
        public ShelteredLocationEvent()
        {
            LocationId = string.Empty;
            LocationKind = string.Empty;
            SeedStreamName = string.Empty;
            RemainingLootSummaryJson = string.Empty;
            Loot = new List<LootItemRecord>();
            Reason = string.Empty;
            EventCorrelationId = string.Empty;
        }

        public string LocationId { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public string LocationKind { get; set; }
        public string SeedStreamName { get; set; }
        public long WorldTick { get; set; }
        public int PlayerId { get; set; }
        public bool IsGenerated { get; set; }
        public bool IsSearched { get; set; }
        public bool IsDepleted { get; set; }
        public string RemainingLootSummaryJson { get; set; }
        public IList<LootItemRecord> Loot { get; set; }
        public string Reason { get; set; }
        public string EventCorrelationId { get; set; }

        public ShelteredLocationEvent Copy()
        {
            ShelteredLocationEvent copy = new ShelteredLocationEvent();
            copy.LocationId = LocationId ?? string.Empty;
            copy.GridX = GridX;
            copy.GridY = GridY;
            copy.LocationKind = LocationKind ?? string.Empty;
            copy.SeedStreamName = SeedStreamName ?? string.Empty;
            copy.WorldTick = WorldTick;
            copy.PlayerId = PlayerId;
            copy.IsGenerated = IsGenerated;
            copy.IsSearched = IsSearched;
            copy.IsDepleted = IsDepleted;
            copy.RemainingLootSummaryJson = RemainingLootSummaryJson ?? string.Empty;
            copy.Reason = Reason ?? string.Empty;
            copy.EventCorrelationId = EventCorrelationId ?? string.Empty;
            copy.Loot = CloneLoot(Loot);
            return copy;
        }

        internal static IList<LootItemRecord> CloneLoot(IList<LootItemRecord> source)
        {
            List<LootItemRecord> copy = new List<LootItemRecord>();
            for (int i = 0; source != null && i < source.Count; i++)
            {
                if (source[i] != null)
                    copy.Add(source[i].Copy());
            }

            return copy;
        }
    }
}
