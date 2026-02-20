# ModAPI Project File Roles (v1.2.1)

This document outlines the role and purpose of each file within the `ModAPI` project, detailing their primary and secondary functions, and how they interconnect.

For exact callable signatures, use `documentation/API_Signatures_Reference.md`.

## Event System

### `ModEventBus.cs`
**Primary Role:** Provides a publish/subscribe event bus for inter-mod communication. Allows mods to send and receive custom typed events without direct coupling.
**Secondary Role:** Enables mods to react to events from other mods, creating an ecosystem of compatible and interoperable mods.
**Key Features:**
- Thread-safe pub/sub system with type safety
- Event-name based routing (use notation: "Author.Mod.EventName")
- Automatic error isolation (one mod's handler error won't crash others)
- Subscriber count tracking and diagnostics

**API Methods:**
```csharp
// Publish an event
ModEventBus.Publish<T>(string eventName, T data)

// Subscribe to an event
ModEventBus.Subscribe<T>(string eventName, Action<T> handler)

// Unsubscribe
ModEventBus.Unsubscribe<T>(string eventName, Action<T> handler)

// Utilities
bool HasSubscribers(string eventName)
int GetSubscriberCount(string eventName)
Dictionary<string, int> GetEventDiagnostics()
```

**Example Usage:**
```csharp
// Mod A publishes an event
public class QuestData { public string QuestId; public int Reward; }
ModEventBus.Publish("Author.Quests.QuestCompleted", new QuestData { QuestId = "first", Reward = 100 });

// Mod B subscribes to it
ModEventBus.Subscribe<QuestData>("Author.Quests.QuestCompleted", data => {
    MMLog.WriteInfo($"Quest {data.QuestId} complete! Reward: {data.Reward}");
});
```

**Interconnections:** Used by any mod that needs to communicate with other mods. Works alongside `ModAPIRegistry` for more structured API sharing.

---

### `ModAPIRegistry.cs`
**Primary Role:** Service discovery registry that allows mods to publish shared APIs that other mods can discover and consume.
**Secondary Role:** Enables structured mod-to-mod API sharing beyond simple events, allowing mods to expose complex functionality.
**Key Features:**
- Type-safe API registration and retrieval
- Provider tracking (which mod registered each API)
- Access control for unregistration
- Diagnostics for debugging API availability

**API Methods:**
```csharp
// Register an API
bool RegisterAPI<T>(string apiName, T implementation, string providerModId = null)

// Get an API
T GetAPI<T>(string apiName)
bool TryGetAPI<T>(string apiName, out T api)

// Check registration
bool IsAPIRegistered(string apiName)

// Utilities
List<string> GetRegisteredAPIs()
Dictionary<string, APIInfo> GetAPIDiagnostics()
```

**Example Usage:**
```csharp
// Mod A: Define and register an API
public interface ICraftingAPI 
{
    void RegisterRecipe(string id, ItemType result, ItemType[] ingredients);
}

internal class CraftingAPIImpl : ICraftingAPI { /* implementation */ }

// In Mod A's Start():
ModAPIRegistry.RegisterAPI<ICraftingAPI>("com.myname.CraftingAPI", new CraftingAPIImpl(), ctx.Mod.Id);

// Mod B: Consume the API
if (ModAPIRegistry.TryGetAPI<ICraftingAPI>("com.myname.CraftingAPI", out var api))
{
    api.RegisterRecipe("my_sword", ItemType.Sword, new[] { ItemType.Metal });
}
```

**Interconnections:** Works with `ModRegistry` for mod detection. Often used alongside `ModEventBus` for comprehensive mod interoperability.

---

### `UIEvents.cs`
**Primary Role:** Provides centralized events for UI panel lifecycle tracking (open, close, resume, pause).
**Secondary Role:** Eliminates the need for individual Harmony patches to track panel state changes, simplifying UI-reactive mod development.
**Key Features:**
- Panel lifecycle events (opened, closed, resumed, paused)
- Optional button click tracking
- Automatic event raising via Harmony patches in `UIPatches.cs`

