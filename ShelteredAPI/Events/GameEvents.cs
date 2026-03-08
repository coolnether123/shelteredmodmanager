using System;
using System.Reflection;

namespace ModAPI.Events
{
    /// <summary>
    /// Binary-compatibility bridge for older mods that resolved GameEvents from ShelteredAPI.
    /// The canonical runtime implementation lives in ModAPI.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<int> OnNewDay
        {
            add { AddHandler("OnNewDay", value); }
            remove { RemoveHandler("OnNewDay", value); }
        }

        public static event Action<SaveData> OnBeforeSave
        {
            add { AddHandler("OnBeforeSave", value); }
            remove { RemoveHandler("OnBeforeSave", value); }
        }

        public static event Action<SaveData> OnAfterLoad
        {
            add { AddHandler("OnAfterLoad", value); }
            remove { RemoveHandler("OnAfterLoad", value); }
        }

        public static event Action OnNewGame
        {
            add { AddHandler("OnNewGame", value); }
            remove { RemoveHandler("OnNewGame", value); }
        }

        public static event Action OnSessionStarted
        {
            add { AddHandler("OnSessionStarted", value); }
            remove { RemoveHandler("OnSessionStarted", value); }
        }

        public static event Action<EncounterCharacter, EncounterCharacter> OnCombatStarted
        {
            add { AddHandler("OnCombatStarted", value); }
            remove { RemoveHandler("OnCombatStarted", value); }
        }

        public static event Action<ExplorationParty> OnPartyReturned
        {
            add { AddHandler("OnPartyReturned", value); }
            remove { RemoveHandler("OnPartyReturned", value); }
        }

        private static void AddHandler(string eventName, Delegate handler)
        {
            ChangeHandler(eventName, handler, true);
        }

        private static void RemoveHandler(string eventName, Delegate handler)
        {
            ChangeHandler(eventName, handler, false);
        }

        private static void ChangeHandler(string eventName, Delegate handler, bool add)
        {
            if (handler == null)
                return;

            try
            {
                Type runtimeType = Type.GetType("ModAPI.Events.GameEvents, ModAPI", false);
                if (runtimeType == null)
                    return;

                EventInfo runtimeEvent = runtimeType.GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
                if (runtimeEvent == null)
                    return;

                if (add)
                    runtimeEvent.AddEventHandler(null, handler);
                else
                    runtimeEvent.RemoveEventHandler(null, handler);
            }
            catch
            {
            }
        }
    }
}
