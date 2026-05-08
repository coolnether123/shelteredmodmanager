using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Networking;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Sessions;
using ShelteredAPI.Bunkers;

namespace ShelteredAPI.Networking
{
    internal enum ShelteredMultiplayerSetupPhase
    {
        Inactive = 0,
        Activated = 1,
        Preparing = 2,
        Received = 3,
        LocalWorldLoaded = 4,
        Released = 5
    }

    internal sealed class ShelteredMultiplayerSessionContext
    {
        public ShelteredMultiplayerSessionContext(
            ShelteredMultiplayerSessionMode mode,
            string sessionId,
            int localPlayerId,
            byte networkLocalPeerId,
            string localStablePeerId,
            int tickRate,
            long worldTick,
            float worldDeltaSeconds,
            ShelteredMultiplayerGameTimeMode gameTimeMode,
            ShelteredMultiplayerSetupPhase setupPhase,
            ShelteredMultiplayerPeerInfo[] roster,
            ShelteredMultiplayerBunkerAssignmentRecord[] bunkerAssignments,
            ShelteredMultiplayerSetupSettings setupSettings,
            string status)
        {
            Mode = mode;
            SessionId = sessionId ?? string.Empty;
            LocalPlayerId = localPlayerId;
            NetworkLocalPeerId = networkLocalPeerId;
            LocalStablePeerId = localStablePeerId ?? string.Empty;
            TickRate = tickRate;
            WorldTick = worldTick;
            WorldDeltaSeconds = worldDeltaSeconds;
            GameTimeMode = gameTimeMode;
            SetupPhase = setupPhase;
            Roster = CloneRoster(roster);
            BunkerAssignments = CloneBunkerAssignments(bunkerAssignments);
            SetupSettings = setupSettings ?? ShelteredMultiplayerSetupSettings.Empty;
            Status = status ?? string.Empty;
        }

        public readonly ShelteredMultiplayerSessionMode Mode;
        public readonly string SessionId;
        public readonly int LocalPlayerId;
        public readonly byte NetworkLocalPeerId;
        public readonly string LocalStablePeerId;
        public readonly int TickRate;
        public readonly long WorldTick;
        public readonly float WorldDeltaSeconds;
        public readonly ShelteredMultiplayerGameTimeMode GameTimeMode;
        public readonly ShelteredMultiplayerSetupPhase SetupPhase;
        public readonly ShelteredMultiplayerPeerInfo[] Roster;
        public readonly ShelteredMultiplayerBunkerAssignmentRecord[] BunkerAssignments;
        public readonly ShelteredMultiplayerSetupSettings SetupSettings;
        public readonly string Status;

        public bool IsMultiplayerActive
        {
            get { return Mode == ShelteredMultiplayerSessionMode.Host || Mode == ShelteredMultiplayerSessionMode.Client; }
        }

        public int GetPlayerIdForNetworkPeer(byte networkPeerId)
        {
            for (int i = 0; i < BunkerAssignments.Length; i++)
            {
                if (BunkerAssignments[i].NetworkPeerId == networkPeerId)
                    return BunkerAssignments[i].PlayerId;
            }

            return networkPeerId == NetworkDefaults.HostPeerId ? 1 : 0;
        }

        public ShelteredMultiplayerSessionContext Snapshot()
        {
            return new ShelteredMultiplayerSessionContext(
                Mode,
                SessionId,
                LocalPlayerId,
                NetworkLocalPeerId,
                LocalStablePeerId,
                TickRate,
                WorldTick,
                WorldDeltaSeconds,
                GameTimeMode,
                SetupPhase,
                Roster,
                BunkerAssignments,
                SetupSettings,
                Status);
        }

        public ShelteredMultiplayerPeerInfo[] GetRosterSnapshot()
        {
            return CloneRoster(Roster);
        }

        public ShelteredMultiplayerBunkerAssignmentRecord[] GetBunkerAssignmentSnapshot()
        {
            return CloneBunkerAssignments(BunkerAssignments);
        }

        public bool TryGetRosterPeer(byte networkPeerId, out ShelteredMultiplayerPeerInfo peerInfo)
        {
            for (int i = 0; i < Roster.Length; i++)
            {
                if (Roster[i] != null && Roster[i].NetworkPeerId == networkPeerId)
                {
                    peerInfo = Roster[i];
                    return true;
                }
            }

            peerInfo = null;
            return false;
        }