**Events:**
```csharp
event Action<BasePanel> OnPanelOpened;   // Panel pushed onto stack
event Action<BasePanel> OnPanelClosed;   // Panel popped from stack
event Action<BasePanel> OnPanelResumed;  // Panel returns to top
event Action<BasePanel> OnPanelPaused;   // Another panel pushed above
event Action<GameObject, string> OnButtonClicked;  // Any button clicked (optional)
```

**Example Usage:**
```csharp
// React to crafting panel opening
UIEvents.OnPanelOpened += panel => {
    if (panel.GetType().Name == "CraftingPanel")
    {
        MMLog.WriteInfo("Crafting panel opened - showing helper UI");
        ShowCraftingGuide();
    }
};

// Auto-save when returning to main menu
UIEvents.OnPanelOpened += panel => {
    if (panel.GetType().Name == "MainMenuPanel")
    {
        SaveManager.instance.SaveToCurrentSlot(false);
    }
};
```

**Interconnections:** Patches are in `UIPatches.cs`. Works with `UIPanelManager` and `BasePanel` from the game.

---

### `ModRegistry.cs`
**Primary Role:** Maps assemblies to ModEntry objects for runtime mod identification and provides RimWorld-style mod detection utilities.
**Secondary Role:** Allows mods to check if other mods are loaded and adapt behavior accordingly.
**Key Features:**
- Automatic registration during mod discovery (no manual setup needed)
- Simple boolean checks for mod presence
- Full mod information retrieval
- List all loaded mods

**Mod Detection API:**
```csharp
// Check if a mod is loaded (simple!)
bool Find(string modId)

// Get mod information
ModEntry GetMod(string modId)
bool TryGetMod(string modId, out ModEntry entry)

// List all mods
List<string> GetLoadedModIds()
List<ModEntry> GetLoadedMods()
int GetLoadedModCount()
```

**Example Usage:**
```csharp
// Simple detection (RimWorld-style)
if (ModRegistry.Find("Other.Expansion"))
{
    ctx.Log.Info("Expansion detected - enabling advanced features");
    EnableExpandedContent();
}
else
{
    ctx.Log.Info("Running in standalone mode");
    LoadBasicContent();
}

// Get detailed info
ModEntry mod = ModRegistry.GetMod("com.other.bigmod");
if (mod != null)
{
    ctx.Log.Info($"Found: {mod.Name} v{mod.Version}");
    if (mod.Version.StartsWith("2."))
        UseV2Integration();
}

// List all loaded mods (debug)
foreach (string modId in ModRegistry.GetLoadedModIds())
{
    MMLog.WriteInfo($"Loaded: {modId}");
}
```

**How It Works (Automatic):**
1. Game starts -> `ModDiscovery` scans `/mods/` folder
2. Reads each mod's `About.json`
3. Automatically calls `ModRegistry.RegisterModById(entry)` (internal, hidden)
4. Now `ModRegistry.Find("mod.id")` works!

**No Manual Registration Required!** Just have an `About.json`:
```json
{
  "id": "MyName.MyMod",
  "name": "My Mod",
  "version": "1.0.0"
}
```

**Interconnections:** Used by `ModDiscovery.cs` for registration. Used by `PluginManager.cs` to resolve mod contexts. Frequently used by mods for conditional behavior.

---

## Core Files

### `ContextExtensions.cs`
**Primary Role:** Provides extension methods for `IPluginContext` objects.
**Secondary Role:** Enhances usability of context-driven calls by abstracting scheduling and scene-ready behavior.
**Interconnections:** Extends `IPluginContext` used by `PluginManager.cs` and `PluginRunner`.

### `ContextUIExtensions.cs`
**Primary Role:** Extends `IPluginContext` with UI-related helpers so mods can integrate into game UI safely.
**Secondary Role:** Facilitates the creation of in-game UI components by mods, ensuring they adhere to the game's UI framework.
**Interconnections:** Similar to `ContextExtensions.cs`, it extends `IPluginContext`. It relies on UI utility helpers and runtime panel lookup.

