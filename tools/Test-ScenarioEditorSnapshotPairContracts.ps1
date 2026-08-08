[CmdletBinding()]
param(
    [string]$RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}
else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

$failures = New-Object 'System.Collections.Generic.List[string]'
function Add-Failure([string]$Message) { $failures.Add($Message) }

$snapshotPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Application\Authoring\ScenarioDraftSnapshotService.cs'
$sidecarPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Infrastructure\Persistence\ScenarioAuthoringSidecarStore.cs'
$verificationPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Diagnostics\ScenarioAuthorTestChecklistVerification.cs'
$historyCommandPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Application\Commands\ScenarioDraftHistoryCommandHandler.cs'

foreach ($requiredPath in @($snapshotPath, $sidecarPath, $verificationPath, $historyCommandPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        Add-Failure "Missing snapshot-pair contract input '$requiredPath'."
    }
}

if ($failures.Count -eq 0) {
    $snapshots = Get-Content -LiteralPath $snapshotPath -Raw
    $sidecars = Get-Content -LiteralPath $sidecarPath -Raw
    $verification = Get-Content -LiteralPath $verificationPath -Raw
    $historyCommand = Get-Content -LiteralPath $historyCommandPath -Raw

    if ($snapshots -notmatch '(?s)SaveSnapshotPair\(string snapshotPath, ScenarioEditorSession session\).*?_serializer\.Save\(.*?pendingScenarioPath\).*?_sidecarStore\.Save\(pendingScenarioPath, session\.EditorState, true\).*?File\.Move\(pendingSidecarPath, sidecarPath\).*?File\.Move\(pendingScenarioPath, snapshotPath\)') {
        Add-Failure 'Snapshots must stage both files, then commit editor state before scenario XML so XML remains the discovery commit marker.'
    }
    if ($snapshots -notmatch '(?s)catch\s*\{.*?File\.Exists\(snapshotPath\).*?TryDeleteTransactionArtifact\(snapshotPath\).*?!File\.Exists\(snapshotPath\).*?File\.Exists\(sidecarPath\).*?TryDeleteTransactionArtifact\(sidecarPath\).*?throw;') {
        Add-Failure 'Snapshot-pair commit failures must roll back both final targets before propagating the error.'
    }
    if ($snapshots -notmatch '(?s)AddSnapshots\(.*?GetSidecarPath\(files\[i\]\).*?!File\.Exists\(sidecarPath\).*?continue;') {
        Add-Failure 'Snapshot discovery must not expose scenario XML whose editor-state partner is missing.'
    }
    if ($snapshots -notmatch '(?s)Restore\(.*?GetSidecarPath\(snapshot\.FilePath\).*?!File\.Exists\(sidecarPath\).*?FormatException') {
        Add-Failure 'Snapshot restore must reject an incomplete pair instead of treating missing editor state as empty.'
    }
    if (($snapshots + $sidecars) -match 'InvalidDataException') {
        Add-Failure 'The editor must not use System.IO.InvalidDataException because Sheltered legacy Mono cannot load it.'
    }
    if ($snapshots -notmatch 'CreateUniqueSnapshotPath\(directory, "version"\)' -or
        $snapshots -match 'EncodeVersionName|DecodeVersionName' -or
        $snapshots -notmatch 'Guid\.NewGuid\(\)\.ToString\("N"\)\.Substring\(0, 12\)') {
        Add-Failure 'Saved-version and pair-staging filenames must remain bounded for Sheltered legacy Mono path limits.'
    }
    if ($historyCommand -notmatch '(?s)!_snapshots\.SaveVersion\(out saved, out error\).*?Result\(false,') {
        Add-Failure 'A failed saved-version write must propagate a rejected action result.'
    }
    if ($sidecars -notmatch '(?s)Save\(\s*string scenarioFilePath,\s*ScenarioEditorState state,\s*bool preserveEmptyState\s*\).*?!preserveEmptyState.*?!state\.HasPersistedContent') {
        Add-Failure 'The sidecar store must support a persisted empty state for complete snapshot pairs.'
    }
    if ($verification -notmatch 'did not commit scenario XML and editor state as a complete pair' -or
        $verification -notmatch 'empty checklist snapshot did not retain its required editor-state half' -or
        $verification -notmatch 'discovery exposed scenario XML without its editor-state pair' -or
        $verification -notmatch 'did not clean an interrupted pair transaction') {
        Add-Failure 'Behavioral verification must cover complete, empty, incomplete, and interrupted snapshot pairs.'
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Host "FAIL $failure" }
    exit 1
}

Write-Host 'Scenario editor snapshot-pair contracts passed.'
