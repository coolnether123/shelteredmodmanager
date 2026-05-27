using System;
using System.Net;
using ModAPI.Networking.Buffers;

namespace ModAPI.Networking.Transport
{
    /// <summary>
    /// A received datagram backed by a pooled buffer.
    /// </summary>
    public sealed class ReceivedPacket : IDisposable
    {
        private readonly PooledBuffer _buffer;

        public ReceivedPacket(IPEndPoint remoteEndPoint, PooledBuffer buffer, int length)
        {
            RemoteEndPoint = remoteEndPoint;
            _buffer = buffer;
            Length = length;
        }

        public IPEndPoint RemoteEndPoint { get; private set; }
        public byte[] Bytes { get { return _buffer != null ? _buffer.Bytes : null; } }
        public int Length { get; private set; }

        public void Dispose()
        {
            if (_buffer != null)
                _buffer.Dispose();
        }
    }
}