### `GameUtil.cs`
**Primary Role:** Contains a collection of static utility functions and helpers that abstract game-specific operations. This provides a consistent and safe way for mods to interact with various game mechanics without directly touching game internals.
**Secondary Role:** Acts as a central point for common game interactions, reducing code duplication across different mods and within the ModAPI itself.
**Interconnections:** Potentially used by `ContextExtensions.cs`, `ContextUIExtensions.cs`, and various mod implementations. It might also interact with `SceneUtil.cs` for scene-related game operations.

### `IPlugin.cs`
**Primary Role:** Defines the fundamental interface that all plugins (mods) must implement. This interface establishes a clear contract, outlining the essential methods and properties that the mod manager expects from any loaded mod.
**Secondary Role:** Enforces a standardized structure for mods, making them discoverable and manageable by `PluginManager.cs`.
**Interconnections:** Crucial for `PluginManager.cs` and `ModDiscovery.cs` to identify and interact with mods. `ModAbout.cs` likely provides data for properties defined in this interface.

### `LoadOrderResolver.cs`
**Primary Role:** Responsible for analyzing mod dependencies and determining the correct loading order of mods. This is critical to ensure that mods with dependencies are loaded after their required counterparts, preventing runtime errors.
**Secondary Role:** Helps maintain stability and compatibility within the modded game environment by resolving potential conflicts arising from incorrect loading sequences.
**Interconnections:** Works closely with `ModRegistry.cs` to get information about registered mods and their dependencies. `PluginManager.cs` uses the resolved order to load mods.

### `LoggerExtensions.cs`
**Primary Role:** Provides extension methods for logging, offering a standardized and convenient way for mods to output diagnostic information, warnings, and errors to the console or log file.
**Secondary Role:** Simplifies logging for mod developers, ensuring consistent log formatting and routing.
**Interconnections:** Extends the logging capabilities provided by `MMLog.cs`.

### `MMLog.cs`
**Primary Role:** Core logging engine for ModAPI and mods. Now features high-performance automatic source attribution.
**Secondary Role:** Centralizes error reporting and provides developer-facing diagnostic tools (timers, once-warnings).
**Key Features (v1.2.1):**
- **Automatic Source Detection**: Uses optimized stack-trace analysis to identify the calling class or mod ID.
- **Assembly Caching**: Caches the result of assembly-to-mod lookups to minimize `StackTrace` overhead on subsequent logs.
- **Explicit Source Injection**: `WriteWithSource` allows performance-critical components to bypass stack walks entirely.
- **Redundant Prefix Filtering**: Automatically handles source tagging so developers don't need to manually add `[Prefix]`.

**Interconnections:** All ModAPI components and individual mods use `MMLog`. It is optimized to perform well even with frequent log calls during game loops.

### `ModAbout.cs`
**Primary Role:** Defines a data structure or class to hold essential metadata about a mod. This includes information such as the mod's name, author, version, description, and potentially dependencies.
**Secondary Role:** Provides a standardized way to present mod information to users and to the mod manager for display and management purposes.
**Interconnections:** Used by `ModDiscovery.cs` to parse mod information and by `ModRegistry.cs` to store details about registered mods. It's often populated from a mod's manifest file.

### `ModRandom.cs`
**Primary Role:** High-performance, deterministic random number generator (XorShift64*).
**Secondary Role:** Provides mod-isolated random streams to prevent "RNG stealing" between mods.
**Key Features:**
- Guaranteed identical results across all platforms/runtimes.
- Supports Gaussian (bell curve), Weighted, and Choose distributions.
- Randomized by default on every save load for fresh gameplay.
- Optional `IsDeterministic` mode for consistent session-to-session logic.

### `ModRandomState.cs`
**Primary Role:** Manages the persistence of RNG state in `seed.json` within save slot folders.
**Secondary Role:** Handles the "fast-forwarding" of the RNG on load and restoration of deterministic seeds.

### `ModAttributes.cs`
**Primary Role:** Defines attributes for the **Spine** settings framework (`[ModConfiguration]`, `[ModSetting]`, `[ModSettingPreset]`).
**Secondary Role:** Enables zero-boilerplate UI generation and reflective settings discovery.
**Interconnections:** Used by `SpineSettingsHelper.cs` and `ModManagerBase.cs`.

