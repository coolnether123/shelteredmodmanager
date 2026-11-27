using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Manager.Core.Services
{
    // Used by Manager discovery, order writes, and modded launch.
    internal sealed class DuplicateModGuard
    {
        internal const string RetryMessage = "Could not move a duplicate mod. Close the game and try again.";
        internal const string RollbackMessage = "Could not finish moving a duplicate mod. Check the Mods folder and its duplicate backups.";
        internal static readonly DuplicateModGuard Shared = new DuplicateModGuard();

        internal sealed class Copy
        {
            internal string Path;
            internal string Id;
            internal ModTypes.ModAboutInfo About;
            internal DateTime Written;
            internal bool LoaderValid;
            internal Version NumericVersion;
            internal string Backup;
        }

        internal sealed class Notice
        {
            internal string Root;
            internal Copy Kept;
            internal Copy[] Moved;
        }

        private sealed class Move
        {
            internal string From;
            internal string To;
        }

        private readonly object _sync = new object();
        private readonly List<Notice> _notices = new List<Notice>();
        private readonly HashSet<string> _blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Move>> _unfinishedRollbacks = new Dictionary<string, List<Move>>(StringComparer.OrdinalIgnoreCase);
        private readonly Action<string, string> _moveDirectory;

        internal DuplicateModGuard() : this(Directory.Move) { }

        internal DuplicateModGuard(Action<string, string> moveDirectory)
        {
            _moveDirectory = moveDirectory;
        }

        internal static string RootKey(string root)
        {
            return Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        }

        internal bool IsBlocked(string root)
        {
            return GetBlockMessage(root) != null;
        }

        internal string GetBlockMessage(string root)
        {
            lock (_sync)
            {
                string key = RootKey(root);
                if (_unfinishedRollbacks.ContainsKey(key))
                    return RollbackMessage;
                return _blocked.Contains(key) ? RetryMessage : null;
            }
        }

        internal Notice[] TakeNotices(string root)
        {
            lock (_sync)
            {
                string key = RootKey(root);
                var notices = _notices.Where(n => StringComparer.OrdinalIgnoreCase.Equals(n.Root, key)).ToArray();
                foreach (var notice in notices)
                    _notices.Remove(notice);
                return notices;
            }
        }

        internal string[] Scan(string root, bool requireResolved)
        {
            lock (_sync)
            {
                if (string.IsNullOrEmpty(root))
                    return new string[0];

                string key = RootKey(root);
                List<Move> rollback;
                if (_unfinishedRollbacks.TryGetValue(key, out rollback))
                {
                    var remaining = RollBack(rollback);
                    if (remaining.Count > 0)
                    {
                        _unfinishedRollbacks[key] = remaining;
                        if (requireResolved)
                            throw new IOException(RollbackMessage);
                        return new string[0];
                    }
                    _unfinishedRollbacks.Remove(key);
                }

                if (!Directory.Exists(root))
                    return new string[0];

                var directories = Directory.GetDirectories(key);
                Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
                var copies = directories.Where(dir => !ModPackageSafety.IsReservedModFolderName(Path.GetFileName(dir)))
                    .Select(dir => ReadCopy(dir)).Where(copy => copy != null);
                var pending = new List<Notice>();
                var losers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var group in copies.GroupBy(copy => copy.Id, StringComparer.OrdinalIgnoreCase))
                {
                    var ranked = group.ToArray();
                    if (ranked.Length < 2)
                        continue;
                    Array.Sort(ranked, CompareCopies);
                    var moved = ranked.Skip(1).ToArray();
                    pending.Add(new Notice { Root = key, Kept = ranked[0], Moved = moved });
                    foreach (var copy in moved)
                        losers.Add(copy.Path);
                }

                try
                {
                    var moves = new List<Move>();
                    foreach (var copy in pending.SelectMany(notice => notice.Moved))
                    {
                        copy.Backup = GetBackupPath(key, copy.Path);
                        moves.Add(new Move { From = copy.Path, To = copy.Backup });
                    }
                    MoveTogether(key, moves);
                    _notices.AddRange(pending);
                    _blocked.Remove(key);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    _blocked.Add(key);
                    System.Diagnostics.Trace.TraceWarning(ex.ToString());
                    if (requireResolved)
                        throw new IOException(GetBlockMessage(key), ex);
                }

                return directories.Where(dir => !losers.Contains(dir)).ToArray();
            }
        }

        // A false result means the notice no longer describes the files on disk.
        internal bool Undo(Notice notice)
        {
            lock (_sync)
            {
                try
                {
                    if (_unfinishedRollbacks.ContainsKey(notice.Root))
                        throw new IOException(RollbackMessage);

                    var kept = ReadCopy(notice.Kept.Path, true);
                    var restore = notice.Moved[0];
                    var backup = ReadCopy(restore.Backup, true);
                    if (!SameCopy(kept, notice.Kept) || !SameCopy(backup, restore))
                        return false;

                    EnsureRestorePathIsFree(restore.Path);

                    MoveTogether(notice.Root, new[]
                    {
                        new Move { From = kept.Path, To = GetBackupPath(notice.Root, kept.Path) },
                        new Move { From = restore.Backup, To = restore.Path }
                    });
                    return true;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    System.Diagnostics.Trace.TraceWarning(ex.ToString());
                    throw new IOException(GetBlockMessage(notice.Root) ?? RetryMessage, ex);
                }
            }
        }

        private static void EnsureRestorePathIsFree(string path)
        {
            try { File.GetAttributes(path); }
            catch (FileNotFoundException) { return; }
            catch (DirectoryNotFoundException) { return; }
            throw new IOException("The duplicate restore path is occupied.");
        }

        private static bool SameCopy(Copy current, Copy saved)
        {
            return current != null && current.Id == saved.Id && current.Written == saved.Written &&
                current.About.version == saved.About.version;
        }

        private static Copy ReadCopy(string path, bool propagateIoErrors = false)
        {
            ModTypes.ModAboutInfo about;
            string id, displayName, preview;
            if (!ModAboutReader.TryLoad(path, out about, out id, out displayName, out preview, propagateIoErrors) ||
                string.IsNullOrEmpty(about.id) || id.Length == 0)
                return null;
            return new Copy { Path = path, Id = id, About = about,
                LoaderValid = !string.IsNullOrEmpty(about.name) && !string.IsNullOrEmpty(about.version) &&
                    !string.IsNullOrEmpty(about.description) && about.authors != null && about.authors.Length > 0,
                NumericVersion = ParseNumericVersion(about.version),
                Written = File.GetLastWriteTimeUtc(Path.Combine(path, "About\\About.json")) };
        }

        internal static int CompareCopies(Copy a, Copy b)
        {
            int valid = b.LoaderValid.CompareTo(a.LoaderValid);
            if (valid != 0) return valid;
            int line = IsSmm2(b.About).CompareTo(IsSmm2(a.About));
            if (line != 0) return line;
            int version = Comparer<Version>.Default.Compare(b.NumericVersion, a.NumericVersion);
            if (version != 0) return version;
            int time = b.Written.CompareTo(a.Written);
            if (time != 0) return time;
            int path = StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path);
            return path != 0 ? path : StringComparer.Ordinal.Compare(a.Path, b.Path);
        }

        private static Version ParseNumericVersion(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            string[] parts = value.Trim().Split('.');
            var numbers = new int[4];
            if (parts.Length > numbers.Length)
                return null;
            for (int i = 0; i < parts.Length; i++)
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                    return null;
            return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        }

        private static bool IsSmm2(ModTypes.ModAboutInfo about)
        {
            return IsSmm2Version(string.IsNullOrEmpty(about.requiredModApiVersion) ? about.modApiVersion : about.requiredModApiVersion)
                || IsSmm2Version(string.IsNullOrEmpty(about.requiredShelteredApiVersion) ? about.shelteredApiVersion : about.requiredShelteredApiVersion);
        }

        private static bool IsSmm2Version(string version)
        {
            int major;
            return ApiCompatibilityService.TryGetMajorVersion(version, out major) && major >= 2;
        }

        private static string GetBackupPath(string root, string original)
        {
            // A nested directory keeps these copies out of Nexus's same-ID backup pruning.
            string backup = Path.Combine(root, "_smm_backup");
            string duplicates = Path.Combine(backup, "duplicates");
            foreach (string directory in new[] { backup, duplicates })
            {
                Directory.CreateDirectory(directory);
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Duplicate backup path is a reparse point.");
            }
            return Path.Combine(duplicates, Guid.NewGuid().ToString("N") + "_" + Path.GetFileName(original));
        }

        private void MoveTogether(string root, IEnumerable<Move> moves)
        {
            var completed = new Stack<Move>();
            try
            {
                foreach (var move in moves)
                {
                    _moveDirectory(move.From, move.To);
                    completed.Push(move);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                var remaining = RollBack(completed);
                if (remaining.Count > 0)
                    _unfinishedRollbacks[root] = remaining;
                throw;
            }
        }

        private List<Move> RollBack(IEnumerable<Move> moves)
        {
            var remaining = new List<Move>();
            foreach (var move in moves)
            {
                try { _moveDirectory(move.To, move.From); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    remaining.Add(move);
                    System.Diagnostics.Trace.TraceError(ex.ToString());
                }
            }
            return remaining;
        }
    }
}
