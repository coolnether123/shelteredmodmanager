# Sheltered agent stress campaign

`Invoke-ShelteredAgentStress.ps1` runs the Steam and Epic Games clients concurrently and drives both
through the Sheltered Agent Interface. The default campaign enables every supported installed mod and
the standalone ShelteredScenarioEditor,
opens the scenario selector and custom-scenario book, rapidly changes book and editor state, creates
disposable scenario drafts, writes long title/description/tag/checklist values, traverses all 12 editor
stages, starts live playtests, spawns objects, moves a survivor, saves periodically, restarts playtests,
and samples process health for ten minutes.

The runner snapshots installation settings plus vanilla saves, ModAPI saves and backups, Cortex data,
and every installed mod's `ScenarioAuthoringDrafts`, `ScenarioAuthoringExports`, and `Scenarios` roots
before launch. This boundary makes draft duplication, export/import, and package installation
transactional. Mutated state is archived with the run artifacts, the original state is restored, and
every restored file is verified by SHA-256. Only processes whose PID and start time belong to the
campaign can be stopped.

Platform ownership is shared with the performance runner: Steam and Epic sessions are both started,
then both pass harness readiness, and only then does the stress workload begin. Cleanup releases leases
and stops both identified processes before restoring mutable game data and finally installation settings.
The parent-issued install-lock authorization set is required by each shared session and remains valid
only while the parent holds every mutex until both storefront sessions are stopped. If PID identity cannot be
confirmed and a session is not positively stopped, mutable saves/Cortex data are not moved or restored;
their current state and the blocked-restoration record remain in the run evidence for manual recovery.
This ordering preserves concurrent storefront pressure without maintaining a second launch/rollback path.

This stress command is the editor-present/enabled row of the extraction matrix. Before accepting an
extraction build, also run the performance runner's `smm-scenario-editor-absent` and
`smm-scenario-editor-off` profiles concurrently on Steam and Epic. The absent row physically removes
only `ShelteredScenarioEditor.dll` through the transactional deployment-role snapshot; the disabled row
keeps the DLL present but must create no editor graph, patch, Unity object, window, draft service, or
preview session. Both rows must retain ShelteredAPI's installed-only scenario browser and the separate
stock vanilla, unlimited Surrounded/Stasis, and modded save lanes.

Run the default dual-storefront campaign:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\stability\Invoke-ShelteredAgentStress.ps1

Run a longer and heavier campaign without rebuilding deployed binaries:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\stability\Invoke-ShelteredAgentStress.ps1 -DurationMinutes 60 -RapidUiActions 1000 -SpawnAttempts 500 -SimulationScale 12 -RestartEveryMinutes 5 -SkipBuild -RunLabel overnight

The enabled long campaign must cover all supported mods, every editor workspace/module, repeated fast
clicks, draft create/edit/duplicate/import/export, checklist sidecar and snapshot-pair behavior, preview
open/refresh/restart/dispose, save/load/restart, clean shutdown, process/log health, memory growth, and
byte-identical restoration. Record Steam and Epic evidence separately even though the workload is concurrent.

The editor pass records each of the 12 stages at 1280x720, 1600x900, and 1920x1080. Every stage emits
a screenshot, a projected shell model, and the live rendered action catalog; the original resolution is
restored in a `finally` block. Screenshots use the harness's in-process framebuffer path with foreground
activation explicitly disabled, so concurrent Steam/Epic evidence collection does not steal desktop focus
or move the user's cursor. The history pass creates a named version, mutates the working draft,
restores the version, and requires both the named version and restore autosave to have matching
`*.editor.xml` sidecars with no `*.pairpending-*` residue. Export requires a checklist honesty line in
`README.txt` and rejects any editor sidecar in the package. The package is then installed through the
editor's import/install action and uninstalled through the book before the outer byte-for-byte rollback.

`/scenario-save-lanes?action=probe` is a harness-only diagnostic. It creates and immediately removes one
metadata save in unlimited Surrounded, unlimited Stasis, and a disposable modded scenario lane. The
campaign also hashes the physical stock `saves` directory around that probe and fails if it changes.
This route does not add a public ShelteredAPI API.

The automated runner cannot semantically determine visual overlap from pixels alone. It captures the
full multi-resolution matrix for human or image-analysis review and treats screenshot/capture failures
as campaign failures. It also does not fabricate loadable game XML for empty metadata saves: repeated
draft saves and live playtest restarts are automated, while ownership and cleanup of the three save
lanes are verified by the transactional diagnostic and filesystem evidence. A populated gameplay
save/load round trip remains manual until the harness has a completion-backed game-save endpoint that
uses the active scenario runtime instead of manufacturing invalid save bytes.

The shared build/deploy hook copies and SHA-256 verifies ModAPI, ShelteredAPI,
ShelteredScenarioEditor, and the harness to both storefronts. Use `-SkipBuild` only when those deployed
identities already match the shared benchmark configuration. The campaign uses the canonical
`ShelteredScenarioEditor.Enabled` option; it does not recognize a legacy alias.

Validate the runner's safety and coverage contracts:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\stability\Test-ShelteredAgentStressContracts.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-ScenarioEditorAssemblyBoundary.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-ShelteredScenarioEditorContracts.ps1

Each run is written below `Decompiled/Benchmarks`. `summary.json` is the pass/fail entry point;
`metrics.json`, `observations.csv`, per-platform process samples, health snapshots, event streams,
screenshots, logs, action CSV/JSONL files, and `restore-verification.json` provide the detailed evidence.

`all-supported-mods` excludes four archived ModAPI v1 diagnostic plugins that cannot implement the
ModAPI 2.0 `IPlugin` contract. Use the performance runner's literal `all-mods` profile when the purpose
is to demonstrate those compatibility failures rather than test the supported runtime set.
