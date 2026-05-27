using System;
using System.Text;

namespace ModAPI.Networking.Serialization
{
    /// <summary>
    /// Primitive reader for network byte buffers.
    /// </summary>
    public struct BitReader
    {
        private readonly byte[] _buffer;
        private readonly int _limit;
        private int _position;

        public BitReader(byte[] buffer, int offset, int count)
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

        public byte ReadByte()
        {
            Require(1);
            return _buffer[_position++];
        }

        public bool ReadBool()
        {
            return ReadByte() != 0;
        }

        public ushort ReadUInt16()
        {
            Require(2);
            int value = _buffer[_position] | (_buffer[_position + 1] << 8);
            _position += 2;
            return (ushort)value;
        }

        public int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        public uint ReadUInt32()
        {
            Require(4);
            uint value = (uint)(_buffer[_position]
                | (_buffer[_position + 1] << 8)
                | (_buffer[_position + 2] << 16)
                | (_buffer[_position + 3] << 24));
            _position += 4;
            return value;
        }

        public void ReadBytes(byte[] destination, int offset, int count)
        {
            if (destination == null)
                throw new ArgumentNullException("destination");
            if (offset < 0 || count < 0 || offset + count > destination.Length)
                throw new ArgumentOutOfRangeException("offset");

            Require(count);
            Buffer.BlockCopy(_buffer, _position, destination, offset, count);
            _position += count;
        }

        public string ReadString()
        {
            ushort length = ReadUInt16();
            if (length == 0)
                return string.Empty;

            Require(length);
            string value = Encoding.UTF8.GetString(_buffer, _position, length);
            _position += length;
            return value;
        }

        private void Require(int bytes)
        {
            if (bytes < 0 || _position + bytes > _limit)
                throw new InvalidOperationException("Network read exceeded the packet buffer.");
        }
    }
}
