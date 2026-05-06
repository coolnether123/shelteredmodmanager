using System;
using System.Net;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Diagnostics;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Serialization;

namespace ModAPI.Networking.Sessions
{
    internal sealed class NetworkClient
    {
        private readonly NetworkSession _session;
        private NetworkPeer _hostPeer;
        private DateTime _startedUtc;
        private DateTime _lastHandshakeUtc;

        public NetworkClient(NetworkSession session)
        {
            _session = session;
        }

        public void Start(IPEndPoint hostEndPoint, NetworkSessionOptions options)
        {
            _session.Peers.Clear();
            _hostPeer = new NetworkPeer(NetworkDefaults.HostPeerId, hostEndPoint, true, NetworkConnectionState.Connecting);
            _hostPeer.ApplicationId = options.ApplicationId;
            _hostPeer.SessionId = options.SessionId;
            _hostPeer.ContentSchemaHash = options.ContentSchemaHash;
            _hostPeer.ModContentHash = options.ModContentHash;
            _session.Peers.Add(_hostPeer);
            _startedUtc = DateTime.UtcNow;
            _lastHandshakeUtc = DateTime.MinValue;
            SendHandshake();
        }

        public void Pump(DateTime utcNow)
        {
            if (_hostPeer == null)
                return;

            if (_session.State == NetworkSessionState.Connecting)
            {
                if ((utcNow - _startedUtc).TotalMilliseconds >= _session.Config.HandshakeTimeoutMilliseconds)
                {
                    Fail(HandshakeRejectReason.Unknown, "Handshake timed out.", null);
                    return;
                }

                if ((utcNow - _lastHandshakeUtc).TotalMilliseconds >= _session.Config.HandshakeRetryMilliseconds)
                    SendHandshake();
            }

            if (_hostPeer.State == NetworkConnectionState.Connected)
            {
                if (_hostPeer.Connection.IsTimedOut(utcNow, _session.Config.ConnectionTimeoutMilliseconds))
                {
                    _hostPeer.SetState(NetworkConnectionState.TimedOut, utcNow);
                    _session.RaisePeerDisconnected(_hostPeer, NetworkDisconnectReason.Timeout, "Host timed out.");
                    Fail(HandshakeRejectReason.Unknown, "Host timed out.", null);
                    return;
                }

                if ((utcNow - _hostPeer.Connection.LastSendUtc).TotalMilliseconds >= _session.Config.HeartbeatIntervalMilliseconds)
                    _session.SendHeartbeat(_hostPeer);
            }
        }

        public void HandleAccept(NetworkMessage message)
        {
            NetworkHandshakeAccept accept;
            try
            {
                BitReader reader = new BitReader(message.Payload, message.Offset, message.Length);
                accept = NetworkHandshakeAccept.ReadFrom(ref reader);
            }
            catch (Exception ex)
            {
                Fail(HandshakeRejectReason.MalformedRequest, "Malformed handshake accept.", ex);
                return;
            }

            HandshakeRejectReason reason;
            string failure;
            if (!ValidateAccept(accept, out reason, out failure))
            {
                Fail(reason, failure, null);
                return;
            }

            _session.LocalPeerId = accept.AssignedPeerId;
            _hostPeer.DisplayName = accept.HostDisplayName ?? string.Empty;
            _hostPeer.ApplicationId = accept.ApplicationId ?? string.Empty;
            _hostPeer.SessionId = accept.SessionId ?? string.Empty;
            _hostPeer.ContentSchemaHash = accept.ContentSchemaHash ?? string.Empty;
            _hostPeer.ModContentHash = accept.ModContentHash ?? string.Empty;
            _hostPeer.SetState(NetworkConnectionState.Connected, DateTime.UtcNow);
            _session.ChangeState(NetworkSessionState.Connected);
            NetworkDiagnostics.Info("Connected to host " + _hostPeer.EndPoint + " as peer " + _session.LocalPeerId + ".");
            _session.RaisePeerConnected(_hostPeer);
        }