### `ModManagerBase.cs`
**Primary Role:** A high-level base class for mods that provides automatic lifecycle management, singleton access, and **Spine** settings binding.
**Secondary Role:** Handles dependency injection for logging and persistence automatically.
**Key Features:**
- `Config` property for type-safe settings access.
- `Log` property for prefixed logging.
- `Instance` singleton for easy access.
**Interconnections:** Inherits from `MonoBehaviour`. orchestrates `Spine` settings load/save.

### `ModPersistenceData.cs`
**Primary Role:** Internal DTO (Data Transfer Object) for mod data persistence.
**Secondary Role:** Stores mod-specific key-value JSON blobs within a save slot's mod data file.
**Interconnections:** Used by `SaveSystemImpl.cs` for serializing mod state.

### `ModAPI.csproj`
**Primary Role:** The project file for the ModAPI itself. It defines the project's structure, references to other assemblies, build configurations (e.g., Debug, Release), and compilation settings.
**Secondary Role:** Essential for the development environment (e.g., Visual Studio) to understand how to build and compile the ModAPI library.
**Interconnections:** Defines the entire ModAPI project and its dependencies.

### `ModDiscovery.cs`
**Primary Role:** Handles the process of scanning designated directories for mod files (e.g., DLLs) and identifying valid mods. It parses mod metadata (e.g., from `ModAbout.cs` information) to determine their characteristics.
**Secondary Role:** Automates the process of finding and registering new mods, making the mod manager dynamic and extensible.
**Interconnections:** Populates `ModRegistry.cs` with discovered mods and their `ModAbout.cs` information.

### `ModRegistry.cs`
**Primary Role:** Manages a central collection or database of all discovered and loaded mods. It provides methods to access, query, and retrieve information about these mods.
**Secondary Role:** Acts as the authoritative source for information about the currently available mods, enabling other components to interact with them.
**Interconnections:** Populated by `ModDiscovery.cs`. `LoadOrderResolver.cs` queries it for mod dependencies. `PluginManager.cs` uses it to access mod instances.

### `ModSettings.cs`
**Primary Role:** Legacy settings manager for key-value pairs (v1.0).
**Secondary Role:** Points to [SETTINGS.md](SETTINGS.md) for usage.
**Interconnections:** Used by `PluginManager.cs` to populate `IPluginContext.Settings`.

### `PluginManager.cs`
**Primary Role:** The central orchestrator of the modding system. It is responsible for loading, initializing, and managing the lifecycle of all plugins (mods) within the game.
**Secondary Role:** Ensures that mods are loaded in the correct order (using `LoadOrderResolver.cs`), initialized properly, and their resources are managed effectively. It's the entry point for integrating mods into the game.
**Interconnections:** Relies heavily on `ModDiscovery.cs` to find mods, `ModRegistry.cs` to store them, `LoadOrderResolver.cs` for ordering, and interacts with `IPlugin.cs` implementations.

### `SceneUtil.cs`
**Primary Role:** Offers utility methods for interacting with game scenes. This includes functionalities like finding specific game objects within a scene, traversing the scene hierarchy, or potentially loading new scenes.
**Secondary Role:** Simplifies scene manipulation for mod developers, allowing them to inject or modify game elements within different scenes.
**Interconnections:** Can be used by `GameUtil.cs` for higher-level game interactions. Potentially used by `Hooks/MainMenuPatches.cs` to interact with the main menu scene.

### `UIFlowGuard.cs`
**Primary Role:** Likely a component designed to manage and control the flow of UI elements or interactions. This could involve preventing conflicting UI actions, ensuring proper sequencing of UI events, or managing UI state.
**Secondary Role:** Enhances UI stability and user experience by preventing race conditions or unexpected behavior when multiple mods or game systems try to manipulate the UI simultaneously.
**Interconnections:** Works in conjunction with `UIUtil.cs` and `ContextUIExtensions.cs` to ensure controlled UI modifications.

### `UIUtil.cs`
**Primary Role:** Contains general utility functions for UI manipulation and creation. This might include methods for creating common UI elements, handling input, or applying styles.
**Secondary Role:** Provides a toolkit for mod developers to build their user interfaces, promoting consistency and reducing the effort required for UI development.
**Interconnections:** Heavily utilized by `ContextUIExtensions.cs` and any mod that needs to create or modify in-game UI.

