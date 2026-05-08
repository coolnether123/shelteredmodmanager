using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using ModAPI.Core;
using ShelteredAPI.Events;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class ShelteredMultiplayerHookService : IShelteredMultiplayerHooks, IShelteredMultiplayerSessionLifecycleHandler
    {
        private const int DefaultTickRate = 20;
        private const string LogSource = "ShelteredAPI.MultiplayerHooks";
        private static readonly ShelteredMultiplayerHookService _instance = new ShelteredMultiplayerHookService();
        private static readonly bool _standaloneTestHost = IsStandaloneTestHost();

        private readonly object _sync = new object();
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private ShelteredMultiplayerSessionState _sessionState;
        private bool _worldStartBlocked;
        private int _mainThreadId;
        private bool _gameEventsAttached;
        private int _queueWarningCount;

        private ShelteredMultiplayerHookService()
        {
            _sessionState = CreateInactiveState("inactive");
            ShelteredMultiplayerSessionCoordinator.Instance.Register(this, true);
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
            ShelteredMultiplayerSessionCoordinator.Instance.ActivateHost(
                sessionId,
                localPlayerId,
                ModAPI.Networking.NetworkDefaults.HostPeerId,
                string.Empty,
                tickRate,
                "hooks-activate-host");
        }

        public void ActivateClient(byte localPlayerId, string sessionId, int tickRate)
        {
            ShelteredMultiplayerSessionCoordinator.Instance.ActivateClient(
                sessionId,
                localPlayerId,
                ModAPI.Networking.NetworkDefaults.UnassignedPeerId,
                string.Empty,
                tickRate,
                "hooks-activate-client");
        }

        public void Deactivate(string reason)
        {
            ShelteredMultiplayerSessionCoordinator.Instance.Deactivate(reason);
        }

        public void SetGameTimeMode(ShelteredMultiplayerGameTimeMode mode)
        {
            SetSessionState(CreateState(ShelteredMultiplayerSessionCoordinator.Instance.SetGameTimeMode(mode, "hooks-set-game-time-mode")));
        }

        public void SetWorldTick(long worldTick, float worldDeltaSeconds)
        {
            SetSessionState(CreateState(ShelteredMultiplayerSessionCoordinator.Instance.SetWorldTick(
                worldTick,
                worldDeltaSeconds,
                "hooks-set-world-tick")));
        }

        public void Handle(ShelteredMultiplayerLifecycleEvent lifecycleEvent)
        {
            if (lifecycleEvent == null || lifecycleEvent.Context == null)
                return;

            if (lifecycleEvent.Kind == ShelteredMultiplayerLifecycleEventKind.SetupPreparing
                || lifecycleEvent.Kind == ShelteredMultiplayerLifecycleEventKind.SetupReceived
                || lifecycleEvent.Kind == ShelteredMultiplayerLifecycleEventKind.LocalWorldLoaded)
            {
                SetWorldStartBlocked(true, lifecycleEvent.Reason);
            }
            else if (lifecycleEvent.Kind == ShelteredMultiplayerLifecycleEventKind.WorldStartReleased
                || lifecycleEvent.Kind == ShelteredMultiplayerLifecycleEventKind.SessionDeactivated)
            {
                SetWorldStartBlocked(false, lifecycleEvent.Reason);
            }

            SetSessionState(CreateState(lifecycleEvent.Context));
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
                    TryWrite(MMLog.LogLevel.Warning,
                        "Main-thread queue contains " + _mainThreadQueue.Count + " actions.");
                }
            }
        }

        internal void SetWorldStartBlocked(bool blocked, string reason)
        {
            lock (_sync)
            {
                if (_worldStartBlocked == blocked)
                    return;

                _worldStartBlocked = blocked;
            }

            TryWrite(MMLog.LogLevel.Info,
                "World start block " + (blocked ? "enabled" : "disabled") + ". Reason=" + (reason ?? string.Empty) + ".");
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
            ShelteredMultiplayerTimePolicy.ApplyGameTimePolicy(gameTime);
            if (state.IsMultiplayerActive)
                ApplyTimescalePolicy();

            ShelteredMultiplayerHookContext context = CreateContext(
                ShelteredMultiplayerHookKind.BeforeGameTimeUpdate,
                "GameTime.Update",
                gameTime);

            if (state.GameTimeMode == ShelteredMultiplayerGameTimeMode.RemoteAuthoritative)
                context.CancelVanilla = true;
            if (IsWorldStartBlocked)
                context.CancelVanilla = true;

            SafeRaise(BeforeGameTimeUpdate, context, "BeforeGameTimeUpdate");
            return !context.CancelVanilla;
        }

        internal void EndGameTimeUpdate(GameTime gameTime)
        {
            if (IsMultiplayerActive)
                ApplyTimescalePolicy();

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
                ApplyTimescalePolicy();

            return !context.CancelVanilla;
        }

        internal void HandleResumeRequest(string source, object hostContext)
        {
            if (IsMultiplayerActive)
                ApplyTimescalePolicy();

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
                SafeReadFrameCount(),
                SafeReadUnityTime(),
                SafeReadUnityDeltaTime(),
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

            TryWriteSessionStateChanged(state);
        }

        private static void TryWriteSessionStateChanged(ShelteredMultiplayerSessionState state)
        {
            try
            {
                TryWrite(MMLog.LogLevel.Info,
                    "Session state changed: mode=" + state.Mode + ", player=" + state.LocalPlayerId
                    + ", session='" + state.SessionId + "', tickRate=" + state.TickRate
                    + ", timeMode=" + state.GameTimeMode + ", status=" + state.Status + ".");
            }
            catch
            {
                // GuardrailAllow: SilentCatch - multiplayer hook logging is best-effort and cannot affect gameplay hook flow.
            }
        }

        private bool IsMainThread
        {
            get
            {
                int id = _mainThreadId;
                return id == 0 || Thread.CurrentThread.ManagedThreadId == id;
            }
        }

        internal bool IsWorldStartBlocked
        {
            get
            {
                lock (_sync)
                {
                    return _worldStartBlocked;
                }
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

        private static ShelteredMultiplayerSessionState CreateState(ShelteredMultiplayerSessionContext context)
        {
            if (context == null || !context.IsMultiplayerActive)
                return CreateInactiveState(context != null ? context.Status : "inactive");

            return new ShelteredMultiplayerSessionState(
                context.Mode,
                ToBytePlayerId(context.LocalPlayerId),
                context.SessionId,
                NormalizeTickRate(context.TickRate),
                context.WorldTick,
                context.WorldDeltaSeconds,
                context.GameTimeMode,
                true,
                context.Status);
        }

        private static byte ToBytePlayerId(int playerId)
        {
            if (playerId <= 0)
                return 0;
            if (playerId > byte.MaxValue)
                return byte.MaxValue;

            return (byte)playerId;
        }

        private static int NormalizeTickRate(int tickRate)
        {
            return tickRate > 0 ? tickRate : DefaultTickRate;
        }

        private void ApplyTimescalePolicy()
        {
            ShelteredMultiplayerTimePolicy.ForceRealtimeTimescale();
        }

        private static void SafeRaise(Action<ShelteredMultiplayerHookContext> handler, ShelteredMultiplayerHookContext context, string name)
        {
            if (handler == null)
                return;

            try { handler(context); }
            catch (Exception ex)
            {
                TryWarnOnce("ShelteredMultiplayerHooks." + name, name + " handler failed: " + ex.Message);
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
                TryWarnOnce("ShelteredMultiplayerHooks." + name, name + " handler failed: " + ex.Message);
            }
        }

        private static void SafeInvoke(Action action, string name)
        {
            try { action(); }
            catch (Exception ex)
            {
                TryWarnOnce("ShelteredMultiplayerHooks." + name, name + " action failed: " + ex.Message);
            }
        }

        private static void TryWrite(MMLog.LogLevel level, string message)
        {
            try
            {
                MMLog.WriteWithSource(level, MMLog.LogCategory.Network, LogSource, message);
            }
            catch
            {
                // GuardrailAllow: SilentCatch - hook diagnostic logging is best-effort and cannot affect gameplay hook flow.
            }
        }

        private static void TryWarnOnce(string key, string message)
        {
            try
            {
                MMLog.WarnOnce(key, message);
            }
            catch
            {
                // GuardrailAllow: SilentCatch - hook warning logging is best-effort and cannot affect gameplay hook flow.
            }
        }

        private static int SafeReadFrameCount()
        {
            object value = TryReadUnityTimeProperty("frameCount");
            return value is int ? (int)value : 0;
        }

        private static float SafeReadUnityTime()
        {
            object value = TryReadUnityTimeProperty("time");
            return value is float ? (float)value : 0f;
        }

        private static float SafeReadUnityDeltaTime()
        {
            object value = TryReadUnityTimeProperty("deltaTime");
            return value is float ? (float)value : 0f;
        }

        private static object TryReadUnityTimeProperty(string name)
        {
            if (_standaloneTestHost)
                return null;

            try
            {
                PropertyInfo property = typeof(Time).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
                return property != null ? property.GetValue(null, null) : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsStandaloneTestHost()
        {
            try
            {
                Assembly entryAssembly = Assembly.GetEntryAssembly();
                string name = entryAssembly != null ? entryAssembly.GetName().Name : string.Empty;
                return name != null && name.IndexOf(".Tests", System.StringComparison.Ordinal) >= 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
