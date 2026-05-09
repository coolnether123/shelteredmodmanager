using System;

namespace ShelteredAPI.Networking.Setup
{
    internal sealed class MultiplayerAutoLoadFlow
    {
        private readonly MultiplayerAutoLoadOptions _options;

        private MultiplayerAutoLoadState _state;
        private int _targetSlot;
        private int _entryFrame;
        private int _entryMilliseconds;
        private int _lastRetryMilliseconds;
        private int _retryCount;
        private string _detailText = "No auto-load or auto-new-save flow is pending.";
        private string _lastError = string.Empty;
        private string _lastAction = string.Empty;

        public MultiplayerAutoLoadFlow()
            : this(MultiplayerAutoLoadOptions.Default())
        {
        }

        public MultiplayerAutoLoadFlow(MultiplayerAutoLoadOptions options)
        {
            _options = options ?? MultiplayerAutoLoadOptions.Default();
            _state = MultiplayerAutoLoadState.Idle;
        }

        public event EventHandler<MultiplayerAutoLoadStateChangedEventArgs> StateChanged;

        public MultiplayerAutoLoadStatus Status
        {
            get { return BuildStatus(); }
        }

        public void Start(int preferredAbsoluteSlot, MultiplayerAutoLoadClockSnapshot clock, string reason)
        {
            _targetSlot = preferredAbsoluteSlot > 0 ? preferredAbsoluteSlot : 0;
            _lastError = string.Empty;
            _lastAction = string.Empty;
            Enter(
                MultiplayerAutoLoadState.SetupReceived,
                clock,
                BuildTargetDetail("Setup received; auto-new-save state machine started.", _targetSlot),
                string.Empty);
        }

        public MultiplayerAutoLoadAction Tick(MultiplayerAutoLoadEnvironment environment, string reason)
        {
            MultiplayerAutoLoadEnvironment env = environment ?? new MultiplayerAutoLoadEnvironment(new MultiplayerAutoLoadClockSnapshot(0, 0));

            if (IsTerminal(_state))
                return MultiplayerAutoLoadAction.None();

            if (TryMarkLoadedFromEnvironment(env, reason))
                return MultiplayerAutoLoadAction.None();

            if (_state == MultiplayerAutoLoadState.SetupReceived)
            {
                Enter(
                    MultiplayerAutoLoadState.WaitingForMainMenu,
                    env.Clock,
                    "Waiting for the main menu before opening Play.",
                    string.Empty);
            }

            switch (_state)
            {
                case MultiplayerAutoLoadState.WaitingForMainMenu:
                    return TickWaitingForMainMenu(env, reason);

                case MultiplayerAutoLoadState.WaitingForGameModeSelection:
                    return TickWaitingForGameModeSelection(env, reason);

                case MultiplayerAutoLoadState.WaitingForSlotSelection:
                    return TickWaitingForSlotSelection(env, reason);

                case MultiplayerAutoLoadState.SelectingSurvival:
                    if (env.GameModeSelectionReady)
                        return BuildChooseSurvivalAction(env.Clock, false);
                    return WaitOrTimeout(env, reason);

                case MultiplayerAutoLoadState.SelectingSuggestedSlot:
                    if (env.SlotSelectionReady)
                        return BuildChooseSlotAction(env.Clock, false);
                    return WaitOrTimeout(env, reason);

                case MultiplayerAutoLoadState.WaitingForLoadingScene:
                    return TickWaitingForLoadingScene(env, reason);

                case MultiplayerAutoLoadState.WaitingForShelterScene:
                    return TickWaitingForShelterScene(env, reason);

                default:
                    return WaitOrTimeout(env, reason);
            }
        }

