using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Reflection;
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
        public static event Action<TimeTriggerBatch> OnSixHourTick
        {
            add { GameTimeTriggerHelper.OnSixHourTick += value; }
            remove { GameTimeTriggerHelper.OnSixHourTick -= value; }
        }
        public static event Action<TimeTriggerBatch> OnStaggeredTick
        {
            add { GameTimeTriggerHelper.OnStaggeredTick += value; }
            remove { GameTimeTriggerHelper.OnStaggeredTick -= value; }
        }

        private static bool _beforeSaveRaised;
        private static bool _afterLoadRaised;
        private static bool _dayHooked;
        private static bool _partyHooked;
        private static readonly object WarnOnceSync = new object();
        private static readonly HashSet<string> LocalWarnOnceKeys = new HashSet<string>(StringComparer.Ordinal);

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
                WarnOnce("GameEvents.NewDay.Hook", "Failed to hook day event: " + ex.Message);
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
                WarnOnce("GameEvents.Party.Hook", "Failed to hook party returned event: " + ex.Message);
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
            if (!TryGetField(mgr, "m_data", out data) || data == null)
                return;

            _beforeSaveRaised = true;
            if (OnBeforeSave != null) SafeInvoke(delegate { OnBeforeSave(data); }, "OnBeforeSave");

            // ALSO RAISE THE V1.2 CUSTOM SAVE EVENT
            try
            {
                TryRaiseCustomSaveEvent("RaiseBeforeSave", "BeforeSave");
            }
            catch (Exception ex)
            {
                WriteError("[GameEvents] Error raising Custom BeforeSave: " + ex);
            }
        }

        internal static void TryRaiseAfterLoad(SaveManager mgr)
        {
            if (mgr == null || _afterLoadRaised)
                return;

            SaveData data;
            if (!TryGetField(mgr, "m_data", out data) || data == null)
                return;

            List<ISaveable> pending;
            if (TryGetField(mgr, "m_toLoad", out pending) && pending != null && pending.Count > 0)
                return;

            _afterLoadRaised = true;
            if (OnAfterLoad != null) SafeInvoke(delegate { OnAfterLoad(data); }, "OnAfterLoad");

            // ALSO RAISE THE V1.2 CUSTOM SAVE EVENT
            try
            {
                TryRaiseCustomSaveEvent("RaiseAfterLoad", "AfterLoad");
            }
            catch (Exception ex)
            {
                WriteError("[GameEvents] Error raising Custom AfterLoad: " + ex);
            }
        }

        internal static void RaiseCombat(EncounterManager mgr)
        {
            if (mgr == null)
                return;

            List<EncounterCharacter> players;
            List<EncounterCharacter> npcs;
            if (!TryGetField(mgr, "player_encounter_chars", out players) || players == null || players.Count == 0)
                return;
            if (!TryGetField(mgr, "npc_encounter_chars", out npcs) || npcs == null || npcs.Count == 0)
                return;

            var player = players[0];
            var enemy = npcs[0];
            if (OnCombatStarted != null) SafeInvoke(delegate { OnCombatStarted(player, enemy); }, "OnCombatStarted");
        }

        private static void HandleNewDay()
        {
            try
            {
                var day = GameTime.Day;
                if (OnNewDay != null) SafeInvoke(delegate { OnNewDay(day); }, "OnNewDay");
            }
            catch (Exception ex)
            {
                WarnOnce("GameEvents.OnNewDay", "OnNewDay handler failed: " + ex.Message);
            }
        }

        private static void OnPartyReturnedInternal(int partyId)
        {
            try
            {
                var mgr = ExplorationManager.Instance;
                var party = mgr != null ? mgr.GetParty(partyId) : null;
                if (OnPartyReturned != null) SafeInvoke(delegate { OnPartyReturned(party); }, "OnPartyReturned");
            }
            catch (Exception ex)
            {
                WarnOnce("GameEvents.PartyReturned", "Party returned handler failed: " + ex.Message);
            }
        }

        private static void SafeInvoke(Action action, string name)
        {
            try { if (action != null) action(); }
            catch (Exception ex) { WarnOnce("GameEvents.Invoke." + name, name + " handler threw: " + ex.Message); }
        }

        internal static void TryRaiseNewGame()
        {
            if (OnNewGame != null) SafeInvoke(delegate { OnNewGame(); }, "OnNewGame");
        }

        internal static void TryRaiseSessionStarted()
        {
            if (OnSessionStarted != null) SafeInvoke(delegate { OnSessionStarted(); }, "OnSessionStarted");
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

        private static bool TryGetField<T>(object instance, string fieldName, out T value)
        {
            value = default(T);
            if (instance == null || string.IsNullOrEmpty(fieldName))
                return false;

            try
            {
                FieldInfo field = FindField(instance.GetType(), fieldName);
                if (field == null)
                    return false;

                object raw = field.GetValue(instance);
                if (raw is T)
                {
                    value = (T)raw;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type cursor = type; cursor != null; cursor = cursor.BaseType)
            {
                FieldInfo field = cursor.GetField(name, flags);
                if (field != null)
                    return field;
            }
            return null;
        }

        private static void TryRaiseCustomSaveEvent(string methodName, string displayName)
        {
            try
            {
                Type proxyType = Type.GetType("ModAPI.Hooks.PlatformSaveProxy, ModAPI", false);
                if (proxyType == null)
                    return;

                FieldInfo activeCustomSave = proxyType.GetField("ActiveCustomSave", BindingFlags.Public | BindingFlags.Static);
                if (activeCustomSave == null)
                    return;

                object customEntry = activeCustomSave.GetValue(null);
                if (customEntry == null)
                    return;

                Type eventsType = Type.GetType("ModAPI.Saves.Events, ModAPI", false);
                if (eventsType == null)
                    return;

                MethodInfo raiseMethod = eventsType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (raiseMethod == null)
                    return;

                raiseMethod.Invoke(null, new object[] { customEntry });
            }
            catch (Exception ex)
            {
                WriteError("[GameEvents] Error raising Custom " + displayName + ": " + ex);
            }
        }

        private static void WarnOnce(string key, string message)
        {
            try
            {
                Type mmLogType = Type.GetType("ModAPI.Core.MMLog, ModAPI", false);
                if (mmLogType != null)
                {
                    MethodInfo warnOnce = mmLogType.GetMethod("WarnOnce", BindingFlags.Public | BindingFlags.Static);
                    if (warnOnce != null)
                    {
                        warnOnce.Invoke(null, new object[] { key, message });
                        return;
                    }
                }
            }
            catch
            {
            }

            lock (WarnOnceSync)
            {
                if (LocalWarnOnceKeys.Contains(key))
                    return;
                LocalWarnOnceKeys.Add(key);
            }
            Debug.LogWarning("[GameEvents] " + message);
        }

        private static void WriteError(string message)
        {
            try
            {
                Type mmLogType = Type.GetType("ModAPI.Core.MMLog, ModAPI", false);
                if (mmLogType != null)
                {
                    MethodInfo writeError = mmLogType.GetMethod("WriteError", BindingFlags.Public | BindingFlags.Static);
                    if (writeError != null)
                    {
                        writeError.Invoke(null, new object[] { message });
                        return;
                    }
                }
            }
            catch
            {
            }

            Debug.LogError(message);
        }
    }
}
