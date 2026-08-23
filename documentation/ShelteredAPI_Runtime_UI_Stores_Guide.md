# ShelteredAPI runtime UI, stores, and cooking stations

This guide covers the supported path for mod-owned panels and object-linked item flows. Use these APIs when a mod needs a fridge, container, cooking UI, quest inventory, event inventory, or any workflow where cloning vanilla NGUI would otherwise be required.

See the [API reference](API_Signatures_Reference.md) and [assembly boundary](README.md#assembly-boundary-canonical).

> Dev/API-preview warning: runtime UI stores and cooking stations are preview APIs. Names and behavior may change before SMM 2.0 is declared stable.

Import the namespaces you use:

```csharp
using System.Collections.Generic;
using ModAPI.Actors;
using ShelteredAPI.Actors;
using ShelteredAPI.Content;
using ShelteredAPI.Storage;
using ShelteredAPI.UI;
using ShelteredAPI.UI.Runtime;
using ShelteredAPI.Workstations;
using UnityEngine;
```

## Design rule

Runtime UI is mod-owned UI. Mods pass DTOs, callbacks, stores, and recipes. ShelteredAPI owns the NGUI objects, panel depth, refresh lifecycle, close handling, and object-menu integration.

Use this instead of:
- duplicating vanilla NGUI templates
- patching panel `Update` loops to keep copied UI alive
- storing custom item data inside vanilla objects that only save fixed fields

Fridge-like custom storage has two models:
- vanilla freezer store: an adapter over `Obj_Freezer`, limited to vanilla freezer food fields
- mod-owned object store: ShelteredAPI persistence keyed to an object, able to hold string-ID items owned by mods

## Augmenting existing panels

Prefer the runtime panel APIs below for mod-owned UI. If an integration must augment an existing Sheltered panel, use `ShelteredAPI.UI.ShelteredUI` for focused template reuse and cleanup:

```csharp
UICloneResult row = ShelteredUI.CloneElement(template, parent);
if (row.Success)
{
    UIButton remove = row.Clone.GetComponent<UIButton>();
    ShelteredUI.BindButtonClick(remove, item, selected => Remove(selected), UIButtonBindingMode.Replace);
}
```

`CloneElement(...)` clears inherited listener and button callbacks by default and reports non-fatal warnings. `SnapshotColors(...)` plus `RestoreColors(...)` handles temporary label, widget, and tween color edits. `SubscribePanelLifecycle(...)` provides a disposable typed close-cleanup hook.

## Runtime panel chrome

Every runtime container or crafting panel can accept `RuntimePanelOptions`.

```csharp
var panelOptions = new RuntimePanelOptions
{
    Width = 700,
    Height = 540,
    HeaderIcon = ShelteredContent.AdvancedAssets.LoadSprite(
        typeof(MyPlugin).Assembly,
        "Assets/Icons/fridge.png"),
    ShowHeaderIcon = true,
    Subtitle = "Shared cold storage",
    Style = new RuntimePanelStyle
    {
        HeaderColor = new Color(0.10f, 0.16f, 0.18f, 1f),
        ButtonColor = new Color(0.18f, 0.24f, 0.26f, 1f)
    }
};
```

For a lightweight text icon, opt in without loading a sprite:

```csharp
new RuntimePanelOptions { HeaderIconText = "Cook", ShowHeaderIcon = true }
```

Unset values use ShelteredAPI defaults.

## Mod-owned stores

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

Mod stores also implement the optional `IReservableItemStore` interface. Reservations subtract from `GetAvailableCount(...)` and normal `CanRemove(...)` checks, but they do not mutate vanilla freezer internals or require other `IItemStore` implementations to opt in. Use `CommitReservation(...)` to consume reserved items after delayed work completes, or `CancelReservation(...)` when the work is cancelled.

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

## Vanilla store adapters

ShelteredAPI also exposes adapters for real vanilla stores:

```csharp
IItemStore inventory = ShelteredStores.ForInventory();
IItemStore nearestFreezer = ShelteredStores.FindNearestFreezer(stove.transform.position);
```

Vanilla `Obj_Freezer` supports only `Meat` and `DesperateMeat`. The freezer adapter preserves that rule. Use a mod-owned store for a fridge with arbitrary item IDs.

## Character item assignments

Use `ShelteredCharacterItems` when a mod needs to tag existing stored items as associated with a survivor. This is an assignment/classification layer over `IItemStore`; it is not a separate physical inventory and does not move, delete, duplicate, or apply equipment effects.

Character identity is actor-backed. The `FamilyMember` overloads are convenience helpers that resolve `ShelteredActors.FamilyMemberActorId(member.GetId())`; assignment records persist the resulting `ActorId`. Use the `ActorId` overloads when your code already works from the actor system.

```csharp
FamilyMember survivor = InteractionManager.Instance.GetSelectedFamilyMember();
IItemStore inventory = ShelteredStores.ForInventory();

CharacterItemAssignment meds = ShelteredCharacterItems.Assign(
    member: survivor,
    source: inventory,
    itemId: "AntiRad",
    quantity: 2,
    kind: CharacterItemAssignmentKind.Assigned,
    slot: CharacterItemSlot.Medicine);

ActorId actorId = meds.ActorId;
```

The item count remains backed by `inventory`. `Assign(...)` validates that the source store currently has enough unassigned quantity. If the source also implements `IReservableItemStore`, assignment checks respect its available count so queued reservations are not treated as free stock.

Use `CharacterItemAssignmentKind.Reserved` when the assignment should hold stock for a character. Reservable stores receive a real reservation and the returned assignment exposes `ReservationId`; non-reservable stores fall back to assignment metadata. Use `CharacterItemSlot` for medical, food, tool, backpack, or hand placement intent.

Query or release the metadata without mutating storage:

```csharp
IList<CharacterItemAssignment> all = ShelteredCharacterItems.GetAssignments(actorId);
IList<CharacterItemAssignment> available = ShelteredCharacterItems.GetAvailableAssignments(actorId);
int assignedMeds = ShelteredCharacterItems.GetAssignedCount(actorId, "AntiRad");

ShelteredCharacterItems.Unassign(meds.AssignmentId);
```

Assignment metadata persists with ShelteredAPI save data. `Unassign(...)`, `ReleaseAssignmentsForActor(...)`, and `ReleaseAssignmentsForMember(...)` remove assignment records and cancel assignment-owned reservations; they do not remove items from the backing store. Use this for "reserved for Alice", "carried by Bob", "equipped in main hand", or quest/medical/food classifications, while keeping global shelter storage as the source of truth.

Container category filters use vanilla item categories. A `Food` filter also includes vanilla `Meat`/`DesperateMeat` rows so fridge-style panels do not hide freezer food.

## Container panel

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

## Object-attached fridge UI

Register an `ObjectPanelRegistration` with `ShelteredRuntimeUI.RegisterObjectPanel(...)`. In its `Open` callback, resolve `ShelteredStores.ForObject(...)`, create a `ContainerUiRequest`, and open it with `ShelteredRuntimeUI.OpenContainer(...)`. The complete example below uses this pattern.

## Minimal fridge-backed cooking flow

This pattern uses a vanilla freezer/fridge object as the world anchor, but stores the contents in a mod-owned object store. The stove looks up the nearest freezer object, resolves the same object-attached store, consumes `Meat x1` from that store, and outputs `Ration x1` to global shelter inventory after a vanilla-style timed job.

1. Find the nearest freezer/fridge object from the stove:

```csharp
Obj_Base fridgeObject = ShelteredStores.FindNearestObject(
    ObjectManager.ObjectType.Freezer,
    stove.transform.position);
```

2. Create or resolve the object-attached store:

```csharp
IItemStore fridgeStore = fridgeObject != null
    ? ShelteredStores.ForObject(
        ownerId: "com.example.cooking",
        targetObject: fridgeObject,
        displayName: "Fridge Storage",
        capacity: 24)
    : null;
```

3. Open a container UI for that store when the fridge object is clicked:

```csharp
ShelteredRuntimeUI.RegisterObjectPanel(new ObjectPanelRegistration
{
    ObjectType = ObjectManager.ObjectType.Freezer,
    InteractionId = "com.example.cooking.open_fridge_storage",
    InteractionText = "Open Fridge Storage",
    Open = context =>
    {
        IItemStore store = ShelteredStores.ForObject(
            ownerId: "com.example.cooking",
            targetObject: context.TargetObject,
            displayName: "Fridge Storage",
            capacity: 24);

        return ShelteredRuntimeUI.OpenContainer(
            ShelteredStores.CreateContainerRequest(
                store: store,
                ownerId: "com.example.cooking",
                panelId: "com.example.cooking.fridge." + context.TargetObject.objectId,
                title: "Fridge Storage"));
    }
});
```

4. Register the stove as a cooking station. The ingredient store is the nearest object-attached fridge store; the output store is global shelter inventory:

```csharp
ShelteredCooking.RegisterStation(new CookingStationRegistration
{
    OwnerId = "com.example.cooking",
    ObjectType = ObjectManager.ObjectType.Stove,
    InteractionId = "com.example.cooking.stove.cook",
    InteractionText = "Cook",
    Title = "Stove",

    CanOpen = context =>
        ShelteredStores.FindNearestObject(
            ObjectManager.ObjectType.Freezer,
            context.TargetObject.transform.position) != null,

    IngredientStore = context =>
        ShelteredStores.FindNearestObjectStore(
            ownerId: "com.example.cooking",
            objectType: ObjectManager.ObjectType.Freezer,
            position: context.TargetObject.transform.position,
            displayName: "Fridge Storage",
            capacity: 24),

    OutputStore = context => ShelteredStores.ForInventory(),

    JobOptions = new CookingStationJobOptions
    {
        JobType = "cook_food",
        AnimationTrigger = "Rummage",
        DurationSeconds = 3f,
        ClosePanelOnQueue = true
    },

    Recipes = new[]
    {
        new CookingStationRecipe
        {
            RecipeId = "com.example.cooking.meat_to_ration",
            DisplayName = "Cook Ration",
            Subtitle = "Meat x1 -> Ration x1",
            OutputItemId = VanillaItems.Ration,
            OutputCount = 1,
            Ingredients = new[]
            {
                new RecipeIngredient
                {
                    ItemId = VanillaItems.Meat,
                    Count = 1
                }
            }
        }
    }
});
```

With those options, pressing Cook queues a survivor job, plays the `Rummage` animation, waits `3f` seconds, then consumes meat from the fridge store and adds the ration to the global shelter inventory. `JobOptions.JobType` controls the interaction value stored in `Job.type`; it does not make the queued callback state restorable after a save and load. If the stove should open without a fridge, return an empty mod store and state why cooking is unavailable. Do not fall back to global inventory or write custom items into `Obj_Freezer`.

## Cooking station options

Use `ShelteredCooking.RegisterStation(...)` when a world object should open a recipe panel backed by stores. The full example above shows the required registration shape.

| Option | Behavior |
| --- | --- |
| `CanOpen` | Decides whether the object interaction can open the station. |
| `IngredientStore` / `OutputStore` | Select the stores used for each craft. |
| `Recipes` / `RecipeSource` | Supply fixed or context-dependent recipes. |
| `JobOptions` | Queue timed survivor work. Without it, crafting applies immediately. |
| `OnCraftQueued` / `OnCrafted` / `OnCraftFailed` | Add feedback or follow-up behavior at each result. |

With `JobOptions` enabled, pressing Cook queues a real character job. ShelteredAPI picks the selected idle member, or the first idle family member, walks them to the workstation, shows vanilla interaction progress, plays the configured animation, and only applies the recipe when the timer completes. If the job is cancelled, the recipe is not applied.

When the ingredient store implements `IReservableItemStore`, queued cooking reserves the aggregated ingredient requirements before the timed job starts. A second queued job or container UI transfer sees only unreserved availability, so the same meat cannot be double-consumed while the first job is still running. Stores without reservation support keep the old behavior and consume only when the job completes.

`AnimationTrigger` and `CompleteAnimationTrigger` are optional. Leave them unset for no animation changes, or set them to vanilla triggers such as `Rummage` and `Idle` when the target object has a matching interaction style.

Without `JobOptions`, the default behavior applies immediately: it checks ingredients, removes them, adds the output, rolls back consumed ingredients if output fails, and refreshes the panel.

Use `OnCraftQueued` for UI/sound feedback when the job is accepted, `OnCrafted` for completion behavior, and `OnCraftFailed` for missing ingredients, full output stores, or cancelled jobs.

## Save and vanilla limits

- Vanilla cooking is an eating-stage interaction, not a real crafting station.
- Vanilla freezers cannot store arbitrary item IDs.
- ShelteredAPI avoids patching `Obj_Freezer` to support custom item types. `Obj_Freezer` remains the vanilla meat/desperate-meat store; mod-owned object stores are the extension point for fridge-backed custom storage.
- ShelteredAPI timed cooking jobs are runtime jobs. If a save is loaded while one is in progress, the vanilla queue loader cannot restore the ShelteredAPI callback state. The callback-based craft state is lost; the fallback base job may still start an interaction on its target.
