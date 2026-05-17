using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Manager.Core.Services
{
    internal interface IArchiveExtractor
    {
        bool TryExtract(string archivePath, string destinationPath, out string errorMessage);
    }

    internal sealed class ZipArchiveExtractor : IArchiveExtractor
    {
        private const uint EndOfCentralDirectorySignature = 0x06054b50;
        private const uint CentralDirectoryFileHeaderSignature = 0x02014b50;
        private const uint LocalFileHeaderSignature = 0x04034b50;
        private const ushort GeneralPurposeEncryptedFlag = 0x0001;
        private const ushort GeneralPurposeUtf8Flag = 0x0800;
        private const ushort StoredCompression = 0;
        private const ushort DeflateCompression = 8;

        public bool TryExtract(string archivePath, string destinationPath, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(archivePath) || !File.Exists(archivePath))
            {
                errorMessage = "Archive file does not exist.";
                return false;
            }

            if (string.IsNullOrEmpty(destinationPath))
            {
                errorMessage = "Archive extraction destination is empty.";
                return false;
            }

            try
            {
                if (!Directory.Exists(destinationPath))
                    Directory.CreateDirectory(destinationPath);

                using (FileStream archive = File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (BinaryReader reader = new BinaryReader(archive))
                    {
                        CentralDirectoryInfo centralDirectory;
                        if (!TryReadEndOfCentralDirectory(archive, out centralDirectory, out errorMessage))
                            return false;

                        List<ZipEntry> entries = ReadCentralDirectory(reader, centralDirectory);
                        if (entries.Count == 0)
                        {
                            errorMessage = "Archive is empty.";
                            return false;
                        }

                        string destinationRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(destinationPath));

                        for (int i = 0; i < entries.Count; i++)
                        {
                            ZipEntry entry = entries[i];
                            string targetPath;
                            if (!TryGetSafeTargetPath(destinationRoot, destinationPath, entry.Name, out targetPath, out errorMessage))
                                return false;

                            if (entry.IsDirectory)
                            {
                                if (!Directory.Exists(targetPath))
                                    Directory.CreateDirectory(targetPath);
                                continue;
                            }

                            string targetDirectory = Path.GetDirectoryName(targetPath);
                            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                                Directory.CreateDirectory(targetDirectory);

                            ExtractEntry(archive, reader, entry, targetPath);
                        }
                    }
                }

                return true;
            }
            catch (InvalidDataException ex)
            {
                errorMessage = "Archive extraction failed: " + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = "Archive extraction failed: " + ex.Message;
                return false;
            }
        }

        private static bool TryReadEndOfCentralDirectory(
            FileStream archive,
            out CentralDirectoryInfo centralDirectory,
            out string errorMessage)
        {
            centralDirectory = null;
            errorMessage = null;

            long archiveLength = archive.Length;
            if (archiveLength < 22)
            {
                errorMessage = "Archive is not a valid ZIP file.";
                return false;
            }

            int tailLength = (int)Math.Min(archiveLength, 22 + ushort.MaxValue);
            byte[] tail = new byte[tailLength];
            archive.Position = archiveLength - tailLength;
            int bytesRead = archive.Read(tail, 0, tail.Length);

            for (int offset = bytesRead - 22; offset >= 0; offset--)
            {
                if (ReadUInt32(tail, offset) != EndOfCentralDirectorySignature)
                    continue;

                int index = offset + 4;
                ushort diskNumber = ReadUInt16(tail, index);
                index += 2;
                ushort centralDirectoryDisk = ReadUInt16(tail, index);
                index += 2;
                ushort entriesOnDisk = ReadUInt16(tail, index);
                index += 2;
                ushort totalEntries = ReadUInt16(tail, index);
                index += 2;
                uint centralDirectorySize = ReadUInt32(tail, index);
                index += 4;
                uint centralDirectoryOffset = ReadUInt32(tail, index);

                if (diskNumber != 0 || centralDirectoryDisk != 0 || entriesOnDisk != totalEntries)
                {
                    errorMessage = "Multi-disk ZIP archives are not supported.";
                    return false;
                }

                if (totalEntries == ushort.MaxValue
                    || centralDirectorySize == uint.MaxValue
                    || centralDirectoryOffset == uint.MaxValue)
                {
                    errorMessage = "Zip64 archives are not supported by this installer.";
                    return false;
                }

                if ((long)centralDirectoryOffset + centralDirectorySize > archiveLength)
                {
                    errorMessage = "Archive central directory is corrupt.";
                    return false;
                }

                centralDirectory = new CentralDirectoryInfo(totalEntries, centralDirectoryOffset);
                return true;
            }

            errorMessage = "Archive is not a valid ZIP file.";
            return false;
        }

        private static List<ZipEntry> ReadCentralDirectory(BinaryReader reader, CentralDirectoryInfo centralDirectory)
        {
            List<ZipEntry> entries = new List<ZipEntry>(centralDirectory.EntryCount);
            reader.BaseStream.Position = centralDirectory.Offset;

            for (int i = 0; i < centralDirectory.EntryCount; i++)
            {
                if (reader.ReadUInt32() != CentralDirectoryFileHeaderSignature)
                    throw new InvalidDataException("Archive central directory is corrupt.");

                reader.ReadUInt16(); // Version made by.
                reader.ReadUInt16(); // Version needed to extract.
                ushort flags = reader.ReadUInt16();
                ushort compressionMethod = reader.ReadUInt16();
                reader.ReadUInt16(); // Last mod file time.
                reader.ReadUInt16(); // Last mod file date.
                uint crc32 = reader.ReadUInt32();
                uint compressedSize = reader.ReadUInt32();
                uint uncompressedSize = reader.ReadUInt32();
                ushort fileNameLength = reader.ReadUInt16();
                ushort extraFieldLength = reader.ReadUInt16();
                ushort fileCommentLength = reader.ReadUInt16();
                ushort diskNumberStart = reader.ReadUInt16();
                reader.ReadUInt16(); // Internal file attributes.
                uint externalFileAttributes = reader.ReadUInt32();
                uint localHeaderOffset = reader.ReadUInt32();

                if ((flags & GeneralPurposeEncryptedFlag) != 0)
                    throw new InvalidDataException("Encrypted ZIP entries are not supported.");

                if (diskNumberStart != 0)
                    throw new InvalidDataException("Multi-disk ZIP archives are not supported.");

                if (compressionMethod != StoredCompression && compressionMethod != DeflateCompression)
                    throw new InvalidDataException("Unsupported ZIP compression method: " + compressionMethod + ".");

                if (compressedSize == uint.MaxValue || uncompressedSize == uint.MaxValue || localHeaderOffset == uint.MaxValue)
                    throw new InvalidDataException("Zip64 entries are not supported by this installer.");

                byte[] nameBytes = reader.ReadBytes(fileNameLength);
                string name = DecodeEntryName(nameBytes, flags);
                reader.BaseStream.Seek(extraFieldLength + fileCommentLength, SeekOrigin.Current);

                bool isDirectory = name.EndsWith("/", StringComparison.Ordinal)
                    || name.EndsWith("\\", StringComparison.Ordinal)
                    || (externalFileAttributes & 0x10) != 0;

                entries.Add(new ZipEntry(
                    name,
                    compressionMethod,
                    crc32,
                    compressedSize,
                    uncompressedSize,
                    localHeaderOffset,
                    isDirectory));
            }

            return entries;
        }

        private static void ExtractEntry(FileStream archive, BinaryReader reader, ZipEntry entry, string targetPath)
        {
            archive.Position = entry.LocalHeaderOffset;
            if (reader.ReadUInt32() != LocalFileHeaderSignature)
                throw new InvalidDataException("Archive local file header is corrupt: " + entry.Name);

            reader.ReadUInt16(); // Version needed to extract.
            ushort localFlags = reader.ReadUInt16();
            ushort localCompressionMethod = reader.ReadUInt16();
            reader.ReadUInt16(); // Last mod file time.
            reader.ReadUInt16(); // Last mod file date.
            reader.ReadUInt32(); // CRC-32, may be zero when a data descriptor is used.
            reader.ReadUInt32(); // Compressed size, may be zero when a data descriptor is used.
            reader.ReadUInt32(); // Uncompressed size, may be zero when a data descriptor is used.
            ushort localFileNameLength = reader.ReadUInt16();
            ushort localExtraFieldLength = reader.ReadUInt16();

            if ((localFlags & GeneralPurposeEncryptedFlag) != 0)
                throw new InvalidDataException("Encrypted ZIP entries are not supported.");

            if (localCompressionMethod != entry.CompressionMethod)
                throw new InvalidDataException("Archive local file header does not match central directory: " + entry.Name);

            archive.Seek(localFileNameLength + localExtraFieldLength, SeekOrigin.Current);

            using (FileStream output = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (BoundedReadStream compressedData = new BoundedReadStream(archive, entry.CompressedSize))
                {
                    if (entry.CompressionMethod == StoredCompression)
                    {
                        CopyAndVerify(compressedData, output, entry);
                    }
                    else
                    {
                        using (DeflateStream deflated = new DeflateStream(compressedData, CompressionMode.Decompress))
                        {
                            CopyAndVerify(deflated, output, entry);
                        }
                    }
                }
            }
        }

        private static void CopyAndVerify(Stream input, Stream output, ZipEntry entry)
        {
            byte[] buffer = new byte[32768];
            Crc32 crc = new Crc32();
            long written = 0;

            while (true)
            {
                int read = input.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;

                output.Write(buffer, 0, read);
                crc.Update(buffer, 0, read);
                written += read;

                if (written > entry.UncompressedSize)
                    throw new InvalidDataException("Archive entry expanded beyond its declared size: " + entry.Name);
            }

            if (written != entry.UncompressedSize)
                throw new InvalidDataException("Archive entry ended before its declared size: " + entry.Name);

            if (crc.Value != entry.Crc32)
                throw new InvalidDataException("Archive entry CRC check failed: " + entry.Name);
        }

        private static bool TryGetSafeTargetPath(
            string destinationRoot,
            string destinationPath,
            string entryName,
            out string targetPath,
            out string errorMessage)
        {
            targetPath = null;
            errorMessage = null;

            if (string.IsNullOrEmpty(entryName))
            {
                errorMessage = "Archive contains an entry with an empty name.";
                return false;
            }

            string normalizedName = entryName
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            if (Path.IsPathRooted(normalizedName))
            {
                errorMessage = "Archive contains an absolute path: " + entryName;
                return false;
            }

            string fullPath = Path.GetFullPath(Path.Combine(destinationPath, normalizedName));
            if (!fullPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Archive contains a path outside the destination: " + entryName;
                return false;
            }

            targetPath = fullPath;
            return true;
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return Path.DirectorySeparatorChar.ToString();

            char last = path[path.Length - 1];
            if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
                return path;

            return path + Path.DirectorySeparatorChar;
        }

        private static string DecodeEntryName(byte[] bytes, ushort flags)
        {
            if ((flags & GeneralPurposeUtf8Flag) != 0)
                return Encoding.UTF8.GetString(bytes);

            try { return Encoding.GetEncoding(437).GetString(bytes); }
            catch { return Encoding.Default.GetString(bytes); }
        }

        private static ushort ReadUInt16(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24));
        }

        private sealed class CentralDirectoryInfo
        {
            public CentralDirectoryInfo(int entryCount, long offset)
            {
                EntryCount = entryCount;
                Offset = offset;
            }

            public int EntryCount { get; private set; }
            public long Offset { get; private set; }
        }

        private sealed class ZipEntry
        {
            public ZipEntry(
                string name,
                ushort compressionMethod,
                uint crc32,
                uint compressedSize,
                uint uncompressedSize,
                uint localHeaderOffset,
                bool isDirectory)
            {
                Name = name;
                CompressionMethod = compressionMethod;
                Crc32 = crc32;
                CompressedSize = compressedSize;
                UncompressedSize = uncompressedSize;
                LocalHeaderOffset = localHeaderOffset;
                IsDirectory = isDirectory;
            }

            public string Name { get; private set; }
            public ushort CompressionMethod { get; private set; }
            public uint Crc32 { get; private set; }
            public uint CompressedSize { get; private set; }
            public uint UncompressedSize { get; private set; }
            public uint LocalHeaderOffset { get; private set; }
            public bool IsDirectory { get; private set; }
        }

        private sealed class BoundedReadStream : Stream
        {
            private readonly Stream _inner;
            private long _remaining;

            public BoundedReadStream(Stream inner, long length)
            {
                if (inner == null)
                    throw new ArgumentNullException("inner");

                _inner = inner;
                _remaining = length;
            }

            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_remaining <= 0)
                    return 0;

                if (count > _remaining)
                    count = (int)Math.Min(count, _remaining);

                int read = _inner.Read(buffer, offset, count);
                _remaining -= read;
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class Crc32
        {
            private static readonly uint[] Table = CreateTable();
            private uint _value = 0xffffffff;

            public uint Value
            {
                get { return _value ^ 0xffffffff; }
            }

            public void Update(byte[] buffer, int offset, int count)
            {
                for (int i = offset; i < offset + count; i++)
                    _value = Table[(int)((_value ^ buffer[i]) & 0xff)] ^ (_value >> 8);
            }

            private static uint[] CreateTable()
            {
                uint[] table = new uint[256];
                for (uint i = 0; i < table.Length; i++)
                {
                    uint value = i;
                    for (int bit = 0; bit < 8; bit++)
                        value = (value & 1) != 0 ? 0xedb88320 ^ (value >> 1) : value >> 1;
                    table[i] = value;
                }
                return table;
            }
        }
    }
}
