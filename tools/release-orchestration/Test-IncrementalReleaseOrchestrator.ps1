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
Assert-True (@($graph.scenarios | Where-Object { $_.id -eq 'four-person-expedition' }).Count -eq 1) 'Four Person must use the canonical transaction scenario ID.'
Assert-True (@($graph.scenarios | Where-Object { $_.id -eq 'expeditions' }).Count -eq 0) 'The retired expeditions scenario ID must not remain in the graph.'
Assert-True (@($graph.scenarios | Where-Object { $_.id -eq 'better-ai-queue-persistence' }).Count -eq 1) 'Better AI Queue must use its completion-backed persistence scenario ID.'
Assert-True (@($graph.scenarios | Where-Object { $_.id -eq 'ai-queue' }).Count -eq 0) 'The status-only Better AI Queue scenario must not remain in the graph.'
Assert-True (@($graph.scenarios | Where-Object { $_.id -eq 'expanded-map-generation' }).Count -eq 1) 'Expanded Map Sizes must use its completion-backed generation scenario ID.'
Assert-True (@($graph.scenarios | Where-Object { $_.id -eq 'expanded-map-sizes' }).Count -eq 0) 'The status-only Expanded Map Sizes scenario must not remain in the graph.'
Assert-True (@($graph.scenarios | Where-Object { $_.id -eq 'display-fixes-wardrobe' }).Count -eq 1) 'Display Fixes must use its completion-backed Wardrobe scenario ID.'
Assert-True (@($graph.scenarios | Where-Object { $_.id -eq 'display-fixes' }).Count -eq 0) 'The status-only Display Fixes scenario must not remain in the graph.'
Assert-True (@($graph.defaults.harnessSharedInputs).Count -gt 0) 'Shared harness route inputs must be explicit.'
foreach ($scenario in @($graph.scenarios | Where-Object { [string]$_.fixturePath -like '/release-scenario/*' })) {
    Assert-True (@($scenario.harnessInputs).Count -gt 0) "Completion-backed scenario $($scenario.id) must declare scoped harness inputs."
}
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
Assert-True (@($lifespan.gates.id) -contains 'gameplay.Steam.family-persistence') 'Lifespan source must select the dependent Family persistence transaction.'
Assert-True (@($lifespan.gates.id) -contains 'gameplay.Epic.family-persistence') 'Lifespan source must select the Epic dependent Family persistence transaction.'
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
Assert-True (@($trading.gates.id) -contains 'gameplay.Epic.trading-amount') 'Trading source must select the shared Epic Trading Amount transaction.'
Assert-True (-not (@($trading.gates.id) -contains 'gameplay.Epic.trading-vanilla-seam')) 'The retired Trading/Vanilla seam fixture must not be selected.'
Assert-True ((@($trading.gates | Where-Object { $_.id -eq 'package.mods' })).Count -eq 1) 'Multiple selected mod owners must share one packaging gate.'
Assert-True (-not (@($trading.gates.id) -contains 'gameplay.Steam.deep-progression')) 'Trading source must not select Deep Expansion progression.'
Assert-True ((@($graph.scenarios | Where-Object { $_.id -eq 'trading-amount' })[0].fixturePath) -eq '/release-scenario/interaction') 'Trading must use the completion-backed scenario transaction, not manual fixture choreography.'