        public bool TryGetBunkerAssignmentForPlayer(int playerId, out ShelteredMultiplayerBunkerAssignmentRecord assignment)
        {
            for (int i = 0; i < BunkerAssignments.Length; i++)
            {
                if (BunkerAssignments[i] != null && BunkerAssignments[i].PlayerId == playerId)
                {
                    assignment = BunkerAssignments[i];
                    return true;
                }
            }

            assignment = null;
            return false;
        }

        private static ShelteredMultiplayerPeerInfo[] CloneRoster(ShelteredMultiplayerPeerInfo[] roster)
        {
            if (roster == null || roster.Length == 0)
                return new ShelteredMultiplayerPeerInfo[0];

            ShelteredMultiplayerPeerInfo[] copy = new ShelteredMultiplayerPeerInfo[roster.Length];
            Array.Copy(roster, copy, roster.Length);
            return copy;
        }

        private static ShelteredMultiplayerBunkerAssignmentRecord[] CloneBunkerAssignments(
            ShelteredMultiplayerBunkerAssignmentRecord[] assignments)
        {
            if (assignments == null || assignments.Length == 0)
                return new ShelteredMultiplayerBunkerAssignmentRecord[0];

            ShelteredMultiplayerBunkerAssignmentRecord[] copy =
                new ShelteredMultiplayerBunkerAssignmentRecord[assignments.Length];
            Array.Copy(assignments, copy, assignments.Length);
            return copy;
        }
    }

    internal sealed class ShelteredMultiplayerPeerInfo
    {
        public ShelteredMultiplayerPeerInfo(byte networkPeerId, bool isHost, string stablePeerId, string displayName, bool isConnected)
        {
            NetworkPeerId = networkPeerId;
            IsHost = isHost;
            StablePeerId = stablePeerId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            IsConnected = isConnected;
        }

        public readonly byte NetworkPeerId;
        public readonly bool IsHost;
        public readonly string StablePeerId;
        public readonly string DisplayName;
        public readonly bool IsConnected;
    }

    internal sealed class ShelteredMultiplayerSetupSettings
    {
        public static readonly ShelteredMultiplayerSetupSettings Empty =
            new ShelteredMultiplayerSetupSettings(0, 0, 1, 1, 1, 1, 1, 0, false);

        public ShelteredMultiplayerSetupSettings(
            int hostAbsoluteSlot,
            int clientSuggestedSlot,
            int rainDifficulty,
            int resourceDifficulty,
            int breachDifficulty,
            int factionDifficulty,
            int moodDifficulty,
            int mapSize,
            bool fog)
        {
            HostAbsoluteSlot = hostAbsoluteSlot;
            ClientSuggestedSlot = clientSuggestedSlot;
            RainDifficulty = rainDifficulty;
            ResourceDifficulty = resourceDifficulty;
            BreachDifficulty = breachDifficulty;
            FactionDifficulty = factionDifficulty;
            MoodDifficulty = moodDifficulty;
            MapSize = mapSize;
            Fog = fog;
        }

        public readonly int HostAbsoluteSlot;
        public readonly int ClientSuggestedSlot;
        public readonly int RainDifficulty;
        public readonly int ResourceDifficulty;
        public readonly int BreachDifficulty;
        public readonly int FactionDifficulty;
        public readonly int MoodDifficulty;
        public readonly int MapSize;
        public readonly bool Fog;
    }

    internal enum ShelteredMultiplayerLifecycleEventKind
    {
        SessionActivated = 0,
        RosterChanged = 1,
        SetupPreparing = 2,
        SetupReceived = 3,
        LocalWorldLoaded = 4,
        WorldStartReleased = 5,
        SessionDeactivated = 6
    }

    internal sealed class ShelteredMultiplayerLifecycleEvent
    {
        public ShelteredMultiplayerLifecycleEvent(
            ShelteredMultiplayerLifecycleEventKind kind,
            ShelteredMultiplayerSessionContext context,
            string reason)
        {
            Kind = kind;
            Context = context;
            Reason = reason ?? string.Empty;
        }

        public readonly ShelteredMultiplayerLifecycleEventKind Kind;
        public readonly ShelteredMultiplayerSessionContext Context;
        public readonly string Reason;
    }

    internal interface IShelteredMultiplayerSessionLifecycleHandler
    {
        void Handle(ShelteredMultiplayerLifecycleEvent lifecycleEvent);
    }

    internal sealed class ShelteredMultiplayerSessionCoordinator
    {
        private const int DefaultTickRate = 20;
        private const string LogSource = "ShelteredAPI.MultiplayerSession";
        private static readonly ShelteredMultiplayerSessionCoordinator _instance = new ShelteredMultiplayerSessionCoordinator();