## Harmony Integration

### `Harmony/HarmonyUtil.cs`
**Primary Role:** Provides helper functions and utilities specifically designed for working with the Harmony patching library. Harmony is a powerful tool for modifying game code at runtime without directly altering the original game files.
**Secondary Role:** Simplifies the process of creating and applying Harmony patches, making it easier for mod developers to inject their code into game methods.
**Interconnections:** Used by `Hooks/HarmonyBootstrap.cs` to initialize Harmony and by `Hooks/MainMenuPatches.cs` (and other potential patch files) to define and apply specific patches.

### `Harmony/FluentTranspiler.cs`
**Primary Role:** A fluent, chainable API for creating readable and robust IL transpilers (`IEnumerable<CodeInstruction>`).
**Secondary Role:** Provides built-in safety checks, label management, and local variable support for complex code injection.
**Interconnections:** Used by mods in their `Transpiler` patches to manipulate method IL.

### `Harmony/TranspilerDebugger.cs`
**Primary Role:** Diagnostic utility that dumps IL instructions to the log or disk before/after modification.
**Secondary Role:** Helps modders troubleshoot why a transpiler isn't matching or is producing invalid IL.
**Interconnections:** Used during development to inspect Harmony outputs.

### `Hooks/HarmonyBootstrap.cs`
**Primary Role:** Responsible for initializing and configuring the Harmony patching library when the mod manager starts up. This prepares the environment for mods to apply their code patches.
**Secondary Role:** Ensures that the Harmony library is correctly set up and ready to be used by any mod that requires code injection.
**Interconnections:** Directly interacts with the Harmony library and uses `HarmonyUtil.cs` for any helper functions. It's a critical component for any mod that uses code patching.

### `Hooks/MainMenuPatches.cs`
**Primary Role:** Contains specific Harmony patches that target methods or functionalities within the game's main menu. These patches allow mods to inject custom functionality, add new UI elements, or modify existing main menu behavior.
**Secondary Role:** Provides a concrete example and a dedicated location for main menu-related code injections, making it easier to manage and extend main menu modifications.
**Interconnections:** Relies on the Harmony library (initialized by `HarmonyBootstrap.cs` and using `HarmonyUtil.cs`). It might interact with `SceneUtil.cs` or `UIUtil.cs` to manipulate the main menu scene or UI.

## Inspector Tools

### `Inspector/BoundsHighlighter.cs`
**Primary Role:** A utility for visualizing the bounding boxes of game objects in the game world. This is primarily a debugging and development tool.
**Secondary Role:** Helps mod developers understand the physical dimensions and collision areas of game objects, which is crucial for accurate placement and interaction logic.
**Interconnections:** Likely interacts with game objects and their renderers to draw the bounding boxes. Could be activated via `RuntimeInspector.cs`.

### `Inspector/HierarchyUtil.cs`
**Primary Role:** Provides helper functions for navigating and inspecting the game's object hierarchy at runtime. This allows developers to easily find parent/child relationships and specific game objects.
**Secondary Role:** Essential for debugging and understanding the structure of game scenes, aiding in the development of mods that need to interact with specific parts of the game world.
**Interconnections:** Used by `RuntimeInspector.cs` to display and navigate the game's object hierarchy.

### `Inspector/RuntimeInspector.cs`
**Primary Role:** Implements an in-game inspector tool that allows developers to view and modify properties of game objects, components, and other runtime data directly within the running game.
**Secondary Role:** A powerful debugging and development aid, enabling real-time inspection and modification of game state, significantly speeding up the mod development process.
**Interconnections:** Utilizes `HierarchyUtil.cs` to display the object hierarchy and potentially `BoundsHighlighter.cs` for visual debugging.

## Project Configuration

### `Properties/AssemblyInfo.cs`
**Primary Role:** Contains assembly-level attributes for the ModAPI project. These attributes include metadata such as the assembly's version number, title, description, company, copyright information, and other build-related settings.
**Secondary Role:** Provides essential identification and versioning information for the compiled ModAPI assembly, which is important for dependency management and release tracking.
**Interconnections:** Standard .NET project file, not directly interconnected with other ModAPI logic but defines the metadata for the compiled ModAPI DLL.

