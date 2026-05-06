namespace ModAPI.Networking.Reliability
{
    /// <summary>
    /// Tracks received packet sequences and produces a cumulative ACK bitfield.
    /// </summary>
    public sealed class AckWindow
    {
        private ushort _latest;
        private uint _ackBits;
        private bool _hasAny;

        public ushort Latest { get { return _latest; } }
        public uint AckBits { get { return _ackBits; } }
        public bool HasAny { get { return _hasAny; } }

        public bool MarkReceived(ushort sequence)
        {
            if (!_hasAny)
            {
                _latest = sequence;
                _ackBits = 0;
                _hasAny = true;
                return true;
            }

            if (sequence == _latest)
                return false;

            if (SequenceUtil.IsNewer(sequence, _latest))
            {
                int shift = sequence - _latest;
                if (shift < 0)
                    shift += 65536;

                if (shift > 32)
                    _ackBits = 0;
                else if (shift == 32)
                    _ackBits = 1u << 31;
                else
                    _ackBits = (_ackBits << shift) | (uint)(1 << (shift - 1));

                _latest = sequence;
                return true;
            }

            int back = _latest - sequence;
            if (back < 0)
                back += 65536;
            if (back <= 0 || back > 32)
                return false;

            uint bit = (uint)(1 << (back - 1));
            bool wasMissing = (_ackBits & bit) == 0;
            _ackBits |= bit;
            return wasMissing;
        }

        public bool IsAcked(ushort sequence, ushort ack, uint ackBits)
        {
            if (sequence == ack)
                return true;

            int back = ack - sequence;
            if (back < 0)
                back += 65536;
            if (back <= 0 || back > 32)
                return false;

            return (ackBits & (uint)(1 << (back - 1))) != 0;
        }
    }
}
