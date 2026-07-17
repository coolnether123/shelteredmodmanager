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
    (Join-Path $repoRoot 'Dist\SMM\bin\ShelteredAPI.dll')
)

foreach ($assemblyPath in $assemblies) {
    if (-not (Test-Path -LiteralPath $assemblyPath)) {
        throw "Required routing verification assembly was not found: $assemblyPath"
    }
    [void][Reflection.Assembly]::LoadFrom($assemblyPath)
}

$shelteredApi = [Reflection.Assembly]::LoadFrom((Join-Path $repoRoot 'Dist\SMM\bin\ShelteredAPI.dll'))
$verificationType = $shelteredApi.GetType(
    'ShelteredAPI.Scenarios.Diagnostics.ScenarioWorkspaceRoutingVerification',
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

Write-Host 'SCENARIO WORKSPACE ROUTING PASS'
