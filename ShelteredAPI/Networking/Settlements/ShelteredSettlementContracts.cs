using System;
using System.Collections.Generic;

namespace ShelteredAPI.Networking.Settlements
{
    public sealed class ShelteredSettlementState
    {
        public ShelteredSettlementState()
        {
            SettlementId = string.Empty;
            OwnerFactionId = string.Empty;
            ProductionTags = new List<string>();
            StorageStoreId = string.Empty;
            LastEventId = string.Empty;
            State = "active";
        }

        public string SettlementId;
        public int OwnerPlayerId;
        public string OwnerFactionId;
        public int GridX;
        public int GridY;
        public int Population;
        public int Defense;
        public IList<string> ProductionTags;
        public string StorageStoreId;
        public long LastProductionTick;
        public string State;
        public string LastEventId;

        public ShelteredSettlementState Copy()
        {
            ShelteredSettlementState copy = new ShelteredSettlementState();
            copy.SettlementId = SettlementId ?? string.Empty;
            copy.OwnerPlayerId = OwnerPlayerId;
            copy.OwnerFactionId = OwnerFactionId ?? string.Empty;
            copy.GridX = GridX;
            copy.GridY = GridY;
            copy.Population = Population;
            copy.Defense = Defense;
            copy.StorageStoreId = StorageStoreId ?? string.Empty;
            copy.LastProductionTick = LastProductionTick;
            copy.State = State ?? string.Empty;
            copy.LastEventId = LastEventId ?? string.Empty;
            for (int i = 0; ProductionTags != null && i < ProductionTags.Count; i++)
                copy.ProductionTags.Add(ProductionTags[i] ?? string.Empty);
            return copy;
        }
    }

    [Serializable]
    internal sealed class ShelteredSettlementEvent
    {
        public ShelteredSettlementEvent()
        {
            SettlementId = string.Empty;
            EventKind = string.Empty;
            OwnerFactionId = string.Empty;
            ProductionTags = new List<string>();
            StorageStoreId = string.Empty;
            PayloadJson = string.Empty;
        }

        public string SettlementId;
        public string EventKind;
        public int OwnerPlayerId;
        public string OwnerFactionId;
        public int GridX;
        public int GridY;
        public int Population;
        public int Defense;
        public IList<string> ProductionTags;
        public string StorageStoreId;
        public long LastProductionTick;
        public string PayloadJson;

        public ShelteredSettlementEvent Copy()
        {
            ShelteredSettlementEvent copy = new ShelteredSettlementEvent();
            copy.SettlementId = SettlementId ?? string.Empty;
            copy.EventKind = EventKind ?? string.Empty;
            copy.OwnerPlayerId = OwnerPlayerId;
            copy.OwnerFactionId = OwnerFactionId ?? string.Empty;
            copy.GridX = GridX;
            copy.GridY = GridY;
            copy.Population = Population;
            copy.Defense = Defense;
            copy.StorageStoreId = StorageStoreId ?? string.Empty;
            copy.LastProductionTick = LastProductionTick;
            copy.PayloadJson = PayloadJson ?? string.Empty;
            for (int i = 0; ProductionTags != null && i < ProductionTags.Count; i++)
                copy.ProductionTags.Add(ProductionTags[i] ?? string.Empty);
            return copy;
        }
    }

    internal sealed class ShelteredSettlementProductionResult
    {
        public ShelteredSettlementProductionResult()
        {
            SettlementId = string.Empty;
            ProducedTags = new List<string>();
        }

        public string SettlementId;
        public long ProductionTick;
        public int ProductionScore;
        public IList<string> ProducedTags;
    }

    internal sealed class ShelteredSettlementApplyResult
    {
        public static readonly ShelteredSettlementApplyResult Applied = new ShelteredSettlementApplyResult(true, string.Empty);
        public static readonly ShelteredSettlementApplyResult IgnoredDuplicate = new ShelteredSettlementApplyResult(false, "duplicate-event-id");

        public static ShelteredSettlementApplyResult Ignored(string reason)
        {
            return new ShelteredSettlementApplyResult(false, reason);
        }

        private ShelteredSettlementApplyResult(bool applied, string reason)
        {
            AppliedEvent = applied;
            Reason = reason ?? string.Empty;
        }

        public bool AppliedEvent { get; private set; }
        public string Reason { get; private set; }
    }
}
