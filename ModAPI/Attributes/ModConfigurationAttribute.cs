using System;

namespace ModAPI.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class ModConfigurationAttribute : Attribute
    {
        public ModConfigurationAttribute()
        {
        }
    }
}
