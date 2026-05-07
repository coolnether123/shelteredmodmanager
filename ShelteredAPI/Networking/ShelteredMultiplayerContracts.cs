using System;

namespace ShelteredAPI.Networking
{
    public enum ShelteredMultiplayerSessionMode
    {
        SinglePlayer = 0,
        Host = 1,
        Client = 2
    }

    public enum ShelteredMultiplayerGameTimeMode
    {
        Vanilla = 0,
        HostAuthoritative = 1,
        RemoteAuthoritative = 2
    }

    public enum ShelteredMultiplayerHookKind
    {
        RuntimeUpdate = 0,
        BeforeGameTimeUpdate = 1,
        AfterGameTimeUpdate = 2,
        PauseRequested = 3,
        ResumeRequested = 4,
        BeforeSave = 5,
        BeforeLoadSceneContents = 6,
        AfterLoad = 7,
        SessionStarted = 8,
        NewGame = 9
    }

    public sealed class ShelteredMultiplayerSessionState
    {
        public ShelteredMultiplayerSessionState(
            ShelteredMultiplayerSessionMode mode,
            byte localPlayerId,
            string sessionId,
            int tickRate,
            long worldTick,
            float worldDeltaSeconds,
            ShelteredMultiplayerGameTimeMode gameTimeMode,
            bool blockVanillaPauseRequests,
            string status)
        {
            Mode = mode;
            LocalPlayerId = localPlayerId;
            SessionId = sessionId ?? string.Empty;
            TickRate = tickRate;
            WorldTick = worldTick;
            WorldDeltaSeconds = worldDeltaSeconds;
            GameTimeMode = gameTimeMode;
            BlockVanillaPauseRequests = blockVanillaPauseRequests;
            Status = status ?? string.Empty;
        }

        public ShelteredMultiplayerSessionMode Mode { get; private set; }
        public byte LocalPlayerId { get; private set; }
        public string SessionId { get; private set; }
        public int TickRate { get; private set; }
        public long WorldTick { get; private set; }
        public float WorldDeltaSeconds { get; private set; }
        public ShelteredMultiplayerGameTimeMode GameTimeMode { get; private set; }
        public bool BlockVanillaPauseRequests { get; private set; }
        public string Status { get; private set; }

        public bool IsMultiplayerActive
        {
            get { return Mode == ShelteredMultiplayerSessionMode.Host || Mode == ShelteredMultiplayerSessionMode.Client; }
        }
    }

    public sealed class ShelteredMultiplayerHookContext
    {
        public ShelteredMultiplayerHookContext(
            ShelteredMultiplayerHookKind kind,
            string source,
            object hostContext,
            ShelteredMultiplayerSessionState sessionState,
            int frame,
            float unityTime,
            float unityDeltaTime,
            bool isMainThread)
        {
            Kind = kind;
            Source = source ?? string.Empty;
            HostContext = hostContext;
            SessionState = sessionState;
            Frame = frame;
            UnityTime = unityTime;
            UnityDeltaTime = unityDeltaTime;
            IsMainThread = isMainThread;
        }

        public ShelteredMultiplayerHookKind Kind { get; private set; }
        public string Source { get; private set; }
        public object HostContext { get; private set; }
        public ShelteredMultiplayerSessionState SessionState { get; private set; }
        public int Frame { get; private set; }
        public float UnityTime { get; private set; }
        public float UnityDeltaTime { get; private set; }
        public bool IsMainThread { get; private set; }

        /// <summary>
        /// Event handlers may set this for prefix hooks that can safely suppress vanilla behavior.
        /// Pause and remote-authoritative time hooks use this to keep one owner for simulation time.
        /// </summary>
        public bool CancelVanilla { get; set; }
    }

    public sealed class ShelteredMultiplayerSnapshotContext
    {
        public ShelteredMultiplayerSnapshotContext(string reason, object hostContext)
        {
            Reason = reason ?? string.Empty;
            HostContext = hostContext;
            Payload = string.Empty;
            Error = string.Empty;
        }

        public string Reason { get; private set; }
        public object HostContext { get; private set; }
        public string Payload { get; set; }
        public bool Handled { get; set; }
        public string Error { get; set; }
    }

    public interface IShelteredMultiplayerHooks
    {
        event Action<ShelteredMultiplayerHookContext> RuntimeUpdate;
        event Action<ShelteredMultiplayerHookContext> BeforeGameTimeUpdate;
        event Action<ShelteredMultiplayerHookContext> AfterGameTimeUpdate;
        event Action<ShelteredMultiplayerHookContext> PauseRequested;
        event Action<ShelteredMultiplayerHookContext> ResumeRequested;
        event Action<ShelteredMultiplayerHookContext> BeforeSave;
        event Action<ShelteredMultiplayerHookContext> BeforeLoadSceneContents;
        event Action<ShelteredMultiplayerHookContext> AfterLoad;
        event Action<ShelteredMultiplayerHookContext> SessionStarted;
        event Action<ShelteredMultiplayerHookContext> NewGame;
        event Action<ShelteredMultiplayerSnapshotContext> CaptureWorldSnapshotRequested;
        event Action<ShelteredMultiplayerSnapshotContext> ApplyWorldSnapshotRequested;

        ShelteredMultiplayerSessionState SessionState { get; }
        bool IsMultiplayerActive { get; }
        long CurrentWorldTick { get; }
        float CurrentWorldDeltaSeconds { get; }

        void ActivateHost(byte localPlayerId, string sessionId, int tickRate);
        void ActivateClient(byte localPlayerId, string sessionId, int tickRate);
        void Deactivate(string reason);
        void SetGameTimeMode(ShelteredMultiplayerGameTimeMode mode);
        void SetWorldTick(long worldTick, float worldDeltaSeconds);
        void EnqueueMainThread(Action action);
        string CaptureWorldSnapshot(string reason, object hostContext);
        bool ApplyWorldSnapshot(string reason, string payload, object hostContext, out string error);
    }
}
