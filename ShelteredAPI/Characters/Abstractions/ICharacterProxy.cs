using System;

using ShelteredAPI.Content;
namespace ShelteredAPI.Characters.Abstractions{
    /// <summary>
    /// Stable identity and ownership information for a Sheltered character.
    /// This is the part of a character proxy that can be used without touching live game objects.
    /// </summary>
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

    /// <summary>
    /// Mod-facing handle for real, visitor, and synthetic Sheltered characters.
    /// Use this instead of storing raw FamilyMember or NpcVisitor references across scene or save transitions.
    /// </summary>
    public interface ICharacterProxy : ICharacterDefinition
    {
        CharacterState State { get; }
        CharacterLocation Location { get; }
        bool IsActive { get; }

        ICharacterEffects Effects { get; }
        ICharacterAttributes Attributes { get; }
        ICharacterData Data { get; }

        event Action<ICharacterProxy> OnUnregistered;
    }

    /// <summary>
    /// Origin of a character proxy.
    /// </summary>
    public enum CharacterSource
    {
        RealFamily,
        Visitor,
        Synthetic
    }

    /// <summary>
    /// Detailed runtime state for a character proxy.
    /// Synthetic states are included so mods can reason about characters that are not backed by vanilla family members.
    /// </summary>
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

    /// <summary>
    /// Coarse location bucket for a character.
    /// Prefer this when code only needs shelter, expedition, or away/missing behavior.
    /// </summary>
    public enum CharacterLocation
    {
        Shelter,
        Expedition,
        Missing,
        Away,
        Unknown
    }
}
