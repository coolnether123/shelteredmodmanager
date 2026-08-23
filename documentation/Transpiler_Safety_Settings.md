# Transpiler safety settings

This document explains global safety flags used by ModAPI transpiler systems.

## Storage

Flags are read via `ModAPI.Core.ModPrefs` and stored in ModAPI user settings.

## Flags

| Flag | Default | Effect |
| --- | --- | --- |
| `TranspilerSafeMode` | `true` | Enables the transpiler safety checks. |
| `TranspilerForcePreserveInstructionCount` | `true` | Preserves instruction count for multi-instruction pattern replacements and rejects unsafe padding. |
| `TranspilerFailFastCritical` | `true` | Turns critical validation warnings into build failures. |
| `TranspilerCooperativeStrictBuild` | `true` | Builds cooperative steps in strict mode and skips a failing step. |
| `TranspilerQuarantineOnFailure` | `true` | Skips later cooperative anchors from an owner after its critical failure. |
| `TranspilerLogValidationWarnings` | `false` | Writes warning-level validation diagnostics. |
| `TranspilerWarnOnVirtualCallMismatch` | `true` | Warns when a call replacement can change virtual dispatch. |
| `TranspilerWarnOnExceptionHandlerMethods` | `true` | Warns when a target method contains exception-handler regions. |

Keep the safe defaults in production. Change one flag at a time when diagnosing a failed transform.

## `StackSentinel` limitation

`StackSentinel` fails validation for methods with `try`, `catch`, `finally`, or filter clauses because it does not model exception flow.

`ReplaceSequence` and `ReplaceAllPatterns` preserve Harmony exception markers and require exact index-aligned replacements on methods with exception handlers.
