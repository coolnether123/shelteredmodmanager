using System.Collections.Generic;

namespace ShelteredAPI.Networking.World
{
    internal sealed class ShelteredWorldEventReplayCursor
    {
        private readonly HashSet<string> _seenEventIds = new HashSet<string>();
        private long _nextWorldTick;

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
            get { return _nextWorldTick; }
        }

        public IList<ShelteredWorldEventRecord> ReadAvailable(IShelteredWorldEventJournal journal)
        {
            if (journal == null)
                return new List<ShelteredWorldEventRecord>();

            IList<ShelteredWorldEventRecord> records = journal.GetSince(_nextWorldTick);
            List<ShelteredWorldEventRecord> unread = new List<ShelteredWorldEventRecord>();
            for (int i = 0; i < records.Count; i++)
            {
                ShelteredWorldEventRecord record = records[i];
                if (record == null || _seenEventIds.Contains(record.EventId))
                    continue;

                unread.Add(record);
                _seenEventIds.Add(record.EventId);
                if (record.WorldTick > _nextWorldTick)
                    _nextWorldTick = record.WorldTick;
            }

            return unread;
        }

        public void Reset(long nextWorldTick)
        {
            _nextWorldTick = nextWorldTick < 0 ? 0 : nextWorldTick;
            _seenEventIds.Clear();
        }
    }
}
