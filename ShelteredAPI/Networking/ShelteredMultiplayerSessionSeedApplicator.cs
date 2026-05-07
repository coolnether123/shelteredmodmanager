using System;
using ModAPI.Core;

namespace ShelteredAPI.Networking
{
    internal sealed class ShelteredMultiplayerSessionSeedApplicator : IShelteredMultiplayerSessionLifecycleHandler
    {
        private const string LogSource = "ShelteredAPI.Multiplayer.SessionSeed";
        private static readonly ShelteredMultiplayerSessionSeedApplicator _instance =
            new ShelteredMultiplayerSessionSeedApplicator();

        private string _appliedSeedSessionId = string.Empty;

        public static ShelteredMultiplayerSessionSeedApplicator Instance
        {
            get { return _instance; }
        }

        public void Handle(ShelteredMultiplayerLifecycleEvent lifecycleEvent)
        {
            if (lifecycleEvent == null || lifecycleEvent.Context == null)
                return;

            if (lifecycleEvent.Kind == ShelteredMultiplayerLifecycleEventKind.SessionDeactivated)
            {
                _appliedSeedSessionId = string.Empty;
                return;
            }

            if (lifecycleEvent.Kind != ShelteredMultiplayerLifecycleEventKind.SessionActivated
                && lifecycleEvent.Kind != ShelteredMultiplayerLifecycleEventKind.SetupReceived
                && lifecycleEvent.Kind != ShelteredMultiplayerLifecycleEventKind.LocalWorldLoaded)
                return;

            Apply(lifecycleEvent.Context, lifecycleEvent.Kind.ToString(), lifecycleEvent.Kind != ShelteredMultiplayerLifecycleEventKind.SessionActivated);
        }

        private void Apply(ShelteredMultiplayerSessionContext context, string reason, bool force)
        {
            if (context == null || !context.IsMultiplayerActive || string.IsNullOrEmpty(context.SessionId))
                return;

            if (!force && string.Equals(_appliedSeedSessionId, context.SessionId, StringComparison.Ordinal))
                return;

            int seed;
            string error;
            if (!ShelteredMultiplayerSessionSeed.TryApply(context.SessionId, out seed, out error))
                throw new InvalidOperationException("Failed to apply multiplayer session seed: " + error);

            _appliedSeedSessionId = context.SessionId;
            MMLog.WriteWithSource(MMLog.LogLevel.Debug, MMLog.LogCategory.Network, LogSource,
                "Session seed applied for " + reason + ". Seed=" + seed + ".");
        }
    }
}
