using System.Collections.Generic;

namespace ShelteredAPI.Networking.World
{
    internal sealed class ShelteredWorldEventReplayCursor
    {
        private readonly HashSet<string> _appliedEventIds = new HashSet<string>();
        private long _lastAppliedTick;
        private string _lastAppliedEventId = string.Empty;

        public ShelteredWorldEventReplayCursor()
            : this(0)
        {
        }

        public ShelteredWorldEventReplayCursor(long nextWorldTick)
        {
            Reset(nextWorldTick);
        }

        public long NextWorldTick
        {
            get { return _lastAppliedTick; }
        }

        public long LastAppliedTick
        {
            get { return _lastAppliedTick; }
        }

        public string LastAppliedEventId
        {
            get { return _lastAppliedEventId; }
        }

        public IList<ShelteredWorldEventRecord> ReadAvailable(IShelteredWorldEventJournal journal)
        {
            return EnumerateUnapplied(journal);
        }

        public IList<ShelteredWorldEventRecord> EnumerateUnapplied(IShelteredWorldEventJournal journal)
        {
            if (journal == null)
                return new List<ShelteredWorldEventRecord>();

            IList<ShelteredWorldEventRecord> records = journal.GetSince(_lastAppliedTick);
            List<ShelteredWorldEventRecord> unread = new List<ShelteredWorldEventRecord>();
            for (int i = 0; i < records.Count; i++)
            {
                ShelteredWorldEventRecord record = records[i];
                if (record == null)
                    continue;
                if (record.WorldTick < _lastAppliedTick)
                    continue;
                if (_appliedEventIds.Contains(record.EventId))
                    continue;

                unread.Add(record);
            }

            return unread;
        }

        public void AdvanceAfterApply(ShelteredWorldEventRecord record)
        {
            if (record == null)
                return;

            _lastAppliedEventId = record.EventId ?? string.Empty;
            if (_lastAppliedEventId.Length > 0)
                _appliedEventIds.Add(_lastAppliedEventId);
            if (record.WorldTick > _lastAppliedTick)
                _lastAppliedTick = record.WorldTick;
        }

        public void Reset(long nextWorldTick)
        {
            ResetToTick(nextWorldTick);
        }

        public void ResetToTick(long worldTick)
        {
            _lastAppliedTick = worldTick < 0 ? 0 : worldTick;
            _lastAppliedEventId = string.Empty;
            _appliedEventIds.Clear();
        }
    }
}
