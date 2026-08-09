[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Steam', 'Epic')]
    [string]$Lane,

    [Parameter(Mandatory = $true)]
    [string]$Scenarios,

    [Parameter(Mandatory = $true)]
    [string]$EvidenceRoot,

    [string]$ShelteredRoot,
    [string]$HarnessRepo,
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrWhiteSpace($ShelteredRoot)) { $ShelteredRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..\..\..')) }
if ([string]::IsNullOrWhiteSpace($HarnessRepo)) {
    $environmentHarness = [Environment]::GetEnvironmentVariable('SHELTERED_AGENT_INTERFACE_ROOT')
    $HarnessRepo = if (-not [string]::IsNullOrWhiteSpace($environmentHarness)) { $environmentHarness } else { 'A:\Dev\Projects\ShelteredAgentInterface' }
}

$scenarioIds = @(
    $Scenarios -split '[,;]' |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
)
if ($scenarioIds.Count -eq 0) { throw 'At least one release scenario ID is required.' }

$graphPath = Join-Path $scriptRoot 'incremental-release-graph.json'
$stageScript = Join-Path $scriptRoot 'Stage-ElevenModRuntime.ps1'
$restoreScript = Join-Path $scriptRoot 'Restore-ElevenModRuntime.ps1'
$transactionRunner = Join-Path $HarnessRepo 'tools\Invoke-TransactionalReleaseScenario.ps1'
foreach ($required in @($graphPath, $stageScript, $restoreScript, $transactionRunner)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required exact-package matrix dependency is missing: $required" }
}

$evidence = [IO.Path]::GetFullPath($EvidenceRoot)
if (Test-Path -LiteralPath $evidence) { throw "Evidence root already exists; refusing to overwrite it: $evidence" }
$gameRoot = if ($Lane -eq 'Steam') { 'A:\SteamLibrary\steamapps\common\Sheltered' } else { 'D:\Epic Games Games\Sheltered' }
$graph = Get-Content -LiteralPath $graphPath -Raw | ConvertFrom-Json
$selectedScenarios = New-Object System.Collections.Generic.List[object]
foreach ($scenarioId in $scenarioIds) {
    $scenario = @($graph.scenarios | Where-Object { [string]$_.id -eq $scenarioId }) | Select-Object -First 1
    if ($null -eq $scenario) { throw "Unknown release scenario ID: $scenarioId" }
    if (@($scenario.platforms) -notcontains $Lane) { throw "Scenario '$scenarioId' does not support $Lane." }
    if (@($scenario.steps).Count -ne 1) { throw "Exact-package matrix requires one completion-backed step for '$scenarioId'." }
    $step = @($scenario.steps)[0]
    if (-not $step.PSObject.Properties['scenario'] -or [string]::IsNullOrWhiteSpace([string]$step.scenario)) {
        throw "Scenario '$scenarioId' is not completion-backed."
    }
    [void]$selectedScenarios.Add([pscustomobject]@{
        id = $scenarioId
        route = [string]$scenario.fixturePath
        argument = if ($step.PSObject.Properties['argument']) { [string]$step.argument } else { 'confirm=true' }
    })
}

$results = New-Object System.Collections.Generic.List[object]
$failure = $null
$restoreFailure = $null
$statePath = Join-Path $evidence 'deployment-state.json'
try {
    $null = & powershell -NoProfile -ExecutionPolicy Bypass -File $stageScript -Lane $Lane -EvidenceRoot $evidence -WorkspaceRoot $ShelteredRoot
    if ($LASTEXITCODE -ne 0) { throw "Exact eleven-package staging exited $LASTEXITCODE." }

    foreach ($scenario in $selectedScenarios) {
        $scenarioEvidence = Join-Path $evidence ([string]$scenario.id)
        $null = & powershell -NoProfile -ExecutionPolicy Bypass -File $transactionRunner -Platform $Lane -GameRoot $gameRoot -Route $scenario.route -Scenario $scenario.id -Argument $scenario.argument -EvidenceRoot $scenarioEvidence -TimeoutSeconds $TimeoutSeconds
        if ($LASTEXITCODE -ne 0) { throw "Scenario '$($scenario.id)' exited $LASTEXITCODE on $Lane." }
        $transactionPath = Join-Path $scenarioEvidence 'transaction-report.json'
        if (-not (Test-Path -LiteralPath $transactionPath -PathType Leaf)) { throw "Scenario '$($scenario.id)' produced no transaction report." }
        $transaction = Get-Content -LiteralPath $transactionPath -Raw | ConvertFrom-Json
        if (-not [bool]$transaction.ok -or -not [bool]$transaction.restoration.ok -or -not [bool]$transaction.result.ok) {
            throw "Scenario '$($scenario.id)' did not produce a passing, restored transaction report."
        }
        [void]$results.Add([pscustomobject]@{
            scenario = [string]$scenario.id
            platform = $Lane
            ok = $true
            transactionReport = $transactionPath
            completedUtc = [string]$transaction.completedUtc
        })
    }
} catch {
    $failure = $_
} finally {
    if (Test-Path -LiteralPath $statePath -PathType Leaf) {
        try {
            $null = & powershell -NoProfile -ExecutionPolicy Bypass -File $restoreScript -Lane $Lane -EvidenceRoot $evidence
            if ($LASTEXITCODE -ne 0) { throw "Exact eleven-package restoration exited $LASTEXITCODE." }
        } catch {
            $restoreFailure = $_
        }
    }

    if (Test-Path -LiteralPath $evidence -PathType Container) {
        $report = [pscustomobject]@{
            schemaVersion = 1
            lane = $Lane
            scenarios = $scenarioIds
            exactPackageCount = 11
            results = $results.ToArray()
            restored = ($null -eq $restoreFailure)
            failure = if ($null -eq $failure) { $null } else { $failure.Exception.Message }
            restorationFailure = if ($null -eq $restoreFailure) { $null } else { $restoreFailure.Exception.Message }
            completedUtc = [DateTime]::UtcNow.ToString('o')
            ok = ($null -eq $failure -and $null -eq $restoreFailure -and $results.Count -eq $scenarioIds.Count)
        }
        $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'exact-package-matrix-report.json') -Encoding UTF8
    }
}

if ($null -ne $failure) { throw $failure }
if ($null -ne $restoreFailure) { throw $restoreFailure }
Get-Content -LiteralPath (Join-Path $evidence 'exact-package-matrix-report.json') -Raw
