using System;

namespace Manager.Core.Models
{
    [Serializable]
    public sealed class ManagerBooleanOptionsFile
    {
        public int version = 1;
        public ManagerBooleanOptionRecord[] booleans = new ManagerBooleanOptionRecord[0];
    }

    [Serializable]
    public sealed class ManagerBooleanOptionRecord
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
