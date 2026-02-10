using System;

namespace ModAPI.Core
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
    public class ModPrivacyAttribute : Attribute 
    {
        public PrivacyLevel Level { get; set; }
        public string Reason { get; set; }
        
        public ModPrivacyAttribute(PrivacyLevel level, string reason = "")
        {
            Level = level;
            Reason = reason;
        }
    }
    
    public enum PrivacyLevel 
    {
        Public,      // Full decompilation visible
        Obfuscated,  // Show method signature only, body replaced with "// Obfuscated by author"
        Private      // Completely hidden from other mods' debuggers
    }
}
