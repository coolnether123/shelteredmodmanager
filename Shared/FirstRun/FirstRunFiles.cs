using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Smm.FirstRun
{
    internal static class FirstRunFiles
    {
        // Save and mod trees can contain junctions. Never copy through one or replace its target.
        internal static FileAttributes? Attributes(string path)
        {
            FileAttributes attributes;
            try { attributes = File.GetAttributes(path); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Migration cannot use a linked file or folder: " + path);
            return attributes;
        }

        internal static bool IsDirectory(string path)
        {
            var attributes = Attributes(path);
            if (!attributes.HasValue) return false;
            if ((attributes.Value & FileAttributes.Directory) == 0)
                throw new IOException("A folder is required at " + path);
            return true;
        }

        internal static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(stream));
        }

        internal static void CopyFile(string source, string destination)
        {
            PublishFile(destination, temporary =>
            {
                File.Copy(source, temporary);
                if (Hash(source) != Hash(temporary))
                    throw new IOException("A file changed while it was being copied: " + source);
            });
        }

        internal static void WriteLines(string destination, IEnumerable<string> lines)
        {
            PublishFile(destination, temporary => File.WriteAllLines(temporary, new List<string>(lines).ToArray()));
        }

        internal static void EnsureDirectory(string directory)
        {
            string parent = Path.GetDirectoryName(directory);
            if (!string.IsNullOrEmpty(parent)) EnsureDirectory(parent);
            if (!IsDirectory(directory)) Directory.CreateDirectory(directory);
        }

        private static void PublishFile(string destination, Action<string> write)
        {
            EnsureDirectory(Path.GetDirectoryName(destination));
            string temporary = Path.Combine(Path.GetDirectoryName(destination),
                ".smm-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                write(temporary);
                // File.Copy carries the read-only flag, including from an archived source.
                File.SetAttributes(temporary, File.GetAttributes(temporary) & ~FileAttributes.ReadOnly);
                if (Attributes(destination).HasValue)
                    File.Replace(temporary, destination, null, true);
                else
                    File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.SetAttributes(temporary, FileAttributes.Normal);
                    File.Delete(temporary);
                }
            }
        }

        internal static void CopyTree(string source, string destination, bool staging)
        {
            if (!IsDirectory(source)) return;
            EnsureDirectory(destination);
            foreach (string entry in Directory.GetFileSystemEntries(source))
            {
                string target = Path.Combine(destination, Path.GetFileName(entry));
                var attributes = Attributes(entry);
                if (!attributes.HasValue)
                    throw new IOException("A file disappeared while it was being copied: " + entry);
                if ((attributes.Value & FileAttributes.Directory) != 0)
                    CopyTree(entry, target, staging);
                else if (staging)
                    File.Copy(entry, target);
                else
                    CopyFile(entry, target);
            }
        }

        internal static void DeleteStagingTree(string root)
        {
            if (!IsDirectory(root)) return;
            foreach (string file in Directory.GetFiles(root))
            {
                Attributes(file);
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            foreach (string directory in Directory.GetDirectories(root))
                DeleteStagingTree(directory);
            Directory.Delete(root);
        }

        internal static string[] Inventory(string root)
        {
            var records = new List<string>();
            AddInventory(root, "", records);
            records.Sort(StringComparer.Ordinal);
            return records.ToArray();
        }

        private static void AddInventory(string path, string relative, List<string> records)
        {
            var attributes = Attributes(path);
            if (!attributes.HasValue) return;
            string name = Convert.ToBase64String(Encoding.UTF8.GetBytes(relative));
            if ((attributes.Value & FileAttributes.Directory) == 0)
            {
                records.Add(Hash(path) + " " + name);
                return;
            }

            records.Add("directory " + name);
            foreach (string entry in Directory.GetFileSystemEntries(path))
                AddInventory(entry, Path.Combine(relative, Path.GetFileName(entry)), records);
        }
    }
}
