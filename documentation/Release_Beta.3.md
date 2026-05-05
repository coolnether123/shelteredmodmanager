# Beta.3 Release Notes

This document is the release-facing checklist for Sheltered Mod Manager v1.3 Beta.3.

## Release Identity

- Public release label: `v1.3.0-beta.3`
- Package/application version: `1.3.0-beta.3`
- Assembly binding version: `1.3.0.0`
- File version: `1.3.0.3`

`AssemblyVersion` stays at `1.3.0.0` so mods built against the v1.3 API line keep loading. `AssemblyInformationalVersion` and manager-facing strings carry the beta.3 label.

## User-Facing Scope

- Split API surface: neutral `ModAPI.dll` plus Sheltered-specific `ShelteredAPI.dll`.
- Custom scenario browser, XML packs, authoring tools, dependency lockout, trigger runtime, scheduled effects, and win/loss support. This surface is experimental for Beta.3 testing.
- Custom-scenario save APIs now reject reserved built-in save ids such as `Standard`, `Vanilla.Surrounded`, `Vanilla.Stasis`, and draft storage. Mods should use the explicit `ShelteredSaves.*Standard` helpers for built-in save buckets.
- Scenario authoring writes `scenario.xml` through same-directory temp files, parse validation, replace, and `.bak` recovery files so failed writes preserve the previous XML.
- Rebindable vanilla and mod-defined keybindings with conflict handling and persistence.
- Desktop and in-game mod manager improvements, including mod metadata, load order, Nexus discovery, install file-set verification/rollback, and compatibility status.
- Unity log filtering is severity-aware: errors, asserts, and exceptions are never suppressed, and benign warning/log suppression is counted for diagnostics.
- Save expansion and save verification for missing mods and version mismatches.

## Documentation Readiness

- README installation, compatibility, uninstall, mod structure, authoring path, and support links reviewed for Beta.3.
- Developer guides now identify the current surface as v1.3 Beta.3.
- API signature and architecture docs keep the v1.3 breaking-line warning visible.
- Release-critical guides are linked from the README documentation table.

## Current All-Mod Launch Timing

Measured from `D:\Epic Games\Sheltered\SMM\mod_manager.log` on 2026-05-05 with the Epic x64 build, ModAPI `1.3.0`, ShelteredAPI `1.3.0.0`, and every mod currently discovered by Sheltered enabled.

- Discovered mods: 17.
- Loaded plugins: 17.
- Startup errors: 0.
- Doorstop process start to ModAPI handoff: 6,493ms.
- Loader launch start to ModAPI handoff: 6,221ms.
- PluginManager startup complete: 4,015ms.
- PrepareModLoads total: 116ms foreground, 2,013ms background.
- Sliced activation window: about 1,363ms from schedule to complete.

| Load order | Mod id | Activation time |
| --- | --- | ---: |
| 1 | `coolnether123.shelteredvanillafixes` | 89ms |
| 2 | `coolnether123.sheltereddisplayfixes` | 15ms |
| 3 | `coolnether123.bunkerrandomlocation` | 186ms |
| 4 | `coolnether123.lifespan` | 173ms |
| 5 | `coolnether123.familyexpansion` | 53ms |
| 6 | `coolnether123.deepexpansion` | 29ms |
| 7 | `coolnether123.shelteredsystemsexpansion` | 70ms |
| 8 | `coolnether123.fourpersonexpeditions` | 187ms |
| 9 | `factionoverhaul` | 272ms |
| 10 | `coolnether123.betteraiqueue` | 52ms |
| 11 | `coolnether123.tradingamount` | 10ms |
| 12 | `volodya14.familypresets.v2` | 9ms |
| 13 | `volodya14.worldtimecontrol` | 78ms |
| 14 | `volodya14.earthquake` | 14ms |
| 15 | `volodya14.autoneedsmod` | 14ms |
| 16 | `volodya14.characterstatseditor` | 18ms |
| 17 | `volodya14.colorfulappearance` | 11ms |

## Pre-Publish Checklist

- Build `ShelteredModManager.sln` in Release.
- Run `tools\verify-modapi-boundary.cmd`.
- Run `tools\verify-shelteredapi-public-surface.cmd`.
- Run `tools\test-shelteredapi-contracts.cmd`.
- Run `tools\verify-runtimecompat-rect.cmd`.
- Smoke test Steam/GOG package against `Sheltered.exe`.
- Smoke test Epic package against `ShelteredWindows64_EOS.exe`.
- Verify `SMM\Manager.exe` About tab shows `Version 1.3.0-beta.3`.
- Verify installed API versions show `1.3.0.3` in the manager.
- Verify the Family Expansion mod package has been rebuilt against ModAPI/ShelteredAPI 1.3 Beta.3 before listing it as compatible.
