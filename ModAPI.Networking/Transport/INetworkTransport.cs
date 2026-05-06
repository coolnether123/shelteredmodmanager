using System;
using System.Net;

namespace ModAPI.Networking.Transport
{
    public interface INetworkTransport : IDisposable
    {
        event Action<ReceivedPacket> PacketReceived;
        event Action<Exception> TransportError;

        bool IsRunning { get; }
        IPEndPoint LocalEndPoint { get; }

        void Start();
        void Start(int port);
        void Stop();
        void Send(IPEndPoint endPoint, byte[] buffer, int offset, int count);
    }
}