foreach ($scenarioId in @('systems-water', 'systems-oxygen', 'deep-progression')) {
    $progression = @($graph.scenarios | Where-Object { [string]$_.id -eq $scenarioId })[0]
    Assert-True ($progression.fixturePath -eq '/release-scenario/progression') "$scenarioId must use the completion-backed progression route."
    Assert-True ($null -eq $progression.fixture) "$scenarioId must not carry the retired fixture selector."
    Assert-True (@($progression.steps).Count -eq 1) "$scenarioId must be one completion-backed call."
    Assert-True ([string]$progression.steps[0].scenario -eq $scenarioId) "$scenarioId must pass scenario=$scenarioId."
    Assert-True ([string]$progression.steps[0].argument -eq 'confirm=true') "$scenarioId must pass confirm=true."
}
foreach ($scenarioId in @('four-person-expedition')) {
    $expeditionScenario = @($graph.scenarios | Where-Object { [string]$_.id -eq $scenarioId })[0]
    Assert-True ($expeditionScenario.fixturePath -eq '/release-scenario/interaction') "$scenarioId must use the completion-backed interaction route."
    Assert-True (@($expeditionScenario.steps).Count -eq 1) "$scenarioId must be one completion-backed call."
    Assert-True ([string]$expeditionScenario.steps[0].scenario -eq $scenarioId) "$scenarioId must pass its canonical scenario ID."
    Assert-True ([string]$expeditionScenario.steps[0].argument -eq 'confirm=true') "$scenarioId must pass confirm=true."
}
foreach ($scenarioId in @('better-ai-queue-persistence', 'expanded-map-generation', 'display-fixes-wardrobe')) {
    $additionalScenario = @($graph.scenarios | Where-Object { [string]$_.id -eq $scenarioId })[0]
    Assert-True ($additionalScenario.fixturePath -eq '/release-scenario/interaction') "$scenarioId must use the completion-backed interaction route."
    Assert-True (@($additionalScenario.steps).Count -eq 1) "$scenarioId must be one completion-backed call."
    Assert-True ([string]$additionalScenario.steps[0].scenario -eq $scenarioId) "$scenarioId must pass its canonical scenario ID."
    Assert-True ([string]$additionalScenario.steps[0].argument -eq 'confirm=true') "$scenarioId must pass confirm=true."
}
foreach ($scenarioId in @('family-persistence', 'lifespan-persistence', 'bunker-persistence')) {
    $persistenceScenario = @($graph.scenarios | Where-Object { [string]$_.id -eq $scenarioId })[0]
    Assert-True ($persistenceScenario.fixturePath -eq '/release-scenario/interaction') "$scenarioId must use the completion-backed interaction route."
    Assert-True ($null -eq $persistenceScenario.fixture) "$scenarioId must not carry the retired family fixture selector."
    Assert-True (@($persistenceScenario.steps).Count -eq 1) "$scenarioId must be one completion-backed call."
    Assert-True ([string]$persistenceScenario.steps[0].scenario -eq $scenarioId) "$scenarioId must pass scenario=$scenarioId."
    Assert-True ([string]$persistenceScenario.steps[0].argument -eq 'confirm=true') "$scenarioId must pass confirm=true."
}
foreach ($scenarioId in @('vanilla-breach', 'vanilla-radio', 'vanilla-quest-weapons', 'vanilla-trading-slots', 'vanilla-weapon-craft', 'vanilla-recycling')) {
    $vanillaScenario = @($graph.scenarios | Where-Object { [string]$_.id -eq $scenarioId })[0]
    Assert-True ($vanillaScenario.fixturePath -eq '/release-scenario/interaction') "$scenarioId must use the completion-backed interaction route."
    Assert-True ($null -eq $vanillaScenario.fixture) "$scenarioId must not carry the retired fixture selector."
    Assert-True (@($vanillaScenario.steps).Count -eq 1) "$scenarioId must be one completion-backed call."
    Assert-True ([string]$vanillaScenario.steps[0].scenario -eq $scenarioId) "$scenarioId must pass scenario=$scenarioId."
    Assert-True ([string]$vanillaScenario.steps[0].argument -eq 'confirm=true') "$scenarioId must pass confirm=true."
}

