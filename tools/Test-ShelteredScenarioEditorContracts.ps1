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

$canonicalOptionId = 'ShelteredScenarioEditor.Enabled'
$retiredOptionId = @('ShelteredAPI', 'PatchCustomScenarioEditor') -join '.'
$failures = New-Object 'System.Collections.Generic.List[string]'
function Add-Failure([string]$Message) { $failures.Add($Message) }

$managerOptionsPath = Join-Path $RepoRoot 'Manager\Core\Services\ManagerBooleanOptionsService.cs'
$managerProjectPath = Join-Path $RepoRoot 'Manager\ManagerGUI.csproj'
$editorFeaturePath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Shared\ScenarioEditorFeature.cs'
$editorProjectPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\ShelteredScenarioEditor.csproj'
$sharedOptionDescriptorPath = Join-Path $RepoRoot 'Shared\ScenarioEditor\ScenarioEditorBooleanOptionDescriptor.cs'
$editorBootstrapPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\ScenarioEditorRuntimeBootstrap.cs'
$editorCompositionPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Composition\ScenarioCompositionRoot.cs'
$editorHarmonyRoot = Join-Path $RepoRoot 'ShelteredScenarioEditor\Infrastructure\Harmony'
$editorOverviewPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Presentation\Authoring\Shell\ScenarioOverviewAuthoringContentBuilder.cs'
$shellCommandPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Application\Commands\ShellUxCommand.cs'
$mapCommandPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Application\Commands\MapAuthoringCommand.cs'
$rendererCommandPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Application\Commands\RendererInteractionCommand.cs'
$shellWindowRendererPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Presentation\Authoring\Shell\Rendering\ScenarioAuthoringShellWindowImguiRenderer.cs'
$stageNavigationPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\Presentation\Authoring\Shell\StageNavigationViewModelBuilder.cs'
$apiRoot = Join-Path $RepoRoot 'ShelteredAPI'

foreach ($requiredPath in @($managerOptionsPath, $managerProjectPath, $editorFeaturePath, $editorProjectPath, $sharedOptionDescriptorPath, $editorBootstrapPath, $editorCompositionPath, $editorHarmonyRoot, $editorOverviewPath, $shellCommandPath, $mapCommandPath, $rendererCommandPath, $shellWindowRendererPath, $stageNavigationPath, $apiRoot)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { Add-Failure "Missing editor contract input '$requiredPath'." }
}

