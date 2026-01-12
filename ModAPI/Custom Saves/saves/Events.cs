using System;
using ModAPI.Core;

namespace ModAPI.Saves
{
    public static class Events
    {
        public static event SaveEvent OnBeforeSave;
        public static event SaveEvent OnAfterSave;
        public static event LoadEvent OnBeforeLoad;
        public static event LoadEvent OnAfterLoad;
        public static event PageChangedEvent OnPageChanged;
        public static event ReservationChangedEvent OnReservationChanged;

        internal static void RaiseBeforeSave(SaveEntry e) { MMLog.WriteDebug($"Event:BeforeSave id={e?.id}"); var h = OnBeforeSave; if (h != null) h(e); }
        internal static void RaiseAfterSave(SaveEntry e) { MMLog.WriteDebug($"Event:AfterSave id={e?.id}"); var h = OnAfterSave; if (h != null) h(e); }
        internal static void RaiseBeforeLoad(SaveEntry e) { MMLog.WriteDebug($"Event:BeforeLoad id={e?.id}"); var h = OnBeforeLoad; if (h != null) h(e); }
        internal static void RaiseAfterLoad(SaveEntry e) { MMLog.WriteDebug($"Event:AfterLoad id={e?.id}"); var h = OnAfterLoad; if (h != null) h(e); }
        internal static void RaisePageChanged(int p) { MMLog.WriteDebug($"Event:PageChanged {p}"); var h = OnPageChanged; if (h != null) h(p); }
        internal static void RaiseReservationChanged(int slot, SlotReservation r) { MMLog.WriteDebug($"Event:ReservationChanged slot={slot} usage={r?.usage}"); var h = OnReservationChanged; if (h != null) h(slot, r); }
    }
}
