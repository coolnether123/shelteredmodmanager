using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Manager.Core.Services
{
    /// <summary>
    /// Finds evidence that Sheltered crashed and bundles the session's logs
    /// into one zip a player can send to a mod developer. Sheltered's Unity
    /// player writes a "yyyy-MM-dd_HHmmss" folder beside the game (error.log,
    /// output_log.txt, crash.dmp) when it crashes; newer Unity builds use
    /// %TEMP%\company\product\Crashes instead. Both are checked.
    /// </summary>
    public sealed class CrashReportService
    {
        private const long MaximumLogBytes = 8L * 1024 * 1024;
        private const long MaximumDumpBytes = 64L * 1024 * 1024;
        private static readonly Regex CrashFolderPattern = new Regex(@"^\d{4}-\d{2}-\d{2}_\d{6}$", RegexOptions.CultureInvariant);

        /// <summary>Where finished reports are saved: Documents\Sheltered Mod Manager\Crash reports.</summary>
        public static string ReportsFolder
        {
            get
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(Path.Combine(documents, "Sheltered Mod Manager"), "Crash reports");
            }
        }

        /// <summary>Crash folders created after <paramref name="sinceUtc"/>, newest first.</summary>
        public List<string> FindCrashFolders(string gameDir, DateTime sinceUtc)
        {
            List<KeyValuePair<DateTime, string>> found = new List<KeyValuePair<DateTime, string>>();
            AddCrashFolders(found, gameDir, sinceUtc, true);

            // Newer Unity players: %TEMP%\<company>\<product>\Crashes\Crash_*. The
            // exact pair comes from app.info, so the temp folder is never scanned.
            foreach (string[] identity in UnityIdentities(gameDir))
            {
                try
                {
                    string crashes = Path.Combine(Path.Combine(Path.Combine(Path.GetTempPath(), identity[0]), identity[1]), "Crashes");
                    AddCrashFolders(found, crashes, sinceUtc, false);
                }
                catch (ArgumentException)
                {
                    // Names with characters Windows paths reject cannot have a folder.
                }
            }

            // Newest first; folders created in the same instant fall back to path order.
            found.Sort(delegate(KeyValuePair<DateTime, string> a, KeyValuePair<DateTime, string> b)
            {
                int byTime = b.Key.CompareTo(a.Key);
                return byTime != 0 ? byTime : string.Compare(a.Value, b.Value, StringComparison.OrdinalIgnoreCase);
            });
            List<string> result = new List<string>();
            foreach (KeyValuePair<DateTime, string> pair in found)
                result.Add(pair.Value);
            return result;
        }

        /// <summary>
        /// Unity 5.5 and later record "company" and "product" on the first two
        /// lines of &lt;game&gt;_Data\app.info; logs and crash folders are filed
        /// under that pair. Older players (such as the Steam build) have none.
        /// </summary>
        private static List<string[]> UnityIdentities(string gameDir)
        {
            List<string[]> identities = new List<string[]>();
            try
            {
                if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
                    return identities;
                string[] dataFolders = Directory.GetDirectories(gameDir, "*_Data");
                Array.Sort(dataFolders, StringComparer.OrdinalIgnoreCase);
                foreach (string dataFolder in dataFolders)
                {
                    string info = Path.Combine(dataFolder, "app.info");
                    if (!File.Exists(info))
                        continue;
                    string[] lines = File.ReadAllLines(info);
                    if (lines.Length >= 2 && lines[0].Trim().Length > 0 && lines[1].Trim().Length > 0)
                        identities.Add(new string[] { lines[0].Trim(), lines[1].Trim() });
                }
            }
            catch
            {
            }
            return identities;
        }

        private static void AddCrashFolders(List<KeyValuePair<DateTime, string>> found, string parent, DateTime sinceUtc, bool requireTimestampName)
        {
            string[] folders;
            try
            {
                if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                    return;
                folders = Directory.GetDirectories(parent);
            }
            catch
            {
                return;
            }
            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                if (requireTimestampName ? !CrashFolderPattern.IsMatch(name) : !name.StartsWith("Crash", StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    if (!File.Exists(Path.Combine(folder, "error.log")) && !File.Exists(Path.Combine(folder, "crash.dmp")))
                        continue;
                    DateTime created = Directory.GetCreationTimeUtc(folder);
                    if (created > sinceUtc)
                        found.Add(new KeyValuePair<DateTime, string>(created, folder));
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Writes a zip with the crash folder's files, the game's Unity log and
        /// the SMM logs written between <paramref name="fromUtc"/> and
        /// <paramref name="toUtc"/>. Text is capped per file (the end is kept)
        /// and the Windows profile path is replaced with %USERPROFILE%.
        /// Returns the zip path, or null with <paramref name="error"/> set.
        /// </summary>
        public string CreateReport(
            string gameDir,
            string crashFolder,
            DateTime fromUtc,
            DateTime toUtc,
            bool includeDump,
            string sessionSummary,
            out string error)
        {
            return CreateReport(gameDir, crashFolder, fromUtc, toUtc, includeDump, sessionSummary, ReportsFolder, out error);
        }

        public string CreateReport(
            string gameDir,
            string crashFolder,
            DateTime fromUtc,
            DateTime toUtc,
            bool includeDump,
            string sessionSummary,
            string outputFolder,
            out string error)
        {
            error = null;
            List<ZipFileWriter.Entry> entries = new List<ZipFileWriter.Entry>();
            List<string> included = new List<string>();
            List<string> notes = new List<string>();

            try
            {
                if (!string.IsNullOrEmpty(crashFolder) && Directory.Exists(crashFolder))
                {
                    string[] crashFiles = Directory.GetFiles(crashFolder);
                    Array.Sort(crashFiles, StringComparer.OrdinalIgnoreCase);
                    foreach (string file in crashFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        bool dump = fileName.EndsWith(".dmp", StringComparison.OrdinalIgnoreCase);
                        if (dump && !includeDump)
                            continue;
                        // Half a dump is useless, so an oversized one is left out rather than cut.
                        if (dump && new FileInfo(file).Length > MaximumDumpBytes)
                        {
                            notes.Add(fileName + " was left out: " + (new FileInfo(file).Length / (1024 * 1024)) + " MB is too large to share.");
                            continue;
                        }
                        AddFile(entries, included, file, "crash/" + fileName, !dump, dump ? MaximumDumpBytes : MaximumLogBytes);
                    }
                }

                // The Unity player log for the session: Sheltered_Data\output_log.txt beside the game,
                // or Player.log under LocalLow for newer builds.
                if (!string.IsNullOrEmpty(gameDir) && Directory.Exists(gameDir))
                {
                    string[] dataFolders = Directory.GetDirectories(gameDir, "*_Data");
                    Array.Sort(dataFolders, StringComparer.OrdinalIgnoreCase);
                    foreach (string dataFolder in dataFolders)
                        AddIfInWindow(entries, included, Path.Combine(dataFolder, "output_log.txt"), "game/output_log.txt", fromUtc, toUtc);

                    string smm = Path.Combine(gameDir, "SMM");
                    AddIfInWindow(entries, included, Path.Combine(smm, "mod_manager.log"), "smm/mod_manager.log", fromUtc, toUtc);
                    if (Directory.Exists(smm))
                    {
                        // Long sessions roll over into dated logs; keep the newest few from this session.
                        string[] rolled = Directory.GetFiles(smm, "mod_manager_*.log");
                        Array.Sort(rolled, delegate(string a, string b)
                        {
                            int byTime = File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a));
                            return byTime != 0 ? byTime : string.Compare(b, a, StringComparison.OrdinalIgnoreCase);
                        });
                        int added = 0;
                        foreach (string log in rolled)
                        {
                            if (added >= 3)
                                break;
                            if (AddIfInWindow(entries, included, log, "smm/" + Path.GetFileName(log), fromUtc, toUtc))
                                added++;
                        }
                    }
                }
                AddLocalLowPlayerLogs(gameDir, entries, included, fromUtc, toUtc);

                StringBuilder info = new StringBuilder();
                info.AppendLine(sessionSummary ?? string.Empty);
                info.AppendLine("Crash folder: " + (string.IsNullOrEmpty(crashFolder) ? "none" : Path.GetFileName(crashFolder)));
                info.AppendLine("Crash dump included: " + (includeDump ? "yes" : "no"));
                info.AppendLine("Files:");
                foreach (string name in included)
                    info.AppendLine("  " + name);
                foreach (string note in notes)
                    info.AppendLine("Note: " + note);
                entries.Insert(0, new ZipFileWriter.Entry("session-info.txt", Encoding.UTF8.GetBytes(Redact(info.ToString()))));

                Directory.CreateDirectory(outputFolder);
                string zipPath = FreeReportPath(outputFolder, DateTime.Now);
                ZipFileWriter.Write(zipPath, entries, true);
                return zipPath;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// "Sheltered crash yyyy-MM-dd HH.mm.ss.zip", numbered when a report
        /// from the same second exists. Formatted with the invariant culture so
        /// the name is the same whatever the PC's calendar.
        /// </summary>
        private static string FreeReportPath(string folder, DateTime when)
        {
            string stem = "Sheltered crash " + when.ToString("yyyy-MM-dd HH.mm.ss", System.Globalization.CultureInfo.InvariantCulture);
            string path = Path.Combine(folder, stem + ".zip");
            for (int n = 2; File.Exists(path); n++)
                path = Path.Combine(folder, stem + " (" + n + ").zip");
            return path;
        }

        /// <summary>Two sources with the same name (say, two *_Data folders) get "name (2)".</summary>
        private static string UniqueEntryName(List<string> included, string entryName)
        {
            string candidate = entryName;
            string extension = Path.GetExtension(entryName);
            string stem = entryName.Substring(0, entryName.Length - extension.Length);
            for (int n = 2; included.Exists(delegate(string existing) { return string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase); }); n++)
                candidate = stem + " (" + n + ")" + extension;
            return candidate;
        }

        private static bool AddIfInWindow(List<ZipFileWriter.Entry> entries, List<string> included, string path, string entryName, DateTime fromUtc, DateTime toUtc)
        {
            try
            {
                if (!File.Exists(path))
                    return false;
                DateTime written = File.GetLastWriteTimeUtc(path);
                if (written < fromUtc || written > toUtc)
                    return false;
                return AddFile(entries, included, path, entryName, true, MaximumLogBytes);
            }
            catch
            {
                return false;
            }
        }

        private static void AddLocalLowPlayerLogs(string gameDir, List<ZipFileWriter.Entry> entries, List<string> included, DateTime fromUtc, DateTime toUtc)
        {
            string localLow = Path.Combine(Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty, "AppData"), "LocalLow");
            foreach (string[] identity in UnityIdentities(gameDir))
            {
                try
                {
                    string product = Path.Combine(Path.Combine(localLow, identity[0]), identity[1]);
                    foreach (string name in new string[] { "Player.log", "output_log.txt" })
                        AddIfInWindow(entries, included, Path.Combine(product, name), "game/" + name, fromUtc, toUtc);
                }
                catch (ArgumentException)
                {
                    // Names with characters Windows paths reject cannot have a folder.
                }
            }
        }

        private static bool AddFile(List<ZipFileWriter.Entry> entries, List<string> included, string path, string entryName, bool text, long maximumBytes)
        {
            byte[] data = ReadTail(path, maximumBytes);
            if (data == null)
                return false;
            if (text)
                data = Encoding.UTF8.GetBytes(Redact(Encoding.UTF8.GetString(data)));
            string name = UniqueEntryName(included, entryName);
            entries.Add(new ZipFileWriter.Entry(name, data));
            included.Add(name);
            return true;
        }

        /// <summary>Reads a file that the game may still hold open, keeping only its last bytes when large.</summary>
        private static byte[] ReadTail(string path, long maximumBytes)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long start = Math.Max(0, stream.Length - maximumBytes);
                    stream.Position = start;
                    byte[] buffer = new byte[stream.Length - start];
                    int read = 0;
                    while (read < buffer.Length)
                    {
                        int chunk = stream.Read(buffer, read, buffer.Length - read);
                        if (chunk <= 0)
                            break;
                        read += chunk;
                    }
                    if (read < buffer.Length)
                        Array.Resize(ref buffer, read);
                    return buffer;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Hides the Windows user folder (which contains the account name) in shared logs.</summary>
        private static string Redact(string text)
        {
            string profile = (Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty);
            if (string.IsNullOrEmpty(profile) || string.IsNullOrEmpty(text))
                return text;
            string result = Regex.Replace(text, Regex.Escape(profile), "%USERPROFILE%", RegexOptions.IgnoreCase);
            return Regex.Replace(result, Regex.Escape(profile.Replace('\\', '/')), "%USERPROFILE%", RegexOptions.IgnoreCase);
        }
    }
}
