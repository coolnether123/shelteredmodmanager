# Cortex Avalonia Host Guide

`Cortex.Host.Avalonia` is the current desktop-first Cortex host.

It is intentionally modest in this phase:

- real application shell window
- onboarding/workspace selection
- settings editing
- workspace/project browsing
- search result navigation over runtime-owned search state
- file preview plus active editor/reference status for editor/workspace-oriented surfaces
- Dock-owned desktop workbench structure
- named-pipe bridge client for the legacy runtime process

It is intentionally not the full future workbench yet:

- no persisted or user-customizable docking layout policy yet
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
- Dock workbench layout composition for onboarding, workspace, and settings surfaces
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
- bridge client
- shell status display

What crosses the bridge:

- versioned session handshake messages
- workbench snapshots for onboarding, settings, workspace/projects, editor status, search results, reference status, and file preview
- semantic user intents for onboarding selection, workspace root selection/import/analyze, settings edits/save, project selection, file preview open, search updates, and search-result open
- operation results and diagnostics

What stays local to the desktop host:

- `%LocalAppData%\Cortex.Host.Avalonia\cortex-desktop.log`

## Package lane

For host-lane packaging, `FutureHostReady` now includes the Avalonia host runtime lane under:

- `artifacts\bundles\FutureHostReady\host\lib\`

That lane is currently intended for `Cortex.Host.Avalonia`, `Cortex.Bridge`, and `Cortex.Shell.Shared`.
