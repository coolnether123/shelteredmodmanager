using System;
using System.Collections.Generic;
using ShelteredAPI.Characters.Internal;

using ShelteredAPI.Characters.Abstractions;
using ShelteredAPI.Characters.Models;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Authoring;
namespace ShelteredAPI.Characters
{
    /// <summary>
    /// Stable Sheltered character facade for mod authors.
    /// Use this entry point to work with family members, NPC visitors, synthetic characters, and character effects.
    /// </summary>
    public static class ShelteredCharacters
    {
        /// <summary>Raised after an effect is applied to a character.</summary>
        public static event Action<ICharacterProxy, EffectInstance> EffectApplied
        {
            add { CharacterEffectSystem.InternalInstance.EffectApplied += value; }
            remove { CharacterEffectSystem.InternalInstance.EffectApplied -= value; }
        }

        /// <summary>Raised after an effect is removed from a character.</summary>
        public static event Action<ICharacterProxy, EffectInstance, RemovalReason> EffectRemoved
        {
            add { CharacterEffectSystem.InternalInstance.EffectRemoved += value; }
            remove { CharacterEffectSystem.InternalInstance.EffectRemoved -= value; }
        }

        /// <summary>Raised when custom character data changes through the character API.</summary>
        public static event Action<ICharacterProxy, string, object> DataChanged
        {
            add { CharacterEffectSystem.InternalInstance.DataChanged += value; }
            remove { CharacterEffectSystem.InternalInstance.DataChanged -= value; }
        }

        /// <summary>Raised after a mod-created synthetic character is registered.</summary>
        public static event Action<ICharacterProxy> SyntheticCharacterCreated
        {
            add { CharacterEffectSystem.InternalInstance.SyntheticCharacterCreated += value; }
            remove { CharacterEffectSystem.InternalInstance.SyntheticCharacterCreated -= value; }
        }

        /// <summary>Raised after a temporary or unloaded synthetic character is removed from the runtime set.</summary>
        public static event Action<ICharacterProxy> SyntheticCharacterUnloaded
        {
            add { CharacterEffectSystem.InternalInstance.SyntheticCharacterUnloaded += value; }
            remove { CharacterEffectSystem.InternalInstance.SyntheticCharacterUnloaded -= value; }
        }

        /// <summary>Raised when an expedition party is created, disbanded, or has members added or removed.</summary>
        public static event Action<PartyChangedEventArgs> PartyCompositionChanged
        {
            add { PartyHelper.OnPartyCompositionChanged += value; }
            remove { PartyHelper.OnPartyCompositionChanged -= value; }
        }

        /// <summary>
        /// Registers a custom effect type under a stable effect ID.
        /// Call this during plugin initialization before applying the effect by ID.
        /// </summary>
        public static void RegisterEffectType<T>(string effectId) where T : ICharacterEffect, new()
        {
            CharacterEffectSystem.InternalInstance.RegisterEffectType<T>(effectId);
        }

        /// <summary>
        /// Resolves a character proxy by its Sheltered unique member or synthetic character ID.
        /// </summary>
        public static ICharacterProxy GetByUniqueId(int uniqueMemberId)
        {
            return CharacterEffectSystem.InternalInstance.GetCharacterById(uniqueMemberId);
        }

        /// <summary>
        /// Starts a fluent query over the current character proxy set.
        /// </summary>
        public static CharacterQuery Query()
        {
            return CharacterEffectSystem.InternalInstance.Query();
        }

        /// <summary>
        /// Lists every known real, visitor, and synthetic character proxy.
        /// </summary>
        public static IReadOnlyList<ICharacterProxy> ListAll()
        {
            return CharacterEffectSystem.InternalInstance.GetAllCharacters();
        }

        /// <summary>
        /// Lists characters that should survive save/load or scene transitions.
        /// </summary>
        public static IReadOnlyList<ICharacterProxy> ListPersistent()
        {
            return CharacterEffectSystem.InternalInstance.GetPersistentCharacters();
        }

        /// <summary>
        /// Lists temporary characters owned by the current runtime session.
        /// </summary>
        public static IReadOnlyList<ICharacterProxy> ListTemporary()
        {
            return CharacterEffectSystem.InternalInstance.GetTemporaryCharacters();
        }

