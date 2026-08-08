[CmdletBinding()]
param([string]$RepoRoot)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

function Read-Source([string]$relativePath) {
    $path = Join-Path $RepoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required lifecycle source was not found: $relativePath"
    }
    return [IO.File]::ReadAllText($path)
}

function Assert-Contains([string]$source, [string]$needle, [string]$message) {
    if ($source.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) {
        throw $message
    }
}

function Assert-NotContains([string]$source, [string]$needle, [string]$message) {
    if ($source.IndexOf($needle, [StringComparison]::Ordinal) -ge 0) {
        throw $message
    }
}

$editorRoot = Join-Path $RepoRoot 'ShelteredScenarioEditor'
$lifecycle = Read-Source 'ShelteredScenarioEditor\Application\Authoring\ScenarioAuthoringSessionLifecycleService.cs'
$bootstrap = Read-Source 'ShelteredScenarioEditor\Application\Authoring\ScenarioAuthoringBootstrapService.cs'
$backend = Read-Source 'ShelteredScenarioEditor\Application\Authoring\ScenarioAuthoringBackendService.cs'
$contracts = Read-Source 'ShelteredScenarioEditor\Application\Authoring\ScenarioAuthoringContracts.cs'
$reload = Read-Source 'ShelteredScenarioEditor\Application\Authoring\ScenarioAuthoringBaseModeReloadService.cs'
$entry = Read-Source 'ShelteredScenarioEditor\Application\Authoring\ScenarioAuthoringEntryFlowService.cs'
$session = Read-Source 'ShelteredScenarioEditor\Application\Authoring\ScenarioAuthoringSession.cs'
$command = Read-Source 'ShelteredScenarioEditor\Application\Commands\EditorLifecycleCommand.cs'
$handler = Read-Source 'ShelteredScenarioEditor\Application\Commands\ScenarioAuthoringCommandHandlers.cs'
$presentation = Read-Source 'ShelteredScenarioEditor\Presentation\Authoring\Shell\ScenarioAuthoringPresentationBuilder.cs'
$workflow = Read-Source 'ShelteredScenarioEditor\Presentation\Authoring\Shell\ScenarioWorkflowAuthoringContentBuilder.cs'
$composition = Read-Source 'ShelteredScenarioEditor\Composition\ScenarioAuthoringModule.cs'
$layout = Read-Source 'ShelteredScenarioEditor\Presentation\Authoring\Shell\ScenarioAuthoringLayoutService.cs'
$manifest = Read-Source 'ShelteredScenarioEditor\Presentation\Authoring\Shell\ScenarioAuthoringRendererActionManifest.cs'

foreach ($phase in @('Inactive', 'Queued', 'WorldLoading', 'Active', 'ReloadPending', 'Closing')) {
    Assert-Contains $lifecycle $phase "Lifecycle phase '$phase' is missing."
}
Assert-Contains $lifecycle 'ScenarioAuthoringSessionTransition' 'Lifecycle transitions must be explicit values.'
Assert-Contains $lifecycle '_revision != revision' 'Close confirmation must reject a stale lifecycle revision.'
Assert-Contains $lifecycle 'MatchesCloseRequest(draftId, revision' 'Close confirmation must validate the draft and revision before acting.'
Assert-Contains $lifecycle 'validation != null && !validation.IsValid' 'Validation errors must remain saveable during close.'
Assert-Contains $lifecycle 'Close blocked: the draft could not be serialized.' 'Serialization failure must keep the editor active.'
Assert-Contains $lifecycle 'ScenarioAuthoringSessionPhase.ReloadPending' 'Reload must be represented by the lifecycle owner.'
Assert-Contains $lifecycle 'ScenarioAuthoringSession QueueCurrentDraftReload(' 'A saved draft reload must be derived by the lifecycle owner.'
Assert-Contains $lifecycle 'ScenarioAuthoringSession current = CurrentOrPending;' 'Reload must use the authoritative current lifecycle identity.'
Assert-Contains $session 'internal ScenarioAuthoringSession CreateReloadSession(' 'A reload session must preserve the current session identity without re-indexing the draft.'

$storePath = Join-Path $editorRoot 'Application\Authoring\ScenarioAuthoringSessionStore.cs'
if (Test-Path -LiteralPath $storePath) {
    throw 'The superseded mutable ScenarioAuthoringSessionStore still exists.'
}
$allEditorSource = (Get-ChildItem -LiteralPath $editorRoot -Recurse -Filter '*.cs' | ForEach-Object {
    [IO.File]::ReadAllText($_.FullName)
}) -join "`n"
Assert-NotContains $allEditorSource 'ScenarioAuthoringSessionStore' 'Editor source still references the superseded session store.'

