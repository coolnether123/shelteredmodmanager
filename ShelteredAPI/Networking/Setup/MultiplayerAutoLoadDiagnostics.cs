using System;

namespace ShelteredAPI.Networking.Setup
{
    internal sealed class MultiplayerAutoLoadStatus
    {
        public MultiplayerAutoLoadStatus(
            MultiplayerAutoLoadState currentState,
            string detailText,
            string lastError,
            int retryCount,
            int targetSlot,
            int entryFrame,
            int entryMilliseconds,
            string expectedCondition,
            string lastAction)
        {
            CurrentState = currentState;
            DetailText = detailText ?? string.Empty;
            LastError = lastError ?? string.Empty;
            RetryCount = retryCount >= 0 ? retryCount : 0;
            TargetSlot = targetSlot > 0 ? targetSlot : 0;
            EntryFrame = entryFrame >= 0 ? entryFrame : 0;
            EntryMilliseconds = entryMilliseconds >= 0 ? entryMilliseconds : 0;
            ExpectedCondition = expectedCondition ?? string.Empty;
            LastAction = lastAction ?? string.Empty;
        }

        public readonly MultiplayerAutoLoadState CurrentState;
        public readonly string DetailText;
        public readonly string LastError;
        public readonly int RetryCount;
        public readonly int TargetSlot;
        public readonly int EntryFrame;
        public readonly int EntryMilliseconds;
        public readonly string ExpectedCondition;
        public readonly string LastAction;

        public bool IsActive
        {
            get
            {
                return CurrentState != MultiplayerAutoLoadState.Idle
                    && CurrentState != MultiplayerAutoLoadState.Loaded
                    && CurrentState != MultiplayerAutoLoadState.Failed
                    && CurrentState != MultiplayerAutoLoadState.Cancelled;
            }
        }

        public override string ToString()
        {
            string text = CurrentState + ": " + DetailText;
            if (!string.IsNullOrEmpty(LastError))
                text += " Error=" + LastError;
            if (TargetSlot > 0)
                text += " TargetSlot=" + TargetSlot;
            if (RetryCount > 0)
                text += " Retries=" + RetryCount;
            return text;
        }
    }

    internal sealed class MultiplayerAutoLoadStateChangedEventArgs : EventArgs
    {
        public MultiplayerAutoLoadStateChangedEventArgs(
            MultiplayerAutoLoadState previousState,
            MultiplayerAutoLoadStatus status)
        {
            PreviousState = previousState;
            Status = status;
        }

        public readonly MultiplayerAutoLoadState PreviousState;
        public readonly MultiplayerAutoLoadStatus Status;
    }

    internal sealed class MultiplayerAutoLoadOptions
    {
        public int PanelTimeoutMilliseconds = 20000;
        public int LoadingTimeoutMilliseconds = 90000;
        public int RetryIntervalMilliseconds = 750;
        public int MaxRetriesPerState = 4;

        public static MultiplayerAutoLoadOptions Default()
        {
            return new MultiplayerAutoLoadOptions();
        }
    }
}
