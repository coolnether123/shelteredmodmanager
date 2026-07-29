# Paralives API Refactor Status

> **Build/reference metadata**
> Research note created/reviewed: 2026-05-30.
> Game build represented: local Paralives managed assemblies from A:\SteamLibrary\steamapps\common\Paralives, DLL timestamps 2026-05-29 UTC.
> Assembly fingerprint: Assembly-CSharp.dll SHA256 885D46DF..., Paralives.dll SHA256 BEE83983..., Plugins.dll SHA256 311E9ED9.... Full hashes are in Decompiled/decompile-state.json.
> Metadata added: 2026-05-30.

Last updated: 2026-05-29

This tracking document records the concurrent ownership map for the Paralives API refactor. Status rows are based on the current `documentation/agent_status/` notes where present. Unknowns are called out instead of inferred as implemented behavior.

`AGENTS.md`: not present in the repository root when checked for this docs pass.

## Agent Ownership

| Agent | Assignment | Owned modules or docs | Current status |
|-------|------------|-----------------------|----------------|
| Agent 1 | API contracts and boundary model | Stable/Native/Unsafe contract shape, runtime aggregate, public contract names, future surface baseline | Status note found. Added API version/capability metadata, Stable/Native/Unsafe namespace scaffolds, and initial Stable interfaces. Build passed in that pass; boundary and stale-version verifiers had pre-existing/out-of-scope failures. |
| Agent 2 | Patch governance | `ParalivesAPI/Patches`, patch metadata/governance conventions, patch diagnostics | Status note found. Routed Paralives patching through `PatchRegistry`, added patch state and diagnostics, and preserved duplicate-application guards. Build later failed in concurrent occupation-panel UI work outside Agent 2 scope. |
| Agent 2 occupation pass | Occupation registry | `ParalivesOccupationRegistry`, occupation definition/result models, `IParalivesOccupationRegistry` | Status note found. Added generic occupation definition registration and exposed it through `ParalivesOccupationFacade.Registry`; runtime auto-apply remains a follow-up. |
| Agent 3 | Save lifecycle and storage | `ParalivesGameLifecycleFacade`, save/load events, save-scoped storage, dirty-state adjacency | Status note found. Added lifecycle/storage facades and save lifecycle patch host. Isolated compile probe passed; full build later failed in concurrent occupation-panel UI work outside Agent 3 scope. |
| Agent 3 occupation pass | Occupation enrollment/swap/restore | `ParalivesOccupationEnrollmentFacade`, enrollment DTOs, restore tokens, `IParalivesOccupationEnrollment` | Status note found. Added generic enrollment, unenrollment, swap, restore, and snapshot operations; successful mutations mark character save data dirty. |
| Agent 4 | Interaction and action seams | `ParalivesInteractionFactory`, `ParalivesInteractionRegistry`, `ParalivesInteractionQueueFacade`, selection/completion dispatchers and patches | Status note found. Added stable interaction/action DTOs, builders, registry overloads, and action lifecycle facade. Build passed in that pass; boundary and stale-version verifiers had known failures. |
| Agent 4 occupation pass | Occupation schedules and attendance | `ParalivesOccupationScheduleFacade`, attendance decision policies, schedule DTOs, `IParalivesOccupationSchedules`, `IParalivesOccupationAttendancePolicies` | Status note found. Added generic schedule registration/read helpers and explicit attendance decisions including travel suppression, skip-today, and remote-work intent. |
| Agent 5 | Character, content, and requirements | Character snapshots, requirement facade, read-only content lookup, settings/content wrappers | Status note found. Added safe character snapshots, requirement evaluation helpers, content snapshots including occupations, and runtime/game facade passthroughs. Full build was blocked by concurrent occupation-panel UI work during that pass. |
| Agent 5 occupation pass | Occupation tasks | `ParalivesOccupationTaskFacade`, task DTO/result models, `IParalivesOccupationTasks` | Status note found. Added a generic occupation-task facade backed by active wants and exposed it through `ParalivesOccupationFacade.Tasks`; no separate task store was added. |
| Agent 6 | Occupation and school facades | `ParalivesOccupationFacade`, `ParalivesOccupationRegistry`, `ParalivesOccupationScheduleFacade`, `ParalivesOccupationTaskFacade`, `ParalivesAttendancePolicyRegistry`, enrollment/school/attendance capability shape, occupation UI context | No `agent-6` status note found in this worktree during this pass. Current occupation code exists and is documented by inspection, but final Agent 6 scope/status is Unknown. |
| Agent 7 | UI seams | `ParalivesUiFacade`, native character tab helpers, occupation panel provider rows, notification/localization UI behavior | Status note found. Added generic occupation panel provider DTOs/registry, native character tab helpers, and occupation UI patch. Build passed in that pass; boundary and stale-version verifiers had known failures. |
| Agent 8 | Initial documentation and verification | `Paralives_API_Seams.md`, `Paralives_API_Public_Surface.md`, this status tracker, agent status convention, public surface scanner | Status note found at `agent-8-docs-verification.md`. Added initial seam/public-surface/refactor docs and verifier. |
| Agent 8 second pass | Generic occupation API docs and guardrails | `Paralives_Occupation_API.md`, occupation sections in seam/public-surface/status docs, verifier Homeschool/Stable raw checks | Status note found at `agent-8-occupation-docs-verification.md`. This pass is documentation and verification only; source code is not modified. |

