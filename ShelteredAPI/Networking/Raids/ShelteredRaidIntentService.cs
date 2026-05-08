using System;

namespace ShelteredAPI.Networking.Raids
{
    internal sealed class ShelteredRaidIntentService : IDisposable
    {
        private readonly ShelteredRaidStateRegistry _registry;
        private bool _disposed;

        public ShelteredRaidIntentService()
            : this(new ShelteredRaidStateRegistry())
        {
        }

        internal ShelteredRaidIntentService(ShelteredRaidStateRegistry registry)
        {
            _registry = registry;
            ShelteredMultiplayerNetworkEvents.IntentReceived += OnIntentReceived;
            ShelteredMultiplayerNetworkEvents.AuthoritativeReceived += OnAuthoritativeReceived;
        }

        public bool PublishIntent(ShelteredRaidEvent raidEvent)
        {
            if (_disposed || raidEvent == null)
                return false;

            ShelteredNetworkGameplayEvent gameplayEvent = ShelteredRaidEvents.ToGameplayEvent(raidEvent);
            return ShelteredMultiplayerNetworkEvents.PublishIntent(gameplayEvent);
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
            if (!ShelteredRaidEvents.IsRaidEventKind(context.GameplayEvent.EventKind))
                return;

            ShelteredRaidEvent raidEvent = ShelteredRaidEvents.FromGameplayEvent(context.GameplayEvent);
            if (!string.Equals(raidEvent.EventKind, ShelteredNetworkEventKinds.RaidIntent, StringComparison.Ordinal))
                return;

            ShelteredRaidEvent authoritative = ValidateIntent(raidEvent);
            context.Accept(ShelteredRaidEvents.ToGameplayEvent(authoritative));
        }

        private void OnAuthoritativeReceived(ShelteredNetworkEventContext context)
        {
            if (_disposed || context == null || context.GameplayEvent == null)
                return;
            if (!ShelteredRaidEvents.IsRaidEventKind(context.GameplayEvent.EventKind))
                return;

            _registry.Apply(ShelteredRaidEvents.FromGameplayEvent(context.GameplayEvent), context.GameplayEvent.EventId);
        }

        private static ShelteredRaidEvent ValidateIntent(ShelteredRaidEvent raidEvent)
        {
            ShelteredRaidEvent result = raidEvent != null ? raidEvent.Copy() : new ShelteredRaidEvent();
            if (string.IsNullOrEmpty(result.RaidId) || result.AttackerPlayerId <= 0 || result.DefenderPlayerId <= 0
                || result.TargetBunkerOwnerId < 0 || result.RaidStrength <= 0)
            {
                result.EventKind = ShelteredNetworkEventKinds.RaidRejected;
                result.RejectionReason = "invalid-raid-intent";
                return result;
            }

            result.EventKind = ShelteredNetworkEventKinds.RaidAccepted;
            result.RejectionReason = string.Empty;
            return result;
        }
    }
}
