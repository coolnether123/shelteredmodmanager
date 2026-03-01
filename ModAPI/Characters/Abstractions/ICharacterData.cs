using UnityEngine;

namespace ModAPI.Characters
{
    public interface ICharacterData
    {
        int UniqueId { get; }
        string PersistenceKey { get; }

        string FirstName { get; set; }
        string LastName { get; set; }
        bool IsMale { get; set; }
        string MeshId { get; set; }
        Color SkinColor { get; set; }
        Color HairColor { get; set; }

        int StrengthLevel { get; set; }
        int DexterityLevel { get; set; }
        int IntelligenceLevel { get; set; }
        int CharismaLevel { get; set; }
        int PerceptionLevel { get; set; }

        int Health { get; set; }
        int MaxHealth { get; set; }

        CharacterSource Source { get; }
        string SourceMod { get; }
        bool IsPersistent { get; }
        float CreatedAtTime { get; }

        void SetCustomData(string key, object value);
        T GetCustomData<T>(string key);
        bool HasCustomData(string key);
    }
}
