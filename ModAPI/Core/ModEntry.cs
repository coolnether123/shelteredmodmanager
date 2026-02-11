using System;
using ModAPI.Spine;

namespace ModAPI.Core
{
    /// <summary>
    /// Descriptor for a single discovered mod folder and its parsed metadata.
    /// </summary>
    public class ModEntry
    {
        /// <summary>Normalized mod id used for lookups and load-order matching.</summary>
        public string Id;
        /// <summary>Human-facing display name.</summary>
        public string Name;
        /// <summary>Optional semantic version string from About.json.</summary>
        public string Version;
        /// <summary>Absolute path to the mod root folder.</summary>
        public string RootPath;
        /// <summary>Absolute path to About/About.json.</summary>
        public string AboutPath;
        /// <summary>Absolute path to the Assemblies folder.</summary>
        public string AssembliesPath;
        /// <summary>Parsed About.json payload.</summary>
        public ModAbout About;
        
        /// <summary>
        /// Reference to the mod's settings provider if it supports the Spine configuration framework.
        /// </summary>
        public ISettingsProvider SettingsProvider;
    }
}
