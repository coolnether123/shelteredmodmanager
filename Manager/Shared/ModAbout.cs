using System;

namespace Manager.Shared
{
    [Serializable]
    public class ModAbout
    {
        public string id;            // unique mod id (e.g., com.yourname.mymod)  
        public string name;          // display name                              
        public string version;       // semantic version                          
        public string[] authors;     // authors                                   
        public string description;   // human-readable description                
        public string entryType;     // optional fully-qualified type name        
        public string[] dependsOn;   // optional dependency constraints           
        public string[] loadBefore;  // optional soft ordering                    
        public string[] loadAfter;   // optional soft ordering                    
        public string[] tags;        // optional tags                             
        public string website;       // optional website                          
    }
}
