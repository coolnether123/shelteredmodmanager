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
- Rebindable vanilla and mod-defined keybindings with conflict handling and persistence.
- Desktop and in-game mod manager improvements, including mod metadata, load order, Nexus discovery, and compatibility status.
- Save expansion and save verification for missing mods and version mismatches.

## Documentation Readiness

- README installation, compatibility, uninstall, mod structure, authoring path, and support links reviewed for Beta.3.
- Developer guides now identify the current surface as v1.3 Beta.3.
- API signature and architecture docs keep the v1.3 breaking-line warning visible.
- Release-critical guides are linked from the README documentation table.

## Pre-Publish Checklist

- Build `ShelteredModManager.sln` in Release.
- Run `tools\verify-modapi-boundary.cmd`.
- Run `tools\verify-shelteredapi-public-surface.cmd`.
- Run `tools\test-shelteredapi-contracts.cmd`.
- Smoke test Steam/GOG package against `Sheltered.exe`.
- Smoke test Epic package against `ShelteredWindows64_EOS.exe`.
- Verify `SMM\Manager.exe` About tab shows `Version 1.3.0-beta.3`.
- Verify installed API versions show `1.3.0.3` in the manager.
- Verify the Family Expansion mod package has been rebuilt against ModAPI/ShelteredAPI 1.3 Beta.3 before listing it as compatible.
