# Agent 7 UI Seams Status

Last updated: 2026-05-29

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## Scope

Paralives UI extension seams for opening native selected-character windows and contributing simple content rows to the native occupation panel. Homeschool-specific UI behavior was not moved into `ParalivesAPI`.

## What Changed

- Preserved existing `ParalivesUiFacade.OpenOccupationsForCharacter(...)` overloads and their existing no-submenu behavior.
- Added native character tab helpers on `ParalivesUiFacade`:
  - `OpenOccupations(characterGuid)`
  - `OpenOccupations(characterGuid, playerIndex)`
  - `OpenOccupations(characterGuid, playerIndex, occupationIndex)`
  - `OpenSkills(characterGuid)`
  - `OpenSkills(characterGuid, playerIndex)`
  - `OpenCharacterTab(characterGuid, tab)`
  - `OpenCharacterTab(characterGuid, tab, playerIndex)`
- Added `ParalivesCharacterTab` for known native selected-character tabs.
- Added a general occupation panel provider contract:
  - `IParalivesOccupationPanelProvider.CanProvide(characterGuid, occupationIndex)`
  - `IParalivesOccupationPanelProvider.BuildPanel(characterGuid, occupationIndex)`
- Added simple UI DTOs:
  - `ParalivesUiText`
  - `ParalivesOccupationPanel`
  - `ParalivesOccupationPanelRow`
- Added an internal occupation panel provider registry with disposable registration handles, delegate registration support, and guarded provider exception logging.
- Wired provider registration through `ParalivesUiFacade.RegisterOccupationPanelProvider(...)`, `UnregisterOccupationPanelProvider(...)`, and `ParalivesUiFacade.Extensions`.
- Added the governed `UIOccupationsUpdateSelectedOccupationPatch` postfix to append provider rows after native `UIOccupations.UpdateSelectedOccupation()` refreshes the panel.
- Avoided compile-time references to `Unity.TextMeshPro`, `Unity.Mathematics`, and `UnityEngine.UI` from the provider seam so `ParalivesAPI.csproj` did not need new assembly references.

## Files Touched

- `ParalivesAPI/Core/ParalivesUiFacade.cs`
- `ParalivesAPI/Core/ParalivesUiModels.cs`
- `ParalivesAPI/Core/ParalivesUiExtensionFacade.cs`
- `ParalivesAPI/Core/ParalivesOccupationPanelProvider.cs`
- `ParalivesAPI/Patches/UIOccupationsUpdateSelectedOccupationPatch.cs`
- `ParalivesAPI/ParalivesAPI.csproj`
- `documentation/agent_status/agent-7-ui-seams.md`

`ParalivesAPI.csproj` already had concurrent Agent 1/API scaffold edits. This agent added the minimal compile entries required for the UI seam files and new patch host.

## UI Seams Used

- Native windows only: `UICharacterSubMenuBar`, `UIOccupations`, `UISkills`, and other registered selected-character tabs.
- `OpenOccupations(...)` and `OpenSkills(...)` select the requested character, show the native character submenu, and open the registered native window.
- Existing `OpenOccupationsForCharacter(...)` keeps its previous path and still supports selecting an occupation index.
- Occupation panel providers append rows through `UIOccupations.UIListPerformanceDataItems` using the native `UIOccupationsJobPerformanceDataItem` row type.

## Provider Contracts Added

- `IParalivesOccupationPanelProvider`
- `ParalivesUiText`
- `ParalivesOccupationPanel`
- `ParalivesOccupationPanelRow`

Rows support plain text or API-localization keys plus tooltip text and a thumbs-up/thumbs-down state. Providers can optionally clear vanilla performance rows before adding their own rows, but default behavior is additive.

## Raw UI Limitations Found

- `UI.Get<T>()` can only open prefab types registered in `ReferencesToUIPrefabs`; no custom `UIWindow` prefab injection was attempted.
- `UIOccupations.UpdateSelectedOccupation()` is private, so the provider seam uses a Harmony patch by method name.
- `UIOccupations` reads the currently selected character every visible update, so helper open calls must select the target character first.
- The native performance row prefab only exposes a label, tooltip, and thumbs icon. Rich custom layouts would need a separate, riskier UI surface.
- Updating the native performance amount text would require touching `TextMeshProUGUI`, which `ParalivesAPI.csproj` does not currently reference. That was intentionally left out of this seam.

## Assumptions Made

- `UISkills` is safe to expose because it is registered in the native selected-character prefab set and reads the selected character like `UIOccupations`.
- The existing `paralives.ui.windows.v1` capability covers native window open helpers for now.
- A separate capability such as `paralives.ui.occupationPanelProviders.v1` should be added by Agent 1 only if the API contract owner wants feature-level capability advertising.
- The old-style `ParalivesAPI.csproj` explicit compile list required minimal entries for new UI seam files.
- `documentation/agent_status` appeared after the initial required-read pass. Agent 1 and Agent 2 notes were read before this final note update.