if ($failures.Count -eq 0) {
    $managerOptions = Get-Content -LiteralPath $managerOptionsPath -Raw
    $editorFeature = Get-Content -LiteralPath $editorFeaturePath -Raw
    $sharedOptionDescriptor = Get-Content -LiteralPath $sharedOptionDescriptorPath -Raw
    $managerProject = Get-Content -LiteralPath $managerProjectPath -Raw
    $editorProject = Get-Content -LiteralPath $editorProjectPath -Raw
    if ($sharedOptionDescriptor -notmatch '(?s)Id\s*=\s*"ShelteredScenarioEditor\.Enabled".*?Owner\s*=\s*"ShelteredScenarioEditor".*?Label\s*=\s*"Custom Scenario Editor".*?DefaultValue\s*=\s*false.*?RequiresRestart\s*=\s*true.*?SortOrder\s*=\s*100') {
        Add-Failure 'The shared scenario-editor option descriptor must own the canonical disabled, restart-required metadata.'
    }
    foreach ($projectContract in @(
        @{ Name = 'Manager'; Source = $managerProject },
        @{ Name = 'ShelteredScenarioEditor'; Source = $editorProject }
    )) {
        if (-not $projectContract.Source.Contains('Shared\ScenarioEditor\ScenarioEditorBooleanOptionDescriptor.cs')) {
            Add-Failure "$($projectContract.Name) must compile the shared scenario-editor option descriptor."
        }
    }
    if ($managerOptions -notmatch '(?s)Id\s*=\s*ScenarioEditorBooleanOptionDescriptor\.Id.*?Owner\s*=\s*ScenarioEditorBooleanOptionDescriptor\.Owner.*?Label\s*=\s*ScenarioEditorBooleanOptionDescriptor\.Label.*?Description\s*=\s*ScenarioEditorBooleanOptionDescriptor\.Description.*?DefaultValue\s*=\s*ScenarioEditorBooleanOptionDescriptor\.DefaultValue.*?RequiresRestart\s*=\s*ScenarioEditorBooleanOptionDescriptor\.RequiresRestart.*?SortOrder\s*=\s*ScenarioEditorBooleanOptionDescriptor\.SortOrder') {
        Add-Failure 'The desktop manager must seed the scenario-editor option exclusively from the shared descriptor.'
    }
    if ($editorFeature -notmatch '(?s)EnabledOptionId\s*=\s*ScenarioEditorBooleanOptionDescriptor\.Id.*?EnabledOptionLabel\s*=\s*ScenarioEditorBooleanOptionDescriptor\.Label.*?EnabledOptionDescription\s*=\s*ScenarioEditorBooleanOptionDescriptor\.Description.*?GetBool\(EnabledOptionId,\s*ScenarioEditorBooleanOptionDescriptor\.DefaultValue\).*?Id\s*=\s*EnabledOptionId.*?DefaultValue\s*=\s*ScenarioEditorBooleanOptionDescriptor\.DefaultValue.*?RequiresRestart\s*=\s*ScenarioEditorBooleanOptionDescriptor\.RequiresRestart') {
        Add-Failure 'The editor runtime must register and read the canonical option exclusively from the shared descriptor.'
    }
    if ($managerOptions.Contains($canonicalOptionId) -or $editorFeature.Contains($canonicalOptionId)) {
        Add-Failure 'The canonical scenario-editor option ID must have one literal source in the shared descriptor.'
    }

    $editorBootstrap = Get-Content -LiteralPath $editorBootstrapPath -Raw
    if ($editorBootstrap -notmatch '(?s)if\s*\(enabled\).*?ScenarioCompositionRoot\.EnsureAuthoringInitialized\(\).*?HarmonyBootstrap\.ApplyDeferredPatchGroup\(\s*PatchStartupTiming\.EditorDeferred') {
        Add-Failure 'The enabled editor bootstrap must build its graph before applying the EditorDeferred Harmony group.'
    }

    $editorComposition = Get-Content -LiteralPath $editorCompositionPath -Raw
    if ($editorComposition -notmatch 'if\s*\(!ScenarioEditorFeature\.Enabled\)\s*throw\s+new\s+InvalidOperationException') {
        Add-Failure 'The editor composition root must structurally reject graph construction while the feature is disabled.'
    }

    $editorOverview = Get-Content -LiteralPath $editorOverviewPath -Raw
    if ($editorOverview -match 'Item\.Action\(\s*"stage\.select\.') {
        Add-Failure 'Overview stage navigation must carry typed ShellUxCommand instances; raw stage.select actions conflict with the canonical semantic manifest.'
    }

    $shellCommand = Get-Content -LiteralPath $shellCommandPath -Raw
    $mapCommand = Get-Content -LiteralPath $mapCommandPath -Raw
    $rendererCommand = Get-Content -LiteralPath $rendererCommandPath -Raw
    $shellWindowRenderer = Get-Content -LiteralPath $shellWindowRendererPath -Raw
    foreach ($textCommand in @(
        @{ Name = 'ShellUxCommand'; Source = $shellCommand },
        @{ Name = 'MapAuthoringCommand'; Source = $mapCommand },
        @{ Name = 'RendererInteractionCommand'; Source = $rendererCommand }
    )) {
        if ($textCommand.Source -notmatch ':\s*ScenarioAuthoringCommand\s*,\s*IScenarioTextValueCommand' -or
            $textCommand.Source -notmatch 'ScenarioAuthoringCommand\s+WithTextValue\(string value\)') {
            Add-Failure "$($textCommand.Name) must use the canonical typed editable-command lane."
        }
    }
    if ($shellWindowRenderer -match '\bas\s+(MapAuthoringCommand|ShellUxCommand)\b|\.WithValue\(') {
        Add-Failure 'The shell renderer must not type-switch editable commands outside IScenarioTextValueCommand.'
    }
    $stageNavigation = Get-Content -LiteralPath $stageNavigationPath -Raw
    if ($stageNavigation -notmatch 'BuildWindowMenuActions[\s\S]*ScenarioDraftHistoryCommand\.Show\(\)') {
        Add-Failure 'Draft history must be reachable through a rendered window-menu command before any named version exists.'
    }
    if ($stageNavigation -notmatch 'BuildWindowMenuActions[\s\S]*EditorLifecycleCommand\.ExitToMainMenu') {
        Add-Failure 'The editor close lifecycle must be reachable through the rendered window menu.'
    }
    $publishCommandSource = Get-Content -LiteralPath (Join-Path $RepoRoot 'ShelteredScenarioEditor\Application\Commands\ScenarioPublishCommandHandler.cs') -Raw
    $publishContentSource = Get-Content -LiteralPath (Join-Path $RepoRoot 'ShelteredScenarioEditor\Presentation\Authoring\Shell\ScenarioPublishAuthoringContentBuilder.cs') -Raw
    if ($publishCommandSource -notmatch 'UninstallLastExport[\s\S]*_exportService\.UninstallLastExport\(\)' -or
        $publishContentSource -notmatch 'ScenarioPublishCommand\.UninstallLastExport\(\)') {
        Add-Failure 'A locally installed export must expose the existing safe uninstall service through the typed Publish command lane.'
    }

    $editorHarmonySources = @(Get-ChildItem -LiteralPath $editorHarmonyRoot -Recurse -File -Filter '*.cs')
    $policyCount = 0
    foreach ($sourceFile in $editorHarmonySources) {
        $source = Get-Content -LiteralPath $sourceFile.FullName -Raw
        $matches = [Text.RegularExpressions.Regex]::Matches($source, '(?s)\[PatchPolicy\((?<Policy>.*?)\)\]')
        foreach ($match in $matches) {
            $policyCount++
            $policy = $match.Groups['Policy'].Value
            if ($policy -notmatch 'ManagerToggleId\s*=\s*ScenarioEditorFeature\.EnabledOptionId') {
                Add-Failure "Editor patch policy in $($sourceFile.Name) is not controlled by the canonical editor option. Move always-on runtime behavior to ShelteredAPI or gate it here."
            }
            if ($policy -notmatch 'ManagerToggleDefault\s*=\s*false') {
                Add-Failure "Editor patch policy in $($sourceFile.Name) does not declare ManagerToggleDefault=false."
            }
            if ($policy -notmatch 'StartupTiming\s*=\s*PatchStartupTiming\.EditorDeferred') {
                Add-Failure "Editor patch policy in $($sourceFile.Name) is not EditorDeferred."
            }
        }
    }
    if ($policyCount -eq 0) { Add-Failure 'No editor patch policies were found to verify.' }

    $apiSources = @(Get-ChildItem -LiteralPath $apiRoot -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })
    foreach ($sourceFile in $apiSources) {
        $source = Get-Content -LiteralPath $sourceFile.FullName -Raw
        if ($source.Contains($canonicalOptionId) -or $source.Contains($retiredOptionId) -or
            $source -match '\bScenarioFeatureToggles\b|\bIsCustomScenarioEditorEnabled\b') {
            Add-Failure "ShelteredAPI runtime is coupled to an editor enablement option: $($sourceFile.FullName.Substring($RepoRoot.Length + 1))."
        }
    }

    $runtimePatchSource = Get-Content -LiteralPath (Join-Path $apiRoot 'Scenarios\Infrastructure\Harmony\ShelteredCustomScenarioPatches.cs') -Raw
    foreach ($runtimeFeature in @('ShelteredCustomScenarioSelection', 'ShelteredCustomScenarioSpawn')) {
        $runtimePolicy = [Text.RegularExpressions.Regex]::Match(
            $runtimePatchSource,
            '(?s)\[PatchPolicy\(PatchDomain\.Scenarios,\s*"' + [Text.RegularExpressions.Regex]::Escape($runtimeFeature) + '"(?<Policy>.*?)\)\]')
        if (-not $runtimePolicy.Success) {
            Add-Failure "Required ShelteredAPI runtime policy '$runtimeFeature' was not found."
        }
        elseif ($runtimePolicy.Groups['Policy'].Value -match 'ManagerToggleId') {
            Add-Failure "Installed custom-scenario runtime policy '$runtimeFeature' must remain available independently of the optional editor."
        }
    }

    $activeRoots = @('Manager', 'ModAPI', 'ShelteredAPI', 'ShelteredScenarioEditor', 'tools')
    foreach ($activeRoot in $activeRoots) {
        Get-ChildItem -LiteralPath (Join-Path $RepoRoot $activeRoot) -Recurse -File |
            Where-Object {
                $_.FullName -notmatch '\\(bin|obj)\\' -and
                $_.Extension -in @('.cs', '.json', '.md', '.ps1', '.psm1')
            } |
            ForEach-Object {
                if ((Get-Content -LiteralPath $_.FullName -Raw).Contains($retiredOptionId)) {
                    Add-Failure "Retired editor option ID remains in active source or tooling: $($_.FullName.Substring($RepoRoot.Length + 1))."
                }
            }
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Host "FAIL $failure" }
    exit 1
}

Write-Host 'Sheltered scenario editor lifecycle contracts passed.'
