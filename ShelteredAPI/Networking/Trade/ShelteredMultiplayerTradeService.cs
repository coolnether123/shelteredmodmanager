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
        private bool _disposed;

        public ShelteredMultiplayerTradeService()
            : this(null)
        {
        }

        public ShelteredMultiplayerTradeService(Func<string, IItemStore> resolveOwnerStore)
        {
            _resolveOwnerStore = resolveOwnerStore;
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

            ShelteredMultiplayerTradeEvent authoritative = validation.Success
                ? CreateAuthoritativeTradeEvent(tradeEvent, ShelteredNetworkEventKinds.TradeOfferAccepted, string.Empty)
                : CreateAuthoritativeTradeEvent(tradeEvent, ShelteredNetworkEventKinds.TradeOfferRejected, validation.ErrorMessage);

            context.Accept(ShelteredMultiplayerTradeContractCodec.ToGameplayEvent(authoritative));
        }

        private void OnAuthoritativeReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null || context.GameplayEvent == null)
                return;
            if (!ShelteredMultiplayerTradeContractCodec.IsTradeEventKind(context.GameplayEvent.EventKind))
                return;

            Raise(AuthoritativeReceived, ShelteredMultiplayerTradeContractCodec.FromGameplayEvent(context.GameplayEvent));
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
    }
}
