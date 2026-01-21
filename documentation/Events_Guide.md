# ModAPI Events Guide
## Sheltered Mod Loader v1.0

**Last Updated:** 2026-01-15  
**For:** Mod developers using the Sheltered ModAPI

---

## Table of Contents

1. [Overview](#overview)
2. [Event Systems](#event-systems)
3. [Game Lifecycle Events](#game-lifecycle-events)
4. [UI Events](#ui-events)
5. [Custom Save Events](#custom-save-events)
6. [Inter-Mod Communication](#inter-mod-communication)
7. [Mod API Registry](#mod-api-registry)
8. [Best Practices](#best-practices)
9. [Performance Considerations](#performance-considerations)
10. [Troubleshooting](#troubleshooting)

---

## Overview

The ModAPI provides **multiple event systems** to help mods react to game state changes without writing Harmony patches. Events are **synchronous** (handlers execute immediately) and **type-safe** (compile-time checking).

### When to Use Events vs Harmony Patches

**Use Events When:**
- ✅ Reacting to common game actions (crafting, saves, UI changes)
- ✅ Sharing data between your own mod components
- ✅ Communicating with other mods
- ✅ You want simpler, more maintainable code

**Use Harmony Patches When:**
- ⚙️ Modifying game logic (changing return values, cancelling actions)
- ⚙️ Accessing private game state
- ⚙️ No suitable event exists yet

---

## Event Systems

### Available Event Systems

| System | Purpose | Location |
|--------|---------|----------|
| `GameEvents` | Core game lifecycle | `ModAPI.Events.GameEvents` |
| `UIEvents` | Panel open/close/resume/pause | `ModAPI.Events.UIEvents` |
| `ModEventBus` | Inter-mod custom events | `ModAPI.Events.ModEventBus` |
| `ModAPIRegistry` | Mod API service discovery | `ModAPI.Core.ModAPIRegistry` |
| Save Events | Custom save system events | `ModAPI.Saves.Events` |

---

## Game Lifecycle Events

**Namespace:** `ModAPI.Events.GameEvents`

### Available Events

```csharp
// Day/time events
public static event Action<int> OnNewDay;

// Save/load events
public static event Action<SaveData> OnBeforeSave;
public static event Action<SaveData> OnAfterLoad;

// Combat events
public static event Action<EncounterCharacter, EncounterCharacter> OnCombatStarted;

// Expedition events
public static event Action<ExplorationParty> OnPartyReturned;
```

### Example: Tracking Days Survived

```csharp
using ModAPI.Core;
using ModAPI.Events;

public class MyMod : IModPlugin
{
    private int daysTracked = 0;
    private IModLogger _log;
    
    public void Initialize(IPluginContext ctx)
    {
        _log = ctx.Log;
        _log.Info("MyMod initializing...");
    }
    
    public void Start(IPluginContext ctx)
    {
        // Subscribe to new day event
        GameEvents.OnNewDay += OnDayChanged;
        _log.Info("Subscribed to OnNewDay event");
    }
    
    private void OnDayChanged(int dayNumber)
    {
        daysTracked++;
        MMLog.Info($"Day {dayNumber} - tracked {daysTracked} days total");
    }
}
```

### Example: Auto-Save Before Combat

```csharp
using ModAPI.Core;
using ModAPI.Events;

public class CombatSafetyMod : IModPlugin
{
    private IModLogger _log;
    
    public void Initialize(IPluginContext ctx)
    {
        _log = ctx.Log;
    }
    
    public void Start(IPluginContext ctx)
    {
        GameEvents.OnCombatStarted += (player, enemy) =>
        {
            _log.Info($"Combat started! Player: {player.GetName()}, Enemy: {enemy.GetName()}");
            
            // Trigger auto-save
            if (SaveManager.instance != null)
            {
                SaveManager.instance.SaveToCurrentSlot(false);
                _log.Info("Auto-saved before combat");
            }
        };
    }
}
```

### Example: Welcome Back After Load

```csharp
GameEvents.OnAfterLoad += (saveData) =>
{
    MMLog.Info("Game loaded successfully!");
    
    // Access save data
    if (saveData != null)
    {
        MMLog.Info($"Loaded save version: {saveData.GetVersion()}");
    }
};
```

---

## UI Events

**Namespace:** `ModAPI.Events.UIEvents`

### Available Events

```csharp
// Panel lifecycle
public static event Action<BasePanel> OnPanelOpened;
public static event Action<BasePanel> OnPanelClosed;
public static event Action<BasePanel> OnPanelResumed;
public static event Action<BasePanel> OnPanelPaused;

// Button clicks (optional, high-frequency)
public static event Action<GameObject, string> OnButtonClicked;
```

### Example: React to Crafting Panel Opening

```csharp
using ModAPI.Core;
using ModAPI.Events;

public class CraftingHelperMod : IModPlugin
{
    private IModLogger _log;
    
    public void Initialize(IPluginContext ctx)
    {
        _log = ctx.Log;
    }
    
    public void Start(IPluginContext ctx)
    {
        UIEvents.OnPanelOpened += panel =>
        {
            // Check panel type by name
            string panelName = panel.GetType().Name;
            
            if (panelName == "CraftingPanel" || panelName == "WorkbenchPanel")
            {
                _log.Info("Crafting panel opened - showing helper UI");
                ShowCraftingHelp();
            }
        };
    }
    
    private void ShowCraftingHelp()
    {
        // Your custom UI logic here
    }
}
```

### Example: Track UI Navigation Flow

```csharp
using System.Collections.Generic;

public class UITrackerMod : IModPlugin
{
    private Stack<string> panelHistory = new Stack<string>();
    
    public void Start(IPluginContext ctx)
    {
        UIEvents.OnPanelOpened += panel =>
        {
            string name = panel.GetType().Name;
            panelHistory.Push(name);
            ctx.Log.Info($"Panel opened: {name} (stack depth: {panelHistory.Count})");
        };
        
        UIEvents.OnPanelClosed += panel =>
        {
            if (panelHistory.Count > 0)
            {
                string popped = panelHistory.Pop();
                ctx.Log.Info($"Panel closed: {popped}");
            }
        };
    }
}
```

### Example: Detect Main Menu Return

```csharp
UIEvents.OnPanelOpened += panel =>
{
    if (panel.GetType().Name == "MainMenuPanel")
    {
        MMLog.Info("Player returned to main menu");
        // Save state, clear caches, etc.
    }
};
```

---

## Custom Save Events

**Namespace:** `ModAPI.Saves.Events`

### Available Events

```csharp
public static event SaveEvent OnBeforeSave;
public static event SaveEvent OnAfterSave;
public static event LoadEvent OnBeforeLoad;
public static event LoadEvent OnAfterLoad;
public static event PageChangedEvent OnPageChanged;
public static event ReservationChangedEvent OnReservationChanged;
```

### Example: Persist Mod Data

```csharp
using ModAPI.Saves;

public class DataPersistenceMod : IModPlugin
{
    private MyModData modData = new MyModData();
    
    public void Start(IPluginContext ctx)
    {
        Events.OnBeforeSave += saveEntry =>
        {
            ctx.Log.Info($"Saving mod data for slot {saveEntry.id}");
            // Use PersistentDataAPI to save your data
            PersistentDataAPI.SaveData("MyMod.CustomData", modData);
        };
        
        Events.OnAfterLoad += saveEntry =>
        {
            ctx.Log.Info($"Loading mod data from slot {saveEntry.id}");
            modData = PersistentDataAPI.LoadData<MyModData>("MyMod.CustomData");
        };
    }
}
```

---

## Inter-Mod Communication

**Namespace:** `ModAPI.Events.ModEventBus`

The `ModEventBus` allows mods to publish and subscribe to **custom typed events** for inter-mod communication.

### Naming Convention

**Always use reverse-domain notation:**
```
com.authorname.modname.EventName
```

Examples:
- `com.coolnether.craftingplus.RecipeDiscovered`
- `com.myname.questmod.QuestCompleted`

### Publishing Events

```csharp
using ModAPI.Events;

public class QuestMod : IModPlugin
{
    public class QuestCompletedEventArgs
    {
        public string QuestId { get; set; }
        public int Reward { get; set; }
    }
    
    public void Start(IPluginContext ctx)
    {
        // When a quest completes, publish the event
        var eventData = new QuestCompletedEventArgs
        {
            QuestId = "first_craft",
            Reward = 100
        };
        
        ModEventBus.Publish("com.myname.questmod.QuestCompleted", eventData);
        ctx.Log.Info("Published quest completion event");
    }
}
```

### Subscribing to Events

```csharp
using ModAPI.Events;

public class RewardMod : IModPlugin
{
    public void Start(IPluginContext ctx)
    {
        // Subscribe to another mod's events
        ModEventBus.Subscribe<QuestCompletedEventArgs>(
            "com.myname.questmod.QuestCompleted", 
            OnQuestCompleted
        );
        
        ctx.Log.Info("Subscribed to quest completion events");
    }
    
    private void OnQuestCompleted(QuestCompletedEventArgs args)
    {
        MMLog.Info($"Quest {args.QuestId} completed! Reward: {args.Reward}");
        // Give bonus reward
        GiveBonusReward(args.Reward * 0.1f);
    }
}
```

### Checking for Subscribers

```csharp
// Only publish if someone is listening (performance optimization)
if (ModEventBus.HasSubscribers("com.mymod.HeavyEvent"))
{
    var heavyData = GatherExpensiveData();
    ModEventBus.Publish("com.mymod.HeavyEvent", heavyData);
}
```

### Unsubscribing (Cleanup)

```csharp
public class MyMod : IModPlugin, IModShutdown
{
    private Action<MyEventArgs> handler;
    
    public void Start(IPluginContext ctx)
    {
        handler = OnMyEvent;
        ModEventBus.Subscribe("com.mymod.Event", handler);
    }
    
    public void Shutdown()
    {
        // Clean up subscriptions
        ModEventBus.Unsubscribe("com.mymod.Event", handler);
    }
}
```

---

## Mod API Registry

**Namespace:** `ModAPI.Core.ModAPIRegistry`

The `ModAPIRegistry` allows mods to publish **shared APIs** that other mods can discover and use.

### Define an API Interface

```csharp
// In your mod's assembly - make this public so other mods can reference it
namespace MyCraftingMod.API
{
    public interface ICraftingAPI
    {
        void RegisterRecipe(string recipeId, ItemType result, ItemType[] ingredients);
        IEnumerable<string> GetCustomRecipeIds();
        bool IsCustomRecipe(string recipeId);
    }
}
```

### Publish Your API

```csharp
using ModAPI.Core;
using MyCraftingMod.API;

public class CraftingMod : IModPlugin
{
    private MyCraftingAPIImpl apiImpl;
    
    public void Start(IPluginContext ctx)
    {
        // Create implementation
        apiImpl = new MyCraftingAPIImpl();
        
        // Register API for other mods to use
        bool success = ModAPIRegistry.RegisterAPI<ICraftingAPI>(
            "com.myname.CraftingAPI",
            apiImpl,
            ctx.Mod.Id
        );
        
        if (success)
        {
            ctx.Log.Info("CraftingAPI registered successfully");
        }
        else
        {
            ctx.Log.Warn("Failed to register CraftingAPI - already registered?");
        }
    }
}

// Your API implementation
internal class MyCraftingAPIImpl : ICraftingAPI
{
    public void RegisterRecipe(string recipeId, ItemType result, ItemType[] ingredients)
    {
        // Implementation
    }
    
    // ... other methods
}
```

### Consume Another Mod's API

```csharp
using ModAPI.Core;
using MyCraftingMod.API; // Reference their mod's API interface

public class RecipePackMod : IModPlugin
{
    public void Start(IPluginContext ctx)
    {
        // Try to get the CraftingAPI
        if (ModAPIRegistry.TryGetAPI<ICraftingAPI>("com.myname.CraftingAPI", out var craftingAPI))
        {
            ctx.Log.Info("Found CraftingAPI - registering custom recipes");
            
            craftingAPI.RegisterRecipe(
                "custom_sword",
                ItemType.Custom_Sword,
                new[] { ItemType.Metal, ItemType.Metal, ItemType.WoodPlank }
            );
        }
        else
        {
            ctx.Log.Warn("CraftingAPI not found - is CraftingMod installed?");
        }
    }
}
```

### Optional Dependencies

Document in your `About.json`:

```json
{
  "id": "com.myname.recipepack",
  "name": "Custom Recipe Pack",
  "version": "1.0.0",
  "description": "Adds new recipes. Optional: requires CraftingMod for advanced recipes.",
  "dependsOn": [],
  "optionalDependencies": ["com.myname.craftingmod"]
}
```

---

## Best Practices

### 1. Always Handle Errors

```csharp
GameEvents.OnNewDay += day =>
{
    try
    {
        // Your logic here
        ProcessDayChange(day);
    }
    catch (Exception ex)
    {
        MMLog.Warn($"Error in OnNewDay handler: {ex.Message}");
    }
};
```

### 2. Unsubscribe When Done

```csharp
public class MyMod : IModPlugin, IModShutdown
{
    private Action<int> dayHandler;
    
    public void Start(IPluginContext ctx)
    {
        dayHandler = OnDayChanged;
        GameEvents.OnNewDay += dayHandler;
    }
    
    public void Shutdown()
    {
        GameEvents.OnNewDay -= dayHandler;
    }
    
    private void OnDayChanged(int day) { /* ... */ }
}
```

### 3. Keep Handlers Fast

```csharp
// BAD: Heavy processing in event handler
UIEvents.OnPanelOpened += panel =>
{
    ProcessMassiveDataset(); // Blocks UI!
};

// GOOD: Defer heavy work
UIEvents.OnPanelOpened += panel =>
{
    ctx.RunNextFrame(() => ProcessMassiveDataset());
};
```

### 4. Use Descriptive Event Names

```csharp
// BAD
ModEventBus.Publish("Data", myData);

// GOOD
ModEventBus.Publish("com.mymod.resourcepack.ResourceDiscovered", discoveryData);
```

### 5. Document Your Events

```csharp
/// <summary>
/// Fired when a custom resource node is discovered on the map.
/// Subscribe with: ModEventBus.Subscribe&lt;ResourceDiscoveredArgs&gt;("com.mymod.ResourceDiscovered", handler);
/// </summary>
public class ResourceDiscoveredArgs
{
    public Vector2 Location { get; set; }
    public string ResourceType { get; set; }
    public int Quantity { get; set; }
}
```

---

## Performance Considerations

### Event Handler Overhead

- **Per-event cost:** ~100-500 nanoseconds
- **Negligible** for most use cases
- **High-frequency events** (e.g., OnButtonClicked) should be used carefully

### Optimization Tips

```csharp
// 1. Filter early
UIEvents.OnPanelOpened += panel =>
{
    // Quick type check
    if (panel.GetType().Name != "CraftingPanel")
        return; // Exit early
    
    // Heavy logic only for relevant panels
    ProcessCraftingPanel(panel);
};

// 2. Cache delegates
private Action<int> cachedHandler; // Reuse instead of creating new lambda each time

public void Start(IPluginContext ctx)
{
    cachedHandler = OnDayChanged;
    GameEvents.OnNewDay += cachedHandler;
}

// 3. Conditional publishing
if (ModEventBus.HasSubscribers("com.mymod.RareEvent"))
{
    // Only gather data if someone is listening
    var data = GatherExpensiveData();
    ModEventBus.Publish("com.mymod.RareEvent", data);
}
```

---

## Troubleshooting

### Events Don't Fire

**Check:**
1. Is the event system initialized? (Harmony patches applied correctly?)
2. Are you subscribing in `Start()` not `Initialize()`?
3. Enable debug logging: `MMLog.WriteDebug` calls will show event firing

```csharp
// Check if event has subscribers
int count = ModEventBus.GetSubscriberCount("com.mymod.Event");
MMLog.Info($"Event has {count} subscribers");

// Get diagnostics
var diagnostics = ModEventBus.GetEventDiagnostics();
foreach (var kvp in diagnostics)
{
    MMLog.Info($"Event: {kvp.Key}, Subscribers: {kvp.Value}");
}
```

### Type Mismatch Errors

```csharp
// BAD: Wrong type
ModEventBus.Subscribe<string>("com.mymod.Event", handler); // Expects string
ModEventBus.Publish("com.mymod.Event", 123); // Sends int

// GOOD: Matching types
ModEventBus.Subscribe<int>("com.mymod.Event", handler); // Expects int
ModEventBus.Publish("com.mymod.Event", 123); // Sends int
```

### Handler Exceptions

The event system catches exceptions to prevent one mod from crashing others:

```csharp
GameEvents.OnNewDay += day =>
{
    throw new Exception("Oops!"); // Caught and logged, doesn't crash game
};
```

Check `mod_manager.log` for error messages:
```
[ModEventBus] Handler error for com.mymod.Event: Oops!
```

### API Not Found

```csharp
// Check if API is registered
if (ModAPIRegistry.IsAPIRegistered("com.other.API"))
{
    var api = ModAPIRegistry.GetAPI<IMyAPI>("com.other.API");
} 
else
{
    MMLog.Warn("API not found - is the provider mod loaded?");
}

// Get all registered APIs
var apis = ModAPIRegistry.GetRegisteredAPIs();
MMLog.Info($"Found {apis.Count} registered APIs");
foreach (var apiName in apis)
{
    MMLog.Info($"  - {apiName}");
}
```

---

## Summary

- **GameEvents**: Core game lifecycle (day, save, combat, expeditions)
- **UIEvents**: Panel open/close/resume/pause tracking
- **ModEventBus**: Custom inter-mod event communication
- **ModAPIRegistry**: Mod API service discovery
- **Save Events**: Custom save system lifecycle

**Remember:**
- ✅ Events are **simpler** than Harmony patches
- ✅ Use **reverse-domain naming** for custom events/APIs
- ✅ Always **handle errors** in event handlers
- ✅ **Unsubscribe** when your mod shuts down
- ✅ Keep handlers **fast** (defer heavy work)

Happy modding!
