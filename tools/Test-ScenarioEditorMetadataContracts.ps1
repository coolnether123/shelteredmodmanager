[CmdletBinding()]
param([string]$RepoRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

$failures = New-Object 'System.Collections.Generic.List[string]'
function Require-Match([string]$Name, [string]$Text, [string]$Pattern) {
    if ($Text -notmatch $Pattern) { $failures.Add("$Name did not satisfy '$Pattern'.") }
}

$statePath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Application\Authoring\ScenarioEditorState.cs'
$oldStatePath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Application\Authoring\ScenarioAuthoringSetupState.cs'
$storePath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Infrastructure\Persistence\ScenarioAuthoringSidecarStore.cs'
$controllerPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Application\Authoring\ScenarioEditorController.cs'
$snapshotPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Application\Authoring\ScenarioDraftSnapshotService.cs'
$repositoryPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Application\Authoring\ScenarioAuthoringDraftRepository.cs'
$compositionPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Composition\ScenarioAuthoringModule.cs'

if (Test-Path -LiteralPath $oldStatePath) { $failures.Add('The retired setup-state persistence file still exists.') }
foreach ($path in @($statePath, $storePath, $controllerPath, $snapshotPath, $repositoryPath, $compositionPath)) {
    if (-not (Test-Path -LiteralPath $path)) { $failures.Add("Missing metadata contract input '$path'.") }
}

if ($failures.Count -eq 0) {
    $state = Get-Content -LiteralPath $statePath -Raw
    $store = Get-Content -LiteralPath $storePath -Raw
    $controller = Get-Content -LiteralPath $controllerPath -Raw
    $snapshots = Get-Content -LiteralPath $snapshotPath -Raw
    $repository = Get-Content -LiteralPath $repositoryPath -Raw
    $composition = Get-Content -LiteralPath $compositionPath -Raw

    Require-Match 'canonical editor state' $state '(?s)class ScenarioEditorState.*AuthorTestChecklist.*SetupFlowEnabled.*ChecklistDismissed.*CompletedTours.*HasCompletedTour'
    Require-Match 'session-owned persistence' $state '(?s)class ScenarioEditorStateSessionService.*IScenarioEditorSessionStore.*ScenarioAuthoringSidecarStore.*SaveCurrent'
    Require-Match 'session store production registration' $composition 'AddSingleton<IScenarioEditorSessionStore>\(delegate\(IServiceResolver resolver\) \{ return new ScenarioEditorSessionStore\(\); \}\)'
    Require-Match 'single sidecar schema' $store '(?s)ScenarioEditorState.*<Setup|WriteStartElement\("Setup"\).*flowEnabled.*checklistDismissed.*CompletedTours.*AuthorTestChecklist'
    Require-Match 'transactional sidecar write' $store '(?s)FileShare\.None.*LoadSidecarFile\(tempPath, false.*File\.Replace\(tempPath, sidecarPath.*File\.Move\(tempPath, sidecarPath\)'
    Require-Match 'controller aggregate lifecycle' $controller '(?s)_sidecarStore\.Load\(scenarioFilePath.*CreateSession\(definition, editorState\).*_sidecarStore\.Save\(path, session\.EditorState\)'
    Require-Match 'snapshot aggregate pair save' $snapshots '_sidecarStore\.Save\(pendingScenarioPath, session\.EditorState, true\)'
    Require-Match 'snapshot aggregate restore' $snapshots '(?s)restoredEditorState = _sidecarStore\.Load.*session\.EditorState = restoredEditorState'
    Require-Match 'new draft aggregate' $repository '_sidecarStore\.Save\(scenarioFilePath, ScenarioEditorState\.CreateForNewDraft\(\)\)'
    Require-Match 'duplicate folder pair' $repository '(?s)TryDuplicateDraft.*CopyDraftFolder'

    $editorSources = (Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'ShelteredScenarioEditor') -Recurse -Filter '*.cs' |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
    if ($editorSources -match 'ScenarioAuthoringSetupState|ScenarioAuthoringSetupStateService|authoring_state\.xml') {
        $failures.Add('A retired setup-state type, service, or file path remains in editor source.')
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Host "FAIL $failure" }
    exit 1
}

Write-Host 'Scenario editor metadata contracts passed.'
