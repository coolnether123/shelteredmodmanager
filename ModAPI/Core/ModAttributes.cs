using System;

namespace ModAPI.Attributes
{
    /// <summary>
    /// Marks a class as a Spine-managed mod settings container.
    /// Add this to a POCO settings type so the settings loader can discover it and show a friendly title.
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
