using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Persistence
{
    /// <summary>
    /// A Dictionary that automatically saves and loads its contents.
    /// Keys must be strings. Values support: int, float, bool, string, Vector2, Vector3, Color.
    /// </summary>
    public class ModDictionary<TValue> : Dictionary<string, TValue>, ISaveable
    {
        private string _id;

        /// <summary>
        /// Creates a new auto-persisted dictionary.
        /// </summary>
        /// <param name="uniqueId">A unique ID for this dictionary, ideally prefixed with your mod ID (e.g. "MyMod_Settings")</param>
        public ModDictionary(string uniqueId)
        {
            this._id = uniqueId;
            ModPersistence.Register(this);
        }

        public bool IsReadyForLoad() => true;
        public bool IsRelocationEnabled() => true;

        public bool SaveLoad(SaveData data)
        {
            data.GroupStart("ModDict_" + _id);
            
            if (data.isSaving)
            {
                int count = this.Count;
                data.SaveLoad("count", ref count);
                int i = 0;
                foreach (var kvp in this)
                {
                    data.GroupStart("entry_" + i);
                    string key = kvp.Key;
                    data.SaveLoad("key", ref key);
                    TValue val = kvp.Value;
                    SaveLoadValue(data, "val", ref val);
                    data.GroupEnd();
                    i++;
                }
            }
            else
            {
                int count = 0;
                data.SaveLoad("count", ref count);
                this.Clear();
                for (int i = 0; i < count; i++)
                {
                    data.GroupStart("entry_" + i);
                    string key = string.Empty;
                    data.SaveLoad("key", ref key);
                    TValue val = default;
                    if (SaveLoadValue(data, "val", ref val))
                    {
                        this[key] = val;
                    }
                    data.GroupEnd();
                }
            }

            data.GroupEnd();
            return true;
        }

        private bool SaveLoadValue(SaveData data, string name, ref TValue value)
        {
            if (typeof(TValue) == typeof(int)) { int v = (int)(object)value; bool r = data.SaveLoad(name, ref v); value = (TValue)(object)v; return r; }
            if (typeof(TValue) == typeof(float)) { float v = (float)(object)value; bool r = data.SaveLoad(name, ref v); value = (TValue)(object)v; return r; }
            if (typeof(TValue) == typeof(bool)) { bool v = (bool)(object)value; bool r = data.SaveLoad(name, ref v); value = (TValue)(object)v; return r; }
            if (typeof(TValue) == typeof(string)) { string v = (string)(object)value; bool r = data.SaveLoad(name, ref v); value = (TValue)(object)v; return r; }
            if (typeof(TValue) == typeof(UnityEngine.Vector2)) { UnityEngine.Vector2 v = (UnityEngine.Vector2)(object)value; bool r = data.SaveLoad(name, ref v); value = (TValue)(object)v; return r; }
            if (typeof(TValue) == typeof(UnityEngine.Vector3)) { UnityEngine.Vector3 v = (UnityEngine.Vector3)(object)value; bool r = data.SaveLoad(name, ref v); value = (TValue)(object)v; return r; }
            if (typeof(TValue) == typeof(UnityEngine.Color)) { UnityEngine.Color v = (UnityEngine.Color)(object)value; bool r = data.SaveLoad(name, ref v); value = (TValue)(object)v; return r; }
            
            return false;
        }
    }
}
