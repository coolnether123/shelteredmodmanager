using System;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// DTO for a mod's <c>About/About.json</c> manifest.
    /// Fields remain public because Unity's JsonUtility serializes fields rather than properties.
    /// </summary>
    [Serializable]
    public class ModAbout
    {
        // Required
        public string id;            // unique mod id (e.g., com.yourname.mymod)  
        public string name;          // display name                              
        public string version;       // semantic version                          
        public string[] authors;     // authors                                   
        public string description;   // human-readable description                

        // Optional
        public string entryType;     // optional fully-qualified type name        
        public string[] dependsOn;   // optional dependency constraints           
        public string[] loadBefore;  // optional soft ordering                    
        public string[] loadAfter;   // optional soft ordering                    
        public string[] tags;        // optional tags                             
        public string website;       // optional website                          
        public string nexusGameDomain; // optional Nexus game domain (e.g. sheltered)
        public int nexusModId;       // optional Nexus legacy mod ID
        public string requiredModApiVersion; // optional minimum ModAPI version
        public string modApiVersion; // legacy optional ModAPI version declaration
        public string requiredShelteredApiVersion; // optional minimum host-specific API version
        public string shelteredApiVersion; // legacy optional host-specific API version declaration
        public string missingModWarning; // optional warning if mod is missing from save
        public bool debugLogging;    // optional: enables Log.Debug() for this mod
    }
}
