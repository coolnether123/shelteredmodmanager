namespace ParalivesAPI.Core
{
    public sealed class ParalivesSaveLoadingEvent
    {
        public ulong SaveGuid { get; internal set; }

        public string SaveKey { get; internal set; }

        public bool ShowTutorial { get; internal set; }

        public bool IsNewGame { get; internal set; }

        public long TimestampUtcTicks { get; internal set; }
    }

    public sealed class ParalivesSaveLoadedEvent
    {
        public ulong SaveGuid { get; internal set; }

        public string SaveKey { get; internal set; }

        public ulong CurrentTownGuid { get; internal set; }

        public ulong CurrentHouseholdGuid { get; internal set; }

        public bool IsNewGame { get; internal set; }

        public float ParaTimeMinutes { get; internal set; }

        public long TimestampUtcTicks { get; internal set; }
    }

    public sealed class ParalivesSaveSavingEvent
    {
        public ulong SaveGuid { get; internal set; }

        public string SaveKey { get; internal set; }

        public ulong CurrentTownGuid { get; internal set; }

        public ulong CurrentHouseholdGuid { get; internal set; }

        public bool FromAutoSave { get; internal set; }

        public bool CopySaveAsDefaultTown { get; internal set; }

        public bool ShouldQuitAfterwards { get; internal set; }

        public bool ShouldMainMenuAfterwards { get; internal set; }

        public float ParaTimeMinutes { get; internal set; }

        public long TimestampUtcTicks { get; internal set; }
    }

    public sealed class ParalivesSaveSavedEvent
    {
        public ulong SaveGuid { get; internal set; }

        public string SaveKey { get; internal set; }

        public ulong CurrentTownGuid { get; internal set; }

        public ulong CurrentHouseholdGuid { get; internal set; }

        public bool FromAutoSave { get; internal set; }

        public bool CopySaveAsDefaultTown { get; internal set; }

        public bool ShouldQuitAfterwards { get; internal set; }

        public bool ShouldMainMenuAfterwards { get; internal set; }

        public float ParaTimeMinutes { get; internal set; }

        public long TimestampUtcTicks { get; internal set; }
    }

    public sealed class ParalivesSaveUnloadingEvent
    {
        public ulong SaveGuid { get; internal set; }

        public string SaveKey { get; internal set; }

        public ulong CurrentTownGuid { get; internal set; }

        public ulong CurrentHouseholdGuid { get; internal set; }

        public float ParaTimeMinutes { get; internal set; }

        public long TimestampUtcTicks { get; internal set; }
    }
}
