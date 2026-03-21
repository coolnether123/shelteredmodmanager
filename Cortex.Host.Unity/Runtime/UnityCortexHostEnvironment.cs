using System.IO;
using Cortex.Presentation.Abstractions;
using UnityEngine;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityCortexHostEnvironment : ICortexHostEnvironment
    {
        private readonly string _gameRootPath;
        private readonly string _hostRootPath;
        private readonly string _hostBinPath;
        private readonly string _managedAssemblyRootPath;
        private readonly string _modsRootPath;
        private readonly string _settingsFilePath;
        private readonly string _workbenchPersistenceFilePath;
        private readonly string _logFilePath;
        private readonly string _projectCatalogPath;
        private readonly string _decompilerCachePath;

        public UnityCortexHostEnvironment()
        {
            _gameRootPath = Directory.GetParent(Application.dataPath).FullName;
            _hostRootPath = Path.Combine(_gameRootPath, "SMM");
            _hostBinPath = Path.Combine(_hostRootPath, "bin");
            _managedAssemblyRootPath = Path.Combine(Path.Combine(_gameRootPath, "Sheltered_Data"), "Managed");
            _modsRootPath = Path.Combine(_hostRootPath, "mods");
            _settingsFilePath = Path.Combine(_hostBinPath, "cortex_settings.json");
            _workbenchPersistenceFilePath = Path.Combine(_hostBinPath, "cortex_workbench.json");
            _logFilePath = Path.Combine(_hostRootPath, "mod_manager.log");
            _projectCatalogPath = Path.Combine(_hostBinPath, "cortex_projects.json");
            _decompilerCachePath = Path.Combine(_hostBinPath, "cortex_cache");
        }

        public string GameRootPath
        {
            get { return _gameRootPath; }
        }

        public string HostRootPath
        {
            get { return _hostRootPath; }
        }

        public string HostBinPath
        {
            get { return _hostBinPath; }
        }

        public string ManagedAssemblyRootPath
        {
            get { return _managedAssemblyRootPath; }
        }

        public string ModsRootPath
        {
            get { return _modsRootPath; }
        }

        public string SettingsFilePath
        {
            get { return _settingsFilePath; }
        }

        public string WorkbenchPersistenceFilePath
        {
            get { return _workbenchPersistenceFilePath; }
        }

        public string LogFilePath
        {
            get { return _logFilePath; }
        }

        public string ProjectCatalogPath
        {
            get { return _projectCatalogPath; }
        }

        public string DecompilerCachePath
        {
            get { return _decompilerCachePath; }
        }
    }
}
