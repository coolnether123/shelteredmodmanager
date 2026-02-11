using UnityEngine;
using System;
using System.Collections;
using ModAPI.Spine;

namespace ModAPI.Core
{
    /// <summary>
    /// Required plugin contract for mods loaded by ModAPI.
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
    /// Optional per-frame update callback.
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
    /// Optional scene lifecycle callbacks.
    /// </summary>
    public interface IModSceneEvents
    {
        /// <summary>Called when a scene is considered loaded by runtime compatibility hooks.</summary>
        void OnSceneLoaded(string sceneName);
        /// <summary>Called when a scene is considered unloaded by runtime compatibility hooks.</summary>
        void OnSceneUnloaded(string sceneName);
    }

    /// <summary>
    /// Optional game session lifecycle callbacks.
    /// </summary>
    public interface IModSessionEvents
    {
        /// <summary>Called when a game session starts (new game or load).</summary>
        void OnSessionStarted();
        /// <summary>Called when a new game world is initialized.</summary>
        void OnNewGame();
    }

    /// <summary>
    /// Context object provided to each plugin containing runtime services and paths.
    /// </summary>
    public interface IPluginContext
    {
        GameObject LoaderRoot { get; }
        GameObject PluginRoot { get; }
        ModEntry Mod { get; }
        ISettingsProvider Settings { get; }
        IModLogger Log { get; }
        IGameHelper Game { get; }
        string GameRoot { get; }
        string ModsRoot { get; }
        bool IsModernUnity { get; }
        ISaveSystem SaveSystem { get; }

        /// <summary>
        /// Queues an action for next frame on the main Unity thread.
        /// </summary>
        void RunNextFrame(Action action);
        Coroutine StartCoroutine(IEnumerator routine);

        /// <summary>
        /// Finds a UI panel by name or path (for example, "UIRoot/ExpeditionMainPanelNew").
        /// </summary>
        GameObject FindPanel(string nameOrPath);

        /// <summary>
        /// Gets or adds a component of type <typeparamref name="T"/> on the target panel.
        /// </summary>
        T AddComponentToPanel<T>(string nameOrPath) where T : Component;
    }

    /// <summary>
    /// High-level helpers for commonly accessed game state (v1.0.1).
    /// Addresses the "Unified Home Storage" and "Game Logic" feedback.
    /// </summary>
    public interface IGameHelper
    {
        /// <summary>
        /// Get the total count of an item across all shelter storage managers
        /// (Inventory, Food, Water, Entertainment).
        /// </summary>
        int GetTotalOwned(string itemId);

        /// <summary>
        /// Get the total count of an item in the primary item inventory.
        /// </summary>
        int GetInventoryCount(string itemId);

        /// <summary>
        /// Try to find a player by their character ID.
        /// </summary>
        FamilyMember FindMember(string characterId);
    }

    /// <summary>
    /// Per-mod save data persistence (v1.1.0).
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
    /// Logger abstraction so plugins do not depend on MMLog static calls directly.
    /// </summary>
    public interface IModLogger
    {
        bool IsDebugEnabled { get; }
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
