using System;

namespace ShelteredAPI.Networking.Diagnostics
{
    internal sealed class ShelteredMultiplayerTimeline
    {
        public const int DefaultCapacity = 128;
        public const int NoNetworkPeer = -1;

        private static readonly ShelteredMultiplayerTimeline _instance =
            new ShelteredMultiplayerTimeline(
                DefaultCapacity,
                delegate { return ShelteredMultiplayerSessionCoordinator.Instance.Context; },
                null);

        private readonly object _sync = new object();
        private readonly ShelteredMultiplayerTimelineEntry[] _entries;
        private readonly Func<ShelteredMultiplayerSessionContext> _contextProvider;
        private readonly Func<DateTime> _clockUtc;
        private int _nextIndex;
        private int _count;
        private long _nextSequence;
        private string _lastClearReason = string.Empty;
        private string _lastAppendError = string.Empty;

        internal ShelteredMultiplayerTimeline(
            int capacity,
            Func<ShelteredMultiplayerSessionContext> contextProvider,
            Func<DateTime> clockUtc)
        {
            _entries = capacity > 0
                ? new ShelteredMultiplayerTimelineEntry[capacity]
                : new ShelteredMultiplayerTimelineEntry[0];
            _contextProvider = contextProvider;
            _clockUtc = clockUtc;
        }

        public static ShelteredMultiplayerTimeline Instance
        {
            get { return _instance; }
        }

        public int Capacity
        {
            get { return _entries.Length; }
        }

        public string LastClearReason
        {
            get
            {
                lock (_sync)
                {
                    return _lastClearReason;
                }
            }
        }

        public string LastAppendError
        {
            get
            {
                lock (_sync)
                {
                    return _lastAppendError;
                }
            }
        }

        public void Append(
            ShelteredMultiplayerTimelineCategory category,
            ShelteredMultiplayerTimelineEventKind eventKind,
            string message)
        {
            Append(category, eventKind, null, NoNetworkPeer, message);
        }

        public void Append(
            ShelteredMultiplayerTimelineCategory category,
            ShelteredMultiplayerTimelineEventKind eventKind,
            int networkPeerId,
            string message)
        {
            Append(category, eventKind, null, networkPeerId, message);
        }

        public void Append(
            ShelteredMultiplayerTimelineCategory category,
            ShelteredMultiplayerTimelineEventKind eventKind,
            ShelteredMultiplayerSessionContext context,
            int networkPeerId,
            string message)
        {
            try
            {
                AppendCore(category, eventKind, context, networkPeerId, message);
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    _lastAppendError = ex.Message;
                }
            }
        }

        public void AppendAutoLoadStateChanged(string state, string detail)
        {
            Append(
                ShelteredMultiplayerTimelineCategory.AutoLoad,
                ShelteredMultiplayerTimelineEventKind.AutoLoadStateChanged,
                Combine("state=" + (state ?? string.Empty), detail));
        }

        public void AppendMapAnchorValidated(string detail)
        {
            Append(
                ShelteredMultiplayerTimelineCategory.MapAnchor,
                ShelteredMultiplayerTimelineEventKind.MapAnchorValidated,
                detail);
        }

        public void AppendMapAnchorFallback(string detail)
        {
            Append(
                ShelteredMultiplayerTimelineCategory.MapAnchor,
                ShelteredMultiplayerTimelineEventKind.MapAnchorFallback,
                detail);
        }

        public ShelteredMultiplayerTimelineEntry[] GetSnapshot()
        {
            lock (_sync)
            {
                ShelteredMultiplayerTimelineEntry[] snapshot =
                    new ShelteredMultiplayerTimelineEntry[_count];
                if (_count == 0)
                    return snapshot;

                int start = _count == _entries.Length ? _nextIndex : 0;
                for (int i = 0; i < _count; i++)
                {
                    ShelteredMultiplayerTimelineEntry entry = _entries[(start + i) % _entries.Length];
                    snapshot[i] = entry != null ? entry.Clone() : null;
                }

                return snapshot;
            }
        }

        public void Clear(string reason)
        {
            lock (_sync)
            {
                for (int i = 0; i < _entries.Length; i++)
                    _entries[i] = null;

                _nextIndex = 0;
                _count = 0;
                _lastClearReason = reason ?? string.Empty;
                _lastAppendError = string.Empty;
            }
        }

        public string[] FormatCompact(int maxEntries)
        {
            return ShelteredMultiplayerTimelineFormatter.FormatCompact(GetSnapshot(), maxEntries);
        }

        public string[] FormatCompact()
        {
            return FormatCompact(DefaultCapacity);
        }

        private void AppendCore(
            ShelteredMultiplayerTimelineCategory category,
            ShelteredMultiplayerTimelineEventKind eventKind,
            ShelteredMultiplayerSessionContext context,
            int networkPeerId,
            string message)
        {
            if (_entries.Length == 0)
                return;

            ShelteredMultiplayerSessionContext effectiveContext = context ?? SafeGetContext();
            lock (_sync)
            {
                ShelteredMultiplayerTimelineEntry entry = CreateEntry(
                    category,
                    eventKind,
                    effectiveContext,
                    networkPeerId,
                    message);
                _entries[_nextIndex] = entry;
                _nextIndex = (_nextIndex + 1) % _entries.Length;
                if (_count < _entries.Length)
                    _count++;
                _lastAppendError = string.Empty;
            }
        }

        private ShelteredMultiplayerTimelineEntry CreateEntry(
            ShelteredMultiplayerTimelineCategory category,
            ShelteredMultiplayerTimelineEventKind eventKind,
            ShelteredMultiplayerSessionContext context,
            int networkPeerId,
            string message)
        {
            ShelteredMultiplayerSessionMode mode = ShelteredMultiplayerSessionMode.SinglePlayer;
            string sessionId = string.Empty;
            int localPlayerId = 0;
            ShelteredMultiplayerSetupPhase setupPhase = ShelteredMultiplayerSetupPhase.Inactive;
            long worldTick = 0;

            if (context != null)
            {
                mode = context.Mode;
                sessionId = context.SessionId ?? string.Empty;
                localPlayerId = context.LocalPlayerId;
                setupPhase = context.SetupPhase;
                worldTick = context.WorldTick;
            }

            return new ShelteredMultiplayerTimelineEntry(
                ++_nextSequence,
                ReadClockUtc(),
                category,
                eventKind,
                mode,
                sessionId,
                ShortenSessionId(sessionId),
                localPlayerId,
                networkPeerId,
                setupPhase,
                worldTick,
                message);
        }

        private DateTime ReadClockUtc()
        {
            if (_clockUtc != null)
                return _clockUtc();

            return DateTime.UtcNow;
        }

        private ShelteredMultiplayerSessionContext SafeGetContext()
        {
            if (_contextProvider == null)
                return null;

            try
            {
                return _contextProvider();
            }
            catch
            {
                // GuardrailAllow: SilentCatch - timeline context capture is diagnostics-only and must not affect session flow.
                return null;
            }
        }

        private static string ShortenSessionId(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return string.Empty;

            if (sessionId.Length <= 12)
                return sessionId;

            return sessionId.Substring(0, 4) + "..." + sessionId.Substring(sessionId.Length - 4);
        }

        private static string Combine(string first, string second)
        {
            if (string.IsNullOrEmpty(first))
                return second ?? string.Empty;
            if (string.IsNullOrEmpty(second))
                return first;

            return first + " " + second;
        }
    }
}
