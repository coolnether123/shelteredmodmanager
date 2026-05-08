using System;
using ShelteredAPI.Networking.Trade;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Encounters
{
    public sealed class ShelteredEncounterNegotiationService : IDisposable
    {
        private readonly ShelteredEncounterNegotiationStateRegistry _states;
        private readonly ShelteredMultiplayerTradeService _tradeService;
        private bool _disposed;

        public ShelteredEncounterNegotiationService()
            : this(null)
        {
        }

        public ShelteredEncounterNegotiationService(ShelteredMultiplayerTradeService tradeService)
            : this(new ShelteredEncounterNegotiationStateRegistry(), tradeService)
        {
        }

        internal ShelteredEncounterNegotiationService(
            ShelteredEncounterNegotiationStateRegistry states,
            ShelteredMultiplayerTradeService tradeService)
        {
            _states = states ?? new ShelteredEncounterNegotiationStateRegistry();
            _tradeService = tradeService;
            ShelteredMultiplayerNetworkEvents.IntentReceived += OnIntentReceived;
            ShelteredMultiplayerNetworkEvents.AuthoritativeReceived += OnAuthoritativeReceived;
        }

        public event Action<ShelteredEncounterNegotiationEvent> IntentReceived;
        public event Action<ShelteredEncounterNegotiationEvent> AuthoritativeReceived;

        public bool PublishInteractionIntent(ShelteredEncounterNegotiationEvent encounterEvent)
        {
            if (encounterEvent == null)
                return false;

            ShelteredEncounterNegotiationEvent copy = encounterEvent.Copy();
            copy.EventKind = ShelteredNetworkEventKinds.EncounterInteractionIntent;
            copy.State = ShelteredEncounterNegotiationStateKind.Proposed;
            return PublishIntent(copy);
        }

        public bool PublishIntent(ShelteredEncounterNegotiationEvent encounterEvent)
        {
            if (_disposed || encounterEvent == null)
                return false;

            return ShelteredMultiplayerNetworkEvents.PublishIntent(
                ShelteredEncounterNegotiationContractCodec.ToGameplayEvent(encounterEvent));
        }

        public bool BroadcastAuthoritative(ShelteredEncounterNegotiationEvent encounterEvent)
        {
            if (_disposed || encounterEvent == null)
                return false;

            return ShelteredMultiplayerNetworkEvents.BroadcastAuthoritative(
                ShelteredEncounterNegotiationContractCodec.ToGameplayEvent(encounterEvent));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ShelteredMultiplayerNetworkEvents.IntentReceived -= OnIntentReceived;
            ShelteredMultiplayerNetworkEvents.AuthoritativeReceived -= OnAuthoritativeReceived;
            _disposed = true;
        }

        private void OnIntentReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null || context.GameplayEvent == null)
                return;
            if (!ShelteredEncounterNegotiationContractCodec.IsEncounterEventKind(context.GameplayEvent.EventKind))
                return;

            ShelteredEncounterNegotiationEvent encounterEvent =
                ShelteredEncounterNegotiationContractCodec.FromGameplayEvent(context.GameplayEvent);
            Raise(IntentReceived, encounterEvent);

            if (!IsIntentKind(encounterEvent.EventKind))
                return;

            ShelteredEncounterNegotiationApplyResult proposed = _states.Apply(encounterEvent);
            if (!proposed.AppliedEvent)
                return;

            ShelteredEncounterNegotiationValidationResult validation = ValidateIntent(encounterEvent);
            ShelteredEncounterNegotiationEvent authoritative = CreateAuthoritative(
                encounterEvent,
                validation.Success
                    ? ShelteredNetworkEventKinds.EncounterNegotiationAccepted
                    : ShelteredNetworkEventKinds.EncounterNegotiationDeclined,
                validation.Success
                    ? ShelteredEncounterNegotiationStateKind.Accepted
                    : ShelteredEncounterNegotiationStateKind.Declined,
                validation.Reason);

            context.Accept(ShelteredEncounterNegotiationContractCodec.ToGameplayEvent(authoritative));
        }

        private void OnAuthoritativeReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null || context.GameplayEvent == null)
                return;
            if (!ShelteredEncounterNegotiationContractCodec.IsEncounterEventKind(context.GameplayEvent.EventKind))
                return;

            ShelteredEncounterNegotiationEvent encounterEvent =
                ShelteredEncounterNegotiationContractCodec.FromGameplayEvent(context.GameplayEvent);
            ShelteredEncounterNegotiationApplyResult result = _states.Apply(encounterEvent);
            if (!result.AppliedEvent)
                return;

            AppendAuthoritativeWorldEvent(encounterEvent);
            Raise(AuthoritativeReceived, encounterEvent);
        }

        private ShelteredEncounterNegotiationValidationResult ValidateIntent(
            ShelteredEncounterNegotiationEvent encounterEvent)
        {
            if (encounterEvent == null)
                return ShelteredEncounterNegotiationValidationResult.Failed("Encounter intent is required.");
            if (string.IsNullOrEmpty(encounterEvent.EncounterId))
                return ShelteredEncounterNegotiationValidationResult.Failed("Encounter id is required.");
            if (string.IsNullOrEmpty(encounterEvent.InitiatorTravelId)
                || string.IsNullOrEmpty(encounterEvent.ResponderTravelId))
            {
                return ShelteredEncounterNegotiationValidationResult.Failed("Participant travel ids are required.");
            }
            if (encounterEvent.InitiatorPlayerId > 0
                && encounterEvent.InitiatorPlayerId == encounterEvent.ResponderPlayerId)
            {
                return ShelteredEncounterNegotiationValidationResult.Failed("Encounter participants must be different players.");
            }

            if (encounterEvent.OfferedAction == ShelteredEncounterActionKind.Trade)
                return ValidateTradeIntent(encounterEvent);
            if (encounterEvent.OfferedAction == ShelteredEncounterActionKind.Fight)
            {
                // TODO(multiplayer-combat): start vanilla EncounterManager combat from this accepted primitive.
                return ShelteredEncounterNegotiationValidationResult.Ok();
            }
            if (encounterEvent.OfferedAction == ShelteredEncounterActionKind.Flee)
                return ShelteredEncounterNegotiationValidationResult.Ok();

            return ShelteredEncounterNegotiationValidationResult.Failed("Unsupported encounter action.");
        }

        private ShelteredEncounterNegotiationValidationResult ValidateTradeIntent(
            ShelteredEncounterNegotiationEvent encounterEvent)
        {
            if (encounterEvent.TradeOffer == null || encounterEvent.TradeOffer.Cargo.Count == 0)
                return ShelteredEncounterNegotiationValidationResult.Ok();

            ShelteredTradeCargoValidationResult tradeValidation = _tradeService != null
                ? _tradeService.ValidateOfferIntentForHost(encounterEvent.TradeOffer)
                : ShelteredMultiplayerTradeValidation.ValidateOfferIntent(encounterEvent.TradeOffer, null);

            return tradeValidation.Success
                ? ShelteredEncounterNegotiationValidationResult.Ok()
                : ShelteredEncounterNegotiationValidationResult.Failed(tradeValidation.ErrorMessage);
        }

        private static ShelteredEncounterNegotiationEvent CreateAuthoritative(
            ShelteredEncounterNegotiationEvent source,
            string eventKind,
            ShelteredEncounterNegotiationStateKind state,
            string reason)
        {
            ShelteredEncounterNegotiationEvent authoritative = source != null
                ? source.Copy()
                : new ShelteredEncounterNegotiationEvent();
            authoritative.EventId = string.Empty;
            authoritative.CorrelationId = string.Empty;
            authoritative.WorldTick = 0;
            authoritative.EventKind = eventKind ?? string.Empty;
            authoritative.State = state;
            authoritative.Reason = reason ?? string.Empty;
            return authoritative;
        }

        private static bool IsIntentKind(string eventKind)
        {
            return string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterInteractionIntent, StringComparison.Ordinal)
                || string.Equals(eventKind, ShelteredNetworkEventKinds.EncounterNegotiationProposed, StringComparison.Ordinal);
        }

        private static void AppendAuthoritativeWorldEvent(ShelteredEncounterNegotiationEvent encounterEvent)
        {
            if (encounterEvent == null)
                return;

            ShelteredWorldEvents.AppendAuthoritative(
                encounterEvent.EventKind,
                encounterEvent.EventId,
                ShelteredEncounterNegotiationContractCodec.ToPayloadJson(encounterEvent),
                encounterEvent.InitiatorPlayerId,
                encounterEvent.InitiatorPeerId);
        }

        private static void Raise(
            Action<ShelteredEncounterNegotiationEvent> handler,
            ShelteredEncounterNegotiationEvent encounterEvent)
        {
            if (handler != null)
                handler(encounterEvent);
        }

        private sealed class ShelteredEncounterNegotiationValidationResult
        {
            private ShelteredEncounterNegotiationValidationResult(bool success, string reason)
            {
                Success = success;
                Reason = reason ?? string.Empty;
            }

            public readonly bool Success;
            public readonly string Reason;

            public static ShelteredEncounterNegotiationValidationResult Ok()
            {
                return new ShelteredEncounterNegotiationValidationResult(true, string.Empty);
            }

            public static ShelteredEncounterNegotiationValidationResult Failed(string reason)
            {
                return new ShelteredEncounterNegotiationValidationResult(false, reason);
            }
        }
    }
}
