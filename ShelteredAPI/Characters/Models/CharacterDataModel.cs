using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModAPI.Characters
{
    public class CharacterDataModel : ICharacterData
    {
        private readonly object _customDataLock = new object();
        private readonly Dictionary<string, object> _customData = new Dictionary<string, object>(StringComparer.Ordinal);

        public int UniqueId { get; internal set; }
        public string PersistenceKey { get; internal set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsMale { get; set; }
        public string MeshId { get; set; }
        public Color SkinColor { get; set; }
        public Color HairColor { get; set; }

        public int StrengthLevel { get; set; }
        public int DexterityLevel { get; set; }
        public int IntelligenceLevel { get; set; }
        public int CharismaLevel { get; set; }
        public int PerceptionLevel { get; set; }

        public int Health { get; set; }
        public int MaxHealth { get; set; }

        public CharacterSource Source { get; internal set; }
        public string SourceMod { get; internal set; }
        public bool IsPersistent { get; internal set; }
        public float CreatedAtTime { get; internal set; }

        public void SetCustomData(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (_customDataLock)
            {
                _customData[key] = value;
            }
        }

        public T GetCustomData<T>(string key)
        {
            if (string.IsNullOrEmpty(key)) return default(T);
            lock (_customDataLock)
            {
                object value;
                if (!_customData.TryGetValue(key, out value)) return default(T);
                if (value is T) return (T)value;
                return default(T);
            }
        }

        public bool HasCustomData(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (_customDataLock)
            {
                return _customData.ContainsKey(key);
            }
        }

        internal Dictionary<string, object> SnapshotCustomData()
        {
            lock (_customDataLock)
            {
                return new Dictionary<string, object>(_customData, StringComparer.Ordinal);
            }
        }

        internal void RestoreCustomData(Dictionary<string, object> values)
        {
            if (values == null) return;
            lock (_customDataLock)
            {
                _customData.Clear();
                foreach (var kv in values)
                {
                    _customData[kv.Key] = kv.Value;
                }
            }
        }
    }
}
