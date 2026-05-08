using System;
using System.Collections.Generic;
using ModAPI.Networking;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Locations
{
    internal sealed class ShelteredLocationLootService : IDisposable
    {
        private const string SeedStreamPrefix = "MultiplayerSync.Loot";
        private static readonly ShelteredLocationLootService _instance = new ShelteredLocationLootService();
        private readonly ShelteredLocationStateRegistry _registry;
        private bool _disposed;

        public static ShelteredLocationLootService Instance
        {
            get { return _instance; }
        }

        public ShelteredLocationLootService()
            : this(new ShelteredLocationStateRegistry(), true)
        {
        }

        internal ShelteredLocationLootService(ShelteredLocationStateRegistry registry, bool subscribe)
        {
            _registry = registry ?? new ShelteredLocationStateRegistry();
            if (subscribe)
            {
                ShelteredMultiplayerNetworkEvents.IntentReceived += OnIntentReceived;
                ShelteredMultiplayerNetworkEvents.AuthoritativeReceived += OnAuthoritativeReceived;
            }
        }

        public ShelteredLocationStateRegistry Registry
        {
            get { return _registry; }
        }

        public event Action<ShelteredLocationEvent> LocationEventApplied;

        public bool RecordGenerated(MapRegion region)
        {
            LocationState state = ShelteredLocationVanillaReader.Read(region);
            if (state == null)
                return false;

            return PublishOrApply(ShelteredNetworkEventKinds.LocationGenerated, CreateEventFromState(state, "Vanilla.MapRegion.Generated"));
        }

        public bool RecordDiscovered(MapRegion region)
        {
            LocationState state = ShelteredLocationVanillaReader.Read(region);
            if (state == null)
                return false;

            return PublishOrApply(ShelteredNetworkEventKinds.LocationDiscovered, CreateEventFromState(state, "Vanilla.MapRegion.Discovered"));
        }

        public bool RecordLootGenerated(MapRegion region)
        {
            LocationState state = ShelteredLocationVanillaReader.Read(region);
            if (state == null)
                return false;

            ShelteredLocationEvent locationEvent = CreateEventFromState(state, "Vanilla.MapRegion.LootGenerated");
            locationEvent.Loot = ShelteredLocationVanillaReader.ReadDiscoveredLoot(region, "Generated");
            return PublishOrApply(ShelteredNetworkEventKinds.LocationLootGenerated, locationEvent);
        }

        public bool RecordLootTaken(MapRegion region, IList<LootItemRecord> taken)
        {
            LocationState state = ShelteredLocationVanillaReader.Read(region);
            if (state == null)
                return false;

            ShelteredLocationEvent locationEvent = CreateEventFromState(state, "Vanilla.MapRegion.LootTaken");
            locationEvent.Loot = ShelteredLocationEvent.CloneLoot(taken);
            locationEvent.EventCorrelationId = BuildLootTakenCorrelationId(state.LocationId, locationEvent.Loot, locationEvent.PlayerId, locationEvent.WorldTick);
            return PublishOrApply(ShelteredNetworkEventKinds.LocationLootTaken, locationEvent);
        }

        public bool RecordDepleted(MapRegion region)
        {
            LocationState state = ShelteredLocationVanillaReader.Read(region);
            if (state == null)
                return false;

            state.IsDepleted = true;
            return PublishOrApply(ShelteredNetworkEventKinds.LocationDepleted, CreateEventFromState(state, "Vanilla.MapRegion.Depleted"));
        }

        public void ApplyAuthoritative(string eventKind, ShelteredLocationEvent locationEvent)
        {
            if (_disposed || locationEvent == null || string.IsNullOrEmpty(locationEvent.LocationId))
                return;

            LocationState state = ToState(locationEvent);
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.LocationLootGenerated, StringComparison.Ordinal))
                _registry.SetLoot(locationEvent.LocationId, locationEvent.Loot);
            else if (string.Equals(eventKind, ShelteredNetworkEventKinds.LocationLootTaken, StringComparison.Ordinal))
                _registry.ApplyLootTaken(locationEvent.LocationId, locationEvent.EventCorrelationId, locationEvent.Loot, locationEvent.PlayerId, locationEvent.WorldTick);
            else if (string.Equals(eventKind, ShelteredNetworkEventKinds.LocationDepleted, StringComparison.Ordinal))
                state.IsDepleted = true;

            if (string.Equals(eventKind, ShelteredNetworkEventKinds.LocationLootTaken, StringComparison.Ordinal)
                && _registry.IsDepleted(locationEvent.LocationId))
                state.IsDepleted = true;

            _registry.Upsert(state);
            ShelteredWorldEvents.AppendAuthoritative(
                eventKind,
                !string.IsNullOrEmpty(locationEvent.EventCorrelationId) ? locationEvent.EventCorrelationId : locationEvent.LocationId,
                ShelteredLocationEvents.ToPayloadJson(locationEvent),
                locationEvent.PlayerId,
                NetworkDefaults.UnassignedPeerId);
            Raise(LocationEventApplied, locationEvent.Copy());
        }

        public static string CreateLocationSeedStreamName(string locationId)
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            string sessionId = context != null ? context.SessionId : string.Empty;
            return SeedStreamPrefix + "." + NormalizeIdPart(sessionId) + "." + NormalizeIdPart(locationId);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ShelteredMultiplayerNetworkEvents.IntentReceived -= OnIntentReceived;
            ShelteredMultiplayerNetworkEvents.AuthoritativeReceived -= OnAuthoritativeReceived;
            _disposed = true;
        }

        private bool PublishOrApply(string eventKind, ShelteredLocationEvent locationEvent)
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive)
            {
                ApplyAuthoritative(eventKind, locationEvent);
                return true;
            }

            if (context.Mode == ShelteredMultiplayerSessionMode.Host)
            {
                ApplyAuthoritative(eventKind, locationEvent);
                if (ShelteredMultiplayerNetworkEvents.IsAvailable)
                    return ShelteredMultiplayerNetworkEvents.BroadcastAuthoritative(ShelteredLocationEvents.ToGameplayEvent(eventKind, locationEvent));
                return true;
            }

            return ShelteredMultiplayerNetworkEvents.PublishIntent(ShelteredLocationEvents.ToGameplayEvent(eventKind, locationEvent));
        }

        private void OnIntentReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null || context.GameplayEvent == null || !ShelteredLocationEvents.IsLocationEventKind(context.GameplayEvent.EventKind))
                return;

            ShelteredMultiplayerSessionContext sessionContext = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (sessionContext == null || sessionContext.Mode != ShelteredMultiplayerSessionMode.Host)
            {
                context.Reject("Location and loot intents require host authority.");
                return;
            }

            context.Accept(context.GameplayEvent.Copy());
        }

        private void OnAuthoritativeReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null || context.GameplayEvent == null || !ShelteredLocationEvents.IsLocationEventKind(context.GameplayEvent.EventKind))
                return;

            ApplyAuthoritative(context.GameplayEvent.EventKind, ShelteredLocationEvents.FromGameplayEvent(context.GameplayEvent));
        }

        private static ShelteredLocationEvent CreateEventFromState(LocationState state, string reason)
        {
            ShelteredLocationEvent locationEvent = new ShelteredLocationEvent();
            locationEvent.LocationId = state.LocationId;
            locationEvent.GridX = state.GridX;
            locationEvent.GridY = state.GridY;
            locationEvent.LocationKind = state.LocationKind;
            locationEvent.SeedStreamName = state.GeneratedSeedStream;
            locationEvent.WorldTick = state.LastUpdatedTick;
            locationEvent.PlayerId = state.DiscoveredByPlayerId;
            locationEvent.IsGenerated = state.IsGenerated;
            locationEvent.IsSearched = state.IsSearched;
            locationEvent.IsDepleted = state.IsDepleted;
            locationEvent.RemainingLootSummaryJson = state.RemainingLootSummaryJson;
            locationEvent.Reason = reason ?? string.Empty;
            return locationEvent;
        }

        private static LocationState ToState(ShelteredLocationEvent locationEvent)
        {
            LocationState state = new LocationState();
            state.LocationId = locationEvent.LocationId;
            state.GridX = locationEvent.GridX;
            state.GridY = locationEvent.GridY;
            state.LocationKind = locationEvent.LocationKind;
            state.GeneratedSeedStream = locationEvent.SeedStreamName;
            state.GeneratedWorldTick = locationEvent.WorldTick;
            state.DiscoveredByPlayerId = locationEvent.PlayerId;
            state.IsGenerated = locationEvent.IsGenerated;
            state.IsSearched = locationEvent.IsSearched;
            state.IsDepleted = locationEvent.IsDepleted;
            state.RemainingLootSummaryJson = locationEvent.RemainingLootSummaryJson;
            state.LastUpdatedTick = locationEvent.WorldTick;
            return state;
        }

        private static string BuildLootTakenCorrelationId(string locationId, IList<LootItemRecord> loot, int playerId, long tick)
        {
            return "loottaken:" + locationId + ":" + playerId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + tick.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + (loot != null ? loot.Count : 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string NormalizeIdPart(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "none";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')
                    continue;
                chars[i] = '_';
            }

            return new string(chars);
        }

        private static void Raise(Action<ShelteredLocationEvent> handler, ShelteredLocationEvent value)
        {
            if (handler != null)
                handler(value);
        }
    }
}
