using System;

namespace ModAPI.Core
{
    /// <summary>
    /// Declares how much source or runtime inspection detail a mod exposes to debugging tools.
    /// This is advisory metadata for ModAPI tools; it is not a security boundary.
    /// </summary>
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
    
    /// <summary>
    /// Visibility levels understood by ModAPI source and debugger inspection tools.
    /// </summary>
    public enum PrivacyLevel 
    {
        Public,      // Full decompilation visible
        Obfuscated,  // Show method signature only, body replaced with "// Obfuscated by author"
        Private      // Completely hidden from other mods' debuggers
    }
}
