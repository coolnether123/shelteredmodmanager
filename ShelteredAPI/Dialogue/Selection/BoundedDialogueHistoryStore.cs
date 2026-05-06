using System;
using System.Collections.Generic;

namespace ShelteredAPI.Dialogue.Selection
{
    /// <summary>
    /// Generic bounded memory of recently selected lines, scoped by owner/mod, context, and speaker.
    /// </summary>
    public sealed class BoundedDialogueHistoryStore : IDialogueHistoryStore
    {
        private const int DefaultMaxTrackedPerScope = 48;
        private const int DefaultRetentionTicks = 256;

        private readonly int _maxTrackedPerScope;
        private readonly int _retentionTicks;
        private readonly Dictionary<string, ScopeMemory> _memoryByScope =
            new Dictionary<string, ScopeMemory>(StringComparer.Ordinal);

        public BoundedDialogueHistoryStore()
            : this(DefaultMaxTrackedPerScope, DefaultRetentionTicks)
        {
        }

        public BoundedDialogueHistoryStore(int maxTrackedPerScope, int retentionTicks)
        {
            _maxTrackedPerScope = maxTrackedPerScope <= 0 ? DefaultMaxTrackedPerScope : maxTrackedPerScope;
            _retentionTicks = retentionTicks <= 0 ? DefaultRetentionTicks : retentionTicks;
        }

        public int GetTicksSinceLastUse(DialogueSelectionContext context, string line, int nowTick)
        {
            if (context == null || string.IsNullOrEmpty(line))
                return int.MaxValue;

            ScopeMemory memory;
            if (!_memoryByScope.TryGetValue(BuildScopeKey(context), out memory) || memory == null)
                return int.MaxValue;

            int lastTick;
            if (!memory.LastTickByLine.TryGetValue(line, out lastTick))
                return int.MaxValue;

            int delta = nowTick - lastTick;
            return delta < 0 ? 0 : delta;
        }

        public void Remember(DialogueSelectionContext context, string line, int nowTick)
        {
            if (context == null || string.IsNullOrEmpty(line))
                return;

            string scopeKey = BuildScopeKey(context);
            ScopeMemory memory;
            if (!_memoryByScope.TryGetValue(scopeKey, out memory) || memory == null)
            {
                memory = new ScopeMemory();
                _memoryByScope[scopeKey] = memory;
            }

            memory.LastTickByLine[line] = nowTick;
            memory.UsageOrder.Enqueue(new UsageRecord(line, nowTick));
            Prune(memory, nowTick);
        }

        public void Clear()
        {
            _memoryByScope.Clear();
        }

        public void Clear(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                Clear();
                return;
            }

            List<string> remove = new List<string>();
            foreach (string key in _memoryByScope.Keys)
            {
                if (key.StartsWith(ownerId + "|", StringComparison.Ordinal))
                    remove.Add(key);
            }

            for (int i = 0; i < remove.Count; i++)
                _memoryByScope.Remove(remove[i]);
        }

        private string BuildScopeKey(DialogueSelectionContext context)
        {
            string owner = string.IsNullOrEmpty(context.OwnerId) ? "owner:none" : context.OwnerId;
            string scope = string.IsNullOrEmpty(context.ContextKey) ? "context:none" : context.ContextKey;
            string speaker = DialogueSpeakerRef.ResolveSpeakerKey(context.Speaker);
            return owner + "|" + scope + "|" + speaker;
        }

        private void Prune(ScopeMemory memory, int nowTick)
        {
            if (memory == null)
                return;

            while (memory.UsageOrder.Count > 0)
            {
                UsageRecord head = memory.UsageOrder.Peek();
                bool expired = _retentionTicks > 0 && (nowTick - head.Tick) > _retentionTicks;
                bool overCapacity = memory.LastTickByLine.Count > _maxTrackedPerScope;
                if (!expired && !overCapacity)
                    break;

                memory.UsageOrder.Dequeue();

                int latestTick;
                if (memory.LastTickByLine.TryGetValue(head.Line, out latestTick) && latestTick == head.Tick)
                    memory.LastTickByLine.Remove(head.Line);
            }
        }

        private sealed class ScopeMemory
        {
            public readonly Dictionary<string, int> LastTickByLine =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public readonly Queue<UsageRecord> UsageOrder = new Queue<UsageRecord>();
        }

        private sealed class UsageRecord
        {
            public readonly string Line;
            public readonly int Tick;

            public UsageRecord(string line, int tick)
            {
                Line = line ?? string.Empty;
                Tick = tick;
            }
        }
    }
}
