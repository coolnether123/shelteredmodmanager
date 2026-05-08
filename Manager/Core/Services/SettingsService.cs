using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Manager.Core.Games;
using Manager.Core.Games.Detection;
using Manager.Core.Games.Models;
using Manager.Core.Models;
using Manager.Core.Security;

namespace Manager.Core.Services
{
    /// <summary>
    /// Handles reading and writing application settings to INI file.
    /// Single responsibility: Settings persistence only.
    /// </summary>
    public class SettingsService
    {
        private const string INI_FILENAME = "mod_manager.ini";
        private const string LegacyNexusApiKeyKey = "NexusApiKey";
        private const string ProtectedNexusApiKeyKey = "NexusApiKeyProtected";
        private readonly string _iniPath;
        private readonly GameProfileRegistry _profileRegistry;
        private readonly GamePathDetector _pathDetector;
        private FileSystemWatcher _watcher;
        private DateTime _lastRead = DateTime.MinValue;
        private bool _suppressWatcher = false;
        
        public delegate void SettingsChangedHandler(AppSettings settings);
        public event SettingsChangedHandler SettingsChanged;

        public SettingsService()
            : this(GameProfileRegistry.CreateDefault())
        {
        }

        public SettingsService(GameProfileRegistry profileRegistry)
        {
            _profileRegistry = profileRegistry ?? GameProfileRegistry.CreateDefault();
            _pathDetector = new GamePathDetector();
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
            _profileRegistry = GameProfileRegistry.CreateDefault();
            _pathDetector = new GamePathDetector();
            _iniPath = customPath;
        }

