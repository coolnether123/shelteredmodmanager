[CmdletBinding()]
param([string]$RepoRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

$failures = New-Object 'System.Collections.Generic.List[string]'

function Read-Source([string]$relativePath) {
    return Get-Content -LiteralPath (Join-Path $RepoRoot $relativePath) -Raw
}

function Assert-Match([string]$name, [string]$text, [string]$pattern) {
    if (-not [regex]::IsMatch($text, $pattern, [Text.RegularExpressions.RegexOptions]::Singleline)) {
        $failures.Add($name)
    }
}

$catalog = Read-Source 'ShelteredScenarioEditor\Application\Selection\ScenarioBackdropTargetCatalogService.cs'
$commands = Read-Source 'ShelteredScenarioEditor\Application\Commands\ScenarioAuthoringCommandHandlers.cs'
$selection = Read-Source 'ShelteredScenarioEditor\Application\Authoring\ScenarioAuthoringSelectionService.cs'
$assets = Read-Source 'ShelteredScenarioEditor\Presentation\Authoring\Shell\ScenarioAssetAuthoringContentBuilder.cs'
$hierarchy = Read-Source 'ShelteredScenarioEditor\Presentation\Authoring\Shell\ScenarioHierarchyAuthoringContentBuilder.cs'
$spriteActions = Read-Source 'ShelteredScenarioEditor\Application\Assets\ScenarioSpriteSwapAuthoringService.Actions.cs'
$spriteAuthoring = Read-Source 'ShelteredScenarioEditor\Application\Assets\ScenarioSpriteSwapAuthoringService.cs'

Assert-Match 'backdrop catalog delegates canonical target classification to selection service' $catalog `
    '_selectionService\.TryCreateTarget\(renderer\.gameObject, out target\).*target\.Kind != ScenarioAuthoringTargetKind\.Background'
Assert-Match 'backdrop panel uses dedicated selection route' $assets `
    'SelectionCommand\.SelectBackdrop\(target\.Id\)'
Assert-Match 'hierarchy consumes the shared backdrop projection' $hierarchy `
    'context\.BackdropSections != null.*sections\.Add\(context\.BackdropSections\[0\]\)'
Assert-Match 'hierarchy live object rows preserve canonical adapter classification' $hierarchy `
    'ScenarioLiveShelterObjectCatalog\.Discover\(\).*BuildTargetAction\(state, obj\.gameObject.*_selectionService\.TryCreateTarget\(gameObject, out target\).*targetKind = target != null \? target\.Kind : kind.*string id = target != null \? target\.Id : string\.Empty'
Assert-Match 'backdrop and hierarchy commands share canonical target resolution' $commands `
    'SelectionCommandKind\.SelectBackdrop.*SelectResolvedTarget.*SelectionCommandKind\.SelectHierarchy.*SelectResolvedTarget.*_selectionService\.TryResolveTarget'
Assert-Match 'selection service owns scope enforcement and direct selection state' $selection `
    'TryApplyDirectSelection.*_scopeService\.CanSelectTargetForCurrentStage.*ApplySelection\(state, target, false\).*ClearSelectionStack\(state\)'
Assert-Match 'sprite actions reconcile stale picker before dispatch' $spriteActions `
    'Execute\(ScenarioAuthoringState state, SpriteSwapCommand command, out string message\).*ReconcilePickerTarget\(state\);.*switch \(command\.Kind\)'
Assert-Match 'PNG import binds current selection before character branching' $spriteAuthoring `
    'private bool ImportPngReplacement.*EnsurePickerOpenForSelection\(state, out message\).*HasCharacterEditor\(state\)'

if ($failures.Count -gt 0) {
    throw ('Backdrop authoring contracts failed: ' + ($failures -join ', '))
}

Write-Host 'Backdrop authoring contracts passed.'
