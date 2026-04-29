namespace ShelteredAPI.Saves
{
    /// <summary>
    /// Stable mod-facing save lifecycle event facade.
    /// </summary>
    public static class ShelteredSaveEvents
    {
        public static event SaveEvent BeforeSave
        {
            add { Events.OnBeforeSave += value; }
            remove { Events.OnBeforeSave -= value; }
        }

        public static event SaveEvent AfterSave
        {
            add { Events.OnAfterSave += value; }
            remove { Events.OnAfterSave -= value; }
        }

        public static event LoadEvent BeforeLoad
        {
            add { Events.OnBeforeLoad += value; }
            remove { Events.OnBeforeLoad -= value; }
        }

        public static event LoadEvent AfterLoad
        {
            add { Events.OnAfterLoad += value; }
            remove { Events.OnAfterLoad -= value; }
        }

        public static event PageChangedEvent PageChanged
        {
            add { Events.OnPageChanged += value; }
            remove { Events.OnPageChanged -= value; }
        }

        public static event ReservationChangedEvent ReservationChanged
        {
            add { Events.OnReservationChanged += value; }
            remove { Events.OnReservationChanged -= value; }
        }
    }
}
