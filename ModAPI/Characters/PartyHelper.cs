using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ModAPI.Harmony;
using ModAPI.Reflection;
using UnityEngine;

namespace ModAPI.Characters
{
    /// <summary>
    /// Information about a family member.
    /// </summary>
    public struct CharacterInfo
    {
        public int Id;
        public string FirstName;
        public string LastName;
        public bool IsDead;
        public bool IsAway;
        public bool IsChild;
        public bool IsAdopted;
        public bool IsUnconscious;
        public bool IsCatatonic;
        public int Health;
        public int MaxHealth;
        public Vector3 Position;

        // Statistics (Accessed via Reflection)
        public int HumansKilled;
        public int CompletedCrafts;
        public int CompletedFixes;
        public float SecondsWithoutFood;
        public float SecondsWithoutWater;
        public float SecondsWithoutSleep;

        // Needs (0-1 range)
        public float Hunger;
        public float Thirst;
        public float Fatigue;
        public float Dirtiness;
        public float Toilet;
        public float Stress;
        public float Trauma;
        public float Loyalty;

        public CharacterInfo(FamilyMember member)
        {
            if (member == null)
            {
                Id = -1;
                FirstName = "Unknown";
                LastName = "";
                IsDead = true;
                IsAway = false;
                IsChild = false;
                IsAdopted = false;
                IsUnconscious = false;
                IsCatatonic = false;
                Health = 0;
                MaxHealth = 0;
                Position = Vector3.zero;

                HumansKilled = 0;
                CompletedCrafts = 0;
                CompletedFixes = 0;
                SecondsWithoutFood = 0f;
                SecondsWithoutWater = 0f;
                SecondsWithoutSleep = 0f;

                Hunger = 0f;
                Thirst = 0f;
                Fatigue = 0f;
                Dirtiness = 0f;
                Toilet = 0f;
                Stress = 0f;
                Trauma = 0f;
                Loyalty = 1f;
                return;
            }

            Id = member.GetId();
            FirstName = member.firstName;
            LastName = member.lastName;
            IsDead = member.isDead;
            IsAway = member.isAway;
            IsChild = member.isChild;
            IsAdopted = member.isAdopted;
            IsUnconscious = member.IsUnconscious;
            IsCatatonic = member.isCatatonic;
            Health = member.health;
            MaxHealth = member.maxHealth;
            Position = member.transform.position;

            HumansKilled = Safe.GetFieldOrDefault(member, "m_humansKilledInCombat", 0);
            CompletedCrafts = Safe.GetFieldOrDefault(member, "m_completedCrafts", 0);
            CompletedFixes = Safe.GetFieldOrDefault(member, "m_completedFixes", 0);
            SecondsWithoutFood = Safe.GetFieldOrDefault(member, "m_secondsWithoutFood", 0f);
            SecondsWithoutWater = Safe.GetFieldOrDefault(member, "m_secondsWithoutWater", 0f);
            SecondsWithoutSleep = Safe.GetFieldOrDefault(member, "m_secondsWithoutSleep", 0f);

            var s = member.stats;
            if (s != null)
            {
                Hunger = s.hunger != null ? s.hunger.NormalizedValue : 0f;
                Thirst = s.thirst != null ? s.thirst.NormalizedValue : 0f;
                Fatigue = s.fatigue != null ? s.fatigue.NormalizedValue : 0f;
                Dirtiness = s.dirtiness != null ? s.dirtiness.NormalizedValue : 0f;
                Toilet = s.toilet != null ? s.toilet.NormalizedValue : 0f;
                Stress = s.stress != null ? s.stress.NormalizedValue : 0f;
                Trauma = s.trauma != null ? s.trauma.NormalizedValue : 0f;
                Loyalty = s.loyalty != null ? s.loyalty.NormalizedValue : 1f;
            }
            else
            {
                Hunger = Thirst = Fatigue = Dirtiness = Toilet = Stress = Trauma = 0f;
                Loyalty = 1f;
            }
        }
    }

    /// <summary>
    /// Information about an active expedition party.
    /// </summary>
    public struct ExpeditionPartyInfo
    {
        public int Id;
        public Vector2 MapLocation;
        public bool IsReturning;
        public int MemberCount;
        public ReadOnlyCollection<int> MemberIds;
        public bool HasVehicle;
        public bool HasHorse;
        public bool HasPet;

