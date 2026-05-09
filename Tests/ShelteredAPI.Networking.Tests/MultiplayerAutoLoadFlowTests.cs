using System;
using System.Collections.Generic;
using ShelteredAPI.Networking.Setup;

namespace ShelteredAPI.Networking.Tests
{
    internal static class MultiplayerAutoLoadFlowTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("AutoLoadFlow_InitialSetupStartsExpectedState", InitialSetupStartsExpectedState));
            tests.Add(new TestCase("AutoLoadFlow_MissingPanelWaitsAndRetries", MissingPanelWaitsAndRetries));
            tests.Add(new TestCase("AutoLoadFlow_TimeoutProducesFailure", TimeoutProducesFailure));
            tests.Add(new TestCase("AutoLoadFlow_SuccessfulSequenceReachesLoaded", SuccessfulSequenceReachesLoaded));
            tests.Add(new TestCase("AutoLoadFlow_CancellationClearsState", CancellationClearsState));
        }

        private static void InitialSetupStartsExpectedState()
        {
            MultiplayerAutoLoadFlow flow = CreateFlow();

            flow.Start(7, Clock(1, 0), "test");
            MultiplayerAutoLoadStatus status = flow.Status;

            TestAssert.Equal(MultiplayerAutoLoadState.SetupReceived, status.CurrentState, "Setup should enter the setup-received state.");
            TestAssert.Equal(7, status.TargetSlot, "Setup should remember the suggested target slot.");
            TestAssert.Equal("main menu ready", status.ExpectedCondition, "Setup should publish the first expected condition.");
            TestAssert.True(status.IsActive, "Setup should be active after setup is received.");
        }

        private static void MissingPanelWaitsAndRetries()
        {
            MultiplayerAutoLoadFlow flow = CreateFlow();
            flow.Start(0, Clock(1, 0), "test");

            MultiplayerAutoLoadAction first = flow.Tick(Env(2, 0), "no-panel");
            TestAssert.Equal(MultiplayerAutoLoadActionKind.None, first.Kind, "Missing main menu should not emit an action.");
            TestAssert.Equal(MultiplayerAutoLoadState.WaitingForMainMenu, flow.Status.CurrentState, "Flow should wait for the main menu.");

            MultiplayerAutoLoadAction second = flow.Tick(Env(3, 60), "retry-wait");
            TestAssert.Equal(MultiplayerAutoLoadActionKind.None, second.Kind, "Missing main menu retry should still avoid hidden clicks.");
            TestAssert.True(flow.Status.RetryCount > 0, "Missing expected panel should increment retry diagnostics.");
            TestAssert.Equal(MultiplayerAutoLoadState.WaitingForMainMenu, flow.Status.CurrentState, "Flow should keep waiting without failing before timeout.");
        }

        private static void TimeoutProducesFailure()
        {
            MultiplayerAutoLoadFlow flow = CreateFlow();
            flow.Start(0, Clock(1, 0), "test");

            flow.Tick(Env(2, 0), "no-panel");
            flow.Tick(Env(3, 250), "timeout");

            TestAssert.Equal(MultiplayerAutoLoadState.Failed, flow.Status.CurrentState, "Timeout should fail the flow.");
            AssertContains(flow.Status.LastError, "main menu", "Failure should name the missing expected state.");
            TestAssert.False(flow.Status.IsActive, "Failed flow should no longer be active.");
        }

        private static void SuccessfulSequenceReachesLoaded()
        {
            MultiplayerAutoLoadFlow flow = CreateFlow();
            flow.Start(5, Clock(1, 0), "test");

            MultiplayerAutoLoadAction play = flow.Tick(Env(2, 10, true, false, false, false, false, false), "main-menu");
            TestAssert.Equal(MultiplayerAutoLoadActionKind.PressPlay, play.Kind, "Main menu should produce a Play action.");
            TestAssert.Equal(MultiplayerAutoLoadState.WaitingForGameModeSelection, flow.Status.CurrentState, "Play should move to game mode wait.");

            MultiplayerAutoLoadAction survival = flow.Tick(Env(3, 20, false, true, false, false, false, false), "game-mode");
            TestAssert.Equal(MultiplayerAutoLoadActionKind.ChooseSurvival, survival.Kind, "Game mode panel should produce Survival action.");
            TestAssert.Equal(MultiplayerAutoLoadState.WaitingForSlotSelection, flow.Status.CurrentState, "Survival should move to slot wait.");

            MultiplayerAutoLoadAction slot = flow.Tick(Env(4, 30, false, false, true, false, false, false), "slot");
            TestAssert.Equal(MultiplayerAutoLoadActionKind.ChooseSlot, slot.Kind, "Slot panel should produce a slot action.");
            TestAssert.Equal(MultiplayerAutoLoadState.WaitingForLoadingScene, flow.Status.CurrentState, "Slot selection should move to loading wait.");

            MultiplayerAutoLoadAction loading = flow.Tick(Env(5, 40, false, false, false, false, true, false), "loading");
            TestAssert.Equal(MultiplayerAutoLoadActionKind.None, loading.Kind, "Loading scene should require no action.");
            TestAssert.Equal(MultiplayerAutoLoadState.WaitingForShelterScene, flow.Status.CurrentState, "Loading scene should move to shelter wait.");

            MultiplayerAutoLoadAction loaded = flow.Tick(Env(6, 50, false, false, false, false, false, true, true), "session-started");
            TestAssert.Equal(MultiplayerAutoLoadActionKind.None, loaded.Kind, "Loaded state should require no action.");
            TestAssert.Equal(MultiplayerAutoLoadState.Loaded, flow.Status.CurrentState, "Shelter scene plus session start should mark the flow loaded.");
            TestAssert.False(flow.Status.IsActive, "Loaded flow should not keep issuing actions.");
        }

        private static void CancellationClearsState()
        {
            MultiplayerAutoLoadFlow flow = CreateFlow();
            flow.Start(4, Clock(1, 0), "test");

            flow.Cancel(Clock(2, 20), "test cancellation");

            TestAssert.Equal(MultiplayerAutoLoadState.Cancelled, flow.Status.CurrentState, "Cancel should enter the cancelled state.");
            TestAssert.Equal(0, flow.Status.TargetSlot, "Cancel should clear the target slot.");
            TestAssert.False(flow.Status.IsActive, "Cancelled flow should no longer be active.");
        }

        private static MultiplayerAutoLoadFlow CreateFlow()
        {
            MultiplayerAutoLoadOptions options = new MultiplayerAutoLoadOptions();
            options.PanelTimeoutMilliseconds = 200;
            options.LoadingTimeoutMilliseconds = 300;
            options.RetryIntervalMilliseconds = 50;
            options.MaxRetriesPerState = 3;
            return new MultiplayerAutoLoadFlow(options);
        }

        private static MultiplayerAutoLoadClockSnapshot Clock(int frame, int milliseconds)
        {
            return new MultiplayerAutoLoadClockSnapshot(frame, milliseconds);
        }

        private static MultiplayerAutoLoadEnvironment Env(int frame, int milliseconds)
        {
            return Env(frame, milliseconds, false, false, false, false, false, false, false);
        }

        private static MultiplayerAutoLoadEnvironment Env(
            int frame,
            int milliseconds,
            bool mainMenuReady,
            bool gameModeReady,
            bool slotReady,
            bool customisationActive,
            bool loadingSceneActive,
            bool shelterSceneActive)
        {
            return Env(
                frame,
                milliseconds,
                mainMenuReady,
                gameModeReady,
                slotReady,
                customisationActive,
                loadingSceneActive,
                shelterSceneActive,
                false);
        }

        private static MultiplayerAutoLoadEnvironment Env(
            int frame,
            int milliseconds,
            bool mainMenuReady,
            bool gameModeReady,
            bool slotReady,
            bool customisationActive,
            bool loadingSceneActive,
            bool shelterSceneActive,
            bool sessionStarted)
        {
            MultiplayerAutoLoadEnvironment env = new MultiplayerAutoLoadEnvironment(Clock(frame, milliseconds));
            env.MainMenuReady = mainMenuReady;
            env.GameModeSelectionReady = gameModeReady;
            env.SlotSelectionReady = slotReady;
            env.CustomisationPanelActive = customisationActive;
            env.LoadingSceneActive = loadingSceneActive;
            env.ShelterSceneActive = shelterSceneActive;
            env.SessionStarted = sessionStarted;
            env.SceneName = loadingSceneActive ? "LoadingScene" : (shelterSceneActive ? "ShelterScene" : "MenuScene");
            return env;
        }

        private static void AssertContains(string value, string expected, string message)
        {
            if ((value ?? string.Empty).IndexOf(expected, StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException(message + " Expected to find '" + expected + "' in '" + value + "'.");
        }
    }
}
