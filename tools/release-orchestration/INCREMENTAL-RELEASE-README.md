# Incremental 2.0 release orchestration

`Invoke-IncrementalRelease.ps1` turns changed repository files into a small, ordered release plan across the sibling repositories in the Sheltered umbrella folder. The graph is machine-readable in `incremental-release-graph.json`; the runner emits JSON so an agent can hand the result to another agent without repeating discovery. This directory is the canonical, Git-tracked copy of the orchestration layer.

## Dry run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-IncrementalRelease.ps1 `
  -ChangedFile 'Lifespan/Lifespan/Lifespan.cs' `
  -OutputPath .\release-plan.json
```

Use `-DetectGit -BaseRef origin/main` to collect working-tree and commit-range changes from every configured repository. A changed file may also be supplied through `-ChangedFilesPath` as either a JSON array or newline-delimited text.

The JSON plan contains selected owners, change classes, targeted gameplay fixture IDs, Steam/Epic smoke gates, package/promotion gates, and `reusedScopes`. Unchanged owners are marked `not-selected`; the runner does not claim that old evidence is valid without evaluating a concrete gate fingerprint.

## Execute a selected plan

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-IncrementalRelease.ps1 `
  -ChangedFile 'TradingAmount/TradingAmount/TradingPanelPatch.cs' `
  -SteamHarnessUrl 'http://127.0.0.1:37421' `
  -EpicHarnessUrl 'http://127.0.0.1:37422' `
  -Execute -AllowHeavy
```

Gameplay execution uses guarded completion-backed harness routes. Trading Amount uses the single `/release-scenario/interaction` transaction: it navigates to verified Standard Slot 3 when needed, creates a legitimate encounter, opens the real TradingPanel, verifies the Owned-label lifecycle, and proves cleanup. It does not synthesize a panel or trader and never uses manual screen clicks.

`-Execute` may build and package selected work. `-AllowHeavy` is required for state-heavy gameplay fixtures. A skipped, blocked, not-configured, or failed selected gate causes a nonzero exit and prevents packaging/promotion. Passed gates write receipts under `release/2.0/evidence/incremental-gates` by default. Reuse requires the same gate/configuration and a matching content fingerprint; gameplay fingerprints also include the actual live game assembly, load order, mod DLL/About files, and harness DLL. Use `-NoEvidenceReuse` for an intentional full rerun. `-Stable` adds the live SMM promotion gate, which intentionally fails closed until the issued Nexus OAuth identity and real-account evidence exist. This tool never commits, pushes, publishes, edits mod repositories, or edits `ShelteredAgentInterface`.

Focused repositories can define `scenarioRules`. For example, a change to `TradingWhiteSlotResetPatches.cs` selects only the Vanilla trading-slot and Trading Amount compatibility matrices, while a shared Vanilla Fixes core change selects all six Vanilla Fixes matrices. This is the mechanism that prevents a late one-file fix from restarting unrelated heavy gameplay tests.

## Self-tests

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-IncrementalReleaseOrchestrator.ps1
```

The self-tests use representative source, test-only, documentation-only, Manager OAuth, dependency-edge, and release-graph changes. They invoke the runner in dry-run mode and verify that unrelated state-heavy matrices are absent.
