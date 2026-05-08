using System.Collections.Generic;
using ShelteredAPI.Networking.Raids;
using ShelteredAPI.Networking.Settlements;
using ShelteredAPI.Networking.Trade;

namespace ShelteredAPI.Networking.Factions
{
    public sealed class NullShelteredFactionBridge : IShelteredMultiplayerFactionBridge
    {
        public bool IsAvailable
        {
            get { return false; }
        }

        public IList<ShelteredFactionInfo> GetKnownFactions()
        {
            return new List<ShelteredFactionInfo>();
        }

        public ShelteredFactionTerritorySnapshot GetFactionTerritorySnapshot()
        {
            return new ShelteredFactionTerritorySnapshot();
        }

        public void OnPlayerJoinedFaction(int playerId, string factionId)
        {
        }

        public void OnSettlementFounded(ShelteredSettlementState settlement)
        {
        }

        public void OnRaidResolved(ShelteredRaidState raid)
        {
        }

        public void OnTradeCompleted(ShelteredMultiplayerTradeEvent tradeEvent)
        {
        }

        public ShelteredFactionWorldTick BuildFactionWorldTick(long worldTick)
        {
            ShelteredFactionWorldTick tick = new ShelteredFactionWorldTick();
            tick.WorldTick = worldTick;
            return tick;
        }
    }
}
