using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Smm.FirstRun
{
    internal sealed class PreUpgradeSaveBackup
    {
        private const string Prefix = "pre-2.0-";
        private const string Marker = "complete.sha256";
        private readonly string _gameRoot;
        private readonly string _backupRoot;

        internal PreUpgradeSaveBackup(string gameRoot)
        {
            _gameRoot = Path.GetFullPath(gameRoot);
            _backupRoot = Path.Combine(Path.Combine(_gameRoot, "SMM"), "Backups");
        }

        internal bool HasBackup
        {
            get { return FindBackup() != null; }
        }

        internal FileStream AcquireLock()
        {
            FirstRunFiles.IsDirectory(_gameRoot);
            FirstRunFiles.IsDirectory(Path.GetDirectoryName(_backupRoot));
            FirstRunFiles.IsDirectory(_backupRoot);
            Directory.CreateDirectory(_backupRoot);
            string path = Path.Combine(_backupRoot, ".pre-2.0.lock");
            FirstRunFiles.Attributes(path);
            return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        internal string Ensure()
        {
            using (AcquireLock())
            {
                string existing = FindBackup();
                if (existing != null)
                {
                    Verify(existing);
                    return existing;
                }
                return Capture(Prefix);
            }
        }

        internal void Restore()
        {
            using (AcquireLock())
            {
                string backup = FindBackup();
                if (backup == null) throw new IOException("No pre-2.0 backup was found.");
                Verify(backup);

                // Keep a verified copy of the current save data before replacing any file.
                Capture("before-restore-");
                FirstRunFiles.IsDirectory(Path.Combine(_gameRoot, "mods"));
                FirstRunFiles.CopyTree(Path.Combine(backup, "saves"), Path.Combine(_gameRoot, "saves"), false);
                FirstRunFiles.CopyTree(ModDataPath(backup), ModDataPath(_gameRoot), false);
            }
        }

        internal static bool TryPrepare(string gameRoot, out Exception error)
        {
            try
            {
                new PreUpgradeSaveBackup(gameRoot).Ensure();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        internal static string DateStamp()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture)
                + "-" + Guid.NewGuid().ToString("N");
        }

        private string FindBackup()
        {
            FirstRunFiles.IsDirectory(_gameRoot);
            FirstRunFiles.IsDirectory(Path.GetDirectoryName(_backupRoot));
            if (!FirstRunFiles.IsDirectory(_backupRoot)) return null;
            string[] backups = Directory.GetDirectories(_backupRoot, Prefix + "*");
            if (backups.Length > 1)
                throw new IOException("Multiple pre-2.0 backups were found.");
            return backups.Length == 0 ? null : backups[0];
        }

        private string Capture(string prefix)
        {
            string staging = Path.Combine(_backupRoot, ".snapshot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                string[] original = Inventory(_gameRoot);
                FirstRunFiles.CopyTree(Path.Combine(_gameRoot, "saves"), Path.Combine(staging, "saves"), true);
                FirstRunFiles.CopyTree(ModDataPath(_gameRoot), ModDataPath(staging), true);
                if (!original.SequenceEqual(Inventory(_gameRoot)))
                    throw new IOException("Save files changed during the backup. Try again with Sheltered closed.");

                File.WriteAllLines(Path.Combine(staging, Marker), original);
                Verify(staging);

                // The dated directory only becomes visible after its payload and marker agree.
                string backup = Path.Combine(_backupRoot, prefix + DateStamp());
                Directory.Move(staging, backup);
                return backup;
            }
            finally
            {
                if (Directory.Exists(staging))
                    FirstRunFiles.DeleteStagingTree(staging);
            }
        }

        private static void Verify(string snapshot)
        {
            if (!FirstRunFiles.IsDirectory(snapshot))
                throw new IOException("The pre-2.0 backup folder is missing.");
            string marker = Path.Combine(snapshot, Marker);
            FirstRunFiles.Attributes(marker);
            if (!File.Exists(marker) || !File.ReadAllLines(marker).SequenceEqual(Inventory(snapshot)))
                throw new IOException("The save backup could not be verified.");
        }

        private static string[] Inventory(string root)
        {
            FirstRunFiles.IsDirectory(Path.Combine(root, "mods"));
            var rows = new List<string> { "SMM first-run save backup 1", "saves" };
            rows.AddRange(FirstRunFiles.Inventory(Path.Combine(root, "saves")));
            rows.Add("mods/ModAPI");
            rows.AddRange(FirstRunFiles.Inventory(ModDataPath(root)));
            return rows.ToArray();
        }

        private static string ModDataPath(string root)
        {
            return Path.Combine(Path.Combine(root, "mods"), "ModAPI");
        }
    }
}
