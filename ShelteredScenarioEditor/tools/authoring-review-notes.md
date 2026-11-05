# Authoring safety verification

All eight claims in `ShelteredScenarioEditor.md` were checked against the assigned branch.

| Finding | Outcome |
| --- | --- |
| 1. Destructive shelter capture | Capture stages discovery and identity assignment, preserves empty captures, saves a recovery snapshot even for clean drafts, and records history before replacing placements. Snapshot failure cancels capture. |
| 2. Duplicate structural lights | Structural object kinds use the same classification as deletion and are excluded from ordinary object capture. Existing structural placements remain. |
| 3. Draft-only bunker undo | Confirmed. Live bunker undo and redo now refuse without changing either state. Full reversible world history remains unresolved. |
| 4. No-op revert destroys redo | All three revert paths record their pre-change definition only after a successful removal. |
| 5. Snapshot restore has no undo | Restore records definition and copied editor metadata. Undo and redo recover both, subject to the live bunker history restriction above. |
| 6. Failed save reported as success | The session records persistence success separately from validation. Missing paths, sidecar failures and exceptions report failure. Sidecar failure keeps pending changes. |
| 7. Failed action creates history | Object placement, wall, wire and object deletion publish their pre-change snapshots only after the draft mutation succeeds. |
| 8. Removal selects another duplicate | Removal uses only the exact placement reference selected by the forward lookup. |

Build route: local shell MSBuild, followed by local PowerShell and cmd checks. Game assemblies were read as references. No game or Manager was launched, and no install was written.

Final Release build: exit 0, 0 errors, 1 existing ambiguous XML documentation warning.
Required checks: 8 scripts passed, 0 failed. Public declarations: 333 of 333 baseline entries. ModAPI boundary: 0 findings, 0 baseline entries.
Additional checks: snapshot-pair contracts passed. `Test-AuthoringSafety.ps1` passed 18 checks, comprising 12 source contracts and 6 history behavior assertions. Run it from the repository root with `powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ShelteredScenarioEditor\tools\Test-AuthoringSafety.ps1`.

Full logs remain locally in this folder's parent as ignored `*.log` files. The history behavior harness compiles the production history and session sources with an inert logger and the public XML serializer. It does not prove Unity integration. Snapshot checklist diagnostics also contain restore undo/redo assertions for the runtime verification harness.

## Remaining work

Finding 3 needs a reversible shelter operation owned by ShelteredAPI, or a coordinated editor implementation with complete live object, room, wall, wire and station snapshots. The existing preview World refresh calls ApplyAll; it cannot be treated as an inverse operation that restores deletions and removes later placements. ModAPI and ShelteredAPI were not edited.

Live checks needed on a disposable preview: repeated capture including room lights, capture discovery failure and recovery snapshot failure, no-op sprite and animation reverts after undo, storage failure during save, snapshot restore with metadata, and the blocked live bunker undo/redo message. Full bunker undo/redo must remain gated until world restoration can be verified.
