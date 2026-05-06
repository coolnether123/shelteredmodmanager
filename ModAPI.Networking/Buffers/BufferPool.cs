using System;
using System.Collections.Generic;

namespace ModAPI.Networking.Buffers
{
    /// <summary>
    /// Fixed-size byte buffer pool for packet send/receive paths.
    /// </summary>
    public sealed class BufferPool
    {
        private readonly object _sync = new object();
        private readonly Queue<byte[]> _buffers = new Queue<byte[]>();
        private readonly int _bufferSize;
        private readonly int _maxRetained;
        private int _created;

        public BufferPool(int bufferSize, int initialCount, int maxRetained)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException("bufferSize");
            if (initialCount < 0)
                throw new ArgumentOutOfRangeException("initialCount");
            if (maxRetained <= 0)
                throw new ArgumentOutOfRangeException("maxRetained");

            _bufferSize = bufferSize;
            _maxRetained = maxRetained;

            for (int i = 0; i < initialCount && i < maxRetained; i++)
            {
                _buffers.Enqueue(new byte[_bufferSize]);
                _created++;
            }
        }

        public int BufferSize { get { return _bufferSize; } }
        public int CreatedCount { get { return _created; } }

        public PooledBuffer Rent()
        {
            byte[] bytes = null;
            lock (_sync)
            {
                if (_buffers.Count > 0)
                    bytes = _buffers.Dequeue();
            }

            if (bytes == null)
            {
                bytes = new byte[_bufferSize];
                lock (_sync)
                {
                    _created++;
                }
            }

            return new PooledBuffer(this, bytes);
        }

        internal void Return(byte[] bytes)
        {
            if (bytes == null)
                return;
            if (bytes.Length != _bufferSize)
                return;

            Array.Clear(bytes, 0, bytes.Length);
            lock (_sync)
            {
                if (_buffers.Count < _maxRetained)
                    _buffers.Enqueue(bytes);
            }
        }
    }
}
