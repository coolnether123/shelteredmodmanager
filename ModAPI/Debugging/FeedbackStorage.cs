using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ModAPI.Debugging
{
    internal sealed class FeedbackStorage
    {
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private readonly string _rootPath;
        private readonly string _scratchPath;
        private readonly string _entriesPath;
        private readonly string _screenshotsPath;

        public FeedbackStorage(string rootPath)
        {
            _rootPath = Path.GetFullPath(rootPath);
            _scratchPath = Path.Combine(_rootPath, "scratch.txt");
            _entriesPath = Path.Combine(_rootPath, "entries.md");
            _screenshotsPath = Path.Combine(_rootPath, "screenshots");
        }

        public string RootPath
        {
            get { return _rootPath; }
        }

        public void EnsureReady()
        {
            Directory.CreateDirectory(_rootPath);
            Directory.CreateDirectory(_screenshotsPath);
        }

        public string LoadScratch()
        {
            EnsureReady();
            return File.Exists(_scratchPath) ? File.ReadAllText(_scratchPath, Utf8WithoutBom) : string.Empty;
        }

        public void SaveScratch(string text)
        {
            EnsureReady();
            File.WriteAllText(_scratchPath, text ?? string.Empty, Utf8WithoutBom);
        }

        public string ReserveScreenshotPath(DateTime timestamp)
        {
            EnsureReady();
            string stem = timestamp.ToString("yyyyMMdd_HHmmss_fff");
            string path = Path.Combine(_screenshotsPath, stem + ".png");
            int suffix = 2;
            while (File.Exists(path))
            {
                path = Path.Combine(_screenshotsPath, stem + "_" + suffix + ".png");
                suffix++;
            }

            return path;
        }

        public string ReadLogExcerpt(string logPath, int maxLines, int maxBytes)
        {
            if (string.IsNullOrEmpty(logPath))
                return string.Empty;

            try
            {
                string fullPath = Path.GetFullPath(logPath);
                if (!File.Exists(fullPath))
                    return "[Runtime log was not found at submission time.]";

                int safeLines = Math.Max(1, maxLines);
                int safeBytes = Math.Max(1024, maxBytes);
                byte[] buffer;
                bool startedMidFile;
                using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long offset = Math.Max(0L, stream.Length - safeBytes);
                    startedMidFile = offset > 0L;
                    stream.Seek(offset, SeekOrigin.Begin);
                    int count = (int)Math.Min((long)safeBytes, stream.Length - offset);
                    buffer = new byte[count];
                    int read = 0;
                    while (read < count)
                    {
                        int next = stream.Read(buffer, read, count - read);
                        if (next <= 0)
                            break;
                        read += next;
                    }
                    if (read != count)
                        Array.Resize(ref buffer, read);
                }

                string text = Utf8WithoutBom.GetString(buffer);
                string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                int first = startedMidFile && lines.Length > 1 ? 1 : 0;
                first = Math.Max(first, lines.Length - safeLines);
                return string.Join(Environment.NewLine, lines, first, lines.Length - first).TrimEnd();
            }
            catch (Exception ex)
            {
                return "[Runtime log excerpt unavailable: " + ex.Message + "]";
            }
        }

        public void AppendEntry(
            DateTime timestamp,
            string text,
            IList<KeyValuePair<string, string>> context,
            string screenshotPath,
            string logExcerpt,
            bool screenshotOnly)
        {
            EnsureReady();
            StringBuilder entry = new StringBuilder();
            if (!File.Exists(_entriesPath) || new FileInfo(_entriesPath).Length == 0)
                entry.AppendLine("# Developer Feedback").AppendLine();

            entry.Append("## ").AppendLine(timestamp.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            entry.AppendLine();
            entry.Append("- Screenshot: `screenshots/")
                .Append(Path.GetFileName(screenshotPath))
                .AppendLine("`");

            if (context != null)
            {
                for (int i = 0; i < context.Count; i++)
                {
                    KeyValuePair<string, string> line = context[i];
                    entry.Append("- ")
                        .Append(NormalizeInline(line.Key))
                        .Append(": ")
                        .AppendLine(NormalizeInline(line.Value));
                }
            }

            entry.AppendLine();
            if (screenshotOnly)
            {
                entry.AppendLine("_(Screenshot only)_");
            }
            else
            {
                entry.AppendLine(text ?? string.Empty);
            }

            if (!string.IsNullOrEmpty(logExcerpt))
            {
                entry.AppendLine().AppendLine("### Runtime log near submission").AppendLine();
                string[] logLines = logExcerpt.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                for (int i = 0; i < logLines.Length; i++)
                    entry.Append("    ").AppendLine(logLines[i]);
            }

            entry.AppendLine().AppendLine("---").AppendLine();
            File.AppendAllText(_entriesPath, entry.ToString(), Utf8WithoutBom);
        }

        private static string NormalizeInline(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "(none)";

            return value.Replace("\r", " ").Replace("\n", " ");
        }
    }
}
