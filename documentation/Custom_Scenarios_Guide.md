# Custom Scenarios Guide

Custom scenarios can be authored in two ways:

- code registration through `ICustomScenarioService`
- XML packs discovered from a loaded mod's `Scenarios/**/scenario.xml` files

Both paths appear under the in-game `Custom Scenarios` scenario-selection hub. Missing or version-mismatched required mods are shown as locked entries and cannot be started until the dependency state matches. The hub is always available from the scenario book, even when there are no existing custom scenarios, so authors can use `Add New Scenario`.

## Code-Driven Registration

Reference `ModAPI.dll` and `ShelteredAPI.dll`, then register during plugin startup.

```csharp
using ModAPI.Core;
using ModAPI.Saves;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios;

public sealed class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        ICustomScenarioService scenarios;
        if (!ModAPIRegistry.TryGetAPI(GameRuntimeApiIds.CustomScenarios, out scenarios))
            return;

        CustomScenarioRegistration registration = new LongRoadScenario().ToRegistration();
        registration.RequiredMods = new[]
        {
            new LoadedModInfo { modId = "com.example.content", version = "1.2.0" }
        };

        scenarios.Register(registration);
    }
}

public sealed class LongRoadScenario : ShelteredCustomScenarioBase
{
    public override string Id { get { return "com.example.scenario.longroad"; } }
    public override string DisplayName { get { return "The Long Road"; } }
    public override string Description { get { return "Survive with a reduced start and a long objective chain."; } }
    public override string Version { get { return "1.0.0"; } }

    public override ScenarioDef BuildDefinition(CustomScenarioBuildContext context)
    {
        return CreateDefinition()
            .SetId(Id)
            .SetNameKey(DisplayName)
            .SetDescriptionKey(Description)
            .UseInModes(true, false, false)
            .AddSimpleStage("longroad_intro")
            .Build();
    }
}
```

`ShelteredCustomScenarioBase.ToRegistration()` maps `Id`, `DisplayName`, `Description`, `Version`, `Order`, `UserData`, `BuildDefinition`, `OnSelected`, and `OnSpawned` into `CustomScenarioRegistration`.

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
    <Requires id="com.example.content" version="1.2.0" />
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

## Scenario Book Browser

The scenario book adds a `Custom Scenarios` button. Selecting it replaces the vanilla scenario buttons with:

- a fixed-size paged list that reuses the vanilla scenario button count for visible custom scenarios
- a dedicated `Add New Scenario` button
- save-style `< Previous`, `Next >`, and `Page X / Y` controls

This keeps the browser usable for arbitrarily large scenario catalogs without instantiating one on-screen button per scenario, and it behaves like the regular custom-save paging flow.

`Add New Scenario` starts `ScenarioEditorController.EnterEditMode(ScenarioBaseGameMode.Survival)` and creates an in-memory draft with the default id `com.author.scenario.new`. The editor backend can create, load, validate, save, and playtest scenario definitions, but the full visual authoring form for editing every field from this menu is still a separate UI task.

## Dependencies And UI Blocking

Dependencies use the same shape as save verification:

```xml
<Requires id="com.example.content" version="1.2.0" />
```

or the compact string form used by the serializer:

```xml
<Requires>com.example.content@1.2.0</Requires>
```

The scenario list labels unsatisfied entries as `[LOCKED]`. The description states whether required mods are missing, version-mismatched, or unverifiable. Starting is blocked by `ShelteredCustomScenarioService.MarkSelected`; even if a confirmation window is shown, a mismatch does not leave pending scenario state behind.

## Save Binding Behavior

When a custom scenario successfully spawns, the runtime stores a `ScenarioRuntimeBinding` in the save:

- `ScenarioId`
- `VersionApplied`
- `IsActive = true`
- `IsConvertedToNormalSave = false`
- `DayCreated`

Failed spawns, dependency failures, and canceled startup flows clear pending scenario state and do not write a new binding. On later loads, active bindings let ShelteredAPI re-load the XML definition by `ScenarioId` and apply supported scenario data after the world is ready. Code-only scenarios still keep identity/version metadata in the save, but reload-time XML application requires a matching `scenario.xml` pack.

## Current Apply Support

Applied now:

- family names and gender
- base stats: `Strength`, `Dexterity`, `Intelligence`, `Charisma`, `Perception`
- traits using `Strength:TraitName` or `Weakness:TraitName`
- starting inventory items resolvable by `InventoryHelper.ResolveItemType`
- bunker wall and wiring sprite indexes
- vanilla object placements by `ObjectManager.ObjectType` via `definition="Generator"` and optional `level`, `movable`, `lockDeconstruct` properties
- asset path validation and sprite preloading

Explicitly deferred:

- skills, because Sheltered does not expose a stable runtime skill/save API comparable to `BaseStats` and `Traits`
- direct `PrefabReference` object placement, because raw prefab-path instantiation can create unsaved or invalid live objects
- triggers, because the XML schema does not yet define a safe runtime action target
- win/loss conditions, because active bindings do not yet persist the spawned `QuestInstance` id needed to complete or fail the scenario safely

Deferred categories are reported through `ScenarioApplyResult.Messages`.

## Compatibility Notes

The framework targets .NET Framework 3.5 and uses `System.Xml` for XML parsing. Keep scenario ids stable across versions. Version changes should be reflected in `CustomScenarioRegistration.Version` or `<Version>` so new saves record the applied scenario version. Required mod version checks are exact, case-insensitive string comparisons.

Asset paths must be relative to the scenario pack folder. Paths that escape the pack folder, including sibling-prefix attempts such as `../Pack2/file.png`, are rejected even if the target file exists.

Run the built-in harness from a debug mod or immediate window when validating the framework:

```csharp
ScenarioValidationResult result = ScenarioFrameworkVerification.Run();
```

`result.IsValid` is `false` if round-trip serialization, dependency verification, catalog discovery, or asset escape validation fails.