## Risks

- `UIOccupations.UpdateSelectedOccupation()` can break if a Paralives update renames or reshapes the private method.
- Multiple providers are composed in registration order; a provider that sets `ReplacePerformanceRows` can clear rows appended by earlier providers.
- Provider rows are rebuilt on each native occupation refresh; this relies on native `UIList` pooling behavior.
- `ParalivesAPI.csproj` has concurrent edits from other agents and remains a coordination point.

## Tests And Verification

Build command run:

```text
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ShelteredModManager.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

Result: passed.

```text
ParalivesAPI -> A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\Dist\SMM\bin\ParalivesAPI.dll
```

Verification:

```text
cmd /c tools\verify-modapi-boundary.cmd
ModAPI boundary verifier failed. New or increased violations: 1
NEW	source-symbol	ModAPI/Core/IGameHelper.cs	Localization	2
```

```text
cmd /c tools\verify-runtimecompat-rect.cmd
RuntimeCompat Rect verifier passed.
```

```text
cmd /c tools\scan-stale-version-references.cmd -FailOnChange
documentation/agent_status/agent-1-api-contracts.md:112	change: release-facing stale version reference	Manager/Core/AppVersionInfo.cs:5	change: release-facing stale version reference	public const string Current = "1.3.0-beta.3";
documentation/agent_status/agent-1-api-contracts.md:113	change: release-facing stale version reference	Manager/ManagerGUI.csproj:25	change: release-facing stale version reference	<ApplicationVersion>1.3.0.3</ApplicationVersion>
documentation/agent_status/agent-1-api-contracts.md:114	change: release-facing stale version reference	Manager/Properties/AssemblyInfo.cs:29	change: release-facing stale version reference	[assembly: AssemblyVersion("1.3.0.0")]
documentation/agent_status/agent-1-api-contracts.md:115	change: release-facing stale version reference	Manager/Properties/AssemblyInfo.cs:30	change: release-facing stale version reference	[assembly: AssemblyFileVersion("1.3.0.3")]
documentation/agent_status/agent-1-api-contracts.md:116	change: release-facing stale version reference	Manager/Properties/AssemblyInfo.cs:31	change: release-facing stale version reference	[assembly: AssemblyInformationalVersion("1.3.0-beta.3")]
documentation/agent_status/agent-1-api-contracts.md:126	change: release-facing stale version reference	- A coordination pass should resolve the pre-existing `ModAPI/Core/IGameHelper.cs` boundary verifier finding and stale Manager 1.3 version metadata.
Manager/Core/AppVersionInfo.cs:5	change: release-facing stale version reference	public const string Current = "1.3.0-beta.3";
Manager/ManagerGUI.csproj:25	change: release-facing stale version reference	<ApplicationVersion>1.3.0.3</ApplicationVersion>
Manager/Properties/AssemblyInfo.cs:29	change: release-facing stale version reference	[assembly: AssemblyVersion("1.3.0.0")]
Manager/Properties/AssemblyInfo.cs:30	change: release-facing stale version reference	[assembly: AssemblyFileVersion("1.3.0.3")]
Manager/Properties/AssemblyInfo.cs:31	change: release-facing stale version reference	[assembly: AssemblyInformationalVersion("1.3.0-beta.3")]
tools/Scan-StaleVersionReferences.ps1:105	keep: scanner pattern text	Write-Host "No stale 1.3/Beta.3 references found."
Stale version scan complete. Findings: 28. Change candidates: 17.
```

The stale-version output also included generated/historical documentation artifacts and this status note echoing the same findings. The release-facing stale Manager version metadata is outside this agent's scope.

Additional check:

```text
git diff --check -- ParalivesAPI\Core\ParalivesUiFacade.cs ParalivesAPI\Core\ParalivesUiModels.cs ParalivesAPI\Core\ParalivesOccupationPanelProvider.cs ParalivesAPI\Core\ParalivesUiExtensionFacade.cs ParalivesAPI\Patches\UIOccupationsUpdateSelectedOccupationPatch.cs ParalivesAPI\ParalivesAPI.csproj
```

Result: no whitespace errors. Git reported only line-ending normalization warnings for `ParalivesUiFacade.cs` and `ParalivesAPI.csproj`.

## Follow-Up Needed

- Agent 1 should decide whether to add `paralives.ui.occupationPanelProviders.v1` as a capability string.
- Boundary owner should resolve the existing `ModAPI/Core/IGameHelper.cs` localization verifier finding.
- Release/version owner should resolve stale Manager `1.3.0-beta.3` metadata so the stale-version scanner can pass.
