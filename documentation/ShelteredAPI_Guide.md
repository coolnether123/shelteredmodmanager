# When To Use ShelteredAPI (v2.0 Beta.1)

`ShelteredAPI.dll` is the game-facing layer for mods that operate on Sheltered content, saves, runtime UI/input, gameplay events, actors/characters, or scenarios. Use [Core ModAPI Basics](ModAPI_Developer_Guide.md) first for a plugin that only needs neutral framework behavior.

Assembly choices and the typed-escape-hatch rule are defined once in the canonical [assembly boundary](README.md#assembly-boundary-canonical). Exact method/type shapes belong in [API Signatures Reference](API_Signatures_Reference.md).

## Facade Chooser

| Need | Public Entry Point | Detail Guide |
|------|--------------------|--------------|
| Register items, recipes, loot, localization, or assets | `ShelteredContent` | [Content Guide](ShelteredAPI_Content_Guide.md) |
| Inspect/control Sheltered save slots or listen to save lifecycle | `ShelteredSaves`, `ShelteredSaveEvents` | [Settings and Persistence](SETTINGS.md#5-sheltered-save-slots) and [Events Guide](Events_Guide.md#5-save-lifecycle-events) |
| Export save/mod/runtime facts for bug reports | `ShelteredSupportBundle` | [API Signatures Reference](API_Signatures_Reference.md#save-manifest--support-bundle-smm-20) |
| Subscribe to Sheltered gameplay, UI, faction, or scheduled-time events | `ShelteredEvents` | [Events Guide](Events_Guide.md) |
| Add Sheltered controls or adjust vanilla input tuning | `ShelteredInput` | [Input Keybindings Guide](Input_Keybindings_Guide.md) |
| Add targeted vanilla UI behavior | `ShelteredUI` | [API Signatures Reference](API_Signatures_Reference.md) |
| Build mod-owned panels, stores, item assignments, or stations | `ShelteredRuntimeUI`, `ShelteredStores`, `ShelteredCharacterItems`, `ShelteredCooking` | [Runtime UI, Stores, and Cooking Stations](ShelteredAPI_Runtime_UI_Stores_Guide.md) |
| Use neutral actors with Sheltered character access when required | `ctx.Actors`, `ShelteredActors`, `ShelteredCharacters` | [Actors Guide](ShelteredAPI_Characters_Guide.md) |
| Register or author custom scenarios | `ShelteredScenarios`, `ShelteredScenarioAuthoring`, `ShelteredScenarioRuntime` | [Custom Scenarios Guide](Custom_Scenarios_Guide.md) |
| Read expedition map geometry or declare map-generation intent | `ShelteredMap` | [API Signatures Reference](API_Signatures_Reference.md#expedition-map-context-smm-20) |
| Project or register expedition map markers and actor snapshots | `ShelteredMapMarkers` | [API Signatures Reference](API_Signatures_Reference.md#map-markers-smm-20) |
| Inspect or conservatively restore player job queues | `ShelteredQueues` | [API Signatures Reference](API_Signatures_Reference.md#player-queues-smm-20) |

## Selection Rules

- Keep ordinary mod settings and save-scoped state on `ModAPI` (`ModManagerBase<T>` and `ctx.SaveSystem`).
- Use `ShelteredSaves` only for Sheltered slot/descriptors/lifecycle work, not as an alternative general persistence store.
- Use `ShelteredSupportBundle.ExportJson(...)` for bug-report diagnostics; absent optional services are reported as `unknown` or `unavailable`.
- Use `ctx.Actors` and DTO/proxy types for ordinary actor logic. Cross into raw `FamilyMember`, `NpcVisitor`, or other vanilla types only through an explicit typed Sheltered escape hatch when the integration requires it.
- Use `ShelteredQueues` for copied player-job metadata and guarded pending-job restoration. Queue capacity is observed metadata; capacity changes remain mod policy.
- Use `ShelteredRuntimeUI` for panels owned by a mod. Do not clone or continually patch vanilla NGUI merely to host custom panel behavior.
- Use the content facade for item IDs and injection rather than assuming Sheltered enum iteration will recognize custom items.
- Use `ShelteredMap.Current` for generated expedition-map facts; scenario map DTOs describe authored scenario content and are not a live runtime snapshot.
- Register runtime features from plugin lifecycle methods, normally `Start(...)`, rather than constructors.

## Targeted Vanilla UI Helpers

Use `ShelteredUI` only when a feature must augment an existing game panel. `CloneElement(...)` reuses a visual template while clearing inherited listeners and button handlers by default; the returned result carries warnings for hierarchy-dependent work.

```csharp
UICloneResult clone = ShelteredUI.CloneElement(templateButton.gameObject, rowParent);
if (clone.Success)
{
    UIButton button = clone.Clone.GetComponent<UIButton>();
    ShelteredUI.BindButtonClick(
        button,
        item,
        selected => OpenDetails(selected),
        UIButtonBindingMode.Replace);
}
```

Capture temporary label/widget/tween colors with `SnapshotColors(...)` and restore them on typed panel close via `SubscribePanelLifecycle(...)`. `UITakeoverSession.BindTooltip(...)` also participates in `Restore()` by hiding its tooltip and reinstating the preceding hover binding.

## Expedition Map Context

`ShelteredMap.Current` exposes read-only expedition dimensions, the `40 x 16` vanilla normal-map baseline, result scale, map seed when assigned, home shelter position, coordinate conversion, and route-distance helpers. Check `IsValid` before using coordinate or route operations: startup, scene transitions, and non-shelter scenes return explicit unavailable or not-yet-generated results.

`ShelteredMap` also accepts focused location-density, town-density, quest-placement, faction-zone, home-shelter, and special-item eligibility policies. Policy composition is deterministic and empty registration produces vanilla/no-op intent. This foundation records and resolves intent; it deliberately does not apply speculative Harmony changes to vanilla map generation.

## Shared Facade Conventions

New SMM 2.0 service APIs follow the facade pattern represented by `ShelteredSaves`, `ShelteredUI`, `ShelteredRuntimeUI`, `ShelteredCharacters`, `ShelteredStores`, and `ShelteredCooking`.

- Sheltered-facing mod-author entry points are small `public static` facade classes named `Sheltered<Domain>`, for example `ShelteredQueues` or `ShelteredMapMarkers`.
- Host-neutral behavior stays in `ModAPI.dll` under its neutral API name, for example `ModRandom`, `ModThreads`, or `PatchRegistry`. Do not add a Sheltered wrapper or a broad capability registry for neutral functionality.
- Backend services, manager adapters, patch hosts, persistence repositories, and runtime coordinators remain `internal`. Expose public interfaces only when mod code must implement or interchange a focused contract.
- Public DTOs carry identifiers, copied values, snapshots, or read-only data. Do not make mutable live vanilla collections or persisted vanilla runtime objects the normal exchange shape.
- Direct `Assembly-CSharp` types in new public signatures are limited to deliberately typed Sheltered adapters or escape hatches, made visible by names such as `FromFamilyMember`, `FindFamilyMember`, or `ForFreezer`.

| Purpose | New Type Name Convention | Notes |
|---------|--------------------------|-------|
| Facade | `Sheltered<Domain>` or established neutral `ModAPI` name | Stable mod-author entry point. |
| Command input | `<Operation>Request`, `<Domain>Options`, or `<Domain>Registration` | Use `Registration` for contributed behavior. |
| Data transfer | `<Domain>Info`, `<Domain>Descriptor`, `<Domain>Snapshot`, or `<Domain>Context` | Snapshot/context DTOs must not hide mutable live game state. |
| Operation outcome | `<Operation>Result` or `<Domain>Result` | Include status/success and actionable failure detail. |
| Unavailable runtime | Normal result with an `Unavailable` status/reason, or an established `TryGet...` pattern | Missing active save/scene/map/service is not an implementation exception. |
| Lifetime token | `<Domain>Handle` or `<Domain>RegistrationHandle` | Implement `IDisposable` for removable registrations/open resources. |
| Diagnostic export | `<Domain>DiagnosticsReport` with `<Domain>Diagnostic` or `<Domain>DiagnosticEntry` records | Reports contain concrete facts, not capability discovery. |

Existing Beta.1 names that predate these rules remain supported, including `SaveEntry` and `PatchApplyReport`. New public `ShelteredAPI` classes, interfaces, structs, and enums require a justified `ShelteredAPI_PublicSurface_Baseline.tsv` row and exact callable signatures in [API Signatures Reference](API_Signatures_Reference.md). Reserved signature sections identify ownership only; they are not callable API promises.

## Status Of Advanced Surfaces

| Surface | Beta.1 Status | Consequence For Authors |
|---------|---------------|-------------------------|
| Content, events, input, actors/characters, and save facades | Documented public author surface | Follow the relevant guide and signature reference. |
| Expedition map context and generation-policy intent | API preview | Query runtime facts safely; generation adapters consuming policy intent remain follow-up work. |
| Map-marker/expedition-actor snapshots and support bundles | API preview / diagnostics | Use copied facts for integrations and reports; do not treat them as mutable game state. |
| Player queue snapshots and conservative restore | API preview | Queue capacity is observed metadata, not framework-owned policy. |
| Runtime UI, stores, item reservations/assignments, and cooking stations | API preview | Expect small API or behavior adjustments before stable 2.0. |
| Custom scenario browser/XML authoring/runtime/scoring snapshots | Experimental | Test using disposable saves and do not promise stable long-running-save behavior yet. |

Back up saves before testing any save-changing or experimental scenario behavior. The public beta status applies even when a particular facade is documented.

## Stable Surface Versus Internals

Mods should call documented facades and exchange their public DTOs. Serializers, catalog services, storage repositories, runtime binding services, NGUI implementations, patch hosts, and manager adapters are implementation detail and may change without becoming supported mod entry points.

For loader/runtime ownership details, use [ModAPI Architecture Guide](ModAPI_Architecture_guide.md). For the completed implementation split and verifier history, use [ModAPI/ShelteredAPI Boundary Refactor](ModAPI_Sheltered_Boundary_Refactor.md).
