using System;
using System.Collections.Generic;

namespace Manager.Core.Models
{
    /// <summary>
    /// Application settings model - single source of truth for all configuration
    /// </summary>
    public class AppSettings
    {
        // Paths
        private string _gamePath = string.Empty;
        private string _modsPath = string.Empty;
        private bool _darkMode = true;
        private string _lastSelectedModId = string.Empty;
        private bool _devMode = false;
        private string _logLevel = "Info";
        private HashSet<string> _logCategories;
        private bool _ignoreOrderChecks = false;
        private bool _skipHarmonyDependencyCheck = false;
        private string _gameBitness;
        private string _installedModApiVersion;
        private string _autoCondenseSaves = "ask"; // yes, no, or ask

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
        
        public string AutoCondenseSaves 
        { 
            get { return _autoCondenseSaves; } 
            set { _autoCondenseSaves = value; } 
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
    }
}
