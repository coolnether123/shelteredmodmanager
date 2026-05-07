# ShelteredAPI Runtime UI, Stores, and Cooking Stations

This guide covers the supported path for mod-owned panels and object-linked item flows. Use these APIs when a mod needs a fridge, container, cooking UI, quest inventory, event inventory, or any workflow where cloning vanilla NGUI would otherwise be required.

Canonical signatures: [API Signatures Reference](API_Signatures_Reference.md).

## Assembly Rule

- Reference `ModAPI.dll`.
- Reference `ShelteredAPI.dll`.
- Import the namespaces you use:

```csharp
using ShelteredAPI.Content;
using ShelteredAPI.Storage;
using ShelteredAPI.UI.Runtime;
using ShelteredAPI.Workstations;
```

## Design Rule

Runtime UI is mod-owned UI. Mods pass DTOs, callbacks, stores, and recipes. ShelteredAPI owns the NGUI objects, panel depth, refresh lifecycle, close handling, and object-menu integration.

Use this instead of:
- duplicating vanilla NGUI templates
- patching panel `Update` loops to keep copied UI alive
- storing custom item data inside vanilla objects that only save fixed fields

## Runtime Panel Chrome

Every runtime container or crafting panel can accept `RuntimePanelOptions`.

```csharp
var panelOptions = new RuntimePanelOptions
{
    Width = 700,
    Height = 540,
    Icon = ShelteredContent.AdvancedAssets.LoadSprite(
        typeof(MyPlugin).Assembly,
        "Assets/Icons/fridge.png"),
    Style = new RuntimePanelStyle
    {
        HeaderColor = new Color(0.10f, 0.16f, 0.18f, 1f),
        ButtonColor = new Color(0.18f, 0.24f, 0.26f, 1f)
    }
};
```

Unset values use ShelteredAPI defaults.

## Mod-Owned Stores

Use `ShelteredStores.ForMod(...)` for arbitrary stored contents. This is the right model for a custom fridge that can hold cooked meat, vegetables, quest items, or any other mod item.

```csharp
IItemStore fridge = ShelteredStores.ForMod(
    ownerId: "com.example.cooking",
    storeId: "kitchen.fridge",
    displayName: "Fridge",
    capacity: 24);

fridge.Add(VanillaItems.Meat, 3);
fridge.Add("com.example.cooked_meat", 1);
```

Mod stores are persisted by ShelteredAPI. Capacity is item-count based; use `0` for unlimited.

For stores attached to world objects, prefer `ForObject(...)`. It keys the store by the clicked object's stable `objectId`, so the fridge UI and the stove can both resolve the same backing store without putting custom items into `Obj_Freezer`.

```csharp
IItemStore fridge = ShelteredStores.ForObject(
    ownerId: "com.example.cooking",
    targetObject: context.TargetObject,
    displayName: "Fridge",
    capacity: 24);
```

From another object, such as a stove, use `FindNearestObjectStore(...)` to link to the nearest fridge-style store:

```csharp
IItemStore nearestFridge = ShelteredStores.FindNearestObjectStore(
    ownerId: "com.example.cooking",
    objectType: ObjectManager.ObjectType.Freezer,
    position: stove.transform.position,
    displayName: "Fridge",
    capacity: 24);
```

## Vanilla Store Adapters

ShelteredAPI also exposes adapters for real vanilla stores:

```csharp
IItemStore inventory = ShelteredStores.ForInventory();
IItemStore nearestFreezer = ShelteredStores.FindNearestFreezer(stove.transform.position);
```

Important: vanilla `Obj_Freezer` only supports `Meat` and `DesperateMeat`. The freezer adapter intentionally preserves that rule. Use a mod-owned store for a fridge with arbitrary item IDs.

## Container Panel

Create a container panel from any `IItemStore`:

```csharp
RuntimeUiHandle handle = ShelteredRuntimeUI.OpenContainer(
    ShelteredStores.CreateContainerRequest(
        store: fridge,
        ownerId: "com.example.cooking",
        panelId: "com.example.cooking.fridge.panel",
    title: "Fridge"));
```

