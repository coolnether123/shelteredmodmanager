using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ShelteredAPI.Events;
using ShelteredAPI.Networking.Diagnostics;
using ShelteredAPI.Networking.Setup;
using ShelteredAPI.Saves;
using ShelteredAPI.Saves.Paging;
using UnityEngine;

namespace ShelteredAPI.Harmony
{
    internal static class AutoLoadFlow
    {
        // Sentinel value from Manager: AutoLoadSaveSlot=-1 means "start a new game in lowest free slot".
        public const int NewSaveSentinel = -1;

        private static readonly MultiplayerAutoLoadFlow Flow = new MultiplayerAutoLoadFlow();
        private static bool _sessionStarted;

        static AutoLoadFlow()
        {
            Flow.StateChanged += OnFlowStateChanged;
            GameEvents.OnSessionStarted += OnSessionStarted;
        }

        public static event Action<MultiplayerAutoLoadStatus> StatusChanged;

        public static bool PendingNewSave
        {
            get { return Flow.Status.IsActive; }
        }

        public static bool MainMenuAdvanceIssued
        {
            get { return HasReached(MultiplayerAutoLoadState.WaitingForGameModeSelection); }
        }

        public static bool ModeChosen
        {
            get { return HasReached(MultiplayerAutoLoadState.WaitingForSlotSelection); }
        }

        public static bool SlotChosen
        {
            get { return HasReached(MultiplayerAutoLoadState.WaitingForLoadingScene); }
        }

        public static int PreferredNewSaveSlot
        {
            get { return Flow.Status.TargetSlot; }
        }

        public static MultiplayerAutoLoadStatus CurrentStatus
        {
            get { return Flow.Status; }
        }

        public static void BeginNewSave()
        {
            BeginNewSave(0);
        }

        public static void BeginNewSave(int preferredAbsoluteSlot)
        {
            _sessionStarted = false;
            Flow.Start(preferredAbsoluteSlot, CaptureClock(), "auto-new-save requested");
        }

        public static bool TryAdvanceFromActiveMainMenu(string reason)
        {
            return Advance(CreateContext(null, null, null), reason);
        }

        public static bool TryAdvanceFromMainMenu(MainMenu mainMenu, string reason)
        {
            return Advance(CreateContext(mainMenu, null, null), reason);
        }

        public static bool TryAdvanceFromGameModeSelection(GameModeSelectionPanel panel, string reason)
        {
            return Advance(CreateContext(null, panel, null), reason);
        }

        public static bool TryAdvanceFromSlotSelection(SlotSelectionPanel panel, string reason)
        {
            return Advance(CreateContext(null, null, panel), reason);
        }

        public static void NotifyManualCancellation(string reason)
        {
            if (!Flow.Status.IsActive)
                return;

            Flow.Cancel(CaptureClock(), string.IsNullOrEmpty(reason) ? "Auto-new-save flow cancelled by user navigation." : reason);
        }

        public static void Reset()
        {
            _sessionStarted = false;
            Flow.Reset();
        }

        private static bool Advance(UnityAutoLoadContext context, string reason)
        {
            if (context == null)
                return false;

            MultiplayerAutoLoadAction action = Flow.Tick(context.Environment, reason);
            if (action == null || !action.HasAction)
                return false;

            try
            {
                return ApplyAction(context, action);
            }
            catch (Exception ex)
            {
                Flow.NotifyActionFailed(CaptureClock(), action, ex.Message);
                MMLog.WriteError("[AutoLoad] Action failed: " + action.Kind + ". " + ex.Message);
                return false;
            }
        }

        private static bool ApplyAction(UnityAutoLoadContext context, MultiplayerAutoLoadAction action)
        {
            if (action.Kind == MultiplayerAutoLoadActionKind.PressPlay)
                return PressPlay(context, action);

            if (action.Kind == MultiplayerAutoLoadActionKind.ChooseSurvival)
                return ChooseSurvival(context, action);

            if (action.Kind == MultiplayerAutoLoadActionKind.ChooseSlot)
                return ChooseSlot(context, action);

            return false;
        }

        private static bool PressPlay(UnityAutoLoadContext context, MultiplayerAutoLoadAction action)
        {
            MainMenu menu = context.MainMenu;
            if (!IsMainMenuReady(menu))
            {
                Flow.NotifyActionFailed(CaptureClock(), action, "Main menu was not ready when Play was requested.");
                return false;
            }

            MMLog.WriteDebug("[AutoLoad] Pressing Play. Reason=" + action.DetailText);
            menu.OnPlayButtonPressed();
            return true;
        }

        private static bool ChooseSurvival(UnityAutoLoadContext context, MultiplayerAutoLoadAction action)
        {
            GameModeSelectionPanel panel = context.GameModeSelectionPanel;
            if (!IsGameModeSelectionReady(panel))
            {
                Flow.NotifyActionFailed(CaptureClock(), action, "Game mode selection panel was not ready.");
                return false;
            }

            MMLog.WriteDebug("[AutoLoad] Selecting Survival mode.");
            panel.OnSurvivalModeChosen();
            return true;
        }