        private readonly object _sync = new object();
        private readonly List<HandlerRegistration> _handlers = new List<HandlerRegistration>();
        private ShelteredMultiplayerSessionContext _context;

        private ShelteredMultiplayerSessionCoordinator()
        {
            _context = CreateInactiveContext("inactive");
            Register(ShelteredMultiplayerSessionSeedApplicator.Instance, true);
            Register(ShelteredMultiplayerSetupSettingsApplicator.Instance, true);
            Register(ShelteredMultiplayerBunkerAssignments.Instance, true);
        }

        public static ShelteredMultiplayerSessionCoordinator Instance
        {
            get { return _instance; }
        }

        public ShelteredMultiplayerSessionContext Context
        {
            get
            {
                lock (_sync)
                {
                    return _context.Snapshot();
                }
            }
        }

        public void Register(IShelteredMultiplayerSessionLifecycleHandler handler, bool startupCritical)
        {
            if (handler == null)
                return;

            lock (_sync)
            {
                for (int i = 0; i < _handlers.Count; i++)
                {
                    if (object.ReferenceEquals(_handlers[i].Handler, handler))
                        return;
                }

                _handlers.Add(new HandlerRegistration(handler, startupCritical));
            }
        }

        public ShelteredMultiplayerSessionContext ActivateHost(
            string sessionId,
            int localPlayerId,
            byte networkLocalPeerId,
            string localStablePeerId,
            int tickRate,
            string reason)
        {
            return Activate(
                ShelteredMultiplayerSessionMode.Host,
                sessionId,
                localPlayerId > 0 ? localPlayerId : 1,
                networkLocalPeerId,
                localStablePeerId,
                tickRate,
                ShelteredMultiplayerGameTimeMode.HostAuthoritative,
                "host-active",
                reason);
        }

        public ShelteredMultiplayerSessionContext ActivateClient(
            string sessionId,
            int localPlayerId,
            byte networkLocalPeerId,
            string localStablePeerId,
            int tickRate,
            string reason)
        {
            return Activate(
                ShelteredMultiplayerSessionMode.Client,
                sessionId,
                localPlayerId > 0 ? localPlayerId : 1,
                networkLocalPeerId,
                localStablePeerId,
                tickRate,
                ShelteredMultiplayerGameTimeMode.RemoteAuthoritative,
                "client-active",
                reason);
        }

        public ShelteredMultiplayerSessionContext UpdateRoster(NetworkSession session, string reason)
        {
            if (session == null)
                return Context;

            List<ShelteredMultiplayerPeerInfo> roster = new List<ShelteredMultiplayerPeerInfo>();
            roster.Add(new ShelteredMultiplayerPeerInfo(
                NetworkDefaults.HostPeerId,
                true,
                session.Mode == NetworkSessionMode.Host ? session.StablePeerId : string.Empty,
                "Host",
                true));

            NetworkPeer[] peers = session.GetPeers();
            for (int i = 0; i < peers.Length; i++)
            {
                NetworkPeer peer = peers[i];
                if (peer == null)
                    continue;

                roster.Add(new ShelteredMultiplayerPeerInfo(
                    peer.PeerId,
                    peer.IsHost,
                    peer.StablePeerId,
                    peer.DisplayName,
                    peer.State == NetworkConnectionState.Connected));
            }

            ShelteredMultiplayerSessionContext updated = With(roster.ToArray(), null, null, null, null, null, reason);
            Raise(ShelteredMultiplayerLifecycleEventKind.RosterChanged, updated, reason);
            return updated;
        }

        public ShelteredMultiplayerSessionContext BeginSetupPreparation(ShelteredMultiplayerSetupSettings setupSettings, string reason)
        {
            ShelteredMultiplayerSessionContext updated = With(
                null,
                null,
                setupSettings,
                null,
                ShelteredMultiplayerSetupPhase.Preparing,
                "setup-preparing",
                reason);
            Raise(ShelteredMultiplayerLifecycleEventKind.SetupPreparing, updated, reason);
            return Context;
        }

