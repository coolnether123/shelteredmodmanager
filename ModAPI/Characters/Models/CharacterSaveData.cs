using System;
using System.Collections.Generic;

namespace ModAPI.Characters
{
    [Serializable]
    public class CharacterSaveData
    {
        public int UniqueId { get; set; }
        public string PersistenceKey { get; set; }
        public CharacterSource Source { get; set; }
        public string SourceMod { get; set; }
        public bool IsPersistent { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsMale { get; set; }
        public string MeshId { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }

        public List<EffectSaveData> Effects { get; set; }
        public List<AttributeSaveData> Attributes { get; set; }
        public Dictionary<string, string> CustomData { get; set; }
    }

    [Serializable]
    public class EffectSaveData
    {
        public string EffectId { get; set; }
        public float Duration { get; set; }
        public float TimeRemaining { get; set; }
        public float TimeApplied { get; set; }
        public int StackCount { get; set; }
        public string SourceModId { get; set; }
        public string SerializedData { get; set; }
        public Dictionary<string, string> CustomDataBlob { get; set; }
    }

    [Serializable]
    public class AttributeSaveData
    {
        public string AttributeName { get; set; }
        public float Value { get; set; }
        public float Duration { get; set; }
        public float TimeRemaining { get; set; }
        public string SourceModId { get; set; }
    }
}
