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

## Upgrade from RC6

RC6 already displays 2.0.0. Its update check requires a newer version, and its
updater leaves pending Nexus links and generated archives in the old SMM
folder. Use the one-time script to run the final updater. It checks the
download, carries over settings and pending Nexus files, and keeps a copy of
RC6. Your mod order and saves live outside SMM and stay in place.

1. Close Sheltered and Sheltered Mod Manager.
2. Download `Upgrade-SmmRc6.ps1`, `ShelteredModManager-2.0.0-Update.zip`,
   and the ZIP's `.sha256` file from the final GitHub release. Put all
   three in the same folder.
3. Open Windows PowerShell in that folder. If Windows protects your game
   folder, open PowerShell as administrator. Run this command with your
   installed SMM folder.

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Upgrade-SmmRc6.ps1 -SmmPath "C:\Games\Sheltered\SMM" -PackagePath ".\ShelteredModManager-2.0.0-Update.zip"
   ```

The final updater starts the manager and restores RC6 if startup fails. To
roll back after a successful upgrade, close both programs, rename the current
`SMM` folder to `SMM-final`, then move the `rc6-backup-…` folder printed
by the script back beside `mods` and rename it to `SMM`. Keep `SMM-final`
if you need settings or Nexus files created since upgrading.

Include `tools/Upgrade-SmmRc6.ps1` with the final GitHub release assets.
Upload the same neutral Update ZIP to Nexus as the manager update file. Put
`sha256:<ZIP hash>` in that file's description. The manager offers a
same-version replacement when the advertised hash differs from the installed
package's hash, then verifies the ZIP before staging it.

## Nexus sign-in

In Manager, open **Settings**, enable Nexus features and paste your [personal Nexus API key](https://www.nexusmods.com/users/myaccount?tab=api).
Select **Save API key** to verify it. The key is protected for your Windows user. Use **Clear key** to sign out.
Premium members can download directly. Other members approve each download on Nexus using **Mod Manager Download**.
These steps also apply to Manager updates from Nexus.

## Build release packages

Run the canonical packaging script from the repository root:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File tools\New-ReleasePackages.ps1 -Configuration Release
```

The script rebuilds the four release products, runs the Nexus, updater, and scenario-editor contracts, checks the package allowlist and PE architectures, and writes hashes under `artifacts/release-packages`.

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
