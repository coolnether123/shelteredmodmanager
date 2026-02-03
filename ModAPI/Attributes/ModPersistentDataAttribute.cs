using System;

namespace ModAPI.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class ModPersistentDataAttribute : Attribute
    {
        public string Key { get; private set; }

        public ModPersistentDataAttribute(string key)
        {
            Key = key;
        }
    }
}
