#requires -Version 5.1
<#
.SYNOPSIS
Runs config-driven vanilla and modded Sheltered performance benchmarks.
.PARAMETER DryRun
Validates and prints the resolved case/mod plan without changing installs.
.PARAMETER ValidateOnly
Validates configuration and install paths, then exits.
.PARAMETER ParallelPlatforms
Runs separate Steam and Epic installs concurrently.
.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\performance\Invoke-ShelteredBenchmark.ps1 -DryRun
.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\performance\Invoke-ShelteredBenchmark.ps1 -Platform steam,epic -Profile vanilla,smm-core,all-mods -Iterations 3 -ParallelPlatforms
#>
[CmdletBinding()]
param(
    [string]$ConfigPath = '',
    [string[]]$Platform,
    [string[]]$Profile,
    [int]$Iterations = 0,
    [switch]$DryRun,
    [switch]$ValidateOnly,
    [switch]$ParallelPlatforms,
    [switch]$MatchedSerial,
    [switch]$SkipBuild,
    [ValidateRange(0, 3600)][int]$ScenarioTimeoutSeconds = 0,
    [ValidateRange(0, 3600)][int]$FpsDurationSeconds = 0,
    [ValidateRange(0, 3600)][int]$VanillaIdleSeconds = 0,
    [string]$RunLabel = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ConfigPath)) { $ConfigPath = Join-Path $PSScriptRoot 'benchmark.config.example.json' }
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runnerModule = Join-Path $PSScriptRoot 'ShelteredBenchmark.Runner.psm1'
Import-Module $runnerModule -Force -DisableNameChecking
Import-Module (Join-Path $PSScriptRoot 'ShelteredBenchmark.Core.psm1') -Force -DisableNameChecking
$config = Import-ShelteredBenchmarkConfig -Path $ConfigPath
if ($ScenarioTimeoutSeconds -gt 0) { $config.sampling.scenarioTimeoutSeconds = $ScenarioTimeoutSeconds }
if ($FpsDurationSeconds -gt 0) { $config.sampling.fpsDurationSeconds = $FpsDurationSeconds }
if ($VanillaIdleSeconds -gt 0) { $config.sampling.vanillaIdleSeconds = $VanillaIdleSeconds }
$validation = Test-ShelteredBenchmarkConfig -Config $config
foreach ($warning in $validation.Warnings) { Write-Warning $warning }
if (-not $validation.Valid) { throw "Invalid benchmark configuration: $($validation.Errors -join '; ')" }
if ($ValidateOnly) { Write-Host "Benchmark configuration is valid: $($config._configPath)"; return }
$Platform = @($Platform | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Profile = @($Profile | ForEach-Object { $_ -split ',' } | Where-Object { $_ })

$platforms = @($config.platforms | Where-Object { [bool](Get-ObjectPropertyValue $_ 'enabled' $true) })
$allProfiles = @($config.profiles)
$profiles = if ($Profile.Count -gt 0) { $allProfiles } else { @($allProfiles | Where-Object { [bool](Get-ObjectPropertyValue $_ 'enabled' $true) }) }
if ($Platform.Count -gt 0) {
    $unknown = @($Platform | Where-Object { $name = $_; -not ($platforms | Where-Object name -IEQ $name) })
    if ($unknown.Count) { throw "Unknown or disabled platform(s): $($unknown -join ', ')" }
    $platforms = @($platforms | Where-Object { $item = $_; $Platform | Where-Object { $_ -ieq $item.name } })
}
if ($Profile.Count -gt 0) {
    $unknown = @($Profile | Where-Object { $name = $_; -not ($profiles | Where-Object name -IEQ $name) })
    if ($unknown.Count) { throw "Unknown or disabled profile(s): $($unknown -join ', ')" }
    $profiles = @($profiles | Where-Object { $item = $_; $Profile | Where-Object { $_ -ieq $item.name } })
}
if (-not $platforms.Count -or -not $profiles.Count) { throw 'The selected benchmark plan has no cases.' }
foreach ($target in $platforms) {
    $target.installRoot = Resolve-ConfiguredPath ([string]$target.installRoot) ([string]$config._configRoot)
}
$iterationCount = if ($Iterations -gt 0) { $Iterations } else { [int](Get-ObjectPropertyValue $config 'iterations' 1) }
if ($iterationCount -lt 1) { throw 'Iterations must be at least 1.' }
$useParallel = if ($MatchedSerial) { $false } else { $ParallelPlatforms -or [bool](Get-ObjectPropertyValue $config 'parallelPlatforms' $false) }
$coreIds = @((Get-ObjectPropertyValue $config 'coreModIds' @('com.harmony.0harmony', 'coolnether123.shelteredagentinterface')) | ForEach-Object { [string]$_ })
$plan = New-Object 'System.Collections.Generic.List[object]'
foreach ($iteration in 1..$iterationCount) {
    foreach ($profileItem in $profiles) {
        foreach ($platformItem in $platforms) {
            $execution = Resolve-BenchmarkExecution -Profile $profileItem -ParallelPlatforms $useParallel -PlatformCount $platforms.Count -ForceMatchedSerial:$MatchedSerial
            $catalog = Get-InstalledModCatalog ([string]$platformItem.installRoot)
            $existing = Get-LoadOrderState ([string]$platformItem.installRoot)
            $order = if ($null -ne $existing) { @($existing.order) } else { @() }
            $enabledIds = Get-EnabledLoadOrderIds $existing
            $platformItem | Add-Member -NotePropertyName '_benchmarkEnabledIds' -NotePropertyValue @($enabledIds) -Force
            $mods = Resolve-ShelteredModProfile -Profile $profileItem -Catalog $catalog -ExistingOrder $order -CoreModIds $coreIds -ExistingEnabledIds $enabledIds
            $plan.Add([pscustomobject]@{
                Platform = [string]$platformItem.name; Profile = [string]$profileItem.name
                Mode = [string]$profileItem.mode; Iteration = $iteration
                InstallRoot = [string]$platformItem.installRoot
                Executable = Join-Path ([string]$platformItem.installRoot) ([string]$platformItem.executable)
                SelectedModIds = @($mods)
                Harness = [bool](Get-ObjectPropertyValue $profileItem 'harness' (([string]$profileItem.mode -ne 'vanilla')))
                ExecutionMode = $execution.ExecutionMode
                ComparisonLane = $execution.ComparisonLane
            })
        }
    }
}
if ($DryRun) {
    [pscustomobject]@{ ConfigPath = $config._configPath; ParallelPlatforms = $useParallel; Cases = $plan.ToArray() } | ConvertTo-Json -Depth 10
    return
}

$datePart = Get-Date -Format 'yyyy-MM-dd_HHmmss'
$safeLabel = if ($RunLabel) { $RunLabel -replace '[^A-Za-z0-9_.-]', '_' } else { 'Sheltered_Performance' }
$runId = $datePart + '_' + $safeLabel
$outputBase = Resolve-ConfiguredPath ([string]$config.outputRoot) ([string]$config._configRoot)
$runRoot = Join-Path $outputBase $runId
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$metadata = [pscustomobject]@{
    RunId = $runId; CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    ConfigPath = [string]$config._configPath; RepositoryRoot = $repositoryRoot
    ParallelPlatforms = $useParallel; MatchedSerial = [bool]$MatchedSerial; Iterations = $iterationCount
}
$metadata | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $runRoot 'run.json') -Encoding UTF8
$plan.ToArray() | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $runRoot 'plan.json') -Encoding UTF8
$config | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $runRoot 'benchmark.config.json') -Encoding UTF8