        public ShelteredMultiplayerSessionContext ApplyReceivedSetup(
            string sessionId,
            int localPlayerId,
            byte networkLocalPeerId,
            string localStablePeerId,
            ShelteredMultiplayerSetupSettings setupSettings,
            ShelteredMultiplayerBunkerAssignmentRecord[] bunkerAssignments,
            string reason)
        {
            ActivateClient(sessionId, localPlayerId, networkLocalPeerId, localStablePeerId, DefaultTickRate, reason);
            ShelteredMultiplayerSessionContext updated = With(
                null,
                bunkerAssignments,
                setupSettings,
                localPlayerId,
                ShelteredMultiplayerSetupPhase.Received,
                "setup-received",
                reason);
            Raise(ShelteredMultiplayerLifecycleEventKind.SetupReceived, updated, reason);
            return Context;
        }

        public ShelteredMultiplayerSessionContext SetBunkerAssignments(
            ShelteredMultiplayerBunkerAssignmentRecord[] bunkerAssignments,
            int localPlayerId,
            string reason)
        {
            return With(
                null,
                bunkerAssignments,
                null,
                localPlayerId,
                null,
                null,
                reason);
        }

        public ShelteredMultiplayerSessionContext MarkLocalWorldLoaded(string reason)
        {
            ShelteredMultiplayerSessionContext updated = With(
                null,
                null,
                null,
                null,
                ShelteredMultiplayerSetupPhase.LocalWorldLoaded,
                "local-world-loaded",
                reason);
            Raise(ShelteredMultiplayerLifecycleEventKind.LocalWorldLoaded, updated, reason);
            return Context;
        }

        public ShelteredMultiplayerSessionContext ReleaseWorldStart(string reason)
        {
            ShelteredMultiplayerSessionContext updated = With(
                null,
                null,
                null,
                null,
                ShelteredMultiplayerSetupPhase.Released,
                "released",
                reason);
            Raise(ShelteredMultiplayerLifecycleEventKind.WorldStartReleased, updated, reason);
            return Context;
        }

        public ShelteredMultiplayerSessionContext SetGameTimeMode(ShelteredMultiplayerGameTimeMode mode, string reason)
        {
            return With(null, null, null, null, null, null, reason, null, null, mode);
        }

        public ShelteredMultiplayerSessionContext SetWorldTick(long worldTick, float worldDeltaSeconds, string reason)
        {
            if (worldTick < 0)
                worldTick = 0;
            if (worldDeltaSeconds < 0f)
                worldDeltaSeconds = 0f;

            return With(null, null, null, null, null, null, reason, new long?(worldTick), new float?(worldDeltaSeconds), null);
        }

        public void Deactivate(string reason)
        {
            ShelteredMultiplayerSessionContext inactive = CreateInactiveContext(string.IsNullOrEmpty(reason) ? "inactive" : reason);
            SetContext(inactive, reason);
            Raise(ShelteredMultiplayerLifecycleEventKind.SessionDeactivated, inactive, reason);
        }

        private ShelteredMultiplayerSessionContext Activate(
            ShelteredMultiplayerSessionMode mode,
            string sessionId,
            int localPlayerId,
            byte networkLocalPeerId,
            string localStablePeerId,
            int tickRate,
            ShelteredMultiplayerGameTimeMode gameTimeMode,
            string status,
            string reason)
        {
            ShelteredMultiplayerSessionContext current = Context;
            ShelteredMultiplayerSessionContext activated = new ShelteredMultiplayerSessionContext(
                mode,
                sessionId,
                localPlayerId,
                networkLocalPeerId,
                localStablePeerId,
                tickRate > 0 ? tickRate : DefaultTickRate,
                current.WorldTick,
                current.WorldDeltaSeconds,
                gameTimeMode,
                ShelteredMultiplayerSetupPhase.Activated,
                current.Roster,
                current.BunkerAssignments,
                current.SetupSettings,
                status);

            SetContext(activated, reason);
            Raise(ShelteredMultiplayerLifecycleEventKind.SessionActivated, activated, reason);
            return Context;
        }

        private ShelteredMultiplayerSessionContext With(
            ShelteredMultiplayerPeerInfo[] roster,
            ShelteredMultiplayerBunkerAssignmentRecord[] bunkerAssignments,
            ShelteredMultiplayerSetupSettings setupSettings,
            int? localPlayerId,
            ShelteredMultiplayerSetupPhase? setupPhase,
            string status,
            string reason)
        {
            return With(roster, bunkerAssignments, setupSettings, localPlayerId, setupPhase, status, reason, null, null, null);
        }