## Reflection Utilities

### `Reflection/Safe.cs`
**Primary Role:** Provides safe reflection utilities. This typically involves methods that handle potential errors or exceptions that can occur during reflection operations (e.g., trying to access a non-existent field or method).
**Secondary Role:** Simplifies the use of reflection for mod developers by providing robust and error-tolerant methods, reducing the likelihood of crashes due to reflection-related issues.
**Interconnections:** Used by any part of the ModAPI or a mod that needs to dynamically access or modify private fields, methods, or types within the game or other assemblies.

---

## Content System

### `Content/ContentRegistry.cs`
**Primary Role:** Central registry for custom content (items, recipes, objects) registered by mods.
**Secondary Role:** Tracks all custom ItemTypes and provides lookup utilities for the injection system.
**Interconnections:** Works with `ContentInjector.cs` to inject custom content into the game's systems.

### `Content/ContentResolver.cs`
**Primary Role:** Resolves content references and dependencies between mods.
**Secondary Role:** Handles ID conflicts and ensures unique ItemType assignments for custom content.
**Interconnections:** Used by `ContentInjector` during mod loading to validate and resolve content.

### `Content/ContentInjector.cs`
**Primary Role:** Injects custom items, recipes, and cooking data into the game's managers.
**Secondary Role:** Manages the injection lifecycle and provides accessors for registered content.
**Key Methods:**
- `ContentRegistry.RegisterItem(...)` / `RegisterItemWithFixedId(...)` - Register custom items
- `ContentRegistry.RegisterRecipe(...)` - Add crafting recipes
- `ContentInjector.GetCookingRecipes()` - Access cooking recipe data
- `ContentInjector.GetRawFoodTypes()` - Access raw food item types
**Interconnections:** Patches `ItemManager`, `CraftingManager`, and `FoodManager` to add custom content.

###`Content/InventoryIntegration.cs`
**Primary Role:** Ensures custom items appear correctly in inventory UIs and storage systems.
**Secondary Role:** Handles item transfer logic between different inventory contexts.
**Interconnections:** Works with `InventoryManager` and UI panels for custom item display.

### `Content/AssetLoader.cs`
**Primary Role:** Loads custom assets (sprites, prefabs) from mod folders at runtime.
**Secondary Role:** Manages asset bundling and memory for mod resources.
**Interconnections:** Used by mods to load custom textures and Unity prefabs.

---

## Character System

### `Characters/PartyHelper.cs`
**Primary Role:** Provides utilities for managing expedition parties and family members.
**Secondary Role:** Raises events when party composition changes.
**Key Events:**
- `OnPartyCompositionChanged` - Fired when members are added/removed from parties
**Interconnections:** Works with `ExplorationManager` and `FamilyManager` from the game.

### `Characters/PartyPatches.cs`
**Primary Role:** Harmony patches for party-related game mechanics.
**Secondary Role:** Enables mod hooks into party creation and modification.
**Interconnections:** Patches game party systems to trigger `PartyHelper` events.

---

## Items System

### `Items/InventoryHelper.cs`
**Primary Role:** Helper methods for inventory manipulation and item management.
**Secondary Role:** Provides safe wrappers around `InventoryManager` operations.
**Interconnections:** Used by mods to add/remove items safely without direct manager access.

---

## Custom Saves System

The Custom Saves system extends Sheltered's 3-slot limit to unlimited save slots with enhanced features.

### Key Files:
- `saves/SaveRegistryCore.cs` - Core save slot management and registry
- `saves/Models.cs` - Data models for save entries, slots, and reservations
- `saves/Events.cs` - Save/load lifecycle events (OnBeforeSave, OnAfterLoad, OnPageChanged)
- `saves/DirectoryProvider.cs` - Save file path management
- `saves/PreviewCapture.cs` - Screenshot capture for save previews
- `paging/PagingManager.cs` - Multi-page save slot UI management
- `paging/SaveVerification.cs` - Save file integrity checking and mod verification
- `paging/SaveDetailsWindow.cs` - Enhanced save details UI

