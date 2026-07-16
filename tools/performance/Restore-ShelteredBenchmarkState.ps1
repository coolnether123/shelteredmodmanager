#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true)][string]$RunRoot,
    [string[]]$Platform,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ShelteredBenchmark.Core.psm1') -Force -DisableNameChecking
$resolvedRunRoot = [IO.Path]::GetFullPath($RunRoot)
$configPath = Join-Path $resolvedRunRoot 'benchmark.config.json'
$snapshotBase = Join-Path $resolvedRunRoot 'suite-install-state-before'
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) { throw "Benchmark config not found: $configPath" }
if (-not (Test-Path -LiteralPath $snapshotBase -PathType Container)) { throw "Suite snapshots not found: $snapshotBase" }

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$requestedPlatforms = @($Platform | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$selectedPlatforms = @($config.platforms | Where-Object { $requestedPlatforms.Count -eq 0 -or $requestedPlatforms -icontains [string]$_.name })
if ($selectedPlatforms.Count -eq 0) { throw 'No configured platform matched the recovery selection.' }
$restored = New-Object 'System.Collections.Generic.List[object]'
$locks = $null
try {
    $installRoots = @($selectedPlatforms | ForEach-Object { [string]$_.installRoot })
    $locks = Enter-BenchmarkInstallLocks -InstallRoots $installRoots
    foreach ($platformItem in $selectedPlatforms) {
        $safeName = ([string]$platformItem.name) -replace '[^A-Za-z0-9_.-]', '_'
        $snapshotPath = Join-Path $snapshotBase (Join-Path $safeName 'snapshot.json')
        if (-not (Test-Path -LiteralPath $snapshotPath -PathType Leaf)) { throw "Snapshot missing for '$($platformItem.name)': $snapshotPath" }
        $active = @(Get-Process -Name ([string]$platformItem.processName) -ErrorAction SilentlyContinue)
        if ($active.Count -gt 0) {
            throw "Refusing to restore '$($platformItem.name)' while process(es) $($active.Id -join ', ') are active."
        }
        $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json
        if ($Force -or $PSCmdlet.ShouldProcess([string]$snapshot.InstallRoot, "restore SHA-verified benchmark suite state from $snapshotPath")) {
            Restore-InstallStateSnapshot -Snapshot $snapshot
            $restored.Add([pscustomobject]@{
                Platform = [string]$platformItem.name
                InstallRoot = [string]$snapshot.InstallRoot
                Snapshot = $snapshotPath
                RestoredAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
            })
        }
    }
}
finally {
    if ($null -ne $locks) { Exit-BenchmarkInstallLocks -Locks $locks }
}

$resultPath = Join-Path $resolvedRunRoot 'manual_restore_result.json'
$restored.ToArray() | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath -Encoding UTF8
Write-Host "Restored $($restored.Count) install(s). Evidence: $resultPath"
