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
}
