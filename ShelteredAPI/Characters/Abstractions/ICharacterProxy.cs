using System;

namespace ModAPI.Characters
{
    public interface ICharacterDefinition
    {
        int UniqueId { get; }
        string Name { get; }
        bool IsPersistent { get; }
        bool IsLoadedOnShelterEntry { get; }
        string PersistenceKey { get; }
        CharacterSource Source { get; }
        string SourceMod { get; }
    }

    public interface ICharacterProxy : ICharacterDefinition
    {
        CharacterState State { get; }
        CharacterLocation Location { get; }
        FamilyMember UnderlyingMember { get; }
        NpcVisitor UnderlyingNpc { get; }
        bool IsActive { get; }

        ICharacterEffects Effects { get; }
        ICharacterAttributes Attributes { get; }
        ICharacterData Data { get; }

        event Action<ICharacterProxy> OnUnregistered;
    }

    public enum CharacterSource
    {
        RealFamily,
        Visitor,
        Synthetic
    }

    public enum CharacterState
    {
        InShelter,
        OnExpedition,
        Unconscious,
        CatatonicGhost,
        Dead,
        InEncounter,
        TemporarilyAbsent,
        SyntheticIdle,
        SyntheticInEncounter,
        SyntheticAbsent
    }

    public enum CharacterLocation
    {
        Shelter,
        Expedition,
        Missing,
        Away,
        Unknown
    }
}
