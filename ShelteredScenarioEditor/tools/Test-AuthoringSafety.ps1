[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$editor = Join-Path $repo 'ShelteredScenarioEditor'
$checks = 0
function Assert-Contract([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
$capture = Get-Content (Join-Path $editor 'Application\Authoring\ScenarioAuthoringCaptureService.cs') -Raw
$visual = Get-Content (Join-Path $editor 'Application\Assets\ScenarioSpriteSwapAuthoringService.cs') -Raw
$placement = Get-Content (Join-Path $editor 'Application\Authoring\ScenarioBuildPlacementAuthoringService.cs') -Raw
$deletion = Get-Content (Join-Path $editor 'Application\Authoring\ScenarioBuildDeletionAuthoringService.cs') -Raw
$handler = Get-Content (Join-Path $editor 'Application\Commands\ScenarioAuthoringCommandHandlers.cs') -Raw
$controller = Get-Content (Join-Path $editor 'Application\Authoring\ScenarioEditorController.cs') -Raw
Assert-Contract ($capture.Contains('captured.Count == preserved.Count')) 'Empty captures must preserve the draft.'
Assert-Contract ($capture.IndexOf('AssignMissingObjectIdentities(staged)') -lt $capture.IndexOf('session.WorkingDefinition.BunkerEdits = staged.WorkingDefinition.BunkerEdits')) 'Identity assignment must finish before capture commits.'
Assert-Contract ($capture.Contains('out protectionError, true')) 'Capture must protect clean drafts too.'
Assert-Contract ($capture.Contains('TryParseSpecialKind(objectType.ToString(), out structuralKind)')) 'Structural lights must not be recaptured as objects.'
Assert-Contract ($capture.Contains('return ReferenceEquals(placement, matchedPlacement);')) 'Removal must use the exact matched placement.'
Assert-Contract (-not $visual.Contains('_historyService.Undo(definition, out ignored)')) 'No-op reverts must not modify history.'
Assert-Contract (([regex]::Matches($visual, 'RecordVisualChange\(\s*beforeRevert,')).Count -eq 3) 'All three revert paths must record only successful changes.'
Assert-Contract ($visual.Contains('_historyService.TryPeekUndo(out pendingDescription, out pendingCategory)') -and $visual.Contains('_historyService.TryPeekRedo(out pendingDescription, out pendingCategory)')) 'Live bunker undo and redo must be guarded.'
Assert-Contract ($handler.Contains('!session.LastSaveSucceeded')) 'Save reporting must check persistence outcome.'
Assert-Contract ($controller.IndexOf('session.LastSaveSucceeded = true') -gt $controller.IndexOf('_sidecarStore.Save(path, session.EditorState)')) 'Save success must follow the sidecar write.'
Assert-Contract (([regex]::Matches($placement, 'RecordBunkerChange\(beforeChange,')).Count -eq 3) 'Placement, wall and wire history must follow successful recording.'
Assert-Contract ($deletion.Contains('if (removedDraft && _historyService != null)')) 'Failed object deletion or missing draft removal must not create history.'
$refs = @((Join-Path $repo 'Dist\SMM\ModAPI.dll'), (Join-Path $repo 'Dist\SMM\bin\ShelteredAPI.dll'))
foreach ($ref in $refs) { [void][Reflection.Assembly]::LoadFrom($ref) }
$paths = @('Application\Authoring\ScenarioAuthoringHistoryService.cs', 'Application\Authoring\ScenarioEditorSession.cs', 'Application\Authoring\ScenarioAuthorTestChecklist.cs')
$bodies = @($paths | ForEach-Object { Get-Content (Join-Path $editor $_) -Raw })
$state = Get-Content (Join-Path $editor 'Application\Authoring\ScenarioEditorState.cs') -Raw
$state = $state.Substring(0, $state.IndexOf('    /// <summary>', $state.IndexOf('public ScenarioEditorState Copy()'))) + "}`n"
$bodies += $state.Replace('using ShelteredScenarioEditor.Infrastructure.Persistence;', '')
$bodies += @'
namespace ModAPI.Core { internal static class MMLog { public static void WriteInfo(string text) {} } }
namespace ShelteredScenarioEditor.Application.Runtime {
    internal static class ScenarioEditorDefinitionCloner {
        public static ShelteredAPI.Scenarios.Definitions.ScenarioDefinition Clone(ShelteredAPI.Scenarios.Definitions.ScenarioDefinition value) {
            return ShelteredAPI.Scenarios.Public.ShelteredScenarioAuthoring.FromXml(ShelteredAPI.Scenarios.Public.ShelteredScenarioAuthoring.ToXml(value));
        }
    }
}
public static class AuthoringSafetyBehavior {
    public static int Run() {
        var session = new ShelteredScenarioEditor.Application.Authoring.ScenarioEditorSession();
        session.WorkingDefinition = new ShelteredAPI.Scenarios.Definitions.ScenarioDefinition { Id = "safety", DisplayName = "Before" };
        session.EditorState.AddCompletedTour("before");
        var history = new ShelteredScenarioEditor.Application.Authoring.ScenarioAuthoringHistoryService();
        history.RecordSnapshotRestore(session);
        session.WorkingDefinition.DisplayName = "Restored";
        session.EditorState = new ShelteredScenarioEditor.Application.Authoring.ScenarioEditorState();
        session.EditorState.AddCompletedTour("restored");
        string description;
        if (!history.Undo(session.WorkingDefinition, out description)) throw new System.Exception("Restore undo unavailable");
        if (session.WorkingDefinition.DisplayName != "Before") throw new System.Exception("Undo lost definition");
        if (!session.EditorState.HasCompletedTour("before") || session.EditorState.HasCompletedTour("restored")) throw new System.Exception("Undo lost editor state");
        if (!history.Redo(session.WorkingDefinition, out description)) throw new System.Exception("Restore redo unavailable");
        if (session.WorkingDefinition.DisplayName != "Restored") throw new System.Exception("Redo lost definition");
        if (!session.EditorState.HasCompletedTour("restored") || session.EditorState.HasCompletedTour("before")) throw new System.Exception("Redo lost editor state");
        return 6;
    }
}
'@
$usings = @($bodies | ForEach-Object { [regex]::Matches($_, '(?m)^using [^;]+;') } | ForEach-Object { $_.Value } | Sort-Object -Unique)
$source = ($usings -join "`n") + "`n" + (($bodies | ForEach-Object { $_ -replace '(?m)^using [^;]+;', '' }) -join "`n")
Add-Type -TypeDefinition $source -Language CSharp -ReferencedAssemblies $refs -IgnoreWarnings -WarningAction SilentlyContinue
$checks += [AuthoringSafetyBehavior]::Run()
Write-Host "AUTHORING SAFETY PASS: $checks checks, 0 failures"