$vanillaTrading = Invoke-PlanCase 'vanilla-trading-file' @('Sheltered-Vanilla-Fixes/Sheltered Vanilla Fixes/Patches/TradingWhiteSlotResetPatches.cs')
Assert-True (@($vanillaTrading.gates.id) -contains 'gameplay.Steam.vanilla-trading-slots') 'A trading-slot patch must select its focused behavior matrix.'
Assert-True (@($vanillaTrading.gates.id) -contains 'gameplay.Epic.trading-amount') 'A trading-slot patch must preserve the Trading Amount compatibility edge.'
Assert-True (-not (@($vanillaTrading.gates.id) -contains 'gameplay.Epic.trading-vanilla-seam')) 'A trading-slot patch must not select the retired seam fixture.'
Assert-True (-not (@($vanillaTrading.gates.id) -contains 'gameplay.Steam.vanilla-breach')) 'A trading-slot-only patch must not rerun the breach matrix.'
Assert-True (-not (@($vanillaTrading.gates.id) -contains 'gameplay.Steam.vanilla-recycling')) 'A trading-slot-only patch must not rerun the recycling matrix.'

$vanillaCore = Invoke-PlanCase 'vanilla-core-file' @('Sheltered-Vanilla-Fixes/Sheltered Vanilla Fixes/Core/FixBootstrapper.cs')
Assert-True (@($vanillaCore.gates.id) -contains 'gameplay.Steam.vanilla-breach') 'Shared Vanilla Fixes core changes must select breach.'
Assert-True (@($vanillaCore.gates.id) -contains 'gameplay.Steam.vanilla-recycling') 'Shared Vanilla Fixes core changes must select recycling.'

$family = Invoke-PlanCase 'family-source' @('Procreation-Framework/Procreation Framework/FamilyExpansionPlugin.cs')
Assert-True (@($family.gates.id) -contains 'gameplay.Steam.family-persistence') 'Family Expansion source must select its completion-backed persistence transaction.'
Assert-True (@($family.gates.id) -contains 'gameplay.Epic.family-persistence') 'Family Expansion source must select its Epic persistence transaction.'

$betterQueue = Invoke-PlanCase 'better-queue-source' @('Better-AI-Queue/Better AI Queue/Persistence/QueuePersistenceData.cs')
Assert-True (@($betterQueue.gates.id) -contains 'gameplay.Steam.better-ai-queue-persistence') 'Better AI Queue source must select the Steam completion-backed persistence transaction.'
Assert-True (@($betterQueue.gates.id) -contains 'gameplay.Epic.better-ai-queue-persistence') 'Better AI Queue source must select the Epic completion-backed persistence transaction.'
Assert-True (-not (@($betterQueue.gates.id) -contains 'gameplay.Steam.family-persistence')) 'Better AI Queue source must not select unrelated Family persistence.'

$expandedMap = Invoke-PlanCase 'expanded-map-source' @('Sheltered-Expanded-Map-Sizes/ExpandedMapSizes/MapSizePlugin.cs')
Assert-True (@($expandedMap.gates.id) -contains 'gameplay.Steam.expanded-map-generation') 'Expanded Map Sizes source must select the Steam completion-backed map-generation transaction.'
Assert-True (@($expandedMap.gates.id) -contains 'gameplay.Epic.expanded-map-generation') 'Expanded Map Sizes source must select the Epic completion-backed map-generation transaction.'
Assert-True (@($expandedMap.gates.id) -contains 'gameplay.Steam.bunker-persistence') 'Expanded Map Sizes source must select the dependent Bunker persistence transaction.'
Assert-True (@($expandedMap.gates.id) -contains 'gameplay.Epic.bunker-persistence') 'Expanded Map Sizes source must select the Epic dependent Bunker persistence transaction.'

$displayFixes = Invoke-PlanCase 'display-fixes-source' @('Sheltered-Display-Fixes/Sheltered Display Fixes/Patches/RenderTexturePatches.cs')
Assert-True (@($displayFixes.gates.id) -contains 'contracts.Sheltered-Display-Fixes') 'Display Fixes source must select its render-texture lifecycle contracts.'
Assert-True (@($displayFixes.gates.id) -contains 'gameplay.Steam.display-fixes-wardrobe') 'Display Fixes source must select the Steam completion-backed Wardrobe transaction.'
Assert-True (@($displayFixes.gates.id) -contains 'gameplay.Epic.display-fixes-wardrobe') 'Display Fixes source must select the Epic completion-backed Wardrobe transaction.'
Assert-True (-not (@($displayFixes.gates.id) -contains 'gameplay.Steam.expanded-map-generation')) 'Display Fixes source must not select unrelated map generation.'

