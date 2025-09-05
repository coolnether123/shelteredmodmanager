using UnityEngine;
using System;
using System.Collections;

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

/**
 * Context passed into plugins. This gives access to:
 *  - LoaderRoot: a global host GameObject created by the loader
 *  - PluginRoot: a per-plugin parent GameObject you can attach behaviours under
 *  - Mod:        identity + paths (populated from About.json / discovery)
 *  - Settings:   typed helpers for Config/default.json + Config/user.json
 *  - Log:        a logger that prefixes with the mod id for easier diagnostics
 *  - GameRoot/ModsRoot: convenience paths derived from the running game
 *  - Scheduling helpers: RunNextFrame / StartCoroutine for main-thread work
 */
public interface IPluginContext
{
    GameObject LoaderRoot { get; }         // global host object
    GameObject PluginRoot { get; }         // per-plugin parent object (safe cleanup)
    ModEntry Mod { get; }                  // discovered mod entry (About, paths, id)
    ModSettings Settings { get; }          // settings accessor bound to this mod
    IModLogger Log { get; }                // mod-prefixed logger
    string GameRoot { get; }               // e.g., <Sheltered install dir>
    string ModsRoot { get; }               // e.g., <GameRoot>/mods

    void RunNextFrame(Action action);      // queues an action for next frame
    Coroutine StartCoroutine(IEnumerator routine);
}

// Simple logger abstraction so plugins don't depend directly on MMLog static
public interface IModLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}
