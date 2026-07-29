# Agent 1 API Contracts Status

Date: 2026-05-29

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## What Changed

- Added the initial Paralives API version and capability model:
  - `ParalivesApiVersion`
  - `ParalivesCapability`
  - `ParalivesCapabilityRegistry`
- Wired version and capability metadata into `ParalivesRuntimeInfo.Current` with additive properties:
  - `Version`
  - `ApiVersion`
  - `AdapterVersion`
  - `Capabilities`
  - `CapabilityStrings`
  - `HasCapability(...)`
- Added matching metadata passthrough properties to `ParalivesGameFacade`.
- Added namespace scaffolds for:
  - `ParalivesAPI.Stable`
  - `ParalivesAPI.Native`
  - `ParalivesAPI.Unsafe`
- Updated the explicit old-style `ParalivesAPI.csproj` compile list for the new source files.
- Did not move existing facades or change existing service-locator registration behavior.

## Files Touched

- `ParalivesAPI/ParalivesAPI.csproj`
- `ParalivesAPI/Core/ParalivesRuntimeInfo.cs`
- `ParalivesAPI/Core/ParalivesGameFacade.cs`
- `ParalivesAPI/Core/ParalivesApiVersion.cs`
- `ParalivesAPI/Core/ParalivesCapability.cs`
- `ParalivesAPI/Core/ParalivesCapabilityRegistry.cs`
- `ParalivesAPI/Stable/IParalivesRuntime.cs`
- `ParalivesAPI/Stable/IParalivesCharacters.cs`
- `ParalivesAPI/Stable/IParalivesInteractions.cs`
- `ParalivesAPI/Stable/IParalivesActions.cs`
- `ParalivesAPI/Stable/IParalivesOccupations.cs`
- `ParalivesAPI/Stable/IParalivesUi.cs`
- `ParalivesAPI/Stable/IParalivesSaveStorage.cs`
- `ParalivesAPI/Native/IParalivesNativeApi.cs`
- `ParalivesAPI/Unsafe/IParalivesUnsafeApi.cs`
- `documentation/agent_status/agent-1-api-contracts.md`

## Interfaces / Contracts Added

- `ParalivesAPI.Stable.IParalivesRuntime`
- `ParalivesAPI.Stable.IParalivesCharacters`
- `ParalivesAPI.Stable.IParalivesInteractions`
- `ParalivesAPI.Stable.IParalivesActions`
- `ParalivesAPI.Stable.IParalivesOccupations`
- `ParalivesAPI.Stable.IParalivesUi`
- `ParalivesAPI.Stable.IParalivesSaveStorage`
- `ParalivesAPI.Native.IParalivesNativeApi`
- `ParalivesAPI.Unsafe.IParalivesUnsafeApi`

These are initial contracts only. Existing facades were not forced to implement them in this pass.

## Capability Strings Added

- `paralives.runtime.v1`
- `paralives.interactions.content.v1`
- `paralives.actions.completion.v1`
- `paralives.characters.native.v1`
- `paralives.occupations.attendancePolicy.v1`
- `paralives.ui.windows.v1`

Recommended string pattern for future additions: `paralives.<domain>.<feature>.v1`. Use a `.native.v1` segment when the capability deliberately exposes raw Paralives runtime types. Do not introduce `v2` names unless the contract is intentionally breaking.

## Assumptions Made

- Initial API version and adapter version are both `1.0.0`, matching the current `ParalivesAPI` assembly version line.
- Stable contracts should be game-facing primitives and existing ParalivesAPI event DTOs, not raw decompiled manager types.
- Native and unsafe namespaces are marker scaffolds for future expansion and should not receive moved facade implementations in this task.
- Other agents may add capability strings later by adding a constant to `ParalivesCapability` and registering it in `ParalivesCapabilityRegistry.CreateDefault()`.

## Risks

- Stable interfaces are placeholders until future adapter work implements or aggregates them; current mod code should keep using the existing `ParalivesRuntimeInfo.Current` facades.
- There are parallel worktree edits outside this assignment, including `ParalivesAPI/Core/ParalivesUiFacade.cs` and new untracked ParalivesAPI model/facade files. They were not modified by this agent.
- `ParalivesAPI.csproj` is now a coordination point. Other agents should avoid editing it without checking this note first.

## Tests / Build Run

Build:

```text
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ShelteredModManager.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

Result: passed. MSBuild emitted existing warning noise from `ModAPI` collection compatibility, obsolete Unity scene APIs, XML comments, and unused fields; no build errors.

Verification:

```text
tools\verify-runtimecompat-rect.cmd
RuntimeCompat Rect verifier passed.
```

```text
tools\verify-modapi-boundary.cmd
ModAPI boundary verifier failed. New or increased violations: 1
NEW	source-symbol	ModAPI/Core/IGameHelper.cs	Localization	2
```

```text
tools\scan-stale-version-references.cmd -FailOnChange
Manager/Core/AppVersionInfo.cs:5	change: release-facing stale version reference	public const string Current = "1.3.0-beta.3";
Manager/ManagerGUI.csproj:25	change: release-facing stale version reference	<ApplicationVersion>1.3.0.3</ApplicationVersion>
Manager/Properties/AssemblyInfo.cs:29	change: release-facing stale version reference	[assembly: AssemblyVersion("1.3.0.0")]
Manager/Properties/AssemblyInfo.cs:30	change: release-facing stale version reference	[assembly: AssemblyFileVersion("1.3.0.3")]
Manager/Properties/AssemblyInfo.cs:31	change: release-facing stale version reference	[assembly: AssemblyInformationalVersion("1.3.0-beta.3")]
Stale version scan complete. Findings: 16. Change candidates: 5.
```

The two failing verification outputs are outside this agent's allowed file scope and were not modified.

## Follow-Up Needed

- Future facade agents can implement the `ParalivesAPI.Stable` interfaces incrementally once their domains are ready.
- A save lifecycle agent should provide the eventual `IParalivesSaveStorage` implementation.
- A coordination pass should resolve the pre-existing `ModAPI/Core/IGameHelper.cs` boundary verifier finding and stale Manager 1.3 version metadata.