$bunker = Invoke-PlanCase 'bunker-source' @('BunkerRandomLocation/BunkerRandomLocation/BunkerRandomLocationPlugin.cs')
Assert-True (@($bunker.gates.id) -contains 'gameplay.Steam.bunker-persistence') 'Bunker source must select its completion-backed persistence transaction.'
Assert-True (-not (@($bunker.gates.id) -contains 'gameplay.Steam.family-persistence')) 'Bunker source must not select Family persistence.'

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
Assert-True ($runnerSource.Contains('Get-RepositoryDefinitionsForGate')) 'Fingerprint scope must use the shared repository/dependency definition selector.'
Assert-True ($runnerSource.Contains('Get-RelevantGraphSlice')) 'Gate fingerprints must use a relevant graph slice.'
Assert-True ($runnerSource.Contains('runtimeSharedModDirectories')) 'Runtime fingerprints must include shared framework mod directories.'
Assert-True ($runnerSource.Contains('harnessInputs')) 'Scenario fingerprints must include declared harness inputs.'
Assert-True ($runnerSource.Contains('runtimeModDirectories')) 'Runtime fingerprints must include selected repository mod directories.'
Assert-True (-not $runnerSource.Contains('Sheltered Agent Interface.dll')) 'Fingerprints must not hash the entire harness DLL.'
Assert-True (-not $runnerSource.Contains('Sheltered Agent Interface.pdb')) 'Fingerprints must not hash the entire harness PDB.'
Assert-True (-not $runnerSource.Contains("Get-ChildItem -LiteralPath `$modsRoot -Recurse")) 'Fingerprints must not hash every installed mod.'
Assert-True (([regex]::Matches($runnerSource, [regex]::Escape("Get-FileHash -LiteralPath `$GraphPath"))).Count -eq 1) 'Whole-graph hashing must be limited to the release-graph validation gate.'
Assert-True ($runnerSource.Contains("Harness response omitted required ok=true contract")) 'Harness results must require explicit ok=true.'
Assert-True ($runnerSource.Contains('[string]$HarnessRepo')) 'Automatic transaction mode must expose the harness repository parameter.'
Assert-True ($runnerSource.Contains('[string]$SteamGameRoot')) 'Automatic transaction mode must expose the Steam game-root parameter.'
Assert-True ($runnerSource.Contains('[string]$EpicGameRoot')) 'Automatic transaction mode must expose the Epic game-root parameter.'
Assert-True ($runnerSource.Contains('Get-AutomaticHarnessRepo')) 'Automatic transaction mode must discover a safe harness repository default.'
Assert-True ($runnerSource.Contains('Get-AutomaticGameRoot')) 'Automatic transaction mode must discover safe Steam/Epic game-root defaults.'
Assert-True ($runnerSource.Contains('Invoke-AutomaticTransaction')) 'Gameplay gates must invoke the transactional runner when no live URL is supplied.'
Assert-True ($runnerSource.Contains('Invoke-TransactionalReleaseScenario.ps1')) 'Automatic mode must target the transactional release runner.'
Assert-True ($runnerSource.Contains('Assert-TransactionReport')) 'Automatic mode must validate transaction-report.json evidence.'
Assert-True ($runnerSource.Contains('restoration evidence is invalid')) 'Automatic mode must fail closed when restoration evidence is invalid.'
Assert-True ($runnerSource.Contains('transactionReportPath')) 'Automatic mode must retain the validated transaction report path for evidence reuse.'
Assert-True ($runnerSource.Contains('platform smoke requires -SteamHarnessUrl/-EpicHarnessUrl')) 'Non-completion platform smoke must remain explicitly URL-backed.'
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
