using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    public class NexusUploadPackageService
    {
        private const uint LocalFileHeaderSignature = 0x04034b50;
        private const uint CentralDirectoryFileHeaderSignature = 0x02014b50;
        private const uint EndOfCentralDirectorySignature = 0x06054b50;
        private const ushort Utf8Flag = 0x0800;

        public NexusUploadPackageResult BuildPackage(ModItem mod, NexusUploadDraft draft, out string errorMessage)
        {
            errorMessage = null;
            if (mod == null || string.IsNullOrEmpty(mod.RootPath) || !Directory.Exists(mod.RootPath))
            {
                errorMessage = "The selected mod folder does not exist.";
                return null;
            }

            if (draft == null)
            {
                errorMessage = "No upload draft is selected.";
                return null;
            }

            try
            {
                string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nexus_uploads");
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                string fileName = SanitizeFileName(draft.Name) + "-" + SanitizeFileName(draft.Version) + ".zip";
                string packagePath = Path.Combine(outputDir, fileName);
                List<PackageFile> files = CollectFiles(mod.RootPath);

                using (FileStream stream = File.Open(packagePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    List<CentralDirectoryRecord> records = new List<CentralDirectoryRecord>();
                    for (int i = 0; i < files.Count; i++)
                    {
                        PackageFile file = files[i];
                        records.Add(WriteLocalFile(writer, file, mod.RootPath));
                    }

                    long centralDirectoryOffset = stream.Position;
                    for (int i = 0; i < records.Count; i++)
                        WriteCentralDirectoryRecord(writer, records[i]);
                    long centralDirectorySize = stream.Position - centralDirectoryOffset;

                    WriteEndOfCentralDirectory(writer, records.Count, centralDirectorySize, centralDirectoryOffset);
                }

                FileInfo info = new FileInfo(packagePath);
                return new NexusUploadPackageResult
                {
                    PackagePath = packagePath,
                    FileCount = files.Count,
                    SizeBytes = info.Length
                };
            }
            catch (Exception ex)
            {
                errorMessage = "Package build failed: " + ex.Message;
                return null;
            }
        }

        public string BuildUploadPageUrl(NexusUploadDraft draft)
        {
            string domain = draft != null ? draft.GameDomain : string.Empty;
            if (string.IsNullOrEmpty(domain))
                return string.Empty;

            if (draft.NexusModId > 0)
                return "https://www.nexusmods.com/" + domain + "/mods/" + draft.NexusModId + "?tab=files";

            return "https://www.nexusmods.com/" + domain + "/mods/add";
        }

        private static List<PackageFile> CollectFiles(string rootPath)
        {
            var result = new List<PackageFile>();
            string[] paths = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                string relative = MakeRelativePath(rootPath, path);
                if (ShouldSkip(relative))
                    continue;

                byte[] bytes = File.ReadAllBytes(path);
                result.Add(new PackageFile(relative.Replace('\\', '/'), bytes, File.GetLastWriteTime(path)));
            }

            return result;
        }

        private static bool ShouldSkip(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return true;

            string normalized = relativePath.Replace('\\', '/');
            return string.Equals(normalized, "About/NexusUploadDraft.json", StringComparison.OrdinalIgnoreCase);
        }

        private static CentralDirectoryRecord WriteLocalFile(BinaryWriter writer, PackageFile file, string rootPath)
        {
            long offset = writer.BaseStream.Position;
            byte[] nameBytes = Encoding.UTF8.GetBytes(file.EntryName);
            uint crc = Crc32.Compute(file.Bytes);
            ushort dosTime;
            ushort dosDate;
            ToDosDateTime(file.LastWriteTime, out dosTime, out dosDate);

            writer.Write(LocalFileHeaderSignature);
            writer.Write((ushort)20);
            writer.Write(Utf8Flag);
            writer.Write((ushort)0);
            writer.Write(dosTime);
            writer.Write(dosDate);
            writer.Write(crc);
            writer.Write((uint)file.Bytes.Length);
            writer.Write((uint)file.Bytes.Length);
            writer.Write((ushort)nameBytes.Length);
            writer.Write((ushort)0);
            writer.Write(nameBytes);
            writer.Write(file.Bytes);

            return new CentralDirectoryRecord(file.EntryName, nameBytes, crc, file.Bytes.Length, dosTime, dosDate, offset);
        }

        private static void WriteCentralDirectoryRecord(BinaryWriter writer, CentralDirectoryRecord record)
        {
            writer.Write(CentralDirectoryFileHeaderSignature);
            writer.Write((ushort)20);
            writer.Write((ushort)20);
            writer.Write(Utf8Flag);
            writer.Write((ushort)0);
            writer.Write(record.DosTime);
            writer.Write(record.DosDate);
            writer.Write(record.Crc32);
            writer.Write((uint)record.Size);
            writer.Write((uint)record.Size);
            writer.Write((ushort)record.NameBytes.Length);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((uint)0);
            writer.Write((uint)record.LocalHeaderOffset);
            writer.Write(record.NameBytes);
        }

        private static void WriteEndOfCentralDirectory(BinaryWriter writer, int count, long centralDirectorySize, long centralDirectoryOffset)
        {
            writer.Write(EndOfCentralDirectorySignature);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)count);
            writer.Write((ushort)count);
            writer.Write((uint)centralDirectorySize);
            writer.Write((uint)centralDirectoryOffset);
            writer.Write((ushort)0);
        }

        private static string MakeRelativePath(string rootPath, string path)
        {
            string root = Path.GetFullPath(rootPath);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString()))
                root += Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).Substring(root.Length);
        }

        private static string SanitizeFileName(string value)
        {
            string text = string.IsNullOrEmpty(value) ? "mod" : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                text = text.Replace(invalid[i], '-');
            return text.Length == 0 ? "mod" : text;
        }

        private static void ToDosDateTime(DateTime value, out ushort dosTime, out ushort dosDate)
        {
            if (value.Year < 1980)
                value = new DateTime(1980, 1, 1);

            dosTime = (ushort)((value.Hour << 11) | (value.Minute << 5) | (value.Second / 2));
            dosDate = (ushort)(((value.Year - 1980) << 9) | (value.Month << 5) | value.Day);
        }

        private sealed class PackageFile
        {
            public PackageFile(string entryName, byte[] bytes, DateTime lastWriteTime)
            {
                EntryName = entryName;
                Bytes = bytes;
                LastWriteTime = lastWriteTime;
            }

            public string EntryName;
            public byte[] Bytes;
            public DateTime LastWriteTime;
        }

        private sealed class CentralDirectoryRecord
        {
            public CentralDirectoryRecord(string entryName, byte[] nameBytes, uint crc32, int size, ushort dosTime, ushort dosDate, long localHeaderOffset)
            {
                EntryName = entryName;
                NameBytes = nameBytes;
                Crc32 = crc32;
                Size = size;
                DosTime = dosTime;
                DosDate = dosDate;
                LocalHeaderOffset = localHeaderOffset;
            }

            public string EntryName;
            public byte[] NameBytes;
            public uint Crc32;
            public int Size;
            public ushort DosTime;
            public ushort DosDate;
            public long LocalHeaderOffset;
        }

        private sealed class Crc32
        {
            private static readonly uint[] Table = CreateTable();

            public static uint Compute(byte[] bytes)
            {
                uint value = 0xffffffff;
                if (bytes != null)
                {
                    for (int i = 0; i < bytes.Length; i++)
                        value = Table[(int)((value ^ bytes[i]) & 0xff)] ^ (value >> 8);
                }
                return value ^ 0xffffffff;
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