Assert-Contains $composition 'new ScenarioAuthoringSessionLifecycleService(' 'Composition must register the lifecycle owner.'
Assert-Contains $backend '_sessionLifecycle.Transitioned += HandleSessionTransition;' 'Backend shell state must project lifecycle transitions.'
Assert-Contains $contracts 'public string ActiveDraftId { get; set; }' 'The shell snapshot must project the lifecycle draft identity for integrations.'
Assert-Contains $contracts 'public string ActiveScenarioFilePath { get; set; }' 'The shell snapshot must project the lifecycle scenario path for integrations.'
Assert-Contains $contracts 'ActiveDraftId = ActiveDraftId' 'Snapshot copies must preserve the projected draft identity.'
Assert-Contains $contracts 'ActiveScenarioFilePath = ActiveScenarioFilePath' 'Snapshot copies must preserve the projected scenario path.'
Assert-Contains $backend 'ActiveDraftId = session.DraftId' 'Initial and world-loading shell projections must expose their lifecycle draft identity.'
Assert-Contains $backend '_state.ActiveDraftId = session.DraftId;' 'Activation must refresh the projected draft identity from the lifecycle session.'
Assert-Contains $backend '_state.ActiveScenarioFilePath = session.ScenarioFilePath;' 'Activation must refresh the projected scenario path from the lifecycle session.'
Assert-Contains $backend '_state.ActiveDraftId = pendingSession != null ? pendingSession.DraftId : _state.ActiveDraftId;' 'Reload handoff must retain the pending lifecycle identity.'
Assert-Contains $backend 'if (_state.ActiveStage == ScenarioStageKind.None)' 'Initial world-loading completion must normalize the shell to an editable stage.'
Assert-Contains $backend '_state.ActiveStage = ScenarioStageKind.BunkerInside;' 'Initial activation must enter the default bunker workspace.'
Assert-Contains $layout 'public void InitializeState(ScenarioAuthoringState state)' 'Layout initialization must remain the window-state initialization boundary.'
$layoutInitialization = ($layout -split 'public void EnsureWindowStates')[0]
Assert-NotContains $layoutInitialization 'state.ActiveStage = ScenarioStageKind.None;' 'Layout initialization must preserve the workflow stage selected by the backend/lifecycle owner.'
Assert-NotContains $layoutInitialization 'state.ActiveShellTab = ScenarioAuthoringShellTab.Shell;' 'Layout initialization must preserve the shell tab selected by the backend/lifecycle owner.'
Assert-Contains $manifest 'CanonicalizeContractActions(actions.ToArray())' 'The serialized semantic contract must collapse the same typed command rendered on multiple surfaces.'
Assert-Contains $manifest 'existing.Command.GetType() == action.Command.GetType()' 'Only equivalent typed command renderings may share a semantic action id.'
Assert-NotContains $backend 'ScenarioAuthoringActionIds.ActionCloseEditor' 'Backend must not special-case the raw close action.'
Assert-NotContains $reload 'ScenarioAuthoringBootstrapService.Instance' 'Base-mode reload must not locate Bootstrap.'
Assert-Contains $reload '_sessionLifecycle.QueueCurrentDraftReload(' 'Template/base reload must use the active lifecycle identity after saving invalidates metadata.'
Assert-NotContains $reload '_sessionLifecycle.QueueExistingDraft(draftId, launchSaveType)' 'An active draft reload must not re-discover its identity through the metadata index.'
Assert-Contains $reload 'return false;' 'Failure to queue the saved draft reload must not be reported as successful.'
Assert-Contains $reload '_sessionLifecycle.Phase != ScenarioAuthoringSessionPhase.Active' 'Baseline selection during initial world loading must not require an active preview session.'
Assert-Contains $reload 'loadingSession.BaseMode == reloadMode' 'A same-base template must ride the authoritative in-progress world load.'
Assert-Contains $reload 'QueueBaselineAfterCurrentLoad(' 'A different-base template must defer its deliberate reload until the current load activates.'
Assert-Contains $reload 'if (queued.UseSavedBaseline)' 'Deferred baseline content must retain its reload intent through activation.'
Assert-NotContains $entry 'ScenarioAuthoringBootstrapService.Instance' 'Entry flow must not locate Bootstrap.'
Assert-Contains $entry 'private ScenarioAuthoringSelectionOutcome _selectionOutcome;' 'Wizard base selection must have an explicit typed outcome.'
Assert-Contains $entry '_selectionOutcome == ScenarioAuthoringSelectionOutcome.Succeeded' 'Wizard readiness must require a successful selection outcome.'
Assert-Contains $entry '_selectionOutcome = ScenarioAuthoringSelectionOutcome.Failed;' 'Failed template/base selection must remain a failed, retryable wizard state.'
Assert-Contains $entry '_selectionOutcome != ScenarioAuthoringSelectionOutcome.Failed' 'Re-selecting a failed choice must retry instead of being treated as already dispatched.'
Assert-NotContains $entry 'IsReloadQueuedMessage' 'Wizard success must come from typed operation results, not status-message wording.'

foreach ($legacyPath in @(
    'RequestCloseActiveSessionToMainMenu',
    'PrepareActiveSessionForVanillaShutdown',
    'RequestReloadActiveSession',
    'CloseActiveSessionToMainMenu',
    'CloseRuntimeStateToMainMenu')) {
    Assert-NotContains $bootstrap $legacyPath "Bootstrap still owns legacy lifecycle path '$legacyPath'."
}

Assert-Contains $command 'ExitToMainMenu' 'A typed ExitToMainMenu lifecycle command is required.'
Assert-Contains $command 'ScenarioAuthoringActionIds.ActionCloseEditor' 'Typed ExitToMainMenu must preserve the close automation id.'
Assert-Contains $handler '_sessionLifecycle.RequestCloseToMainMenu' 'The typed exit handler must route through the lifecycle owner.'
Assert-Contains $presentation 'Action(EditorLifecycleCommand.ExitToMainMenu' 'The shell exit producer must emit the typed command.'
Assert-Contains $workflow 'Item.Action(EditorLifecycleCommand.ExitToMainMenu' 'The workflow exit producer must emit the typed command.'

Write-Host 'SCENARIO AUTHORING LIFECYCLE CONTRACTS PASS'
