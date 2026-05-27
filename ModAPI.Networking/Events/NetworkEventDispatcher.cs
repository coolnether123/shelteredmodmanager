using System;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Sessions;

namespace ModAPI.Networking.Events
{
    /// <summary>
    /// Sends and receives generic event envelopes over a NetworkSession application message.
    /// </summary>
    public sealed class NetworkEventDispatcher : IDisposable
    {
        private readonly NetworkSession _session;
        private readonly ushort _messageType;
        private bool _attached;
        private bool _disposed;

        public NetworkEventDispatcher(NetworkSession session)
            : this(session, NetworkEventMessageTypes.DefaultEventEnvelope)
        {
        }

        public NetworkEventDispatcher(NetworkSession session, ushort messageType)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            if (SessionMessageTypes.IsReserved(messageType))
                throw new ArgumentOutOfRangeException("messageType", "Event envelope message type must be an application message type.");

            _session = session;
            _messageType = messageType;
        }

        public event EventHandler<NetworkEventReceivedEventArgs> EventReceived;
        public event EventHandler<NetworkEventParseFailedEventArgs> ParseFailed;

        public ushort MessageType
        {
            get { return _messageType; }
        }

        public void Start()
        {
            if (_disposed || _attached)
                return;

            _session.MessageReceived += OnMessageReceived;
            _attached = true;
        }

        public void Stop()
        {
            if (!_attached)
                return;

            _session.MessageReceived -= OnMessageReceived;
            _attached = false;
        }

        public bool SendToHost(NetworkEventEnvelope envelope, NetworkChannel channel)
        {
            if (envelope == null)
                throw new ArgumentNullException("envelope");

            return _session.SendToHost(_messageType, channel, envelope.ToPayload());
        }

        public bool SendToPeer(byte peerId, NetworkEventEnvelope envelope, NetworkChannel channel)
        {
            if (envelope == null)
                throw new ArgumentNullException("envelope");

            return _session.SendToPeer(peerId, _messageType, channel, envelope.ToPayload());
        }

        public int Broadcast(NetworkEventEnvelope envelope, NetworkChannel channel)
        {
            if (envelope == null)
                throw new ArgumentNullException("envelope");

            return _session.Broadcast(_messageType, channel, envelope.ToPayload());
        }

        public bool TryHandleMessage(NetworkMessageReceivedEventArgs args)
        {
            if (args == null || args.MessageType != _messageType)
                return false;

            try
            {
                NetworkEventEnvelope envelope = NetworkEventEnvelope.FromPayload(args.Payload);
                RaiseEventReceived(args.Peer, envelope);
            }
            catch (Exception ex)
            {
                RaiseParseFailed(args, ex);
            }

            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _disposed = true;
        }

        private void OnMessageReceived(object sender, NetworkMessageReceivedEventArgs e)
        {
            TryHandleMessage(e);
        }

        private void RaiseEventReceived(ModAPI.Networking.Connections.NetworkPeer peer, NetworkEventEnvelope envelope)
        {
            EventHandler<NetworkEventReceivedEventArgs> handler = EventReceived;
            if (handler != null)
                handler(this, new NetworkEventReceivedEventArgs(peer, envelope));
        }

        private void RaiseParseFailed(NetworkMessageReceivedEventArgs args, Exception exception)
        {
            EventHandler<NetworkEventParseFailedEventArgs> handler = ParseFailed;
            if (handler != null)
                handler(this, new NetworkEventParseFailedEventArgs(args, exception));
        }
    }

    public sealed class NetworkEventParseFailedEventArgs : EventArgs
    {
        public NetworkEventParseFailedEventArgs(NetworkMessageReceivedEventArgs message, Exception exception)
        {
            Message = message;
            Exception = exception;
        }

        public NetworkMessageReceivedEventArgs Message { get; private set; }
        public Exception Exception { get; private set; }
    }
}
