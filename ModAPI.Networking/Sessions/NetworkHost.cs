using System;
using System.Net;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Diagnostics;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Serialization;

namespace ModAPI.Networking.Sessions
{
    internal sealed class NetworkHost
    {
        private readonly NetworkSession _session;
        private readonly System.Collections.Generic.List<DisconnectedPeerRecord> _disconnectedPeers =
            new System.Collections.Generic.List<DisconnectedPeerRecord>();

        public NetworkHost(NetworkSession session)
        {
            _session = session;
        }

        public void Start(NetworkSessionOptions options)
        {
            _session.Peers.Clear();
            _disconnectedPeers.Clear();
            NetworkDiagnostics.Info("Host session listening for application '" + options.ApplicationId + "'.");
        }

        public void RememberDisconnectedPeer(NetworkPeer peer)
        {
            if (peer == null)
                return;

            string stablePeerId = Normalize(peer.StablePeerId);
            string reconnectToken = Normalize(peer.ReconnectToken);
            if (stablePeerId.Length == 0 && reconnectToken.Length == 0)
                return;

            for (int i = _disconnectedPeers.Count - 1; i >= 0; i--)
            {
                if (_disconnectedPeers[i].PeerId == peer.PeerId
                    || MatchesResumeField(_disconnectedPeers[i].StablePeerId, stablePeerId)
                    || MatchesResumeField(_disconnectedPeers[i].ReconnectToken, reconnectToken))
                {
                    _disconnectedPeers.RemoveAt(i);
                }
            }

            DisconnectedPeerRecord record = new DisconnectedPeerRecord();
            record.PeerId = peer.PeerId;
            record.StablePeerId = stablePeerId;
            record.ReconnectToken = reconnectToken;
            record.SessionNonce = Normalize(peer.SessionNonce);
            _disconnectedPeers.Add(record);

            while (_disconnectedPeers.Count > NetworkDefaults.DefaultMaxPeers * 4)
                _disconnectedPeers.RemoveAt(0);
        }

        public void Pump(DateTime utcNow)
        {
            NetworkPeer[] peers = _session.Peers.GetAll();
            for (int i = 0; i < peers.Length; i++)
            {
                NetworkPeer peer = peers[i];
                if (peer.Connection.IsTimedOut(utcNow, _session.Config.ConnectionTimeoutMilliseconds))
                {
                    peer.SetState(NetworkConnectionState.TimedOut, utcNow);
                    _session.RemovePeer(peer, NetworkDisconnectReason.Timeout, "Peer timed out.");
                    continue;
                }

                if (peer.State == NetworkConnectionState.Connected
                    && (utcNow - peer.Connection.LastSendUtc).TotalMilliseconds >= _session.Config.HeartbeatIntervalMilliseconds)
                {
                    _session.SendHeartbeat(peer);
                }
            }
        }

        public void HandleHandshakeRequest(IPEndPoint remoteEndPoint, NetworkMessage message)
        {
            NetworkHandshakeRequest request;
            try
            {
                BitReader reader = new BitReader(message.Payload, message.Offset, message.Length);
                request = NetworkHandshakeRequest.ReadFrom(ref reader);
            }
            catch (Exception ex)
            {
                SendReject(remoteEndPoint, HandshakeRejectReason.MalformedRequest, "Malformed handshake request.");
                _session.ReportSessionError("Malformed handshake from " + remoteEndPoint, ex, false);
                return;
            }

            HandshakeRejectReason rejectReason;
            string rejectMessage;
            if (!ValidateRequest(request, out rejectReason, out rejectMessage))
            {
                SendReject(remoteEndPoint, rejectReason, rejectMessage);
                return;
            }

            NetworkPeer peer = _session.Peers.FindByEndPoint(remoteEndPoint);
            bool isNewPeer = false;
            bool isResume = false;
            byte previousPeerId = NetworkDefaults.UnassignedPeerId;
            DateTime utcNow = DateTime.UtcNow;
            if (peer == null)
            {
                byte peerId;
                byte maxPeerId = (byte)(_session.Options.MaxPeers - 1);
                DisconnectedPeerRecord resumeRecord;
                if (TryTakeResumeRecord(request, out resumeRecord)
                    && _session.Peers.FindByPeerId(resumeRecord.PeerId) == null)
                {
                    peerId = resumeRecord.PeerId;
                    previousPeerId = resumeRecord.PeerId;
                    isResume = true;
                }
                else if (_session.RemotePeerCount + 1 >= _session.Options.MaxPeers
                    || !_session.Peers.TryAllocatePeerId(1, maxPeerId, out peerId))
                {
                    SendReject(remoteEndPoint, HandshakeRejectReason.ServerFull, "Host is full.");
                    return;
                }

                peer = new NetworkPeer(peerId, remoteEndPoint, false, NetworkConnectionState.Connected);
                _session.Peers.Add(peer);
                isNewPeer = true;
            }

            ApplyRequest(peer, request);
            peer.SetState(NetworkConnectionState.Connected, utcNow);
            SendAccept(peer);

            if (isNewPeer)
            {
                if (isResume)
                    _session.RaiseTransportReconnected(peer, previousPeerId);
                _session.RaisePeerConnected(peer);
            }
        }

