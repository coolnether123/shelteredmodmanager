using System;

namespace ShelteredAPI.Networking.Trade
{
    /// <summary>
    /// First-pass trade event policy. It validates DTO shape and relies on the
    /// event sync service to perform host-authoritative broadcast.
    /// </summary>
    public sealed class ShelteredMultiplayerTradeService : IDisposable
    {
        private bool _disposed;

        public ShelteredMultiplayerTradeService()
        {
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

            string validationError;
            ShelteredMultiplayerTradeEvent authoritative = ValidateTradeOfferIntent(tradeEvent, out validationError)
                ? CreateAuthoritativeTradeEvent(tradeEvent, ShelteredNetworkEventKinds.TradeOfferAccepted, string.Empty)
                : CreateAuthoritativeTradeEvent(tradeEvent, ShelteredNetworkEventKinds.TradeOfferRejected, validationError);

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

        private static bool ValidateTradeOfferIntent(ShelteredMultiplayerTradeEvent tradeEvent, out string error)
        {
            if (tradeEvent == null)
            {
                error = "Trade event is required.";
                return false;
            }

            if (string.IsNullOrEmpty(tradeEvent.SourceOwnerId))
            {
                error = "Source owner is required.";
                return false;
            }

            if (string.IsNullOrEmpty(tradeEvent.TargetOwnerId))
            {
                error = "Target owner is required.";
                return false;
            }

            if (tradeEvent.Cargo.Count == 0)
            {
                error = "Cargo is required.";
                return false;
            }

            for (int i = 0; i < tradeEvent.Cargo.Count; i++)
            {
                ShelteredTradeCargoDto cargo = tradeEvent.Cargo[i];
                if (cargo == null)
                {
                    error = "Cargo line is required.";
                    return false;
                }

                if (string.IsNullOrEmpty(cargo.ItemId))
                {
                    error = "Cargo item id is required.";
                    return false;
                }

                if (cargo.Count <= 0)
                {
                    error = "Cargo count must be positive.";
                    return false;
                }

                if (string.IsNullOrEmpty(cargo.SourceOwnerId))
                {
                    error = "Cargo source owner is required.";
                    return false;
                }

                if (string.IsNullOrEmpty(cargo.TargetOwnerId))
                {
                    error = "Cargo target owner is required.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
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
