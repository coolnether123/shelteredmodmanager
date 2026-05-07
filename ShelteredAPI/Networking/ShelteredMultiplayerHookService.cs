using System;
using System.Collections.Generic;
using System.Threading;
using ModAPI.Core;
using ShelteredAPI.Events;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class ShelteredMultiplayerHookService : IShelteredMultiplayerHooks
    {
        private const int DefaultTickRate = 20;
        private const string LogSource = "ShelteredAPI.MultiplayerHooks";
        private static readonly ShelteredMultiplayerHookService _instance = new ShelteredMultiplayerHookService();

        private readonly object _sync = new object();
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private ShelteredMultiplayerSessionState _sessionState;
        private int _mainThreadId;
        private bool _gameEventsAttached;
        private int _queueWarningCount;

        private ShelteredMultiplayerHookService()
        {
            _sessionState = CreateInactiveState("inactive");
        }

        public static ShelteredMultiplayerHookService Instance
        {
            get { return _instance; }
        }

        public event Action<ShelteredMultiplayerHookContext> RuntimeUpdate;
        public event Action<ShelteredMultiplayerHookContext> BeforeGameTimeUpdate;
        public event Action<ShelteredMultiplayerHookContext> AfterGameTimeUpdate;
        public event Action<ShelteredMultiplayerHookContext> PauseRequested;
        public event Action<ShelteredMultiplayerHookContext> ResumeRequested;
        public event Action<ShelteredMultiplayerHookContext> BeforeSave;
        public event Action<ShelteredMultiplayerHookContext> BeforeLoadSceneContents;
        public event Action<ShelteredMultiplayerHookContext> AfterLoad;
        public event Action<ShelteredMultiplayerHookContext> SessionStarted;
        public event Action<ShelteredMultiplayerHookContext> NewGame;
        public event Action<ShelteredMultiplayerSnapshotContext> CaptureWorldSnapshotRequested;
        public event Action<ShelteredMultiplayerSnapshotContext> ApplyWorldSnapshotRequested;

        public ShelteredMultiplayerSessionState SessionState
        {
            get
            {
                lock (_sync)
                {
                    return _sessionState;
                }
            }
        }

        public bool IsMultiplayerActive
        {
            get { return SessionState.IsMultiplayerActive; }
        }

        public long CurrentWorldTick
        {
            get { return SessionState.WorldTick; }
        }

        public float CurrentWorldDeltaSeconds
        {
            get { return SessionState.WorldDeltaSeconds; }
        }

        public void EnsureInstalled()
        {
            if (_mainThreadId == 0)
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;

            AttachGameEvents();
        }

        public void ActivateHost(byte localPlayerId, string sessionId, int tickRate)
        {
            SetSessionState(new ShelteredMultiplayerSessionState(
                ShelteredMultiplayerSessionMode.Host,
                localPlayerId,
                sessionId,
                NormalizeTickRate(tickRate),
                CurrentWorldTick,
                CurrentWorldDeltaSeconds,
                ShelteredMultiplayerGameTimeMode.HostAuthoritative,
                true,
                "host-active"));
        }

        public void ActivateClient(byte localPlayerId, string sessionId, int tickRate)
        {
            SetSessionState(new ShelteredMultiplayerSessionState(
                ShelteredMultiplayerSessionMode.Client,
                localPlayerId,
                sessionId,
                NormalizeTickRate(tickRate),
                CurrentWorldTick,
                CurrentWorldDeltaSeconds,
                ShelteredMultiplayerGameTimeMode.RemoteAuthoritative,
                true,
                "client-active"));
        }

        public void Deactivate(string reason)
        {
            SetSessionState(CreateInactiveState(string.IsNullOrEmpty(reason) ? "inactive" : reason));
        }

        public void SetGameTimeMode(ShelteredMultiplayerGameTimeMode mode)
        {
            ShelteredMultiplayerSessionState current = SessionState;
            SetSessionState(new ShelteredMultiplayerSessionState(
                current.Mode,
                current.LocalPlayerId,
                current.SessionId,
                current.TickRate,
                current.WorldTick,
                current.WorldDeltaSeconds,
                mode,
                current.BlockVanillaPauseRequests,
                current.Status));
        }

        public void SetWorldTick(long worldTick, float worldDeltaSeconds)
        {
            if (worldTick < 0)
                worldTick = 0;
            if (worldDeltaSeconds < 0f)
                worldDeltaSeconds = 0f;

            ShelteredMultiplayerSessionState current = SessionState;
            SetSessionState(new ShelteredMultiplayerSessionState(
                current.Mode,
                current.LocalPlayerId,
                current.SessionId,
                current.TickRate,
                worldTick,
                worldDeltaSeconds,
                current.GameTimeMode,
                current.BlockVanillaPauseRequests,
                current.Status));
        }

        public void EnqueueMainThread(Action action)
        {
            if (action == null)
                return;

            if (IsMainThread)
            {
                SafeInvoke(action, "main-thread-inline");
                return;
            }

            lock (_sync)
            {
                _mainThreadQueue.Enqueue(action);
                if (_mainThreadQueue.Count > 256 && _queueWarningCount < 3)
                {
                    _queueWarningCount++;
                    MMLog.WriteWithSource(MMLog.LogLevel.Warning, MMLog.LogCategory.Network, LogSource,
                        "Main-thread queue contains " + _mainThreadQueue.Count + " actions.");
                }
            }
        }

        public string CaptureWorldSnapshot(string reason, object hostContext)
        {
            ShelteredMultiplayerSnapshotContext context = new ShelteredMultiplayerSnapshotContext(reason, hostContext);
            SafeRaise(CaptureWorldSnapshotRequested, context, "CaptureWorldSnapshotRequested");
            return context.Handled ? context.Payload : string.Empty;
        }

        public bool ApplyWorldSnapshot(string reason, string payload, object hostContext, out string error)
        {
            ShelteredMultiplayerSnapshotContext context = new ShelteredMultiplayerSnapshotContext(reason, hostContext);
            context.Payload = payload ?? string.Empty;
            SafeRaise(ApplyWorldSnapshotRequested, context, "ApplyWorldSnapshotRequested");
            error = context.Error ?? string.Empty;
            return context.Handled && string.IsNullOrEmpty(error);
        }

        internal void RuntimeUpdateTick()
        {
            EnsureInstalled();
            DrainMainThreadQueue();
            SafeRaise(RuntimeUpdate, CreateContext(ShelteredMultiplayerHookKind.RuntimeUpdate, "RuntimeDriver.Update", null), "RuntimeUpdate");
        }

        internal bool BeginGameTimeUpdate(GameTime gameTime)
        {
            ShelteredMultiplayerSessionState state = SessionState;
            if (state.IsMultiplayerActive)
                ForceRealtimeTimescale();

            ShelteredMultiplayerHookContext context = CreateContext(
                ShelteredMultiplayerHookKind.BeforeGameTimeUpdate,
                "GameTime.Update",
                gameTime);

            if (state.GameTimeMode == ShelteredMultiplayerGameTimeMode.RemoteAuthoritative)
                context.CancelVanilla = true;

            SafeRaise(BeforeGameTimeUpdate, context, "BeforeGameTimeUpdate");
            return !context.CancelVanilla;
        }

        internal void EndGameTimeUpdate(GameTime gameTime)
        {
            if (IsMultiplayerActive)
                ForceRealtimeTimescale();

            SafeRaise(
                AfterGameTimeUpdate,
                CreateContext(ShelteredMultiplayerHookKind.AfterGameTimeUpdate, "GameTime.Update", gameTime),
                "AfterGameTimeUpdate");
        }

        internal bool HandlePauseRequest(string source, object hostContext)
        {
            ShelteredMultiplayerSessionState state = SessionState;
            ShelteredMultiplayerHookContext context = CreateContext(
                ShelteredMultiplayerHookKind.PauseRequested,
                source,
                hostContext);

            if (state.IsMultiplayerActive && state.BlockVanillaPauseRequests)
                context.CancelVanilla = true;

            SafeRaise(PauseRequested, context, "PauseRequested");

            if (context.CancelVanilla)
                ForceRealtimeTimescale();

            return !context.CancelVanilla;
        }

        internal void HandleResumeRequest(string source, object hostContext)
        {
            if (IsMultiplayerActive)
                ForceRealtimeTimescale();

            SafeRaise(
                ResumeRequested,
                CreateContext(ShelteredMultiplayerHookKind.ResumeRequested, source, hostContext),
                "ResumeRequested");
        }

        private void AttachGameEvents()
        {
            lock (_sync)
            {
                if (_gameEventsAttached)
                    return;

                _gameEventsAttached = true;
            }

            GameEvents.OnBeforeSave += OnBeforeSave;
            GameEvents.OnBeforeLoadSceneContents += OnBeforeLoadSceneContents;
            GameEvents.OnAfterLoad += OnAfterLoad;
            GameEvents.OnSessionStarted += OnSessionStarted;
            GameEvents.OnNewGame += OnNewGame;
        }

        private void OnBeforeSave(SaveData data)
        {
            SafeRaise(BeforeSave, CreateContext(ShelteredMultiplayerHookKind.BeforeSave, "SaveManager.BeforeSave", data), "BeforeSave");
        }

        private void OnBeforeLoadSceneContents(SaveData data)
        {
            SafeRaise(BeforeLoadSceneContents, CreateContext(ShelteredMultiplayerHookKind.BeforeLoadSceneContents, "SaveManager.BeforeLoadSceneContents", data), "BeforeLoadSceneContents");
        }

        private void OnAfterLoad(SaveData data)
        {
            SafeRaise(AfterLoad, CreateContext(ShelteredMultiplayerHookKind.AfterLoad, "SaveManager.AfterLoad", data), "AfterLoad");
        }

        private void OnSessionStarted()
        {
            SafeRaise(SessionStarted, CreateContext(ShelteredMultiplayerHookKind.SessionStarted, "GameTime.Awake", null), "SessionStarted");
        }

        private void OnNewGame()
        {
            SafeRaise(NewGame, CreateContext(ShelteredMultiplayerHookKind.NewGame, "GameTime.Awake", null), "NewGame");
        }

        private void DrainMainThreadQueue()
        {
            while (true)
            {
                Action action = null;
                lock (_sync)
                {
                    if (_mainThreadQueue.Count == 0)
                        return;
                    action = _mainThreadQueue.Dequeue();
                }

                SafeInvoke(action, "main-thread-queued");
            }
        }

        private ShelteredMultiplayerHookContext CreateContext(ShelteredMultiplayerHookKind kind, string source, object hostContext)
        {
            return new ShelteredMultiplayerHookContext(
                kind,
                source,
                hostContext,
                SessionState,
                Time.frameCount,
                Time.time,
                Time.deltaTime,
                IsMainThread);
        }

        private void SetSessionState(ShelteredMultiplayerSessionState state)
        {
            if (state == null)
                return;

            lock (_sync)
            {
                _sessionState = state;
            }

            MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                "Session state changed: mode=" + state.Mode + ", player=" + state.LocalPlayerId
                + ", session='" + state.SessionId + "', tickRate=" + state.TickRate
                + ", timeMode=" + state.GameTimeMode + ", status=" + state.Status + ".");
        }

        private bool IsMainThread
        {
            get
            {
                int id = _mainThreadId;
                return id == 0 || Thread.CurrentThread.ManagedThreadId == id;
            }
        }

        private static ShelteredMultiplayerSessionState CreateInactiveState(string status)
        {
            return new ShelteredMultiplayerSessionState(
                ShelteredMultiplayerSessionMode.SinglePlayer,
                0,
                string.Empty,
                DefaultTickRate,
                0,
                0f,
                ShelteredMultiplayerGameTimeMode.Vanilla,
                false,
                status);
        }

        private static int NormalizeTickRate(int tickRate)
        {
            return tickRate > 0 ? tickRate : DefaultTickRate;
        }

        private static void ForceRealtimeTimescale()
        {
            if (Math.Abs(Time.timeScale - 1f) > 0.001f)
                Time.timeScale = 1f;
        }

        private static void SafeRaise(Action<ShelteredMultiplayerHookContext> handler, ShelteredMultiplayerHookContext context, string name)
        {
            if (handler == null)
                return;

            try { handler(context); }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerHooks." + name, name + " handler failed: " + ex.Message);
            }
        }

        private static void SafeRaise(Action<ShelteredMultiplayerSnapshotContext> handler, ShelteredMultiplayerSnapshotContext context, string name)
        {
            if (handler == null)
                return;

            try { handler(context); }
            catch (Exception ex)
            {
                context.Error = ex.Message;
                MMLog.WarnOnce("ShelteredMultiplayerHooks." + name, name + " handler failed: " + ex.Message);
            }
        }

        private static void SafeInvoke(Action action, string name)
        {
            try { action(); }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerHooks." + name, name + " action failed: " + ex.Message);
            }
        }
    }
}