        private ShelteredMultiplayerSessionContext With(
            ShelteredMultiplayerPeerInfo[] roster,
            ShelteredMultiplayerBunkerAssignmentRecord[] bunkerAssignments,
            ShelteredMultiplayerSetupSettings setupSettings,
            int? localPlayerId,
            ShelteredMultiplayerSetupPhase? setupPhase,
            string status,
            string reason,
            long? worldTick,
            float? worldDeltaSeconds,
            ShelteredMultiplayerGameTimeMode? gameTimeMode)
        {
            ShelteredMultiplayerSessionContext current = Context;
            ShelteredMultiplayerSessionContext updated = new ShelteredMultiplayerSessionContext(
                current.Mode,
                current.SessionId,
                localPlayerId.HasValue ? localPlayerId.Value : current.LocalPlayerId,
                current.NetworkLocalPeerId,
                current.LocalStablePeerId,
                current.TickRate,
                worldTick.HasValue ? worldTick.Value : current.WorldTick,
                worldDeltaSeconds.HasValue ? worldDeltaSeconds.Value : current.WorldDeltaSeconds,
                gameTimeMode.HasValue ? gameTimeMode.Value : current.GameTimeMode,
                setupPhase.HasValue ? setupPhase.Value : current.SetupPhase,
                roster ?? current.Roster,
                bunkerAssignments ?? current.BunkerAssignments,
                setupSettings ?? current.SetupSettings,
                status ?? current.Status);

            SetContext(updated, reason);
            return updated;
        }

        private void SetContext(ShelteredMultiplayerSessionContext context, string reason)
        {
            lock (_sync)
            {
                _context = context;
            }

            TryWriteContextChanged(context, reason);
        }

        private static void TryWriteContextChanged(ShelteredMultiplayerSessionContext context, string reason)
        {
            try
            {
                MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                    "Context changed: mode=" + context.Mode + ", session='" + context.SessionId
                    + "', gameplayPlayer=" + context.LocalPlayerId + ", networkPeer=" + context.NetworkLocalPeerId
                    + ", phase=" + context.SetupPhase + ", status=" + context.Status
                    + ", reason=" + (reason ?? string.Empty) + ".");
            }
            catch
            {
            }
        }

        private void Raise(ShelteredMultiplayerLifecycleEventKind kind, ShelteredMultiplayerSessionContext context, string reason)
        {
            HandlerRegistration[] handlers;
            lock (_sync)
            {
                handlers = _handlers.ToArray();
            }

            ShelteredMultiplayerLifecycleEvent lifecycleEvent =
                new ShelteredMultiplayerLifecycleEvent(kind, context, reason);

            for (int i = 0; i < handlers.Length; i++)
            {
                HandlerRegistration registration = handlers[i];
                try
                {
                    registration.Handler.Handle(lifecycleEvent);
                }
                catch (Exception ex)
                {
                    string message = kind + " handler " + registration.Handler.GetType().Name + " failed: " + ex.Message;
                    if (registration.StartupCritical)
                    {
                        TryWriteLifecycleHandlerError(message);
                        throw new InvalidOperationException(message, ex);
                    }

                    TryWriteLifecycleHandlerWarning(
                        "ShelteredMultiplayerSession." + kind + "." + registration.Handler.GetType().Name,
                        message);
                }
            }
        }

        private static void TryWriteLifecycleHandlerError(string message)
        {
            try
            {
                MMLog.WriteWithSource(MMLog.LogLevel.Error, MMLog.LogCategory.Network, LogSource, message);
            }
            catch
            {
            }
        }

        private static void TryWriteLifecycleHandlerWarning(string key, string message)
        {
            try
            {
                MMLog.WarnOnce(key, message);
            }
            catch
            {
            }
        }

        private static ShelteredMultiplayerSessionContext CreateInactiveContext(string status)
        {
            return new ShelteredMultiplayerSessionContext(
                ShelteredMultiplayerSessionMode.SinglePlayer,
                string.Empty,
                0,
                NetworkDefaults.UnassignedPeerId,
                string.Empty,
                DefaultTickRate,
                0,
                0f,
                ShelteredMultiplayerGameTimeMode.Vanilla,
                ShelteredMultiplayerSetupPhase.Inactive,
                new ShelteredMultiplayerPeerInfo[0],
                new ShelteredMultiplayerBunkerAssignmentRecord[0],
                ShelteredMultiplayerSetupSettings.Empty,
                status);
        }

        private sealed class HandlerRegistration
        {
            public HandlerRegistration(IShelteredMultiplayerSessionLifecycleHandler handler, bool startupCritical)
            {
                Handler = handler;
                StartupCritical = startupCritical;
            }

            public readonly IShelteredMultiplayerSessionLifecycleHandler Handler;
            public readonly bool StartupCritical;
        }
    }
}
