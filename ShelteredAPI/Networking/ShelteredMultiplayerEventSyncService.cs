using System;
using ModAPI.Core;
using ModAPI.Networking;
using ModAPI.Networking.Events;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Sessions;

namespace ShelteredAPI.Networking
{
    internal sealed class ShelteredMultiplayerEventSyncService : IDisposable
    {
        private const string LogComponent = "EventSync";

        private readonly NetworkSession _session;
        private readonly NetworkEventRegistry _registry;
        private readonly NetworkEventDispatcher _dispatcher;
        private readonly EventSyncLogSink _log;
        private bool _disposed;

        internal ShelteredMultiplayerEventSyncService(NetworkSession session, EventSyncLogSink log)
        {
            if (session == null)
                throw new ArgumentNullException("session");

            _session = session;
            _log = log;
            _registry = new NetworkEventRegistry();
            _registry.Register(new ShelteredNetworkGameplayEventSerializer());
            _registry.Register(ShelteredNetworkGameplayEventSerializer.CreateLegacy());
            _dispatcher = new NetworkEventDispatcher(session);
            _dispatcher.EventReceived += OnEventReceived;
            _dispatcher.ParseFailed += OnParseFailed;
            _dispatcher.Start();
            ShelteredMultiplayerNetworkEvents.Attach(this);
        }

        internal delegate void EventSyncLogSink(MMLog.LogLevel level, string component, string message);

        public bool PublishIntent(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            if (_disposed || gameplayEvent == null)
                return false;

            NetworkEventEnvelope envelope = CreateEnvelope(gameplayEvent, NetworkEventPhase.Intent);
            if (_session.Mode == NetworkSessionMode.Client)
                return _dispatcher.SendToHost(envelope, NetworkChannel.Reliable);

            if (_session.Mode == NetworkSessionMode.Host)
            {
                HandleEnvelope(null, envelope);
                return true;
            }

            return false;
        }

        public bool BroadcastAuthoritative(ShelteredNetworkGameplayEvent gameplayEvent)
        {
            if (_disposed || gameplayEvent == null || _session.Mode != NetworkSessionMode.Host)
                return false;

            NetworkEventEnvelope envelope = CreateEnvelope(gameplayEvent, NetworkEventPhase.Authoritative);
            int sent = _dispatcher.Broadcast(envelope, NetworkChannel.Reliable);
            HandleEnvelope(null, envelope);
            return sent > 0 || _session.GetPeers().Length == 0;
        }

        public bool TryHandleMessage(NetworkMessageReceivedEventArgs args)
        {
            return !_disposed && _dispatcher.TryHandleMessage(args);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ShelteredMultiplayerNetworkEvents.Detach(this);
            _dispatcher.EventReceived -= OnEventReceived;
            _dispatcher.ParseFailed -= OnParseFailed;
            _dispatcher.Dispose();
            _disposed = true;
        }

        private NetworkEventEnvelope CreateEnvelope(ShelteredNetworkGameplayEvent gameplayEvent, NetworkEventPhase phase)
        {
            uint worldTick = ResolveWorldTick();
            NetworkEventEnvelope envelope = NetworkEventEnvelope.Create(
                ShelteredNetworkGameplayEvent.EnvelopeEventName,
                ShelteredNetworkGameplayEvent.CurrentVersion,
                phase,
                _session.LocalPeerId,
                worldTick,
                new byte[0]);

            ShelteredNetworkGameplayEvent payloadEvent = StampEventMetadata(
                gameplayEvent,
                envelope.EventId,
                envelope.CorrelationId,
                worldTick);

            byte[] payload = _registry.SerializePayload(
                ShelteredNetworkGameplayEvent.EnvelopeEventName,
                ShelteredNetworkGameplayEvent.CurrentVersion,
                payloadEvent);

            envelope.Payload = payload;
            return envelope;
        }

        private void OnEventReceived(object sender, NetworkEventReceivedEventArgs e)
        {
            if (e == null)
                return;

            HandleEnvelope(e.Peer, e.Envelope);
        }

