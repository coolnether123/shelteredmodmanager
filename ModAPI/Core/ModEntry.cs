using System;
using ModAPI.Spine;

namespace ModAPI.Core
{
    /**
     * Author: coolnether123
     * Descriptor for a single mod discovered on disk.
     * Split into its own file to keep ModDiscovery focused on orchestration.
     */
    public class ModEntry
    {
        public string Id;            // normalized id
        public string Name;          // display name
        public string Version;       // optional version string
        public string RootPath;      // mod root folder
        public string AboutPath;     // path to About/About.json
        public string AssembliesPath;// path to Assemblies folder
        public ModAbout About;       // parsed about
        
        /// <summary>
        /// Reference to the mod's settings provider if it supports the Spine configuration framework.
        /// </summary>
        public ISettingsProvider SettingsProvider;
    }
}
