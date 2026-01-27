using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModAPI.Util
{
    /// <summary>
    /// A serializable wrapper for Dictionaries to work around legacy XML/JSON limitations in Sheltered.
    /// Used by mods to track persistent state (e.g. character IDs mapped to mod data).
    /// </summary>
    [Serializable]
    public class SaveLoadDictionary<TKey, TValue>
    {
        [Serializable]
        public struct Pair
        {
            public TKey Key;
            public TValue Value;
        }

        [SerializeField]
        public List<Pair> Entries = new List<Pair>();

        private Dictionary<TKey, TValue> _runtimeCache;

        /// <summary>
        /// Access the dictionary at runtime. Changes are reflected in the serializable list.
        /// </summary>
        public Dictionary<TKey, TValue> Data
        {
            get
            {
                if (_runtimeCache == null) SyncFromList();
                return _runtimeCache;
            }
        }

        public void Clear()
        {
            Data.Clear();
            SyncToList();
        }

        /// <summary>
        /// Synchronizes the serializable Entries list from the runtime dictionary.
        /// Call this before saving.
        /// </summary>
        public void SyncToList()
        {
            if (_runtimeCache == null) return;
            Entries.Clear();
            foreach (var kv in _runtimeCache)
            {
                Entries.Add(new Pair { Key = kv.Key, Value = kv.Value });
            }
        }

        /// <summary>
        /// Synchronizes the runtime dictionary from the serializable Entries list.
        /// Call this after loading.
        /// </summary>
        public void SyncFromList()
        {
            _runtimeCache = new Dictionary<TKey, TValue>();
            if (Entries == null) return;
            foreach (var entry in Entries)
            {
                if (entry.Key == null) continue;
                _runtimeCache[entry.Key] = entry.Value;
            }
        }
    }
}
