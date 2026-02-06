using System;

namespace ModAPI.Attributes
{
    /// <summary>
    /// Mark a class as a container for mod settings.
    /// Used by the Spine framework to identify and auto-load configuration classes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ModConfigurationAttribute : Attribute
    {
        public string Title { get; set; }

        public ModConfigurationAttribute(string title = null)
        {
            Title = title;
        }
    }
}
