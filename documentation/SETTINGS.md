# Settings and persistence

Use Spine settings for player configuration. Use `IPluginContext.SaveSystem` for ordinary mod-owned data in the active save.

## Add settings to a plugin

The loader can discover one settings holder field or property on an `IModPlugin`. Name it `Settings` or `Config` and initialize it before `Initialize(...)` returns.

```csharp
using ModAPI.Core;
using ModAPI.Spine;

public sealed class MySettings
{
    [ModSetting(
        "Enable feature",
        Mode = SettingMode.Simple,
        TrueLabel = "Enabled",
        FalseLabel = "Disabled")]
    public bool Enabled = true;

    [ModSetting("Speed", Min = 1f, Max = 10f, StepSize = 0.5f)]
    public float Speed = 5f;
}

public sealed class MyPlugin : IModPlugin
{
    public readonly MySettings Settings = new MySettings();

    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        if (Settings.Enabled)
            ctx.Log.Info("Feature enabled at speed " + Settings.Speed);
    }
}
```

After `Initialize(...)`, the loader finds the holder, creates a `SettingsController`, loads persisted values, and then calls `Start(...)`. It also reloads the controller at session start and saves it before the active game save.

Do not implement a raw `ISettingsProvider` only to scan an attributed object. The current raw-provider path registers the provider but does not create or load a `SettingsController`. Use the discovered holder pattern unless the plugin owns the complete load and save behavior.

`ModManagerBase<T>` is not the recommended settings path until its `MonoBehaviour` activation and cleanup are verified. See [Develop a plugin](how%20to%20develop%20a%20plugin.md#modmanagerbase-status).

## Choose setting metadata

Common `ModSettingAttribute` fields are:

| Field | Use |
| --- | --- |
| `Scope` | Store globally or per save |
| `Mode` | Show in Simple or Advanced view |
| `Category`, `SortOrder` | Group and order settings |
| `Min`, `Max`, `StepSize` | Configure numeric input |
| `TrueLabel`, `FalseLabel` | Set Boolean state text |
| `OptionsSource` | Supply choice values from a method or property |
| `OnChanged`, `ValidateMethod`, `VisibilityMethod` | Name optional callbacks |
| `RequiresRestart` | Mark a change that takes effect after restart |
| `CarryOverToNewGamePlus`, `NewGamePlusMerge` | Control New Game Plus behavior for per-save values |

Use the exact callback signatures documented by IntelliSense. A missing callback name is reported in `SMM/mod_manager.log`.

## Save mod-owned state

Register one object for each stable key during `Initialize(...)`:

```csharp
using ModAPI.Core;

public sealed class MySaveState
{
    public int Visits;
}

public sealed class MyPlugin : IModPlugin
{
    private readonly MySaveState _state = new MySaveState();

    public void Initialize(IPluginContext ctx)
    {
        ctx.SaveSystem.RegisterModData("state", _state);
    }

    public void Start(IPluginContext ctx) { }
}
```

Keep the registration key stable. The save system scopes it to the active mod and save.

## Synchronize runtime state

Implement `IModPersistenceLifecycle` when the stored object mirrors another runtime service:

```csharp
using ModAPI.Core;
using ModAPI.Persistence;

public sealed class MySaveState : IModPersistenceLifecycle
{
    public int Visits;

    public void PrepareForSave(IModSaveContext context)
    {
        // Copy current runtime values into this object.
    }

    public void RestoreAfterLoad(IModSaveContext context)
    {
        // Apply restored values to the runtime owner.
    }

    public bool ValidateAfterLoad(
        IModSaveContext context,
        out string diagnosticMessage)
    {
        diagnosticMessage = Visits < 0 ? "Visits cannot be negative." : null;
        return diagnosticMessage == null;
    }
}
```

The save system calls `RestoreAfterLoad` and `ValidateAfterLoad` once for loaded, migrated, or defaulted data in each active save context. A missing key resets to its registration-time state instead of retaining values from the previous save.

## Work with Sheltered slots

Use `ctx.SaveSystem` for normal mod state. Use `ShelteredSaves` and `ShelteredSaveEvents` only when a mod must inspect or operate on Sheltered slot descriptors and lifecycle:

```csharp
using ShelteredAPI.Saves;

SaveEntry[] saves = ShelteredSaves.ListStandard(0, 20);
```

These APIs require `ShelteredAPI.dll`.

## Framework diagnostic flags

`ModPrefs` contains global ModAPI diagnostic and safety flags. They are framework settings, not per-mod configuration. Change them only for a specific diagnosis and restore the production defaults afterward. See [Transpiler safety settings](Transpiler_Safety_Settings.md).
