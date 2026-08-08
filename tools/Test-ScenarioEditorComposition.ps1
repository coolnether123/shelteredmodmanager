[CmdletBinding()]
param([string]$RepoRoot)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

$managedRoot = 'D:\Epic Games Games\Sheltered\ShelteredWindows64_EOS_Data\Managed'
$assemblies = @(
    (Join-Path $managedRoot 'UnityEngine.dll'),
    (Join-Path $managedRoot 'UnityEngine.UI.dll'),
    (Join-Path $managedRoot 'Assembly-CSharp.dll'),
    (Join-Path $RepoRoot 'mods\0Harmony\Assemblies\0Harmony.dll'),
    (Join-Path $RepoRoot 'Dist\SMM\ModAPI.dll'),
    (Join-Path $RepoRoot 'Dist\SMM\bin\ShelteredAPI.dll'),
    (Join-Path $RepoRoot 'Dist\SMM\bin\ShelteredScenarioEditor.dll')
)

foreach ($assemblyPath in $assemblies) {
    if (-not (Test-Path -LiteralPath $assemblyPath)) {
        throw "Required composition verification assembly was not found: $assemblyPath"
    }
    [void][Reflection.Assembly]::LoadFrom($assemblyPath)
}

$editorAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $RepoRoot 'Dist\SMM\bin\ShelteredScenarioEditor.dll'))
$verificationType = $editorAssembly.GetType(
    'ShelteredScenarioEditor.Diagnostics.ScenarioEditorCompositionVerification',
    $true,
    $false)
$runMethod = $verificationType.GetMethod('Run', [Reflection.BindingFlags]'Public,Static')
if ($null -eq $runMethod) {
    throw 'ScenarioEditorCompositionVerification.Run was not found.'
}

$errors = [string[]]$runMethod.Invoke($null, @())
if ($errors.Count -gt 0) {
    throw ("Scenario editor composition verification failed:`n - " + ($errors -join "`n - "))
}

Write-Host 'SCENARIO EDITOR COMPOSITION PASS'