        /// <summary>
        /// Load settings from INI file
        /// </summary>
        public AppSettings Load()
        {
            var settings = new AppSettings();
            var raw = ReadIniFile();

            string selectedGameId;
            if (raw.TryGetValue("SelectedGameId", out selectedGameId))
                settings.SelectedGameId = selectedGameId;

            GameProfile profile = _profileRegistry.Resolve(settings.SelectedGameId);
            settings.SelectedGameId = profile.Id;

            // Game path with auto-detection fallback
            string gamePath;
            if (raw.TryGetValue("GamePath", out gamePath))
            {
                settings.GamePath = gamePath;
            }
            
            if (string.IsNullOrEmpty(settings.GamePath) || !File.Exists(settings.GamePath))
            {
                string detected = TryAutoDetectGamePath(profile);
                if (!string.IsNullOrEmpty(detected))
                    settings.GamePath = detected;
            }

            // Mods path derived from game path
            if (!string.IsNullOrEmpty(settings.GamePath) && File.Exists(settings.GamePath))
            {
                settings.ModsPath = profile.GetModsPath(settings.GamePath);
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

            string shelteredApiVersion;
            if (raw.TryGetValue("InstalledShelteredApiVersion", out shelteredApiVersion))
                settings.InstalledShelteredApiVersion = shelteredApiVersion;

            string installedApiVersions;
            if (raw.TryGetValue("InstalledApiVersions", out installedApiVersions))
                settings.InstalledApiVersions = ParseInstalledApiVersions(installedApiVersions);

            if (!string.IsNullOrEmpty(settings.InstalledModApiVersion))
                settings.InstalledApiVersions["ModAPI"] = settings.InstalledModApiVersion;
            if (!string.IsNullOrEmpty(settings.InstalledShelteredApiVersion))
                settings.InstalledApiVersions["ShelteredAPI"] = settings.InstalledShelteredApiVersion;

            string enableNexus;
            if (raw.TryGetValue("EnableNexusIntegration", out enableNexus))
            {
                bool enabled;
                if (bool.TryParse(enableNexus, out enabled))
                    settings.EnableNexusIntegration = enabled;
            }

            string nexusDomain;
            if (raw.TryGetValue("NexusGameDomain", out nexusDomain))
                settings.NexusGameDomain = nexusDomain;
            else
                settings.NexusGameDomain = profile.DefaultNexusGameDomain ?? string.Empty;

            string protectedNexusApiKey;
            bool hasProtectedApiKey = raw.TryGetValue(ProtectedNexusApiKeyKey, out protectedNexusApiKey);
            if (hasProtectedApiKey && !string.IsNullOrEmpty(protectedNexusApiKey))
            {
                string decrypted;
                if (NexusApiKeyProtector.TryUnprotect(protectedNexusApiKey, out decrypted))
                {
                    settings.NexusApiKey = decrypted;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Nexus API key decrypt failed. Falling back to legacy plaintext key if available.");
                }
            }

            // Legacy plaintext fallback for migration from older settings files.
            if (string.IsNullOrEmpty(settings.NexusApiKey))
            {
                string legacyNexusApiKey;
                if (raw.TryGetValue(LegacyNexusApiKeyKey, out legacyNexusApiKey))
                    settings.NexusApiKey = legacyNexusApiKey;
            }

            string managerNexusModId;
            if (raw.TryGetValue("ManagerNexusModId", out managerNexusModId))
            {
                int parsedManagerModId;
                if (int.TryParse(managerNexusModId, out parsedManagerModId))
                    settings.ManagerNexusModId = parsedManagerModId;
            }

            if (settings.ManagerNexusModId <= 0 &&
                profile.DefaultManagerNexusModId > 0 &&
                string.Equals(settings.NexusGameDomain ?? string.Empty, profile.DefaultNexusGameDomain ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                settings.ManagerNexusModId = profile.DefaultManagerNexusModId;
            }

            string autoLoadSlot;
            if (raw.TryGetValue("AutoLoadSaveSlot", out autoLoadSlot))
            {
                if (int.TryParse(autoLoadSlot, out int slot))
                    settings.AutoLoadSaveSlot = slot;
            }

            string windowX;
            if (raw.TryGetValue("WindowX", out windowX))
            {
                if (int.TryParse(windowX, out int x))
                    settings.WindowX = x;
            }

            string windowY;
            if (raw.TryGetValue("WindowY", out windowY))
            {
                if (int.TryParse(windowY, out int y))
                    settings.WindowY = y;
            }

            string windowWidth;
            if (raw.TryGetValue("WindowWidth", out windowWidth))
            {
                if (int.TryParse(windowWidth, out int width))
                    settings.WindowWidth = width;
            }

            string windowHeight;
            if (raw.TryGetValue("WindowHeight", out windowHeight))
            {
                if (int.TryParse(windowHeight, out int height))
                    settings.WindowHeight = height;
            }

            string windowMaximized;
            if (raw.TryGetValue("WindowMaximized", out windowMaximized))
            {
                if (bool.TryParse(windowMaximized, out bool maximized))
                    settings.WindowMaximized = maximized;
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
            data["SelectedGameId"] = settings.SelectedGameId ?? string.Empty;
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
            data["InstalledShelteredApiVersion"] = settings.InstalledShelteredApiVersion ?? string.Empty;
            data["InstalledApiVersions"] = SerializeInstalledApiVersions(settings.InstalledApiVersions);
            data["EnableNexusIntegration"] = settings.EnableNexusIntegration.ToString();
            GameProfile profile = _profileRegistry.Resolve(settings.SelectedGameId);
            data["NexusGameDomain"] = settings.NexusGameDomain ?? (profile.DefaultNexusGameDomain ?? string.Empty);
            string plaintextNexusApiKey = settings.NexusApiKey ?? string.Empty;
            string protectedNexusApiKeyValue = NexusApiKeyProtector.Protect(plaintextNexusApiKey);
            if (!string.IsNullOrEmpty(protectedNexusApiKeyValue))
            {
                data[ProtectedNexusApiKeyKey] = protectedNexusApiKeyValue;
            }
            else
            {
                if (string.IsNullOrEmpty(plaintextNexusApiKey))
                {
                    data[ProtectedNexusApiKeyKey] = string.Empty;
                }
                else
                {
                    string existingProtected;
                    if (!data.TryGetValue(ProtectedNexusApiKeyKey, out existingProtected))
                        existingProtected = string.Empty;

                    data[ProtectedNexusApiKeyKey] = existingProtected;
                    System.Diagnostics.Debug.WriteLine("Failed to protect Nexus API key. Keeping previously stored protected value.");
                }
            }
            // Never persist plaintext Nexus API key.
            data.Remove(LegacyNexusApiKeyKey);
            data["ManagerNexusModId"] = settings.ManagerNexusModId.ToString();
            data["AutoLoadSaveSlot"] = settings.AutoLoadSaveSlot.ToString();
            data["WindowX"] = settings.WindowX.ToString();
            data["WindowY"] = settings.WindowY.ToString();
            data["WindowWidth"] = settings.WindowWidth.ToString();
            data["WindowHeight"] = settings.WindowHeight.ToString();
            data["WindowMaximized"] = settings.WindowMaximized.ToString();

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
                lines.Add("# Mod Manager Configuration");
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

        private string TryAutoDetectGamePath(GameProfile profile)
        {
            try
            {
                return _pathDetector.TryDetect(profile);
            }
            catch { }

            return string.Empty;
        }

        private static Dictionary<string, string> ParseInstalledApiVersions(string value)
        {
            Dictionary<string, string> versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(value))
                return versions;

            string[] parts = value.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] pair = parts[i].Split(new char[] { '=' }, 2);
                if (pair.Length != 2)
                    continue;

                string name = pair[0].Trim();
                string version = pair[1].Trim();
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(version))
                    versions[name] = version;
            }

            return versions;
        }

        private static string SerializeInstalledApiVersions(Dictionary<string, string> versions)
        {
            if (versions == null || versions.Count == 0)
                return string.Empty;

            List<string> keys = new List<string>(versions.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);

            List<string> parts = new List<string>();
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                string value;
                if (!versions.TryGetValue(key, out value) || string.IsNullOrEmpty(value))
                    continue;

                parts.Add(key + "=" + value);
            }

            return string.Join(";", parts.ToArray());
        }
    }
}
