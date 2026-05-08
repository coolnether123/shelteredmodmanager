using System;
using System.Collections.Generic;
using ModAPI.Networking;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Travel
{
    internal sealed class ShelteredTravelSyncService : IDisposable
    {
        private const string TravelIdPrefix = "travel";
        private const string SeedStreamPrefix = "MultiplayerSync.Travel";

        private readonly Dictionary<string, ShelteredTravelStartedEvent> _activeTravels =
            new Dictionary<string, ShelteredTravelStartedEvent>(StringComparer.Ordinal);
        private readonly IShelteredTravelStateRegistry _stateRegistry;
        private bool _disposed;

        public ShelteredTravelSyncService()
            : this(ShelteredExpeditionTravelHookService.Instance.Registry)
        {
        }

        internal ShelteredTravelSyncService(IShelteredTravelStateRegistry stateRegistry)
        {
            _stateRegistry = stateRegistry;
            Active = this;
            ShelteredMultiplayerNetworkEvents.IntentReceived += OnIntentReceived;
            ShelteredMultiplayerNetworkEvents.AuthoritativeReceived += OnAuthoritativeReceived;
        }

        public static ShelteredTravelSyncService Active { get; private set; }

        public event Action<ShelteredTravelStartedEvent> TravelStarted;
        public event Action<ShelteredTravelCorrectedEvent> TravelCorrected;
        public event Action<ShelteredTravelArrivedEvent> TravelArrived;

        public bool PublishTravelStarted(
            int partyId,
            int startGridX,
            int startGridY,
            int destinationGridX,
            int destinationGridY,
            float worldUnitsPerTick,
            long expectedArrivalTick)
        {
            if (_disposed)
                return false;

            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive)
                return false;

            ShelteredTravelStartedEvent started = CreateTravelStarted(
                context,
                partyId,
                startGridX,
                startGridY,
                destinationGridX,
                destinationGridY,
                worldUnitsPerTick,
                expectedArrivalTick);

            if (context.Mode == ShelteredMultiplayerSessionMode.Host)
                return BroadcastAuthoritativeTravelStarted(started);

            return ShelteredMultiplayerNetworkEvents.PublishIntent(
                ShelteredTravelContractCodec.ToGameplayEvent(started));
        }

        public bool PublishTravelStarted(ShelteredTravelStartedEvent started)
        {
            if (_disposed || started == null)
                return false;

            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive)
                return false;

            if (context.Mode == ShelteredMultiplayerSessionMode.Host)
                return BroadcastAuthoritativeTravelStarted(started);

            return ShelteredMultiplayerNetworkEvents.PublishIntent(
                ShelteredTravelContractCodec.ToGameplayEvent(started));
        }

        public bool PublishTravelCorrected(ShelteredTravelCorrectedEvent corrected)
        {
            if (_disposed || corrected == null)
                return false;

            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive)
                return false;

            if (context.Mode != ShelteredMultiplayerSessionMode.Host)
                return ShelteredMultiplayerNetworkEvents.PublishIntent(
                    ShelteredTravelContractCodec.ToGameplayEvent(corrected));

            return ShelteredMultiplayerNetworkEvents.BroadcastAuthoritative(
                ShelteredTravelContractCodec.ToGameplayEvent(
                    NormalizeAuthoritativeCorrected(corrected, context)));
        }

        public bool PublishTravelArrived(ShelteredTravelArrivedEvent arrived)
        {
            if (_disposed || arrived == null)
                return false;

            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive)
                return false;

            if (context.Mode != ShelteredMultiplayerSessionMode.Host)
                return ShelteredMultiplayerNetworkEvents.PublishIntent(
                    ShelteredTravelContractCodec.ToGameplayEvent(arrived));

            return ShelteredMultiplayerNetworkEvents.BroadcastAuthoritative(
                ShelteredTravelContractCodec.ToGameplayEvent(
                    NormalizeAuthoritativeArrived(arrived, context)));
        }

        public bool BroadcastAuthoritativeTravelStarted(ShelteredTravelStartedEvent started)
        {
            if (_disposed || started == null)
                return false;

            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || context.Mode != ShelteredMultiplayerSessionMode.Host)
                return false;

            ShelteredTravelStartedEvent authoritative = NormalizeAuthoritativeStarted(started, context);
            return ShelteredMultiplayerNetworkEvents.BroadcastAuthoritative(
                ShelteredTravelContractCodec.ToGameplayEvent(authoritative));
        }

        public void ApplyAuthoritativeTravel(ShelteredTravelStartedEvent started)
        {
            ApplyAuthoritativeTravel(started, null);
        }

        public void ApplyAuthoritativeTravel(ShelteredTravelStartedEvent started, string eventId)
        {
            if (_disposed || started == null || string.IsNullOrEmpty(started.TravelId))
                return;

            if (_stateRegistry != null)
            {
                ShelteredTravelApplyResult applyResult =
                    _stateRegistry.ApplyTravelStarted(started, ResolveCurrentEventId(eventId, started.TravelId, ShelteredNetworkEventKinds.TravelStarted));
                if (!applyResult.AppliedEvent)
                    return;
            }

            _activeTravels[started.TravelId] = started.Copy();
            ShelteredWorldEvents.AppendAuthoritative(
                ShelteredNetworkEventKinds.TravelStarted,
                started.TravelId,
                ShelteredTravelContractCodec.ToPayloadJson(started),
                started.OwnerPlayerId,
                started.OwnerPeerId);
            Raise(TravelStarted, started.Copy());
        }

        public void ApplyAuthoritativeTravel(ShelteredTravelCorrectedEvent corrected)
        {
            ApplyAuthoritativeTravel(corrected, null);
        }

        public void ApplyAuthoritativeTravel(ShelteredTravelCorrectedEvent corrected, string eventId)
        {
            if (_disposed || corrected == null || string.IsNullOrEmpty(corrected.TravelId))
                return;

            ShelteredTravelStartedEvent existing;
            _activeTravels.TryGetValue(corrected.TravelId, out existing);
            if (_stateRegistry != null)
            {
                ShelteredTravelApplyResult applyResult =
                    _stateRegistry.ApplyTravelCorrected(corrected, ResolveCurrentEventId(eventId, corrected.TravelId, ShelteredNetworkEventKinds.TravelCorrected));
                if (!applyResult.AppliedEvent)
                    return;
            }

            _activeTravels[corrected.TravelId] =
                ShelteredTravelPrediction.CreateCorrectedStart(existing, corrected);
            ShelteredWorldEvents.AppendAuthoritative(
                ShelteredNetworkEventKinds.TravelCorrected,
                corrected.TravelId,
                ShelteredTravelContractCodec.ToPayloadJson(corrected),
                existing != null ? existing.OwnerPlayerId : 0,
                existing != null ? existing.OwnerPeerId : NetworkDefaults.UnassignedPeerId);
            Raise(TravelCorrected, corrected.Copy());
        }

        public void ApplyAuthoritativeTravel(ShelteredTravelArrivedEvent arrived)
        {
            ApplyAuthoritativeTravel(arrived, null);
        }

        public void ApplyAuthoritativeTravel(ShelteredTravelArrivedEvent arrived, string eventId)
        {
            if (_disposed || arrived == null || string.IsNullOrEmpty(arrived.TravelId))
                return;

            ShelteredTravelStartedEvent existing;
            _activeTravels.TryGetValue(arrived.TravelId, out existing);
            if (_stateRegistry != null)
            {
                ShelteredTravelApplyResult applyResult =
                    _stateRegistry.ApplyTravelArrived(arrived, ResolveCurrentEventId(eventId, arrived.TravelId, ShelteredNetworkEventKinds.TravelArrived));
                if (!applyResult.AppliedEvent)
                    return;
            }

            _activeTravels.Remove(arrived.TravelId);
            ShelteredWorldEvents.AppendAuthoritative(
                ShelteredNetworkEventKinds.TravelArrived,
                arrived.TravelId,
                ShelteredTravelContractCodec.ToPayloadJson(arrived),
                existing != null ? existing.OwnerPlayerId : 0,
                existing != null ? existing.OwnerPeerId : NetworkDefaults.UnassignedPeerId);
            Raise(TravelArrived, arrived.Copy());
        }

        public bool TryGetActiveTravel(string travelId, out ShelteredTravelStartedEvent started)
        {
            started = null;
            if (string.IsNullOrEmpty(travelId))
                return false;

            ShelteredTravelStartedEvent existing;
            if (!_activeTravels.TryGetValue(travelId, out existing))
                return false;

            started = existing.Copy();
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ShelteredMultiplayerNetworkEvents.IntentReceived -= OnIntentReceived;
            ShelteredMultiplayerNetworkEvents.AuthoritativeReceived -= OnAuthoritativeReceived;
            _activeTravels.Clear();
            if (ReferenceEquals(Active, this))
                Active = null;
            _disposed = true;
        }

        private void OnIntentReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null || context.GameplayEvent == null)
                return;
            if (!ShelteredTravelContractCodec.IsTravelEventKind(context.GameplayEvent.EventKind))
                return;

            ShelteredMultiplayerSessionContext sessionContext = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (sessionContext == null || sessionContext.Mode != ShelteredMultiplayerSessionMode.Host)
            {
                context.Reject("Travel intents require host authority.");
                return;
            }

            if (string.Equals(context.GameplayEvent.EventKind, ShelteredNetworkEventKinds.TravelStarted, StringComparison.Ordinal))
            {
                ShelteredTravelStartedEvent started =
                    ShelteredTravelContractCodec.StartedFromGameplayEvent(context.GameplayEvent);
                context.Accept(ShelteredTravelContractCodec.ToGameplayEvent(
                    NormalizeAuthoritativeStarted(started, sessionContext)));
                return;
            }

            if (string.Equals(context.GameplayEvent.EventKind, ShelteredNetworkEventKinds.TravelCorrected, StringComparison.Ordinal))
            {
                context.Accept(ShelteredTravelContractCodec.ToGameplayEvent(
                    NormalizeAuthoritativeCorrected(
                        ShelteredTravelContractCodec.CorrectedFromGameplayEvent(context.GameplayEvent),
                        sessionContext)));
                return;
            }

            if (string.Equals(context.GameplayEvent.EventKind, ShelteredNetworkEventKinds.TravelArrived, StringComparison.Ordinal))
            {
                context.Accept(ShelteredTravelContractCodec.ToGameplayEvent(
                    NormalizeAuthoritativeArrived(
                        ShelteredTravelContractCodec.ArrivedFromGameplayEvent(context.GameplayEvent),
                        sessionContext)));
            }
        }

        private void OnAuthoritativeReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null || context.GameplayEvent == null)
                return;
            if (!ShelteredTravelContractCodec.IsTravelEventKind(context.GameplayEvent.EventKind))
                return;

            if (string.Equals(context.GameplayEvent.EventKind, ShelteredNetworkEventKinds.TravelStarted, StringComparison.Ordinal))
            {
                ApplyAuthoritativeTravel(
                    ShelteredTravelContractCodec.StartedFromGameplayEvent(context.GameplayEvent),
                    context.GameplayEvent.EventId);
                return;
            }

            if (string.Equals(context.GameplayEvent.EventKind, ShelteredNetworkEventKinds.TravelCorrected, StringComparison.Ordinal))
            {
                ApplyAuthoritativeTravel(
                    ShelteredTravelContractCodec.CorrectedFromGameplayEvent(context.GameplayEvent),
                    context.GameplayEvent.EventId);
                return;
            }

            if (string.Equals(context.GameplayEvent.EventKind, ShelteredNetworkEventKinds.TravelArrived, StringComparison.Ordinal))
                ApplyAuthoritativeTravel(
                    ShelteredTravelContractCodec.ArrivedFromGameplayEvent(context.GameplayEvent),
                    context.GameplayEvent.EventId);
        }

        private static ShelteredTravelStartedEvent CreateTravelStarted(
            ShelteredMultiplayerSessionContext context,
            int partyId,
            int startGridX,
            int startGridY,
            int destinationGridX,
            int destinationGridY,
            float worldUnitsPerTick,
            long expectedArrivalTick)
        {
            long startTick = context.WorldTick;
            string travelId = CreateTravelId(context, partyId, startTick, destinationGridX, destinationGridY);
            ShelteredTravelStartedEvent started = new ShelteredTravelStartedEvent();
            started.TravelId = travelId;
            started.OwnerPlayerId = context.LocalPlayerId;
            started.OwnerPeerId = context.NetworkLocalPeerId;
            started.PartyId = partyId;
            started.StartTick = startTick;
            started.StartGridX = startGridX;
            started.StartGridY = startGridY;
            started.DestinationGridX = destinationGridX;
            started.DestinationGridY = destinationGridY;
            started.WorldUnitsPerTick = worldUnitsPerTick;
            started.ExpectedArrivalTick = expectedArrivalTick;
            started.SeedStreamName = CreateSeedStreamName(context.SessionId, travelId);
            return started;
        }

        private static ShelteredTravelStartedEvent NormalizeAuthoritativeStarted(
            ShelteredTravelStartedEvent started,
            ShelteredMultiplayerSessionContext context)
        {
            ShelteredTravelStartedEvent authoritative = started != null
                ? started.Copy()
                : new ShelteredTravelStartedEvent();

            if (string.IsNullOrEmpty(authoritative.TravelId))
            {
                authoritative.TravelId = CreateTravelId(
                    context,
                    authoritative.PartyId,
                    authoritative.StartTick,
                    authoritative.DestinationGridX,
                    authoritative.DestinationGridY);
            }

            if (authoritative.OwnerPlayerId <= 0)
                authoritative.OwnerPlayerId = context.GetPlayerIdForNetworkPeer(authoritative.OwnerPeerId);
            if (authoritative.OwnerPeerId == NetworkDefaults.UnassignedPeerId)
                authoritative.OwnerPeerId = context.NetworkLocalPeerId;
            if (string.IsNullOrEmpty(authoritative.SeedStreamName))
                authoritative.SeedStreamName = CreateSeedStreamName(context.SessionId, authoritative.TravelId);

            return authoritative;
        }

        private static ShelteredTravelCorrectedEvent NormalizeAuthoritativeCorrected(
            ShelteredTravelCorrectedEvent corrected,
            ShelteredMultiplayerSessionContext context)
        {
            ShelteredTravelCorrectedEvent authoritative = corrected != null
                ? corrected.Copy()
                : new ShelteredTravelCorrectedEvent();

            long acceptedTick = ResolveAuthoritativeTick(context);
            long remainingDuration = ResolvePositiveDuration(
                authoritative.CorrectionTick,
                authoritative.ExpectedArrivalTick);
            authoritative.CorrectionTick = acceptedTick;
            if (remainingDuration > 0)
                authoritative.ExpectedArrivalTick = acceptedTick + remainingDuration;
            else if (authoritative.ExpectedArrivalTick < acceptedTick)
                authoritative.ExpectedArrivalTick = acceptedTick;

            return authoritative;
        }

        private static ShelteredTravelArrivedEvent NormalizeAuthoritativeArrived(
            ShelteredTravelArrivedEvent arrived,
            ShelteredMultiplayerSessionContext context)
        {
            ShelteredTravelArrivedEvent authoritative = arrived != null
                ? arrived.Copy()
                : new ShelteredTravelArrivedEvent();

            authoritative.ArrivalTick = ResolveAuthoritativeTick(context);
            return authoritative;
        }

        private static long ResolveAuthoritativeTick(ShelteredMultiplayerSessionContext context)
        {
            return context != null && context.WorldTick > 0 ? context.WorldTick : 0;
        }

        private static long ResolvePositiveDuration(long startTick, long expectedArrivalTick)
        {
            return expectedArrivalTick > startTick ? expectedArrivalTick - startTick : 0;
        }

        private static string CreateTravelId(
            ShelteredMultiplayerSessionContext context,
            int partyId,
            long startTick,
            int destinationGridX,
            int destinationGridY)
        {
            string sessionId = context != null ? context.SessionId : string.Empty;
            int ownerPlayerId = context != null ? context.LocalPlayerId : 0;
            return TravelIdPrefix + ":"
                + NormalizeIdPart(sessionId) + ":"
                + ownerPlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"
                + partyId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"
                + startTick.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"
                + destinationGridX.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"
                + destinationGridY.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string CreateSeedStreamName(string sessionId, string travelId)
        {
            return SeedStreamPrefix + "." + NormalizeIdPart(sessionId) + "." + NormalizeIdPart(travelId);
        }

        private static string NormalizeIdPart(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "none";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if ((c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '.'
                    || c == '_'
                    || c == '-')
                {
                    continue;
                }

                chars[i] = '_';
            }

            return new string(chars);
        }

        private static void Raise(Action<ShelteredTravelStartedEvent> handler, ShelteredTravelStartedEvent value)
        {
            if (handler != null)
                handler(value);
        }

        private static void Raise(Action<ShelteredTravelCorrectedEvent> handler, ShelteredTravelCorrectedEvent value)
        {
            if (handler != null)
                handler(value);
        }

        private static void Raise(Action<ShelteredTravelArrivedEvent> handler, ShelteredTravelArrivedEvent value)
        {
            if (handler != null)
                handler(value);
        }

        private static string ResolveCurrentEventId(string eventId, string travelId, string eventKind)
        {
            if (!string.IsNullOrEmpty(eventId))
                return eventId;

            return "travel-sync:" + (eventKind ?? string.Empty) + ":" + (travelId ?? string.Empty) + ":"
                + ShelteredMultiplayer.Hooks.CurrentWorldTick.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
