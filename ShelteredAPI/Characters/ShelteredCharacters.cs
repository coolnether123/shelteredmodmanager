using System;
using System.Collections.Generic;
using ShelteredAPI.Characters.Internal;

namespace ShelteredAPI.Characters
{
    /// <summary>
    /// Stable Sheltered character facade for mod authors.
    /// </summary>
    public static class ShelteredCharacters
    {
        public static event Action<ICharacterProxy, EffectInstance> EffectApplied
        {
            add { CharacterEffectSystem.InternalInstance.EffectApplied += value; }
            remove { CharacterEffectSystem.InternalInstance.EffectApplied -= value; }
        }

        public static event Action<ICharacterProxy, EffectInstance, RemovalReason> EffectRemoved
        {
            add { CharacterEffectSystem.InternalInstance.EffectRemoved += value; }
            remove { CharacterEffectSystem.InternalInstance.EffectRemoved -= value; }
        }

        public static event Action<ICharacterProxy, string, object> DataChanged
        {
            add { CharacterEffectSystem.InternalInstance.DataChanged += value; }
            remove { CharacterEffectSystem.InternalInstance.DataChanged -= value; }
        }

        public static event Action<ICharacterProxy> SyntheticCharacterCreated
        {
            add { CharacterEffectSystem.InternalInstance.SyntheticCharacterCreated += value; }
            remove { CharacterEffectSystem.InternalInstance.SyntheticCharacterCreated -= value; }
        }

        public static event Action<ICharacterProxy> SyntheticCharacterUnloaded
        {
            add { CharacterEffectSystem.InternalInstance.SyntheticCharacterUnloaded += value; }
            remove { CharacterEffectSystem.InternalInstance.SyntheticCharacterUnloaded -= value; }
        }

        public static event Action<PartyChangedEventArgs> PartyCompositionChanged
        {
            add { PartyHelper.OnPartyCompositionChanged += value; }
            remove { PartyHelper.OnPartyCompositionChanged -= value; }
        }

        public static void RegisterEffectType<T>(string effectId) where T : ICharacterEffect, new()
        {
            CharacterEffectSystem.InternalInstance.RegisterEffectType<T>(effectId);
        }

        public static ICharacterProxy GetByUniqueId(int uniqueMemberId)
        {
            return CharacterEffectSystem.InternalInstance.GetCharacterById(uniqueMemberId);
        }

        public static CharacterQuery Query()
        {
            return CharacterEffectSystem.InternalInstance.Query();
        }

        public static IReadOnlyList<ICharacterProxy> ListAll()
        {
            return CharacterEffectSystem.InternalInstance.GetAllCharacters();
        }

        public static IReadOnlyList<ICharacterProxy> ListPersistent()
        {
            return CharacterEffectSystem.InternalInstance.GetPersistentCharacters();
        }

        public static IReadOnlyList<ICharacterProxy> ListTemporary()
        {
            return CharacterEffectSystem.InternalInstance.GetTemporaryCharacters();
        }

        public static ICharacterProxy CreateSyntheticCharacter(
            string firstName,
            string lastName,
            string persistenceKey,
            string sourceModId,
            bool isPersistent = true)
        {
            return CharacterEffectSystem.InternalInstance.CreateSyntheticCharacter(
                firstName,
                lastName,
                persistenceKey,
                sourceModId,
                isPersistent);
        }

        public static ICharacterProxy CreateTemporaryCharacter(
            string firstName,
            string lastName,
            string sourceModId)
        {
            return CharacterEffectSystem.InternalInstance.CreateTemporaryCharacter(firstName, lastName, sourceModId);
        }

        public static ICharacterProxy FindSyntheticCharacter(string persistenceKey)
        {
            return CharacterEffectSystem.InternalInstance.GetSyntheticCharacter(persistenceKey);
        }

        public static void Unregister(ICharacterProxy character)
        {
            CharacterEffectSystem.InternalInstance.UnregisterCharacter(character);
        }

        public static int UnloadTemporaryCharacters(string sourceModId)
        {
            return CharacterEffectSystem.InternalInstance.UnloadTemporaryCharacters(sourceModId);
        }

        public static ICharacterProxy FromFamilyMember(FamilyMember member)
        {
            return CharacterEffectSystem.InternalInstance.GetCharacter(member);
        }

        public static ICharacterProxy FromNpcVisitor(NpcVisitor npc)
        {
            return CharacterEffectSystem.InternalInstance.GetCharacter(npc);
        }

        public static FamilyMember FindFamilyMember(ICharacterProxy character)
        {
            LiveCharacterProxy live = character as LiveCharacterProxy;
            return live != null ? live.UnderlyingMember : null;
        }

        public static NpcVisitor FindNpcVisitor(ICharacterProxy character)
        {
            LiveCharacterProxy live = character as LiveCharacterProxy;
            return live != null ? live.UnderlyingNpc : null;
        }

        public static void SwapEncounterCharacter(
            EncounterCharacter encounterActor,
            ICharacterProxy newCharacter,
            Action<EncounterCharacter> onSwapComplete = null)
        {
            CharacterEffectSystem.InternalInstance.SwapEncounterCharacter(encounterActor, newCharacter, onSwapComplete);
        }

        public static System.Collections.ObjectModel.ReadOnlyCollection<CharacterInfo> ListFamilyMemberInfo()
        {
            return PartyHelper.GetAllFamilyMembers();
        }

        public static CharacterInfo? GetCharacterInfo(string characterId)
        {
            return PartyHelper.GetCharacter(characterId);
        }

        public static System.Collections.ObjectModel.ReadOnlyCollection<ExpeditionPartyInfo> ListActiveParties()
        {
            return PartyHelper.GetActiveParties();
        }

        public static ExpeditionPartyInfo? GetPartyInfo(int partyId)
        {
            return PartyHelper.GetParty(partyId);
        }
    }
}
