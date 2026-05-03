using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Scenarios
{
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

    public interface IScenarioModFolderSource
    {
        ScenarioModFolder[] GetLoadedModFolders();
    }

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