        public void MarkLoaded(MultiplayerAutoLoadClockSnapshot clock, string reason)
        {
            if (_state == MultiplayerAutoLoadState.Idle
                || _state == MultiplayerAutoLoadState.Loaded
                || _state == MultiplayerAutoLoadState.Failed
                || _state == MultiplayerAutoLoadState.Cancelled)
            {
                return;
            }

            Enter(
                MultiplayerAutoLoadState.Loaded,
                clock,
                "Shelter session started; auto-new-save flow is loaded.",
                string.Empty);
        }

        public void Cancel(MultiplayerAutoLoadClockSnapshot clock, string reason)
        {
            _targetSlot = 0;
            _lastError = string.Empty;
            _lastAction = string.Empty;
            Enter(
                MultiplayerAutoLoadState.Cancelled,
                clock,
                string.IsNullOrEmpty(reason) ? "Auto-new-save flow cancelled." : reason,
                string.Empty);
        }

        public void Reset()
        {
            _state = MultiplayerAutoLoadState.Idle;
            _targetSlot = 0;
            _entryFrame = 0;
            _entryMilliseconds = 0;
            _lastRetryMilliseconds = 0;
            _retryCount = 0;
            _detailText = "No auto-load or auto-new-save flow is pending.";
            _lastError = string.Empty;
            _lastAction = string.Empty;
        }

        public void UpdateTargetSlot(int absoluteSlot, MultiplayerAutoLoadClockSnapshot clock, string reason)
        {
            if (absoluteSlot <= 0)
                return;

            _targetSlot = absoluteSlot;
            _detailText = BuildTargetDetail(string.IsNullOrEmpty(reason) ? "Target save slot resolved." : reason, _targetSlot);
            RaiseStateChanged(_state, BuildStatus());
        }

        public void NotifyActionFailed(MultiplayerAutoLoadClockSnapshot clock, MultiplayerAutoLoadAction action, string reason)
        {
            string actionText = action != null ? action.Kind.ToString() : "unknown action";
            Fail(clock, "Auto-load action failed: " + actionText + ". " + (reason ?? string.Empty));
        }

        private MultiplayerAutoLoadAction TickWaitingForMainMenu(MultiplayerAutoLoadEnvironment env, string reason)
        {
            if (AdoptForwardProgress(env, reason))
                return Tick(env, reason);

            if (env.MainMenuReady)
                return BuildPressPlayAction(env.Clock, false);

            return WaitOrTimeout(env, reason);
        }

        private MultiplayerAutoLoadAction TickWaitingForGameModeSelection(MultiplayerAutoLoadEnvironment env, string reason)
        {
            if (AdoptForwardProgress(env, reason))
                return Tick(env, reason);

            if (env.MainMenuReady && ShouldRetry(env.Clock))
                return BuildPressPlayAction(env.Clock, true);

            return WaitOrTimeout(env, reason);
        }

        private MultiplayerAutoLoadAction TickWaitingForSlotSelection(MultiplayerAutoLoadEnvironment env, string reason)
        {
            if (AdoptForwardProgress(env, reason))
                return Tick(env, reason);

            if (env.MainMenuReady)
            {
                Fail(env.Clock, "Manual navigation returned to the main menu while waiting for slot selection.");
                return MultiplayerAutoLoadAction.None();
            }

            if (env.GameModeSelectionReady && ShouldRetry(env.Clock))
                return BuildChooseSurvivalAction(env.Clock, true);

            return WaitOrTimeout(env, reason);
        }

        private MultiplayerAutoLoadAction TickWaitingForLoadingScene(MultiplayerAutoLoadEnvironment env, string reason)
        {
            if (TryMarkLoadedFromEnvironment(env, reason))
                return MultiplayerAutoLoadAction.None();

            if (env.LoadingSceneActive || env.ShelterSceneActive)
            {
                Enter(
                    MultiplayerAutoLoadState.WaitingForShelterScene,
                    env.Clock,
                    BuildSceneDetail("Loading started; waiting for the shelter scene and session start.", env),
                    string.Empty);
                return MultiplayerAutoLoadAction.None();
            }

            if (env.MainMenuReady || env.GameModeSelectionReady)
            {
                Fail(env.Clock, "Manual navigation left the slot flow after a save slot was selected.");
                return MultiplayerAutoLoadAction.None();
            }

            if (env.SlotSelectionReady && ShouldRetry(env.Clock))
                return BuildChooseSlotAction(env.Clock, true);

            if (env.CustomisationPanelActive)
            {
                RecordWaitRetry(env.Clock);
                _detailText = "Slot selected; waiting for customization completion and loading scene.";
                return CheckTimeout(env.Clock);
            }

            return WaitOrTimeout(env, reason);
        }