        public void HandleReject(NetworkMessage message)
        {
            NetworkHandshakeReject reject;
            try
            {
                BitReader reader = new BitReader(message.Payload, message.Offset, message.Length);
                reject = NetworkHandshakeReject.ReadFrom(ref reader);
            }
            catch (Exception ex)
            {
                Fail(HandshakeRejectReason.MalformedRequest, "Malformed handshake reject.", ex);
                return;
            }

            Fail(reject.Reason, reject.Message, null);
        }

        public void SendDisconnect(NetworkDisconnectReason reason, string message)
        {
            if (_hostPeer == null)
                return;
            _session.SendDisconnect(_hostPeer, reason, message);
        }

        private void SendHandshake()
        {
            if (_hostPeer == null)
                return;

            NetworkSessionOptions options = _session.Options;
            NetworkHandshakeRequest request = new NetworkHandshakeRequest();
            request.ApplicationId = options.ApplicationId;
            request.SessionId = options.SessionId;
            request.ContentSchemaHash = options.ContentSchemaHash;
            request.ModContentHash = options.ModContentHash;
            request.DisplayName = options.DisplayName;
            request.ReconnectToken = options.ReconnectToken;
            _session.SendBuiltIn(_hostPeer, SessionMessageTypes.HandshakeRequest,
                _session.CreatePayload(request.WriteTo), PacketFlags.IsHandshake);
            _lastHandshakeUtc = DateTime.UtcNow;
        }

        private bool ValidateAccept(NetworkHandshakeAccept accept, out HandshakeRejectReason reason, out string message)
        {
            if (accept.ProtocolVersion != NetworkDefaults.ProtocolVersion)
            {
                reason = HandshakeRejectReason.ProtocolMismatch;
                message = "Protocol version mismatch.";
                return false;
            }

            if (accept.AssignedPeerId == NetworkDefaults.HostPeerId || accept.AssignedPeerId == NetworkDefaults.UnassignedPeerId)
            {
                reason = HandshakeRejectReason.MalformedRequest;
                message = "Host assigned an invalid peer id.";
                return false;
            }

            if (!string.Equals(accept.ApplicationId ?? string.Empty, _session.Options.ApplicationId, StringComparison.Ordinal))
            {
                reason = HandshakeRejectReason.ApplicationMismatch;
                message = "Application id mismatch.";
                return false;
            }

            if (!MatchesOptional(_session.Options.SessionId, accept.SessionId, false))
            {
                reason = HandshakeRejectReason.SessionMismatch;
                message = "Session id mismatch.";
                return false;
            }

            if (!MatchesOptional(_session.Options.ContentSchemaHash, accept.ContentSchemaHash, false))
            {
                reason = HandshakeRejectReason.ContentSchemaMismatch;
                message = "Content schema hash mismatch.";
                return false;
            }

            if (!MatchesOptional(_session.Options.ModContentHash, accept.ModContentHash, false))
            {
                reason = HandshakeRejectReason.ModContentMismatch;
                message = "Mod/content hash mismatch.";
                return false;
            }

            reason = HandshakeRejectReason.None;
            message = string.Empty;
            return true;
        }

        private void Fail(HandshakeRejectReason reason, string message, Exception exception)
        {
            if (_hostPeer != null)
            {
                _hostPeer.LastError = message ?? string.Empty;
                _hostPeer.SetState(NetworkConnectionState.Disconnected, DateTime.UtcNow);
            }

            _session.ChangeState(NetworkSessionState.Failed);
            NetworkDiagnostics.Warn("Connection failed: " + reason + " " + (message ?? string.Empty));
            _session.RaiseConnectionFailed(_hostPeer, reason, message, exception);
        }

        private static bool MatchesOptional(string expected, string actual, bool allowEmptyActual)
        {
            expected = expected ?? string.Empty;
            actual = actual ?? string.Empty;
            if (expected.Length == 0)
                return true;
            if (actual.Length == 0 && allowEmptyActual)
                return true;
            return string.Equals(expected, actual, StringComparison.Ordinal);
        }
    }
}
