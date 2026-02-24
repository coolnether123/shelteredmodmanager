using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ModAPI.Util
{
    /// <summary>
    /// A serializable wrapper for Dictionaries to work around legacy XML/JSON limitations in Sheltered.
    /// Used by mods to track persistent state (e.g. character IDs mapped to mod data).
    /// </summary>
    [Serializable]
    public class SaveLoadDictionary<TKey, TValue> : ISerializationCallbackReceiver, IDictionary<TKey, TValue>
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

        public TValue this[TKey key]
        {
            get { return Data[key]; }
            set { Data[key] = value; }
        }

        public ICollection<TKey> Keys { get { return Data.Keys; } }
        public ICollection<TValue> Values { get { return Data.Values; } }
        public int Count { get { return Data.Count; } }
        public bool IsReadOnly { get { return false; } }

        public void Add(TKey key, TValue value) { Data.Add(key, value); }
        public bool Remove(TKey key) { return Data.Remove(key); }
        public bool ContainsKey(TKey key) { return Data.ContainsKey(key); }
        public bool TryGetValue(TKey key, out TValue value) { return Data.TryGetValue(key, out value); }

        public void Add(KeyValuePair<TKey, TValue> item) { Add(item.Key, item.Value); }
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            TValue value;
            if (!Data.TryGetValue(item.Key, out value)) return false;
            return EqualityComparer<TValue>.Default.Equals(value, item.Value);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (!Contains(item)) return false;
            return Data.Remove(item.Key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException("array");
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException("arrayIndex");
            if (array.Length - arrayIndex < Data.Count) throw new ArgumentException("Destination array is too small.");

            foreach (var kv in Data)
            {
                array[arrayIndex++] = kv;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() { return Data.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return Data.GetEnumerator(); }

        public void Clear()
        {
            Data.Clear();
            Entries.Clear();
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
                if (IsNullKey(entry.Key)) continue;
                _runtimeCache[entry.Key] = entry.Value;
            }
        }

        public void OnBeforeSerialize()
        {
            SyncToList();
        }

        public void OnAfterDeserialize()
        {
            SyncFromList();
        }

        private static bool IsNullKey(TKey key)
        {
            return object.ReferenceEquals(key, null);
        }
    }
}
