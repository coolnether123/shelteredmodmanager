using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Persistence
{
    /// <summary>
    /// A List that automatically saves and loads its contents.
    /// Supports: int, float, bool, string, Vector2, Vector3, Color.
    /// </summary>
    public class ModList<T> : List<T>, ISaveable
    {
        private string _id;

        /// <summary>
        /// Creates a new auto-persisted list.
        /// </summary>
        /// <param name="uniqueId">A unique ID for this list, ideally prefixed with your mod ID (e.g. "MyMod_Players")</param>
        public ModList(string uniqueId)
        {
            this._id = uniqueId;
            ModPersistence.Register(this);
        }

        public bool IsReadyForLoad() => true;
        public bool IsRelocationEnabled() => true;

        public bool SaveLoad(SaveData data)
        {
            data.GroupStart("ModList_" + _id);
            int size = this.Count;
            data.SaveLoad("size", ref size);

            if (data.isLoading)
            {
                this.Clear();
                for (int i = 0; i < size; i++)
                {
                    T val = default;
                    if (SaveLoadValue(data, "i" + i, ref val))
                    {
                        this.Add(val);
                    }
                }
            }
            else
            {
                for (int i = 0; i < size; i++)
                {
                    T val = this[i];
                    SaveLoadValue(data, "i" + i, ref val);
                }
            }

            data.GroupEnd();
            return true;
        }

        private bool SaveLoadValue(SaveData data, string name, ref T value)
        {
            // We have to use reflection or dynamic because SaveData.SaveLoad is overloaded
            // and doesn't use generics.
            if (typeof(T) == typeof(int)) { int v = (int)(object)value; bool r = data.SaveLoad(name, ref v); value = (T)(object)v; return r; }
            if (typeof(T) == typeof(float)) { float v = (float)(object)value; bool r = data.SaveLoad(name, ref v); value = (T)(object)v; return r; }
            if (typeof(T) == typeof(bool)) { bool v = (bool)(object)value; bool r = data.SaveLoad(name, ref v); value = (T)(object)v; return r; }
            if (typeof(T) == typeof(string)) { string v = (string)(object)value; bool r = data.SaveLoad(name, ref v); value = (T)(object)v; return r; }
            if (typeof(T) == typeof(UnityEngine.Vector2)) { UnityEngine.Vector2 v = (UnityEngine.Vector2)(object)value; bool r = data.SaveLoad(name, ref v); value = (T)(object)v; return r; }
            if (typeof(T) == typeof(UnityEngine.Vector3)) { UnityEngine.Vector3 v = (UnityEngine.Vector3)(object)value; bool r = data.SaveLoad(name, ref v); value = (T)(object)v; return r; }
            if (typeof(T) == typeof(UnityEngine.Color)) { UnityEngine.Color v = (UnityEngine.Color)(object)value; bool r = data.SaveLoad(name, ref v); value = (T)(object)v; return r; }
            
            return false;
        }
    }
}
