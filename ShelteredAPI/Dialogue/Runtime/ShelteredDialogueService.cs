using System;
using System.Collections.Generic;
using ShelteredAPI.Dialogue.Selection;

namespace ShelteredAPI.Dialogue.Runtime
{
    internal sealed class ShelteredDialogueService : IShelteredDialogueService
    {
        private const float RoutineSpacingSeconds = 15f;
        private const float ReactiveSpacingSeconds = 6f;
        private const float ImmediateSpacingSeconds = 0.1f;
        private const float DefaultJitterSeconds = 5f;
        private const float DefaultConversationMinSpacingSeconds = 1.0f;
        private const float DefaultConversationMaxSpacingSeconds = 2.5f;

        private readonly IDialogueClock _clock;
        private readonly IDialogueRandom _random;
        private readonly IDialogueHistoryStore _historyStore;
        private readonly IDialogueLineSelector _lineSelector;
        private readonly List<QueuedDialogue> _queue = new List<QueuedDialogue>();
        private readonly Dictionary<string, IDialogueChannelAdapter> _channels =
            new Dictionary<string, IDialogueChannelAdapter>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DailyBudget> _dailyBudgets =
            new Dictionary<string, DailyBudget>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DailyBudget> _speakerDailyBudgets =
            new Dictionary<string, DailyBudget>(StringComparer.OrdinalIgnoreCase);

        private float _nextMessageTime;
        private string _activeSequenceId;
        private int _idCounter;
        private int _tickCounter;

        public ShelteredDialogueService()
            : this(new SystemDialogueClock(), new SystemDialogueRandom(), null, null)
        {
        }

        internal ShelteredDialogueService(
            IDialogueClock clock,
            IDialogueRandom random,
            IDialogueHistoryStore historyStore,
            IDialogueLineSelector lineSelector)
        {
            _clock = clock ?? new SystemDialogueClock();
            _random = random ?? new SystemDialogueRandom();
            _historyStore = historyStore ?? new BoundedDialogueHistoryStore();
            _lineSelector = lineSelector ?? new DefaultDialogueLineSelector(_historyStore, _random);
        }

        public event Action<DialogueRequest> DialogueQueued;
        public event Action<DialogueRequest> DialogueStarted;
        public event Action<DialogueRequest> DialogueCompleted;
        public event Action<DialogueRequest, DialogueRequestResult> DialogueSkipped;

        public IDialogueLineSelector LineSelector
        {
            get { return _lineSelector; }
        }

        public IDialogueHistoryStore HistoryStore
        {
            get { return _historyStore; }
        }

        public DialogueRequestResult Queue(DialogueRequest request)
        {
            DialogueRequest normalized = NormalizeRequest(request);
            DialogueRequestResult validation = ValidateRequest(normalized);
            if (validation != null)
                return validation;

            if (!HasChannel(normalized.Channel))
                return DialogueRequestResult.FromRequest(DialogueRequestResultStatus.RejectedNoChannel, normalized, "No dialogue channel is registered for this request.");

            if (!TryReserveBudget(normalized))
                return DialogueRequestResult.FromRequest(DialogueRequestResultStatus.RejectedBudget, normalized, "Dialogue budget is exhausted.");

            _queue.Add(new QueuedDialogue(normalized, null));
            RaiseQueued(normalized);
            return DialogueRequestResult.Queued(normalized);
        }

