# Core ModAPI Basics (v2.0 Beta.1)

This guide covers the minimum host-neutral plugin model. For a packaged first mod, start with [How to Develop a Plugin](how%20to%20develop%20a%20plugin.md). For exact method shapes, use [API Signatures Reference](API_Signatures_Reference.md).

Read the canonical [ModAPI/ShelteredAPI assembly boundary](README.md#assembly-boundary-canonical) before adding game-facing features. Do not copy Sheltered-specific examples into a neutral plugin unless the mod actually needs that assembly.

## What ModAPI Provides

| Area | Use It For |
|------|------------|
| `IModPlugin` and `IPluginContext` | lifecycle, logging, roots, Unity scheduling, neutral runtime access |
| `ModManagerBase<T>` and Spine settings contracts | common mod configuration setup |
| `ctx.SaveSystem` | ordinary per-mod persisted state scoped to the active save |
| `ModEventBus` and `ModAPIRegistry` | mod-to-mod messages and service lookup |
| Input, actor, Harmony, random, and background-work contracts | host-neutral behavior where the current API exposes it |

`ShelteredAPI` supplies Sheltered runtime implementations behind some neutral contracts, but a mod that only consumes neutral `ModAPI` types does not compile against those implementation types.

## Minimal Lifecycle

```csharp
using ModAPI.Core;

public class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx)
    {
        ctx.Log.Info("Initialize");
    }

    public void Start(IPluginContext ctx)
    {
        ctx.Log.Info("Start");
    }
}
```

Use the lifecycle consistently:

1. `Initialize(...)`: cache context, register neutral mod data, and set up lightweight state.
2. `Start(...)`: subscribe runtime behavior, register game-facing content through the appropriate facade, and apply patches.
3. `IModShutdown.Shutdown()`: unsubscribe and clean up owned runtime behavior when needed.

Keep constructors free of runtime side effects. Scene objects may not exist at startup; use `RunNextFrame(...)` or scene callbacks where appropriate.

## Settings And Ordinary Mod Data

For settings, use `ModManagerBase<T>` unless the mod needs explicit `ISettingsProvider` control. For ordinary save-scoped state, stay on the neutral persistence path:

```csharp
public class SaveState
{
    public int Counter;
}

private readonly SaveState _state = new SaveState();

public void Initialize(IPluginContext ctx)
{
    ctx.SaveSystem.RegisterModData("state", _state);
}
```

For data that mirrors runtime services, implement `ModAPI.Persistence.IModPersistenceLifecycle` on the registered object. Its `PrepareForSave` hook runs before serialization, while `RestoreAfterLoad` and `ValidateAfterLoad` run once for loaded, successfully migrated, or registered-default data in each active save context. Existing `IModPersistenceLogic` callers remain supported.

Do not use Sheltered save-slot APIs merely to persist normal mod state. Use them only when a mod needs to inspect or control Sheltered slot/descriptors/lifecycle. Complete settings and persistence examples live in [Settings and Persistence](SETTINGS.md).

## Deterministic Random Streams

Use `ModRandom` for save-replayable choices. It is the canonical neutral random service:

```csharp
ModRandomStream encounterRandom = ModRandom.GetStream("com.mymod", "encounter-rewards");
int rewardIndex = encounterRandom.Range(0, rewards.Count);
```

Use a stable mod ID and feature ID for each decision family. Draws from one named stream do not advance unrelated streams. `ResetForSaveSeed(...)` restarts save-seeded streams, while deterministic save restoration resumes the stored stream states. `ModManagerBase.Random` already uses a scoped canonical stream.

## Choose The Next Guide

| Mod requirement | Guide |
|-----------------|-------|
| Items, recipes, loot, or content assets | [ShelteredAPI Content Guide](ShelteredAPI_Content_Guide.md) |
| Sheltered save slots, UI, input, game events, or scenarios | [ShelteredAPI Guide](ShelteredAPI_Guide.md) |
| Events and time triggers | [Events Guide](Events_Guide.md) |
| Keybindings | [Input Keybindings Guide](Input_Keybindings_Guide.md) |
| Actors and Sheltered characters | [Actors Guide](ShelteredAPI_Characters_Guide.md) |
| Mod-owned panels, stores, or cooking stations | [Runtime UI, Stores, and Cooking Stations](ShelteredAPI_Runtime_UI_Stores_Guide.md) |
| Custom scenarios | [Custom Scenarios Guide](Custom_Scenarios_Guide.md) |
| Harmony and transpilers | [Harmony Patch Guide](how%20to%20develop%20a%20patch%20with%20harmony.md) |

## Practical Rules

- Use stable, namespaced IDs for data keys, actions, components, triggers, and registered content.
- Prefer the documented facade for a task before patching vanilla behavior.
- Use `ctx.Log` for mod logs.
- Treat public facades and DTOs as the author surface; internal manager-binding classes may move.
- The Beta.1 API is a breaking public-beta line. Back up saves before testing a mod that touches save or scenario behavior.