$suiteLocks = Enter-BenchmarkInstallLocks @($platforms | ForEach-Object { [string]$_.installRoot })
$suiteSnapshots = New-Object 'System.Collections.Generic.List[object]'
$activeJobs = New-Object 'System.Collections.Generic.List[object]'
try {
    foreach ($platformItem in $platforms) {
        $safeName = ([string]$platformItem.name) -replace '[^A-Za-z0-9_.-]', '_'
        $snapshotRoot = Join-Path $runRoot ("suite-install-state-before\$safeName")
        $suiteSnapshots.Add((New-InstallStateSnapshot ([string]$platformItem.installRoot) $snapshotRoot))
    }
    $build = Get-ObjectPropertyValue $config 'build'
    if (-not $SkipBuild -and $null -ne $build -and [bool](Get-ObjectPropertyValue $build 'enabled' $false)) {
        [void](Invoke-BenchmarkCommand $build ([string]$config._configRoot) $repositoryRoot (Join-Path $runRoot 'build.log'))
    }
    if (-not $SkipBuild) {
        foreach ($platformItem in $platforms) {
            $prepare = Get-ObjectPropertyValue $platformItem 'prepare'
            if ($null -ne $prepare -and [bool](Get-ObjectPropertyValue $prepare 'enabled' $false)) {
                $safeName = ([string]$platformItem.name) -replace '[^A-Za-z0-9_.-]', '_'
                [void](Invoke-BenchmarkCommand $prepare ([string]$config._configRoot) $repositoryRoot (Join-Path $runRoot ("prepare-$safeName.log")))
            }
        }
    }

    # Freeze the verified deployment identities for the duration of this run.
    # Another IDE/build may legitimately rewrite obj/Dist while a long matrix
    # is collecting, but that must not invalidate an unchanged deployed stack.
    # A later external deployment still fails because the installed hash no
    # longer matches this immutable expectation.
    $requiresInstrumentedGates = @($profiles | Where-Object {
        ([string]$_.mode -ne 'vanilla') -and [bool](Get-ObjectPropertyValue $_ 'harness' $true)
    }).Count -gt 0
    if ($requiresInstrumentedGates) {
        $frozenGates = New-Object 'System.Collections.Generic.List[object]'
        foreach ($platformItem in $platforms) {
            $gates = @(Get-ObjectPropertyValue $platformItem 'hashGates' @())
            $safeName = ([string]$platformItem.name) -replace '[^A-Za-z0-9_.-]', '_'
            $preflightPath = Join-Path $runRoot ("deployment-preflight-$safeName.json")
            $verified = Test-BenchmarkDeploymentHashes -Gates $gates -InstallRoot ([string]$platformItem.installRoot) `
                -ConfigRoot ([string]$config._configRoot) -OutputPath $preflightPath
            for ($gateIndex = 0; $gateIndex -lt $gates.Count; $gateIndex++) {
                $requiredHash = [string]$verified.Gates[$gateIndex].RequiredSha256
                $gates[$gateIndex] | Add-Member -NotePropertyName sha256 -NotePropertyValue $requiredHash -Force
                $frozenGates.Add([pscustomobject]@{
                    Platform = [string]$platformItem.name
                    Name = [string](Get-ObjectPropertyValue $gates[$gateIndex] 'name' 'unnamed')
                    RequiredSha256 = $requiredHash
                    DeployedPath = [string]$verified.Gates[$gateIndex].DeployedPath
                    SourcePath = [string]$verified.Gates[$gateIndex].SourcePath
                })
            }
        }
        $frozenGates.ToArray() | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $runRoot 'frozen_deployment_hashes.json') -Encoding UTF8
    }

    $results = New-Object 'System.Collections.Generic.List[object]'
    foreach ($iteration in 1..$iterationCount) {
        foreach ($profileItem in $profiles) {
            $execution = Resolve-BenchmarkExecution -Profile $profileItem -ParallelPlatforms $useParallel -PlatformCount $platforms.Count -ForceMatchedSerial:$MatchedSerial
            if ($execution.ExecutionMode -eq 'parallel-platforms') {
            $jobs = @()
            foreach ($platformItem in $platforms) {
                $platformSafe = ([string]$platformItem.name) -replace '[^A-Za-z0-9_.-]', '_'
                $profileSafe = ([string]$profileItem.name) -replace '[^A-Za-z0-9_.-]', '_'
                $caseRoot = Join-Path $runRoot ("cases\{0}\{1}\iteration-{2:d3}" -f $platformSafe, $profileSafe, $iteration)
                $job = Start-Job -Name "$platformSafe-$profileSafe-$iteration" -ScriptBlock {
                    param($module, $platform, $profile, $configuration, $repo, $root, $number, $id, $executionMode, $comparisonLane)
                    Import-Module $module -Force -DisableNameChecking
                    Invoke-ShelteredBenchmarkCase $platform $profile $configuration $repo $root $number $id $executionMode $comparisonLane
                } -ArgumentList $runnerModule, $platformItem, $profileItem, $config, $repositoryRoot, $caseRoot, $iteration, $runId, $execution.ExecutionMode, $execution.ComparisonLane
                $jobs += $job
                $activeJobs.Add($job)
            }
            foreach ($job in $jobs) {
                [void](Wait-Job $job)
                $received = @(Receive-Job $job -ErrorAction SilentlyContinue)
                $result = $received | Where-Object { $_.PSObject.Properties['Platform'] -and $_.PSObject.Properties['Profile'] } | Select-Object -Last 1
                if ($null -eq $result) {
                    $result = [pscustomobject]@{
                        Platform = $job.Name; Profile = [string]$profileItem.name; Iteration = $iteration
                        ExecutionMode = $execution.ExecutionMode; ComparisonLane = $execution.ComparisonLane
                        Status = 'failed'; Error = (($job.ChildJobs[0].JobStateInfo.Reason | Out-String).Trim())
                        ReadinessMethod = ''; StartupMs = $null; StartupCpuSeconds = $null
                        PeakWorkingSetMiB = $null; ScenarioTransitionMs = $null
                        MenuMedianSmoothFps = $null; MenuP05SmoothFps = $null
                    }
                }
                $results.Add($result)
                Remove-Job $job -Force
                [void]$activeJobs.Remove($job)
            }
            }
            else {
                foreach ($platformItem in $platforms) {
                    $platformSafe = ([string]$platformItem.name) -replace '[^A-Za-z0-9_.-]', '_'
                    $profileSafe = ([string]$profileItem.name) -replace '[^A-Za-z0-9_.-]', '_'
                    $caseRoot = Join-Path $runRoot ("cases\{0}\{1}\iteration-{2:d3}" -f $platformSafe, $profileSafe, $iteration)
                    $result = Invoke-ShelteredBenchmarkCase $platformItem $profileItem $config $repositoryRoot $caseRoot $iteration $runId $execution.ExecutionMode $execution.ComparisonLane
                    $results.Add($result)
                }
            }
        }
    }
    Write-BenchmarkAggregateReport $results.ToArray() $runRoot $metadata
    Write-Host "Benchmark complete: $runRoot"
    if (@($results | Where-Object Status -EQ 'failed').Count -and [bool](Get-ObjectPropertyValue $config 'failOnCaseFailure' $true)) {
        throw 'One or more cases failed. Reports were written and install restoration was attempted.'
    }
}
finally {
    $suiteRestoreErrors = New-Object 'System.Collections.Generic.List[string]'
    try {
        foreach ($job in @($activeJobs.ToArray())) {
            try {
                if ($job.State -in @('NotStarted', 'Running')) { Stop-Job $job -ErrorAction SilentlyContinue }
                [void](Wait-Job $job -Timeout 15 -ErrorAction SilentlyContinue)
                [void](Receive-Job $job -ErrorAction SilentlyContinue)
                Remove-Job $job -Force -ErrorAction SilentlyContinue
            }
            catch { $suiteRestoreErrors.Add("Job '$($job.Name)' cleanup failed: $($_.Exception.Message)") }
        }
        $orphanCleanup = New-Object 'System.Collections.Generic.List[object]'
        foreach ($ownershipPath in @(Get-ChildItem -LiteralPath (Join-Path $runRoot 'cases') -Filter 'owned_process.json' -Recurse -File -ErrorAction SilentlyContinue)) {
            try {
                $ownership = Get-Content -LiteralPath $ownershipPath.FullName -Raw | ConvertFrom-Json
                $ownedProcess = Get-Process -Id ([int]$ownership.Pid) -ErrorAction SilentlyContinue
                if ($null -eq $ownedProcess) { continue }
                $actualStart = [DateTimeOffset]$ownedProcess.StartTime.ToUniversalTime()
                $expectedStart = [DateTimeOffset]::Parse([string]$ownership.StartTimeUtc)
                $identityMatches = $ownedProcess.ProcessName -ieq [string]$ownership.ProcessName -and [math]::Abs(($actualStart - $expectedStart).TotalSeconds) -lt 1
                if (-not $identityMatches) {
                    $orphanCleanup.Add([pscustomobject]@{ Pid = $ownership.Pid; Stopped = $false; Reason = 'PID was reused by a different process identity'; Evidence = $ownershipPath.FullName })
                    continue
                }
                Stop-Process -Id $ownedProcess.Id -Force -ErrorAction Stop
                $ownedProcess.WaitForExit(10000)
                $orphanCleanup.Add([pscustomobject]@{ Pid = $ownership.Pid; Stopped = $true; Reason = 'owned benchmark orphan stopped'; Evidence = $ownershipPath.FullName })
            }
            catch { $suiteRestoreErrors.Add("Owned-process cleanup failed for '$($ownershipPath.FullName)': $($_.Exception.Message)") }
        }
        $orphanCleanupJson = if ($orphanCleanup.Count -gt 0) { $orphanCleanup.ToArray() | ConvertTo-Json -Depth 5 } else { '[]' }
        Set-Content -LiteralPath (Join-Path $runRoot 'orphan_process_cleanup.json') -Value $orphanCleanupJson -Encoding UTF8
        foreach ($snapshot in $suiteSnapshots) {
            $platformItem = $platforms | Where-Object { [string]$_.installRoot -eq [string]$snapshot.InstallRoot } | Select-Object -First 1
            $active = if ($null -ne $platformItem) { @(Get-Process -Name ([string]$platformItem.processName) -ErrorAction SilentlyContinue) } else { @() }
            if ($active.Count -gt 0) {
                $suiteRestoreErrors.Add("$($snapshot.InstallRoot): restoration blocked because process(es) $($active.Id -join ', ') still hold the install.")
                continue
            }
            try { Restore-InstallStateSnapshot $snapshot }
            catch { $suiteRestoreErrors.Add("$($snapshot.InstallRoot): $($_.Exception.Message)") }
        }
    }
    finally { Exit-BenchmarkInstallLocks $suiteLocks }
    if ($suiteRestoreErrors.Count -gt 0) {
        $suiteRestoreErrors.ToArray() | Set-Content (Join-Path $runRoot 'SUITE_RESTORE_FAILURE.txt') -Encoding UTF8
        throw "Benchmark suite could not restore one or more installs: $($suiteRestoreErrors.ToArray() -join '; ')"
    }
}
