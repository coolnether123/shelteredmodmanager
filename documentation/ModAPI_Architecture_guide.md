# ModAPI V1.2: Managers & Architecture Guide

This guide explains the "engine" behind **ModAPI V1.2**, detailing how mods are discovered, loaded, and managed at runtime. It covers both the internal systems (`PluginManager`, `PluginRunner`) and the high-level tools you use to build your mod (`ModManagerBase`).

---

## 1. Architectural Overview

The ModAPI follows a "Central Hub" architecture.

1.  **The Hub (`PluginManager`)**: A static singleton that discovery mods and coordinates their lifecycle.
2.  **The Heart (`PluginRunner`)**: A persistent `MonoBehaviour` on the `ModAPI.Loader` GameObject that provides Unity-specific ticks (`Update`) and scene event monitoring.
3.  **The Mod Manager (`ModManagerBase`)**: Your mod's personalized "brain." It is a `MonoBehaviour` that is automatically created and attached by the Hub.

---

## 2. Mod Lifecycle

Knowing the exact sequence of events is critical for stable modding.

### Step 1: Discovery & Assembly Load
At game startup, `PluginManager` scans the `/mods/` folder.
*   It reads `About.json` to verify the Mod ID and version.
*   It loads all `.dll` files in the `/Assemblies/` folder.
*   It resolves the **Load Order** (based on `loadorder.json` or discovery order).

### Step 2: Plugin Instantiation
The Hub iterates through all loaded classes. If a class implements `IModPlugin`, the Hub:
1.  Creates a new `GameObject` named `Mod-[YourModID]`.
2.  Creates a unique `IPluginContext` for that mod.
3.  Instantiates your plugin class.

### Step 3: Initialization Sequence
The Hub calls these methods in order:
1.  **`Initialize(context)`**: This is where basic setup happens. If you use `ModManagerBase`, this automatically binds your settings.
2.  **`Spine Auto-Load`**: If you use the Spine framework, your settings are loaded from disk now.
3.  **`Start(context)`**: Your mod is now fully "Online." You can safely communicate with other mods here.

---

## 3. ModManagerBase (The Recommended Way)

Instead of implementing `IModPlugin` manually, V1.2 ModAPI recommends inheriting from `ModManagerBase`. This adds "batteries included" functionality to your mod.

### Automated Services
By inheriting from `ModManagerBase`, you get immediate (protected) access to:
*   **`Log`**: Automatically prefixed with your Mod ID.
*   **`SaveSystem`**: For per-save data persistence.
*   **`Settings`**: For accessing and binding configuration.
*   **`Context`**: Your full link to the API.

### Automatic Settings & Persistence (v1.2)
If you define fields with `ModAttributes` or `[ModPersistentData]`, `ModManagerBase` automatically works its magic.

1.  **Settings (`[ModConfiguration]`)**: Scans for your configuration class, instantiates it, and links it to `this.Config`.
2.  **Persistence (`[ModPersistentData]`)**: Scans for data classes, registers them with `SaveSystem`, and injects instances into your plugin.
3.  **UI Generation**: Automatically creates the Spine UI menu.

```csharp
public class MyCoolMod : ModManagerBase<MyConfig> 
{
    // Auto-Injected
    public MySaveData Data;
    
    public override void Initialize(IPluginContext ctx) 
    {
        base.Initialize(ctx); // Settings & Data are now bound!
    }
}
```

---

## 4. PluginRunner (The Heartbeat)

The `PluginRunner` is an internal component, but its role is vital.

*   **Ticking**: It calls `IModUpdate.Update()` on all registered mods during the Unity `Update` loop.
*   **Scene Events**: It monitors Unity's `SceneManager` and broadcasts `OnSceneLoaded` and `OnSceneUnloaded` events to compatible mods.
*   **Coroutine Host**: Because the ModAPI needs to run coroutines even if your mod's script is disabled, `PluginRunner` acts as the global host for `StartCoroutine`.

---

## 5. Directory Structure Reference

The Manager system expects a specific layout to function correctly:

```
/mods/[YourModID]/
  ├── About/
  │    └── About.json         # Metadata (ID, Name, Version)
  ├── Assemblies/
  │    └── YourMod.dll        # Your compiled code
  ├── Assets/
  │    └── (Icon, Textures)   # Loaded via AssetLoader
  └── Config/
       └── (Auto-generated)   # Persistent settings files
```

---
**First Edition (V1) - Documentation for ModAPI V1.2.0**

---

## 6. Save System V1.2: Robust Discovery & Safe Shutdown

The ModAPI V1.2 significantly upgrades the existing Custom Slot system capabilities, focusing on reliability and stability during the critical "Save & Exit" phase.

### Robust Discovery ("Drag and Drop")
Unlike V1.1, which relied on a central manifest file, V1.2 uses **directory-based discovery**. You can now drag and drop save folders (e.g., `Slot_5`) directly into the saves directory, and the game will automatically detect and parse them. This requires the system to inspect save files directly to read metadata (Days, Family Name).

### Safe Shutdown (Regex Parsing)
Performing XML parsing via Unity's internal serialization during the **"Save & Exit"** sequence is dangerous because Unity objects are being destroyed simultaneously, leading to `Access Violation` crashes.

To solve this while maintaining full metadata accuracy, we use **Safe Regex Parsing**:
1.  **Unity Independence**: Instead of using `new SaveData(bytes)`, which triggers Unity's serialization engine, we use high-performance **Regular Expressions** to extract tags directly from the XML string.
2.  **Shutdown Safety**: Because Regex is an atomic string operation, it can be called safely even when Unity's internal managers are offline.
3.  **Always Up-to-Date**: Unlike the experimental "Fast Save" (v1.2.0-beta), which skipped metadata during exit, the final V1.2 system performs a full, high-quality save every time. 
    *   **Result**: Your `manifest.json` and in-game UI always show the correct Family Name and Days Survived immediately, with zero risk of crashing on exit.