        private MultiplayerAutoLoadAction TickWaitingForShelterScene(MultiplayerAutoLoadEnvironment env, string reason)
        {
            if (TryMarkLoadedFromEnvironment(env, reason))
                return MultiplayerAutoLoadAction.None();

            if (env.ShelterSceneActive)
                _detailText = "Shelter scene is active; waiting for session start.";
            else if (env.LoadingSceneActive)
                _detailText = BuildSceneDetail("Loading scene active; waiting for shelter scene.", env);

            return WaitOrTimeout(env, reason);
        }

        private bool AdoptForwardProgress(MultiplayerAutoLoadEnvironment env, string reason)
        {
            if (env.LoadingSceneActive || env.ShelterSceneActive)
            {
                Enter(
                    MultiplayerAutoLoadState.WaitingForShelterScene,
                    env.Clock,
                    BuildSceneDetail("Observed loading progress; waiting for shelter scene.", env),
                    string.Empty);
                return true;
            }

            if ((_state == MultiplayerAutoLoadState.WaitingForMainMenu
                    || _state == MultiplayerAutoLoadState.WaitingForGameModeSelection
                    || _state == MultiplayerAutoLoadState.WaitingForSlotSelection)
                && env.SlotSelectionReady)
            {
                return EnterSlotSelection(env.Clock, "Slot selection is ready.");
            }

            if ((_state == MultiplayerAutoLoadState.WaitingForMainMenu
                    || _state == MultiplayerAutoLoadState.WaitingForGameModeSelection)
                && env.GameModeSelectionReady)
            {
                Enter(
                    MultiplayerAutoLoadState.SelectingSurvival,
                    env.Clock,
                    "Game mode selection is ready; selecting Survival.",
                    "choose Survival");
                return true;
            }

            return false;
        }

        private bool EnterSlotSelection(MultiplayerAutoLoadClockSnapshot clock, string detail)
        {
            Enter(
                MultiplayerAutoLoadState.SelectingSuggestedSlot,
                clock,
                BuildTargetDetail(detail, _targetSlot),
                "choose suggested slot");
            return true;
        }

        private MultiplayerAutoLoadAction BuildPressPlayAction(MultiplayerAutoLoadClockSnapshot clock, bool retry)
        {
            Enter(
                MultiplayerAutoLoadState.OpeningPlay,
                clock,
                retry ? "Retrying explicit Play button transition." : "Opening Play from the main menu.",
                "press Play");
            Enter(
                MultiplayerAutoLoadState.WaitingForGameModeSelection,
                clock,
                "Play pressed; waiting for game mode selection.",
                "press Play");
            return MultiplayerAutoLoadAction.PressPlay("Press Play on the main menu.");
        }

        private MultiplayerAutoLoadAction BuildChooseSurvivalAction(MultiplayerAutoLoadClockSnapshot clock, bool retry)
        {
            Enter(
                MultiplayerAutoLoadState.SelectingSurvival,
                clock,
                retry ? "Retrying Survival selection." : "Selecting Survival mode.",
                "choose Survival");
            Enter(
                MultiplayerAutoLoadState.WaitingForSlotSelection,
                clock,
                "Survival selected; waiting for slot selection.",
                "choose Survival");
            return MultiplayerAutoLoadAction.ChooseSurvival("Choose Survival mode.");
        }

