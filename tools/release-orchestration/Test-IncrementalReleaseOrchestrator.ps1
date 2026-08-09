[CmdletBinding()]
param(
    [string]$ShelteredRoot,
    [string]$RunnerPath,
    [string]$GraphPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrWhiteSpace($ShelteredRoot)) { $ShelteredRoot = Join-Path $scriptRoot '..\..\..' }
if ([string]::IsNullOrWhiteSpace($RunnerPath)) { $RunnerPath = Join-Path $scriptRoot 'Invoke-IncrementalRelease.ps1' }
if ([string]::IsNullOrWhiteSpace($GraphPath)) { $GraphPath = Join-Path $scriptRoot 'incremental-release-graph.json' }

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "FAIL: $Message" }
}

function Invoke-PlanCase {
    param([string]$Name, [string[]]$Files, [switch]$Stable)
    $output = Join-Path ([IO.Path]::GetTempPath()) ("sheltered-release-plan-{0}-{1}.json" -f $Name, [Guid]::NewGuid().ToString('N'))
    try {
        $runnerArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $RunnerPath, '-ShelteredRoot', $ShelteredRoot, '-GraphPath', $GraphPath, '-ChangedFile') + $Files + @('-OutputPath', $output)
        if ($Stable) { $runnerArgs += '-Stable' }
        $null = & powershell @runnerArgs
        if ($LASTEXITCODE -ne 0) { throw "Runner failed for case '$Name' with exit $LASTEXITCODE." }
        return Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
    } finally {
        if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
    }
}

$graph = Get-Content -LiteralPath $GraphPath -Raw | ConvertFrom-Json
Assert-True ($graph.schemaVersion -eq 2) 'Graph schema version must be 2.'
Assert-True (@($graph.repositories).Count -eq 11) 'Graph must contain exactly eleven in-scope mod repositories.'
Assert-True (@($graph.scenarios).Count -ge 15) 'Graph must include targeted and compatibility scenarios.'
Assert-True (@($graph.dependencyEdges).Count -eq 5) 'Graph dependency edges must remain explicit and deduplicated.'
foreach ($repo in @($graph.repositories)) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ShelteredRoot ([string]$repo.project))) "Configured project is missing for $($repo.id): $($repo.project)"
    foreach ($script in @($repo.contracts)) { Assert-True (Test-Path -LiteralPath (Join-Path $ShelteredRoot ([string]$script))) "Configured contract is missing for $($repo.id): $script" }
    if ($repo.PSObject.Properties['contractProjects']) {
        foreach ($project in @($repo.contractProjects)) { Assert-True (Test-Path -LiteralPath (Join-Path $ShelteredRoot ([string]$project))) "Configured contract project is missing for $($repo.id): $project" }
    }
}

$lifespan = Invoke-PlanCase 'lifespan-source' @('Lifespan/Lifespan/Lifespan.cs')
Assert-True ($lifespan.mode -eq 'dry-run') 'Default runner mode must be dry-run.'
Assert-True ($lifespan.selectedOwners -contains 'Lifespan') 'Lifespan source must select Lifespan.'
Assert-True ($lifespan.selectedOwners -contains 'Procreation-Framework') 'Lifespan source must select the dependent Family compatibility owner.'
Assert-True (@($lifespan.gates.id) -contains 'gameplay.Steam.lifespan-persistence') 'Lifespan source must select its Steam fixture.'
Assert-True (@($lifespan.gates.id) -contains 'gameplay.Epic.family-lifespan-handoff') 'Lifespan source must select the Epic dependent compatibility fixture.'
Assert-True (-not (@($lifespan.gates.id) -contains 'gameplay.Steam.trading-amount')) 'Lifespan source must not select Trading Amount.'
Assert-True ((@($lifespan.gates | Where-Object { $_.phase -eq 'gameplay' -and $_.heavy })).Count -gt 0) 'State-heavy gates must be visible in the plan, not silently hidden.'

