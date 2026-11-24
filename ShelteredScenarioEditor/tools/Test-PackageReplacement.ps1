[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AssemblyRoot,
    [Parameter(Mandatory = $true)][string]$ScratchRoot,
    [string]$ManagedRoot = 'D:\Epic Games Games\Sheltered\ShelteredWindows64_EOS_Data\Managed'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
foreach ($path in @(
    (Join-Path $ManagedRoot 'UnityEngine.dll'),
    (Join-Path $ManagedRoot 'UnityEngine.UI.dll'),
    (Join-Path $ManagedRoot 'Assembly-CSharp.dll'),
    (Join-Path $repo 'mods\0Harmony\Assemblies\0Harmony.dll'),
    (Join-Path $AssemblyRoot 'ModAPI.dll'),
    (Join-Path $AssemblyRoot 'ShelteredAPI.dll')
)) {
    [void][Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $path).Path)
}

$editor = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath (Join-Path $AssemblyRoot 'ShelteredScenarioEditor.dll')).Path)
$type = $editor.GetType('ShelteredScenarioEditor.Diagnostics.ScenarioPackageUxVerification', $true)
$root = [IO.Path]::GetFullPath($ScratchRoot)
[void][IO.Directory]::CreateDirectory($root)
$fixture = Join-Path $root ('scenario-package-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($fixture)
$previousDirectory = [IO.Directory]::GetCurrentDirectory()
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
try {
    [IO.Directory]::SetCurrentDirectory($fixture)
    $env:TEMP = $fixture
    $env:TMP = $fixture
    $failures = @($type.GetMethod('Run').Invoke($null, [string[]]@($fixture)))
}
finally {
    [IO.Directory]::SetCurrentDirectory($previousDirectory)
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}
if ($failures.Length -gt 0) {
    throw ("Package verification failed:`n - " + ($failures -join "`n - "))
}
Write-Host 'Package verification passed. Export and install passed all five fault steps, corruption checks, file locks and recovery.'
