using System;

namespace ModAPI.Core
{
    /// <summary>
    /// Attribute to define a toggle (checkbox) in the mod settings UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ModToggleAttribute : Attribute
    {
        public string Label { get; private set; }
        public string Description { get; private set; }

        public ModToggleAttribute(string label, string description = "")
        {
            Label = label;
            Description = description;
        }
    }

    /// <summary>
    /// Attribute to define a slider in the mod settings UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ModSliderAttribute : Attribute
    {
        public string Label { get; private set; }
        public float MinView { get; private set; }
        public float MaxView { get; private set; }

        public ModSliderAttribute(string label, float min, float max)
        {
            Label = label;
            MinView = min;
            MaxView = max;
        }
    }
}