        public DialogueRequestResult QueueSequence(DialogueSequence sequence)
        {
            if (sequence == null)
                return new DialogueRequestResult(DialogueRequestResultStatus.RejectedInvalid, null, null, "Dialogue sequence is required.");

            if (sequence.Turns == null || sequence.Turns.Count == 0)
                return new DialogueRequestResult(DialogueRequestResultStatus.RejectedInvalid, sequence.Id, sequence.OwnerId, "Dialogue sequence has no turns.");

            string sequenceId = string.IsNullOrEmpty(sequence.Id) ? NextId("sequence") : sequence.Id;
            List<DialogueRequest> requests = new List<DialogueRequest>();
            for (int i = 0; i < sequence.Turns.Count; i++)
            {
                DialogueTurn turn = sequence.Turns[i];
                if (turn == null)
                    continue;

                DialogueRequest request = new DialogueRequest();
                request.Id = sequenceId + ".turn" + (i + 1).ToString();
                request.OwnerId = sequence.OwnerId;
                request.ContextKey = sequence.ContextKey;
                request.Channel = turn.Channel ?? sequence.Channel;
                request.Speaker = turn.Speaker;
                request.Text = turn.Text;
                request.Priority = sequence.Priority;
                request.Validation = CombineValidation(sequence.Validation, turn.Validation);
                request.HasCustomDelay = true;
                request.MinDelaySeconds = sequence.MinTurnDelaySeconds <= 0f ? DefaultConversationMinSpacingSeconds : sequence.MinTurnDelaySeconds;
                request.MaxDelaySeconds = sequence.MaxTurnDelaySeconds <= 0f ? DefaultConversationMaxSpacingSeconds : sequence.MaxTurnDelaySeconds;
                request.UseDailyBudget = sequence.UseDailyBudget;
                request.MaxPerDay = sequence.MaxPerDay;
                request.MaxPerSpeakerPerDay = sequence.MaxPerSpeakerPerDay;
                request.BudgetKey = sequence.BudgetKey;
                DialogueSpeakerRef.CopyMetadata(sequence.Metadata, request.Metadata);
                DialogueSpeakerRef.CopyMetadata(turn.Metadata, request.Metadata);

                DialogueRequest normalized = NormalizeRequest(request);
                DialogueRequestResult validation = ValidateRequest(normalized);
                if (validation != null)
                    return validation;

                if (!HasChannel(normalized.Channel))
                    return DialogueRequestResult.FromRequest(DialogueRequestResultStatus.RejectedNoChannel, normalized, "No dialogue channel is registered for a sequence turn.");

                requests.Add(normalized);
            }

            if (requests.Count == 0)
                return new DialogueRequestResult(DialogueRequestResultStatus.RejectedInvalid, sequenceId, sequence.OwnerId, "Dialogue sequence has no valid turns.");

            if (!TryReserveSequenceBudget(sequence, requests))
                return new DialogueRequestResult(DialogueRequestResultStatus.RejectedBudget, sequenceId, sequence.OwnerId, "Dialogue sequence budget is exhausted.");

            for (int i = 0; i < requests.Count; i++)
            {
                _queue.Add(new QueuedDialogue(requests[i], sequenceId));
                RaiseQueued(requests[i]);
            }

            return DialogueRequestResult.Queued(requests[0]);
        }

        public int Clear()
        {
            return Clear(null);
        }

        public int Clear(string ownerId)
        {
            int removed = 0;
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(ownerId) || string.Equals(_queue[i].Request.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
                {
                    DialogueRequest request = _queue[i].Request;
                    _queue.RemoveAt(i);
                    removed++;
                    RaiseSkipped(request, DialogueRequestResult.FromRequest(DialogueRequestResultStatus.Cleared, request, "Dialogue request was cleared."));
                }
            }

            if (string.IsNullOrEmpty(ownerId) || HasNoQueuedSequence(_activeSequenceId))
                _activeSequenceId = null;

            if (_queue.Count == 0)
                _nextMessageTime = 0f;

            if (string.IsNullOrEmpty(ownerId))
            {
                _historyStore.Clear();
                _dailyBudgets.Clear();
                _speakerDailyBudgets.Clear();
            }
            else
            {
                _historyStore.Clear(ownerId);
                ClearBudgetsForOwner(_dailyBudgets, ownerId);
                ClearBudgetsForOwner(_speakerDailyBudgets, ownerId);
            }

            return removed;
        }

        public IDisposable RegisterChannel(DialogueChannel channel, IDialogueChannelAdapter adapter)
        {
            if (channel == null || string.IsNullOrEmpty(channel.Id) || adapter == null)
                return DialogueDisposable.Empty;

            _channels[channel.Id] = adapter;
            return new DialogueDisposable(delegate { UnregisterChannel(channel.Id); });
        }

        public bool UnregisterChannel(string channelId)
        {
            if (string.IsNullOrEmpty(channelId))
                return false;

            return _channels.Remove(channelId);
        }

        public void Update()
        {
            if (_queue.Count == 0)
                return;

            float now = _clock.TimeSeconds;
            if (now < _nextMessageTime)
                return;

            int index = GetNextIndex();
            if (index < 0 || index >= _queue.Count)
                return;

            QueuedDialogue queued = _queue[index];
            _queue.RemoveAt(index);

            if (!string.IsNullOrEmpty(queued.SequenceId) && string.IsNullOrEmpty(_activeSequenceId))
                _activeSequenceId = queued.SequenceId;

            ProcessQueued(queued);

            if (!string.IsNullOrEmpty(queued.SequenceId) &&
                string.Equals(_activeSequenceId, queued.SequenceId, StringComparison.Ordinal) &&
                HasNoQueuedSequence(queued.SequenceId))
            {
                _activeSequenceId = null;
            }

            _nextMessageTime = now + ResolveSpacing(queued.Request);
        }

        internal int QueueCount
        {
            get { return _queue.Count; }
        }

