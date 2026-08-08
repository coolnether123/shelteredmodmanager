[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repoRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Test-AuthoringShellWorkspaceProjection.ps1')
$managedRoot = 'D:\Epic Games Games\Sheltered\ShelteredWindows64_EOS_Data\Managed'
$assemblies = @(
    (Join-Path $managedRoot 'UnityEngine.dll'),
    (Join-Path $managedRoot 'UnityEngine.UI.dll'),
    (Join-Path $managedRoot 'Assembly-CSharp.dll'),
    (Join-Path $repoRoot 'mods\0Harmony\Assemblies\0Harmony.dll'),
    (Join-Path $repoRoot 'Dist\SMM\ModAPI.dll'),
    (Join-Path $repoRoot 'Dist\SMM\bin\ShelteredAPI.dll'),
    (Join-Path $repoRoot 'Dist\SMM\bin\ShelteredScenarioEditor.dll')
)

foreach ($assemblyPath in $assemblies) {
    if (-not (Test-Path -LiteralPath $assemblyPath)) {
        throw "Required routing verification assembly was not found: $assemblyPath"
    }
    [void][Reflection.Assembly]::LoadFrom($assemblyPath)
}

$scenarioEditor = [Reflection.Assembly]::LoadFrom((Join-Path $repoRoot 'Dist\SMM\bin\ShelteredScenarioEditor.dll'))
$verificationType = $scenarioEditor.GetType(
    'ShelteredScenarioEditor.Diagnostics.ScenarioWorkspaceRoutingVerification',
    $true,
    $false)
$runMethod = $verificationType.GetMethod(
    'Run',
    [Reflection.BindingFlags]'Public,Static')
if ($null -eq $runMethod) {
    throw 'ScenarioWorkspaceRoutingVerification.Run was not found.'
}

$errors = [string[]]$runMethod.Invoke($null, @())
if ($errors.Count -gt 0) {
    throw ("Scenario workspace routing verification failed:`n - " + ($errors -join "`n - "))
}

$mapVerificationType = $scenarioEditor.GetType(
    'ShelteredScenarioEditor.Presentation.Authoring.Shell.ScenarioMapWorkspaceVerification',
    $true,
    $false)
$mapRunMethod = $mapVerificationType.GetMethod(
    'Run',
    [Reflection.BindingFlags]'Public,Static')
if ($null -eq $mapRunMethod) {
    throw 'ScenarioMapWorkspaceVerification.Run was not found.'
}

$mapErrors = [string[]]$mapRunMethod.Invoke($null, @())
if ($mapErrors.Count -gt 0) {
    throw ("Scenario map workspace verification failed:`n - " + ($mapErrors -join "`n - "))
}

Write-Host 'SCENARIO WORKSPACE ROUTING PASS'
