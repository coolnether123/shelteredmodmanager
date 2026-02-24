using ModAPI.Core;
using ModAPI.Events;

namespace ShelteredAPI.Core
{
    /// <summary>
    /// Specialized base class for Sheltered mods with first-class game lifecycle hooks.
    /// </summary>
    public abstract class ShelteredModManagerBase : ModManagerBase
    {
        public override void Initialize(IPluginContext context)
        {
            base.Initialize(context);

            Events.Bind(
                delegate { GameEvents.OnBeforeSave += HandleBeforeSave; },
                delegate { GameEvents.OnBeforeSave -= HandleBeforeSave; });
            Events.Bind(
                delegate { GameEvents.OnAfterLoad += HandleAfterLoad; },
                delegate { GameEvents.OnAfterLoad -= HandleAfterLoad; });
            Events.Bind(
                delegate { GameEvents.OnSessionStarted += HandleSessionStarted; },
                delegate { GameEvents.OnSessionStarted -= HandleSessionStarted; });
            Events.Bind(
                delegate { GameEvents.OnNewGame += HandleNewGame; },
                delegate { GameEvents.OnNewGame -= HandleNewGame; });
        }

        private void HandleBeforeSave(SaveData data) { OnBeforeSave(data); }
        private void HandleAfterLoad(SaveData data) { OnAfterLoad(data); }
        private void HandleSessionStarted() { OnSessionStarted(); }
        private void HandleNewGame() { OnNewGame(); }

        /// <summary>
        /// Called before save data is serialized.
        /// </summary>
        protected virtual void OnBeforeSave(SaveData data) { }

        /// <summary>
        /// Called after save data has been loaded and core managers are ready.
        /// </summary>
        protected virtual void OnAfterLoad(SaveData data) { }

        /// <summary>
        /// Called when a gameplay session starts (new game or load).
        /// </summary>
        protected virtual void OnSessionStarted() { }

        /// <summary>
        /// Called when a new game is started.
        /// </summary>
        protected virtual void OnNewGame() { }
    }
}
