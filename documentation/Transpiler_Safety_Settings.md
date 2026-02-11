# Transpiler Safety Settings

This document explains global safety flags used by ModAPI transpiler systems.

## Location

Flags are read via `ModAPI.Core.ModPrefs` and stored in ModAPI user settings.

## Flags

- `TranspilerSafeMode` (default `true`)
  - Master safety toggle.
  - Enables guardrail behavior in transpiler pipeline.

- `TranspilerForcePreserveInstructionCount` (default `true`)
  - Forces `ReplaceAllPatterns` to keep instruction count stable.
  - Protects branch targets when replacing multi-instruction spans.

- `TranspilerFailFastCritical` (default `true`)
  - Critical warnings become hard failures.
  - Prevents runtime execution of known-dangerous IL states.

- `TranspilerCooperativeStrictBuild` (default `true`)
  - Cooperative transpiler steps build in strict mode.
  - Failing steps are skipped instead of partially applied.

- `TranspilerQuarantineOnFailure` (default `true`)
  - Quarantines patch owners in cooperative mode after critical failures.
  - Subsequent anchors from that owner are skipped for the run.

## Why These Defaults Exist

These defaults are intentionally conservative. They protect game stability when mods use brittle IL patterns or stale assumptions after updates.

If you need temporary flexibility for debugging, you can disable individual flags, but production runs should keep safe defaults enabled.
