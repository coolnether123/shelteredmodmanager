using UnityEngine;
using System;
using System.Collections;
using ModAPI.Spine;
using ModAPI.Actors;

namespace ModAPI.Core
{
    /// <summary>
    /// Required plugin contract for mods loaded by ModAPI.
    /// Keep setup that only needs ModAPI services in <see cref="Initialize"/> and defer Unity scene work to <see cref="Start"/>.
    /// </summary>
    public interface IModPlugin
    {
        /// <summary>
        /// Called once during loader bootstrap before gameplay systems are considered live.
        /// </summary>
        void Initialize(IPluginContext ctx);

        /// <summary>
        /// Called after <see cref="Initialize"/> when Unity context is ready for runtime work.
        /// </summary>
        void Start(IPluginContext ctx);
    }

    /// <summary>
    /// Optional per-frame update callback for mods that need Unity main-thread polling.
    /// Prefer events or explicit scheduling when work does not need to run every frame.
    /// </summary>
    public interface IModUpdate
    {
        /// <summary>
        /// Called every frame by <see cref="PluginRunner"/>.
        /// </summary>
        void Update();
    }

    /// <summary>
    /// Optional shutdown callback for cleanup during app quit or loader teardown.
    /// </summary>
    public interface IModShutdown
    {
        /// <summary>
        /// Called when the loader is shutting down plugins.
        /// </summary>
        void Shutdown();
    }

    /// <summary>
    /// Optional scene lifecycle callbacks raised through ModAPI's Unity compatibility layer.
    /// Use these for scene object discovery instead of assuming one Unity version's event API.
    /// </summary>
    public interface IModSceneEvents
    {
        /// <summary>Called when a scene is considered loaded by runtime compatibility hooks.</summary>
        void OnSceneLoaded(string sceneName);
        /// <summary>Called when a scene is considered unloaded by runtime compatibility hooks.</summary>
        void OnSceneUnloaded(string sceneName);
    }

    /// <summary>
    /// Optional game session lifecycle callbacks for save/world-level transitions.
    /// These run after plugin bootstrap and before mods should assume a long-lived world session is stable.
    /// </summary>
    public interface IModSessionEvents
    {
        /// <summary>Called when a game session starts (new game or load).</summary>
        void OnSessionStarted();
        /// <summary>Called when a new game world is initialized.</summary>
        void OnNewGame();
    }

    /// <summary>
    /// Runtime services, paths, and plugin metadata provided to each loaded mod.
    /// Treat Unity objects exposed here as main-thread only, and prefer the typed services over global lookups.
    /// </summary>
    public interface IPluginContext
    {
        /// <summary>Persistent ModAPI root GameObject that hosts shared runtime components.</summary>
        GameObject LoaderRoot { get; }
        /// <summary>Per-plugin root GameObject for components owned by this mod.</summary>
        GameObject PluginRoot { get; }
        /// <summary>Descriptor for the loaded mod that owns this context.</summary>
        ModEntry Mod { get; }
        /// <summary>Settings provider registered by this mod, or null when the mod has none.</summary>
        ISettingsProvider Settings { get; }
        /// <summary>Plugin-scoped logger that prefixes messages with the owning mod.</summary>
        IModLogger Log { get; }
        /// <summary>Host-neutral helper for common game-state reads.</summary>
        IGameHelper Game { get; }
        /// <summary>Shared actor system for identity, components, and actor events.</summary>
        IActorSystem Actors { get; }
        /// <summary>Absolute path to the game installation root.</summary>
        string GameRoot { get; }
        /// <summary>Absolute path to the root mods directory.</summary>
        string ModsRoot { get; }
        /// <summary>True when the runtime is using the newer Unity scene APIs.</summary>
        bool IsModernUnity { get; }
        /// <summary>Per-mod save data API for the active save slot.</summary>
        ISaveSystem SaveSystem { get; }

        /// <summary>
        /// Queues an action for next frame on the main Unity thread.
        /// </summary>
        void RunNextFrame(Action action);
        /// <summary>
        /// Starts a coroutine on the persistent plugin runner.
        /// Use this when a mod needs Unity main-thread waits without adding its own host component.
        /// </summary>
        Coroutine StartCoroutine(IEnumerator routine);

        /// <summary>
        /// Finds a UI panel by name or path (for example, "UI root/ExpeditionMainPanelNew").
        /// </summary>
        GameObject FindPanel(string nameOrPath);

        /// <summary>
        /// Gets or adds a component of type <typeparamref name="T"/> on the target panel.
        /// </summary>
        T AddComponentToPanel<T>(string nameOrPath) where T : Component;
    }

    /// <summary>
    /// Per-mod save data persistence for the active save slot.
    /// Register data during plugin initialization so it can be loaded when saves become active.
    /// </summary>
    public interface ISaveSystem
    {
        /// <summary>
        /// Gets the absolute path to the active save folder (e.g., .../Saves/Standard/Slot_8).
        /// Returns null if no save is currently loaded.
        /// </summary>
        string GetCurrentSlotPath();

        /// <summary>
        /// Gets the human-readable slot index (e.g., 8). Returns -1 if no save is loaded.
        /// </summary>
        int ActiveSlotIndex { get; }

        /// <summary>
        /// Registers a data object to be automatically saved/loaded in the active slot's folder.
        /// The data is saved as JSON in 'mods_data.json' within the slot folder.
        /// Call this during Initialize().
        /// </summary>
        /// <param name="migrationCallback">Optional callback invoked if no data is found for this key (e.g., to load from legacy path).</param>
        void RegisterModData<T>(string key, T data, Action<T> migrationCallback = null) where T : class;
    }

    /// <summary>
    /// Plugin-scoped logger abstraction.
    /// Use this instead of direct <see cref="MMLog"/> calls when the message belongs to a specific mod.
    /// </summary>
    public interface IModLogger
    {
        /// <summary>True when debug messages for this mod should be emitted.</summary>
        bool IsDebugEnabled { get; }
        /// <summary>Writes diagnostic detail intended for development and troubleshooting.</summary>
        void Debug(string message);
        /// <summary>Writes normal informational output.</summary>
        void Info(string message);
        /// <summary>Writes a recoverable problem or degraded behavior.</summary>
        void Warn(string message);
        /// <summary>Writes a failure that prevented the requested operation from completing.</summary>
        void Error(string message);
    }
}
