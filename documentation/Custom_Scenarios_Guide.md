# Custom Scenarios Guide

The 1.3 line is a breaking clean API line.

## Assembly Rule

- Always reference `ModAPI.dll`.
- Reference `ShelteredAPI.dll` when your mod uses Sheltered content, saves, UI, input, events, actors, or scenarios.

## API Stability Rules

- Public facades are stable.
- Implementation classes are internal.
- Typed Sheltered escape hatches are explicit.
- Future migrations should happen behind facades.

Custom scenarios can be authored in two ways:

- code registration through `ShelteredScenarios`
- XML packs discovered from a loaded mod's `Scenarios/**/scenario.xml` files

Both paths appear under the in-game `Custom Scenarios` scenario-selection hub. Missing or version-mismatched required mods are shown as locked entries and cannot be started until the dependency state matches. The hub is always available from the scenario book, even when there are no existing custom scenarios, so authors can use `Add New Scenario`.

`ModAPI.Scenarios` is the neutral registration and lifecycle surface: custom scenario registrations, opaque definition factories, lifecycle state/events, portable catalog metadata, dependency manifest conversion, and validation result containers. `ShelteredAPI.Scenarios` is the Sheltered scenario authoring/runtime pack: Sheltered XML definitions, family/survivor/bunker/inventory/quest/weather sections, the `ShelteredScenarios`, `ShelteredScenarioAuthoring`, and `ShelteredScenarioRuntime` facades, plus the `ShelteredScenarioDefBuilder` escape hatch. Serializers, validators, runtime binding, browser controllers, and apply services are implementation details.

## Code-Driven Registration

Reference `ModAPI.dll` for neutral registration contracts and `ShelteredAPI.dll` for Sheltered scenario authoring/runtime helpers. Code-driven scenarios that return `ScenarioDef` also need a compile reference to `Assembly-CSharp.dll`.

```csharp
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios;

public sealed class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        CustomScenarioRegistration registration = new LongRoadScenario().ToRegistration();
        registration.RequiredMods = new[]
        {
            new ScenarioModDependency { modId = "com.example.content", version = "1.0.0" }
        };

        ShelteredScenarios.Register(registration);
    }
}

public sealed class LongRoadScenario : ShelteredCustomScenarioBase
{
    public override string Id { get { return "com.example.scenario.longroad"; } }
    public override string DisplayName { get { return "The Long Road"; } }
    public override string DisplayNameKey { get { return "scenario.example.longroad.name"; } }
    public override string Description { get { return "Survive with a reduced start and a long objective chain."; } }
    public override string DescriptionKey { get { return "scenario.example.longroad.desc"; } }
    public override string Version { get { return "1.0.0"; } }

    public override ScenarioModDependency[] RequiredMods
    {
        get
        {
            return new[]
            {
                new ScenarioModDependency { modId = "com.example.content", version = "1.0.0" }
            };
        }
    }

    public override ScenarioDef BuildDefinition(CustomScenarioBuildContext context)
    {
        return CreateDefinition()
            .UseInModes(true, false, false)
            .OnceOnly(false)
            .AddSimpleStage("longroad_intro")
            .Build();
    }
}
```

`ShelteredCustomScenarioBase.ToRegistration()` maps `Id`, `DisplayName`, `Description`, `Version`, `Order`, `UserData`, `BuildDefinition`, `OnSelected`, and `OnSpawned` into `CustomScenarioRegistration`.
`DisplayName` and `Description` are the registration/browser text. `DisplayNameKey` and `DescriptionKey` are the localization keys written into the in-game `ScenarioDef`; they default to the display text for backward compatibility. Class-based scenarios can expose required mod metadata by overriding `ShelteredCustomScenarioBase.RequiredMods` or by implementing `IShelteredCustomScenarioDependencies` directly. The registration helper clones that array before storing it.

`ScenarioRegistered` fires for both new registrations and replacements. `ScenarioUnregistered` does not fire when a registration is replaced. Event listeners cannot distinguish add vs. replace from the event alone; only the direct `Register` caller receives `CustomScenarioRegistrationResult.ReplacedExisting`.

## XML Scenario Packs

Place `scenario.xml` below a loaded mod's `Scenarios` folder:

```text
MyMod/
  About/
  Assemblies/
  Scenarios/
    LongRoad/
      scenario.xml
      Assets/
        icon.png
```

Minimal XML:

