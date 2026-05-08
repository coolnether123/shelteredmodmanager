using System;
using System.Collections.Generic;
using ShelteredAPI.Networking.Raids;
using ShelteredAPI.Networking.Settlements;
using ShelteredAPI.Networking.Trade;

namespace ShelteredAPI.Networking.Factions
{
    public sealed class ShelteredFactionInfo
    {
        public ShelteredFactionInfo()
        {
            FactionId = string.Empty;
            DisplayName = string.Empty;
            PayloadJson = string.Empty;
        }

        public string FactionId { get; set; }
        public string DisplayName { get; set; }
        public string PayloadJson { get; set; }
    }

    public sealed class ShelteredFactionTerritoryCell
    {
        public ShelteredFactionTerritoryCell()
        {
            FactionId = string.Empty;
        }

        public string FactionId { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public int Influence { get; set; }
    }

    public sealed class ShelteredFactionTerritorySnapshot
    {
        public ShelteredFactionTerritorySnapshot()
        {
            Cells = new List<ShelteredFactionTerritoryCell>();
        }

        public IList<ShelteredFactionTerritoryCell> Cells { get; private set; }
    }

    public sealed class ShelteredFactionWorldTick
    {
        public ShelteredFactionWorldTick()
        {
            Events = new List<ShelteredFactionWorldEvent>();
        }

        public long WorldTick { get; set; }
        public IList<ShelteredFactionWorldEvent> Events { get; private set; }
    }

    public interface IShelteredMultiplayerFactionBridge
    {
        bool IsAvailable { get; }
        IList<ShelteredFactionInfo> GetKnownFactions();
        ShelteredFactionTerritorySnapshot GetFactionTerritorySnapshot();
        void OnPlayerJoinedFaction(int playerId, string factionId);
        void OnSettlementFounded(ShelteredSettlementState settlement);
        void OnRaidResolved(ShelteredRaidState raid);
        void OnTradeCompleted(ShelteredMultiplayerTradeEvent tradeEvent);
        ShelteredFactionWorldTick BuildFactionWorldTick(long worldTick);
    }
}