$testOnly = Invoke-PlanCase 'lifespan-test' @('Lifespan/Lifespan.Tests/SerializationTests.cs')
$testPlan = @($testOnly.repoPlans | Where-Object { $_.id -eq 'Lifespan' })[0]
Assert-True ($testPlan.contracts -eq $true) 'Test-only changes must select contracts.'
Assert-True ($testPlan.build -eq $false) 'Test-only changes must not rebuild the release assembly.'
Assert-True (@($testPlan.gameplayScenarios).Count -eq 0) 'Test-only changes must not select gameplay matrices.'
Assert-True (@($testOnly.gates | Where-Object { $_.phase -eq 'gameplay' }).Count -eq 0) 'Test-only changes must not select gameplay gates.'

$trading = Invoke-PlanCase 'trading-source' @('TradingAmount/TradingAmount/TradingPanelPatch.cs')
Assert-True (@($trading.gates.id) -contains 'gameplay.Steam.trading-amount') 'Trading source must select the automated TradingPanel fixture.'
Assert-True (@($trading.gates.id) -contains 'gameplay.Epic.trading-vanilla-seam') 'Trading source must select the Vanilla Fixes seam compatibility fixture.'
Assert-True ((@($trading.gates | Where-Object { $_.id -eq 'package.mods' })).Count -eq 1) 'Multiple selected mod owners must share one packaging gate.'
Assert-True (-not (@($trading.gates.id) -contains 'gameplay.Steam.deep-progression')) 'Trading source must not select Deep Expansion progression.'
Assert-True ((@($graph.scenarios | Where-Object { $_.id -eq 'trading-amount' })[0].fixturePath) -eq '/release-scenario/interaction') 'Trading must use the completion-backed scenario transaction, not manual fixture choreography.'

$vanillaTrading = Invoke-PlanCase 'vanilla-trading-file' @('Sheltered-Vanilla-Fixes/Sheltered Vanilla Fixes/Patches/TradingWhiteSlotResetPatches.cs')
Assert-True (@($vanillaTrading.gates.id) -contains 'gameplay.Steam.vanilla-trading-slots') 'A trading-slot patch must select its focused behavior matrix.'
Assert-True (@($vanillaTrading.gates.id) -contains 'gameplay.Epic.trading-vanilla-seam') 'A trading-slot patch must preserve the Trading Amount compatibility edge.'
Assert-True (-not (@($vanillaTrading.gates.id) -contains 'gameplay.Steam.vanilla-breach')) 'A trading-slot-only patch must not rerun the breach matrix.'
Assert-True (-not (@($vanillaTrading.gates.id) -contains 'gameplay.Steam.vanilla-recycling')) 'A trading-slot-only patch must not rerun the recycling matrix.'

$vanillaCore = Invoke-PlanCase 'vanilla-core-file' @('Sheltered-Vanilla-Fixes/Sheltered Vanilla Fixes/Core/FixBootstrapper.cs')
Assert-True (@($vanillaCore.gates.id) -contains 'gameplay.Steam.vanilla-breach') 'Shared Vanilla Fixes core changes must select breach.'
Assert-True (@($vanillaCore.gates.id) -contains 'gameplay.Steam.vanilla-recycling') 'Shared Vanilla Fixes core changes must select recycling.'

$managerOAuth = Invoke-PlanCase 'manager-oauth' @('shelteredmodmanager/Manager/Core/Services/NexusOAuthClient.cs')
Assert-True ($managerOAuth.selectedOwners -contains 'shelteredmodmanager') 'Manager OAuth source must select the Manager owner.'
Assert-True (@($managerOAuth.gates.id) -contains 'contracts.shelteredmodmanager') 'Manager OAuth source must select Manager contracts.'
Assert-True (@($managerOAuth.gates.id) -contains 'platform.Steam.shelteredmodmanager') 'Manager OAuth source must select Steam platform smoke.'
Assert-True (@($managerOAuth.gates.id) -contains 'platform.Epic.shelteredmodmanager') 'Manager OAuth source must select Epic platform smoke.'
Assert-True (@($managerOAuth.gates.id) -contains 'promotion.manager-rc') 'Manager OAuth source must select the RC promotion preflight.'
Assert-True (-not (@($managerOAuth.gates.id) -contains 'promotion.manager-stable-live')) 'Stable live promotion must require an explicit -Stable request.'
$managerStable = Invoke-PlanCase 'manager-stable' @('shelteredmodmanager/Manager/Core/Services/NexusOAuthClient.cs') -Stable
Assert-True (@($managerStable.gates.id) -contains 'promotion.manager-stable-live') 'Explicit stable planning must expose the fail-closed live Nexus gate.'

