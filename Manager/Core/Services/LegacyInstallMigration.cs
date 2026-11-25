using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Smm.FirstRun;

namespace Manager.Core.Services
{
    internal enum MigrationChoice
    {
        Later,
        KeepCurrent,
        UseOld
    }

    internal sealed class LegacyInstallMigration
    {
        private static readonly Version LegacyVersion = new Version(1, 2, 2, 0);
        // Import the settings 1.2.2 actually wrote. Account information belongs to the current Manager.
        private static readonly HashSet<string> LegacySettings = new HashSet<string>(new[]
        {
            "DarkMode", "DevMode", "LogLevel", "LogCategories", "IgnoreOrderChecks",
            "SkipHarmonyDependencyCheck", "GameBitness", "AutoCondenseSaves", "AutoLoadSaveSlot",
            "WindowX", "WindowY", "WindowWidth", "WindowHeight", "WindowMaximized"
        }, StringComparer.OrdinalIgnoreCase);

        private readonly string _gamePath;
        private readonly string _root;
        private readonly string _backups;
        private readonly PreUpgradeSaveBackup _snapshot;

        internal LegacyInstallMigration(string gamePath)
        {
            _gamePath = Path.GetFullPath(gamePath);
            _root = Path.GetDirectoryName(_gamePath);
            _backups = Path.Combine(Path.Combine(_root, "SMM"), "Backups");
            _snapshot = new PreUpgradeSaveBackup(_root);
        }

        internal bool IsLegacyRoot { get { return IsLegacyInstall(_root); } }

