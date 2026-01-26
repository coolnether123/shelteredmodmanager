using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Reflection;
using UnityEngine;

namespace ModAPI.Events
{
    /// <summary>
    /// Central event bus so mods can subscribe instead of duplicating Harmony patches.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<int> OnNewDay;
        public static event Action<SaveData> OnBeforeSave;
        public static event Action<SaveData> OnAfterLoad;
        public static event Action OnNewGame;
        public static event Action OnSessionStarted;
        public static event Action<EncounterCharacter, EncounterCharacter> OnCombatStarted;
        public static event Action<ExplorationParty> OnPartyReturned;

        private static bool _beforeSaveRaised;
        private static bool _afterLoadRaised;
        private static bool _dayHooked;
        private static bool _partyHooked;

        public static void HookDayEvents()
        {
            if (_dayHooked)
                return;
            try
            {
                GameTime.newDay += HandleNewDay;
                _dayHooked = true;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("GameEvents.NewDay.Hook", "Failed to hook day event: " + ex.Message);
            }
        }

        public static void HookPartyEvents()
        {
            if (_partyHooked)
                return;

            try
            {
                var mgr = ExplorationManager.Instance;
                if (mgr == null)
                    return;
                mgr.onPartyReturned += OnPartyReturnedInternal;
                _partyHooked = true;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("GameEvents.Party.Hook", "Failed to hook party returned event: " + ex.Message);
            }
        }

        internal static void ResetSaveFlags()
        {
            _beforeSaveRaised = false;
            _afterLoadRaised = false;
        }

        internal static void TryRaiseBeforeSave(SaveManager mgr)
        {
            if (mgr == null || _beforeSaveRaised)
                return;

            SaveData data;
            if (!Safe.TryGetField(mgr, "m_data", out data) || data == null)
                return;

            _beforeSaveRaised = true;
            SafeInvoke(() => OnBeforeSave?.Invoke(data), "OnBeforeSave");
        }

        internal static void TryRaiseAfterLoad(SaveManager mgr)
        {
            if (mgr == null || _afterLoadRaised)
                return;

            SaveData data;
            if (!Safe.TryGetField(mgr, "m_data", out data) || data == null)
                return;

            List<ISaveable> pending;
            if (Safe.TryGetField(mgr, "m_toLoad", out pending) && pending != null && pending.Count > 0)
                return;

            _afterLoadRaised = true;
            SafeInvoke(() => OnAfterLoad?.Invoke(data), "OnAfterLoad");
        }

        internal static void RaiseCombat(EncounterManager mgr)
        {
            if (mgr == null)
                return;

            List<EncounterCharacter> players;
            List<EncounterCharacter> npcs;
            if (!Safe.TryGetField(mgr, "player_encounter_chars", out players) || players == null || players.Count == 0)
                return;
            if (!Safe.TryGetField(mgr, "npc_encounter_chars", out npcs) || npcs == null || npcs.Count == 0)
                return;

            var player = players[0];
            var enemy = npcs[0];
            SafeInvoke(() => OnCombatStarted?.Invoke(player, enemy), "OnCombatStarted");
        }

        private static void HandleNewDay()
        {
            try
            {
                var day = GameTime.Day;
                SafeInvoke(() => OnNewDay?.Invoke(day), "OnNewDay");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("GameEvents.OnNewDay", "OnNewDay handler failed: " + ex.Message);
            }
        }

        private static void OnPartyReturnedInternal(int partyId)
        {
            try
            {
                var mgr = ExplorationManager.Instance;
                var party = mgr != null ? mgr.GetParty(partyId) : null;
                SafeInvoke(() => OnPartyReturned?.Invoke(party), "OnPartyReturned");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("GameEvents.PartyReturned", "Party returned handler failed: " + ex.Message);
            }
        }

        private static void SafeInvoke(Action action, string name)
        {
            try { action?.Invoke(); }
            catch (Exception ex) { MMLog.WarnOnce("GameEvents.Invoke." + name, name + " handler threw: " + ex.Message); }
        }

        internal static void TryRaiseNewGame()
        {
            SafeInvoke(() => OnNewGame?.Invoke(), "OnNewGame");
        }

        internal static void TryRaiseSessionStarted()
        {
            SafeInvoke(() => OnSessionStarted?.Invoke(), "OnSessionStarted");
        }

        // Harmony patches ---------------------------------------------------
        [HarmonyPatch(typeof(GameTime), "Awake")]
        private static class GameTime_Awake_Patch
        {
            private static void Postfix()
            {
                GameEvents.HookDayEvents();
                GameEvents.TryRaiseSessionStarted();

                // Check for new game specifically
                try
                {
                    var sm = SaveManager.instance;
                    if (sm != null && !sm.isLoading)
                    {
                        GameEvents.TryRaiseNewGame();
                    }
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(SaveManager), "StartSave")]
        private static class SaveManager_StartSave_Patch
        {
            private static void Prefix()
            {
                GameEvents.ResetSaveFlags();
            }
        }

        [HarmonyPatch(typeof(SaveManager), "StartLoad")]
        private static class SaveManager_StartLoad_Patch
        {
            private static void Prefix()
            {
                GameEvents.ResetSaveFlags();
            }
        }

        [HarmonyPatch(typeof(SaveManager), "Update_SaveSceneContents")]
        private static class SaveManager_Update_SaveSceneContents_Patch
        {
            private static void Prefix(SaveManager __instance)
            {
                GameEvents.TryRaiseBeforeSave(__instance);
            }
        }

        [HarmonyPatch(typeof(SaveManager), "Update_LoadSceneContents")]
        private static class SaveManager_Update_LoadSceneContents_Patch
        {
            private static void Postfix(SaveManager __instance)
            {
                GameEvents.TryRaiseAfterLoad(__instance);
            }
        }

        [HarmonyPatch(typeof(ExplorationManager), "StartManager")]
        private static class ExplorationManager_StartManager_Patch
        {
            private static void Postfix()
            {
                GameEvents.HookPartyEvents();
            }
        }

        [HarmonyPatch(typeof(EncounterManager), "EnterState_Combat")]
        private static class EncounterManager_EnterState_Combat_Patch
        {
            private static void Postfix(EncounterManager __instance)
            {
                GameEvents.RaiseCombat(__instance);
            }
        }
    }
}
