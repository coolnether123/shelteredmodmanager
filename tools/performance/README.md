# Sheltered performance benchmark system

This tool turns the July 2026 investigation workflow into a repeatable benchmark matrix. It runs the
same startup, process, menu, scenario-selection, custom-book, memory, and Unity loop-rate probes
against Steam x86 and Epic x64. Every result identifies its executable, loader, API, harness, mod,
Git, OS, CPU, and GPU state.

The default matrix is:

| Profile | Doorstop | Harness | Custom editor | Mods | Navigation |
|---|---:|---:|---:|---|---|
| vanilla | off | no | no | none | startup/menu reference |
| smm-native | on | no | on | Harmony only | startup/menu reference |
| smm-core | on | yes | on | Harmony + agent | menu → scenario panel → custom book |
| smm-custom-editor-off | on | yes | off | Harmony + agent | menu → scenario panel |
| enabled-mods | on | yes | on | mods enabled before the suite plus core | configurable |
| all-mods | on | yes | on | every valid installed manifest | menu → scenario panel → custom book |
| explicit-example | on | yes | on | include set plus dependencies/core | configurable |

Publishable vanilla comparisons run every profile sequentially and use optimized Release artifacts.
This Unity build pauses when unfocused, making simultaneous vanilla startup measurements invalid.
`-ParallelPlatforms` is an explicitly separate functional/hotspot stress lane: instrumented Steam and
Epic cases can run concurrently because the runner acquires each harness lease and enables
Application.runInBackground, but their startup values must not be compared to serial vanilla values.
The short Main Menu to Scenario Selection segment uses a cross-process mutex because its hybrid
fallback may need the one Windows foreground/cursor. Startup, idle sampling, and non-native phases
remain concurrent across the two platform installs.
Instrumented cases preserve `HarnessMenuReadyMs` as the semantic startup milestone, then pass the
same stable native reference-frame gate as vanilla before recording `StartupMs`. Comparisons in the
aggregate report therefore use the common native milestone.

## Quick start

Validation and a non-mutating resolved plan:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\performance\Invoke-ShelteredBenchmark.ps1 -ValidateOnly
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\performance\Invoke-ShelteredBenchmark.ps1 -DryRun

Live runs use the enabled build hook to rebuild the manager and harness once, then deploy the exact
same managed artifacts to both storefronts. The harness is compiled against Steam's older Unity
surface as the compatibility floor. Use `-SkipBuild` only when the installed binaries already match
every configured deployment hash gate.

Canonical matched-serial three-run matrix:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\performance\Invoke-ShelteredBenchmark.ps1 -Platform steam,epic -Profile vanilla,smm-native,smm-core,smm-custom-editor-off,all-mods -Iterations 3 -MatchedSerial

Parallel functional/hotspot lane (never use its vanilla deltas as canonical):

    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\performance\Invoke-ShelteredBenchmark.ps1 -Platform steam,epic -Profile smm-core,all-mods -Iterations 3 -ParallelPlatforms -RunLabel parallel-hotspot

Focused regression:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\performance\Invoke-ShelteredBenchmark.ps1 -Platform steam -Profile smm-core -Iterations 5 -RunLabel scenario-navigation

Short live smoke before a long matrix (the recorded config copy retains these overrides):

    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\performance\Invoke-ShelteredBenchmark.ps1 -Platform steam,epic -Profile smm-core -Iterations 1 -ParallelPlatforms -FpsDurationSeconds 2 -ScenarioTimeoutSeconds 30 -RunLabel smoke

DryRun never writes to an install or launches a process. ValidateOnly only validates JSON and paths.
SkipBuild skips both the global build and per-platform preparation hooks.

If PowerShell itself is force-terminated and cannot execute its suite `finally`, close only game processes
you own and recover the captured install state with:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\performance\Restore-ShelteredBenchmarkState.ps1 -RunRoot <benchmark-folder> -Platform steam,epic -Force

Recovery acquires the same install locks, refuses any active game process, restores the suite snapshots,
verifies their SHA-256 identities, and writes `manual_restore_result.json` into the run folder.

## Profiles

Profile modes are:

- vanilla: disables Doorstop and selects no mods.
- core: selects coreModIds.
- enabled: selects the enabled state captured from the install before the case, plus configured core.
- all: selects every installed folder with a valid About/About.json.
- explicit: selects include.

Disabled example profiles are omitted from the default matrix but may be selected explicitly by
name. For example, the bundled Steam-only subset can be exercised with
`-Platform steam -Profile explicit-example`.

Every non-vanilla profile can add include, remove exclude, and recursively include dependsOn with
includeDependencies. Setting harness true injects the configured core IDs, so instrumentation stays
available for explicit profiles. Existing load order is retained where possible, followed by a
stable dependency/load-after/load-before relaxation.

An all-mod run is deliberately literal. If incompatible installed mods fail discovery, the case is
retained as a failure with its exact mod set and copied SMM log. Put known exclusions in the profile
instead of silently changing what “all” means.

managerOptions maps existing Boolean option IDs to run values. Unknown IDs fail the case rather than
creating an option the installed build does not understand.

## Build and preparation

The optional global build object accepts an enabled flag, executable, argument array, and working
directory. Each platform may define a prepare object with the same shape for a platform-owned deploy
script. Build and preparation happen once before install snapshots and produce build.log and
prepare-platform.log. Preparation is expected to deploy binaries; case cleanup restores runtime
configuration, not deployed binaries.

Keep Steam and Epic deploy commands separate because they require different native Doorstop
architectures and harness port files. Preparation should SHA-256 verify deployment, as the existing
Epic deployment script does.

Example:

    {
      "enabled": true,
      "executable": "C:\\Path\\MSBuild.exe",
      "arguments": ["ShelteredModManager.sln", "/m", "/p:Configuration=Release"],
      "workingDirectory": "../.."
    }

