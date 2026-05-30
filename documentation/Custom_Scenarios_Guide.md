# Custom Scenarios Guide (v2.0, Experimental)

The 2.0 line is a breaking clean API line. Custom scenarios are experimental in 2.0 and should be tested with disposable saves before being used in long-running playthroughs.

Exact scenario signatures are in [API Signatures Reference](API_Signatures_Reference.md); use this guide for authoring flow and behavior. Reference choices and facade rules are centralized in the canonical [assembly boundary](README.md#assembly-boundary-canonical).

Custom scenarios can be authored in two ways:

- code registration through `ShelteredScenarios`
- XML packs discovered from a loaded mod's `Scenarios/**/scenario.xml` files

Choose XML when the scenario is mostly data: starting family, inventory, bunker edits, triggers, quests, and win/loss conditions. Choose code registration when you need runtime logic, custom selection behavior, or direct construction of a Sheltered `ScenarioDef`.

Authoring checklist:

1. Pick a stable scenario id, such as `com.author.mod.scenario.longroad`.
2. Put XML packs under `Scenarios/<ScenarioName>/scenario.xml`, or register code scenarios from `Start(...)`.
3. Add required mod dependencies when the scenario depends on another content pack.
4. Keep asset paths relative to the scenario pack folder.
5. Test a new game, save/load, dependency-missing startup, and return-to-menu/reload.

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

XML packs are refreshed when the custom scenario UI opens. If a code registration and an XML pack share the same scenario id, the code registration wins. If an active save binding references an XML scenario that is missing at load time, the runtime keeps that binding in a blocked pending state and retries after the catalog refreshes in the same session.

### Triggers

`<Trigger>` definitions become persisted runtime signals. Automatic trigger types include `immediate`, `startup`, `timeReached`/`dayReached`, `scenarioFlagSet`, `questActive`, `questCompleted`, `questFailed`, `survivorPresent`, `itemQuantityAvailable`, `bunkerExpansionUnlocked`, and `technologyUnlocked`. `manual`, `custom`, blank, and `code` triggers are reserved for explicit `FireTrigger` scheduled effects or mod code calling `ShelteredScenarioRuntime.FireTrigger("triggerId")`.

Quests with `startTriggerId` are now scheduled behind a `CustomTrigger` condition and start after the referenced trigger fires. To avoid ambiguous starts, omit `<ScheduledStart>` on trigger-started quests.

### Scoring Metadata And Snapshots

Custom scenario scoring is authoring metadata plus an optional per-save runtime snapshot. The v2.0 foundation does not reuse Sheltered's Survival EOS leaderboard or the Surrounded/Stasis result panels; those are mode-specific vanilla implementations, not a generic scenario scoring API.

Use `<Scoring>` to declare the score label, ordering preference, categories, and neutral rules that an editor or future detail/leaderboard UI can display:

```xml
<Scoring enabled="true" scoreLabel="Points" higherIsBetter="true" leaderboardKey="longroad">
  <Categories>
    <Category id="survival" displayName="Survival" sortOrder="10" />
  </Categories>
  <Rules>
    <Rule id="days-survived" categoryId="survival" displayName="Days Survived" source="daysSurvived" operation="Add" weight="1">
      <Properties>
        <Property key="metric" value="GameTime.Day" />
      </Properties>
    </Rule>
  </Rules>
  <Metadata>
    <Property key="notes" value="Scenario code supplies the runtime score snapshot." />
  </Metadata>
</Scoring>
```

Rules are deliberately neutral. `source`, `operation`, `weight`, and rule properties describe how scenario code should evaluate the score, but ShelteredAPI does not guess at vanilla formulas or run a generic score calculator yet.

Scenario runtime code can persist a score snapshot in the active save:

```csharp
ScenarioScoreSnapshot snapshot = new ScenarioScoreSnapshot();
snapshot.HasTotalScore = true;
snapshot.TotalScore = 1200;
snapshot.CompletionState = ScenarioScoreCompletionState.InProgress;
snapshot.Categories.Add(new ScenarioScoreCategorySnapshot
{
    CategoryId = "survival",
    DisplayName = "Survival",
    Score = 1200
});

ShelteredScenarioRuntime.SetScoreSnapshot(snapshot);
```

`SetScoreSnapshot` fills missing scenario identity/version/runtime binding fields from the active custom scenario state and stamps the current game time when no snapshot time is supplied. `GetScoreSnapshot` returns a defensive copy, and `ClearScoreSnapshot` removes the persisted snapshot. When win/loss conditions resolve an active custom scenario, an existing snapshot is marked `Won` or `Lost` and receives the outcome condition id.

## Scenario Book Browser

The scenario book adds a `Custom Scenarios` button. Selecting it replaces the vanilla scenario buttons with:

- a fixed-size paged list that reuses the vanilla scenario button count for visible custom scenarios
- a dedicated `Add New Scenario` button
- save-style `< Previous`, `Next >`, and `Page X / Y` controls

This keeps the browser usable for arbitrarily large scenario catalogs without instantiating one on-screen button per scenario, and it behaves like the regular custom-save paging flow.

`Add New Scenario` creates an in-memory draft with the default id `com.author.scenario.new` through internal browser/editor services. XML and code authors should use `ShelteredScenarioAuthoring` to create, load, validate, save, and run framework verification for scenario definitions; browser controllers and editor backend services are not public API. The Survivors workspace exposes the character editor for both starting crew and future survivors: add/remove starting people, move the start crew order, cycle names/gender/age, step individual stats, cycle strength/weakness traits, copy full identity from a selected live family member, and copy or clear appearance. Future survivors use the same character editor row underneath their arrival scheduling controls.

## Public XML Authoring API

Use `ShelteredScenarioAuthoring` for XML scenario files and in-memory XML edits. Use `ShelteredScenarios` for the live catalog/registration surface. The editor controllers, browser controllers, serializers, validators, and runtime apply services are internal implementation details.

Create a new XML-backed definition:

```csharp
ScenarioDefinition definition = ShelteredScenarioAuthoring.CreateDefinition(ScenarioBaseGameMode.Survival);
definition.Id = "com.example.scenario.longroad";
definition.DisplayName = "The Long Road";
definition.Description = "Start light and survive seven days.";
definition.Author = "Example Author";
definition.Version = "1.0.0";

definition.StartingInventory.OverrideRandomStart = true;
definition.StartingInventory.Items.Add(new ItemEntry { ItemId = "Water", Quantity = 4 });

ScenarioValidationResult validation = ShelteredScenarioAuthoring.ValidateDefinition(definition, filePath);
if (validation.IsValid)
    ShelteredScenarioAuthoring.SaveDefinition(definition, filePath);
```

`SaveDefinition` never overwrites the live `scenario.xml` directly. It writes a same-directory temp file, parses that temp file back into a scenario definition, then replaces the live file and leaves `scenario.xml.bak` when replacing an existing file. If a write or replace fails, the previous `scenario.xml` remains in place. If loading a scenario file fails and a `.bak` exists, the loader reports the backup path instead of silently discarding local edits.

Edit an existing XML scenario file:

```csharp
ScenarioDefinition definition = ShelteredScenarioAuthoring.LoadDefinition(filePath);
definition.DisplayName = "The Long Road - Revised";
definition.ModDependencies.Add(new ScenarioModDependencyDefinition
{
    ModId = "com.example.content",
    Version = "1.0.0",
    Kind = ScenarioModDependencyKind.Required,
    Manual = true
});

ScenarioValidationResult validation = ShelteredScenarioAuthoring.ValidateDefinition(definition, filePath);

if (validation.IsValid)
    ShelteredScenarioAuthoring.SaveDefinition(definition, filePath);
```

Convert between XML text and DTOs without touching disk:

```csharp
ScenarioDefinition definition = ShelteredScenarioAuthoring.FromXml(xmlText);
string updatedXml = ShelteredScenarioAuthoring.ToXml(definition);
```

Work with the loaded mod XML catalog:

```csharp
ShelteredScenarios.RefreshXmlDefinitions();
ScenarioInfo[] xmlScenarios = ShelteredScenarios.ListXmlDefinitions();

ScenarioDefinition definition;
string scenarioFilePath;
ScenarioValidationResult validation;
if (ShelteredScenarioAuthoring.TryLoadXmlDefinition(
    "com.example.scenario.longroad",
    out definition,
    out scenarioFilePath,
    out validation))
{
    // The definition is valid and came from scenarioFilePath.
}
```

`TryLoadXmlDefinition` returns `false` when the scenario id is not indexed, the XML cannot be read, or validation has errors. In all cases, inspect the returned `ScenarioValidationResult` for author-facing messages. `ValidateXmlDefinition(scenarioId)` is useful when you only need the validation result for a catalog entry.

## Dependencies And UI Blocking

Dependencies use the same shape as save verification:

```xml
<Requires id="com.example.content" version="1.0.0" />
```

or the compact string form used by the serializer:

```xml
<Requires>com.example.content@1.0.0</Requires>
```

Typed dependency declarations are also supported:

```xml
<Dependencies>
  <ModDependency id="com.example.content" version="1.0.0" kind="Required" manual="true" />
  <ModDependency id="com.example.cosmetics" kind="Optional" manual="true" />
</Dependencies>
```

Only required dependencies block startup. Optional dependencies are shown in compatibility reporting but do not lock the scenario. Required `<Requires>` and required `<ModDependency>` entries are both included in the save-style dependency manifest used by the scenario browser.

The scenario list labels unsatisfied entries as `[LOCKED]`. The description states whether required mods are missing, version-mismatched, or unverifiable. Starting is blocked by `ShelteredCustomScenarioService.MarkSelected`; even if a confirmation window is shown, a mismatch does not leave pending scenario state behind.

## Save Binding Behavior

When a custom scenario successfully spawns, the runtime stores a `ScenarioRuntimeBinding` in the save:

- `ScenarioId`
- `VersionApplied`
- `IsActive = true`
- `IsConvertedToNormalSave = false`
- `DayCreated`
- `ScenarioQuestInstanceId` after the `ScenarioDef` is spawned successfully

If scenario code publishes a score snapshot through `ShelteredScenarioRuntime.SetScoreSnapshot`, the save also stores `HasScoreSnapshot` and a nested `ScoreSnapshot` with completion state, outcome, optional total score, category rows, rule rows, and metadata. Existing saves without these fields continue loading as unscored saves.

Failed spawns, dependency failures, and canceled startup flows clear pending scenario state and do not write a new binding. On later loads, active bindings let ShelteredAPI re-load the XML definition by `ScenarioId` and apply supported scenario data after the world is ready. Code-only scenarios still keep identity/version metadata in the save, but reload-time XML application requires a matching `scenario.xml` pack.

Public custom-scenario save helpers such as `ShelteredSaves.ListScenario`, `GetScenario`, `CreateScenario`, `OverwriteScenario`, and `DeleteScenario` only accept custom scenario ids. Reserved built-in ids, including `Standard`, `Vanilla.Surrounded`, `Vanilla.Stasis`, and `ScenarioAuthoringDrafts`, throw before resolving any save path. Use explicit built-in helpers such as `ListStandard`, `GetStandard`, `OverwriteStandard`, and `DeleteStandard` when your mod intentionally works with standard save slots.

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

XML parsing rejects DTD and external entity declarations. Scenario XML should be plain data under a `<Scenario>` root; do not rely on external XML entities or document type declarations.

`result.IsValid` is `false` if round-trip serialization, dependency verification, catalog discovery, secure XML parsing, atomic XML replacement, or asset escape validation fails.