        private static bool ChooseSlot(UnityAutoLoadContext context, MultiplayerAutoLoadAction action)
        {
            SlotSelectionPanel panel = context.SlotSelectionPanel;
            if (!IsSlotSelectionReady(panel))
            {
                Flow.NotifyActionFailed(CaptureClock(), action, "Slot selection panel was not ready.");
                return false;
            }

            int targetSlot = ResolveTargetSurvivalSlot(action.TargetSlot);
            int targetPage;
            int targetIndex;
            ResolveSlotPageAndIndex(targetSlot, out targetPage, out targetIndex);

            if (!MoveToPage(panel, targetPage))
            {
                Flow.NotifyActionFailed(CaptureClock(), action, "Could not reach save-slot page " + targetPage + ".");
                return false;
            }

            Traverse.Create(panel).Field("m_selectedSlot").SetValue(targetIndex);
            Flow.UpdateTargetSlot(targetSlot, CaptureClock(), "Target save slot resolved");
            MMLog.Write("[AutoLoad] Starting New Save in slot " + targetSlot
                + " (page " + targetPage + ", index " + targetIndex + ").");

            panel.OnSlotChosen();
            return true;
        }

        private static UnityAutoLoadContext CreateContext(
            MainMenu mainMenu,
            GameModeSelectionPanel gameModeSelectionPanel,
            SlotSelectionPanel slotSelectionPanel)
        {
            MainMenu menu = mainMenu ?? FindActiveObject<MainMenu>();
            GameModeSelectionPanel modePanel = gameModeSelectionPanel ?? FindActiveObject<GameModeSelectionPanel>();
            SlotSelectionPanel slotPanel = slotSelectionPanel ?? FindActiveObject<SlotSelectionPanel>();
            CustomisationPanel customisationPanel = FindActiveObject<CustomisationPanel>();
            string sceneName = SafeActiveSceneName();

            MultiplayerAutoLoadEnvironment env = new MultiplayerAutoLoadEnvironment(CaptureClock());
            env.MainMenuReady = IsMainMenuReady(menu);
            env.GameModeSelectionReady = IsGameModeSelectionReady(modePanel);
            env.SlotSelectionReady = IsSlotSelectionReady(slotPanel);
            env.CustomisationPanelActive = customisationPanel != null;
            env.SceneName = sceneName;
            env.LoadingSceneActive = IsLoadingSceneActive(sceneName);
            env.ShelterSceneActive = IsShelterScene(sceneName);
            env.SessionStarted = _sessionStarted;

            return new UnityAutoLoadContext(env, menu, modePanel, slotPanel);
        }