**Key Features:**
- Unlimited save slots (3 vanilla + unlimited custom)
- Save previews with screenshots
- Mod dependency tracking
- CRC32 integrity verification
- Multi-page UI with navigation

**Events:**
```csharp
ModAPI.Saves.Events.OnBeforeSave   // Before save operation
ModAPI.Saves.Events.OnAfterSave    // After save completes
ModAPI.Saves.Events.OnBeforeLoad   // Before load operation
ModAPI.Saves.Events.OnAfterLoad    // After load completes
ModAPI.Saves.Events.OnPageChanged  // Save slot page navigation
```

**Interconnections:** Patches `SaveManager`, `SlotSelection Panel`, and UI systems. Works with `PlatformSaveProxy` for cross-platform save handling.

---

## Game State

### `GameState/ManagerStateHelper.cs`
**Primary Role:** Utilities for querying and managing game manager states.
**Secondary Role:** Provides safe access to game manager readiness and lifecycle.
**Interconnections:** Works with all game managers to check initialization status.

---

## Utility Modules

### `Util/GameUtil.cs`
**Primary Role:** Game-specific utility functions for common operations.
**Secondary Role:** Abstracts game mechanics into safe, mod-friendly APIs.
**Interconnections:** Used throughout ModAPI for game interactions.

### `Util/SceneUtil.cs`
**Primary Role:** Scene management and navigation utilities.
**Secondary Role:** Helps mods interact with Unity scene system safely.
**Interconnections:** Works with Unity's SceneManager and mod scene hooks.

### `Util/SceneCompat.cs`
**Primary Role:** Compatibility layer for different Unity versions.
**Secondary Role:** Abstracts scene API differences between Steam and EGS versions.
**Interconnections:** Used by `SceneUtil` for cross-version compatibility.

### `Util/PersistentDataAPI.cs`
**Primary Role:** Simple API for mods to save/load persistent data across game sessions.
**Secondary Role:** Handles JSON serialization and file management for mod data.
**Key Methods:**
- `ctx.SaveData<T>(string key, T data)` - Save mod data (extension on `IPluginContext`)
- `ctx.LoadData<T>(string key, out T value)` - Load mod data
**Interconnections:** Used by mods for configuration and state persistence.

### `Util/SaveLoadDictionary.cs`
**Primary Role:** A serializable dictionary wrapper that makes persisting dynamic collections of mod data easy.
**Secondary Role:** Implements `ISerializationCallbackReceiver` to handle dictionary persistence via JSON.
**Interconnections:** Designed to be used with `ISaveSystem.RegisterModData`.

---

## UI System

### `UI/ModManagerPanel.cs`
**Primary Role:** In-game UI panel for managing mods, viewing details, and accessing settings.
**Secondary Role:** Provides visual feedback for mod status and configuration.
**Interconnections:** Integrates with `PluginManager` and `ModRegistry` for mod information.

### `UI/UIFactory.cs`
**Primary Role:** Factory methods for creating common UI elements with consistent styling.
**Secondary Role:** Provides tiered helpers for simple buttons to complex interactive elements.
**Key Methods:**
- `CreateIconButton()` - Simple icon buttons
- `CreateArrowButton()` - Directional navigation buttons
- `CreateInteractiveElement()` - Complex UI elements with options
**Interconnections:** Used by mods to create in-game UI that matches vanilla style.

### `UI/UIHelper.cs`
**Primary Role:** General UI manipulation utilities and helpers.
**Secondary Role:** Simplifies common UI operations like finding panels and objects.
**Interconnections:** Works with NGUI and Unity UI systems.

### `UI/NGUIScrollHelper.cs`
**Primary Role:** Utilities for working with NGUI scroll views.
**Secondary Role:** Handles scroll position, dragging, and content management.
**Interconnections:** Used by UI panels with scrolling content.

### `UI/UIDebug.cs`
**Primary Role:** Debug utilities for UI development and troubleshooting.
**Secondary Role:** Provides visual overlays and hierarchy inspection.
**Interconnections:** Works with `RuntimeInspector` for UI debugging.

