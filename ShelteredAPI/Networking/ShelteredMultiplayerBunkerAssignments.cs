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

        private ShelteredMultiplayerBunkerAssignments()
        {
            ShelteredMultiplayerNetworkEvents.AuthoritativeReceived += OnAuthoritativeBunkerEvent;
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

            CopyExistingAssignments(context, snapshot);

            List<Participant> remotePeers = new List<Participant>();
            for (int i = 0; i < context.Roster.Length; i++)
            {
                ShelteredMultiplayerPeerInfo peer = context.Roster[i];
                if (peer != null && !peer.IsHost && peer.IsConnected)
                    remotePeers.Add(CreateRemoteParticipant(context.SessionId, peer));
            }

            remotePeers.Sort(CompareParticipants);

            List<Vector2> positions = CalculatePositions(context.SessionId, NetworkDefaults.DefaultMaxPeers);
            EnsureHostAssignment(context, snapshot, positions);
            for (int i = 0; i < remotePeers.Count; i++)
            {
                EnsureRemoteAssignment(snapshot, remotePeers[i], positions);
            }

            RefreshOnlineState(context, snapshot);
            snapshot.Records.Sort(CompareRecords);
            return snapshot;
        }

        private static void CopyExistingAssignments(
            ShelteredMultiplayerSessionContext context,
            ShelteredMultiplayerBunkerAssignmentSnapshot snapshot)
        {
            if (context == null || context.BunkerAssignments == null || snapshot == null)
                return;

            for (int i = 0; i < context.BunkerAssignments.Length; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord record = context.BunkerAssignments[i];
                if (record == null || FindByBunkerOwnerId(snapshot, record.BunkerOwnerId) != null)
                    continue;

                snapshot.Records.Add(record);
            }
        }

        private static void EnsureHostAssignment(
            ShelteredMultiplayerSessionContext context,
            ShelteredMultiplayerBunkerAssignmentSnapshot snapshot,
            List<Vector2> positions)
        {
            ShelteredMultiplayerBunkerAssignmentRecord existing = FindByBunkerOwnerId(snapshot, 0);
            Vector2 position = existing != null ? existing.Position : GetPositionForBunkerOwner(positions, 0);
            string displayName = existing != null ? existing.DisplayName : "Host";

            Upsert(snapshot, new ShelteredMultiplayerBunkerAssignmentRecord(
                NetworkDefaults.HostPeerId,
                1,
                0,
                position,
                ResolveDisplayName(1, displayName),
                true));
        }

        private static void EnsureRemoteAssignment(
            ShelteredMultiplayerBunkerAssignmentSnapshot snapshot,
            Participant participant,
            List<Vector2> positions)
        {
            if (snapshot == null || participant == null)
                return;

            ShelteredMultiplayerBunkerAssignmentRecord existing = FindByNetworkPeerId(snapshot, participant.NetworkPeerId);
            if (existing != null)
            {
                Upsert(snapshot, new ShelteredMultiplayerBunkerAssignmentRecord(
                    existing.NetworkPeerId,
                    existing.PlayerId,
                    existing.BunkerOwnerId,
                    existing.Position,
                    ResolveDisplayName(existing.PlayerId, participant.DisplayName.Length > 0 ? participant.DisplayName : existing.DisplayName),
                    true));
                return;
            }

            int playerId = NextPlayerId(snapshot);
            int bunkerOwnerId = NextBunkerOwnerId(snapshot);
            Upsert(snapshot, new ShelteredMultiplayerBunkerAssignmentRecord(
                participant.NetworkPeerId,
                playerId,
                bunkerOwnerId,
                GetPositionForBunkerOwner(positions, bunkerOwnerId),
                ResolveDisplayName(playerId, participant.DisplayName),
                true));
        }

        private static void RefreshOnlineState(
            ShelteredMultiplayerSessionContext context,
            ShelteredMultiplayerBunkerAssignmentSnapshot snapshot)
        {
            if (context == null || snapshot == null)
                return;

            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord record = snapshot.Records[i];
                bool online = record.BunkerOwnerId == 0 || IsPeerConnected(context, record.NetworkPeerId);
                if (record.IsOnline == online)
                    continue;

                snapshot.Records[i] = new ShelteredMultiplayerBunkerAssignmentRecord(
                    record.NetworkPeerId,
                    record.PlayerId,
                    record.BunkerOwnerId,
                    record.Position,
                    record.DisplayName,
                    online);
            }
        }

        private static bool IsPeerConnected(ShelteredMultiplayerSessionContext context, byte networkPeerId)
        {
            if (networkPeerId == NetworkDefaults.HostPeerId)
                return true;

            for (int i = 0; i < context.Roster.Length; i++)
            {
                ShelteredMultiplayerPeerInfo peer = context.Roster[i];
                if (peer != null && peer.NetworkPeerId == networkPeerId && peer.IsConnected)
                    return true;
            }

            return false;
        }

        private static void Upsert(
            ShelteredMultiplayerBunkerAssignmentSnapshot snapshot,
            ShelteredMultiplayerBunkerAssignmentRecord record)
        {
            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord existing = snapshot.Records[i];
                if (existing.BunkerOwnerId == record.BunkerOwnerId
                    || existing.NetworkPeerId == record.NetworkPeerId)
                {
                    snapshot.Records[i] = record;
                    return;
                }
            }

            snapshot.Records.Add(record);
        }

        private static ShelteredMultiplayerBunkerAssignmentRecord FindByNetworkPeerId(
            ShelteredMultiplayerBunkerAssignmentSnapshot snapshot,
            byte networkPeerId)
        {
            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                if (snapshot.Records[i].NetworkPeerId == networkPeerId)
                    return snapshot.Records[i];
            }

            return null;
        }

        private static ShelteredMultiplayerBunkerAssignmentRecord FindByBunkerOwnerId(
            ShelteredMultiplayerBunkerAssignmentSnapshot snapshot,
            int bunkerOwnerId)
        {
            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                if (snapshot.Records[i].BunkerOwnerId == bunkerOwnerId)
                    return snapshot.Records[i];
            }

            return null;
        }

        private static int NextPlayerId(ShelteredMultiplayerBunkerAssignmentSnapshot snapshot)
        {
            int candidate = 1;
            bool used;
            do
            {
                used = false;
                for (int i = 0; i < snapshot.Records.Count; i++)
                {
                    if (snapshot.Records[i].PlayerId == candidate)
                    {
                        used = true;
                        candidate++;
                        break;
                    }
                }
            }
            while (used);

            return candidate;
        }

        private static int NextBunkerOwnerId(ShelteredMultiplayerBunkerAssignmentSnapshot snapshot)
        {
            int candidate = 0;
            bool used;
            do
            {
                used = false;
                for (int i = 0; i < snapshot.Records.Count; i++)
                {
                    if (snapshot.Records[i].BunkerOwnerId == candidate)
                    {
                        used = true;
                        candidate++;
                        break;
                    }
                }
            }
            while (used);

            return candidate;
        }

        private static Vector2 GetPositionForBunkerOwner(List<Vector2> positions, int bunkerOwnerId)
        {
            if (positions != null && bunkerOwnerId >= 0 && bunkerOwnerId < positions.Count)
                return positions[bunkerOwnerId];

            return ShelteredBunkers.Service.CalculateSecondaryPosition();
        }

        private static int CompareRecords(
            ShelteredMultiplayerBunkerAssignmentRecord left,
            ShelteredMultiplayerBunkerAssignmentRecord right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return left.BunkerOwnerId.CompareTo(right.BunkerOwnerId);
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
                        bunker.PeerId,
                        bunker.Id + 1,
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
                    record.BunkerOwnerId,
                    record.Position,
                    record.DisplayName,
                    true,
                    record.IsOnline,
                    record.NetworkPeerId));
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
            ShelteredBunkers.SetActivePlayerId(ResolveBunkerOwnerId(assignments, localPlayerId));
            ShelteredMultiplayerMapSeedRuntime.CacheActiveBunkerPosition(reason);

            MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                "Applied " + definitions.Count + " multiplayer bunker assignment(s). LocalPlayerId="
                + localPlayerId + ", Reason=" + (reason ?? string.Empty) + ".");
        }

        internal static int ResolveBunkerOwnerId(ShelteredMultiplayerBunkerAssignmentRecord[] assignments, int localPlayerId)
        {
            if (assignments != null)
            {
                for (int i = 0; i < assignments.Length; i++)
                {
                    ShelteredMultiplayerBunkerAssignmentRecord record = assignments[i];
                    if (record != null && record.PlayerId == localPlayerId)
                        return record.BunkerOwnerId;
                }
            }

            return localPlayerId > 0 ? localPlayerId - 1 : 0;
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
            BroadcastBunkerAssignmentChanges(context.BunkerAssignments, snapshot.Records.ToArray());
        }

        private static void BroadcastBunkerAssignmentChanges(
            ShelteredMultiplayerBunkerAssignmentRecord[] previous,
            ShelteredMultiplayerBunkerAssignmentRecord[] current)
        {
            if (current == null || current.Length == 0 || !ShelteredMultiplayerNetworkEvents.IsAvailable)
                return;

            for (int i = 0; i < current.Length; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord record = current[i];
                if (record == null)
                    continue;

                ShelteredMultiplayerBunkerAssignmentRecord oldRecord = FindByBunkerOwnerId(previous, record.BunkerOwnerId);
                string kind = ResolveBunkerEventKind(oldRecord, record);
                if (kind.Length == 0)
                    continue;

                ShelteredMultiplayerNetworkEvents.BroadcastAuthoritative(CreateBunkerEvent(kind, record));
            }
        }

        private static string ResolveBunkerEventKind(
            ShelteredMultiplayerBunkerAssignmentRecord previous,
            ShelteredMultiplayerBunkerAssignmentRecord current)
        {
            if (previous == null)
                return ShelteredNetworkEventKinds.BunkerRegistered;

            if (previous.IsOnline != current.IsOnline)
                return ShelteredNetworkEventKinds.BunkerOnlineStateChanged;

            if (Vector2.Distance(previous.Position, current.Position) > 0.001f)
                return ShelteredNetworkEventKinds.BunkerMoved;

            if (previous.NetworkPeerId != current.NetworkPeerId
                || previous.DisplayName != current.DisplayName)
                return ShelteredNetworkEventKinds.BunkerRegistered;

            return string.Empty;
        }

        private static ShelteredMultiplayerBunkerAssignmentRecord FindByBunkerOwnerId(
            ShelteredMultiplayerBunkerAssignmentRecord[] records,
            int bunkerOwnerId)
        {
            if (records == null)
                return null;

            for (int i = 0; i < records.Length; i++)
            {
                if (records[i] != null && records[i].BunkerOwnerId == bunkerOwnerId)
                    return records[i];
            }

            return null;
        }

        private static ShelteredNetworkGameplayEvent CreateBunkerEvent(
            string kind,
            ShelteredMultiplayerBunkerAssignmentRecord record)
        {
            BunkerMapRecord mapRecord = ShelteredBunkers.GetBunkerMapRecord(record.BunkerOwnerId);
            ExpeditionMap.GridRef gridRef = mapRecord != null
                ? mapRecord.GridRef
                : new ExpeditionMap.GridRef(0, 0);
            Vector3 mapPixels = mapRecord != null ? mapRecord.MapPixels : Vector3.zero;

            ShelteredNetworkGameplayEvent gameplayEvent = new ShelteredNetworkGameplayEvent();
            gameplayEvent.EventKind = kind;
            gameplayEvent.ActorId = record.NetworkPeerId.ToString();
            gameplayEvent.TargetId = record.BunkerOwnerId.ToString();
            gameplayEvent.Details = record.DisplayName ?? string.Empty;
            gameplayEvent.PeerId = record.NetworkPeerId;
            gameplayEvent.BunkerOwnerId = record.BunkerOwnerId;
            gameplayEvent.DisplayName = record.DisplayName ?? string.Empty;
            gameplayEvent.WorldPosition = record.Position;
            gameplayEvent.MapPixels = mapPixels;
            gameplayEvent.GridX = gridRef.x;
            gameplayEvent.GridY = gridRef.y;
            gameplayEvent.IsOnline = record.IsOnline;
            return gameplayEvent;
        }

        private void OnAuthoritativeBunkerEvent(ShelteredNetworkEventContext context)
        {
            if (context == null || context.GameplayEvent == null)
                return;

            ShelteredNetworkGameplayEvent gameplayEvent = context.GameplayEvent;
            if (!IsBunkerEvent(gameplayEvent.EventKind) || gameplayEvent.BunkerOwnerId < 0)
                return;

            byte peerId = gameplayEvent.PeerId >= 0 && gameplayEvent.PeerId <= byte.MaxValue
                ? (byte)gameplayEvent.PeerId
                : NetworkDefaults.UnassignedPeerId;

            ShelteredBunkers.RegisterBunker(new BunkerDefinition(
                gameplayEvent.BunkerOwnerId,
                gameplayEvent.WorldPosition,
                gameplayEvent.DisplayName,
                true,
                gameplayEvent.IsOnline,
                peerId));
        }

        private static bool IsBunkerEvent(string eventKind)
        {
            return eventKind == ShelteredNetworkEventKinds.BunkerRegistered
                || eventKind == ShelteredNetworkEventKinds.BunkerMoved
                || eventKind == ShelteredNetworkEventKinds.BunkerOnlineStateChanged;
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

            int sortKey = ShelteredMultiplayerSessionSeed.DeriveScopedSeed(
                sessionId,
                PlayerIdSeedScope + key);
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
            ModRandomStream stream = new ModRandomStream(ShelteredMultiplayerSessionSeed.DeriveScopedSeed(
                sessionId,
                BunkerSeedScope + playerCount));

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
            : this(networkPeerId, playerId, playerId > 0 ? playerId - 1 : 0, position, displayName, isOnline)
        {
        }

        public ShelteredMultiplayerBunkerAssignmentRecord(
            byte networkPeerId,
            int playerId,
            int bunkerOwnerId,
            Vector2 position,
            string displayName,
            bool isOnline)
        {
            NetworkPeerId = networkPeerId;
            PlayerId = playerId;
            BunkerOwnerId = bunkerOwnerId;
            Position = position;
            DisplayName = displayName ?? string.Empty;
            IsOnline = isOnline;
        }

        public readonly byte NetworkPeerId;
        public readonly int PlayerId;
        public readonly int BunkerOwnerId;
        public readonly Vector2 Position;
        public readonly string DisplayName;
        public readonly bool IsOnline;
    }
}
