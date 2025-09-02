using System;
using UnityEngine;

/**
 * Author: coolnether123
 * This file defines the ModManifest class and related types.
 * It is deserialized from About/About.json using UnityEngine.JsonUtility.
 * Fields are public to comply with JsonUtility requirements.
 * Coolnether123
 */
[Serializable]
public class ModManifest
{
    // Required
    public string id;            // unique mod id (e.g., com.yourname.mymod)  // Coolnether123
    public string name;          // display name                              // Coolnether123
    public string version;       // semantic version                           // Coolnether123
    public string[] authors;     // authors                                    // Coolnether123
    public string description;   // human-readable description                 // Coolnether123

    // Optional
    public string entryType;     // optional fully-qualified type name         // Coolnether123
    public string[] dependsOn;   // optional dependency constraints            // Coolnether123
    public string[] loadBefore;  // optional soft ordering                     // Coolnether123
    public string[] loadAfter;   // optional soft ordering                     // Coolnether123
    public string[] tags;        // optional tags                              // Coolnether123
    public string website;       // optional website                           // Coolnether123
}

