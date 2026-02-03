using ModAPI.Saves;

namespace ModAPI.Persistence
{
    public interface IModPersistenceLogic
    {
        /// <summary>
        /// Called immediately after data is deserialized from the save file.
        /// Use this to push data INTO your runtime managers.
        /// </summary>
        void OnLoaded(SaveEntry entry);

        /// <summary>
        /// Called immediately before data is serialized to the save file.
        /// Use this to pull data FROM your runtime managers.
        /// </summary>
        void OnSaving(SaveEntry entry);
    }
}
