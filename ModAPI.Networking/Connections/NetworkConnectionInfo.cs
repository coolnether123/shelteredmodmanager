using System;
using System.Net;

namespace ModAPI.Networking.Connections
{
    public enum NetworkConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        TimedOut = 3
    }

    public sealed class NetworkConnectionInfo
    {
        public IPEndPoint RemoteEndPoint;
        public NetworkConnectionState State;
        public ushort LocalSequence;
        public ushort RemoteSequence;
        public DateTime LastReceiveUtc = DateTime.UtcNow;
        public DateTime LastSendUtc = DateTime.UtcNow;

        public NetworkConnectionInfo()
        {
        }

        public NetworkConnectionInfo(IPEndPoint remoteEndPoint, NetworkConnectionState state)
        {
            RemoteEndPoint = remoteEndPoint;
            SetState(state, DateTime.UtcNow);
        }

        public void SetState(NetworkConnectionState state)
        {
            SetState(state, DateTime.UtcNow);
        }

        public void SetState(NetworkConnectionState state, DateTime utcNow)
        {
            State = state;
            if (state == NetworkConnectionState.Connecting || state == NetworkConnectionState.Connected)
            {
                LastReceiveUtc = utcNow;
                LastSendUtc = utcNow;
            }
        }

        public void TouchReceive()
        {
            LastReceiveUtc = DateTime.UtcNow;
        }

        public void TouchSend()
        {
            LastSendUtc = DateTime.UtcNow;
        }

        public bool IsTimedOut(DateTime utcNow, int timeoutMilliseconds)
        {
            if (State != NetworkConnectionState.Connected && State != NetworkConnectionState.Connecting)
                return false;
            if (LastReceiveUtc == DateTime.MinValue)
                return false;

            return (utcNow - LastReceiveUtc).TotalMilliseconds > timeoutMilliseconds;
        }
    }
}