        public void HandleDiscoveryRequest(IPEndPoint remoteEndPoint, NetworkMessage message)
        {
            if (!_session.Config.EnableBroadcastDiscovery)
                return;

            NetworkDiscoveryRequest request;
            try
            {
                BitReader reader = new BitReader(message.Payload, message.Offset, message.Length);
                request = NetworkDiscoveryRequest.ReadFrom(ref reader);
            }
            catch
            {
                return;
            }

            if (request.ProtocolVersion != NetworkDefaults.ProtocolVersion)
                return;
            if (!string.Equals(Normalize(request.ApplicationId), _session.Options.ApplicationId, StringComparison.Ordinal))
                return;
            if (!MatchesOptional(request.SessionId, _session.Options.SessionId, false))
                return;

            SendDiscoveryResponse(remoteEndPoint);
        }

        private bool ValidateRequest(NetworkHandshakeRequest request, out HandshakeRejectReason reason, out string message)
        {
            NetworkSessionOptions options = _session.Options;
            if (request.ProtocolVersion != NetworkDefaults.ProtocolVersion)
            {
                reason = HandshakeRejectReason.ProtocolMismatch;
                message = "Protocol version mismatch.";
                return false;
            }

            if (!string.Equals(Normalize(request.ApplicationId), options.ApplicationId, StringComparison.Ordinal))
            {
                reason = HandshakeRejectReason.ApplicationMismatch;
                message = "Application id mismatch.";
                return false;
            }

            if (!MatchesOptional(options.SessionId, request.SessionId, false))
            {
                reason = HandshakeRejectReason.SessionMismatch;
                message = "Session id mismatch.";
                return false;
            }

            if (!MatchesOptional(options.SessionNonce, request.SessionNonce, true))
            {
                reason = HandshakeRejectReason.SessionMismatch;
                message = "Session nonce mismatch.";
                return false;
            }

            if (!MatchesOptional(options.ContentSchemaHash, request.ContentSchemaHash, false))
            {
                reason = HandshakeRejectReason.ContentSchemaMismatch;
                message = "Content schema hash mismatch.";
                return false;
            }

            if (!MatchesOptional(options.ModContentHash, request.ModContentHash, false))
            {
                reason = HandshakeRejectReason.ModContentMismatch;
                message = "Mod/content hash mismatch.";
                return false;
            }

            reason = HandshakeRejectReason.None;
            message = string.Empty;
            return true;
        }

        private void ApplyRequest(NetworkPeer peer, NetworkHandshakeRequest request)
        {
            peer.ApplicationId = Normalize(request.ApplicationId);
            peer.SessionId = Normalize(request.SessionId);
            peer.SessionNonce = Normalize(_session.Options.SessionNonce);
            peer.ContentSchemaHash = Normalize(request.ContentSchemaHash);
            peer.ModContentHash = Normalize(request.ModContentHash);
            peer.DisplayName = Normalize(request.DisplayName);
            peer.StablePeerId = Normalize(request.StablePeerId);
            peer.ReconnectToken = Normalize(request.ReconnectToken);
            peer.LastError = string.Empty;
        }

