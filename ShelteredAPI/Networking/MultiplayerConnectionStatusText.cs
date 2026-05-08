using System;
using ModAPI.Networking.Sessions;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerConnectionActionState
    {
        private MultiplayerConnectionActionState(string label, bool enabled, string disabledReason)
        {
            Label = label ?? string.Empty;
            Enabled = enabled;
            DisabledReason = disabledReason ?? string.Empty;
        }

        public string Label { get; private set; }
        public bool Enabled { get; private set; }
        public string DisabledReason { get; private set; }

        public static MultiplayerConnectionActionState Available(string label)
        {
            return new MultiplayerConnectionActionState(label, true, string.Empty);
        }

        public static MultiplayerConnectionActionState Unavailable(string label, string disabledReason)
        {
            return new MultiplayerConnectionActionState(label, false, disabledReason);
        }
    }

    internal sealed class MultiplayerEndpointSuggestion
    {
        public MultiplayerEndpointSuggestion(string label, string endpointText, string description)
        {
            Label = label ?? string.Empty;
            EndpointText = endpointText ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string Label { get; private set; }
        public string EndpointText { get; private set; }
        public string Description { get; private set; }
    }

    internal sealed class MultiplayerConnectionStatusText
    {
        public string RoleText = "Offline";
        public string StateText = "Not connected";
        public string SummaryText = "No multiplayer session is running.";
        public string DetailText = "Host a game or join a friend by endpoint.";
    }

    internal enum MultiplayerSetupReadinessKind
    {
        NotStarted = 0,
        Loading = 1,
        Waiting = 2,
        EveryoneLoaded = 3,
        Released = 4,
        Error = 5
    }

    internal sealed class MultiplayerSetupReadinessText
    {
        public MultiplayerSetupReadinessKind Kind = MultiplayerSetupReadinessKind.NotStarted;
        public string StatusText = "Not started";
        public string DetailText = "Setup has not started.";
    }

    internal static class MultiplayerConnectionStatusTextBuilder
    {
        public static MultiplayerConnectionStatusText Build(
            NetworkSessionMode mode,
            NetworkSessionState state,
            bool hasActiveSession,
            int connectedPeers,
            int totalPeers)
        {
            MultiplayerConnectionStatusText text = new MultiplayerConnectionStatusText();
            text.RoleText = BuildRoleText(mode, hasActiveSession);
            text.StateText = BuildStateText(state, hasActiveSession);

            if (!hasActiveSession)
                return text;

            if (mode == NetworkSessionMode.Host)
            {
                text.SummaryText = state == NetworkSessionState.Listening
                    ? "Hosting a session. Share the LAN endpoint with friends."
                    : "Host session is " + text.StateText.ToLowerInvariant() + ".";
                text.DetailText = connectedPeers == 0
                    ? "Waiting for clients to join."
                    : connectedPeers + " connected peer(s), " + totalPeers + " total peer record(s).";
                return text;
            }

            if (mode == NetworkSessionMode.Client)
            {
                text.SummaryText = state == NetworkSessionState.Connected
                    ? "Connected to the host."
                    : "Client session is " + text.StateText.ToLowerInvariant() + ".";
                text.DetailText = state == NetworkSessionState.Connecting
                    ? "Waiting for the host handshake to complete."
                    : "Host peer records: " + totalPeers + ".";
                return text;
            }

            text.SummaryText = "Multiplayer session is active.";
            text.DetailText = "Role has not been assigned yet.";
            return text;
        }

        public static string BuildRoleText(NetworkSessionMode mode, bool hasActiveSession)
        {
            if (!hasActiveSession || mode == NetworkSessionMode.None)
                return "Offline";
            if (mode == NetworkSessionMode.Host)
                return "Host";
            if (mode == NetworkSessionMode.Client)
                return "Client";
            return mode.ToString();
        }

        public static string BuildStateText(NetworkSessionState state, bool hasActiveSession)
        {
            if (!hasActiveSession)
                return "Not connected";

            switch (state)
            {
                case NetworkSessionState.Stopped:
                    return "Stopped";
                case NetworkSessionState.Starting:
                    return "Starting";
                case NetworkSessionState.Listening:
                    return "Listening";
                case NetworkSessionState.Connecting:
                    return "Connecting";
                case NetworkSessionState.Connected:
                    return "Connected";
                case NetworkSessionState.Disconnecting:
                    return "Disconnecting";
                case NetworkSessionState.Failed:
                    return "Failed";
                default:
                    return state.ToString();
            }
        }
    }

    internal static class MultiplayerSetupReadinessTextBuilder
    {
        public static MultiplayerSetupReadinessText Build(
            string rawStatus,
            string lastError,
            NetworkSessionMode mode,
            bool hasActiveSession,
            bool canReleaseSetup,
            int connectedPeerCount)
        {
            MultiplayerSetupReadinessText text = new MultiplayerSetupReadinessText();

            if (!string.IsNullOrEmpty(lastError))
            {
                text.Kind = MultiplayerSetupReadinessKind.Error;
                text.StatusText = "Setup error";
                text.DetailText = lastError;
                return text;
            }

            string status = rawStatus ?? string.Empty;
            string normalized = status.ToLowerInvariant();
            if (!hasActiveSession
                || normalized.Length == 0
                || normalized == "inactive"
                || normalized == "idle"
                || normalized == "cancelled")
            {
                text.Kind = MultiplayerSetupReadinessKind.NotStarted;
                text.StatusText = "Not started";
                text.DetailText = mode == NetworkSessionMode.Host
                    ? "Begin setup after hosting and choosing who should join."
                    : "Join a host and wait for setup to begin.";
                return text;
            }

            if (Contains(normalized, "released"))
            {
                text.Kind = MultiplayerSetupReadinessKind.Released;
                text.StatusText = "Released";
                text.DetailText = "World start has been released.";
                return text;
            }

            if (canReleaseSetup || Contains(normalized, "all players loaded"))
            {
                text.Kind = MultiplayerSetupReadinessKind.EveryoneLoaded;
                text.StatusText = "Everyone loaded";
                text.DetailText = mode == NetworkSessionMode.Host
                    ? "All expected players are loaded. Release start when ready."
                    : "Waiting for the host to release world start.";
                return text;
            }

            if (Contains(normalized, "waiting"))
            {
                text.Kind = MultiplayerSetupReadinessKind.Waiting;
                text.StatusText = "Waiting";
                text.DetailText = status;
                if (mode == NetworkSessionMode.Host && connectedPeerCount == 0)
                    text.DetailText = "Waiting for at least one client to connect and load.";
                return text;
            }

            if (Contains(normalized, "loading")
                || Contains(normalized, "loaded")
                || Contains(normalized, "setup received")
                || Contains(normalized, "setup started")
                || Contains(normalized, "startup"))
            {
                text.Kind = MultiplayerSetupReadinessKind.Loading;
                text.StatusText = "Loading";
                text.DetailText = status;
                return text;
            }

            text.Kind = MultiplayerSetupReadinessKind.Loading;
            text.StatusText = "Setup active";
            text.DetailText = status;
            return text;
        }

        private static bool Contains(string value, string text)
        {
            return value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
