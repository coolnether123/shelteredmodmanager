using System;

namespace GameModding.Shared.Mods
{
    [Serializable]
    public class ModAboutInfo
    {
        public string id;
        public string name;
        public string version;
        public string description;
        public string entryType;
        public string[] authors;
        public string[] dependsOn;
        public string[] loadBefore;
        public string[] loadAfter;
        public string[] tags;
        public string website;
        public string requiredModApiVersion;
        public string modApiVersion;
        public string requiredShelteredApiVersion;
        public string shelteredApiVersion;
        public string nexusGameDomain;
        public int nexusModId;
    }
}
