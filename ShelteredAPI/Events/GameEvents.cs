using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using ModAPI.Harmony;
using ShelteredAPI.Saves;
using ShelteredAPI.Saves.Backups;
using ShelteredAPI.Saves.Runtime;


namespace ShelteredAPI.Events
{
    /// <summary>Confirmed death, with the native encounter identities when combat supplies them.</summary>
    public sealed class CharacterDeathEventArgs : EventArgs
    {
        public BaseCharacter Victim { get; internal set; }
        public BaseCharacter Killer { get; internal set; }
        public EncounterCharacter EncounterVictim { get; internal set; }
        public EncounterCharacter EncounterKiller { get; internal set; }
        public BaseCharacter.DamageType Cause { get; internal set; }
        public ItemDefinition_Combat.DamageTypeEnum EncounterDamageType { get; internal set; }
        public string Details { get; internal set; }
        /// <summary>Shelter, Breach, or ExpeditionEncounter.</summary>
        public string Context { get; internal set; }
    }

    /// <summary>One timed wear operation. Handlers share this instance in registration order.</summary>
    public sealed class ObjectDegradingEventArgs : EventArgs
    {
        public Obj_Integrity Object { get; private set; }
        public int OriginalWearAmount { get; private set; }
        public int WearAmount { get; set; }
        public float OriginalInterval { get; private set; }
        /// <summary>Interval used to schedule the next wear tick. The stored base interval is restored after Update.</summary>
        public float Interval { get; set; }
        internal ObjectDegradingEventArgs(Obj_Integrity target, int amount, float interval)
        {
            Object = target; OriginalWearAmount = WearAmount = amount;
            OriginalInterval = Interval = interval;
        }
    }