        private void SendAccept(NetworkPeer peer)
        {
            NetworkHandshakeAccept accept = new NetworkHandshakeAccept();
            accept.AssignedPeerId = peer.PeerId;
            accept.MaxPeers = _session.Options.MaxPeers;
            accept.CurrentPeerCount = _session.RemotePeerCount + 1;
            accept.ApplicationId = _session.Options.ApplicationId;
            accept.SessionId = _session.Options.SessionId;
            accept.SessionNonce = _session.Options.SessionNonce;
            accept.ContentSchemaHash = _session.Options.ContentSchemaHash;
            accept.ModContentHash = _session.Options.ModContentHash;
            accept.HostDisplayName = _session.Options.DisplayName;
            accept.HostStablePeerId = _session.Options.StablePeerId;
            accept.ReconnectToken = _session.Options.ReconnectToken;

            _session.SendBuiltIn(peer, SessionMessageTypes.HandshakeAccept,
                _session.CreatePayload(accept.WriteTo), PacketFlags.IsHandshake);
        }

        private bool TryTakeResumeRecord(NetworkHandshakeRequest request, out DisconnectedPeerRecord record)
        {
            string stablePeerId = Normalize(request.StablePeerId);
            string reconnectToken = Normalize(request.ReconnectToken);
            string sessionNonce = Normalize(request.SessionNonce);

            for (int i = _disconnectedPeers.Count - 1; i >= 0; i--)
            {
                DisconnectedPeerRecord candidate = _disconnectedPeers[i];
                if (sessionNonce.Length > 0 && candidate.SessionNonce.Length > 0
                    && !string.Equals(sessionNonce, candidate.SessionNonce, StringComparison.Ordinal))
                    continue;

                if (MatchesResumeField(candidate.StablePeerId, stablePeerId)
                    || MatchesResumeField(candidate.ReconnectToken, reconnectToken))
                {
                    _disconnectedPeers.RemoveAt(i);
                    record = candidate;
                    return true;
                }
            }

            record = null;
            return false;
        }

        private void SendReject(IPEndPoint remoteEndPoint, HandshakeRejectReason reason, string message)
        {
            NetworkHandshakeReject reject = new NetworkHandshakeReject();
            reject.Reason = reason;
            reject.Message = message ?? string.Empty;
            _session.SendBuiltIn(remoteEndPoint, SessionMessageTypes.HandshakeReject,
                _session.CreatePayload(reject.WriteTo), PacketFlags.IsHandshake);
            NetworkDiagnostics.Warn("Rejected handshake from " + remoteEndPoint + ": " + reason + " " + reject.Message);
        }

        private void SendDiscoveryResponse(IPEndPoint remoteEndPoint)
        {
            NetworkDiscoveryResponse response = new NetworkDiscoveryResponse();
            response.ApplicationId = _session.Options.ApplicationId;
            response.SessionId = _session.Options.SessionId;
            response.PeerCount = _session.RemotePeerCount + 1;
            response.MaxPeers = _session.Options.MaxPeers;
            response.DisplayName = _session.Options.DisplayName;

            _session.SendBuiltIn(remoteEndPoint, SessionMessageTypes.DiscoveryResponse,
                _session.CreatePayload(response.WriteTo), PacketFlags.None);
        }

        private static bool MatchesOptional(string expected, string actual, bool allowEmptyActual)
        {
            expected = Normalize(expected);
            actual = Normalize(actual);
            if (expected.Length == 0)
                return true;
            if (actual.Length == 0 && allowEmptyActual)
                return true;
            return string.Equals(expected, actual, StringComparison.Ordinal);
        }

        private static string Normalize(string value)
        {
            return value ?? string.Empty;
        }

        private static bool MatchesResumeField(string expected, string actual)
        {
            return expected != null
                && expected.Length > 0
                && actual != null
                && actual.Length > 0
                && string.Equals(expected, actual, StringComparison.Ordinal);
        }

        private sealed class DisconnectedPeerRecord
        {
            public byte PeerId;
            public string StablePeerId;
            public string ReconnectToken;
            public string SessionNonce;
        }
    }
}