```xml
<Scenario>
  <Meta>
    <Id>com.example.scenario.xml.longroad</Id>
    <DisplayName>XML Long Road</DisplayName>
    <Description>Start light, survive seven days, and keep the shelter running.</Description>
    <Author>Example Author</Author>
    <Version>1.0.0</Version>
  </Meta>
  <Dependencies>
    <Requires id="com.example.content" version="1.0.0" />
  </Dependencies>
  <BaseMode>Survival</BaseMode>
  <FamilySetup>
    <OverrideVanillaFamily>false</OverrideVanillaFamily>
    <Members>
      <Member>
        <Name>Alex</Name>
        <Gender>Female</Gender>
        <Stats>
          <Stat id="Strength" value="7" />
        </Stats>
        <Traits>
          <Trait>Strength:Courageous</Trait>
        </Traits>
      </Member>
    </Members>
  </FamilySetup>
  <StartingInventory>
    <OverrideRandomStart>true</OverrideRandomStart>
    <Items>
      <Item id="Water" quantity="4" />
      <Item id="Ration" quantity="2" />
    </Items>
  </StartingInventory>
  <BunkerEdits>
    <RoomChanges>
      <RoomEdit gridX="0" gridY="0" wallSpriteIndex="1" />
    </RoomChanges>
    <ObjectPlacements>
      <ObjectPlacement definition="Generator">
        <Position x="2" y="-4" z="0" />
        <CustomProperties>
          <Property key="level" value="1" />
          <Property key="movable" value="true" />
        </CustomProperties>
      </ObjectPlacement>
    </ObjectPlacements>
  </BunkerEdits>
  <AssetReferences>
    <CustomIcons>
      <Icon id="scenarioIcon" path="Assets/icon.png" />
    </CustomIcons>
  </AssetReferences>
</Scenario>
```

XML packs are refreshed when the custom scenario UI opens. If a code registration and an XML pack share the same scenario id, the code registration wins.

### Triggers

`<Trigger>` definitions become persisted runtime signals. Automatic trigger types include `immediate`, `startup`, `timeReached`/`dayReached`, `scenarioFlagSet`, `questActive`, `questCompleted`, `questFailed`, `survivorPresent`, `itemQuantityAvailable`, `bunkerExpansionUnlocked`, and `technologyUnlocked`. `manual`, `custom`, blank, and `code` triggers are reserved for explicit `FireTrigger` scheduled effects or mod code calling `ShelteredScenarioRuntime.FireTrigger("triggerId")`.

Quests with `startTriggerId` are now scheduled behind a `CustomTrigger` condition and start after the referenced trigger fires. To avoid ambiguous starts, omit `<ScheduledStart>` on trigger-started quests.

## Scenario Book Browser

The scenario book adds a `Custom Scenarios` button. Selecting it replaces the vanilla scenario buttons with:

- a fixed-size paged list that reuses the vanilla scenario button count for visible custom scenarios
- a dedicated `Add New Scenario` button
- save-style `< Previous`, `Next >`, and `Page X / Y` controls

This keeps the browser usable for arbitrarily large scenario catalogs without instantiating one on-screen button per scenario, and it behaves like the regular custom-save paging flow.

`Add New Scenario` creates an in-memory draft with the default id `com.author.scenario.new` through internal browser/editor services. XML and code authors should use `ShelteredScenarioAuthoring` to create, load, validate, save, and run framework verification for scenario definitions; browser controllers and editor backend services are not public API. The Survivors workspace exposes the character editor for both starting crew and future survivors: add/remove starting people, move the start crew order, cycle names/gender/age, step individual stats, cycle strength/weakness traits, copy full identity from a selected live family member, and copy or clear appearance. Future survivors use the same character editor row underneath their arrival scheduling controls.

Public XML authoring helpers:

```csharp
ScenarioDefinition definition = ShelteredScenarioAuthoring.LoadDefinition(filePath);
ScenarioValidationResult validation = ShelteredScenarioAuthoring.ValidateDefinition(definition, filePath);

if (validation.IsValid)
    ShelteredScenarioAuthoring.SaveDefinition(definition, filePath);
```

## Dependencies And UI Blocking

Dependencies use the same shape as save verification:

```xml
<Requires id="com.example.content" version="1.0.0" />
```

or the compact string form used by the serializer:

```xml
<Requires>com.example.content@1.0.0</Requires>
```

