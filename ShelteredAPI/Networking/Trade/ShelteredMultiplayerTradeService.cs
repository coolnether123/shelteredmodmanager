using System;
using ShelteredAPI.Storage;

namespace ShelteredAPI.Networking.Trade
{
    /// <summary>
    /// First-pass trade event policy. It validates DTO shape and relies on the
    /// event sync service to perform host-authoritative broadcast.
    /// </summary>
    public sealed class ShelteredMultiplayerTradeService : IDisposable
    {
        private readonly Func<string, IItemStore> _resolveOwnerStore;
        private readonly ShelteredMultiplayerTradeStateRegistry _states;
        private readonly ShelteredMultiplayerTradeCargoReservationService _reservations;
        private bool _disposed;

        public ShelteredMultiplayerTradeService()
            : this(null)
        {
        }

        public ShelteredMultiplayerTradeService(Func<string, IItemStore> resolveOwnerStore)
            : this(
                resolveOwnerStore,
                new ShelteredMultiplayerTradeStateRegistry(),
                resolveOwnerStore != null ? new ShelteredMultiplayerTradeCargoReservationService(resolveOwnerStore) : null)
        {
        }

        internal ShelteredMultiplayerTradeService(
            Func<string, IItemStore> resolveOwnerStore,
            ShelteredMultiplayerTradeStateRegistry states,
            ShelteredMultiplayerTradeCargoReservationService reservations)
        {
            _resolveOwnerStore = resolveOwnerStore;
            _states = states;
            _reservations = reservations;
            ShelteredMultiplayerNetworkEvents.IntentReceived += OnIntentReceived;
            ShelteredMultiplayerNetworkEvents.AuthoritativeReceived += OnAuthoritativeReceived;
        }

        public event Action<ShelteredMultiplayerTradeEvent> IntentReceived;
        public event Action<ShelteredMultiplayerTradeEvent> AuthoritativeReceived;

        public bool PublishOfferIntent(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            if (tradeEvent == null)
                return false;

            ShelteredMultiplayerTradeEvent copy = tradeEvent.Copy();
            copy.EventKind = ShelteredNetworkEventKinds.TradeOfferIntent;
            return PublishIntent(copy);
        }

        public bool PublishIntent(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            if (_disposed || tradeEvent == null)
                return false;

            ShelteredNetworkGameplayEvent gameplayEvent = ShelteredMultiplayerTradeContractCodec.ToGameplayEvent(tradeEvent);
            return ShelteredMultiplayerNetworkEvents.PublishIntent(gameplayEvent);
        }

        public bool BroadcastAuthoritative(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            if (_disposed || tradeEvent == null)
                return false;

            ShelteredNetworkGameplayEvent gameplayEvent = ShelteredMultiplayerTradeContractCodec.ToGameplayEvent(tradeEvent);
            return ShelteredMultiplayerNetworkEvents.BroadcastAuthoritative(gameplayEvent);
        }

        internal ShelteredTradeCargoValidationResult ValidateOfferIntentForHost(
            ShelteredMultiplayerTradeEvent tradeEvent)
        {
            return ShelteredMultiplayerTradeValidation.ValidateOfferIntent(tradeEvent, _resolveOwnerStore);
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
            if (!ShelteredMultiplayerTradeContractCodec.IsTradeEventKind(context.GameplayEvent.EventKind))
                return;

            ShelteredMultiplayerTradeEvent tradeEvent = ShelteredMultiplayerTradeContractCodec.FromGameplayEvent(context.GameplayEvent);
            Raise(IntentReceived, tradeEvent);

            if (!string.Equals(tradeEvent.EventKind, ShelteredNetworkEventKinds.TradeOfferIntent, StringComparison.Ordinal))
                return;

            ShelteredTradeCargoValidationResult validation =
                ShelteredMultiplayerTradeValidation.ValidateOfferIntent(tradeEvent, _resolveOwnerStore);

            ShelteredMultiplayerTradeEvent authoritative;
            if (validation.Success && _reservations != null)
            {
                ShelteredTradeCargoReservationResult reservation = _reservations.Reserve(tradeEvent);
                if (!reservation.Success)
                    validation = ShelteredTradeCargoValidationResult.Failed(
                        reservation.ErrorMessage,
                        validation.TotalCargoLines,
                        validation.TotalItemCount);
            }

            authoritative = validation.Success
                ? CreateAuthoritativeTradeEvent(tradeEvent, ShelteredNetworkEventKinds.TradeOfferAccepted, string.Empty)
                : CreateAuthoritativeTradeEvent(tradeEvent, ShelteredNetworkEventKinds.TradeOfferRejected, validation.ErrorMessage);

            ApplyState(authoritative);

            context.Accept(ShelteredMultiplayerTradeContractCodec.ToGameplayEvent(authoritative));
        }

        private void OnAuthoritativeReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null || context.GameplayEvent == null)
                return;
            if (!ShelteredMultiplayerTradeContractCodec.IsTradeEventKind(context.GameplayEvent.EventKind))
                return;

            ShelteredMultiplayerTradeEvent tradeEvent =
                ShelteredMultiplayerTradeContractCodec.FromGameplayEvent(context.GameplayEvent);
            ApplyState(tradeEvent);
            Raise(AuthoritativeReceived, tradeEvent);
        }

        private static ShelteredMultiplayerTradeEvent CreateAuthoritativeTradeEvent(
            ShelteredMultiplayerTradeEvent source,
            string eventKind,
            string rejectionReason)
        {
            ShelteredMultiplayerTradeEvent authoritative = source != null
                ? source.Copy()
                : new ShelteredMultiplayerTradeEvent();
            authoritative.EventKind = eventKind ?? string.Empty;
            authoritative.RejectionReason = rejectionReason ?? string.Empty;
            return authoritative;
        }

        private static void Raise(Action<ShelteredMultiplayerTradeEvent> handler, ShelteredMultiplayerTradeEvent tradeEvent)
        {
            if (handler != null)
                handler(tradeEvent);
        }

        private void ApplyState(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            if (_states != null && tradeEvent != null)
                _states.Apply(tradeEvent);
        }
    }
}