    /// <summary>
    /// Central event bus so mods can subscribe instead of duplicating Harmony patches.
    /// </summary>
    [PatchPolicy(PatchDomain.Events, "GameEvents",
        TargetBehavior = "Session, save/load, combat, and party event bridges",
        FailureMode = "Event callbacks stop firing and mods may fall back to their own Harmony patches.",
        RollbackStrategy = "Disable the Events patch domain or remove the affected event bridge host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    internal static class GameEvents
    {
        internal static event Action<ExplorationManager> OnExpeditionTick;
        internal static event Action<CharacterDeathEventArgs> OnCharacterDied;
        internal static event Action<FamilyMember, bool> OnCharacterFatalDamage;
        internal static event Action<FamilyMember> OnCharacterFatalDamageStarting;
        internal static event Action<ObjectDegradingEventArgs> OnObjectDegrading;
        private static readonly List<WeakReference> EncounterDeaths = new List<WeakReference>();

        private static bool RemoveEncounterDeath(BaseCharacter character)
        {
            bool found = false;
            for (int i = EncounterDeaths.Count - 1; i >= 0; i--)
            {
                object target = EncounterDeaths[i].Target;
                if (target == null || ReferenceEquals(target, character))
                {
                    found |= ReferenceEquals(target, character);
                    EncounterDeaths.RemoveAt(i);
                }
            }
            return found;
        }

        internal static bool HasWearSubscribers { get { return OnObjectDegrading != null; } }
        internal static void RaiseExpeditionTick(ExplorationManager manager)
        {
            ModAPI.Core.SubscriberDispatch.Invoke(OnExpeditionTick, "ExpeditionTick", manager);
        }

        internal static void RaiseFatalDamage(FamilyMember member, bool allowed)
        {
            ModAPI.Core.SubscriberDispatch.Invoke(OnCharacterFatalDamage, "CharacterFatalDamage", member, allowed);
        }

        internal static void RaiseFatalDamageStarting(FamilyMember member)
        {
            ModAPI.Core.SubscriberDispatch.Invoke(OnCharacterFatalDamageStarting, "CharacterFatalDamageStarting", member);
        }

        internal static void RaiseDeath(BaseCharacter victim, bool wasAlive, BaseCharacter.DamageType cause, string details, bool wasteland)
        {
            if (!wasAlive || victim == null) return;
            if (victim.health > 0) return;
            // Encounter settlement subsequently applies damage to the shelter representation.
            // That is the same death, already reported with the richer combat identities.
            if (RemoveEncounterDeath(victim)) return;
            FamilyMember member = victim as FamilyMember;
            string context = wasteland || (member != null && member.isAway) ? "ExpeditionEncounter"
                : BreachMan.instance != null && BreachMan.instance.inProgress ? "Breach" : "Shelter";
            ModAPI.Core.SubscriberDispatch.Invoke(OnCharacterDied, "CharacterDied", new CharacterDeathEventArgs
            {
                Victim = victim, Cause = cause, Details = details, Context = context
            });
        }

        internal static BaseCharacter ResolveEncounterCharacter(EncounterCharacter actor)
        {
            if (actor == null) return null;
            return actor.partyMember != null ? actor.partyMember.person : (BaseCharacter)actor.visitor;
        }

        internal static void RaiseEncounterDeath(EncounterCharacter victim, bool wasAlive)
        {
            if (!wasAlive || victim == null || !victim.isDead || victim.isKnockedOut) return;
            BaseCharacter person = ResolveEncounterCharacter(victim);
            if (person != null)
            {
                RemoveEncounterDeath(person);
                EncounterDeaths.Add(new WeakReference(person));
            }
            EncounterCharacter killer = victim.lastDamageFrom;
            ModAPI.Core.SubscriberDispatch.Invoke(OnCharacterDied, "CharacterDied", new CharacterDeathEventArgs
            {
                Victim = person, Killer = ResolveEncounterCharacter(killer),
                EncounterVictim = victim, EncounterKiller = killer,
                EncounterDamageType = victim.lastDamageType,
                Cause = victim.lastDamageType == ItemDefinition_Combat.DamageTypeEnum.Undefined
                    ? BaseCharacter.DamageType.Bleeding : BaseCharacter.DamageType.Weapon,
                Details = victim.lastDamagedBy.ToString(),
                Context = victim.GetCurrentBattleLocation == EncounterCharacter.BattleLocation.field ? "ExpeditionEncounter"
                    : victim.GetCurrentBattleLocation == EncounterCharacter.BattleLocation.shelterBreach ? "Breach" : "Shelter"
            });
        }

        internal static void ClearEncounterDeath(BaseCharacter character)
        {
            if (character != null && character.health > 0) RemoveEncounterDeath(character);
        }

        internal static ObjectDegradingEventArgs RaiseObjectDegrading(Obj_Integrity target, int amount, float interval)
        {
            ObjectDegradingEventArgs args = new ObjectDegradingEventArgs(target, amount, interval);
            ModAPI.Core.SubscriberDispatch.Invoke(OnObjectDegrading, "ObjectDegrading", args);
            return args;
        }
        public static event Action<int> OnNewDay;
        public static event Action<SaveData> OnBeforeSave;
        public static event Action<SaveData> OnBeforeLoadSceneContents;
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
        private static bool _beforeLoadSceneContentsRaised;
        private static bool _afterLoadRaised;
        private static ExplorationManager _partyEventManager;
        private static SaveData _loadStartData;
        private static bool _suppressNextNewGameForLoad;
        private static readonly object WarnOnceSync = new object();
        private static readonly HashSet<string> LocalWarnOnceKeys = new HashSet<string>(StringComparer.Ordinal);

        internal static void HookDayEvents()
        {
            try
            {
                // Awake clears vanilla's static delegates on each shelter session.
                GameTime.newDay -= HandleNewDay;
                GameTime.newDay += HandleNewDay;
            }
            catch (Exception ex)
            {
                WarnOnce("GameEvents.NewDay.Hook", "Failed to hook day event: " + ex.Message);
            }
        }

        internal static void HookPartyEvents()
        {
            try
            {
                var mgr = ExplorationManager.Instance;
                if (mgr == null)
                    return;
                if (ReferenceEquals(_partyEventManager, mgr))
                    return;
                if (!ReferenceEquals(_partyEventManager, null))
                    _partyEventManager.onPartyReturned -= OnPartyReturnedInternal;
                mgr.onPartyReturned += OnPartyReturnedInternal;
                _partyEventManager = mgr;
            }
            catch (Exception ex)
            {
                WarnOnce("GameEvents.Party.Hook", "Failed to hook party returned event: " + ex.Message);
            }
        }

        internal static void ResetSaveFlags()
        {
            _beforeSaveRaised = false;
            _beforeLoadSceneContentsRaised = false;
            _afterLoadRaised = false;
            SaveBackupService.ClearCurrentSavePass();
        }

        internal static void CaptureLoadStartState(SaveManager mgr)
        {
            SaveData data;
            _loadStartData = mgr != null && TryGetField(mgr, "m_data", out data) ? data : null;
            _suppressNextNewGameForLoad = false;
        }

        private static bool ConsumeLoadNewGameSuppression()
        {
            bool suppress = _suppressNextNewGameForLoad;
            _suppressNextNewGameForLoad = false;
            return suppress;
        }

        internal static void TryRaiseBeforeSave(SaveManager mgr)
        {
            if (mgr == null || _beforeSaveRaised)
                return;

            SaveData data;
            if (!TryGetField(mgr, "m_data", out data) || data == null)
                return;

            EnsureSaveOperationContext(mgr);
            SaveBackupService.BackupCurrentSlotBeforeSave(mgr);
            _beforeSaveRaised = true;
            if (OnBeforeSave != null) ModAPI.Core.SubscriberDispatch.Invoke(OnBeforeSave, "OnBeforeSave", data);

            // Also raise the Sheltered custom-save event bridge.
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
            if (OnAfterLoad != null) ModAPI.Core.SubscriberDispatch.Invoke(OnAfterLoad, "OnAfterLoad", data);

            // Also raise the Sheltered custom-save event bridge.
            try
            {
                TryRaiseCustomSaveEvent("RaiseAfterLoad", "AfterLoad");
            }
            catch (Exception ex)
            {
                WriteError("[GameEvents] Error raising Custom AfterLoad: " + ex);
            }
        }

        internal static void TryRaiseBeforeLoadSceneContents(SaveManager mgr)
        {
            if (mgr == null || _beforeLoadSceneContentsRaised)
                return;

            SaveData data;
            if (!TryGetField(mgr, "m_data", out data) || data == null || ReferenceEquals(data, _loadStartData) || data.isFinished)
                return;

            SaveManager.SaveType type;
            if (TryGetField(mgr, "m_currentType", out type) &&
                (type == SaveManager.SaveType.GlobalData || type == SaveManager.SaveType.Invalid))
                return;

            _beforeLoadSceneContentsRaised = true;
            _suppressNextNewGameForLoad = true;
            if (OnBeforeLoadSceneContents != null)
                ModAPI.Core.SubscriberDispatch.Invoke(OnBeforeLoadSceneContents, "OnBeforeLoadSceneContents", data);

            try
            {
                TryRaiseCustomSaveEvent("RaiseBeforeLoad", "BeforeLoad");
            }
            catch (Exception ex)
            {
                WriteError("[GameEvents] Error raising Custom BeforeLoad: " + ex);
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
            if (OnCombatStarted != null) ModAPI.Core.SubscriberDispatch.Invoke(OnCombatStarted, "OnCombatStarted", player, enemy);
        }

        private static void HandleNewDay()
        {
            try
            {
                var day = GameTime.Day;
                if (OnNewDay != null) ModAPI.Core.SubscriberDispatch.Invoke(OnNewDay, "OnNewDay", day);
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
                if (OnPartyReturned != null) ModAPI.Core.SubscriberDispatch.Invoke(OnPartyReturned, "OnPartyReturned", party);
            }
            catch (Exception ex)
            {
                WarnOnce("GameEvents.PartyReturned", "Party returned handler failed: " + ex.Message);
            }
        }

        internal static void TryRaiseNewGame()
        {
            if (OnNewGame != null) ModAPI.Core.SubscriberDispatch.Invoke(OnNewGame, "OnNewGame");
        }

        internal static void TryRaiseSessionStarted()
        {
            if (OnSessionStarted != null) ModAPI.Core.SubscriberDispatch.Invoke(OnSessionStarted, "OnSessionStarted");
        }

        // Harmony patches ---------------------------------------------------
        [HarmonyPatch(typeof(GameTime), "Awake")]
        private static class GameTime_Awake_Patch
        {
            private static void Postfix()
            {
                GameEvents.HookDayEvents();
                bool loadStartedThisSession = GameEvents.ConsumeLoadNewGameSuppression();
                GameEvents.TryRaiseSessionStarted();

                // SaveManager.isLoading can already be false by GameTime.Awake during a
                // completed load. Valid slot data marks this session as a load;
                // consume it once so load startup cannot be misreported as NewGame.
                try
                {
                    var sm = SaveManager.instance;
                    if (sm != null && !sm.isLoading && !loadStartedThisSession)
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
            private static void Postfix(SaveManager.SaveType type, bool __result)
            {
                if (!__result || type == SaveManager.SaveType.GlobalData || type == SaveManager.SaveType.Invalid)
                    return;
                GameEvents.ResetSaveFlags();
                SaveRuntimeState.TryBeginSaveOperation(type);
            }
        }

        [HarmonyPatch(typeof(SaveManager), "StartLoad")]
        private static class SaveManager_StartLoad_Patch
        {
            private static void Postfix(SaveManager __instance, SaveManager.SaveType type, bool __result)
            {
                if (!__result || type == SaveManager.SaveType.GlobalData || type == SaveManager.SaveType.Invalid)
                    return;
                GameEvents.CaptureLoadStartState(__instance);
                GameEvents.ResetSaveFlags();
            }
        }

        [HarmonyPatch(typeof(SaveManager), "Update_LoadData")]
        private static class SaveManager_Update_LoadData_Patch
        {
            private static void Postfix(SaveManager __instance)
            {
                GameEvents.TryRaiseBeforeLoadSceneContents(__instance);
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
            private static void Prefix(SaveManager __instance)
            {
                GameEvents.TryRaiseBeforeLoadSceneContents(__instance);
            }

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
                SaveManager.SaveType operationType;
                SaveEntry scopedEntry;
                SaveEntry customEntry = SaveRuntimeState.TryGetCurrentSaveOperation(out operationType, out scopedEntry)
                    ? scopedEntry
                    : SaveRuntimeState.ActiveCustomSave;
                if (customEntry == null)
                    return;

                if (methodName == "RaiseBeforeSave")
                    ShelteredAPI.Saves.Events.RaiseBeforeSave(customEntry);
                else if (methodName == "RaiseAfterLoad")
                    ShelteredAPI.Saves.Events.RaiseAfterLoad(customEntry);
                else if (methodName == "RaiseBeforeLoad")
                    ShelteredAPI.Saves.Events.RaiseBeforeLoad(customEntry);
            }
            catch (Exception ex)
            {
                WriteError("[GameEvents] Error raising Custom " + displayName + ": " + ex);
            }
        }

        private static void EnsureSaveOperationContext(SaveManager manager)
        {
            SaveManager.SaveType operationType;
            SaveEntry operationEntry;
            if (SaveRuntimeState.TryGetCurrentSaveOperation(out operationType, out operationEntry))
                return;

            SaveManager.SaveType type;
            if (TryGetField(manager, "m_slotInUse", out type)
                && type != SaveManager.SaveType.Invalid
                && type != SaveManager.SaveType.GlobalData
                && SaveRuntimeState.TryBeginSaveOperation(type))
            {
                return;
            }

            if (TryGetField(manager, "m_currentType", out type)
                && type != SaveManager.SaveType.Invalid
                && type != SaveManager.SaveType.GlobalData)
            {
                SaveRuntimeState.TryBeginSaveOperation(type);
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
