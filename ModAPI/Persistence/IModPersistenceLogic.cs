using ModAPI.Core;

namespace ModAPI.Persistence
{
    /// <summary>
    /// Optional lifecycle hook for mod objects that mirror save data into runtime managers.
    /// Use this when simple JSON registration is not enough and load/save needs explicit coordination.
    /// </summary>
    public interface IModPersistenceLogic
    {
        /// <summary>
        /// Called immediately after data is deserialized from the save file.
        /// Use this to push data INTO your runtime managers.
        /// </summary>
        void OnLoaded(IModSaveContext context);

        /// <summary>
        /// Called immediately before data is serialized to the save file.
        /// Use this to pull data FROM your runtime managers.
        /// </summary>
        void OnSaving(IModSaveContext context);
    }

    /// <summary>
    /// Optional complete persistence lifecycle for registered data that needs preparation,
    /// restoration, or validation in addition to JSON storage.
    /// </summary>
    /// <remarks>
    /// This is additive to <see cref="IModPersistenceLogic"/>. Restore and validation run
    /// once per active save context for data that was loaded, migrated, or restored to its
    /// registered default state. If an object implements both interfaces, both contracts run.
    /// </remarks>
    public interface IModPersistenceLifecycle
    {
        /// <summary>
        /// Called immediately before registered data is serialized.
        /// Use this to copy runtime state into the registered data object.
        /// </summary>
        void PrepareForSave(IModSaveContext context);

        /// <summary>
        /// Called after data is loaded, migrated, or reset to its registered defaults.
        /// Use this to apply registered data to runtime state.
        /// </summary>
        void RestoreAfterLoad(IModSaveContext context);

        /// <summary>
        /// Called after <see cref="RestoreAfterLoad"/> completes.
        /// Return false with a diagnostic message when restored state is not usable.
        /// </summary>
        bool ValidateAfterLoad(IModSaveContext context, out string diagnosticMessage);
    }
}
