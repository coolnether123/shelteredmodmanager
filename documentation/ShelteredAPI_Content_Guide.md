# ShelteredAPI content guide

Use `ShelteredAPI.Content` to register items, recipes, loot, assets, and localized content. See the [assembly boundary](README.md#assembly-boundary-canonical) before you add references.

> Dev/API-preview warning: runtime stores and cooking stations are preview behavior in the current 2.0 line. Use them for mod-author testing, but expect small signature or behavior changes before these APIs are declared stable.

## 1. What lives here

`ShelteredAPI.Content` owns the Sheltered-specific content runtime:
- item registration metadata
- deterministic custom item type assignment
- asset loading for icons and prefabs
- runtime injection into `ItemManager`
- runtime injection into `CraftingManager`
- inventory slot expansion for custom items
- inventory helper operations and string-ID-to-`ItemType` resolution for Sheltered runtime items
- content-localized text fallback and generated keys
- loot table injection

## 2. What mod authors must do

Every content mod should follow these rules:

1. Reference `ShelteredAPI.dll` and import `ShelteredAPI.Content`.
2. Register items, recipes, cooking recipes, loot entries, and patches in `Start(...)`.
3. Give every custom item a stable explicit string ID.
4. Provide display name text or a display name localization key.
5. Keep asset paths relative to the mod root, usually under `Assets/...`.
6. Treat `ItemManager.ItemType` as runtime-owned. Do not hardcode numeric custom IDs unless you have a specific compatibility reason.
7. Resolve items by string ID in gameplay code when possible.
8. Use `ShelteredContent` for runtime inventory helpers and item-ID resolution.

What mod authors should not do:
- do not register content in constructors
- do not assume the game's enum iteration will discover your item automatically
- do not patch core managers to add item definitions unless you are extending the framework itself
- do not use raw text in key-only fields if you want consistent localization behavior

## 3. Minimal item example

```csharp
using ModAPI.Core;
using ShelteredAPI.Content;
using ContentItemDefinition = ShelteredAPI.Content.ItemDefinition;

public class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        var item = new ContentItemDefinition()
            .WithId("com.mymod.power_cell")
            .WithDisplayNameText("Power Cell")
            .WithDescriptionText("A high-capacity energy cell")
            .WithCategory(ItemCategory.Normal)
            .WithStackSize(10)
            .WithTradeValue(120)
            .WithScrapValue(5f)
            .WithIcon("Assets/Icons/power_cell.png");

        var result = ShelteredContent.RegisterItem(item);
        if (!result.Success)
        {
            ctx.Log.Error("Item registration failed: " + result.ErrorMessage);
            return;
        }

        ShelteredContent.RegisterRecipe(
            new RecipeDefinition()
                .WithId("com.mymod.recipe.power_cell")
                .WithResultItem("com.mymod.power_cell")
                .WithStation(CraftStation.Workbench)
                .WithLevel(1)
                .WithCraftTime(45f)
                .WithIngredient(VanillaItems.Component, 3)
                .WithIngredient(VanillaItems.Metal, 2));
    }
}
```

### Storage and cooking

Do not force custom food into `Obj_Freezer`. Vanilla freezers use fixed fields and item types. Use a mod-owned object store for custom storage and the shelter inventory adapter for global output.

The [runtime UI and stores guide](ShelteredAPI_Runtime_UI_Stores_Guide.md#minimal-fridge-backed-cooking-flow) contains the complete cooking-station example.

## 4. Item registration checklist

Before calling `ShelteredContent.RegisterItem(...)`, make sure the item has:
- a stable `Id`
- a display name source
  - `WithDisplayNameKey(...)`
  - `WithDisplayNameText(...)`
- a sensible `Category`
- a valid icon path if the item should be visible in UI

Optional but commonly useful:
- `WithDescriptionKey(...)` or `WithDescriptionText(...)`
- `WithStackSize(...)`
- `WithTradeValue(...)`
- `WithScrapValue(...)`
- `WithFabrication(...)`
- `WithRation(...)`
- `WithRawFood(...)`
- `WithRecycling(...)`
- `WithObjectType(...)`

## 5. How Sheltered handles items

### 5.1 Registration phase

Your mod writes metadata through the `ShelteredContent` facade:
- `RegisterItem(...)`
- `RegisterRecipe(...)`
- `RegisterCookingRecipe(...)`
- `PatchItem(...)`
- `PatchRecipe(...)`
- `AddLoot(...)`

At this stage nothing is in the live game managers yet.

### 5.2 Resolution phase

The internal content resolver converts registered metadata into runtime-ready assets:
- resolves owning assembly
- loads icons
- loads prefabs from bundles when configured
- pairs `ItemDefinition` with resolved assets

### 5.3 Injection phase

The internal content injector binds to the active Sheltered runtime managers:
- reads `ItemManager.Instance`
- reads `CraftingManager.Instance`
- creates runtime `ItemDefinition` objects for the game
- injects them into the game's item definition dictionary
- injects recipes into `CraftingManager`
- injects cooking recipes and loot hooks

This runtime is manager-scoped, not process-scoped. If Sheltered rebuilds its managers after returning to the main menu and starting another family, the injector rebinds and reapplies content to the new manager instances.

### 5.4 Inventory and UI phase

Sheltered's vanilla code often assumes the compiled enum contains every item. Custom items break that assumption.

The framework compensates by:
- expanding `InventoryManager` slot storage for newly injected items
- augmenting item panels that would otherwise skip custom item types
- resolving string IDs back to runtime `ItemType` values when helper APIs are used

### 5.5 Localization phase

Sheltered UI expects localization keys, not arbitrary raw display text, in most item-definition fields.

The localization layer applies these rules:
- explicit key APIs keep your key as-is
- explicit text APIs generate internal keys
- generated keys are stored in the content localization table
- a localization patch returns those values directly to preserve original casing

Generated keys use the pattern:
- `shelteredapi.<modid>.<itemid>.name`
- `shelteredapi.<modid>.<itemid>.desc`

## 6. Recommended author workflow

1. Create your `ItemDefinition`.
2. Register the item in `Start(...)`.
3. Register any recipe that produces it.
4. Add icon and other supporting assets under `Assets/...`.
5. Use `ShelteredContent.Runtime.ResolveItemType(...)`, `ctx.Game`, or the store APIs in the [runtime UI and stores guide](ShelteredAPI_Runtime_UI_Stores_Guide.md) when interacting with the item at runtime.
6. Test these flows:
   - new family
   - return to main menu
   - second new family in the same game launch
   - save/load
   - storage UI
   - object-attached storage UI
   - cooking station UI and timed job completion
   - crafting UI
   - trading UI

## 7. Common pitfalls

### Missing display name

`RegisterItem(...)` will fail if the item has no display name value.

### Missing or wrong icon path

The item can still register, but UI will not have the intended icon.

### Wrong registration timing

Register content from `Start(...)`, after plugin initialization has completed.

### Assuming enum iteration will find the item

Vanilla `Enum.GetValues(typeof(ItemManager.ItemType))` does not know about your custom numeric values.

### Reusing another mod's string ID

String IDs must be unique across the content registry.

## 8. Asset path rules

Use mod-relative asset paths:
- `Assets/Icons/power_cell.png`
- `Assets/Bundles/myitems.bundle|PowerCellPrefab`

The framework resolves them relative to the owning mod root.

## 9. Troubleshooting signals

Check the log for:
- item registration failures
- custom item ID collisions
- unresolved recipe result items
- unresolved recipe ingredients
- failed sprite or bundle loads
- localization warnings about likely literal text being treated as keys

## API types to learn first

Start with these types:
- `ShelteredAPI.Content.ShelteredContent`
- `ShelteredAPI.Content.ItemDefinition`
- `ShelteredAPI.Content.RecipeDefinition`
- `ShelteredAPI.Content.CookingRecipe`
- `ShelteredAPI.Content.ItemPatch`
- `ShelteredAPI.Content.RecipePatch`
- `ShelteredAPI.Content.LootEntry`

If you only need to add a normal item with a crafting recipe, you usually only need:
- `ItemDefinition`
- `RecipeDefinition`
- `ShelteredContent`
