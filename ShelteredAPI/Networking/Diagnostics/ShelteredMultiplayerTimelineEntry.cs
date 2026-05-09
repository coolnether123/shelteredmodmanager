using System;

namespace ShelteredAPI.Networking.Diagnostics
{
    internal enum ShelteredMultiplayerTimelineCategory
    {
        Connection = 0,
        Setup = 1,
        AutoLoad = 2,
        Load = 3,
        Release = 4,
        MapAnchor = 5
    }

    internal enum ShelteredMultiplayerTimelineEventKind
    {
        HostStarted = 0,
        JoinRequested = 1,
        PeerConnected = 2,
        PeerDisconnected = 3,
        Reconnect = 4,
        SetupBeginSent = 5,
        SetupReceived = 6,
        SetupDifficultyUpdated = 7,
        AutoLoadStateChanged = 8,
        LocalWorldLoaded = 9,
        PeerLoaded = 10,
        ReleaseBlocked = 11,
        WorldReleased = 12,
        MapAnchorValidated = 13,
        MapAnchorFallback = 14,
        ConnectionFailure = 15
    }

    internal sealed class ShelteredMultiplayerTimelineEntry
    {
        public ShelteredMultiplayerTimelineEntry(
            long sequence,
            DateTime timestampUtc,
            ShelteredMultiplayerTimelineCategory category,
            ShelteredMultiplayerTimelineEventKind eventKind,
            ShelteredMultiplayerSessionMode mode,
            string sessionId,
            string shortSessionId,
            int localPlayerId,
            int networkPeerId,
            ShelteredMultiplayerSetupPhase setupPhase,
            long worldTick,
            string message)
        {
            Sequence = sequence;
            TimestampUtc = timestampUtc;
            Category = category;
            EventKind = eventKind;
            Mode = mode;
            SessionId = sessionId ?? string.Empty;
            ShortSessionId = shortSessionId ?? string.Empty;
            LocalPlayerId = localPlayerId;
            NetworkPeerId = networkPeerId;
            SetupPhase = setupPhase;
            WorldTick = worldTick;
            Message = message ?? string.Empty;
        }

        public long Sequence { get; private set; }
        public DateTime TimestampUtc { get; private set; }
        public ShelteredMultiplayerTimelineCategory Category { get; private set; }
        public ShelteredMultiplayerTimelineEventKind EventKind { get; private set; }
        public ShelteredMultiplayerSessionMode Mode { get; private set; }
        public string SessionId { get; private set; }
        public string ShortSessionId { get; private set; }
        public int LocalPlayerId { get; private set; }
        public int NetworkPeerId { get; private set; }
        public ShelteredMultiplayerSetupPhase SetupPhase { get; private set; }
        public long WorldTick { get; private set; }
        public string Message { get; private set; }

        public ShelteredMultiplayerTimelineEntry Clone()
        {
            return new ShelteredMultiplayerTimelineEntry(
                Sequence,
                TimestampUtc,
                Category,
                EventKind,
                Mode,
                SessionId,
                ShortSessionId,
                LocalPlayerId,
                NetworkPeerId,
                SetupPhase,
                WorldTick,
                Message);
        }
    }
}
