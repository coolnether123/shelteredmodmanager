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

        internal static void RaiseBeforeSave(SaveEntry e) { var h = OnBeforeSave; if (h != null) h(e); }
        internal static void RaiseAfterSave(SaveEntry e) { var h = OnAfterSave; if (h != null) h(e); }
        internal static void RaiseBeforeLoad(SaveEntry e) { var h = OnBeforeLoad; if (h != null) h(e); }
        internal static void RaiseAfterLoad(SaveEntry e) { var h = OnAfterLoad; if (h != null) h(e); }
        internal static void RaisePageChanged(int p) { var h = OnPageChanged; if (h != null) h(p); }
        internal static void RaiseReservationChanged(int slot, SlotReservation r) { var h = OnReservationChanged; if (h != null) h(slot, r); }
    }
}
