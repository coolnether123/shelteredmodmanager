# SMM 2.0 release notes

SMM 2.0 is in prerelease review. This page describes the planned `v2.0.0` release and its remaining release gates. It does not mean that a final tag exists.

## Release identity

- Release label: `v2.0.0`
- Package version: `2.0.0`
- Assembly and file version: `2.0.0.0`

## Changes for players

- The modding API is split into the neutral `ModAPI.dll` and the Sheltered-specific `ShelteredAPI.dll`.
- Custom scenarios support XML and code registration, dependency checks, triggers, scheduled effects, scoring snapshots, and win or loss conditions.
- The optional `ShelteredScenarioEditor.dll` contains advanced in-game authoring and defaults off.
- Surrounded, Stasis, and modded scenarios have separate save archives in the Custom Scenarios window. The stock vanilla saves remain in the vanilla scenario window.
- Rebindable vanilla and mod controls share conflict checks and persistence.
- The manager verifies installed file sets and can roll back a failed mod replacement.
- Save verification reports missing mods and version mismatches.

Custom-scenario save APIs reject reserved built-in save IDs such as `Standard`, `Vanilla.Standard`, `Vanilla.Surrounded`, and `Vanilla.Stasis`. Use the explicit `ShelteredSaves` standard-slot helpers for those buckets.

## Compatibility

Mods built for SMM 1.2.2 or the old 1.3 beta may need a 2.0 rebuild. Back up saves and test framework upgrades with a disposable save. See the [player migration guide](SMM_2.0_Migration.md) and [modder migration guide](For_Modders_2.0_API_Migration.md).

## Nexus limitation

Nexus has not issued the public OAuth client ID. Metadata browsing works, but sign-in and direct install remain unavailable. Stable promotion stays blocked until registration and real-account evidence are complete.

## Build release packages

Run the canonical packaging script from the repository root:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File tools\New-ReleasePackages.ps1 -Configuration Release
```

The script rebuilds the solution, runs the Nexus, updater, and scenario-editor contracts, checks the package allowlist and PE architectures, and writes hashes under `artifacts/release-packages`.

Also run the assembly guardrails:

```cmd
tools\verify-modapi-boundary.cmd
tools\verify-shelteredapi-public-surface.cmd
tools\test-shelteredapi-contracts.cmd
tools\verify-runtimecompat-rect.cmd
tools\scan-stale-version-references.cmd
```

## Manual release checks

- Launch once with `ShelteredScenarioEditor.dll` absent, once present and disabled, and once present and enabled.
- Smoke test the x86 Steam or GOG package with `Sheltered.exe`.
- Smoke test the x64 Epic package with `ShelteredWindows64_EOS.exe`.
- Confirm that the manager shows version `2.0.0` and both API assemblies show `2.0.0.0`.
- Rebuild and smoke test each companion mod before listing it as compatible.
- Publish SMM before companion mod packages that require the 2.0 APIs.
- Include the Doorstop antivirus false-positive note in release text.