The scenario list labels unsatisfied entries as `[LOCKED]`. The description states whether required mods are missing, version-mismatched, or unverifiable. Starting is blocked by `ShelteredCustomScenarioService.MarkSelected`; even if a confirmation window is shown, a mismatch does not leave pending scenario state behind.

## Save Binding Behavior

When a custom scenario successfully spawns, the runtime stores a `ScenarioRuntimeBinding` in the save:

- `ScenarioId`
- `VersionApplied`
- `IsActive = true`
- `IsConvertedToNormalSave = false`
- `DayCreated`
- `ScenarioQuestInstanceId` after the `ScenarioDef` is spawned successfully

Failed spawns, dependency failures, and canceled startup flows clear pending scenario state and do not write a new binding. On later loads, active bindings let ShelteredAPI re-load the XML definition by `ScenarioId` and apply supported scenario data after the world is ready. Code-only scenarios still keep identity/version metadata in the save, but reload-time XML application requires a matching `scenario.xml` pack.

## Current Apply Support

Applied now:

- family names and gender
- extra starting family members when `OverrideVanillaFamily` defines more people than the vanilla startup spawned
- base stats: `Strength`, `Dexterity`, `Intelligence`, `Charisma`, `Perception`
- traits using `Strength:TraitName` or `Weakness:TraitName`
- future survivor auto-join spawns and ask-to-join recruit arrivals using the same name, gender, stat, trait, and appearance config shape as starting family members
- starting inventory items resolvable by `ShelteredContent.ResolveItemType` from `ShelteredAPI.dll`
- bunker wall and wiring sprite indexes
- vanilla object placements by `ObjectManager.ObjectType` via `definition="Generator"` and optional `level`, `movable`, `lockDeconstruct` properties
- asset path validation and sprite preloading
- trigger runtime state: automatic trigger definitions can fire persisted `CustomTrigger` records, scheduled actions can use `FireTrigger`, and code can call `ShelteredScenarioRuntime.FireTrigger(...)`
- trigger-started quests through `StartTriggerId`; the quest starts after the referenced trigger has fired
- win/loss conditions when the active binding has a spawned `ScenarioQuestInstanceId`; supported condition types are `surviveDays`, `timeReached`/`dayReached`, `itemQuantityAvailable`, `questActive`, `questCompleted`, `questFailed`, `survivorPresent`, `bunkerExpansionUnlocked`, `technologyUnlocked`, `scenarioFlagSet`, and `customTrigger`

Explicitly deferred:

- skills, because Sheltered does not expose a stable runtime skill/save API comparable to `BaseStats` and `Traits`
- direct `PrefabReference` object placement, because raw prefab-path instantiation can create unsaved or invalid live objects
- direct quest `CompletionConditionId` completion loops, because completion still depends on authored scheduled actions or win/loss conditions rather than an automatic per-quest completion adapter

Deferred categories are reported through `ScenarioApplyResult.Messages`.

## Compatibility Notes

The Sheltered scenario pack targets .NET Framework 3.5 and uses `System.Xml` for XML parsing. Keep scenario ids stable across versions. Version changes should be reflected in `CustomScenarioRegistration.Version` or `<Version>` so new saves record the applied scenario version. Required mod version checks are exact, case-insensitive string comparisons.

Asset paths must be relative to the scenario pack folder. Paths that escape the pack folder, including sibling-prefix attempts such as `../Pack2/file.png`, are rejected even if the target file exists.

`ShelteredScenarioDefBuilder` uses reflection to write Sheltered's private `ScenarioDef` fields. Missing critical fields now throw `InvalidOperationException` instead of returning a partial definition. Use `ShelteredScenarioDefBuilder.CheckCompatibility()` when diagnosing game-version mismatches; `DescribeFailures()` reports missing reflected fields. Selection-related fields are only required after calling `UseInModes` or `OnceOnly`.

Run the built-in harness from a debug mod or immediate window when validating the framework:

```csharp
ScenarioValidationResult result = ShelteredScenarioAuthoring.RunFrameworkVerification();
```

`result.IsValid` is `false` if round-trip serialization, dependency verification, catalog discovery, or asset escape validation fails.

Manual refactor verification covered:

- registration validation for null, missing id, missing display name, missing definition/factory, and non-`ScenarioDef` definitions
- replacement result and event semantics
- `List()` ordering by order, display name, then id
- dependency manifest merge from registration and XML definitions
- pending spawn dependency failure clearing state
- definition factory wrong-type and thrown-exception error messages
- builder compatibility failure paths for missing stages and selection fields