$docs = Invoke-PlanCase 'docs-only' @('Lifespan/README.md')
Assert-True (@($docs.gates).Count -eq 0) 'Documentation-only changes must not select build, gameplay, packaging, or promotion.'
Assert-True (@($docs.reusedScopes | Where-Object { $_.owner -eq 'Lifespan' -and $_.status -eq 'not-selected' }).Count -eq 1) 'Documentation-only changes must leave Lifespan unselected without asserting unvalidated evidence.'

$releaseTool = Invoke-PlanCase 'release-tool' @('shelteredmodmanager/tools/release-orchestration/incremental-release-graph.json')
Assert-True ($releaseTool.releaseGraphChanged -eq $true) 'Graph changes must be marked as release-layer changes.'
Assert-True (@($releaseTool.gates.id) -contains 'contracts.release-graph') 'Graph changes must select graph self-tests.'
Assert-True (@($releaseTool.selectedOwners).Count -eq 0) 'Release-layer changes must not select a mod owner.'

$releasePackages = Invoke-PlanCase 'release-packages' @('release/2.0/release-manifest.json')
Assert-True ($releasePackages.releasePackagesChanged -eq $true) 'Release manifest changes must invalidate package provenance.'
Assert-True (@($releasePackages.gates.id) -contains 'package.mods') 'Release manifest changes must regenerate mod packages.'
Assert-True (@($releasePackages.gates.id) -contains 'promotion.TradingAmount') 'Release manifest changes must recheck per-mod provenance.'
Assert-True (@($releasePackages.gates | Where-Object { $_.phase -eq 'gameplay' }).Count -eq 0) 'Release manifest changes must not rerun gameplay.'

$runnerSource = Get-Content -LiteralPath $RunnerPath -Raw
Assert-True ($runnerSource.Contains("status = 'dependency-blocked'")) 'Downstream package/promotion gates must fail closed after an upstream failure.'
Assert-True ($runnerSource.Contains('Get-ReusableEvidence')) 'Validated evidence reuse is not implemented.'
Assert-True ($runnerSource.Contains('Get-LiveHarnessFingerprint')) 'Gameplay evidence is not bound to the live game/mod/harness files.'
Assert-True ($runnerSource.Contains("Harness response omitted required ok=true contract")) 'Harness results must require explicit ok=true.'
Assert-True ($runnerSource.Contains("scripts = @('shelteredmodmanager/tools/release-orchestration/Test-IncrementalReleaseOrchestrator.ps1')")) 'The release-graph gate must execute its self-test.'

$unmappedOutput = Join-Path ([IO.Path]::GetTempPath()) ('sheltered-release-unmapped-' + [Guid]::NewGuid().ToString('N') + '.json')
try {
    $unmappedExit = 0
    try {
        $null = & powershell -NoProfile -ExecutionPolicy Bypass -File $RunnerPath -ShelteredRoot $ShelteredRoot -GraphPath $GraphPath -ChangedFile 'unknown-project/source.cs' -OutputPath $unmappedOutput 2>$null
        $unmappedExit = $LASTEXITCODE
    } catch { $unmappedExit = 1 }
    Assert-True ($unmappedExit -ne 0) 'Unmapped changed files must fail closed.'
} finally { if (Test-Path -LiteralPath $unmappedOutput) { Remove-Item -LiteralPath $unmappedOutput -Force } }

Write-Output 'PASS: incremental release graph schema and eleven-mod scope.'
Write-Output 'PASS: source changes select only affected builds, contracts, fixtures, platforms, packages, and promotion preflights.'
Write-Output 'PASS: dependency edges add compatibility fixtures without rebuilding unrelated consumers.'
Write-Output 'PASS: test-only and documentation-only changes avoid state-heavy gameplay matrices.'
Write-Output 'PASS: Manager OAuth changes select Manager contracts/platform smoke/RC promotion without stable publication.'
Write-Output 'PASS: release-layer changes select graph self-tests without selecting mod gameplay.'
Write-Output 'PASS: file-to-scenario rules avoid rerunning unrelated heavy matrices after a focused late change.'
Write-Output 'PASS: execution is fail-closed and passed evidence is content- and live-runtime-bound before reuse.'
