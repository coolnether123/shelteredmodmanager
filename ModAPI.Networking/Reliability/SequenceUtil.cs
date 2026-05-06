namespace ModAPI.Networking.Reliability
{
    public static class SequenceUtil
    {
        public static bool IsNewer(ushort current, ushort previous)
        {
            return current != previous && ((current > previous && current - previous <= 32768)
                || (current < previous && previous - current > 32768));
        }

        public static ushort Next(ushort sequence)
        {
            return (ushort)(sequence + 1);
        }
    }
}