        private MultiplayerAutoLoadAction BuildChooseSlotAction(MultiplayerAutoLoadClockSnapshot clock, bool retry)
        {
            Enter(
                MultiplayerAutoLoadState.SelectingSuggestedSlot,
                clock,
                retry
                    ? BuildTargetDetail("Retrying save slot selection.", _targetSlot)
                    : BuildTargetDetail("Selecting suggested save slot.", _targetSlot),
                "choose suggested slot");
            Enter(
                MultiplayerAutoLoadState.WaitingForLoadingScene,
                clock,
                "Save slot selected; waiting for loading scene or customization completion.",
                "choose suggested slot");
            return MultiplayerAutoLoadAction.ChooseSlot(_targetSlot, "Choose suggested save slot.");
        }

        private MultiplayerAutoLoadAction WaitOrTimeout(MultiplayerAutoLoadEnvironment env, string reason)
        {
            RecordWaitRetry(env.Clock);
            return CheckTimeout(env.Clock);
        }

        private MultiplayerAutoLoadAction CheckTimeout(MultiplayerAutoLoadClockSnapshot clock)
        {
            if (!HasTimedOut(clock))
                return MultiplayerAutoLoadAction.None();

            Fail(clock, BuildTimeoutFailure());
            return MultiplayerAutoLoadAction.None();
        }

        private bool TryMarkLoadedFromEnvironment(MultiplayerAutoLoadEnvironment env, string reason)
        {
            if (!env.SessionStarted || !env.ShelterSceneActive)
                return false;

            MarkLoaded(env.Clock, reason);
            return true;
        }

        private bool ShouldRetry(MultiplayerAutoLoadClockSnapshot clock)
        {
            if (_retryCount >= SafeMaxRetries())
                return false;

            int elapsed = clock.Milliseconds - _lastRetryMilliseconds;
            if (elapsed < SafeRetryInterval())
                return false;

            _retryCount++;
            _lastRetryMilliseconds = clock.Milliseconds;
            return true;
        }

        private void RecordWaitRetry(MultiplayerAutoLoadClockSnapshot clock)
        {
            if (_retryCount >= SafeMaxRetries())
                return;

            int elapsed = clock.Milliseconds - _lastRetryMilliseconds;
            if (elapsed < SafeRetryInterval())
                return;

            _retryCount++;
            _lastRetryMilliseconds = clock.Milliseconds;
            RaiseStateChanged(_state, BuildStatus());
        }

        private bool HasTimedOut(MultiplayerAutoLoadClockSnapshot clock)
        {
            int timeout = GetTimeoutMilliseconds(_state);
            if (timeout <= 0)
                return false;

            return clock.Milliseconds - _entryMilliseconds >= timeout;
        }

        private int GetTimeoutMilliseconds(MultiplayerAutoLoadState state)
        {
            if (state == MultiplayerAutoLoadState.WaitingForLoadingScene
                || state == MultiplayerAutoLoadState.WaitingForShelterScene)
            {
                return _options.LoadingTimeoutMilliseconds > 0 ? _options.LoadingTimeoutMilliseconds : 90000;
            }

            return _options.PanelTimeoutMilliseconds > 0 ? _options.PanelTimeoutMilliseconds : 20000;
        }

        private int SafeRetryInterval()
        {
            return _options.RetryIntervalMilliseconds > 0 ? _options.RetryIntervalMilliseconds : 750;
        }

        private int SafeMaxRetries()
        {
            return _options.MaxRetriesPerState >= 0 ? _options.MaxRetriesPerState : 4;
        }

        private void Fail(MultiplayerAutoLoadClockSnapshot clock, string error)
        {
            _lastError = error ?? string.Empty;
            _targetSlot = 0;
            Enter(
                MultiplayerAutoLoadState.Failed,
                clock,
                "Auto-new-save flow failed.",
                string.Empty);
        }