## Second-Pass Occupation Refactor

The second-pass occupation work is explicitly generic:

- occupation-first naming and diagrams;
- school as a specialization of occupations;
- no hardcoded Homeschool APIs or capability names;
- support for jobs, schools, custom careers, clubs, apprenticeships, gigs, remote work, and similar systems;
- guardrails against mod-specific public names and accidental raw native types in Stable interfaces.

Current implementation facts:

- `ParalivesRuntimeInfo.Current.Occupations` exists and exposes occupation reads, registry, enrollment/swap/restore, schedules, tasks, unlockables, attendance/panel-provider contracts, performance, upgrade, legacy enrollment helpers, and school helper methods.
- `ParalivesRuntimeInfo.Current.Occupations.Registry` exists and maps API-owned occupation definitions to native occupation settings.
- `ParalivesRuntimeInfo.Current.Occupations.Schedules` exists and registers/reads occupation schedule types.
- `ParalivesRuntimeInfo.Current.Occupations.Tasks` exists and wraps occupation tasks over active wants.
- `ParalivesRuntimeInfo.Current.Occupations.Enrollment` exists and provides structured enroll, unenroll, swap, restore, and snapshot operations.
- `ParalivesRuntimeInfo.Current.Occupations.Unlockables` exists and provides structured read/mutation helpers for expertises, extras, and pending upgrades.
- `ParalivesRuntimeInfo.Current.AttendancePolicies` exists and is connected to `OccupationsManager.ShouldBeWorkingNow` through an optional governed patch.
- `ParalivesRuntimeInfo.Current.Content.ReadOccupation(...)` and `ReadOccupations()` expose read-only occupation content snapshots.
- `IParalivesOccupationPanelProvider` and `ParalivesOccupationPanel` provide a generic occupation panel contribution surface.
- Current Core occupation APIs still expose raw native types and should not be treated as final Stable shape.
- Stable occupation interface scaffolds exist for registry, enrollment, schedules, tasks, unlockables, attendance policies, and panel providers. Some concrete adapters or referenced DTOs are still incomplete or not found in this pass.
- Agent status notes and the current build report that registry/schedule runtime auto-application, richer remote-work native behavior, and final aggregate wiring still need follow-up. The current full build fails on a missing `ParalivesOccupationContractMapper` reference.

## Integration Rules

- Each owning agent should update its own file under `documentation/agent_status/`.
- Architecture docs should be corrected only when code has landed or when a status note provides a concrete fact.
- The occupation API should stay generic. Use school-specific names only for behavior tied to the game's school type, and do not add Homeschool-specific public API names.
- Raw `Paralives.dll` types should move toward `ParalivesAPI.Native` or `ParalivesAPI.Unsafe`, or be wrapped by stable DTOs.
- Public examples should use Stable or Stable-direction surfaces when possible.
- Save-backed mutations should document the dirty asset they mark.
- Harmony patches should stay thin and route behavior through facade-owned services.

## Known Unknowns

| Unknown | Owner |
|---------|-------|
| Exact final namespace layout for `ParalivesAPI.Native` and `ParalivesAPI.Unsafe` | API contracts agent |
| Whether current raw signatures will be moved, obsoleted, or duplicated with stable wrappers first | API contracts agent with each facade owner |
| Whether current `ParalivesOccupationRegistry` is final or needs Stable adapter/Native split before release | Occupation facade agent |
| Whether current enrollment/swap/restore DTOs and restore token shape are final Stable contracts | Occupation enrollment agent and API contracts agent |
| Whether current `ParalivesOccupationTaskFacade` should be the final Stable task facade or an adapter over a richer wants/goals bridge | Occupation facade agent with interaction/action and wants/goals owners |
| Whether current `ParalivesOccupationUnlockableFacade` should be split into Stable/Native layers before release | Occupation unlockables agent and API contracts agent |
| Current full-build failure around missing `ParalivesOccupationContractMapper` referenced by `ParalivesOccupationFacade` | Occupation/API-contract integration owner |
| Final stable attendance context that removes raw `AssetCharacter` and `Setting.Occupation` exposure | Occupation facade agent |
| Final save facade shape and event names exposed through runtime aggregate | Save lifecycle agent |
| Whether a strict public-surface baseline should be introduced before or after facade splits | API contracts agent and docs/verification agent |
| Exact patch metadata format for any future Paralives deferred patch groups | Patch governance agent |

## Proposed Verification Baselines

No new strict baseline is created by this docs pass. If later phases need one, use:

```text
documentation/ParalivesAPI_PublicSurface_Baseline.tsv
```

The current scanner can list public types and report raw/native drift:

```cmd
tools\verify-paralivesapi-surface.cmd -ListCurrent
tools\verify-paralivesapi-surface.cmd
tools\verify-paralivesapi-surface.cmd -FailOnRawGameTypes
```

The verifier should report Homeschool-specific public API names and Stable interface raw-type exposure by default while keeping the command non-failing unless an explicit failing mode is used.
