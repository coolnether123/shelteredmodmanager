using System;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Data loaded from a mod's <c>About/About.json</c> manifest.
    /// Fields remain public because Unity's JsonUtility serializes fields rather than properties.
    /// </summary>
    [Serializable]
    public class ModAbout
    {
        // Required
        public string id;            // Unique mod ID, such as com.yourname.mymod.
        public string name;          // Display name.
        public string version;       // Version string.
        public string[] authors;     // Author names.
        public string description;   // Description shown to users.

        // Optional
        public string entryType;     // Fully qualified entry type name.
        public string[] dependsOn;   // Dependency constraints.
        public string[] loadBefore;  // Mods that load after this mod.
        public string[] loadAfter;   // Mods that load before this mod.
        public string[] tags;        // Search and category tags.
        public string website;       // Project website.
        public string nexusGameDomain; // Nexus game domain, such as sheltered.
        public int nexusModId;       // Legacy Nexus mod ID.
        public string requiredModApiVersion; // Minimum ModAPI version.
        public string modApiVersion; // Legacy ModAPI version declaration.
        public string requiredShelteredApiVersion; // Minimum host-specific API version.
        public string shelteredApiVersion; // Legacy host-specific API version declaration.
        public string missingModWarning; // Warning shown when a save references a missing mod.
        public bool debugLogging;    // Enables Log.Debug() for this mod.
    }
}
