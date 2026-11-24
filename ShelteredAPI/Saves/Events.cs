using System;
using ModAPI.Core;

namespace ShelteredAPI.Saves
{
    internal static class Events
    {
        public static event SaveEvent OnBeforeSave;
        public static event SaveEvent OnAfterSave;
        public static event LoadEvent OnBeforeLoad;
        public static event LoadEvent OnAfterLoad;
        public static event PageChangedEvent OnPageChanged;
        public static event ReservationChangedEvent OnReservationChanged;

        internal static void RaiseBeforeSave(SaveEntry e) { SubscriberDispatch.Invoke(OnBeforeSave, "BeforeSave", e); }
        internal static void RaiseAfterSave(SaveEntry e) { SubscriberDispatch.Invoke(OnAfterSave, "AfterSave", e); }
        internal static void RaiseBeforeLoad(SaveEntry e) { SubscriberDispatch.Invoke(OnBeforeLoad, "BeforeLoad", e); }
        internal static void RaiseAfterLoad(SaveEntry e) { SubscriberDispatch.Invoke(OnAfterLoad, "AfterLoad", e); }
        internal static void RaisePageChanged(int p) { SubscriberDispatch.Invoke(OnPageChanged, "PageChanged", p); }
        internal static void RaiseReservationChanged(int slot, SlotReservation r) { SubscriberDispatch.Invoke(OnReservationChanged, "ReservationChanged", slot, r); }
    }
}
