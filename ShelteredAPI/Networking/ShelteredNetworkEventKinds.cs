namespace ShelteredAPI.Networking
{
    /// <summary>
    /// Shared event-kind keys for Sheltered gameplay synchronization.
    /// This is intentionally small; patches and mods can add more kind strings as they wire new events.
    /// </summary>
    public static class ShelteredNetworkEventKinds
    {
        public const string BunkerRegistered = "Bunker.Registered";
        public const string BunkerMoved = "Bunker.Moved";
        public const string BunkerOnlineStateChanged = "Bunker.OnlineStateChanged";
        public const string WorldClockSample = "World.ClockSample";
        public const string ExpeditionStarted = "Expedition.Started";
        public const string ExpeditionRouteChanged = "Expedition.RouteChanged";
        public const string ExpeditionReturned = "Expedition.Returned";
        public const string LocationSearched = "Location.Searched";
        public const string LocationGenerated = "Location.Generated";
        public const string LocationDiscovered = "Location.Discovered";
        public const string LocationLootGenerated = "Location.LootGenerated";
        public const string LocationLootTaken = "Location.LootTaken";
        public const string LocationDepleted = "Location.Depleted";
        public const string LocationCorrected = "Location.Corrected";
        public const string ResourceClaimed = "Resource.Claimed";
        public const string ResourceNodeGenerated = "ResourceNode.Generated";
        public const string ResourceNodeClaimed = "ResourceNode.Claimed";
        public const string ResourceNodeHarvested = "ResourceNode.Harvested";
        public const string ResourceNodeDepleted = "ResourceNode.Depleted";
        public const string ResourceNodeRegenerated = "ResourceNode.Regenerated";
        public const string TradeOfferIntent = "Trade.OfferIntent";
        public const string TradeOfferAccepted = "Trade.OfferAccepted";
        public const string TradeOfferRejected = "Trade.OfferRejected";
        public const string TradeCargoReserved = "Trade.CargoReserved";
        public const string TradeCaravanLaunched = "Trade.CaravanLaunched";
        public const string TradeCaravanArrived = "Trade.CaravanArrived";
        public const string TradeCompleted = "Trade.Completed";
        public const string TradeCancelled = "Trade.Cancelled";
        public const string TradeFailed = "Trade.Failed";
        public const string TravelStarted = "Travel.Started";
        public const string TravelCorrected = "Travel.Corrected";
        public const string TravelArrived = "Travel.Arrived";
        public const string EncounterInteractionIntent = "Encounter.InteractionIntent";
        public const string EncounterNegotiationProposed = "Encounter.NegotiationProposed";
        public const string EncounterNegotiationAccepted = "Encounter.NegotiationAccepted";
        public const string EncounterNegotiationDeclined = "Encounter.NegotiationDeclined";
        public const string EncounterNegotiationResolved = "Encounter.NegotiationResolved";
        public const string EncounterNegotiationExpired = "Encounter.NegotiationExpired";
        public const string RaidIntent = "Raid.Intent";
        public const string RaidAccepted = "Raid.Accepted";
        public const string RaidRejected = "Raid.Rejected";
        public const string RaidLaunched = "Raid.Launched";
        public const string RaidWarning = "Raid.Warning";
        public const string RaidArrived = "Raid.Arrived";
        public const string RaidResolved = "Raid.Resolved";
        public const string RaidCancelled = "Raid.Cancelled";
        public const string SettlementFounded = "Settlement.Founded";
        public const string SettlementUpdated = "Settlement.Updated";
        public const string SettlementDestroyed = "Settlement.Destroyed";
        public const string ResourceShipmentLaunched = "ResourceShipment.Launched";
        public const string FactionMarkerUpdated = "Faction.MarkerUpdated";
    }
}
