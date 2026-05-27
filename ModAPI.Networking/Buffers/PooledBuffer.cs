using System;

namespace ModAPI.Networking.Buffers
{
    /// <summary>
    /// A rented byte buffer. Dispose returns it to its owner pool.
    /// </summary>
    public sealed class PooledBuffer : IDisposable
    {
        private readonly BufferPool _owner;
        private bool _disposed;

        internal PooledBuffer(BufferPool owner, byte[] bytes)
        {
            _owner = owner;
            Bytes = bytes;
        }

        public byte[] Bytes { get; private set; }
        public int Length { get; set; }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            byte[] bytes = Bytes;
            Bytes = null;
            Length = 0;

            if (_owner != null && bytes != null)
                _owner.Return(bytes);
        }
    }
}
