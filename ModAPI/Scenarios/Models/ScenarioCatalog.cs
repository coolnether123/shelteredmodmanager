using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Scenarios
{
    /// <summary>
    /// Read-only catalog entry for an XML scenario definition discovered from a mod folder.
    /// </summary>
    public sealed class ScenarioInfo
    {
        public ScenarioInfo(string id, string displayName, string author, string version, string filePath, string ownerModId)
        {
            Id = id;
            DisplayName = displayName;
            Author = author;
            Version = version;
            FilePath = filePath;
            OwnerModId = ownerModId;
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string Author { get; private set; }
        public string Version { get; private set; }
        public string FilePath { get; private set; }
        public string OwnerModId { get; private set; }
    }

    /// <summary>
    /// Mod folder that can contain scenario definition files.
    /// </summary>
    public sealed class ScenarioModFolder
    {
        public ScenarioModFolder(string modId, string rootPath)
        {
            ModId = modId;
            RootPath = rootPath;
        }

        public string ModId { get; private set; }
        public string RootPath { get; private set; }
    }

    /// <summary>
    /// Supplies mod roots for scenario catalog discovery.
    /// Implement this to scan a custom mod source outside the default ModRegistry.
    /// </summary>
    public interface IScenarioModFolderSource
    {
        ScenarioModFolder[] GetLoadedModFolders();
    }

    /// <summary>
    /// Scenario folder source backed by loaded ModAPI registry entries.
    /// </summary>
    public sealed class ModRegistryScenarioModFolderSource : IScenarioModFolderSource
    {
        public ScenarioModFolder[] GetLoadedModFolders()
        {
            List<ScenarioModFolder> results = new List<ScenarioModFolder>();
            List<ModEntry> mods = ModRegistry.GetLoadedMods();
            for (int i = 0; i < mods.Count; i++)
            {
                ModEntry mod = mods[i];
                if (mod != null && !string.IsNullOrEmpty(mod.RootPath))
                    results.Add(new ScenarioModFolder(mod.Id, mod.RootPath));
            }

            return results.ToArray();
        }
    }

}
