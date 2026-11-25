using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Smm.Shared
{
    // The shipping projects target .NET 3.5. Read the PE/CLI AssemblyRef table
    // directly so inspecting a mod never gives its image to the assembly loader.
    internal sealed class ModAssemblyMetadata
    {
        private readonly BinaryReader _reader;
        private readonly uint[] _rows = new uint[64];
        private int _strings;
        private int _guid;
        private int _blob;

        public const string LegacyUpdateMessage = "Built for SMM 1.2.2 \u2014 needs a 2.0 update";

        public static bool IsLegacyReference(string name, int majorVersion)
        {
            return (string.Equals(name, "ModAPI", StringComparison.OrdinalIgnoreCase) && majorVersion < 2) ||
                (string.Equals(name, "Sheltered" + "API", StringComparison.OrdinalIgnoreCase) && majorVersion == 0);
        }

        // Returns false for native DLLs. Invalid or truncated images throw.
        public static bool ReadReferences(byte[] image, List<AssemblyName> references)
        {
            using (var reader = new BinaryReader(new MemoryStream(image)))
                return new ModAssemblyMetadata(reader).ReadImage(references);
        }

        private ModAssemblyMetadata(BinaryReader reader)
        {
            _reader = reader;
        }

        private bool ReadImage(List<AssemblyName> references)
        {
            if (_reader.ReadUInt16() != 0x5a4d)
                throw InvalidImage();
            Seek(0x3c, 4, _reader.BaseStream.Length);
            long pe = _reader.ReadUInt32();
            Seek(pe, 24, _reader.BaseStream.Length);
            if (_reader.ReadUInt32() != 0x4550)
                throw InvalidImage();
            _reader.ReadUInt16(); // Machine
            int sectionCount = _reader.ReadUInt16();
            Seek(pe + 20, 2, _reader.BaseStream.Length);
            int optionalSize = _reader.ReadUInt16();
            long optional = pe + 24;
            long sections = optional + optionalSize;
            Seek(optional, 2, sections);
            int magic = _reader.ReadUInt16();
            if (magic != 0x10b && magic != 0x20b)
                throw InvalidImage();
            long directories = optional + (magic == 0x10b ? 96 : 112);
            Seek(directories - 4, 4, sections);
            if (_reader.ReadUInt32() <= 14)
                return false;
            Seek(directories + 14 * 8, 8, sections);
            uint cliRva = _reader.ReadUInt32();
            uint cliSize = _reader.ReadUInt32();
            if (cliRva == 0 && cliSize == 0)
                return false;
            if (cliRva == 0 || cliSize < 16)
                throw InvalidImage();

            long cli = MapRva(sections, sectionCount, cliRva, 16);
            Seek(cli + 8, 8, _reader.BaseStream.Length);
            uint metadataRva = _reader.ReadUInt32();
            uint metadataSize = _reader.ReadUInt32();
            long metadata = MapRva(sections, sectionCount, metadataRva, metadataSize);
            ReadMetadata(metadata, metadataSize, references);
            return true;
        }

        private long MapRva(long sections, int count, uint rva, uint size)
        {
            for (int i = 0; i < count; i++)
            {
                Seek(sections + i * 40 + 12, 12, _reader.BaseStream.Length);
                uint address = _reader.ReadUInt32();
                uint rawSize = _reader.ReadUInt32();
                uint rawStart = _reader.ReadUInt32();
                long offset = (long)rva - address;
                if (offset >= 0 && offset + size <= rawSize)
                {
                    long position = rawStart + offset;
                    Seek(position, size, _reader.BaseStream.Length);
                    return position;
                }
            }
            throw InvalidImage();
        }

        private void ReadMetadata(long start, long size, List<AssemblyName> references)
        {
            long end = start + size;
            Seek(start, 16, end);
            if (_reader.ReadUInt32() != 0x424a5342)
                throw InvalidImage();
            Seek(start + 12, 4, end);
            uint versionLength = _reader.ReadUInt32();
            Seek(start + 16 + versionLength, 4, end);
            _reader.ReadUInt16(); // Flags
            int streamCount = _reader.ReadUInt16();
            long tables = 0, tablesEnd = 0, strings = 0, stringsEnd = 0;
            for (int i = 0; i < streamCount; i++)
            {
                long header = _reader.BaseStream.Position;
                Seek(header, 8, end);
                long streamStart = start + _reader.ReadUInt32();
                long streamSize = _reader.ReadUInt32();
                string name = ReadString(end);
                long next = start + ((_reader.BaseStream.Position - start + 3) & ~3L);
                Seek(streamStart, streamSize, end);
                if (name == "#~" || name == "#-")
                {
                    tables = streamStart;
                    tablesEnd = streamStart + streamSize;
                }
                else if (name == "#Strings")
                {
                    strings = streamStart;
                    stringsEnd = streamStart + streamSize;
                }
                Seek(next, 0, end);
            }
            if (tables == 0 || strings == 0)
                throw InvalidImage();

            Seek(tables + 6, 18, tablesEnd);
            byte heaps = _reader.ReadByte();
            _strings = (heaps & 1) != 0 ? 4 : 2;
            _guid = (heaps & 2) != 0 ? 4 : 2;
            _blob = (heaps & 4) != 0 ? 4 : 2;
            _reader.ReadByte(); // Reserved
            ulong valid = _reader.ReadUInt64();
            _reader.ReadUInt64(); // Sorted mask
            for (int table = 0; table < _rows.Length; table++)
            {
                if ((valid & (1UL << table)) == 0)
                    continue;
                Seek(_reader.BaseStream.Position, 4, tablesEnd);
                _rows[table] = _reader.ReadUInt32();
            }
            if ((heaps & 0x40) != 0) // Extra data in uncompressed metadata
                Seek(_reader.BaseStream.Position + 4, 0, tablesEnd);

            const int assemblyRef = 35;
            long rowStart = _reader.BaseStream.Position;
            for (int table = 0; table < assemblyRef; table++)
                rowStart += (long)_rows[table] * RowWidth(table);

            int rowWidth = 12 + 2 * _blob + 2 * _strings;
            Seek(rowStart, (long)rowWidth * _rows[assemblyRef], tablesEnd);
            for (uint i = 0; i < _rows[assemblyRef]; i++)
            {
                Seek(rowStart + i * (long)rowWidth, rowWidth, tablesEnd);
                var version = new Version(_reader.ReadUInt16(), _reader.ReadUInt16(), _reader.ReadUInt16(), _reader.ReadUInt16());
                Seek(_reader.BaseStream.Position + 4 + _blob, _strings, tablesEnd);
                uint nameIndex = _strings == 2 ? _reader.ReadUInt16() : _reader.ReadUInt32();
                Seek(strings + nameIndex, 1, stringsEnd);
                string name = ReadString(stringsEnd);
                if (name.Length == 0)
                    throw InvalidImage();
                references.Add(new AssemblyName { Name = name, Version = version });
            }
        }

        // ECMA-335 II.22. The tables are stored in ID order. Only widths
        // before AssemblyRef are needed to locate its rows.
        private int RowWidth(int table)
        {
            switch (table)
            {
                case 0: return 2 + _strings + 3 * _guid; // Module
                case 1: return Coded(2, 0, 26, 35, 1) + 2 * _strings; // TypeRef
                case 2: return 4 + 2 * _strings + Coded(2, 2, 1, 27) + Index(4) + Index(6); // TypeDef
                case 3: return Index(4); // FieldPtr
                case 4: return 2 + _strings + _blob; // Field
                case 5: return Index(6); // MethodPtr
                case 6: return 8 + _strings + _blob + Index(8); // MethodDef
                case 7: return Index(8); // ParamPtr
                case 8: return 4 + _strings; // Param
                case 9: return Index(2) + Coded(2, 2, 1, 27); // InterfaceImpl
                case 10: return Coded(3, 2, 1, 26, 6, 27) + _strings + _blob; // MemberRef
                case 11: return 2 + Coded(2, 4, 8, 23) + _blob; // Constant
                case 12: return Coded(5, 6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17, 26, 27, 32, 35, 38, 39, 40, 42, 44, 43)
                                + Coded(3, 6, 10) + _blob; // CustomAttribute
                case 13: return Coded(1, 4, 8) + _blob; // FieldMarshal
                case 14: return 2 + Coded(2, 2, 6, 32) + _blob; // DeclSecurity
                case 15: return 6 + Index(2); // ClassLayout
                case 16: return 4 + Index(4); // FieldLayout
                case 17: return _blob; // StandAloneSig
                case 18: return Index(2) + Index(20); // EventMap
                case 19: return Index(20); // EventPtr
                case 20: return 2 + _strings + Coded(2, 2, 1, 27); // Event
                case 21: return Index(2) + Index(23); // PropertyMap
                case 22: return Index(23); // PropertyPtr
                case 23: return 2 + _strings + _blob; // Property
                case 24: return 2 + Index(6) + Coded(1, 20, 23); // MethodSemantics
                case 25: return Index(2) + 2 * Coded(1, 6, 10); // MethodImpl
                case 26: return _strings; // ModuleRef
                case 27: return _blob; // TypeSpec
                case 28: return 2 + Coded(1, 4, 6) + _strings + Index(26); // ImplMap
                case 29: return 4 + Index(4); // FieldRVA
                case 30: return 8; // ENCLog
                case 31: return 4; // ENCMap
                case 32: return 16 + _blob + 2 * _strings; // Assembly
                case 33: return 4; // AssemblyProcessor
                case 34: return 12; // AssemblyOS
                default: throw InvalidImage();
            }
        }

        private int Index(int table)
        {
            return _rows[table] >= 65536 ? 4 : 2;
        }

        private int Coded(int tagBits, params int[] tables)
        {
            uint limit = 1u << (16 - tagBits);
            foreach (int table in tables)
                if (_rows[table] >= limit)
                    return 4;
            return 2;
        }

        private string ReadString(long end)
        {
            var bytes = new List<byte>();
            while (_reader.BaseStream.Position < end)
            {
                byte value = _reader.ReadByte();
                if (value == 0)
                    return Encoding.UTF8.GetString(bytes.ToArray());
                bytes.Add(value);
            }
            throw InvalidImage();
        }

        private void Seek(long position, long size, long end)
        {
            if (position < 0 || position + size > end || end > _reader.BaseStream.Length)
                throw InvalidImage();
            _reader.BaseStream.Position = position;
        }

        private static BadImageFormatException InvalidImage()
        {
            return new BadImageFormatException("Invalid PE or CLI metadata.");
        }
    }
}
