using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Ookii.Dialogs.WinForms;

namespace Cortex.PathPicker.Host
{
    internal static class Program
    {
        private const int ExitCodeSuccess = 0;
        private const int ExitCodeFailure = 1;
        private const int ExitCodeCancelled = 2;

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var options = PathPickerOptions.Parse(args);
                if (options == null)
                {
                    WriteError("No picker options were supplied.");
                    return ExitCodeFailure;
                }

                string selectedPath;
                if (!TryShowDialog(options, out selectedPath))
                {
                    return ExitCodeCancelled;
                }

                Console.Out.Write(selectedPath ?? string.Empty);
                return ExitCodeSuccess;
            }
            catch (Exception ex)
            {
                WriteError(ex.Message ?? ex.ToString());
                return ExitCodeFailure;
            }
        }

        private static bool TryShowDialog(PathPickerOptions options, out string selectedPath)
        {
            selectedPath = string.Empty;
            if (options == null)
            {
                return false;
            }

            return options.Mode == PathPickerMode.File
                ? TrySelectFile(options, out selectedPath)
                : TrySelectFolder(options, out selectedPath);
        }

        private static bool TrySelectFolder(PathPickerOptions options, out string selectedPath)
        {
            selectedPath = string.Empty;

            if (VistaFolderBrowserDialog.IsVistaFolderDialogSupported)
            {
                using (var dialog = new VistaFolderBrowserDialog())
                {
                    dialog.Description = options.Title ?? string.Empty;
                    dialog.UseDescriptionForTitle = true;
                    dialog.SelectedPath = options.InitialPath ?? string.Empty;
                    dialog.ShowNewFolderButton = true;
                    if (dialog.ShowDialog() != DialogResult.OK)
                    {
                        return false;
                    }

                    selectedPath = NormalizePath(dialog.SelectedPath);
                    return !string.IsNullOrEmpty(selectedPath);
                }
            }

            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = options.Title ?? string.Empty;
                dialog.SelectedPath = options.InitialPath ?? string.Empty;
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return false;
                }

                selectedPath = NormalizePath(dialog.SelectedPath);
                return !string.IsNullOrEmpty(selectedPath);
            }
        }

        private static bool TrySelectFile(PathPickerOptions options, out string selectedPath)
        {
            selectedPath = string.Empty;
            using (var dialog = new VistaOpenFileDialog())
            {
                dialog.Title = options.Title ?? string.Empty;
                dialog.Filter = string.IsNullOrEmpty(options.Filter) ? "All Files|*.*" : options.Filter;
                dialog.CheckFileExists = options.CheckPathExists;
                dialog.CheckPathExists = options.CheckPathExists;
                dialog.RestoreDirectory = options.RestoreDirectory;

                if (!string.IsNullOrEmpty(options.InitialPath))
                {
                    if (Directory.Exists(options.InitialPath))
                    {
                        dialog.InitialDirectory = options.InitialPath;
                    }
                    else
                    {
                        var fileDirectory = Path.GetDirectoryName(options.InitialPath);
                        if (!string.IsNullOrEmpty(fileDirectory) && Directory.Exists(fileDirectory))
                        {
                            dialog.InitialDirectory = fileDirectory;
                        }

                        var fileName = Path.GetFileName(options.InitialPath);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            dialog.FileName = fileName;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(options.SuggestedFileName))
                {
                    dialog.FileName = options.SuggestedFileName;
                }

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return false;
                }

                selectedPath = NormalizePath(dialog.FileName);
                return !string.IsNullOrEmpty(selectedPath);
            }
        }

        private static void WriteError(string message)
        {
            Console.Error.Write(message ?? string.Empty);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private enum PathPickerMode
        {
            Folder,
            File
        }

        private sealed class PathPickerOptions
        {
            public PathPickerMode Mode = PathPickerMode.Folder;
            public string Title = string.Empty;
            public string InitialPath = string.Empty;
            public string SuggestedFileName = string.Empty;
            public string Filter = string.Empty;
            public bool CheckPathExists = true;
            public bool RestoreDirectory = true;

            public static PathPickerOptions Parse(string[] args)
            {
                var options = new PathPickerOptions();
                if (args == null || args.Length == 0)
                {
                    return options;
                }

                options.Mode = string.Equals(args[0], "file", StringComparison.OrdinalIgnoreCase)
                    ? PathPickerMode.File
                    : PathPickerMode.Folder;

                var values = ParseNamedArguments(args);
                options.Title = GetValue(values, "--title");
                options.InitialPath = GetValue(values, "--initial-path");
                options.SuggestedFileName = GetValue(values, "--suggested-file-name");
                options.Filter = GetValue(values, "--filter");
                options.CheckPathExists = ParseBool(GetValue(values, "--check-path-exists"), true);
                options.RestoreDirectory = ParseBool(GetValue(values, "--restore-directory"), true);
                return options;
            }

            private static Dictionary<string, string> ParseNamedArguments(string[] args)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var index = 1; index < args.Length; index++)
                {
                    var name = args[index];
                    if (string.IsNullOrEmpty(name) || !name.StartsWith("--", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var value = index + 1 < args.Length ? args[index + 1] : string.Empty;
                    values[name] = value ?? string.Empty;
                    index++;
                }

                return values;
            }

            private static string GetValue(IDictionary<string, string> values, string name)
            {
                string value;
                return values != null && !string.IsNullOrEmpty(name) && values.TryGetValue(name, out value)
                    ? value ?? string.Empty
                    : string.Empty;
            }

            private static bool ParseBool(string value, bool fallback)
            {
                bool parsed;
                return bool.TryParse(value, out parsed) ? parsed : fallback;
            }
        }
    }
}
