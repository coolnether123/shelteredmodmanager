using System;

namespace ModAPI.InputActions
{
    /// <summary>
    /// Metadata and defaults for a rebindable action.
    /// </summary>
    public class ModInputAction
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public string Category { get; private set; }
        public string Description { get; private set; }
        public InputBinding DefaultBinding { get; private set; }

        public ModInputAction(string id, string label, string category, InputBinding defaultBinding, string description = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Input action id cannot be null or empty.", "id");
            if (string.IsNullOrEmpty(label)) throw new ArgumentException("Input action label cannot be null or empty.", "label");

            Id = id;
            Label = label;
            Category = string.IsNullOrEmpty(category) ? "General" : category;
            DefaultBinding = defaultBinding;
            Description = description ?? string.Empty;
        }
    }
}