        private void ProcessQueued(QueuedDialogue queued)
        {
            DialogueRequest request = queued.Request;
            if (request.Validation != null)
            {
                bool valid = false;
                try
                {
                    valid = request.Validation();
                }
                catch
                {
                    valid = false;
                }

                if (!valid)
                {
                    RaiseSkipped(request, DialogueRequestResult.FromRequest(DialogueRequestResultStatus.SkippedValidation, request, "Dialogue validation returned false."));
                    return;
                }
            }

            IDialogueChannelAdapter adapter;
            if (!TryGetChannel(request.Channel, out adapter) || adapter == null)
            {
                RaiseSkipped(request, DialogueRequestResult.FromRequest(DialogueRequestResultStatus.RejectedNoChannel, request, "Dialogue channel is not registered."));
                return;
            }

            string suppressionReason = null;
            RaiseStarted(request);
            bool started = false;
            try
            {
                started = adapter.CanHandle(request) && adapter.TryStart(request, out suppressionReason);
            }
            catch (Exception ex)
            {
                suppressionReason = ex.Message;
                started = false;
            }

            if (started)
            {
                RaiseCompleted(request);
            }
            else
            {
                RaiseSkipped(request, DialogueRequestResult.FromRequest(
                    DialogueRequestResultStatus.Suppressed,
                    request,
                    string.IsNullOrEmpty(suppressionReason) ? "Dialogue request was suppressed." : suppressionReason));
            }
        }

        private DialogueRequest NormalizeRequest(DialogueRequest request)
        {
            DialogueRequest normalized = request != null ? request.Clone() : new DialogueRequest();
            if (string.IsNullOrEmpty(normalized.Id))
                normalized.Id = NextId("request");
            if (normalized.Channel == null)
                normalized.Channel = DialogueChannel.AmbientSurvivorSpeech;
            if (string.IsNullOrEmpty(normalized.OwnerId))
                normalized.OwnerId = "unknown";
            if (string.IsNullOrEmpty(normalized.ContextKey))
                normalized.ContextKey = normalized.Channel.Id;
            if (normalized.HasCustomDelay)
            {
                normalized.MinDelaySeconds = Max(0f, normalized.MinDelaySeconds);
                normalized.MaxDelaySeconds = Max(normalized.MinDelaySeconds, normalized.MaxDelaySeconds);
            }
            return normalized;
        }

        private DialogueRequestResult ValidateRequest(DialogueRequest request)
        {
            if (request == null)
                return new DialogueRequestResult(DialogueRequestResultStatus.RejectedInvalid, null, null, "Dialogue request is required.");

            if (request.Channel == null || string.IsNullOrEmpty(request.Channel.Id))
                return DialogueRequestResult.FromRequest(DialogueRequestResultStatus.RejectedInvalid, request, "Dialogue channel is required.");

            if (string.IsNullOrEmpty(request.Text))
                return DialogueRequestResult.FromRequest(DialogueRequestResultStatus.RejectedInvalid, request, "Dialogue text is required.");

            return null;
        }

        private bool HasChannel(DialogueChannel channel)
        {
            IDialogueChannelAdapter adapter;
            return TryGetChannel(channel, out adapter);
        }

        private bool TryGetChannel(DialogueChannel channel, out IDialogueChannelAdapter adapter)
        {
            adapter = null;
            if (channel == null || string.IsNullOrEmpty(channel.Id))
                return false;

            return _channels.TryGetValue(channel.Id, out adapter) && adapter != null;
        }