        private static T FindActiveObject<T>() where T : UnityEngine.Object
        {
            try
            {
                return UnityEngine.Object.FindObjectOfType<T>();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsMainMenuReady(MainMenu instance)
        {
            if (instance == null)
                return false;

            TweenAlpha tween = GetTween(instance, typeof(MainMenu), "m_tween");
            if (tween != null && tween.direction == AnimationOrTween.Direction.Reverse)
                return false;

            bool inputEnabled = GetBooleanField(instance, typeof(MainMenu), "m_inputEnabled", false);
            bool userSignedOut = GetBooleanField(instance, typeof(MainMenu), "m_userSignedOut", false);
            return inputEnabled && !userSignedOut;
        }

        private static bool IsGameModeSelectionReady(GameModeSelectionPanel instance)
        {
            if (instance == null)
                return false;

            TweenAlpha tween = GetTween(instance, typeof(GameModeSelectionPanel), "m_tween");
            if (tween != null && tween.direction == AnimationOrTween.Direction.Reverse)
                return false;

            return GetBooleanField(instance, typeof(GameModeSelectionPanel), "m_inputEnabled", false);
        }

        private static bool IsSlotSelectionReady(SlotSelectionPanel instance)
        {
            if (instance == null || !instance.m_inputEnabled || SaveManager.instance == null)
                return false;

            TweenAlpha tween = GetTween(instance, typeof(SlotSelectionPanel), "m_tween");
            return tween == null || tween.direction != AnimationOrTween.Direction.Reverse;
        }

        private static TweenAlpha GetTween(object instance, Type type, string fieldName)
        {
            if (instance == null)
                return null;

            FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null ? field.GetValue(instance) as TweenAlpha : null;
        }

        private static bool GetBooleanField(object instance, Type type, string fieldName, bool fallback)
        {
            if (instance == null)
                return fallback;

            try
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                    return fallback;

                object value = field.GetValue(instance);
                return value is bool ? (bool)value : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static int ResolveTargetSurvivalSlot(int preferredSlot)
        {
            if (preferredSlot > 0 && IsSurvivalSlotAvailable(preferredSlot))
                return preferredSlot;

            return FindLowestAvailableSurvivalSlot();
        }

        private static bool IsSurvivalSlotAvailable(int slot)
        {
            if (slot <= 0)
                return false;

            if (slot <= 3)
                return SaveRegistryCore.ReadVanillaSaveInfo(slot) == null;

            return ExpandedVanillaSaves.GetBySlot(slot) == null;
        }

        private static int FindLowestAvailableSurvivalSlot()
        {
            for (int slot = 1; slot <= 3; slot++)
            {
                if (SaveRegistryCore.ReadVanillaSaveInfo(slot) == null)
                    return slot;
            }

            int customSlot = 4;
            while (ExpandedVanillaSaves.GetBySlot(customSlot) != null)
                customSlot++;

            return customSlot;
        }

        private static void ResolveSlotPageAndIndex(int absoluteSlot, out int targetPage, out int targetIndex)
        {
            if (absoluteSlot <= 3)
            {
                targetPage = 0;
                targetIndex = absoluteSlot - 1;
                return;
            }

            int customOffset = absoluteSlot - 4;
            targetPage = (customOffset / 3) + 1;
            targetIndex = customOffset % 3;
        }

        private static bool MoveToPage(SlotSelectionPanel panel, int targetPage)
        {
            int currentPage = PagingManager.GetPage(panel);
            while (currentPage < targetPage)
            {
                int before = currentPage;
                PagingManager.ChangePage(panel, 1);
                currentPage = PagingManager.GetPage(panel);
                if (currentPage == before)
                    break;
            }

            while (currentPage > targetPage)
            {
                int before = currentPage;
                PagingManager.ChangePage(panel, -1);
                currentPage = PagingManager.GetPage(panel);
                if (currentPage == before)
                    break;
            }

            return currentPage == targetPage;
        }

        private static bool IsLoadingSceneActive(string sceneName)
        {
            if (string.Equals(sceneName, "LoadingScene", StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                if (!string.IsNullOrEmpty(LoadingScreen.nextLevel))
                    return true;
                if (LoadingScreen.Instance != null && LoadingScreen.Instance.isShowing)
                    return true;
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool IsShelterScene(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName)
                && sceneName.StartsWith("ShelterScene", StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeActiveSceneName()
        {
            string sceneName;
            return RuntimeCompat.TryGetActiveSceneName(out sceneName) ? sceneName : string.Empty;
        }

        private static MultiplayerAutoLoadClockSnapshot CaptureClock()
        {
            int frame = 0;
            int milliseconds = 0;

            try
            {
                frame = Time.frameCount;
                milliseconds = (int)(Time.realtimeSinceStartup * 1000f);
            }
            catch
            {
                // Unity time can be unavailable in standalone contract tests.
            }

            return new MultiplayerAutoLoadClockSnapshot(frame, milliseconds);
        }

        private static void OnSessionStarted()
        {
            _sessionStarted = true;
            Flow.MarkLoaded(CaptureClock(), "GameTime.Awake session started");
        }

        private static void OnFlowStateChanged(object sender, MultiplayerAutoLoadStateChangedEventArgs e)
        {
            if (e == null || e.Status == null)
                return;

            MultiplayerAutoLoadStatus status = e.Status;
            if (e.PreviousState == status.CurrentState)
                return;

            string detail = status.DetailText;
            if (!string.IsNullOrEmpty(status.LastError))
                detail = detail + " error=" + status.LastError;
            if (status.TargetSlot > 0)
                detail = detail + " targetSlot=" + status.TargetSlot;
            if (status.RetryCount > 0)
                detail = detail + " retries=" + status.RetryCount;

            MMLog.WriteWithSource(
                string.IsNullOrEmpty(status.LastError) ? MMLog.LogLevel.Info : MMLog.LogLevel.Error,
                MMLog.LogCategory.Network,
                "ShelteredAPI.AutoLoad",
                status.CurrentState + ": " + detail);

            try
            {
                ShelteredMultiplayerTimeline.Instance.AppendAutoLoadStateChanged(status.CurrentState.ToString(), detail);
            }
            catch
            {
                // Timeline diagnostics are best-effort and must not affect menu automation.
            }

            Action<MultiplayerAutoLoadStatus> handler = StatusChanged;
            if (handler != null)
                handler(status);
        }

        private static bool HasReached(MultiplayerAutoLoadState state)
        {
            MultiplayerAutoLoadState current = Flow.Status.CurrentState;
            return current >= state
                && current != MultiplayerAutoLoadState.Failed
                && current != MultiplayerAutoLoadState.Cancelled;
        }

        private sealed class UnityAutoLoadContext
        {
            public UnityAutoLoadContext(
                MultiplayerAutoLoadEnvironment environment,
                MainMenu mainMenu,
                GameModeSelectionPanel gameModeSelectionPanel,
                SlotSelectionPanel slotSelectionPanel)
            {
                Environment = environment;
                MainMenu = mainMenu;
                GameModeSelectionPanel = gameModeSelectionPanel;
                SlotSelectionPanel = slotSelectionPanel;
            }

            public readonly MultiplayerAutoLoadEnvironment Environment;
            public readonly MainMenu MainMenu;
            public readonly GameModeSelectionPanel GameModeSelectionPanel;
            public readonly SlotSelectionPanel SlotSelectionPanel;
        }
    }
}
