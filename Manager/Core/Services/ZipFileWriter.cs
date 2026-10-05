using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Manager.Core.Services
{
    /// <summary>
    /// Small ZIP writer compatible with .NET 3.5: stored or deflated entries,
    /// UTF-8 names, no ZIP64 (entries and archives stay under 4 GB).
    /// </summary>
    internal static class ZipFileWriter
    {
        public sealed class Entry
        {
            public string Name;
            public byte[] Data;

            public Entry(string name, byte[] data)
            {
                Name = name;
                Data = data;
            }
        }

        private sealed class Record
        {
            public byte[] Name;
            public ushort Method;
            public uint Crc;
            public uint CompressedSize;
            public uint Size;
            public uint Offset;
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        /// <summary>
        /// Writes <paramref name="entries"/> to a new archive. With
        /// <paramref name="compress"/>, entries that shrink are deflated;
        /// anything else is stored.
        /// </summary>
        public static void Write(string outputPath, IList<Entry> entries, bool compress)
        {
            Write(outputPath, entries, compress, DateTime.Now);
        }

        /// <summary>
        /// As <see cref="Write(string, IList{Entry}, bool)"/>, stamping every entry with
        /// <paramref name="modified"/>; the same entries and time give the same bytes.
        /// </summary>
        public static void Write(string outputPath, IList<Entry> entries, bool compress, DateTime modified)
        {
            if (entries.Count > ushort.MaxValue)
                throw new InvalidOperationException("A ZIP without ZIP64 holds at most " + ushort.MaxValue + " entries.");
            ushort dosTime;
            ushort dosDate;
            ToDosDateTime(modified, out dosTime, out dosDate);
            List<Record> records = new List<Record>(entries.Count);

            using (FileStream stream = File.Create(outputPath))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry entry = entries[i];
                    byte[] data = entry.Data ?? new byte[0];
                    if ((long)data.Length + stream.Position > uint.MaxValue)
                        throw new InvalidOperationException("The archive would pass 4 GB, which needs ZIP64.");
                    byte[] payload = data;
                    ushort method = 0;
                    if (compress && data.Length > 128)
                    {
                        byte[] deflated = Deflate(data);
                        if (deflated.Length < data.Length)
                        {
                            payload = deflated;
                            method = 8;
                        }
                    }

                    Record record = new Record
                    {
                        Name = Encoding.UTF8.GetBytes((entry.Name ?? "file").Replace('\\', '/')),
                        Method = method,
                        Crc = Crc32(data),
                        CompressedSize = (uint)payload.Length,
                        Size = (uint)data.Length,
                        Offset = (uint)stream.Position
                    };
                    records.Add(record);

                    writer.Write(0x04034b50u);
                    writer.Write((ushort)20);
                    writer.Write((ushort)0x0800);
                    writer.Write(record.Method);
                    writer.Write(dosTime);
                    writer.Write(dosDate);
                    writer.Write(record.Crc);
                    writer.Write(record.CompressedSize);
                    writer.Write(record.Size);
                    writer.Write((ushort)record.Name.Length);
                    writer.Write((ushort)0);
                    writer.Write(record.Name);
                    writer.Write(payload);
                }

                uint centralOffset = (uint)stream.Position;
                for (int i = 0; i < records.Count; i++)
                {
                    Record record = records[i];
                    writer.Write(0x02014b50u);
                    writer.Write((ushort)20);
                    writer.Write((ushort)20);
                    writer.Write((ushort)0x0800);
                    writer.Write(record.Method);
                    writer.Write(dosTime);
                    writer.Write(dosDate);
                    writer.Write(record.Crc);
                    writer.Write(record.CompressedSize);
                    writer.Write(record.Size);
                    writer.Write((ushort)record.Name.Length);
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write(0u);
                    writer.Write(record.Offset);
                    writer.Write(record.Name);
                }
                uint centralSize = (uint)stream.Position - centralOffset;
                writer.Write(0x06054b50u);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)records.Count);
                writer.Write((ushort)records.Count);
                writer.Write(centralSize);
                writer.Write(centralOffset);
                writer.Write((ushort)0);
            }
        }

        public static uint Crc32(byte[] bytes)
        {
            uint crc = 0xffffffffu;
            for (int i = 0; i < bytes.Length; i++)
                crc = CrcTable[(crc ^ bytes[i]) & 0xff] ^ (crc >> 8);
            return ~crc;
        }

        private static byte[] Deflate(byte[] data)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (DeflateStream deflate = new DeflateStream(output, CompressionMode.Compress, true))
                    deflate.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        private static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int bit = 0; bit < 8; bit++)
                    c = (c & 1) != 0 ? 0xedb88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        private static void ToDosDateTime(DateTime value, out ushort time, out ushort date)
        {
            int year = Math.Max(1980, Math.Min(2107, value.Year));
            time = (ushort)((value.Hour << 11) | (value.Minute << 5) | (value.Second / 2));
            date = (ushort)(((year - 1980) << 9) | (value.Month << 5) | value.Day);
        }
    }
}
