# Scenario authoring architecture

This reference defines ownership inside the optional in-game scenario editor. Mod authors should use the [custom scenarios guide](Custom_Scenarios_Guide.md).

## Assembly boundary

```text
ShelteredScenarioEditor.dll -> ShelteredAPI.dll -> ModAPI.dll
```

| Assembly | Owns |
| --- | --- |
| `ShelteredScenarioEditor.dll` | Interactive drafts, authoring sessions, commands, selection, editor projections, preview lifetime, diagnostics, and presentation |
| `ShelteredAPI.dll` | Public scenario definitions and facades, XML, validation, installed catalog, browser, playback, runtime binding, saves, triggers, scoring, and apply behavior |
| `ModAPI.dll` | Neutral plugin lifecycle, scenario registration contracts, settings, persistence ports, and runtime bootstrap contracts |

Mods reference `ModAPI.dll` and `ShelteredAPI.dll`. They do not reference the editor assembly. ShelteredAPI must build and run when the editor is absent.

Use `ShelteredScenarios` for registration and catalog operations, `ShelteredScenarioAuthoring` for XML and definition work, and `ShelteredScenarioRuntime` for active-scenario operations. Do not add another registration wrapper.

## Owners

| Concern | Owner |
| --- | --- |
| Live target identity, validation, and mutation | `ScenarioAuthoringSelectionService` and its target adapters |
| Backdrop discovery | `ScenarioBackdropTargetCatalogService` |
| Backdrop presentation | `ScenarioAssetAuthoringContentBuilder` |
| Inspector item construction | `ScenarioInspectorItemFactory` |
| Settings decoding | `ScenarioAuthoringSettingsSnapshot` |
| Commands and policy | `ScenarioAuthoringCommandService` |
| Command handler lookup | `ScenarioCommandDispatcher` |
| Launch and close state | `ScenarioAuthoringSessionLifecycleService` |
| Rendering | `ScenarioAuthoringShellImguiRenderModule` |
| Editor checklist storage | `ScenarioAuthoringSidecarStore` |
| Current preview | `ScenarioPreviewSessionHost` over `IScenarioPreviewSession` |
| XML, definitions, validation, and runtime behavior | ShelteredAPI scenario facades and their internal implementations |

Presentation code reads snapshots from these owners. It must not rediscover targets, keep a second session state, or mutate a definition through an alternate command path.

## Command flow

Every editor action uses a typed command:

```text
presentation, shortcut, automation, or search
                    |
                    v
       ScenarioAuthoringCommand
                    |
                    v
    ScenarioAuthoringCommandService
       policy and safety snapshot
                    |
                    v
       ScenarioCommandDispatcher
                    |
                    v
       one concrete handler
```

`AutomationId` identifies a control for testing and diagnostics. It is not a command language. Do not parse it to choose behavior.

Put reload, world-readiness, and safety-snapshot requirements on `ScenarioAuthoringCommandPolicy`. Indirect actions, including search routes, must re-enter `ScenarioAuthoringCommandService` for each step.

## Composition

`ScenarioCompositionRoot` and its authoring and presentation modules construct editor services and command handlers. Application and presentation services receive dependencies through constructors.

Static lookup is limited to entry points whose lifetime comes from Unity, Harmony, or native IMGUI callbacks. A boundary callback resolves one composed service and delegates. It does not own editor state.

## Lifecycle

`ScenarioAuthoringSessionLifecycleService` owns the current launch identity and phase:

```text
Inactive -> Queued -> WorldLoading -> Active
   ^                                  |
   |                                  v
   +------------- Closing <---- ReloadPending
```

Its revision prevents an asynchronous callback from changing a newer draft or session. Bootstrap, reload, activation, close, orphan cleanup, and shutdown all pass through this service.

The manager option is `ShelteredScenarioEditor.Enabled`. It defaults to `false` and requires a restart.

- If the DLL is absent, ShelteredAPI still browses and plays installed scenarios.
- If the DLL is present but disabled, the editor does not create its composition graph, patches, Unity objects, windows, repositories, or sessions.
- If the DLL is present and enabled, the editor creates its graph after runtime bootstrap and calls ShelteredAPI through public contracts.

## Editor metadata and preview

The runtime file is `scenario.xml`. The editor stores checklist and workflow state beside it as `scenario.editor.xml`. Draft copies and snapshots preserve the pair. Published packages exclude the editor sidecar.

`ScenarioPreviewSessionHost` owns one disposable preview. Replacing a preview, leaving playtest, closing the editor, an initialization failure, and shutdown must all dispose that session. ShelteredAPI then releases the preview definition, seed, carrier, and runtime binding.

## Extension rules

- Add a target kind through a selection adapter.
- Add a command as one typed `ScenarioAuthoringCommand` and one owning handler.
- Add dependencies through the composition modules and constructor parameters.
- Extend the existing backdrop or inspector projection instead of building one per window.
- Keep mod-facing scenario APIs in ShelteredAPI.
- Keep drafts, editor commands, and authoring presentation in the editor assembly.
- Do not add a second renderer, dispatcher, session store, installed-scenario catalog, or preview owner.

## Verify changes

Run the contracts that cover the changed owner:

```cmd
tools\test-scenario-authoring-toggle-contracts.cmd
```

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File tools\Test-ScenarioEditorAssemblyBoundary.ps1
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File tools\Test-ScenarioEditorComposition.ps1
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File tools\Test-ScenarioAuthoringLifecycleContracts.ps1
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File tools\Test-ScenarioWorkspaceRouting.ps1
```