        private int GetNextIndex()
        {
            if (!string.IsNullOrEmpty(_activeSequenceId))
            {
                for (int i = 0; i < _queue.Count; i++)
                {
                    if (string.Equals(_queue[i].SequenceId, _activeSequenceId, StringComparison.Ordinal))
                        return i;
                }

                _activeSequenceId = null;
            }

            if (_queue.Count == 0)
                return -1;

            int bestIndex = 0;
            DialoguePriority bestPriority = _queue[0].Request.Priority;
            for (int i = 1; i < _queue.Count; i++)
            {
                if (_queue[i].Request.Priority > bestPriority)
                {
                    bestPriority = _queue[i].Request.Priority;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private bool HasNoQueuedSequence(string sequenceId)
        {
            if (string.IsNullOrEmpty(sequenceId))
                return true;

            for (int i = 0; i < _queue.Count; i++)
            {
                if (string.Equals(_queue[i].SequenceId, sequenceId, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private float ResolveSpacing(DialogueRequest request)
        {
            if (request != null && request.HasCustomDelay)
                return _random.Range(request.MinDelaySeconds, request.MaxDelaySeconds);

            DialoguePriority priority = request != null ? request.Priority : DialoguePriority.Routine;
            if (priority == DialoguePriority.Immediate)
                return ImmediateSpacingSeconds;

            float baseSpacing = priority == DialoguePriority.Reactive ? ReactiveSpacingSeconds : RoutineSpacingSeconds;
            return baseSpacing + _random.Range(0f, DefaultJitterSeconds);
        }

        private bool TryReserveSequenceBudget(DialogueSequence sequence, IList<DialogueRequest> requests)
        {
            if (sequence == null || !sequence.UseDailyBudget)
                return true;

            DialogueRequest probe = requests != null && requests.Count > 0 ? requests[0] : null;
            if (probe == null)
                return true;

            return TryReserveBudget(probe);
        }

        private bool TryReserveBudget(DialogueRequest request)
        {
            if (request == null || !request.UseDailyBudget)
                return true;

            int maxPerDay = request.MaxPerDay;
            int maxPerSpeaker = request.MaxPerSpeakerPerDay;
            if (maxPerDay <= 0 && maxPerSpeaker <= 0)
                return true;

            int day = _clock.CurrentDay;
            if (maxPerDay > 0)
            {
                string key = BuildBudgetKey(request, false);
                if (!TryReserve(_dailyBudgets, key, day, maxPerDay))
                    return false;
            }

            if (maxPerSpeaker > 0)
            {
                string key = BuildBudgetKey(request, true);
                if (!TryReserve(_speakerDailyBudgets, key, day, maxPerSpeaker))
                {
                    if (maxPerDay > 0)
                        Release(_dailyBudgets, BuildBudgetKey(request, false), day);
                    return false;
                }
            }

            return true;
        }

        private static bool TryReserve(Dictionary<string, DailyBudget> budgets, string key, int day, int max)
        {
            DailyBudget budget;
            if (!budgets.TryGetValue(key, out budget) || budget == null || budget.Day != day)
            {
                budget = new DailyBudget(day);
                budgets[key] = budget;
            }

            if (budget.Count >= max)
                return false;

            budget.Count++;
            return true;
        }

        private static void Release(Dictionary<string, DailyBudget> budgets, string key, int day)
        {
            DailyBudget budget;
            if (budgets.TryGetValue(key, out budget) && budget != null && budget.Day == day && budget.Count > 0)
                budget.Count--;
        }

        private static string BuildBudgetKey(DialogueRequest request, bool includeSpeaker)
        {
            string owner = string.IsNullOrEmpty(request.OwnerId) ? "owner:none" : request.OwnerId;
            string budget = string.IsNullOrEmpty(request.BudgetKey) ? request.ContextKey : request.BudgetKey;
            string channel = request.Channel != null ? request.Channel.Id : "channel:none";
            string key = owner + "|" + channel + "|" + budget;
            if (includeSpeaker)
                key += "|" + DialogueSpeakerRef.ResolveSpeakerKey(request.Speaker);
            return key;
        }

        private static void ClearBudgetsForOwner(Dictionary<string, DailyBudget> budgets, string ownerId)
        {
            if (budgets == null || string.IsNullOrEmpty(ownerId))
                return;

            List<string> remove = new List<string>();
            foreach (string key in budgets.Keys)
            {
                if (key.StartsWith(ownerId + "|", StringComparison.OrdinalIgnoreCase))
                    remove.Add(key);
            }

            for (int i = 0; i < remove.Count; i++)
                budgets.Remove(remove[i]);
        }

        private string NextId(string prefix)
        {
            _idCounter++;
            return "shelteredapi.dialogue." + prefix + "." + _idCounter.ToString();
        }

        private static Func<bool> CombineValidation(Func<bool> first, Func<bool> second)
        {
            if (first == null)
                return second;
            if (second == null)
                return first;

            return delegate { return first() && second(); };
        }

        private static float Max(float left, float right)
        {
            return left > right ? left : right;
        }

        private void RaiseQueued(DialogueRequest request)
        {
            Action<DialogueRequest> handler = DialogueQueued;
            if (handler != null)
                handler(request);
        }

        private void RaiseStarted(DialogueRequest request)
        {
            _tickCounter++;
            Action<DialogueRequest> handler = DialogueStarted;
            if (handler != null)
                handler(request);
        }

        private void RaiseCompleted(DialogueRequest request)
        {
            Action<DialogueRequest> handler = DialogueCompleted;
            if (handler != null)
                handler(request);
        }

        private void RaiseSkipped(DialogueRequest request, DialogueRequestResult result)
        {
            Action<DialogueRequest, DialogueRequestResult> handler = DialogueSkipped;
            if (handler != null)
                handler(request, result);
        }

        private sealed class QueuedDialogue
        {
            public readonly DialogueRequest Request;
            public readonly string SequenceId;

            public QueuedDialogue(DialogueRequest request, string sequenceId)
            {
                Request = request;
                SequenceId = sequenceId;
            }
        }

        private sealed class DailyBudget
        {
            public readonly int Day;
            public int Count;

            public DailyBudget(int day)
            {
                Day = day;
            }
        }
    }
}