        /// <summary>
        /// Creates a mod-owned synthetic character that can participate in character APIs without a vanilla FamilyMember.
        /// Use a stable persistence key when <paramref name="isPersistent"/> is true.
        /// </summary>
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

        /// <summary>
        /// Creates a mod-owned temporary character for the current session.
        /// Temporary characters are intended for encounters, previews, or transient systems.
        /// </summary>
        public static ICharacterProxy CreateTemporaryCharacter(
            string firstName,
            string lastName,
            string sourceModId)
        {
            return CharacterEffectSystem.InternalInstance.CreateTemporaryCharacter(firstName, lastName, sourceModId);
        }

        /// <summary>
        /// Finds a persistent synthetic character by the persistence key supplied at creation time.
        /// </summary>
        public static ICharacterProxy FindSyntheticCharacter(string persistenceKey)
        {
            return CharacterEffectSystem.InternalInstance.GetSyntheticCharacter(persistenceKey);
        }

        /// <summary>
        /// Removes a character proxy from the character runtime.
        /// Use this only for mod-owned synthetic or temporary characters.
        /// </summary>
        public static void Unregister(ICharacterProxy character)
        {
            CharacterEffectSystem.InternalInstance.UnregisterCharacter(character);
        }

        /// <summary>
        /// Unloads all temporary characters owned by a mod and returns the number removed.
        /// </summary>
        public static int UnloadTemporaryCharacters(string sourceModId)
        {
            return CharacterEffectSystem.InternalInstance.UnloadTemporaryCharacters(sourceModId);
        }

        /// <summary>
        /// Wraps a vanilla family member in a character proxy.
        /// Do not store the raw family member across scene or save transitions.
        /// </summary>
        public static ICharacterProxy FromFamilyMember(FamilyMember member)
        {
            return CharacterEffectSystem.InternalInstance.GetCharacter(member);
        }

        /// <summary>
        /// Wraps a vanilla NPC visitor in a character proxy.
        /// </summary>
        public static ICharacterProxy FromNpcVisitor(NpcVisitor npc)
        {
            return CharacterEffectSystem.InternalInstance.GetCharacter(npc);
        }

        /// <summary>
        /// Returns the current vanilla FamilyMember backing a live character proxy, or null when none exists.
        /// </summary>
        public static FamilyMember FindFamilyMember(ICharacterProxy character)
        {
            LiveCharacterProxy live = character as LiveCharacterProxy;
            return live != null ? live.UnderlyingMember : null;
        }

        /// <summary>
        /// Returns the current vanilla NpcVisitor backing a live character proxy, or null when none exists.
        /// </summary>
        public static NpcVisitor FindNpcVisitor(ICharacterProxy character)
        {
            LiveCharacterProxy live = character as LiveCharacterProxy;
            return live != null ? live.UnderlyingNpc : null;
        }

        /// <summary>
        /// Replaces an encounter actor's visible character data with another character proxy.
        /// Use this for scenario or event encounters that should present a mod-selected character.
        /// </summary>
        public static void SwapEncounterCharacter(
            EncounterCharacter encounterActor,
            ICharacterProxy newCharacter,
            Action<EncounterCharacter> onSwapComplete = null)
        {
            CharacterEffectSystem.InternalInstance.SwapEncounterCharacter(encounterActor, newCharacter, onSwapComplete);
        }

        /// <summary>
        /// Returns read-only snapshots for all current family members.
        /// </summary>
        public static System.Collections.ObjectModel.ReadOnlyCollection<CharacterInfo> ListFamilyMemberInfo()
        {
            return PartyHelper.GetAllFamilyMembers();
        }

        /// <summary>
        /// Returns a read-only family member snapshot by ID or first name.
        /// </summary>
        public static CharacterInfo? GetCharacterInfo(string characterId)
        {
            return PartyHelper.GetCharacter(characterId);
        }

        /// <summary>
        /// Returns read-only snapshots for active expedition parties.
        /// </summary>
        public static System.Collections.ObjectModel.ReadOnlyCollection<ExpeditionPartyInfo> ListActiveParties()
        {
            return PartyHelper.GetActiveParties();
        }

        /// <summary>
        /// Returns a read-only expedition party snapshot by party ID.
        /// </summary>
        public static ExpeditionPartyInfo? GetPartyInfo(int partyId)
        {
            return PartyHelper.GetParty(partyId);
        }
    }
}
