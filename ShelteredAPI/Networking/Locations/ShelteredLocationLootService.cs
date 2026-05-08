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

            ShelteredLocationEvent locationEvent = CreateEventFromState(state, "Vanilla.MapRegion.Generated");
            locationEvent.EventCorrelationId = BuildLocationEventCorrelationId(ShelteredNetworkEventKinds.LocationGenerated, state.LocationId);
            return PublishOrApply(ShelteredNetworkEventKinds.LocationGenerated, locationEvent);
        }

        public bool RecordDiscovered(MapRegion region)
        {
            LocationState state = ShelteredLocationVanillaReader.Read(region);
            if (state == null)
                return false;

            ShelteredLocationEvent locationEvent = CreateEventFromState(state, "Vanilla.MapRegion.Discovered");
            locationEvent.EventCorrelationId = BuildLocationEventCorrelationId(ShelteredNetworkEventKinds.LocationDiscovered, state.LocationId);
            return PublishOrApply(ShelteredNetworkEventKinds.LocationDiscovered, locationEvent);
        }

        public bool RecordLootGenerated(MapRegion region)
        {
            LocationState state = ShelteredLocationVanillaReader.Read(region);
            if (state == null)
                return false;

            ShelteredLocationEvent locationEvent = CreateEventFromState(state, "Vanilla.MapRegion.LootGenerated");
            locationEvent.Loot = ShelteredLocationVanillaReader.ReadDiscoveredLoot(region, "Generated");
            locationEvent.EventCorrelationId = ShelteredLocationStateRegistry.BuildLootSetEventKey(
                state.LocationId,
                locationEvent.Loot,
                locationEvent.WorldTick);
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
            ShelteredLocationEvent locationEvent = CreateEventFromState(state, "Vanilla.MapRegion.Depleted");
            locationEvent.EventCorrelationId = BuildLocationEventCorrelationId(ShelteredNetworkEventKinds.LocationDepleted, state.LocationId);
            return PublishOrApply(ShelteredNetworkEventKinds.LocationDepleted, locationEvent);
        }

        public bool ApplyAuthoritative(string eventKind, ShelteredLocationEvent locationEvent)
        {
            return ApplyAuthoritative(eventKind, locationEvent, string.Empty);
        }

        internal bool ApplyAuthoritative(string eventKind, ShelteredLocationEvent locationEvent, string authoritativeEventId)
        {
            if (_disposed || locationEvent == null || string.IsNullOrEmpty(locationEvent.LocationId))
                return false;

            LocationState state = ToState(locationEvent);
            CompleteStateFromExisting(state);
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.LocationLootGenerated, StringComparison.Ordinal))
            {
                _registry.Upsert(state);
                if (!_registry.TrySetLoot(locationEvent.LocationId, locationEvent.Loot))
                    return false;
                state.IsDepleted = _registry.IsDepleted(locationEvent.LocationId);
                state.RemainingLootSummaryJson = ShelteredLocationLootDiagnostics.ToLootSummaryJson(_registry.GetLoot(locationEvent.LocationId));
            }
            else if (string.Equals(eventKind, ShelteredNetworkEventKinds.LocationLootTaken, StringComparison.Ordinal))
            {
                string errorMessage;
                if (!_registry.TryApplyLootTaken(
                    locationEvent.LocationId,
                    ResolveEventCorrelationId(locationEvent, authoritativeEventId),
                    locationEvent.Loot,
                    locationEvent.PlayerId,
                    locationEvent.WorldTick,
                    out errorMessage))
                {
                    return false;
                }
                state.IsDepleted = _registry.IsDepleted(locationEvent.LocationId);
                state.RemainingLootSummaryJson = ShelteredLocationLootDiagnostics.ToLootSummaryJson(_registry.GetLoot(locationEvent.LocationId));
            }
            else if (string.Equals(eventKind, ShelteredNetworkEventKinds.LocationDepleted, StringComparison.Ordinal))
                state.IsDepleted = true;

            _registry.Upsert(state);
            ShelteredWorldEvents.AppendAuthoritative(
                eventKind,
                ResolveEventCorrelationId(locationEvent, authoritativeEventId),
                ShelteredLocationEvents.ToPayloadJson(locationEvent),
                locationEvent.PlayerId,
                NetworkDefaults.UnassignedPeerId);
            Raise(LocationEventApplied, locationEvent.Copy());
            return true;
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
                return ApplyAuthoritative(eventKind, locationEvent);

            if (context.Mode == ShelteredMultiplayerSessionMode.Host)
            {
                if (!ApplyAuthoritative(eventKind, locationEvent))
                    return false;
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

            ShelteredLocationEvent locationEvent = ShelteredLocationEvents.FromGameplayEvent(context.GameplayEvent);
            if (string.IsNullOrEmpty(locationEvent.LocationId))
            {
                context.Reject("Location id is required.");
                return;
            }

            if (string.Equals(context.GameplayEvent.EventKind, ShelteredNetworkEventKinds.LocationLootTaken, StringComparison.Ordinal))
            {
                string errorMessage;
                if (!_registry.CanApplyLootTaken(locationEvent.LocationId, locationEvent.Loot, out errorMessage))
                {
                    context.Reject(errorMessage);
                    return;
                }
            }

            context.Accept(ShelteredLocationEvents.ToGameplayEvent(context.GameplayEvent.EventKind, locationEvent));
        }

        private void OnAuthoritativeReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null || context.GameplayEvent == null || !ShelteredLocationEvents.IsLocationEventKind(context.GameplayEvent.EventKind))
                return;

            ApplyAuthoritative(
                context.GameplayEvent.EventKind,
                ShelteredLocationEvents.FromGameplayEvent(context.GameplayEvent),
                context.GameplayEvent.EventId);
        }

        private static ShelteredLocationEvent CreateEventFromState(LocationState state, string reason)
        {
            ShelteredLocationEvent locationEvent = new ShelteredLocationEvent();
            locationEvent.LocationId = state.LocationId;
            locationEvent.MapIdentity = state.MapIdentity;
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
            state.MapIdentity = locationEvent.MapIdentity;
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
            return ShelteredLocationStateRegistry.BuildTakenEventKey(locationId, loot, playerId, tick);
        }

        private static string BuildLocationEventCorrelationId(string eventKind, string locationId)
        {
            return "locationevent:" + (eventKind ?? string.Empty) + ":" + (locationId ?? string.Empty);
        }

        private static string ResolveEventCorrelationId(ShelteredLocationEvent locationEvent, string fallbackEventId)
        {
            if (locationEvent != null && !string.IsNullOrEmpty(locationEvent.EventCorrelationId))
                return locationEvent.EventCorrelationId;
            if (!string.IsNullOrEmpty(fallbackEventId))
                return fallbackEventId;
            return locationEvent != null ? locationEvent.LocationId : string.Empty;
        }

        private void CompleteStateFromExisting(LocationState state)
        {
            if (state == null || string.IsNullOrEmpty(state.LocationId))
                return;

            LocationState existing;
            if (!_registry.TryGet(state.LocationId, out existing))
                return;

            if (string.IsNullOrEmpty(state.MapIdentity))
                state.MapIdentity = existing.MapIdentity;
            if (string.IsNullOrEmpty(state.LocationKind))
                state.LocationKind = existing.LocationKind;
            if (string.IsNullOrEmpty(state.GeneratedSeedStream))
                state.GeneratedSeedStream = existing.GeneratedSeedStream;
            if (state.GeneratedWorldTick <= 0)
                state.GeneratedWorldTick = existing.GeneratedWorldTick;
            if (state.DiscoveredByPlayerId <= 0)
                state.DiscoveredByPlayerId = existing.DiscoveredByPlayerId;
            state.IsGenerated = state.IsGenerated || existing.IsGenerated;
            state.IsSearched = state.IsSearched || existing.IsSearched;
            if (string.IsNullOrEmpty(state.RemainingLootSummaryJson))
                state.RemainingLootSummaryJson = existing.RemainingLootSummaryJson;
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
