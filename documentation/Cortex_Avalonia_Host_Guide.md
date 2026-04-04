# Cortex Avalonia Host Guide

`Cortex.Host.Avalonia` is the current desktop-first Cortex host.

It is intentionally modest in this phase:

- real application shell window
- onboarding/workspace selection
- settings editing
- workspace/project browsing
- editor document summary and preview
- search result navigation over runtime-owned search state
- reference/source inspection for the runtime-owned reference browser lane
- persisted Dock-owned desktop workbench structure
- host-local runtime/status surface
- named-pipe bridge client for the legacy runtime process

It is intentionally not the full future workbench yet:

- no broad plugin surface policy yet
- no attempt to redesign the IDE UX in this pass

## Build and run

Build the shared bridge and desktop host:

- `dotnet build Cortex.Bridge\Cortex.Bridge.csproj`
- `dotnet build Cortex.Shell.Shared\Cortex.Shell.Shared.csproj`
- `dotnet build Cortex.Host.Avalonia\Cortex.Host.Avalonia.csproj`

Start the legacy runtime process first. The bridge publisher starts automatically when the legacy shell/workbench runtime initializes.

Run the host:

- `dotnet run --project Cortex.Host.Avalonia\Cortex.Host.Avalonia.csproj`
- `dotnet run --project Cortex.Host.Avalonia\Cortex.Host.Avalonia.csproj -- --pipe-name cortex.desktop.bridge`

The pipe name can also be overridden on both processes with `CORTEX_DESKTOP_BRIDGE_PIPE_NAME`.

## Startup and session seam

Desktop startup and session policy now lives under `Cortex.Host.Avalonia/Composition`:

- `DesktopSessionStartupService` resolves command-line and environment bridge settings
- `DesktopHostPathPolicy` owns the host data-root and log-path convention
- `DesktopHostOptions` and `DesktopBridgeClientOptions` carry the resolved startup/session options
- `DesktopHostApplicationSession` owns logging, composition-root lifetime, and main-window creation

`App.axaml.cs` is now only the Avalonia application entry and lifetime hook.

The host data root now also owns:

- `%LocalAppData%\Cortex.Host.Avalonia\desktop-shell-state.json`
- `%LocalAppData%\Cortex.Host.Avalonia\desktop-dock-layout.json`

## Shell state and Dock layout ownership

Host-local shell ownership now lives entirely inside `Cortex.Host.Avalonia`:

- `DesktopShellStateStore` persists host-local shell state and surface visibility
- `DesktopDockLayoutPersistenceService` persists the Dock group layout state
- `DesktopWorkbenchSurfaceRegistry` defines the known host-local workbench surfaces
- `DesktopWorkbenchCompositionService` composes Dock layout from runtime snapshot state plus host-local persistence

The Dock-specific structure stays host-local. No Dock models or persistence contracts cross into `Cortex.Bridge` or `Cortex.Shell.Shared`.

Current user-directed layout policy is intentionally small and explicit:

- surface visibility is host-local and persisted in shell state
- `Save Layout` captures the current Dock arrangement for later sessions
- `Reset Layout` drops the saved Dock arrangement and returns to the runtime-selected layout preset

## Current composition boundary

The host consumes `Cortex.Bridge` for:

- session handshake, snapshot, intent, operation-result, and diagnostic envelopes
- the named-pipe transport contract and pipe-name convention

The host consumes `Cortex.Shell.Shared` for:

- settings document/session/apply workflows
- onboarding models and selection logic
- workspace/project discovery and tree shaping
- editor/search/reference workbench projection models

The legacy runtime process owns:

- named-pipe listener lifetime
- producing bridge snapshots from runtime-owned state
- applying semantic intents through runtime-owned services and state
- publishing snapshot updates after runtime state changes

The desktop host owns:

- Avalonia startup and window composition
- named-pipe client connection lifecycle
- snapshot-to-view-model projection
- Dock workbench layout composition for onboarding, workspace, editor, search, reference, settings, and runtime/status surfaces
- host-local shell state and Dock layout persistence
- Serilog configuration
- connection/lifecycle status presentation

## Process split

What runs in the legacy runtime process:

- runtime shell/session ownership
- settings/onboarding/workspace services
- editor/search/reference bridge projection services
- bridge snapshot production
- semantic intent handling

What runs in the desktop host process:

- Avalonia window and Dock structure
- host-local shell state and persisted layout policy
- bridge client
- shell status display and surface visibility policy

What crosses the bridge:

- versioned session handshake messages
- workbench snapshots for onboarding, settings, workspace/projects, editor status, search results, reference status, and file preview
- semantic user intents for onboarding selection, workspace root selection/import/analyze, settings edits/save, project selection, file preview open, search updates, and search-result open
- operation results and diagnostics

What stays local to the desktop host:

- `%LocalAppData%\Cortex.Host.Avalonia\cortex-desktop.log`
- `%LocalAppData%\Cortex.Host.Avalonia\desktop-shell-state.json`
- `%LocalAppData%\Cortex.Host.Avalonia\desktop-dock-layout.json`

## Package lane

For host-lane packaging, `FutureHostReady` now includes the Avalonia host runtime lane under:

- `artifacts\bundles\FutureHostReady\host\lib\`

That lane is currently intended for `Cortex.Host.Avalonia`, `Cortex.Bridge`, and `Cortex.Shell.Shared`.
