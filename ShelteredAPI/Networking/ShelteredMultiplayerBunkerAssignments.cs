using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Networking;
using ShelteredAPI.Bunkers;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class ShelteredMultiplayerBunkerAssignments : IShelteredMultiplayerSessionLifecycleHandler
    {
        private const string PlayerIdSeedScope = "ShelteredAPI.Multiplayer.PlayerIds:";
        private const string BunkerSeedScope = "ShelteredAPI.Multiplayer.Bunkers:";
        private const string LogSource = "ShelteredAPI.Multiplayer.Bunkers";
        private const float FallbackWorldWidth = 813f;
        private const float FallbackWorldHeight = 327f;
        private const float PlacementMarginScale = 0.35f;

        private static readonly ShelteredMultiplayerBunkerAssignments _instance =
            new ShelteredMultiplayerBunkerAssignments();

        public static ShelteredMultiplayerBunkerAssignments Instance
        {
            get { return _instance; }
        }

        public void Handle(ShelteredMultiplayerLifecycleEvent lifecycleEvent)
        {
            if (lifecycleEvent == null || lifecycleEvent.Context == null)
                return;

            if (lifecycleEvent.Kind == ShelteredMultiplayerLifecycleEventKind.SetupPreparing
                || lifecycleEvent.Kind == ShelteredMultiplayerLifecycleEventKind.RosterChanged)
            {
                HandleHostAssignment(lifecycleEvent.Context, lifecycleEvent.Reason);
                return;
            }

            if (lifecycleEvent.Kind == ShelteredMultiplayerLifecycleEventKind.SetupReceived
                || lifecycleEvent.Kind == ShelteredMultiplayerLifecycleEventKind.LocalWorldLoaded
                || lifecycleEvent.Kind == ShelteredMultiplayerLifecycleEventKind.WorldStartReleased)
            {
                Apply(lifecycleEvent.Context, lifecycleEvent.Reason);
            }
        }

        public static ShelteredMultiplayerBunkerAssignmentSnapshot CreateForHost(
            ShelteredMultiplayerSessionContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            ShelteredMultiplayerBunkerAssignmentSnapshot snapshot =
                new ShelteredMultiplayerBunkerAssignmentSnapshot(context.SessionId);

            List<Participant> peers = new List<Participant>();
            peers.Add(CreateHostParticipant(context.LocalStablePeerId));

            for (int i = 0; i < context.Roster.Length; i++)
            {
                ShelteredMultiplayerPeerInfo peer = context.Roster[i];
                if (peer != null && !peer.IsHost && peer.IsConnected)
                    peers.Add(CreateRemoteParticipant(context.SessionId, peer));
            }

            List<Participant> remotePeers = new List<Participant>();
            for (int i = 0; i < peers.Count; i++)
            {
                if (!peers[i].IsHost)
                    remotePeers.Add(peers[i]);
            }

            remotePeers.Sort(CompareParticipants);

            List<Participant> ordered = new List<Participant>();
            ordered.Add(peers[0]);
            ordered.AddRange(remotePeers);

            List<Vector2> positions = CalculatePositions(context.SessionId, ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                Participant participant = ordered[i];
                int playerId = i + 1;
                Vector2 position = positions[i];
                snapshot.Records.Add(new ShelteredMultiplayerBunkerAssignmentRecord(
                    participant.NetworkPeerId,
                    playerId,
                    position,
                    ResolveDisplayName(playerId, participant.DisplayName),
                    true));
            }

            return snapshot;
        }

        public static ShelteredMultiplayerBunkerAssignmentRecord[] FromBunkers(
            string sessionId,
            IList<BunkerDefinition> bunkers)
        {
            List<ShelteredMultiplayerBunkerAssignmentRecord> records =
                new List<ShelteredMultiplayerBunkerAssignmentRecord>();

            if (bunkers != null)
            {
                for (int i = 0; i < bunkers.Count; i++)
                {
                    BunkerDefinition bunker = bunkers[i];
                    if (bunker == null)
                        continue;

                    records.Add(new ShelteredMultiplayerBunkerAssignmentRecord(
                        NetworkDefaults.UnassignedPeerId,
                        bunker.Id,
                        bunker.Position,
                        bunker.DisplayName,
                        bunker.IsOnline));
                }
            }

            return records.ToArray();
        }

        public static List<BunkerDefinition> ToBunkers(ShelteredMultiplayerBunkerAssignmentRecord[] assignments)
        {
            List<BunkerDefinition> definitions = new List<BunkerDefinition>();
            if (assignments == null)
                return definitions;

            for (int i = 0; i < assignments.Length; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord record = assignments[i];
                definitions.Add(new BunkerDefinition(
                    record.PlayerId,
                    record.Position,
                    record.DisplayName,
                    true,
                    record.IsOnline));
            }

            return definitions;
        }

        public static void Apply(ShelteredMultiplayerBunkerAssignmentSnapshot snapshot, int localPlayerId, string reason)
        {
            if (snapshot == null || snapshot.Records.Count == 0)
                return;

            Apply(snapshot.Records.ToArray(), localPlayerId, reason);
        }

        public static void Apply(ShelteredMultiplayerSessionContext context, string reason)
        {
            if (context == null)
                return;

            Apply(context.BunkerAssignments, context.LocalPlayerId, reason);
        }

        public static void Apply(ShelteredMultiplayerBunkerAssignmentRecord[] assignments, int localPlayerId, string reason)
        {
            if (assignments == null || assignments.Length == 0)
                return;

            List<BunkerDefinition> definitions = ToBunkers(assignments);
            ShelteredBunkers.Service.LoadDefinitions(definitions);
            ShelteredBunkers.SetActivePlayerId(localPlayerId > 0 ? localPlayerId : 1);

            MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                "Applied " + definitions.Count + " multiplayer bunker assignment(s). LocalPlayerId="
                + localPlayerId + ", Reason=" + (reason ?? string.Empty) + ".");
        }

        private static void HandleHostAssignment(ShelteredMultiplayerSessionContext context, string reason)
        {
            if (context.Mode != ShelteredMultiplayerSessionMode.Host || string.IsNullOrEmpty(context.SessionId))
                return;

            ShelteredMultiplayerBunkerAssignmentSnapshot snapshot = CreateForHost(context);
            ShelteredMultiplayerSessionCoordinator.Instance.SetBunkerAssignments(
                snapshot.Records.ToArray(),
                snapshot.GetPlayerIdForNetworkPeer(NetworkDefaults.HostPeerId),
                reason);
            Apply(snapshot, 1, reason);
        }

        private static Participant CreateHostParticipant(string hostStablePeerId)
        {
            string key = Normalize(hostStablePeerId);
            if (key.Length == 0)
                key = "host";

            return new Participant(NetworkDefaults.HostPeerId, true, 0, key, "Host");
        }

        private static Participant CreateRemoteParticipant(string sessionId, ShelteredMultiplayerPeerInfo peer)
        {
            string key = Normalize(peer.StablePeerId);
            if (key.Length == 0)
                key = Normalize(peer.DisplayName);
            if (key.Length == 0)
                key = "peer-" + peer.NetworkPeerId;

            int sortKey = ModRandom.DeriveStableSeed(PlayerIdSeedScope + Normalize(sessionId) + ":" + key);
            return new Participant(peer.NetworkPeerId, false, sortKey, key + ":" + peer.NetworkPeerId, peer.DisplayName);
        }

        private static int CompareParticipants(Participant left, Participant right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int sortCompare = left.SortKey.CompareTo(right.SortKey);
            if (sortCompare != 0)
                return sortCompare;

            return string.Compare(left.StableKey, right.StableKey, StringComparison.Ordinal);
        }

        private static List<Vector2> CalculatePositions(string sessionId, int playerCount)
        {
            List<Vector2> positions = new List<Vector2>();
            if (playerCount <= 0)
                return positions;

            float width;
            float height;
            GetWorldSize(out width, out height);

            float marginX = width * PlacementMarginScale;
            float marginY = height * PlacementMarginScale;
            ModRandomStream stream = new ModRandomStream(ModRandom.DeriveStableSeed(
                BunkerSeedScope + Normalize(sessionId) + ":" + playerCount));

            List<Vector2> candidates = new List<Vector2>
            {
                new Vector2(-marginX, marginY),
                new Vector2(marginX, marginY),
                new Vector2(-marginX, -marginY),
                new Vector2(marginX, -marginY)
            };
            stream.Shuffle(candidates);

            float minDistance = width * 0.25f;
            for (int i = 0; i < playerCount; i++)
            {
                Vector2 position;
                if (TryTakeCorner(candidates, positions, minDistance, out position)
                    || TryFindRandomPosition(stream, positions, marginX, marginY, minDistance, out position)
                    || TryFindRandomPosition(stream, positions, marginX, marginY, 10f, out position))
                {
                    positions.Add(position);
                    continue;
                }

                positions.Add(new Vector2(stream.Range(-marginX, marginX), stream.Range(-marginY, marginY)));
            }

            return positions;
        }

        private static bool TryTakeCorner(List<Vector2> candidates, List<Vector2> existing, float minDistance, out Vector2 position)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2 candidate = candidates[i];
                if (IsClear(candidate, existing, minDistance))
                {
                    candidates.RemoveAt(i);
                    position = candidate;
                    return true;
                }
            }

            position = Vector2.zero;
            return false;
        }

        private static bool TryFindRandomPosition(
            ModRandomStream stream,
            List<Vector2> existing,
            float marginX,
            float marginY,
            float minDistance,
            out Vector2 position)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector2 candidate = new Vector2(stream.Range(-marginX, marginX), stream.Range(-marginY, marginY));
                if (IsClear(candidate, existing, minDistance))
                {
                    position = candidate;
                    return true;
                }
            }

            position = Vector2.zero;
            return false;
        }

        private static bool IsClear(Vector2 candidate, List<Vector2> existing, float minDistance)
        {
            for (int i = 0; i < existing.Count; i++)
            {
                if (Vector2.Distance(candidate, existing[i]) < minDistance)
                    return false;
            }

            return true;
        }

        private static void GetWorldSize(out float width, out float height)
        {
            width = FallbackWorldWidth;
            height = FallbackWorldHeight;

            ExplorationManager manager = ExplorationManager.Instance;
            if (manager == null)
                return;

            if (manager.worldWidth > 0f)
                width = manager.worldWidth;
            if (manager.worldHeight > 0f)
                height = manager.worldHeight;
        }

        private static string ResolveDisplayName(int playerId, string displayName)
        {
            if (!string.IsNullOrEmpty(displayName))
                return displayName;

            return "Player " + playerId;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private sealed class Participant
        {
            public Participant(byte networkPeerId, bool isHost, int sortKey, string stableKey, string displayName)
            {
                NetworkPeerId = networkPeerId;
                IsHost = isHost;
                SortKey = sortKey;
                StableKey = stableKey ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
            }

            public readonly byte NetworkPeerId;
            public readonly bool IsHost;
            public readonly int SortKey;
            public readonly string StableKey;
            public readonly string DisplayName;
        }
    }

    internal sealed class ShelteredMultiplayerBunkerAssignmentSnapshot
    {
        public ShelteredMultiplayerBunkerAssignmentSnapshot(string sessionId)
        {
            SessionId = sessionId ?? string.Empty;
            Records = new List<ShelteredMultiplayerBunkerAssignmentRecord>();
        }

        public readonly string SessionId;
        public readonly List<ShelteredMultiplayerBunkerAssignmentRecord> Records;

        public int PlayerCount
        {
            get { return Records.Count; }
        }

        public int GetPlayerIdForNetworkPeer(byte networkPeerId)
        {
            for (int i = 0; i < Records.Count; i++)
            {
                if (Records[i].NetworkPeerId == networkPeerId)
                    return Records[i].PlayerId;
            }

            return networkPeerId == NetworkDefaults.HostPeerId ? 1 : 0;
        }
    }

    internal sealed class ShelteredMultiplayerBunkerAssignmentRecord
    {
        public ShelteredMultiplayerBunkerAssignmentRecord(
            byte networkPeerId,
            int playerId,
            Vector2 position,
            string displayName,
            bool isOnline)
        {
            NetworkPeerId = networkPeerId;
            PlayerId = playerId;
            Position = position;
            DisplayName = displayName ?? string.Empty;
            IsOnline = isOnline;
        }

        public readonly byte NetworkPeerId;
        public readonly int PlayerId;
        public readonly Vector2 Position;
        public readonly string DisplayName;
        public readonly bool IsOnline;
    }
}
