namespace ModAPI.Core
{
    /// <summary>
    /// Optional validation hook for mod settings providers.
    /// Split pure configuration checks from runtime checks so mods do not touch game state before it exists.
    /// </summary>
    public interface IModSettingsValidator 
    {
        /// <summary>
        /// Called immediately after settings load.
        /// Keep this to math, range, and null checks; game-owned runtime singletons may not be ready.
        /// </summary>
        void Validate();        

        /// <summary>
        /// Called during session startup when game state is available.
        /// Use this for validation that depends on loaded saves, world state, or runtime services.
        /// </summary>
        void ValidateRuntime(); 
    }
}
