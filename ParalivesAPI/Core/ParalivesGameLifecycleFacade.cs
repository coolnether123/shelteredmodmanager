using System;
using ModAPI.Core;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesGameLifecycleFacade : IGameLifecycleSource
    {
        public const string RegistryId = "GameRuntime.Paralives.SaveLifecycle";

        public static readonly ParalivesGameLifecycleFacade Current = new ParalivesGameLifecycleFacade();

        private readonly object _sync = new object();
        private ulong _pendingLoadSaveGuid;

        private event Action<object> NeutralBeforeSave;
        private event Action<object> NeutralBeforeLoadSceneContents;
        private event Action<object> NeutralAfterLoad;
        private event Action NeutralSessionStarted;
        private event Action NeutralNewGame;

        public event Action<ParalivesSaveLoadingEvent> SaveLoading;
        public event Action<ParalivesSaveLoadedEvent> SaveLoaded;
        public event Action<ParalivesSaveSavingEvent> SaveSaving;
        public event Action<ParalivesSaveSavedEvent> SaveSaved;
        public event Action<ParalivesSaveUnloadingEvent> SaveUnloading;

        private ParalivesGameLifecycleFacade()
        {
        }

        public bool IsGameLoaded
        {
            get
            {
                try
                {
                    return global::SavedGameManager.Instance != null
                        && global::SavedGameManager.Instance.IsGameLoaded;
                }
                catch
                {
                    return false;
                }
            }
        }

        public ulong CurrentSaveGuid
        {
            get { return GetCurrentSaveGuid(); }
        }

        public string CurrentSaveKey
        {
            get { return ToSaveKey(CurrentSaveGuid); }
        }

        public ulong CurrentTownGuid
        {
            get
            {
                SaveIdentity identity = CaptureIdentity(0UL);
                return identity.CurrentTownGuid;
            }
        }

        public ulong CurrentHouseholdGuid
        {
            get
            {
                SaveIdentity identity = CaptureIdentity(0UL);
                return identity.CurrentHouseholdGuid;
            }
        }

        event Action<object> IGameLifecycleSource.BeforeSave
        {
            add { NeutralBeforeSave += value; }
            remove { NeutralBeforeSave -= value; }
        }

        event Action<object> IGameLifecycleSource.BeforeLoadSceneContents
        {
            add { NeutralBeforeLoadSceneContents += value; }
            remove { NeutralBeforeLoadSceneContents -= value; }
        }

        event Action<object> IGameLifecycleSource.AfterLoad
        {
            add { NeutralAfterLoad += value; }
            remove { NeutralAfterLoad -= value; }
        }

        event Action IGameLifecycleSource.SessionStarted
        {
            add { NeutralSessionStarted += value; }
            remove { NeutralSessionStarted -= value; }
        }

        event Action IGameLifecycleSource.NewGame
        {
            add { NeutralNewGame += value; }
            remove { NeutralNewGame -= value; }
        }

        internal string GetStorageSaveKey()
        {
            ulong saveGuid = GetCurrentSaveGuid();
            if (saveGuid != 0UL)
                return ToSaveKey(saveGuid);

            lock (_sync)
            {
                return ToSaveKey(_pendingLoadSaveGuid);
            }
        }

        internal void PublishSaveLoading(ulong saveGuid, bool showTutorial, bool isNewGame)
        {
            if (saveGuid == 0UL)
                return;

            lock (_sync)
            {
                _pendingLoadSaveGuid = saveGuid;
            }

            ParalivesSaveLoadingEvent evt = new ParalivesSaveLoadingEvent
            {
                SaveGuid = saveGuid,
                SaveKey = ToSaveKey(saveGuid),
                ShowTutorial = showTutorial,
                IsNewGame = isNewGame,
                TimestampUtcTicks = DateTime.UtcNow.Ticks
            };

            Publish(SaveLoading, evt);
            Publish(NeutralBeforeLoadSceneContents, evt);
        }

        internal void PublishSaveLoaded(ulong saveGuid, bool isNewGame)
        {
            SaveIdentity identity = CaptureIdentity(saveGuid);
            lock (_sync)
            {
                _pendingLoadSaveGuid = 0UL;
            }

            ParalivesSaveLoadedEvent evt = new ParalivesSaveLoadedEvent
            {
                SaveGuid = identity.SaveGuid,
                SaveKey = identity.SaveKey,
                CurrentTownGuid = identity.CurrentTownGuid,
                CurrentHouseholdGuid = identity.CurrentHouseholdGuid,
                IsNewGame = isNewGame,
                ParaTimeMinutes = GetParaTimeMinutes(),
                TimestampUtcTicks = DateTime.UtcNow.Ticks
            };

            Publish(SaveLoaded, evt);
            Publish(NeutralAfterLoad, evt);
            Publish(NeutralSessionStarted);
            if (isNewGame)
                Publish(NeutralNewGame);
        }

        internal void PublishSaveSaving(
            bool fromAutoSave,
            bool copySaveAsDefaultTown,
            bool shouldQuitAfterwards,
            bool shouldMainMenuAfterwards)
        {
            SaveIdentity identity = CaptureIdentity(0UL);
            if (identity.SaveGuid == 0UL)
                return;

            ParalivesSaveSavingEvent evt = new ParalivesSaveSavingEvent
            {
                SaveGuid = identity.SaveGuid,
                SaveKey = identity.SaveKey,
                CurrentTownGuid = identity.CurrentTownGuid,
                CurrentHouseholdGuid = identity.CurrentHouseholdGuid,
                FromAutoSave = fromAutoSave,
                CopySaveAsDefaultTown = copySaveAsDefaultTown,
                ShouldQuitAfterwards = shouldQuitAfterwards,
                ShouldMainMenuAfterwards = shouldMainMenuAfterwards,
                ParaTimeMinutes = GetParaTimeMinutes(),
                TimestampUtcTicks = DateTime.UtcNow.Ticks
            };

            Publish(SaveSaving, evt);
            Publish(NeutralBeforeSave, evt);
        }

        internal void PublishSaveSaved(
            bool fromAutoSave,
            bool copySaveAsDefaultTown,
            bool shouldQuitAfterwards,
            bool shouldMainMenuAfterwards)
        {
            SaveIdentity identity = CaptureIdentity(0UL);
            if (identity.SaveGuid == 0UL)
                return;

            ParalivesSaveSavedEvent evt = new ParalivesSaveSavedEvent
            {
                SaveGuid = identity.SaveGuid,
                SaveKey = identity.SaveKey,
                CurrentTownGuid = identity.CurrentTownGuid,
                CurrentHouseholdGuid = identity.CurrentHouseholdGuid,
                FromAutoSave = fromAutoSave,
                CopySaveAsDefaultTown = copySaveAsDefaultTown,
                ShouldQuitAfterwards = shouldQuitAfterwards,
                ShouldMainMenuAfterwards = shouldMainMenuAfterwards,
                ParaTimeMinutes = GetParaTimeMinutes(),
                TimestampUtcTicks = DateTime.UtcNow.Ticks
            };

            Publish(SaveSaved, evt);
        }

        internal void PublishSaveUnloading()
        {
            SaveIdentity identity = CaptureIdentity(0UL);
            if (identity.SaveGuid == 0UL)
                return;

            ParalivesSaveUnloadingEvent evt = new ParalivesSaveUnloadingEvent
            {
                SaveGuid = identity.SaveGuid,
                SaveKey = identity.SaveKey,
                CurrentTownGuid = identity.CurrentTownGuid,
                CurrentHouseholdGuid = identity.CurrentHouseholdGuid,
                ParaTimeMinutes = GetParaTimeMinutes(),
                TimestampUtcTicks = DateTime.UtcNow.Ticks
            };

            Publish(SaveUnloading, evt);
        }

        internal static string ToSaveKey(ulong saveGuid)
        {
            return saveGuid == 0UL ? string.Empty : saveGuid.ToString();
        }

        private static ulong GetCurrentSaveGuid()
        {
            try
            {
                return global::SavedGameManager.Instance != null
                    ? global::SavedGameManager.Instance.CurrentSavedGameGUID
                    : 0UL;
            }
            catch
            {
                return 0UL;
            }
        }

        private static SaveIdentity CaptureIdentity(ulong fallbackSaveGuid)
        {
            SaveIdentity identity = new SaveIdentity();
            identity.SaveGuid = GetCurrentSaveGuid();
            if (identity.SaveGuid == 0UL)
                identity.SaveGuid = fallbackSaveGuid;
            identity.SaveKey = ToSaveKey(identity.SaveGuid);

            try
            {
                global::AssetSavedGame currentSave = global::SavedGameManager.Instance != null
                    ? global::SavedGameManager.Instance.CurrentSavedGame
                    : null;
                if (currentSave != null && currentSave.Data != null)
                {
                    identity.CurrentTownGuid = currentSave.Data.CurrentTownGUID;
                    identity.CurrentHouseholdGuid = currentSave.Data.CurrentHouseholdGUID;
                }
            }
            catch
            {
            }

            return identity;
        }

        private static float GetParaTimeMinutes()
        {
            try
            {
                return global::ParaTime.TotalMinutes;
            }
            catch
            {
                return 0f;
            }
        }

        private static void Publish<T>(Action<T> handler, T evt) where T : class
        {
            if (handler == null || evt == null)
                return;

            try
            {
                handler(evt);
            }
            catch
            {
            }
        }

        private static void Publish(Action<object> handler, object evt)
        {
            if (handler == null)
                return;

            try
            {
                handler(evt);
            }
            catch
            {
            }
        }

        private static void Publish(Action handler)
        {
            if (handler == null)
                return;

            try
            {
                handler();
            }
            catch
            {
            }
        }

        private struct SaveIdentity
        {
            internal ulong SaveGuid;
            internal string SaveKey;
            internal ulong CurrentTownGuid;
            internal ulong CurrentHouseholdGuid;
        }
    }
}
