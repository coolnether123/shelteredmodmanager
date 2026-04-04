# Cortex Desktop Bridge Guide

This document describes the first real out-of-process desktop bridge for Cortex.

## Runtime-side ownership

The legacy runtime process owns:

- bridge session lifetime and named-pipe listener startup
- producing `WorkbenchBridgeSnapshot` instances from runtime-owned state
- applying semantic `BridgeIntentMessage` requests back into runtime-owned services
- publishing operation results and diagnostics

The runtime bridge is now split by feature instead of one monolithic session class:

- `RuntimeDesktopBridgeSettingsFeature` owns settings and onboarding projection/apply behavior
- `RuntimeDesktopBridgeWorkspaceFeature` owns workspace/project discovery, selection, and preview state
- `RuntimeDesktopBridgeWorkbenchFeature` owns editor, search, and reference workflow projection plus workbench-oriented intents
- `RuntimeDesktopBridgeSnapshotBuilder` assembles the bridge snapshot from those feature-owned lanes

The runtime bridge currently starts inside the existing legacy shell path. No Avalonia code runs in that process.

## Desktop-host ownership

`Cortex.Host.Avalonia` owns:

- connecting to the named pipe bridge
- opening a bridge session
- mapping snapshots into Avalonia view-model state
- sending semantic intents back to the runtime
- showing connection and lifecycle status in the shell

Dock remains the structural owner of the desktop workbench layout.

## What crosses the bridge

Only bounded, versioned bridge messages cross the process boundary:

- session open handshake
- workbench snapshot push
- user intent/request back to the runtime
- operation result or acknowledgement
- diagnostics and connection status

The snapshot currently carries only the proven desktop surfaces for this phase:

- workbench identity and layout selection
- onboarding state and available choices
- settings document, navigation, selection, search, and draft state
- workspace root, discovered projects, tree, selected project, and file preview
- editor document summary and active editor status
- search query, result groups, and active match state
- reference browser status and decompile/source projection state
- runtime status and connection state

No Avalonia views, Unity objects, IMGUI layout state, textures, or raw UI event streams cross the bridge.

The shared workflow lane for those desktop surfaces currently lives in `Cortex.Shell.Shared`:

- `EditorWorkbenchModel`
- `SearchWorkbenchModel`
- `ReferenceWorkbenchModel`
- `SearchQueryModel`, `SearchDocumentResultModel`, and `SearchMatchModel`

## Transport

The current bridge transport is a local Windows-first named pipe.

- default pipe name: `cortex.desktop.bridge`
- override pipe name for both processes: `CORTEX_DESKTOP_BRIDGE_PIPE_NAME`

The transport is replaceable behind a small interface boundary, but this phase intentionally implements exactly one real transport.
