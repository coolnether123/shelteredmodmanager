# Agent 8 Docs Verification Status

Last updated: 2026-05-29

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## Scope

Documentation, diagrams/status support, and lightweight verification tooling for the Paralives API seam/refactor work. This pass did not modify `ParalivesAPI` source files or project files.

## What Changed

- Added the agent status note convention for concurrent refactor work.
- Added a Paralives API seam map with current raw seams, a Mermaid boundary diagram, Stable/Native/Unsafe boundary rules, grouped systems, facade ownership, and current versus proposed directions.
- Added a Paralives API public-surface document summarizing the current scan result and raw native exposure categories.
- Added a Paralives API refactor status tracker with all assigned agents and known ownership.
- Linked the new docs from `documentation/README.md`.
- Added a lightweight public-surface scanner for `ParalivesAPI`.

## Files Touched

- `documentation/README.md`
- `documentation/Paralives_API_Seams.md`
- `documentation/Paralives_API_Public_Surface.md`
- `documentation/Paralives_API_Refactor_Status.md`
- `documentation/agent_status/README.md`
- `documentation/agent_status/agent-8-docs-verification.md`
- `tools/Verify-ParalivesApiSurface.ps1`
- `tools/verify-paralivesapi-surface.cmd`

## Documentation Added

- `documentation/Paralives_API_Seams.md`
- `documentation/Paralives_API_Public_Surface.md`
- `documentation/Paralives_API_Refactor_Status.md`
- `documentation/agent_status/README.md`

## Verification Scripts Added

- `tools/Verify-ParalivesApiSurface.ps1`
- `tools/verify-paralivesapi-surface.cmd`

Default behavior is non-invasive: the scanner lists public type counts and warns about raw game type exposure outside `ParalivesAPI.Native` and `ParalivesAPI.Unsafe`. Use `-FailOnRawGameTypes` later when the Stable/Native/Unsafe split is ready to enforce.

## Assumptions

- Agent 1's status note is authoritative for the current API contract scaffolding and capability metadata.
- Agents 2 through 7 have not yet written status notes in this worktree, so their rows in the refactor tracker are inferred from the assignment prompt.
- `ParalivesAPI.Stable`, `ParalivesAPI.Native`, and `ParalivesAPI.Unsafe` are current marker/contract namespaces, but most existing concrete facades still live under `ParalivesAPI.Core`.
- No strict public-surface baseline should be created until the owning agents finish their facade splits.

## Risks

- `ParalivesAPI.csproj` and several `ParalivesAPI/Core` source files were changed concurrently by other agents. This pass read those changes for documentation but did not edit them.
- Current public signatures still expose raw Paralives game types in `ParalivesAPI.Core`; the new verifier reports these as warnings by default.
- The stale-version scanner currently scans agent status notes, so copying stale-version failure lines into status notes can create recursive findings.

## Tests And Verification

```text
tools\verify-paralivesapi-surface.cmd
ParalivesAPI public-surface scan completed.
Public type declarations: 138
Public raw game type exposures outside allowed namespaces: 141
Result: passed with warnings only.
```

```text
tools\verify-paralivesapi-surface.cmd -ListCurrent
Result: passed. Current public-surface list was generated; summary count is 113 classes, 14 enums, and 11 interfaces.
```

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
tools\test-shelteredapi-contracts.cmd
Get-Content : Cannot find path
'A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ShelteredAPI\Content\ContentRegistry.cs' because it does not
exist.
At A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\tools\Test-ShelteredApiContracts.ps1:19 char:12
+     return Get-Content -LiteralPath (Join-Path $RepoRoot $RelativePat ...
+            ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    + CategoryInfo          : ObjectNotFound: (A:\Dev\Worktree...tentRegistry.cs:String) [Get-Content], ItemNotFoundEx
   ception
    + FullyQualifiedErrorId : PathNotFound,Microsoft.PowerShell.Commands.GetContentCommand
```

```text
tools\verify-shelteredapi-public-surface.cmd
Get-ChildItem : Cannot find path 'A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ShelteredAPI' because it does
not exist.
At A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\tools\Verify-ShelteredApiPublicSurface.ps1:65 char:14
+ ...    $files = Get-ChildItem -LiteralPath $ApiRoot -Recurse -File -Filte ...
+                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    + CategoryInfo          : ObjectNotFound: (A:\Dev\Worktree...mm\ShelteredAPI:String) [Get-ChildItem], ItemNotFound
   Exception
    + FullyQualifiedErrorId : PathNotFound,Microsoft.PowerShell.Commands.GetChildItemCommand
```

```text
tools\scan-stale-version-references.cmd -FailOnChange
Result: failed. The scan reported 22 findings and 11 change candidates, including Agent 1's status note quoting prior stale-version output and existing Manager version metadata. Exact stale-version lines are not copied here to avoid adding recursive status-note findings to future scans.
```

Full MSBuild was not run by this pass because this agent changed documentation and verification tooling only, and the assignment allows documenting verification results when source files are not touched.

## Follow-Up Needed

- API contract/facade owners should decide whether to move raw `ParalivesAPI.Core` signatures into `ParalivesAPI.Native`/`ParalivesAPI.Unsafe`, add stable DTO wrappers first, or keep compatibility overloads with explicit documentation.
- Save lifecycle owner should provide the eventual save facade and dirty-state helpers.
- Patch governance owner should decide whether Paralives patches need metadata similar to the Sheltered patch governance docs.
- A coordination pass should resolve the existing `ModAPI/Core/IGameHelper.cs` boundary finding and stale Manager version metadata.
