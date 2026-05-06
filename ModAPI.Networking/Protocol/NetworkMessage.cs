namespace ModAPI.Networking.Protocol
{
    public struct NetworkMessage
    {
        public ushort MessageType;
        public NetworkChannel Channel;
        public byte[] Payload;
        public int Offset;
        public int Length;

        public NetworkMessage(ushort messageType, NetworkChannel channel, byte[] payload, int offset, int length)
        {
            MessageType = messageType;
            Channel = channel;
            Payload = payload;
            Offset = offset;
            Length = length;
        }
    }
}
