using System;
using UnityEngine;

/**
 * Author: coolnether123
 * This file defines the ModAbout class and related types.
 * It is deserialized from About/About.json using UnityEngine.JsonUtility.
 * Fields are public to comply with JsonUtility requirements.
 * Coolnether123
 */
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
}

