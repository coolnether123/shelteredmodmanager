# 2.0 Beta.1 Release Notes

This document is the release-facing checklist for Sheltered Mod Manager v2.0 Beta.1.
It supersedes the previous Beta.3 release checklist for the public 2.0 release-candidate line.

## Release Identity

- Public release label: `v2.0.0-beta.1`
- Package/application version: `2.0.0-beta.1`
- Assembly binding version: `2.0.0.0`
- File version: `2.0.0.1`

`AssemblyVersion` moves to `2.0.0.0` for the public major compatibility break. `AssemblyInformationalVersion` and manager-facing strings carry the beta.1 label.

## User-Facing Scope

- Split API surface: neutral `ModAPI.dll` plus Sheltered-specific `ShelteredAPI.dll`.
- Custom scenario browser, XML packs, authoring tools, dependency lockout, trigger runtime, scheduled effects, and win/loss support. This surface is experimental for Beta.1 testing.
- Custom-scenario save APIs now reject reserved built-in save ids such as `Standard`, `Vanilla.Surrounded`, `Vanilla.Stasis`, and draft storage. Mods should use the explicit `ShelteredSaves.*Standard` helpers for built-in save buckets.
- Scenario authoring writes `scenario.xml` through same-directory temp files, parse validation, replace, and `.bak` recovery files so failed writes preserve the previous XML.
- Save backup lineage support records manager-created save backups so users can recover from failed save writes or compatibility testing regressions.
- Rebindable vanilla and mod-defined keybindings with conflict handling and persistence.
- Desktop and in-game mod manager improvements, including mod metadata, load order, Nexus discovery, install file-set verification/rollback, and compatibility status.
- Unity log filtering is severity-aware: errors, asserts, and exceptions are never suppressed, and benign warning/log suppression is counted for diagnostics.
- Save expansion and save verification for missing mods and version mismatches.

## Beta Safety And Compatibility

- This is a public beta, not stable 2.0.
- Players should back up saves before testing Beta.1 packages.
- The custom scenario browser/editor and Stasis/Surrounded expanded saves remain active Beta.1 testing surfaces.
- Family Expansion and Deep Expansion should be treated as not compatible until rebuilt and smoke tested against ModAPI/ShelteredAPI 2.0 Beta.1.
- Some 1.2.2 mods may break because Sheltered-specific API surface moved from `ModAPI.dll` to `ShelteredAPI.dll`.
- Bug reports should include storefront/version, mod list, save type, custom scenario editor toggle state, reproduction steps, and `SMM\mod_manager.log`.

## Documentation Readiness

- README installation, compatibility, uninstall, mod structure, authoring path, and support links reviewed for Beta.1.
- Developer guides now identify the current surface as v2.0 Beta.1.
- API signature and architecture docs keep the v2.0 breaking-line warning visible.
- Release-critical guides are linked from the README documentation table.

## Current All-Mod Launch Timing

Measured from `D:\Epic Games\Sheltered\SMM\mod_manager.log` on 2026-05-05 with the Epic x64 build, ModAPI `2.0.0.0`, ShelteredAPI `2.0.0.0`, and every mod currently discovered by Sheltered enabled.

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

- Confirm Visual Studio 2022 MSBuild is used for solution builds; do not use `dotnet build` for this legacy solution.
- Confirm local Sheltered/Unity reference paths resolve to the intended Steam/GOG or Epic managed assemblies.
- Use `C:\Program Files (x86)\GOG Galaxy\Games\Sheltered` as the current GOG staging install when copying builds for local release verification.
- Build `ShelteredModManager.sln` in Release.
- Run `tools\verify-modapi-boundary.cmd`.
- Run `tools\verify-shelteredapi-public-surface.cmd`.
- Run `tools\test-shelteredapi-contracts.cmd`.
- Run `tools\verify-runtimecompat-rect.cmd`.
- Run `tools\scan-stale-version-references.cmd`.
- Smoke test Steam/GOG package against `Sheltered.exe`.
- Smoke test Epic package against `ShelteredWindows64_EOS.exe`.
- Verify `SMM\Manager.exe` About tab shows `Version 2.0.0-beta.1`.
- Verify installed API versions show `2.0.0.1` in the manager.
- Verify the Family Expansion mod package has been rebuilt against ModAPI/ShelteredAPI 2.0 Beta.1 before listing it as compatible.
