# ShelteredAPI Characters Guide

This guide explains how to use the `ModAPI.Characters` API exposed through:

```csharp
IPluginContext.Characters
```

## 1. Getting Started

In your plugin:

```csharp
using ModAPI.Core;
using ModAPI.Characters;

public class MyMod : IModPlugin
{
    private ICharacterEffectSystem _characters;

    public void Initialize(IPluginContext ctx)
    {
        _characters = ctx.Characters;
    }

    public void Start(IPluginContext ctx)
    {
    }
}
```

## 2. Character Types

`CharacterSource` tells you where a character comes from:

- `RealFamily`: a real `FamilyMember`
- `Visitor`: a real `NpcVisitor`
- `Synthetic`: pure data character created by mods

Use `CharacterState` for lifecycle and `CharacterLocation` for shelter/away context.

## 3. Accessing Real Characters

```csharp
FamilyMember member = FamilyManager.Instance.GetFamilyMember(1);
ICharacterProxy proxy = _characters.GetCharacter(member);

string name = proxy.Name;
CharacterState state = proxy.State;
CharacterLocation location = proxy.Location;
```

You can also query all tracked characters:

```csharp
var all = _characters.GetAllCharacters();
```

## 4. Creating Synthetic Characters

Persistent synthetic character:

```csharp
var citizen = _characters.CreateSyntheticCharacter(
    firstName: "Aria",
    lastName: "Stone",
    persistenceKey: "mymod.faction.citizen.001",
    sourceModId: "mymod",
    isPersistent: true
);
```

Temporary synthetic character:

```csharp
var tempNpc = _characters.CreateTemporaryCharacter(
    firstName: "Scout",
    lastName: "One",
    sourceModId: "mymod"
);
```

Important:

- Persistence keys must be unique.
- Use namespaced keys (`modid.something`) to avoid collisions.

## 5. Synthetic Lifecycle

```csharp
_characters.SyntheticCharacterCreated += c => { /* track */ };
_characters.SyntheticCharacterUnloaded += c => { /* cleanup */ };
```

Unload temporary synthetic characters when your encounter/session ends:

```csharp
int removed = _characters.UnloadTemporaryCharacters("mymod");
```

## 6. Character Data (including custom data)

`ICharacterData` contains structured fields plus arbitrary mod data:

```csharp
var c = _characters.GetSyntheticCharacter("mymod.faction.citizen.001");
c.Data.Health = 85;
c.Data.MaxHealth = 100;

c.Data.SetCustomData("mymod.rank", "captain");
c.Data.SetCustomData("mymod.reputation", 42);

string rank = c.Data.GetCustomData<string>("mymod.rank");
bool hasRep = c.Data.HasCustomData("mymod.reputation");
```

## 7. Effects and Attributes

Register effect types once:

```csharp
_characters.RegisterEffectType<MyBleedEffect>("mymod.bleed");
```

Apply/query/remove:

```csharp
ICharacterProxy target = _characters.GetCharacterById(1);

target.Effects.Apply("mymod.bleed", duration: 30f, sourceModId: "mymod");
bool bleeding = target.Effects.Has("mymod.bleed");
target.Effects.RemoveAllOfType("mymod.bleed");
```

Attributes:

```csharp
target.Attributes.Apply("Strength", 2f, 60f, "mymod");
float total = target.Attributes.GetModifier("Strength");
```

## 8. Query API

```csharp
var persistentSynthetic = _characters.Query()
    .FromSource(CharacterSource.Synthetic)
    .OnlyPersistent()
    .ToList();

var myModTemps = _characters.Query()
    .CreatedByMod("mymod")
    .OnlyTemporary()
    .ToList();
```

## 9. Encounter Swapping

`SwapEncounterCharacter(...)` performs a best-effort actor swap (name + combat-relevant stats where available through reflection):

```csharp
_characters.SwapEncounterCharacter(encounterActor, citizenProxy, swapped =>
{
    // Optional post-swap logic
});
```

Note:

- This is intentionally defensive due to changing internal game fields.
- If a field/property does not exist in a game build, it is skipped.

## 10. Events

```csharp
_characters.EffectApplied += (character, effect) => { };
_characters.EffectRemoved += (character, effect, reason) => { };
_characters.DataChanged += (character, key, value) => { };
```

## 11. Save/Load Behavior

- Persistent synthetic characters are saved/loaded by the character system.
- Temporary synthetic characters are in-memory only.
- Effect instance custom payload (`EffectInstance.CustomData`) is serialized/restored.
- `ICharacterData` custom data is serialized/restored for persistent synthetic characters.

## 12. Recommended Conventions

- Always namespace IDs and keys:
  - Effects: `modid.effectname`
  - Persistence keys: `modid.entity.type.id`
  - Custom data keys: `modid.key`
- Use `OnlyTemporary()` queries + `UnloadTemporaryCharacters(modId)` for cleanup discipline.
- Treat encounter swaps as best-effort patch points, not hard engine contracts.
