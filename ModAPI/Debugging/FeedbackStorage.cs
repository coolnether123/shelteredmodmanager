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

        public void AppendEntry(
            DateTime timestamp,
            string text,
            IList<KeyValuePair<string, string>> context,
            string screenshotPath,
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
