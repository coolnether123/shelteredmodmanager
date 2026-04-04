using Cortex.Presentation.Abstractions;
using UnityEngine;

namespace Cortex.Host.Sheltered.Runtime
{
    public sealed class ShelteredCortexHostEnvironment : ICortexHostEnvironment
    {
        private readonly ShelteredHostPathLayout _layout;

        public ShelteredCortexHostEnvironment()
        {
            _layout = ShelteredHostPathLayout.FromUnityApplicationDataPath(Application.dataPath);
        }

        public string ApplicationRootPath
        {
            get { return _layout.ApplicationRootPath; }
        }

        public string HostRootPath
        {
            get { return _layout.HostRootPath; }
        }

        public string HostBinPath
        {
            get { return _layout.HostBinPath; }
        }

        public string BundledPluginSearchRoots
        {
            get { return _layout.BundledPluginSearchRoots; }
        }

        public string BundledToolRootPath
        {
            get { return _layout.BundledToolRootPath; }
        }

        public string ConfiguredPluginSearchRoots
        {
            get { return _layout.ConfiguredPluginSearchRoots; }
        }

        public string ReferenceAssemblyRootPath
        {
            get { return _layout.ReferenceAssemblyRootPath; }
        }

        public string RuntimeContentRootPath
        {
            get { return _layout.RuntimeContentRootPath; }
        }

        public string SettingsFilePath
        {
            get { return _layout.SettingsFilePath; }
        }

        public string WorkbenchPersistenceFilePath
        {
            get { return _layout.WorkbenchPersistenceFilePath; }
        }

        public string LogFilePath
        {
            get { return _layout.LogFilePath; }
        }

        public string ProjectCatalogPath
        {
            get { return _layout.ProjectCatalogPath; }
        }

        public string DecompilerCachePath
        {
            get { return _layout.DecompilerCachePath; }
        }
    }
}
