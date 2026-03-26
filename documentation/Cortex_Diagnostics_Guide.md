# Cortex Diagnostics Guide

This guide documents the Cortex diagnostics path for internal Cortex code and future loader integrations.

The important boundary is:

- Cortex code depends on `Cortex.Core.Diagnostics`
- platform adapters decide how diagnostics are enabled and where they are written
- no Cortex feature should depend directly on `ModAPI.Core.MMLog` or another loader logger

## Goals

Use Cortex diagnostics when you need:

- temporary workflow tracing
- opt-in module or editor diagnostics
- platform-routed debug output
- a debugging path that can survive loader replacement

Do not add ad hoc `MMLog` calls to editor surfaces or shell orchestration code for temporary tracing unless the message is real product logging that should always exist.

## Core API

Diagnostics are defined in `Cortex.Core.Diagnostics`:

- `ICortexDiagnosticConfiguration`
- `CortexDiagnostics`
- `CortexDiagnosticLogger`

Typical usage:

```csharp
using Cortex.Core.Diagnostics;

internal static class ExampleDiagnostics
{
    private static readonly CortexDiagnosticLogger SelectionDiagnostics =
        CortexDiagnostics.ForChannel("editor.selection", "Cortex.Editor");

    public static void WriteSelection(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        SelectionDiagnostics.WriteInfo(message);
    }
}
```

## Channel rules

Channel names should describe the behavior being traced, not the current host implementation.

Recommended pattern:

- `editor.hover`
- `editor.selection`
- `editor.context-menu`
- `editor.scroll`
- `language.requests`
- `harmony.preview`

Use dotted names and stable semantics so future loaders can expose them consistently.

## Platform boundary

`ICortexPlatformModule` now exposes `DiagnosticConfiguration`.

That means:

- Cortex core and shell code only ask whether a diagnostic channel is enabled
- the active platform module decides the enablement rule
- the active log sink decides where the message is written

Today the ModAPI adapter implements this through `ModApiCortexDiagnosticConfiguration`, but that is not the contract. A different loader can replace it by implementing `ICortexDiagnosticConfiguration` and `ICortexLogSink`.

## Current ModAPI adapter

The current ModAPI adapter reads these optional keys from `SMM/bin/mod_manager.ini`:

- `CortexDiagnostics`
- `CortexDiagnosticsLevel`

Examples:

```ini
CortexDiagnostics=editor.selection,editor.context-menu
CortexDiagnosticsLevel=Info
```

```ini
CortexDiagnostics=editor.*
CortexDiagnosticsLevel=Debug
```

```ini
CortexDiagnostics=all
CortexDiagnosticsLevel=Info
```

Rules:

- if `CortexDiagnostics` is missing, Cortex diagnostics are disabled
- `all`, `*`, or `true` enables every channel
- `prefix*` enables a channel prefix
- `CortexDiagnosticsLevel` defaults to `Info`

These keys belong to the current ModAPI-backed loader adapter. They should be treated as a host implementation detail, not as a dependency that Cortex code relies on.

## Logging guidance

Use `CortexLog` for:

- normal product logging
- important lifecycle messages
- failures that should appear in standard logs

Use `CortexDiagnostics` for:

- opt-in tracing
- investigation paths
- high-volume or behavior-specific diagnostics

If a trace is only useful during investigation, put it behind a diagnostic channel.

## Design rule

When adding diagnostics:

1. Define a stable Cortex channel name.
2. Route writes through `CortexDiagnostics.ForChannel(...)`.
3. Keep loader-specific parsing or config in the platform module.
4. Remove one-off local debug plumbing once the channel exists.

This keeps debugging DRY, keeps the shell from turning into a logging god object, and preserves loader replaceability.