### `UI/UIPatches.cs`
**Primary Role:** Harmony patches for UI panels to inject custom items and trigger events.
**Secondary Role:** Ensures custom content appears in Storage, Recycling, Trading, and Fabrication panels.
**Interconnections:** Patches vanilla UI panels and raises `UIEvents` lifecycle events.

---

## ModAPI Project File Tree

```
shelteredmodmanager/
ModAPI/
    Core/
       ContextExtensions.cs
       ContextUIExtensions.cs
       IPlugin.cs
       MMLog.cs
       LoggerExtensions.cs
       ModAbout.cs
       ModAboutReader.cs
       ModDiscovery.cs
       ModEntry.cs
       ModRegistry.cs
       ModAPIRegistry.cs
       ModSettings.cs
       ModAttributes.cs
       ModRandom.cs
       ModRandomState.cs
       ModManagerBase.cs
       ModPersistenceData.cs
       PluginManager.cs
       RuntimeCompat.cs
       SaveProtection.cs
    Events/
       GameEvents.cs         (Core game lifecycle events)
       ModEventBus.cs       (Inter-mod communication)
       UIEvents.cs          (UI panel lifecycle events)
    Content/
       AssetLoader.cs
       ContentInjector.cs
       ContentRegistry.cs
       ContentResolver.cs
       InventoryIntegration.cs
    Characters/
       PartyHelper.cs
       PartyPatches.cs
    Items/
       InventoryHelper.cs
    Custom Saves/
       paging/
          PagingManager.cs
          SaveDetailsWindow.cs
          SaveVerification.cs
          SlotSelectionPatches.cs
       saves/
          CRC32.cs
          DirectoryProvider.cs
          Events.cs
          ExpandedVanillaSaves.cs
          IdGenerator.cs
          ISaveApi.cs
          Models.cs
          NameSanitizer.cs
          PreviewAuto.cs
          PreviewCapture.cs
          SaveManagerInspector.cs
          SaveManager_SaveGlobalData_Patch.cs
          SaveRegistryCore.cs
          ScenarioRegistry.cs
          ScenarioSaves.cs
       PlatformSaveProxy.cs
       SaveExitPanelOnCancelPatch.cs
       SaveManager_Injection_Patch.cs
       SaveManager_SaveToCurrentSlot_Patch.cs
    GameState/
       ManagerStateHelper.cs
    UI/
       ContextUIExtensions.cs
       ModManagerPanel.cs
       NGUIScrollHelper.cs
       UIDebug.cs
       UIFactory.cs
       UIFlowGuard.cs
       UIHelper.cs
       UIPatches.cs
       UIUtil.cs
    Util/
       GameUtil.cs
       PersistentDataAPI.cs
       SaveLoadDictionary.cs
       SceneCompat.cs
       SceneUtil.cs
    Harmony/
       AdvancedExtensions.cs
       FluentTranspiler.cs
       FluentTranspilerPatterns.cs
       HarmonyBootstrap.cs
       HarmonyHelper.cs
       HarmonyUtil.cs
       MainMenuPatches.cs
       ShelteredPatterns.cs
       TranspilerDebugger.cs
       UnityPatterns.cs
    Inspector/
       BoundsHighlighter.cs
       HierarchyUtil.cs
       RuntimeInspector.cs
    Reflection/
       Safe.cs
    Properties/
       AssemblyInfo.cs
    ModAPI.csproj
```

---

## Event System Summary

The ModAPI provides **four complementary event systems** for different use cases:

| System | Use When | Example |
|--------|----------|---------|
| **GameEvents** | Reacting to core game events | `GameEvents.OnNewDay`, `OnCombatStarted` |
| **ModEventBus** | Custom inter-mod communication | Custom quest/trade/discovery events |
| **ModAPIRegistry** | Sharing complex APIs between mods | Crafting framework, economy system APIs |
| **UIEvents** | Tracking UI panel lifecycle | Auto-save on menu, react to panel opens |
| **ModRegistry** | Detecting if other mods are loaded | Conditional features, compatibility |

**Design Philosophy:** RimWorld-style automatic discovery with zero-configuration mod detection. Just add `About.json` and everything works!

For detailed examples and best practices, see `Events_Guide.md`.
