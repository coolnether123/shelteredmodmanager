using System;
using System.Text;

namespace ModAPI.Networking.Serialization
{
    /// <summary>
    /// Allocation-light primitive writer for network byte buffers.
    /// </summary>
    public struct BitWriter
    {
        private readonly byte[] _buffer;
        private readonly int _limit;
        private int _position;

        public BitWriter(byte[] buffer)
            : this(buffer, 0, buffer != null ? buffer.Length : 0)
        {
        }

        public BitWriter(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");

            _buffer = buffer;
            _position = offset;
            _limit = offset + count;
        }

        public int Position { get { return _position; } }
        public int Remaining { get { return _limit - _position; } }

        public void WriteByte(byte value)
        {
            Require(1);
            _buffer[_position++] = value;
        }

        public void WriteBool(bool value)
        {
            WriteByte(value ? (byte)1 : (byte)0);
        }

        public void WriteUInt16(ushort value)
        {
            Require(2);
            _buffer[_position++] = (byte)value;
            _buffer[_position++] = (byte)(value >> 8);
        }

        public void WriteInt32(int value)
        {
            WriteUInt32((uint)value);
        }

        public void WriteUInt32(uint value)
        {
            Require(4);
            _buffer[_position++] = (byte)value;
            _buffer[_position++] = (byte)(value >> 8);
            _buffer[_position++] = (byte)(value >> 16);
            _buffer[_position++] = (byte)(value >> 24);
        }

        public void WriteBytes(byte[] bytes, int offset, int count)
        {
            if (bytes == null)
                throw new ArgumentNullException("bytes");
            if (offset < 0 || count < 0 || offset + count > bytes.Length)
                throw new ArgumentOutOfRangeException("offset");

            Require(count);
            Buffer.BlockCopy(bytes, offset, _buffer, _position, count);
            _position += count;
        }

        public void WriteString(string value)
        {
            if (value == null)
            {
                WriteUInt16(0);
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > ushort.MaxValue)
                throw new ArgumentOutOfRangeException("value", "String payload is too large.");

            WriteUInt16((ushort)bytes.Length);
            WriteBytes(bytes, 0, bytes.Length);
        }

        private void Require(int bytes)
        {
            if (bytes < 0 || _position + bytes > _limit)
                throw new InvalidOperationException("Network write exceeded the packet buffer.");
        }
    }
}
