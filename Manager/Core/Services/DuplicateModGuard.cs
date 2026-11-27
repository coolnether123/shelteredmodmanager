using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Manager.Core.Services
{
    // Used by Manager discovery, order writes, and modded launch.
    internal sealed class DuplicateModGuard
    {
        internal const string RetryMessage = "Could not move a duplicate mod. Close the game and try again.";
        internal static readonly DuplicateModGuard Shared = new DuplicateModGuard();

        internal sealed class Copy
        {
            internal string Path;
            internal string Id;
            internal ModTypes.ModAboutInfo About;
            internal DateTime Written;
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

        internal static string RootKey(string root)
        {
            return Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        }

        internal bool IsBlocked(string root)
        {
            lock (_sync)
                return _blocked.Contains(RootKey(root));
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
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    return new string[0];

                string key = RootKey(root);
                var directories = Directory.GetDirectories(key);
                Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
                var copies = directories.Where(dir => !ModPackageSafety.IsReservedModFolderName(Path.GetFileName(dir)))
                    .Select(ReadCopy).Where(copy => copy != null);
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
                    MoveTogether(moves);
                    _notices.AddRange(pending);
                    _blocked.Remove(key);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    _blocked.Add(key);
                    System.Diagnostics.Trace.TraceWarning(ex.ToString());
                    if (requireResolved)
                        throw new IOException(RetryMessage, ex);
                }

                return directories.Where(dir => !losers.Contains(dir)).ToArray();
            }
        }

        internal void Undo(Notice notice)
        {
            lock (_sync)
            {
                var kept = ReadCopy(notice.Kept.Path);
                var restore = notice.Moved[0];
                var backup = ReadCopy(restore.Backup);
                if (!SameCopy(kept, notice.Kept) || !SameCopy(backup, restore))
                    throw new IOException(RetryMessage);

                try
                {
                    MoveTogether(new[]
                    {
                        new Move { From = kept.Path, To = GetBackupPath(notice.Root, kept.Path) },
                        new Move { From = restore.Backup, To = restore.Path }
                    });
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    System.Diagnostics.Trace.TraceWarning(ex.ToString());
                    throw new IOException(RetryMessage, ex);
                }
            }
        }

        private static bool SameCopy(Copy current, Copy saved)
        {
            return current != null && current.Id == saved.Id && current.Written == saved.Written &&
                current.About.version == saved.About.version;
        }

        private static Copy ReadCopy(string path)
        {
            ModTypes.ModAboutInfo about;
            string id, error;
            if (!ModPackageSafety.TryReadRequiredAbout(path, out about, out id, out error) || id.Length == 0)
                return null;
            return new Copy { Path = path, Id = id, About = about,
                Written = File.GetLastWriteTimeUtc(Path.Combine(path, "About\\About.json")) };
        }

        private static int CompareCopies(Copy a, Copy b)
        {
            int version = NexusVersionComparer.CompareVersions(b.About.version, a.About.version);
            if (version != 0) return version;
            int line = IsSmm2(b.About).CompareTo(IsSmm2(a.About));
            if (line != 0) return line;
            int time = b.Written.CompareTo(a.Written);
            if (time != 0) return time;
            return StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path);
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

        private static void MoveTogether(IEnumerable<Move> moves)
        {
            var completed = new Stack<Move>();
            try
            {
                foreach (var move in moves)
                {
                    Directory.Move(move.From, move.To);
                    completed.Push(move);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                foreach (var move in completed)
                {
                    try { Directory.Move(move.To, move.From); }
                    catch (Exception rollback) when (rollback is IOException || rollback is UnauthorizedAccessException)
                    {
                        System.Diagnostics.Trace.TraceError(rollback.ToString());
                    }
                }
                throw;
            }
        }
    }
}