        internal IEnumerable<string> FindWrappers()
        {
            return Directory.GetDirectories(_root, "SMM_*_v1.*")
                .Where(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
                .Where(IsLegacyInstall)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        }

        internal bool WasImported(string wrapper)
        {
            return ReadState(wrapper) == "imported";
        }

        internal bool Import(string wrapper, Func<MigrationChoice> choose)
        {
            using (_snapshot.AcquireLock())
            {
                if (!IsLegacyInstall(wrapper))
                    throw new IOException("The old Manager files could not be verified.");

                string state = ReadState(wrapper);
                if (state == "imported") return false;
                if (state == "")
                {
                    MigrationChoice choice = HasPlayerData(_root) && HasPlayerData(wrapper)
                        ? choose() : MigrationChoice.UseOld;
                    if (choice == MigrationChoice.Later) return false;
                    state = choice == MigrationChoice.UseOld ? "use-old" : "keep-current";
                    WriteState(wrapper, state);
                }
                if (state != "use-old" && state != "keep-current")
                    throw new IOException("The previous import choice could not be read.");

                bool useOld = state == "use-old";
                string displaced = Path.Combine(ImportFolder(wrapper), "previous");
                ImportSettings(wrapper, displaced, useOld);
                ImportMods(wrapper, displaced, useOld);
                WriteState(wrapper, "imported");
                return true;
            }
        }

        internal void Tidy(string wrapper)
        {
            using (_snapshot.AcquireLock())
            {
                if (!IsLegacyInstall(wrapper) || !WasImported(wrapper))
                    throw new IOException("Only an imported 1.2.2 folder can be moved to backups.");
                string archive = Path.Combine(_backups, "old-1.2.2-" + PreUpgradeSaveBackup.DateStamp());
                Directory.CreateDirectory(archive);
                Directory.Move(wrapper, Path.Combine(archive, Path.GetFileName(wrapper)));
            }
        }

        private static bool IsLegacyInstall(string root)
        {
            string smm = Path.Combine(root, "SMM");
            if (!FirstRunFiles.IsDirectory(smm)) return false;
            return IsLegacyBinary(Path.Combine(smm, "ModAPI.dll"), "ModAPI")
                && IsLegacyBinary(Path.Combine(smm, "Manager.exe"), "Manager");
        }

        private static bool IsLegacyBinary(string file, string name)
        {
            if (!FirstRunFiles.Attributes(file).HasValue) return false;
            try
            {
                var metadata = AssemblyName.GetAssemblyName(file);
                return metadata.Name == name && metadata.Version == LegacyVersion;
            }
            catch (BadImageFormatException) { return false; }
            catch (FileLoadException) { return false; }
        }

        private static bool HasPlayerData(string root)
        {
            FirstRunFiles.IsDirectory(Path.Combine(Path.Combine(root, "SMM"), "bin"));
            return FirstRunFiles.Attributes(SettingsPath(root)).HasValue || ModEntries(root).Length != 0;
        }

        private static string SettingsPath(string root)
        {
            return Path.Combine(Path.Combine(Path.Combine(root, "SMM"), "bin"), "mod_manager.ini");
        }

        private static string[] ModEntries(string root)
        {
            string mods = Path.Combine(root, "mods");
            return FirstRunFiles.IsDirectory(mods) ? Directory.GetFileSystemEntries(mods) : new string[0];
        }

        private void ImportSettings(string wrapper, string displaced, bool useOld)
        {
            string source = SettingsPath(wrapper);
            FirstRunFiles.IsDirectory(Path.GetDirectoryName(source));
            if (!FirstRunFiles.Attributes(source).HasValue) return;
            string destination = SettingsPath(_root);
            FirstRunFiles.IsDirectory(Path.GetDirectoryName(destination));
            bool exists = FirstRunFiles.Attributes(destination).HasValue;
            if (exists && !useOld) return;

            string[] current = exists ? File.ReadAllLines(destination) : new string[0];
            string[] oldSettings = File.ReadAllLines(source).Where(line => LegacySettings.Contains(SettingKey(line))).ToArray();
            var changedKeys = new HashSet<string>(oldSettings.Select(SettingKey), StringComparer.OrdinalIgnoreCase);
            changedKeys.Add("GamePath");
            string archived = Path.Combine(displaced, "mod_manager.ini");
            if (exists && !FirstRunFiles.Attributes(archived).HasValue)
                FirstRunFiles.WriteLines(archived, current.Where(line => changedKeys.Contains(SettingKey(line))));

            FirstRunFiles.WriteLines(destination, current.Where(line => !changedKeys.Contains(SettingKey(line)))
                .Concat(oldSettings).Concat(new[] { "GamePath=" + _gamePath }));
        }

        private static string SettingKey(string line)
        {
            int separator = line.IndexOf('=');
            return separator < 0 ? "" : line.Substring(0, separator).Trim();
        }

        private void ImportMods(string wrapper, string displaced, bool useOld)
        {
            string destination = Path.Combine(_root, "mods");
            FirstRunFiles.IsDirectory(destination);
            foreach (string source in ModEntries(wrapper))
            {
                string name = Path.GetFileName(source);
                string target = Path.Combine(destination, name);
                string previous = Path.Combine(Path.Combine(displaced, "mods"), name);
                ImportItem(source, target, previous, useOld,
                    string.Equals(name, "ModAPI", StringComparison.OrdinalIgnoreCase));
            }
        }

        private void ImportItem(string source, string target, string previous, bool useOld, bool merge)
        {
            var original = FirstRunFiles.Attributes(source);
            if (!original.HasValue) throw new IOException("An old mod folder changed during import.");
            var current = FirstRunFiles.Attributes(target);
            bool directory = (original.Value & FileAttributes.Directory) != 0;
            if (merge && directory && current.HasValue && (current.Value & FileAttributes.Directory) != 0)
            {
                foreach (string entry in Directory.GetFileSystemEntries(source))
                {
                    string name = Path.GetFileName(entry);
                    ImportItem(entry, Path.Combine(target, name), Path.Combine(previous, name), useOld, true);
                }
                return;
            }
            if (current.HasValue && !useOld) return;
            string[] inventory = FirstRunFiles.Inventory(source);
            if (current.HasValue && inventory.SequenceEqual(FirstRunFiles.Inventory(target))) return;

            // Promote a complete mod folder in one rename. Combining versions can leave duplicate DLLs.
            FirstRunFiles.EnsureDirectory(Path.GetDirectoryName(target));
            string staged = Path.Combine(_backups, ".migration-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (directory) FirstRunFiles.CopyTree(source, staged, true);
                else FirstRunFiles.CopyFile(source, staged);
                if (!inventory.SequenceEqual(FirstRunFiles.Inventory(staged)))
                    throw new IOException("An old mod changed during import.");
                if (current.HasValue)
                {
                    if (FirstRunFiles.Attributes(previous).HasValue)
                        throw new IOException("A mod changed after an interrupted import. Both copies were kept.");
                    FirstRunFiles.EnsureDirectory(Path.GetDirectoryName(previous));
                    Move(target, previous, (current.Value & FileAttributes.Directory) != 0);
                }
                Move(staged, target, directory);
            }
            finally
            {
                if (Directory.Exists(staged)) FirstRunFiles.DeleteStagingTree(staged);
                else if (File.Exists(staged)) File.Delete(staged);
            }
        }

        private static void Move(string source, string destination, bool directory)
        {
            if (directory) Directory.Move(source, destination);
            else File.Move(source, destination);
        }

        private string ImportFolder(string wrapper)
        {
            string folder = Path.Combine(_backups, "import-" + Path.GetFileName(wrapper));
            FirstRunFiles.IsDirectory(folder);
            return folder;
        }

        private string ReadState(string wrapper)
        {
            string path = Path.Combine(ImportFolder(wrapper), "state.txt");
            return FirstRunFiles.Attributes(path).HasValue ? File.ReadAllText(path).Trim() : "";
        }

        private void WriteState(string wrapper, string state)
        {
            FirstRunFiles.WriteLines(Path.Combine(ImportFolder(wrapper), "state.txt"), new[] { state });
        }
    }
}