        private void HandleEnvelope(ModAPI.Networking.Connections.NetworkPeer peer, NetworkEventEnvelope envelope)
        {
            object payload;
            string error;
            if (!_registry.TryDeserializePayload(envelope, out payload, out error))
            {
                WriteLog(MMLog.LogLevel.Warning, "Rejected event envelope: " + error);
                return;
            }

            ShelteredNetworkGameplayEvent gameplayEvent = payload as ShelteredNetworkGameplayEvent;
            if (gameplayEvent == null)
            {
                WriteLog(MMLog.LogLevel.Warning, "Rejected event envelope with unexpected payload type.");
                return;
            }

            ApplyEnvelopeMetadata(gameplayEvent, envelope);
            ShelteredNetworkEventContext context = new ShelteredNetworkEventContext(peer, envelope, gameplayEvent);
            ShelteredMultiplayerNetworkEvents.RaiseAny(context);

            if (envelope.Phase == NetworkEventPhase.Intent)
            {
                ShelteredMultiplayerNetworkEvents.RaiseIntent(context);
                if (_session.Mode == NetworkSessionMode.Host && context.Accepted)
                    BroadcastAcceptedIntent(envelope, context.AcceptedEvent);
                return;
            }

            if (envelope.Phase == NetworkEventPhase.Authoritative)
            {
                ShelteredMultiplayerNetworkEvents.RaiseAuthoritative(context);
                return;
            }

            ShelteredMultiplayerNetworkEvents.RaiseNotification(context);
        }

        private void BroadcastAcceptedIntent(NetworkEventEnvelope intent, ShelteredNetworkGameplayEvent acceptedEvent)
        {
            if (acceptedEvent == null)
                return;

            NetworkEventEnvelope authoritative = NetworkEventEnvelope.Create(
                intent.EventName,
                ShelteredNetworkGameplayEvent.CurrentVersion,
                NetworkEventPhase.Authoritative,
                _session.LocalPeerId,
                ResolveWorldTick(),
                new byte[0]);
            authoritative.CorrelationId = intent.EventId;

            ShelteredNetworkGameplayEvent payloadEvent = StampEventMetadata(
                acceptedEvent,
                authoritative.EventId,
                authoritative.CorrelationId,
                authoritative.WorldTick);

            byte[] payload = _registry.SerializePayload(
                ShelteredNetworkGameplayEvent.EnvelopeEventName,
                ShelteredNetworkGameplayEvent.CurrentVersion,
                payloadEvent);

            authoritative.Payload = payload;

            _dispatcher.Broadcast(authoritative, NetworkChannel.Reliable);
            HandleEnvelope(null, authoritative);
        }

        private static ShelteredNetworkGameplayEvent StampEventMetadata(
            ShelteredNetworkGameplayEvent source,
            string eventId,
            string correlationId,
            uint worldTick)
        {
            ShelteredNetworkGameplayEvent copy = source != null
                ? source.Copy()
                : new ShelteredNetworkGameplayEvent();
            copy.EventId = eventId ?? string.Empty;
            copy.CorrelationId = correlationId ?? string.Empty;
            copy.WorldTick = worldTick;
            return copy;
        }

        private static void ApplyEnvelopeMetadata(ShelteredNetworkGameplayEvent gameplayEvent, NetworkEventEnvelope envelope)
        {
            if (gameplayEvent == null || envelope == null)
                return;

            gameplayEvent.EventId = envelope.EventId ?? string.Empty;
            gameplayEvent.CorrelationId = envelope.CorrelationId ?? string.Empty;
            gameplayEvent.WorldTick = envelope.WorldTick;
        }

        private void OnParseFailed(object sender, NetworkEventParseFailedEventArgs e)
        {
            string message = e != null && e.Exception != null ? e.Exception.Message : "unknown parse error";
            WriteLog(MMLog.LogLevel.Warning, "Failed to parse network event envelope: " + message);
        }

        private uint ResolveWorldTick()
        {
            long tick = ShelteredMultiplayer.Hooks.CurrentWorldTick;
            if (tick < 0)
                return 0;
            if (tick > uint.MaxValue)
                return uint.MaxValue;
            return (uint)tick;
        }

        private void WriteLog(MMLog.LogLevel level, string message)
        {
            if (_log != null)
                _log(level, LogComponent, message ?? string.Empty);
        }
    }
}
