using System;
using System.Net.Sockets;

namespace ModAPI.Networking.Diagnostics
{
    public sealed class NetworkBindException : InvalidOperationException
    {
        public NetworkBindException(int port, SocketException innerException)
            : base(CreateMessage(port, innerException), innerException)
        {
            Port = port;
            SocketErrorCode = innerException != null ? innerException.SocketErrorCode : SocketError.SocketError;
        }

        public int Port { get; private set; }
        public SocketError SocketErrorCode { get; private set; }

        private static string CreateMessage(int port, SocketException innerException)
        {
            string detail = innerException != null ? innerException.Message : "Unknown socket error.";
            return "Failed to bind UDP port " + port + ". The port may already be in use, blocked, or unavailable on this machine. " + detail;
        }
    }
}
