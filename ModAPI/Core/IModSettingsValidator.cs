namespace ModAPI.Core
{
    public interface IModSettingsValidator 
    {
        /// <summary>
        /// Called immediately after Loading. 
        /// SAFE: Math checks, null checks.
        /// UNSAFE: Accessing Game Managers (GameModeManager.Instance is null here).
        /// </summary>
        void Validate();        

        /// <summary>
        /// Called during OnSessionStarted().
        /// SAFE: Full Game State access.
        /// </summary>
        void ValidateRuntime(); 
    }
}