The default helper wires the row transfer button between the container and shelter inventory. Use the overload that accepts a `transferStore` when the other side should be something else, or build `ContainerUiRequest` directly when you need custom buttons like "Deposit All" or quest-specific actions.

For custom transfer behavior, create `ContainerUiRequest` directly and use `ItemSource`, `OnTransferRequested`, and footer `Actions`.

## Object-Attached Fridge UI

Register an object interaction that opens a mod-owned fridge panel without editing the vanilla freezer UI.

```csharp
ShelteredRuntimeUI.RegisterObjectPanel(new ObjectPanelRegistration
{
    ObjectType = ObjectManager.ObjectType.Freezer,
    InteractionId = "com.example.cooking.open_fridge",
    InteractionText = "Open Fridge",
    Open = context =>
    {
        IItemStore store = ShelteredStores.ForObject(
            "com.example.cooking",
            context.TargetObject,
            "Fridge",
            24);

        ContainerUiRequest request = ShelteredStores.CreateContainerRequest(
            store,
            "com.example.cooking",
            "com.example.cooking.fridge." + context.TargetObject.objectId,
            "Fridge");
        request.PanelOptions = panelOptions;
        request.RefreshEveryFrame = true;
        return ShelteredRuntimeUI.OpenContainer(request);
    }
});
```

This keeps fridge content separate from `Obj_Freezer` internals while still attaching the UI to the world object.

## Cooking Station

Use `ShelteredCooking.RegisterStation(...)` when a world object should open a recipe panel backed by stores.

```csharp
ShelteredCooking.RegisterStation(new CookingStationRegistration
{
    OwnerId = "com.example.cooking",
    ObjectType = ObjectManager.ObjectType.Stove,
    InteractionId = "com.example.cooking.stove",
    InteractionText = "Cook",
    Title = "Stove",
    PanelOptions = panelOptions,
    JobOptions = new CookingStationJobOptions
    {
        DurationSeconds = 5f,
        AnimationTrigger = "Rummage",
        JobType = "cook_food",
        ClosePanelOnQueue = true,
        TargetIntegrityCost = 2
    },
    IngredientStore = context =>
        ShelteredStores.FindNearestObjectStore(
            "com.example.cooking",
            ObjectManager.ObjectType.Freezer,
            context.TargetObject.transform.position,
            "Fridge",
            24)
        ?? ShelteredStores.ForInventory(),
    OutputStore = context => ShelteredStores.ForInventory(),
    RecipeSource = context => new[]
    {
        new CookingStationRecipe
        {
            RecipeId = "com.example.cooking.meat_to_ration",
            DisplayName = "Cook Meat",
            Subtitle = "Turns stored meat into rations",
            DurationSeconds = 5f,
            OutputItemId = VanillaItems.Ration,
            OutputCount = 1,
            Ingredients = new[]
            {
                new RecipeIngredient { ItemId = VanillaItems.Meat, Count = 1 }
            }
        }
    },
    OnCrafted = craft =>
    {
        if (craft.Result != null && craft.Result.Success)
            craft.Panel.Refresh();
    }
});
```

With `JobOptions` enabled, pressing Cook queues a real character job. ShelteredAPI picks the selected idle member, or the first idle family member, walks them to the workstation, shows vanilla interaction progress, plays the configured animation, and only applies the recipe when the timer completes. If the job is cancelled, the recipe is not applied.

Without `JobOptions`, the default behavior applies immediately: it checks ingredients, removes them, adds the output, rolls back consumed ingredients if output fails, and refreshes the panel.

Use `OnCraftQueued` for UI/sound feedback when the job is accepted, `OnCrafted` for completion behavior, and `OnCraftFailed` for missing ingredients, full output stores, or cancelled jobs.

## What This Does Not Hide

- Vanilla cooking is an eating-stage interaction, not a real crafting station.
- Vanilla freezers cannot store arbitrary item IDs.
- ShelteredAPI timed cooking jobs are runtime jobs. If a save is loaded while one is in progress, the vanilla queue loader does not know how to restore ShelteredAPI callback state, so the job may safely fall back to a no-op/failed vanilla-style job instead of duplicating outputs.