        private void Enter(
            MultiplayerAutoLoadState nextState,
            MultiplayerAutoLoadClockSnapshot clock,
            string detail,
            string action)
        {
            MultiplayerAutoLoadState previous = _state;
            bool changed = previous != nextState;

            _state = nextState;
            _detailText = detail ?? string.Empty;
            if (!string.IsNullOrEmpty(action))
                _lastAction = action;

            if (changed)
            {
                _entryFrame = clock.Frame;
                _entryMilliseconds = clock.Milliseconds;
                _lastRetryMilliseconds = clock.Milliseconds;
                _retryCount = 0;
            }

            RaiseStateChanged(previous, BuildStatus());
        }

        private void RaiseStateChanged(MultiplayerAutoLoadState previous, MultiplayerAutoLoadStatus status)
        {
            EventHandler<MultiplayerAutoLoadStateChangedEventArgs> handler = StateChanged;
            if (handler == null)
                return;

            handler(this, new MultiplayerAutoLoadStateChangedEventArgs(previous, status));
        }

        private MultiplayerAutoLoadStatus BuildStatus()
        {
            return new MultiplayerAutoLoadStatus(
                _state,
                _detailText,
                _lastError,
                _retryCount,
                _targetSlot,
                _entryFrame,
                _entryMilliseconds,
                GetExpectedCondition(_state),
                _lastAction);
        }

        private string BuildTimeoutFailure()
        {
            string expected = GetExpectedCondition(_state);
            if (_state == MultiplayerAutoLoadState.WaitingForLoadingScene)
                return "Timed out waiting for loading scene after slot selection. If customization opened, finish it or cancel setup.";
            if (_state == MultiplayerAutoLoadState.WaitingForShelterScene)
                return "Timed out waiting for shelter scene and session start.";

            return "Timed out waiting for " + (string.IsNullOrEmpty(expected) ? _state.ToString() : expected) + ".";
        }

        private static string GetExpectedCondition(MultiplayerAutoLoadState state)
        {
            switch (state)
            {
                case MultiplayerAutoLoadState.SetupReceived:
                case MultiplayerAutoLoadState.WaitingForMainMenu:
                    return "main menu ready";
                case MultiplayerAutoLoadState.OpeningPlay:
                    return "main menu Play button";
                case MultiplayerAutoLoadState.WaitingForGameModeSelection:
                    return "game mode selection panel ready";
                case MultiplayerAutoLoadState.SelectingSurvival:
                    return "Survival game mode button";
                case MultiplayerAutoLoadState.WaitingForSlotSelection:
                    return "slot selection panel ready";
                case MultiplayerAutoLoadState.SelectingSuggestedSlot:
                    return "suggested survival save slot ready";
                case MultiplayerAutoLoadState.WaitingForLoadingScene:
                    return "loading scene or customization completion";
                case MultiplayerAutoLoadState.WaitingForShelterScene:
                    return "shelter scene and session start";
                case MultiplayerAutoLoadState.Loaded:
                    return "loaded";
                case MultiplayerAutoLoadState.Failed:
                    return "failed";
                case MultiplayerAutoLoadState.Cancelled:
                    return "cancelled";
                default:
                    return string.Empty;
            }
        }

        private static string BuildTargetDetail(string detail, int targetSlot)
        {
            if (targetSlot > 0)
                return detail + " Target slot " + targetSlot + ".";

            return detail + " Target slot is the lowest available survival slot.";
        }

        private static string BuildSceneDetail(string detail, MultiplayerAutoLoadEnvironment env)
        {
            string scene = env != null ? env.SceneName : string.Empty;
            if (string.IsNullOrEmpty(scene))
                return detail;

            return detail + " Scene=" + scene + ".";
        }

        private static bool IsTerminal(MultiplayerAutoLoadState state)
        {
            return state == MultiplayerAutoLoadState.Idle
                || state == MultiplayerAutoLoadState.Loaded
                || state == MultiplayerAutoLoadState.Failed
                || state == MultiplayerAutoLoadState.Cancelled;
        }
    }
}
