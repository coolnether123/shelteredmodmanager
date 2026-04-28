using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ModAPI.Reflection;
using UnityEngine;

namespace ModAPI.GameState
{
    // --- Interfaces for Read-Only access ---

    public interface IReadOnlyInventory
    {
        int StorageCapacity { get; }
        int UsedStorage { get; }
        bool IsFull { get; }
        int GetItemCount(ItemManager.ItemType item, bool includeParties = false);
        List<ItemStack> GetAllItems();
    }

    public interface IReadOnlyFamilyRoster
    {
        int Count { get; }
        int LivingCount { get; }
        bool IsGameOver { get; }
        List<FamilyMember> GetAllMembers();
        FamilyMember GetMemberById(int id);
    }

    public interface IReadOnlyExplorationState
    {
        bool AnyPartiesExploring { get; }
        int ActivePartyCount { get; }
        List<ExplorationParty> GetAllParties();
    }

    public interface IReadOnlyEncounterState
    {
        bool InProgress { get; }
        EncounterManager.EncounterState State { get; }
        List<EncounterCharacter> PlayerCharacters { get; }
        List<EncounterCharacter> NpcCharacters { get; }
    }

    public interface IReadOnlyWeatherState
    {
        WeatherManager.WeatherState CurrentWeather { get; }
        int DaysSinceRain { get; }
    }

    public interface IReadOnlyGameTime
    {
        int Day { get; }
        float TimeOfDay { get; }
        bool IsPaused { get; }
    }

    // --- Implementation Wrappers ---

    internal class InventoryWrapper : IReadOnlyInventory
    {
        private InventoryManager Mgr => InventoryManager.Instance;
        public int StorageCapacity => Mgr != null ? Mgr.storageCapacity : 0;
        public int UsedStorage => Mgr != null ? Mgr.GetTotalStackCount() : 0;
        public bool IsFull => Mgr != null && Mgr.IsInventoryFull();
        public int GetItemCount(ItemManager.ItemType item, bool includeParties = false) => Mgr != null ? Mgr.GetItemCountInStorage(item, includeParties) : 0;
        public List<ItemStack> GetAllItems() => Mgr != null ? Mgr.GetItems() : new List<ItemStack>();
    }

    internal class FamilyRosterWrapper : IReadOnlyFamilyRoster
    {
        private FamilyManager Mgr => FamilyManager.Instance;
        public int Count => Mgr != null ? Mgr.GetAllFamilyMembers().Count : 0;
        public int LivingCount
        {
            get
            {
                if (Mgr == null) return 0;
                int count = 0;
                foreach (var m in Mgr.GetAllFamilyMembers())
                {
                    if (m != null && !m.isDead) count++;
                }
                return count;
            }
        }
        public bool IsGameOver => Mgr != null && Mgr.isGameOver;
        public List<FamilyMember> GetAllMembers() => Mgr != null ? Mgr.GetAllFamilyMembers() : new List<FamilyMember>();
        public FamilyMember GetMemberById(int id) => Mgr != null ? Mgr.GetFamilyMember(id) : null;
    }

    internal class ExplorationWrapper : IReadOnlyExplorationState
    {
        private ExplorationManager Mgr => ExplorationManager.Instance;
        public bool AnyPartiesExploring => Mgr != null && Mgr.isPartyExploring;
        public int ActivePartyCount => Mgr != null ? Mgr.GetAllExplorarionParties().Count : 0;
        public List<ExplorationParty> GetAllParties() => Mgr != null ? Mgr.GetAllExplorarionParties() : new List<ExplorationParty>();
    }

    internal class EncounterWrapper : IReadOnlyEncounterState
    {
        private EncounterManager Mgr => EncounterManager.Instance;
        public bool InProgress => Mgr != null && Mgr.EncounterInProgress;
        public EncounterManager.EncounterState State => Mgr != null ? Mgr.GetEncounterState() : EncounterManager.EncounterState.NotStarted;
        public List<EncounterCharacter> PlayerCharacters => Mgr != null ? Mgr.GetPlayerCharacters() : new List<EncounterCharacter>();
        public List<EncounterCharacter> NpcCharacters => Mgr != null ? Mgr.GetNPCs() : new List<EncounterCharacter>();
    }

    internal class WeatherWrapper : IReadOnlyWeatherState
    {
        private WeatherManager Mgr => WeatherManager.Instance;
        public WeatherManager.WeatherState CurrentWeather => Mgr != null ? Mgr.currentState : WeatherManager.WeatherState.None;
        public int DaysSinceRain => Safe.GetFieldOrDefault(Mgr, "daysSinceRain", 0);
    }

    internal class GameTimeWrapper : IReadOnlyGameTime
    {
        public int Day => GameTime.Day;
        public float TimeOfDay => GameTime.DayProgress;
        public bool IsPaused => Time.timeScale == 0f; // Simplified check for paused state
    }

    /// <summary>
    /// Safe access to game singletons and state.
    /// Provides read-only interfaces to prevent mods from accidentally corrupting game state.
    /// </summary>
    public static class ManagerStateHelper
    {
        private static readonly IReadOnlyInventory _inventory = new InventoryWrapper();
        private static readonly IReadOnlyFamilyRoster _family = new FamilyRosterWrapper();
        private static readonly IReadOnlyExplorationState _exploration = new ExplorationWrapper();
        private static readonly IReadOnlyEncounterState _encounter = new EncounterWrapper();
        private static readonly IReadOnlyWeatherState _weather = new WeatherWrapper();
        private static readonly IReadOnlyGameTime _time = new GameTimeWrapper();

        public static IReadOnlyInventory GetInventory() => _inventory;
        public static IReadOnlyFamilyRoster GetFamilyRoster() => _family;
        public static IReadOnlyExplorationState GetExplorationState() => _exploration;
        public static IReadOnlyEncounterState GetEncounterState() => _encounter;
        public static IReadOnlyWeatherState GetWeatherState() => _weather;
        public static IReadOnlyGameTime GetGameTime() => _time;

        // Safe Queries
        public static bool IsCharacterAway(string characterId)
        {
            if (FamilyManager.Instance == null) return false;
            foreach (var m in FamilyManager.Instance.GetAllFamilyMembers())
            {
                if (m.firstName == characterId || m.GetId().ToString() == characterId)
                {
                    return m.isAway;
                }
            }
            return false;
        }

        public static int GetPartyCount()
        {
            return ExplorationManager.Instance != null ? ExplorationManager.Instance.GetAllExplorarionParties().Count : 0;
        }

        public static ReadOnlyCollection<string> GetActivePartyMembers()
        {
            var list = new List<string>();
            if (ExplorationManager.Instance == null) return list.AsReadOnly();

            foreach (var party in ExplorationManager.Instance.GetAllExplorarionParties())
            {
                for (int i = 0; i < party.membersCount; i++)
                {
                    var m = party.GetMember(i);
                    if (m != null && m.person != null)
                        list.Add(m.person.firstName);
                }
            }
            return list.AsReadOnly();
        }
    }
}
