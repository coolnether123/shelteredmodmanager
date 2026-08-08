using System;

namespace ModAPI.Core
{
    /// <summary>Assembly-local policy input shared by the desktop and runtime adapters.</summary>
    internal sealed class ManagerBooleanOptionDescriptor
    {
        public string Id;
        public string Owner;
        public string Label;
        public string Description;
        public bool DefaultValue = true;
        public bool RequiresRestart = true;
        public int SortOrder;
    }

    [Serializable]
    internal sealed class ManagerBooleanOptionsFile
    {
        public int version = 1;
        public ManagerBooleanOptionRecord[] booleans = new ManagerBooleanOptionRecord[0];
    }

    [Serializable]
    internal sealed class ManagerBooleanOptionRecord
    {
        public string id;
        public string owner;
        public string label;
        public string description;
        public bool value;
        public bool defaultValue;
        public bool requiresRestart;
        public int sortOrder;
    }
}
