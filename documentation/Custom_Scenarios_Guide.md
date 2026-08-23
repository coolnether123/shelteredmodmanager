# Custom scenarios

ShelteredAPI supports installed custom scenarios, XML and code registration, playback, save binding, runtime triggers, and scoring snapshots. The optional `ShelteredScenarioEditor.dll` adds interactive authoring and defaults off.

Reference `ModAPI.dll` and `ShelteredAPI.dll`. A code scenario that returns `ScenarioDef` also references `Assembly-CSharp.dll`. Mods do not reference `ShelteredScenarioEditor.dll`.

## Choose XML or code

Use XML for starting survivors, inventory, shelter edits, triggers, quests, weather, dependencies, and win or loss conditions. Use code when the scenario needs custom selection behavior or a directly built `ScenarioDef`.

Give every scenario a stable, namespaced ID. Changing the ID disconnects installed saves from the scenario.

## Package an XML scenario

Place each scenario below the loaded mod's `Scenarios` folder:

```text
MyMod/
  About/
    About.json
  Scenarios/
    LongRoad/
      scenario.xml
      Assets/
        icon.png
```

A minimal file is:

```xml
<Scenario>
  <Meta>
    <Id>com.example.scenario.longroad</Id>
    <DisplayName>The Long Road</DisplayName>
    <Description>Start light and keep the shelter running.</Description>
    <Author>Example Author</Author>
    <Version>1.0.0</Version>
  </Meta>
  <Dependencies>
    <Requires id="com.example.content" version="1.0.0" />
  </Dependencies>
  <BaseMode>Survival</BaseMode>
  <StartingInventory>
    <OverrideRandomStart>true</OverrideRandomStart>
    <Items>
      <Item id="Water" quantity="4" />
      <Item id="Ration" quantity="2" />
    </Items>
  </StartingInventory>
</Scenario>
```

Asset paths are relative to the scenario folder. The catalog refreshes when the custom-scenario UI opens. A code registration wins when code and XML use the same scenario ID.

## Choose a launch mode

`LaunchSetup.Mode` accepts:

- `FullSetup`, which uses the vanilla setup flow and is the default for older XML;
- `Direct`, which skips difficulty and family customization;
- `Guided`, which keeps the vanilla flow but can lock authored category values.

Difficulty values are `0` through `3` for rain, resources, breach, faction, and mood. Map size uses `0` through `2`; fog uses `0` or `1`. Unknown category IDs survive an XML round trip and produce a validation warning.

## Register a code scenario

This example uses the current namespaces and declares its dependency once:

```csharp
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Public;

public sealed class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        ShelteredScenarios.Register(new LongRoadScenario());
    }
}

public sealed class LongRoadScenario : ShelteredCustomScenarioBase
{
    public override string Id { get { return "com.example.scenario.longroad"; } }
    public override string DisplayName { get { return "The Long Road"; } }
    public override string Description { get { return "Start light and keep the shelter running."; } }
    public override string Version { get { return "1.0.0"; } }

    public override ScenarioModDependency[] RequiredMods
    {
        get
        {
            return new[]
            {
                new ScenarioModDependency
                {
                    modId = "com.example.content",
                    version = "1.0.0"
                }
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

`ShelteredScenarios.Register(...)` replaces an existing registration with the same ID. `ScenarioRegistered` fires for both additions and replacements; `ScenarioUnregistered` does not fire for a replacement.

## Create or edit XML in code

Use the public authoring facade and definition models:

```csharp
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;

ScenarioDefinition definition =
    ShelteredScenarioAuthoring.CreateDefinition(ScenarioBaseGameMode.Survival);

definition.Id = "com.example.scenario.longroad";
definition.DisplayName = "The Long Road";
definition.Description = "Start light and keep the shelter running.";
definition.Author = "Example Author";
definition.Version = "1.0.0";
definition.StartingInventory.OverrideRandomStart = true;
definition.StartingInventory.Items.Add(
    new ItemEntry { ItemId = "Water", Quantity = 4 });

ScenarioValidationResult validation =
    ShelteredScenarioAuthoring.ValidateDefinition(definition, filePath);

if (validation.IsValid)
    ShelteredScenarioAuthoring.SaveDefinition(definition, filePath);
```

`SaveDefinition` writes and validates a same-directory temporary file before it replaces `scenario.xml`. When replacing a file, it leaves `scenario.xml.bak`. Use `TryLoadDefinitionWithRecovery(...)` to report recovery from that backup.

The authoring facade also provides `LoadDefinition`, `FromXml`, `ToXml`, `LoadDefinitionInfo`, `BumpVersion`, map-icon checks, reference indexing, story-flow analysis, and play-readiness checks. See the [API reference](API_Signatures_Reference.md) for the public members.

## Use triggers and scoring

Automatic trigger types include `immediate`, `startup`, `timeReached`, `dayReached`, `scenarioFlagSet`, quest state, survivor presence, item quantity, bunker expansion, and technology unlocks. Use `ShelteredScenarioRuntime.FireTrigger(triggerId)` for manual or code-driven triggers.

Quests that use `startTriggerId` start after that trigger fires. Do not also add `ScheduledStart` to the same quest.

The `<Scoring>` section declares labels, categories, and rules. ShelteredAPI does not calculate a generic score from those rules. Scenario code supplies the current result with `ShelteredScenarioRuntime.SetScoreSnapshot(...)`. The runtime persists the snapshot with the active scenario save and marks an existing snapshot won or lost when an outcome resolves.

## Catalog, dependencies, and saves

Installed scenarios appear in a paged custom-scenario browser. Missing or mismatched required mods lock the scenario until dependencies match. The browser also exposes separate save archives for Surrounded, Stasis, and each custom scenario. These archives do not replace the stock vanilla saves.

If an active save refers to missing XML, the runtime keeps the binding pending and retries after the catalog refreshes. Reinstalling a package with the same stable scenario ID reconnects its archived runs.

## Optional editor

Enable `ShelteredScenarioEditor.Enabled` in the desktop manager and restart the game to use interactive authoring. The Publish area can install the current export as a local test copy and uninstall that test copy. The editor does not own installed-scenario playback or saves.

Editor-only checklist data is stored beside a draft as `scenario.editor.xml`. Published packages exclude this sidecar. Mods must not read it.

## Test a scenario

Before publishing:

1. Validate and reload the XML.
2. Start a new run in each supported launch mode.
3. Save, return to the menu, and reload.
4. Test with a required mod missing and with a version mismatch.
5. Fire each trigger and resolve each win or loss condition.
6. Restart the game and confirm that the installed catalog and saves still bind to the same scenario ID.

For maintainer ownership and extension rules, see [Scenario authoring architecture](Scenario_Authoring_Architecture.md).
