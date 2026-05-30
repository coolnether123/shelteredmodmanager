using System;
using System.Collections.Generic;

namespace Manager.Core.Models
{
    /// <summary>
    /// Application settings model - single source of truth for all configuration
    /// </summary>
    public class AppSettings
    {
        public const int SaveBackupRetentionAlways = -1;
        public const int SaveBackupRetentionDisabled = 0;
        public const int DefaultSaveBackupRetention = 3;
        public const string DebugLogScopeMod = "Mod";
        public const string DebugLogScopeAll = "All";

        // Paths
        private string _gamePath = string.Empty;
        private string _modsPath = string.Empty;
        private bool _darkMode = true;
        private string _lastSelectedModId = string.Empty;
        private bool _devMode = false;
        private string _logLevel = "Info";
        private string _debugLogScope = DebugLogScopeMod;
        private HashSet<string> _logCategories;
        private bool _ignoreOrderChecks = false;
        private bool _skipHarmonyDependencyCheck = false;
        private bool _includeNexusPrereleaseFiles = false;
        private string _gameBitness;
        private string _installedModApiVersion;
        private string _installedShelteredApiVersion;
        private string _autoCondenseSaves = "ask"; // yes, no, or ask
        private int _saveBackupRetention = DefaultSaveBackupRetention; // 0 disables, positive keeps N, -1 keeps all
        private bool _enableNexusIntegration = true;
        private bool _enableExperimentalPublishTab = false;
        private string _lastSeenReleaseNoticeVersion = string.Empty;
        private string _nexusGameDomain = "sheltered";
        private string _nexusApiKey = string.Empty;
        private int _managerNexusModId = 1;
        private int _windowX = int.MinValue;
        private int _windowY = int.MinValue;
        private int _windowWidth = 0;
        private int _windowHeight = 0;
        private bool _windowMaximized = false;

        public AppSettings()
        {
            _logCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _logCategories.Add("General");
            _logCategories.Add("Loader");
            _logCategories.Add("Plugin");
            _logCategories.Add("Assembly");
        }

        public string GamePath 
        { 
            get { return _gamePath; } 
            set { _gamePath = value; } 
        }
        
        public string ModsPath 
        { 
            get { return _modsPath; } 
            set { _modsPath = value; } 
        }
        
        public bool DarkMode 
        { 
            get { return _darkMode; } 
            set { _darkMode = value; } 
        }
        
        public string LastSelectedModId 
        { 
            get { return _lastSelectedModId; } 
            set { _lastSelectedModId = value; } 
        }
        
        public bool DevMode 
        { 
            get { return _devMode; } 
            set { _devMode = value; } 
        }
        
        public string LogLevel 
        { 
            get { return _logLevel; } 
            set { _logLevel = value; } 
        }

        public string DebugLogScope
        {
            get { return NormalizeDebugLogScope(_debugLogScope); }
            set { _debugLogScope = NormalizeDebugLogScope(value); }
        }
        
        public HashSet<string> LogCategories 
        { 
            get { return _logCategories; } 
            set { _logCategories = value; } 
        }
        
        public bool IgnoreOrderChecks 
        { 
            get { return _ignoreOrderChecks; } 
            set { _ignoreOrderChecks = value; } 
        }
        
        public bool SkipHarmonyDependencyCheck 
        { 
            get { return _skipHarmonyDependencyCheck; } 
            set { _skipHarmonyDependencyCheck = value; } 
        }

        public bool IncludeNexusPrereleaseFiles
        {
            get { return _includeNexusPrereleaseFiles; }
            set { _includeNexusPrereleaseFiles = value; }
        }
        
        public string GameBitness 
        { 
            get { return _gameBitness; } 
            set { _gameBitness = value; } 
        }
        
        public string InstalledModApiVersion 
        { 
            get { return _installedModApiVersion; } 
            set { _installedModApiVersion = value; } 
        }

        public string InstalledShelteredApiVersion
        {
            get { return _installedShelteredApiVersion; }
            set { _installedShelteredApiVersion = value; }
        }
        
        public string AutoCondenseSaves 
        { 
            get { return _autoCondenseSaves; } 
            set { _autoCondenseSaves = value; } 
        }

        public int SaveBackupRetention
        {
            get { return _saveBackupRetention; }
            set { _saveBackupRetention = value; }
        }

        public bool EnableNexusIntegration
        {
            get { return _enableNexusIntegration; }
            set { _enableNexusIntegration = value; }
        }

        public bool EnableExperimentalPublishTab
        {
            get { return _enableExperimentalPublishTab; }
            set { _enableExperimentalPublishTab = value; }
        }

        public string LastSeenReleaseNoticeVersion
        {
            get { return _lastSeenReleaseNoticeVersion; }
            set { _lastSeenReleaseNoticeVersion = value; }
        }

        public string NexusGameDomain
        {
            get { return _nexusGameDomain; }
            set { _nexusGameDomain = value; }
        }

        public string NexusApiKey
        {
            get { return _nexusApiKey; }
            set { _nexusApiKey = value; }
        }

        public int ManagerNexusModId
        {
            get { return _managerNexusModId; }
            set { _managerNexusModId = value; }
        }

        private int _autoLoadSaveSlot = 0;
        public int AutoLoadSaveSlot
        {
            get { return _autoLoadSaveSlot; }
            set { _autoLoadSaveSlot = value; }
        }

        public int WindowX
        {
            get { return _windowX; }
            set { _windowX = value; }
        }

        public int WindowY
        {
            get { return _windowY; }
            set { _windowY = value; }
        }

        public int WindowWidth
        {
            get { return _windowWidth; }
            set { _windowWidth = value; }
        }

        public int WindowHeight
        {
            get { return _windowHeight; }
            set { _windowHeight = value; }
        }

        public bool WindowMaximized
        {
            get { return _windowMaximized; }
            set { _windowMaximized = value; }
        }
        
        /// <summary>
        /// All available log categories
        /// </summary>
        public static readonly string[] AllLogCategories = new string[]
        {
            "General", "Loader", "Plugin", "Assembly", "Dependency",
            "Configuration", "Performance", "Memory", "Scene", "UI", "Network", "IO"
        };

        public bool IsGamePathValid 
        { 
            get { return !string.IsNullOrEmpty(GamePath) && System.IO.File.Exists(GamePath); } 
        }
        
        public bool IsModsPathValid 
        { 
            get { return !string.IsNullOrEmpty(ModsPath) && System.IO.Directory.Exists(ModsPath); } 
        }

        public static int ParseSaveBackupRetention(string raw, int fallback)
        {
            string value = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Length == 0)
                return fallback;

            if (value == "none" || value == "disabled" || value == "disable" || value == "off" || value == "false" || value == "0")
                return SaveBackupRetentionDisabled;

            if (value == "always" || value == "forever" || value == "all" || value == "unlimited")
                return SaveBackupRetentionAlways;

            int count;
            if (int.TryParse(value, out count))
                return count <= 0 ? SaveBackupRetentionDisabled : count;

            return fallback;
        }

        public static string FormatSaveBackupRetention(int value)
        {
            if (value < 0)
                return "always";
            if (value == 0)
                return "none";
            return value.ToString();
        }

        public static string NormalizeDebugLogScope(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Equals(DebugLogScopeAll, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Everything", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Framework", StringComparison.OrdinalIgnoreCase))
                return DebugLogScopeAll;

            return DebugLogScopeMod;
        }
    }
}
