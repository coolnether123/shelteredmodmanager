using System;
using System.Collections.Generic;
using System.IO;
using Manager.Core.Games.Saves;

namespace Manager.Core.Games.Models
{
    public sealed class GameProfile
    {
        private string _managerTitle;

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string ManagerTitle
        {
            get
            {
                if (!string.IsNullOrEmpty(_managerTitle))
                    return _managerTitle;
                return (DisplayName ?? "Game") + " Mod Manager";
            }
            set { _managerTitle = value; }
        }

        public string LocateExecutableTitle { get; set; }
        public string ExecutableDialogFilter { get; set; }
        public string DefaultNexusGameDomain { get; set; }
        public int DefaultManagerNexusModId { get; set; }
        public string SteamAppId { get; set; }
        public string SteamCommonFolderName { get; set; }
        public string GogCommonFolderName { get; set; }
        public string[] ExecutableNames { get; set; }
        public string[] CommonInstallDirectories { get; set; }
        public GameApiAssembly[] ApiAssemblies { get; set; }
        public RuntimeFileRequirement[] RequiredRuntimeFiles { get; set; }
        public string[] LogFileRelativePaths { get; set; }
        public GameRuntimeLayout RuntimeLayout { get; set; }
        public GameAboutContent AboutContent { get; set; }
        public ISaveDiscoveryStrategy SaveDiscovery { get; set; }
        public bool SupportsSaveDiscovery { get; set; }
        public bool SupportsDoorstopLaunch { get; set; }

        public GameProfile()
        {
            Id = string.Empty;
            DisplayName = "Game";
            LocateExecutableTitle = "Locate game executable";
            ExecutableDialogFilter = "Executable|*.exe|All Files|*.*";
            DefaultNexusGameDomain = string.Empty;
            ExecutableNames = new string[0];
            CommonInstallDirectories = new string[0];
            ApiAssemblies = new GameApiAssembly[0];
            RequiredRuntimeFiles = new RuntimeFileRequirement[0];
            LogFileRelativePaths = new string[0];
            RuntimeLayout = new GameRuntimeLayout();
            AboutContent = new GameAboutContent();
            SaveDiscovery = new NoOpSaveDiscoveryStrategy();
            SupportsDoorstopLaunch = true;
        }

        public string GetModsPath(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath))
                return string.Empty;

            string gameDirectory = File.Exists(gamePath)
                ? Path.GetDirectoryName(gamePath)
                : gamePath;

            if (string.IsNullOrEmpty(gameDirectory))
                return string.Empty;

            return RuntimeLayout.GetModsPath(gameDirectory);
        }

        public string[] GetApiAssemblyNames()
        {
            List<string> names = new List<string>();
            if (ApiAssemblies == null)
                return names.ToArray();

            for (int i = 0; i < ApiAssemblies.Length; i++)
            {
                GameApiAssembly api = ApiAssemblies[i];
                if (api == null || string.IsNullOrEmpty(api.Name))
                    continue;

                if (!Contains(names, api.Name))
                    names.Add(api.Name);
            }

            return names.ToArray();
        }

        public bool UsesApiAssembly(string apiName)
        {
            if (string.IsNullOrEmpty(apiName))
                return false;

            string[] names = GetApiAssemblyNames();
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], apiName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool Contains(IList<string> values, string candidate)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
