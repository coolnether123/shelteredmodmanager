using UnityEngine;
using System;
using System.Collections;

namespace ModAPI.Core
{
    /**
     * Author: benjaminfoo
     * Maintainer: coolnether123
     * See: https://github.com/benjaminfoo/shelteredmodmanager
     * 
     * (Coolnether123 WIP): Replaced the previous minimal IPlugin (initialize/start with GameObject)
     * with a context-first API. Keeping the barrier to entry low while giving plugins direct,
     * typed access to their mod metadata, paths, settings, and a per-plugin parent GameObject.
     * 
     */

    // Core plugin interface (required)
    public interface IModPlugin
    {
        void Initialize(IPluginContext ctx);   // called once before Start
        void Start(IPluginContext ctx);        // called after Initialize when Unity context is ready
    }

    // Optional per-frame update (opt-in)
    public interface IModUpdate
    {
        void Update();                         // called every frame by the loader's PluginRunner
    }

    // Optional shutdown/cleanup (opt-in)
    public interface IModShutdown
    {
        void Shutdown();                       // called when the loader tears down or disables the mod
    }

    // Optional scene events (opt-in)
    public interface IModSceneEvents
    {
        void OnSceneLoaded(string sceneName);
        void OnSceneUnloaded(string sceneName);
    }

    // Optional session events (opt-in) (v1.0.1) - Signals the lifecycle of a play session
    public interface IModSessionEvents
    {
        void OnSessionStarted();               // called when a game session starts (New Game or Load)
        void OnNewGame();                      // called specifically when a New Game is initialized
    }

    /**
     * Context passed into plugins. This gives access to:
     *  - LoaderRoot: a global host GameObject created by the loader
     *  - PluginRoot: a per-plugin parent GameObject you can attach behaviours under
     *  - Mod:        identity + paths (populated from About.json / discovery)
     *  - Settings:   typed helpers for Config/default.json + Config/user.json
     *  - Log:        a logger that prefixes with the mod id for easier diagnostics
     *  - GameRoot/ModsRoot: convenience paths derived from the running game
     *  - Game:       unified helper for commonly accessed game state (storage, objects)
     *  - Scheduling helpers: RunNextFrame / StartCoroutine for main-thread work
     *  - UI helpers: FindPanel("UIRoot/SomePanel") and AddComponentToPanel<T>(name)
     *
     * Notes:
     *  - Prefer storing per-UI state in your own MonoBehaviour attached via
     *    AddComponentToPanel<MyLogic>("SomePanel"), rather than static fields in patches.
     *  - Read configuration up front, e.g. var maxParty = Settings.GetInt("maxPartySize", 4);
     */
    public interface IPluginContext
    {
        GameObject LoaderRoot { get; }         // global host object
        GameObject PluginRoot { get; }         // per-plugin parent object (safe cleanup)
        ModEntry Mod { get; }                  // discovered mod entry (About, paths, id)
        ModSettings Settings { get; }          // settings accessor bound to this mod
        IModLogger Log { get; }                // mod-prefixed logger
        IGameHelper Game { get; }              // unified game state helper
        string GameRoot { get; }               // e.g., <Sheltered install dir>
        string ModsRoot { get; }               // e.g., <GameRoot>/mods
        bool IsModernUnity { get; }           // true if running on Unity 5.4+ (e.g., EGS version)
        ISaveSystem SaveSystem { get; }        // per-mod save data persistence

        void RunNextFrame(Action action);      // queues an action for next frame
        Coroutine StartCoroutine(IEnumerator routine);

        // --- UI helpers (new) --------------------------------------------------
        // Finds a UI panel (GameObject) by name or path (e.g., "UIRoot/ExpeditionMainPanelNew").
        // Returns null if not found. Prefer calling from Start() or OnSceneLoaded().
        GameObject FindPanel(string nameOrPath);

        // Adds (or gets) a custom MonoBehaviour component on an existing panel in the scene.
        // Example: ctx.AddComponentToPanel<MyPartySetupLogic>("ExpeditionMainPanelNew");
        // Returns the component instance attached to that panel (or null if panel not found).
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

    // Simple logger abstraction so plugins don't depend directly on MMLog static
    public interface IModLogger
    {
        bool IsDebugEnabled { get; }
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
