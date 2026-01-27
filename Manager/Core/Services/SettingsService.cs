using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Handles reading and writing application settings to INI file.
    /// Single responsibility: Settings persistence only.
    /// </summary>
    public class SettingsService
    {
        private const string INI_FILENAME = "mod_manager.ini";
        private readonly string _iniPath;
        private FileSystemWatcher _watcher;
        private DateTime _lastRead = DateTime.MinValue;
        private bool _suppressWatcher = false;
        
        public delegate void SettingsChangedHandler(AppSettings settings);
        public event SettingsChangedHandler SettingsChanged;

        public SettingsService()
        {
            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string smmDir = Path.Combine(exeDir, "SMM");
            string binDir;
            
            if (Directory.Exists(smmDir))
            {
                // Deployed mode: use SMM/bin folder for centralized config
                binDir = Path.Combine(smmDir, "bin");
            }
            else
            {
                // Local/Dev mode: use Manager/bin folder
                binDir = Path.Combine(exeDir, "bin");
            }

            if (!Directory.Exists(binDir)) Directory.CreateDirectory(binDir);
            _iniPath = Path.Combine(binDir, INI_FILENAME);
            
            SetupWatcher(binDir);
        }

        private void SetupWatcher(string directory)
        {
            try
            {
                _watcher = new FileSystemWatcher(directory, INI_FILENAME);
                _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
                _watcher.Changed += (s, e) => OnFileChanged();
                _watcher.Created += (s, e) => OnFileChanged();
                _watcher.EnableRaisingEvents = true;
            }
            catch { }
        }

        private void OnFileChanged()
        {
            if (_suppressWatcher) return;
            
            // Debounce: ignore multiple events in short succession
            if ((DateTime.Now - _lastRead).TotalMilliseconds < 500) return;
            _lastRead = DateTime.Now;

            try
            {
                // Give the other process a tiny bit of time to release the file
                System.Threading.Thread.Sleep(50);
                var settings = Load();
                SettingsChanged?.Invoke(settings);
            }
            catch { }
        }

        public SettingsService(string customPath)
        {
            _iniPath = customPath;
        }

        /// <summary>
        /// Load settings from INI file
        /// </summary>
        public AppSettings Load()
        {
            var settings = new AppSettings();
            var raw = ReadIniFile();

            // Game path with auto-detection fallback
            string gamePath;
            if (raw.TryGetValue("GamePath", out gamePath))
            {
                settings.GamePath = gamePath;
            }
            
            if (string.IsNullOrEmpty(settings.GamePath) || !File.Exists(settings.GamePath))
            {
                string detected = TryAutoDetectGamePath();
                if (!string.IsNullOrEmpty(detected))
                    settings.GamePath = detected;
            }

            // Mods path derived from game path
            if (!string.IsNullOrEmpty(settings.GamePath) && File.Exists(settings.GamePath))
            {
                settings.ModsPath = Path.Combine(Path.GetDirectoryName(settings.GamePath), "mods");
            }

            // UI settings
            string darkMode;
            if (raw.TryGetValue("DarkMode", out darkMode))
            {
                bool dm;
                if (bool.TryParse(darkMode, out dm))
                    settings.DarkMode = dm;
            }

            // Developer settings
            string devMode;
            if (raw.TryGetValue("DevMode", out devMode))
            {
                bool dv;
                if (bool.TryParse(devMode, out dv))
                    settings.DevMode = dv;
            }

            string logLevel;
            if (raw.TryGetValue("LogLevel", out logLevel))
                settings.LogLevel = logLevel;

            string logCategories;
            if (raw.TryGetValue("LogCategories", out logCategories))
            {
                var categories = logCategories.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                settings.LogCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var cat in categories)
                {
                    settings.LogCategories.Add(cat.Trim());
                }
            }

            string ignoreOrder;
            if (raw.TryGetValue("IgnoreOrderChecks", out ignoreOrder))
            {
                bool io;
                if (bool.TryParse(ignoreOrder, out io))
                    settings.IgnoreOrderChecks = io;
            }

            string skipHarmony;
            if (raw.TryGetValue("SkipHarmonyDependencyCheck", out skipHarmony))
            {
                bool sh;
                if (bool.TryParse(skipHarmony, out sh))
                    settings.SkipHarmonyDependencyCheck = sh;
            }

            string bitness;
            if (raw.TryGetValue("GameBitness", out bitness))
                settings.GameBitness = bitness;
            
            string autoCondense;
            if (raw.TryGetValue("AutoCondenseSaves", out autoCondense))
                settings.AutoCondenseSaves = autoCondense;

            string apiVersion;
            if (raw.TryGetValue("InstalledModApiVersion", out apiVersion))
                settings.InstalledModApiVersion = apiVersion;

            string autoLoadSlot;
            if (raw.TryGetValue("AutoLoadSaveSlot", out autoLoadSlot))
            {
                if (int.TryParse(autoLoadSlot, out int slot))
                    settings.AutoLoadSaveSlot = slot;
            }

            return settings;
        }

        /// <summary>
        /// Save settings to INI file
        /// </summary>
        public void Save(AppSettings settings)
        {
            if (settings == null) return;

            // Read existing keys first to preserve unknown ones
            var data = ReadIniFile();
            
            data["GamePath"] = settings.GamePath ?? string.Empty;
            data["DarkMode"] = settings.DarkMode.ToString();
            data["DevMode"] = settings.DevMode.ToString();
            data["LogLevel"] = settings.LogLevel ?? "Info";
            
            // Convert HashSet to comma-separated string
            var cats = settings.LogCategories ?? new HashSet<string>();
            data["LogCategories"] = string.Join(",", new List<string>(cats).ToArray());
            
            data["IgnoreOrderChecks"] = settings.IgnoreOrderChecks.ToString();
            data["SkipHarmonyDependencyCheck"] = settings.SkipHarmonyDependencyCheck.ToString();
            data["GameBitness"] = settings.GameBitness ?? string.Empty;
            data["AutoCondenseSaves"] = settings.AutoCondenseSaves ?? "ask";
            data["InstalledModApiVersion"] = settings.InstalledModApiVersion ?? string.Empty;
            data["AutoLoadSaveSlot"] = settings.AutoLoadSaveSlot.ToString();

            WriteIniFile(data);
            
            if (SettingsChanged != null)
                SettingsChanged(settings);
        }

        private Dictionary<string, string> ReadIniFile()
        {
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            if (!File.Exists(_iniPath)) 
                return settings;

            try
            {
                foreach (var line in File.ReadAllLines(_iniPath))
                {
                    if (string.IsNullOrEmpty(line) || line.Trim().Length == 0 || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split(new char[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        settings[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error reading INI: " + ex.Message);
            }

            return settings;
        }

        private void WriteIniFile(Dictionary<string, string> data)
        {
            try
            {
                _suppressWatcher = true;
                
                var lines = new List<string>();
                lines.Add("# Sheltered Mod Manager Configuration");
                lines.Add("# Last modified: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                lines.Add("");

                var sortedKeys = new List<string>(data.Keys);
                sortedKeys.Sort();
                
                foreach (var key in sortedKeys)
                {
                    lines.Add(key + "=" + data[key]);
                }

                File.WriteAllLines(_iniPath, lines.ToArray());
                
                // Give the watcher time to process any pending events before re-enabling
                System.Threading.Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error writing INI: " + ex.Message);
            }
            finally
            {
                _suppressWatcher = false;
            }
        }

        private string TryAutoDetectGamePath()
        {
            try
            {
                string[] exeNames = new string[] { "Sheltered.exe", "ShelteredWindows64_EOS.exe" };

                // 1. Check if game is already running
                try
                {
                    foreach (var exeName in exeNames)
                    {
                        var procs = System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exeName));
                        if (procs.Length > 0)
                        {
                            string path = procs[0].MainModule.FileName;
                            if (File.Exists(path)) return path;
                        }
                    }
                }
                catch { }

                // 2. Check current directory and parents
                string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var searchDirs = new List<string>();
                searchDirs.Add(exeDir);
                
                var parent = Directory.GetParent(exeDir);
                if (parent != null) searchDirs.Add(parent.FullName);
                
                DirectoryInfo grandparent = null;
                if (parent != null) grandparent = parent.Parent;
                if (grandparent != null) searchDirs.Add(grandparent.FullName);

                // 3. Check common Steam locations
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 356040"))
                    {
                        var installPath = key?.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(installPath)) searchDirs.Add(installPath);
                    }
                }
                catch { }

                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    {
                        var steamPath = key?.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(steamPath))
                        {
                            searchDirs.Add(Path.Combine(steamPath, @"steamapps\common\Sheltered"));
                        }
                    }
                }
                catch { }

                // Common C: paths
                searchDirs.Add(@"C:\Program Files (x86)\Steam\steamapps\common\Sheltered");
                searchDirs.Add(@"C:\Program Files\Steam\steamapps\common\Sheltered");

                foreach (var dir in searchDirs)
                {
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

                    foreach (var exeName in exeNames)
                    {
                        string path = Path.Combine(dir, exeName);
                        if (File.Exists(path))
                            return path;
                    }
                }
            }
            catch { }

            return string.Empty;
        }
    }
}
