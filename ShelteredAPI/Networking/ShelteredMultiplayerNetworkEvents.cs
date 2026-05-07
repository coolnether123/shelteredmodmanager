using System;
using ModAPI.Core;

namespace ShelteredAPI.Networking
{
    /// <summary>
    /// Public Sheltered-facing event-sync facade. Game hooks and mods raise intents here and subscribe
    /// to authoritative events without referencing the low-level transport.
    /// </summary>
    public static class ShelteredMultiplayerNetworkEvents
    {
        private static readonly object Sync = new object();
        private static ShelteredMultiplayerEventSyncService _service;

        public static event Action<ShelteredNetworkEventContext> IntentReceived;
        public static event Action<ShelteredNetworkEventContext> AuthoritativeReceived;
        public static event Action<ShelteredNetworkEventContext> NotificationReceived;
        public static event Action<ShelteredNetworkEventContext> AnyReceived;

        public static bool IsAvailable
        {
            get
            {
                lock (Sync)
                {
                    return _service != null;
                }
            }
        }

        public static bool PublishIntent(string eventKind, string actorId, string targetId, string details)
        {
            ShelteredNetworkGameplayEvent gameplayEvent = new ShelteredNetworkGameplayEvent();
            gameplayEvent.EventKind = eventKind ?? string.Empty;
            gameplayEvent.ActorId = actorId ?? string.Empty;
            gameplayEvent.TargetId = targetId ?? string.Empty;
            gameplayEvent.Details = details ?? string.Empty;
            return PublishIntent(gameplayEvent);
        }

        public static bool PublishIntent(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            ShelteredMultiplayerEventSyncService service = CurrentService;
            return service != null && service.PublishIntent(gameplayEvent);
        }

        public static bool BroadcastAuthoritative(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            ShelteredMultiplayerEventSyncService service = CurrentService;
            return service != null && service.BroadcastAuthoritative(gameplayEvent);
        }

        internal static void Attach(ShelteredMultiplayerEventSyncService service)
        {
            lock (Sync)
            {
                _service = service;
            }
        }

        internal static void Detach(ShelteredMultiplayerEventSyncService service)
        {
            lock (Sync)
            {
                if (object.ReferenceEquals(_service, service))
                    _service = null;
            }
        }

        internal static void RaiseAny(ShelteredNetworkEventContext context)
        {
            Raise(AnyReceived, context);
        }

        internal static void RaiseIntent(ShelteredNetworkEventContext context)
        {
            Raise(IntentReceived, context);
        }

        internal static void RaiseAuthoritative(ShelteredNetworkEventContext context)
        {
            Raise(AuthoritativeReceived, context);
        }

        internal static void RaiseNotification(ShelteredNetworkEventContext context)
        {
            Raise(NotificationReceived, context);
        }

        private static ShelteredMultiplayerEventSyncService CurrentService
        {
            get
            {
                lock (Sync)
                {
                    return _service;
                }
            }
        }

        private static void Raise(Action<ShelteredNetworkEventContext> handler, ShelteredNetworkEventContext context)
        {
            if (handler == null)
                return;

            Delegate[] handlers = handler.GetInvocationList();
            for (int i = 0; i < handlers.Length; i++)
            {
                Action<ShelteredNetworkEventContext> callback = handlers[i] as Action<ShelteredNetworkEventContext>;
                if (callback == null)
                    continue;

                try { callback(context); }
                catch (Exception ex)
                {
                    MMLog.WarnOnce("ShelteredMultiplayerNetworkEvents.Handler",
                        "Network event handler failed: " + ex.Message);
                }
            }
        }
    }
}