        // Expedition Details (Accessed via Reflection)
        public float Water;
        public float Petrol;
        public float DistanceTraveled;
        public int CombatKills;
        public float WaterContamination;

        public ExpeditionPartyInfo(ExplorationParty party)
        {
            if (party == null)
            {
                Id = -1;
                MapLocation = Vector2.zero;
                IsReturning = false;
                MemberCount = 0;
                MemberIds = new List<int>().AsReadOnly();
                HasVehicle = false;
                HasHorse = false;
                HasPet = false;

                Water = 0f;
                Petrol = 0f;
                DistanceTraveled = 0f;
                CombatKills = 0;
                WaterContamination = 0f;
                return;
            }

            Id = party.id;
            MapLocation = party.location;
            IsReturning = party.isReturning;
            MemberCount = party.membersCount;
            
            var ids = new List<int>();
            for (int i = 0; i < party.membersCount; i++)
            {
                var m = party.GetMember(i);
                if (m != null && m.person != null)
                {
                    ids.Add(m.person.GetId());
                }
            }
            MemberIds = ids.AsReadOnly();
            
            HasVehicle = party.GetVehicle() != null;
            HasHorse = party.GetHorse() != null;
            HasPet = party.GetPet() != null;

            Water = Safe.GetFieldOrDefault(party, "m_water", 0f);
            Petrol = Safe.GetFieldOrDefault(party, "m_petrol", 0f);
            DistanceTraveled = Safe.GetFieldOrDefault(party, "m_distanceTraveled", 0f);
            CombatKills = Safe.GetFieldOrDefault(party, "m_numberOfCombatKills", 0);
            WaterContamination = Safe.GetFieldOrDefault(party, "m_waterContamination", 0f);
        }
    }

    public class PartyChangedEventArgs : EventArgs
    {
        public int PartyId { get; }
        public PartyChangedEventArgs(int partyId) => PartyId = partyId;
    }

    /// <summary>
    /// Safe, read-only access to family and expedition party data.
    /// </summary>
    public static class PartyHelper
    {
        /// <summary>
        /// Raised when an expedition party's composition changes (member added/removed) 
        /// or a party is created/disbanded.
        /// </summary>
        public static event Action<PartyChangedEventArgs> OnPartyCompositionChanged;

        /// <summary>
        /// Returns all family members currently in the game state.
        /// </summary>
        public static ReadOnlyCollection<CharacterInfo> GetAllFamilyMembers()
        {
            var list = new List<CharacterInfo>();
            if (FamilyManager.Instance == null) return list.AsReadOnly();

            foreach (var m in FamilyManager.Instance.GetAllFamilyMembers())
            {
                if (m != null) list.Add(new CharacterInfo(m));
            }
            return list.AsReadOnly();
        }

        /// <summary>
        /// Looks up a character by their ID or FirstName.
        /// </summary>
        public static CharacterInfo? GetCharacter(string characterId)
        {
            if (FamilyManager.Instance == null) return null;

            foreach (var m in FamilyManager.Instance.GetAllFamilyMembers())
            {
                if (m != null && (m.GetId().ToString() == characterId || m.firstName == characterId))
                {
                    return new CharacterInfo(m);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns all currently active expedition parties.
        /// </summary>
        public static ReadOnlyCollection<ExpeditionPartyInfo> GetActiveParties()
        {
            var list = new List<ExpeditionPartyInfo>();
            if (ExplorationManager.Instance == null) return list.AsReadOnly();

            foreach (var p in ExplorationManager.Instance.GetAllExplorarionParties())
            {
                if (p != null) list.Add(new ExpeditionPartyInfo(p));
            }
            return list.AsReadOnly();
        }

        /// <summary>
        /// Looks up an active expedition party by its ID.
        /// </summary>
        public static ExpeditionPartyInfo? GetParty(int partyId)
        {
            if (ExplorationManager.Instance == null) return null;
            var p = ExplorationManager.Instance.GetParty(partyId);
            if (p == null) return null;
            return new ExpeditionPartyInfo(p);
        }

        // Internal trigger for the event
        internal static void NotifyPartyChanged(int partyId)
        {
            try
            {
                OnPartyCompositionChanged?.Invoke(new PartyChangedEventArgs(partyId));
            }
            catch (Exception e)
            {
                Debug.LogError($"[PartyHelper] Error in OnPartyCompositionChanged handler: {e}");
            }
        }
    }
}
