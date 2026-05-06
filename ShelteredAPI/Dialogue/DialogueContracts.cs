using System;
using System.Collections.Generic;

namespace ShelteredAPI.Dialogue
{
    public interface IShelteredDialogueService
    {
        event Action<DialogueRequest> DialogueQueued;
        event Action<DialogueRequest> DialogueStarted;
        event Action<DialogueRequest> DialogueCompleted;
        event Action<DialogueRequest, DialogueRequestResult> DialogueSkipped;

        IDialogueLineSelector LineSelector { get; }
        IDialogueHistoryStore HistoryStore { get; }

        DialogueRequestResult Queue(DialogueRequest request);
        DialogueRequestResult QueueSequence(DialogueSequence sequence);

        int Clear();
        int Clear(string ownerId);

        IDisposable RegisterChannel(DialogueChannel channel, IDialogueChannelAdapter adapter);
        bool UnregisterChannel(string channelId);

        void Update();
    }

    public interface IDialogueChannelAdapter
    {
        string ChannelId { get; }
        bool CanHandle(DialogueRequest request);
        bool TryStart(DialogueRequest request, out string suppressionReason);
    }

    public interface IDialogueLineSelector
    {
        bool TrySelectLine(DialogueSelectionContext context, IList<DialogueLineOption> options, out string line);
    }

    public interface IDialogueHistoryStore
    {
        int GetTicksSinceLastUse(DialogueSelectionContext context, string line, int nowTick);
        void Remember(DialogueSelectionContext context, string line, int nowTick);
        void Clear();
        void Clear(string ownerId);
    }

    internal interface IDialogueClock
    {
        float TimeSeconds { get; }
        int CurrentDay { get; }
    }

    internal interface IDialogueRandom
    {
        float Range(float minInclusive, float maxInclusive);
        int Range(int minInclusive, int maxExclusive);
    }
}
