using System;
using System.Collections.Generic;

namespace ModAPI.Characters
{
    public interface ICharacterEffectSystem
    {
        void RegisterEffectType<T>(string effectId) where T : ICharacterEffect, new();

        ICharacterProxy GetCharacter(FamilyMember member);
        ICharacterProxy GetCharacter(NpcVisitor npc);
        ICharacterProxy GetCharacterById(int uniqueMemberId);

        CharacterQuery Query();
        IReadOnlyList<ICharacterProxy> GetAllCharacters();
        IReadOnlyList<ICharacterProxy> GetPersistentCharacters();
        IReadOnlyList<ICharacterProxy> GetTemporaryCharacters();
        void UnregisterCharacter(ICharacterProxy character);

        ICharacterProxy CreateSyntheticCharacter(
            string firstName,
            string lastName,
            string persistenceKey,
            string sourceModId,
            bool isPersistent = true);

        ICharacterProxy CreateTemporaryCharacter(
            string firstName,
            string lastName,
            string sourceModId);

        void SwapEncounterCharacter(
            EncounterCharacter encounterActor,
            ICharacterProxy newCharacter,
            Action<EncounterCharacter> onSwapComplete = null);

        int UnloadTemporaryCharacters(string sourceModId);
        ICharacterProxy GetSyntheticCharacter(string persistenceKey);

        event Action<ICharacterProxy, EffectInstance> EffectApplied;
        event Action<ICharacterProxy, EffectInstance, RemovalReason> EffectRemoved;
        event Action<ICharacterProxy, string, object> DataChanged;
        event Action<ICharacterProxy> SyntheticCharacterCreated;
        event Action<ICharacterProxy> SyntheticCharacterUnloaded;
    }

    public interface ICharacterFactory
    {
        ICharacterProxy CreateSyntheticCharacter(
            string name,
            int? baseId = null,
            string persistenceKey = null,
            string sourceModId = null);

        ICharacterProxy CreateTemporaryCharacter(
            string name,
            string sourceModId);

        ICharacterProxy RestoreSyntheticCharacter(CharacterSaveData data);
    }
}
