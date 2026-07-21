[CmdletBinding()]
param(
    [string]$RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
} else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

$managerOptions = Get-Content -LiteralPath (Join-Path $RepoRoot "Manager\Core\Services\ManagerBooleanOptionsService.cs") -Raw
$runtimeToggle = Get-Content -LiteralPath (Join-Path $RepoRoot "ShelteredAPI\Scenarios\Shared\ScenarioFeatureToggles.cs") -Raw
$harmonyRoot = Join-Path $RepoRoot "ShelteredAPI\Scenarios\Infrastructure\Harmony"
$harmonySources = Get-ChildItem -LiteralPath $harmonyRoot -Filter "*.cs" |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$allHarmony = [string]::Join("`n", $harmonySources)
$failures = New-Object "System.Collections.Generic.List[string]"

if ($managerOptions -notmatch '(?s)id\s*=\s*CustomScenarioEditorOptionId.*?label\s*=\s*"Custom Scenario Authoring \(Preview\)".*?defaultValue\s*=\s*false') {
    $failures.Add("The desktop manager must seed Custom Scenario Authoring (Preview) as disabled.")
}

if ($runtimeToggle -notmatch '(?s)Id\s*=\s*CustomScenarioEditorPatchToggleId.*?Label\s*=\s*CustomScenarioEditorPatchLabel.*?DefaultValue\s*=\s*false') {
    $failures.Add("The runtime option registration must default custom scenario authoring off.")
}

if ($runtimeToggle -notmatch 'GetBool\(CustomScenarioEditorPatchToggleId,\s*false\)') {
    $failures.Add("The runtime fallback must keep custom scenario authoring off when no option exists.")
}

$policyPattern = '(?s)\[PatchPolicy\((?<Policy>.*?)\)\]'
$policyMatches = [System.Text.RegularExpressions.Regex]::Matches($allHarmony, $policyPattern)
$authoringPolicyCount = 0
foreach ($match in $policyMatches) {
    $policy = $match.Groups["Policy"].Value
    if ($policy -notmatch 'ManagerToggleId\s*=\s*ScenarioFeatureToggles\.CustomScenarioEditorPatchToggleId') {
        continue
    }

    $authoringPolicyCount++
    if ($policy -notmatch 'ManagerToggleDefault\s*=\s*false') {
        $failures.Add("Every patch policy controlled by the authoring preview must declare ManagerToggleDefault=false.")
    }
    if ($policy -notmatch 'StartupTiming\s*=\s*PatchStartupTiming\.EditorDeferred') {
        $failures.Add("The authoring preview toggle may only gate EditorDeferred patches.")
    }
}

if ($authoringPolicyCount -ne 8) {
    $failures.Add("Expected 8 authoring-gated patch policies but found $authoringPolicyCount.")
}

$selectionPolicy = [System.Text.RegularExpressions.Regex]::Match(
    $allHarmony,
    '(?s)\[PatchPolicy\(PatchDomain\.Scenarios,\s*"ShelteredCustomScenarioSelection"(?<Policy>.*?)\)\]')
if (-not $selectionPolicy.Success -or $selectionPolicy.Groups["Policy"].Value -match 'CustomScenarioEditorPatchToggleId') {
    $failures.Add("Installed custom scenario selection must not be gated by the authoring preview.")
}

$spawnPolicy = [System.Text.RegularExpressions.Regex]::Match(
    $allHarmony,
    '(?s)\[PatchPolicy\(PatchDomain\.Scenarios,\s*"ShelteredCustomScenarioSpawn"(?<Policy>.*?)\)\]')
if (-not $spawnPolicy.Success -or $spawnPolicy.Groups["Policy"].Value -match 'CustomScenarioEditorPatchToggleId') {
    $failures.Add("Installed custom scenario runtime spawning must not be gated by the authoring preview.")
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Error $failure
    }
    exit 1
}

Write-Host "Scenario authoring toggle contracts passed."