## What a case records

Every case starts a 100 ms process sampler before readiness polling. Samples contain:

- cumulative CPU time;
- working set and private memory;
- thread and handle counts;
- responding state, first native window, title, and phase;
- startup, menu idle, scenario transition, scenario-selection idle, book transition, and book-idle
  phase labels.

Instrumented profiles also record:

- /status, /state/health, /health/pump, /instances, /tools, /state/loadorder, and /state/apis;
- lease acquisition/release and Application.runInBackground;
- Time.smoothDeltaTime samples at menu, scenario selection, and scenario book;
- semantic Main Menu → Play → Scenarios timing, including click-to-panel time;
- /scenario-book/open, projected /scenario-book/rows, and route envelopes;
- automatic status, health, events, flow-state, Scenario UI-tree, and framebuffer capture when a semantic route fails;
- startup-scoped and whole-case resource metrics plus CSV/JSON CPU, working-set, private-memory, thread, and handle summaries for every phase;
- client screenshots with user-context-preservation response headers;
- the final SMM/mod_manager.log.
- parsed StartupTiming rows plus a top-20 hotspot JSON file (parent and child timings are labeled as
  nested and must not be summed).

Time.smoothDeltaTime is a Unity loop-rate stall detector, not presented/display FPS. Samples whose
harness request takes at least 100 ms are excluded from the FPS summary by default.
Every FPS summary reports valid/total coverage. A case is flagged partial when coverage falls below
sampling.minimumFpsCoveragePercent (70 percent in the example), preventing a sparse sample set from
looking authoritative.

Vanilla readiness uses a platform-specific PrintWindow reference and requires three consecutive
matches under the RMSE threshold. If a reference is absent, the result is explicitly labeled
window-delay-fallback and is not an exact menu-ready comparison.

## Output

Runs are date-stamped under:

    Decompiled/Benchmarks/YYYY-MM-DD_HHMMSS_label/
      README.md
      results.csv
      summary.csv
      manifest.json
      plan.json
      run.json
      benchmark.config.json
      cases/platform/profile/iteration-NNN/
        result.json
        raw/
          environment.json
          case.json
          deployment_hash_gates.json
          process_samples.csv
          fps sample and summary files
          scenario transition files
          startup_hotspots.csv and startup_hotspots_top20.json
          harness response files
          mod_manager.log
          install-state-before/
        screenshots/
          menu.png
          scenario_selection.png
          scenario_book.png

results.csv contains every case. summary.csv contains per-platform/profile medians, P05/P95 startup,
CPU, memory, transitions, and same-iteration paired vanilla deltas. The Markdown report includes per-metric
sample counts, startup min/median/max, aggregate and individual
views.

environment.json fingerprints the game executable, native and managed Doorstop, ModAPI,
ShelteredAPI, load order, every immutable selected mod DLL/JSON, Git commit/branch/status/diff hash, and
system hardware. It is the build-number and binary-identity authority for the case.

## Restoration and coordination

Before each case, the runner copies:

- doorstop_config.ini;
- mods/loadorder.json;
- SMM/bin/manager_options.json.

The snapshot records whether each file existed. Cleanup runs from finally, closes only the launched
process, releases its lease, copies the rotating SMM log, and restores or removes each tracked file.
Existing game processes cause a hard refusal; the runner never adopts or kills an unrelated session.

Before any snapshot, a named mutex is acquired for every selected install and held through the final
suite restore. A second runner targeting Steam or Epic is refused. Restore copies are SHA-256 checked
against their snapshots; residual differences are a hard failure. The launched process must terminate
and release its handle before case restore, and the suite performs a second no-live-process gate before
restoring the pre-build/preparation state.

Instrumented cases also hard-gate ModAPI, ShelteredAPI, and harness deployment hashes before launch.
Each hash gate compares the deployed file to its configured source build (or a fixed SHA-256). A stale,
missing, or unconfigured binary fails before the game starts and leaves deployment_hash_gates.json as
evidence. Build and deploy current binaries before a live run; the example deliberately does not hide
a stale installation.

After the build/deploy hook passes its preflight, the runner freezes those verified SHA-256 values in
`frozen_deployment_hashes.json`. IDE builds may then rewrite `obj`/`Dist` without invalidating an
unchanged test stack; any external write to a deployed binary still fails the next case gate. Every case
also rechecks its core and selected-mod manifest after the process exits. Runtime-generated JSON paths must
be declared explicitly with `fileIntegrity.mutableModRelativePathPatterns`; files outside those patterns
remain immutable and unexpected DLL/JSON files fail the case.

Separate platform installs are the only parallel unit. Two profiles never run concurrently against
one install.

## Offline contracts

Run without launching Sheltered:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\performance\Test-ShelteredBenchmarkContracts.ps1

The contracts cover configuration failures, discovery/dependency order, load-order isolation,
targeted Doorstop/options mutation, restoration of existing and absent files, harness URI escaping,
FPS filtering, process sampling, and report generation.

## Extending the suite

Keep collection separate from interpretation:

- ShelteredBenchmark.Core.psm1 owns configuration, profiles, state, fingerprints, sampling summaries,
  command hooks, and reports.
- ShelteredBenchmark.Harness.psm1 owns route probes, screenshots, transitions, and loop-rate samples.
- ShelteredBenchmark.Runner.psm1 composes phases and cleanup for one install/profile case.
- Invoke-ShelteredBenchmark.ps1 owns selection, parallel scheduling, build hooks, and aggregation.

New helpers should represent reusable responsibilities. The paired sampler and native-frame helpers
are intentionally isolated despite one orchestration caller: they own resource lifetimes and are
directly contract-testable.
