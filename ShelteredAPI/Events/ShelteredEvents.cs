using System;
using System.Collections.Generic;
using UnityEngine;

using ShelteredAPI.Saves;
namespace ShelteredAPI.Events
{
    /// <summary>
    /// Stable mod-facing event facade for Sheltered game, UI, faction, and time events.
    /// </summary>
    public static class ShelteredEvents
    {
        public static event Action<int> NewDay
        {
            add { GameEvents.OnNewDay += value; }
            remove { GameEvents.OnNewDay -= value; }
        }

        public static event Action<int, int, int, int> CalendarTimeProjected
        {
            add { GameEvents.OnCalendarTimeProjected += value; }
            remove { GameEvents.OnCalendarTimeProjected -= value; }
        }

        public static event Action<SaveData> BeforeSave
        {
            add { GameEvents.OnBeforeSave += value; }
            remove { GameEvents.OnBeforeSave -= value; }
        }

        public static event Action<SaveData> BeforeLoadSceneContents
        {
            add { GameEvents.OnBeforeLoadSceneContents += value; }
            remove { GameEvents.OnBeforeLoadSceneContents -= value; }
        }

        public static event Action<SaveData> AfterLoad
        {
            add { GameEvents.OnAfterLoad += value; }
            remove { GameEvents.OnAfterLoad -= value; }
        }

        public static event Action NewGame
        {
            add { GameEvents.OnNewGame += value; }
            remove { GameEvents.OnNewGame -= value; }
        }

        public static event Action SessionStarted
        {
            add { GameEvents.OnSessionStarted += value; }
            remove { GameEvents.OnSessionStarted -= value; }
        }

        public static event Action<EncounterCharacter, EncounterCharacter> CombatStarted
        {
            add { GameEvents.OnCombatStarted += value; }
            remove { GameEvents.OnCombatStarted -= value; }
        }

        public static event Action<ExplorationParty> PartyReturned
        {
            add { GameEvents.OnPartyReturned += value; }
            remove { GameEvents.OnPartyReturned -= value; }
        }

        public static event Action<BasePanel> PanelOpened
        {
            add { UIEvents.OnPanelOpened += value; }
            remove { UIEvents.OnPanelOpened -= value; }
        }

        public static event Action<BasePanel> PanelClosed
        {
            add { UIEvents.OnPanelClosed += value; }
            remove { UIEvents.OnPanelClosed -= value; }
        }

        public static event Action<BasePanel> PanelResumed
        {
            add { UIEvents.OnPanelResumed += value; }
            remove { UIEvents.OnPanelResumed -= value; }
        }

        public static event Action<BasePanel> PanelPaused
        {
            add { UIEvents.OnPanelPaused += value; }
            remove { UIEvents.OnPanelPaused -= value; }
        }

        public static event Action<GameObject, string> ButtonClicked
        {
            add { UIEvents.OnButtonClicked += value; }
            remove { UIEvents.OnButtonClicked -= value; }
        }

        public static event Action<int> FactionSpawned
        {
            add { FactionEvents.OnFactionSpawned += value; }
            remove { FactionEvents.OnFactionSpawned -= value; }
        }

        public static event Action<int, int> FactionZoneGrew
        {
            add { FactionEvents.OnFactionZoneGrow += value; }
            remove { FactionEvents.OnFactionZoneGrow -= value; }
        }

        public static event Action<int, int> FactionTerritoryChanged
        {
            add { FactionEvents.OnFactionTerritoryChanged += value; }
            remove { FactionEvents.OnFactionTerritoryChanged -= value; }
        }

        public static event Action<TimeTriggerBatch> SixHourTick
        {
            add { GameTimeTriggerHelper.OnSixHourTick += value; }
            remove { GameTimeTriggerHelper.OnSixHourTick -= value; }
        }

        public static event Action<TimeTriggerBatch> StaggeredTick
        {
            add { GameTimeTriggerHelper.OnStaggeredTick += value; }
            remove { GameTimeTriggerHelper.OnStaggeredTick -= value; }
        }

        public static int StaggeredMinHours
        {
            get { return GameTimeTriggerHelper.StaggeredMinHours; }
        }

        public static int StaggeredMaxHours
        {
            get { return GameTimeTriggerHelper.StaggeredMaxHours; }
        }

        public static void RegisterTimeTrigger(string triggerId)
        {
            GameTimeTriggerHelper.RegisterTrigger(triggerId);
        }

        public static void RegisterTimeTrigger(string triggerId, int priority)
        {
            GameTimeTriggerHelper.RegisterTrigger(triggerId, priority);
        }

        public static void RegisterTimeTrigger(string triggerId, int priority, TimeTriggerCadence cadence)
        {
            GameTimeTriggerHelper.RegisterTrigger(triggerId, priority, cadence);
        }

        public static void RegisterTimeTrigger(string triggerId, int priority, TimeTriggerCadence cadence, Action<TimeTriggerBatch> callback)
        {
            GameTimeTriggerHelper.RegisterTrigger(triggerId, priority, cadence, callback);
        }

        public static bool UnregisterTimeTrigger(string triggerId)
        {
            return GameTimeTriggerHelper.UnregisterTrigger(triggerId);
        }

        public static List<TimeTriggerInfo> GetTimeTriggerPriorityList(TimeTriggerCadence cadence)
        {
            return GameTimeTriggerHelper.GetPriorityList(cadence);
        }

        public static void ConfigureStaggeredTimeRange(int minInclusiveHours, int maxInclusiveHours)
        {
            GameTimeTriggerHelper.ConfigureStaggeredRange(minInclusiveHours, maxInclusiveHours);
        }

        public static bool IsPanelOpen<TPanel>() where TPanel : BasePanel
        {
            return UIEvents.IsPanelOpen<TPanel>();
        }

        public static string GetUiDiagnostics()
        {
            return UIEvents.GetDiagnostics();
        }
    }
}
