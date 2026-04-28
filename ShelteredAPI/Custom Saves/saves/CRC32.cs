using System;

namespace ModAPI.Saves
{
    internal static class CRC32
    {
        private static readonly uint[] Table = InitTable();

        private static uint[] InitTable()
        {
            const uint poly = 0xEDB88320u;
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ poly;
                    else
                        crc >>= 1;
                }
                table[i] = crc;
            }
            return table;
        }

        public static uint Compute(byte[] data)
        {
            if (data == null) return 0;
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++)
            {
                var idx = (byte)((crc ^ data[i]) & 0xFF);
                crc = (crc >> 1) ^ Table[idx];
            }
            return ~crc;
        }
    }
}

