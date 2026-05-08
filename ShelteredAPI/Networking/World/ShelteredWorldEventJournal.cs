using System;
using System.Collections.Generic;

namespace ShelteredAPI.Networking.World
{
    internal sealed class ShelteredWorldEventJournal : IShelteredWorldEventJournal
    {
        public const int DefaultMaxRetainedEvents = 4096;

        private readonly object _sync = new object();
        private readonly List<ShelteredWorldEventRecord> _records = new List<ShelteredWorldEventRecord>();
        private readonly Dictionary<string, ShelteredWorldEventRecord> _recordsById =
            new Dictionary<string, ShelteredWorldEventRecord>(StringComparer.Ordinal);
        private readonly int _maxRetainedEvents;
        private long _latestTick;

        public ShelteredWorldEventJournal()
            : this(DefaultMaxRetainedEvents)
        {
        }

        public ShelteredWorldEventJournal(int maxRetainedEvents)
        {
            _maxRetainedEvents = maxRetainedEvents > 0 ? maxRetainedEvents : DefaultMaxRetainedEvents;
        }

        public long LatestTick
        {
            get
            {
                lock (_sync)
                {
                    return _latestTick;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return _records.Count;
                }
            }
        }

        public ShelteredWorldEventAppendResult Append(ShelteredWorldEventRecord record)
        {
            if (record == null)
                return ShelteredWorldEventAppendResult.Rejected(string.Empty, "World event record is required.");

            string eventKind = Normalize(record.EventKind);
            if (eventKind.Length == 0)
                return ShelteredWorldEventAppendResult.Rejected(Normalize(record.EventId), "World event kind is required.");

            ShelteredWorldEventRecord normalized = record.Clone();
            normalized.EventId = Normalize(normalized.EventId);
            if (normalized.EventId.Length == 0)
                normalized.EventId = "worldevent." + Guid.NewGuid().ToString("N");
            normalized.EventKind = eventKind;
            normalized.CorrelationId = Normalize(normalized.CorrelationId);
            normalized.PayloadJson = normalized.PayloadJson ?? string.Empty;
            if (normalized.WorldTick < 0)
                normalized.WorldTick = 0;
            if (normalized.WorldDeltaSeconds < 0f)
                normalized.WorldDeltaSeconds = 0f;
            if (normalized.CreatedUtc == DateTime.MinValue)
                normalized.CreatedUtc = DateTime.UtcNow;

            lock (_sync)
            {
                if (_recordsById.ContainsKey(normalized.EventId))
                    return ShelteredWorldEventAppendResult.Rejected(normalized.EventId, "World event already exists.");

                _records.Add(normalized);
                _recordsById.Add(normalized.EventId, normalized);
                if (normalized.WorldTick > _latestTick)
                    _latestTick = normalized.WorldTick;

                TrimOldestIfNeeded();
            }

            return ShelteredWorldEventAppendResult.Accepted(normalized.EventId);
        }

        public IList<ShelteredWorldEventRecord> GetSince(long worldTick)
        {
            if (worldTick < 0)
                worldTick = 0;

            lock (_sync)
            {
                List<ShelteredWorldEventRecord> result = new List<ShelteredWorldEventRecord>();
                for (int i = 0; i < _records.Count; i++)
                {
                    if (_records[i].WorldTick >= worldTick)
                        result.Add(_records[i].Clone());
                }

                return result;
            }
        }

        public IList<ShelteredWorldEventRecord> GetRange(long startTick, long endTick)
        {
            if (startTick < 0)
                startTick = 0;
            if (endTick < startTick)
                return new List<ShelteredWorldEventRecord>();

            lock (_sync)
            {
                List<ShelteredWorldEventRecord> result = new List<ShelteredWorldEventRecord>();
                for (int i = 0; i < _records.Count; i++)
                {
                    ShelteredWorldEventRecord record = _records[i];
                    if (record.WorldTick >= startTick && record.WorldTick <= endTick)
                        result.Add(record.Clone());
                }

                return result;
            }
        }

        public ShelteredWorldEventRecord GetById(string eventId)
        {
            string key = Normalize(eventId);
            if (key.Length == 0)
                return null;

            lock (_sync)
            {
                ShelteredWorldEventRecord record;
                return _recordsById.TryGetValue(key, out record) ? record.Clone() : null;
            }
        }

        public bool Contains(string eventId)
        {
            string key = Normalize(eventId);
            if (key.Length == 0)
                return false;

            lock (_sync)
            {
                return _recordsById.ContainsKey(key);
            }
        }

        public void Clear(string reason)
        {
            lock (_sync)
            {
                _records.Clear();
                _recordsById.Clear();
                _latestTick = 0;
            }
        }

        private void TrimOldestIfNeeded()
        {
            while (_records.Count > _maxRetainedEvents)
            {
                ShelteredWorldEventRecord oldest = _records[0];
                _records.RemoveAt(0);
                if (oldest != null)
                    _recordsById.Remove(oldest.EventId);
            }

            RecalculateLatestTick();
        }

        private void RecalculateLatestTick()
        {
            long latest = 0;
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].WorldTick > latest)
                    latest = _records[i].WorldTick;
            }

            _latestTick = latest;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
