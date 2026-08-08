#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'Invoke-ShelteredAgentStress.ps1'
$runnerPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'performance\ShelteredBenchmark.Runner.psm1'
$configPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'performance\benchmark.config.example.json'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
$source = Get-Content -LiteralPath $scriptPath -Raw
$runnerSource = Get-Content -LiteralPath $runnerPath -Raw
$configSource = Get-Content -LiteralPath $configPath -Raw
$config = $configSource | ConvertFrom-Json
$failures = New-Object 'System.Collections.Generic.List[string]'

function Invoke-Contract {
    param([Parameter(Mandatory = $true)][string]$Name, [Parameter(Mandatory = $true)][scriptblock]$Check)
    try {
        & $Check
        Write-Host "PASS $Name"
    }
    catch {
        $failures.Add("$Name`: $($_.Exception.Message)")
        Write-Host "FAIL $Name"
    }
}

function Assert-Contract {
    param([Parameter(Mandatory = $true)][bool]$Condition, [Parameter(Mandatory = $true)][string]$Message)
    if (-not $Condition) { throw $Message }
}

Invoke-Contract 'script parses without syntax errors' {
    $parseMessage = if ($parseErrors.Count -eq 0) { 'PowerShell parser reported an error.' } else { ($parseErrors | ForEach-Object Message) -join '; ' }
    Assert-Contract ($parseErrors.Count -eq 0) $parseMessage
    Assert-Contract ($null -ne $ast) 'PowerShell parser returned no AST.'
}

Invoke-Contract 'dual storefront and supported-mod defaults are preserved' {
    Assert-Contract ($source -match "\[string\[\]\]\`$Platform = @\('steam', 'epic'\)") 'Default platforms are no longer Steam and Epic.'
    Assert-Contract ($source -match "\[string\]\`$Profile = 'all-supported-mods'") 'Default profile is no longer all-supported-mods.'
}

Invoke-Contract 'isolated stability sessions cannot auto-load a user save' {
    Assert-Contract ($runnerSource -match "if \(\`$harnessEnabled\)[\s\S]*AutoLoadSaveSlot=0") 'Shared harness sessions do not disable manager-driven save auto-load after snapshotting and before launch.'
}

Invoke-Contract 'default stress profile deploys and enables the standalone editor' {
    $retiredEditorOptionId = @('ShelteredAPI', 'PatchCustomScenarioEditor') -join '.'
    Assert-Contract (-not $configSource.Contains($retiredEditorOptionId)) 'Shared stress configuration retains the retired API-owned editor option.'
    $profile = $config.profiles | Where-Object name -EQ 'all-supported-mods' | Select-Object -First 1
    Assert-Contract ($null -ne $profile) 'Shared configuration no longer defines the default stability profile.'
    Assert-Contract ([bool]$profile.managerOptions.'ShelteredScenarioEditor.Enabled') 'Default stability profile does not enable the standalone editor.'
    foreach ($platform in @($config.platforms)) {
        $editorGates = @($platform.hashGates | Where-Object role -EQ 'scenarioeditor')
        Assert-Contract ($editorGates.Count -eq 1) "Platform '$($platform.name)' does not hash-gate exactly one editor DLL."
        Assert-Contract ([string]$editorGates[0].deployedPath -eq 'SMM\bin\ShelteredScenarioEditor.dll') "Platform '$($platform.name)' editor hash gate targets the wrong file."
    }
}

Invoke-Contract 'stress defaults retain UI spawn soak and restart pressure' {
    foreach ($expected in @('DurationMinutes = 10', 'RapidUiActions = 150', 'SpawnAttempts = 100', 'RestartEveryMinutes = 3')) {
        Assert-Contract ($source.Contains($expected)) "Missing stress default '$expected'."
    }
}

Invoke-Contract 'owned-process cleanup checks PID identity' {
    Assert-Contract ($runnerSource -match 'actualStart[\s\S]*ExpectedStartUtc[\s\S]*Refusing to stop reused PID') 'Process cleanup no longer validates PID start time.'
}

Invoke-Contract 'stress uses shared lifecycle without serializing storefront startup' {
    Assert-Contract ($source -notmatch '\bStart-Process\b') 'Stress reintroduced a private process-launch path.'
    Assert-Contract ($source -match 'foreach \(\$platformConfig in \$platforms\)[\s\S]*Start-ShelteredPlatformSession[\s\S]*foreach \(\$context in \$contexts\)[\s\S]*Wait-ShelteredPlatformSessionReady') 'Steam and Epic must both start before readiness waits begin.'
    Assert-Contract ($source -match 'Stop-ShelteredPlatformSession[\s\S]*Restore-StabilityPathSnapshot[\s\S]*Restore-ShelteredPlatformSession') 'Shared process stop, mutable-state restore, and install restore ordering drifted.'
    Assert-Contract ($source -match 'if \(\$null -eq \$context -or -not \[bool\]\$context\.ProcessStopped\)[\s\S]*current state and evidence were preserved[\s\S]*continue[\s\S]*Restore-StabilityPathSnapshot') 'Mutable paths can be restored without a positively stopped platform session.'
    Assert-Contract ($source -match 'Start-ShelteredPlatformSession[\s\S]*-InstallLocks \$locks') 'Stress does not pass its acquired install-lock ownership set into the shared session.'
}

Invoke-Contract 'session DTO and snapshots expose one canonical field per concept' {
    Assert-Contract ($runnerSource -notmatch 'Name = \$platformName; PlatformName =') 'Session still duplicates Name and PlatformName.'
    Assert-Contract ($runnerSource -notmatch 'ArtifactRoot = \$ArtifactRoot; PlatformRoot =') 'Session still duplicates ArtifactRoot and PlatformRoot.'
    Assert-Contract ($source -notmatch '\bKind = \$kind\b') 'Stability snapshot still emits unused Kind metadata.'
    Assert-Contract ($runnerSource -notmatch 'function Copy-BenchmarkRuntimeLog') 'One-call runtime-log wrapper still exists.'
}

Invoke-Contract 'mutable game state is snapshotted and restored' {
    foreach ($path in @('vanilla-saves', 'modapi-saves', 'modapi-user-saves', 'modapi-user-scenarios', 'modapi-user-authoring-exports', 'modapi-save-backups', 'cortex-data', 'ScenarioAuthoringDrafts', 'ScenarioAuthoringExports', "'Scenarios'")) {
        Assert-Contract ($source.Contains($path)) "Missing mutable-state boundary '$path'."
    }
    Assert-Contract ($source -match 'Restore-StabilityPathSnapshot[\s\S]*restore-verification\.json') 'Restoration is no longer verified and persisted.'
}

Invoke-Contract 'all editor stages and responsive layout sizes are evidence-backed' {
    foreach ($stage in @('Bunker', 'BunkerBackground', 'BunkerInside', 'InventoryStorage', 'People', 'Events', 'Quests', 'Map', 'Test', 'Publish', 'Assets')) {
        Assert-Contract ($source.Contains("'$stage'")) "Missing editor stage '$stage'."
    }
    foreach ($size in @('1280; Height = 720', '1600; Height = 900', '1920; Height = 1080')) {
        Assert-Contract ($source.Contains($size)) "Missing responsive layout size '$size'."
    }
    foreach ($evidence in @('editor-layout', 'editor-stage-layout-coverage.csv', '/authoring/shell', '/actions/catalog')) {
        Assert-Contract ($source.Contains($evidence)) "Missing editor layout evidence '$evidence'."
    }
    Assert-Contract ($source -match 'finally\s*\{[\s\S]*OriginalScreenWidth[\s\S]*OriginalScreenHeight') 'Original resolution is not restored in a finally block.'
}

Invoke-Contract 'long metadata rapid edits and all rendered action catalogs are exercised' {
    Assert-Contract ($source.Contains("'shell.window.toggle.scenario'")) 'Stress does not open the rendered Home workspace before submitting Home-owned editable fields.'
    Assert-Contract ($source.Contains("'stage.select.Test'")) 'Stress does not open the rendered Test workspace before submitting checklist commands.'
    foreach ($field in @('editor.draft.title.', 'editor.draft.description.', 'editor.draft.tags.', 'testchecklist.note.verified_required_mods.')) {
        Assert-Contract ($source.Contains($field)) "Missing long-field edit '$field'."
    }
    Assert-Contract ($source.Contains('testchecklist.toggle.verified_required_mods')) 'The required-mod note is no longer explicitly checked before export.'
    Assert-Contract ($source -match "Item\[@id='verified_required_mods' and @checked='True'\]") 'The persisted required-mod checklist completion is not verified.'
    Assert-Contract ($source.Contains('rapid-editor-actions.csv')) 'Rapid editor action evidence is missing.'
    Assert-Contract ($source -match 'for \(\$i = 0; \$i -lt \$RapidUiActions; \$i\+\+\)[\s\S]*stage\.select') 'Rapid editor actions no longer pressure stage selection.'
}

Invoke-Contract 'scenario and editor sidecars remain transactional pairs' {
    foreach ($token in @('*.editor.xml', '*.pairpending-*', 'editor.history.save_version', 'editor.history.restore.*', 'editor.history.confirm_restore', 'snapshot-pairs.json')) {
        Assert-Contract ($source.Contains($token)) "Missing snapshot-pair proof '$token'."
    }
    Assert-Contract ($source.Contains('function Open-StabilityDraftHistory') -and [Text.RegularExpressions.Regex]::Matches($source, 'Open-StabilityDraftHistory \$context').Count -ge 2) 'Draft history must be entered through its rendered global-search command before save and restore.'
    Assert-Contract ($source -match 'NamedVersions -lt 1[\s\S]*Autosaves -lt 1') 'History restore no longer requires both a named version and an autosave.'
    Assert-Contract ($source -match 'Published package leaked editor sidecars or pair-pending files') 'Export package no longer rejects editor sidecars.'
}

Invoke-Contract 'playtest export import duplicate and close lifecycle is explicit' {
    Assert-Contract ($source -match "stage\.select\.Test'[\s\S]*editor\.playtest\.toggle") 'Playtest must be started from its rendered Test workspace.'
    foreach ($action in @('editor.playtest.toggle', 'editor.playtest.restart', 'publish.export', 'publish.export.install', 'publish.export.install_confirm', 'publish.export.uninstall', 'editor.close')) {
        Assert-Contract ($source.Contains($action)) "Missing lifecycle action '$action'."
    }
    foreach ($route in @('/flow/custom-draft')) {
        Assert-Contract ($source.Contains($route)) "Missing disposable lifecycle route '$route'."
    }
    Assert-Contract ($source -notmatch "'/library'[\s\S]*action = 'uninstall'") 'Package uninstall regressed into the runtime browser lane.'
    Assert-Contract ($source -notmatch '/scenario-book/(duplicate|delete)') 'Draft lifecycle regressed into the installed-scenario browser lane.'
    Assert-Contract ($source -match "action = 'create'; template = 'small-survival-challenge'[\s\S]*action = 'status'[\s\S]*draftReady\.completed[\s\S]*draftReady\.running") 'The stress runner must wait for a known playable starter-template flow to complete before authoring or playtest actions.'
    Assert-Contract ($source -match 'isPlaytesting[\s\S]*playtest-stop\.json[\s\S]*isActive -and -not \[bool\]\$state\.isPlaytesting') 'Playtest stop/dispose is not authoritatively verified.'
    Assert-Contract ($source -match 'editor\.close[\s\S]*editor-state-closed\.json[\s\S]*/scenario-book/open') 'Editor close is not verified before reopening the book.'
}

Invoke-Contract 'stock unlimited and modded save lanes are isolated' {
    Assert-Contract ($source.Contains('/scenario-save-lanes')) 'Harness save-lane diagnostic is missing.'
    Assert-Contract ($source.Contains('StockVanillaFilesUnchanged')) 'Physical stock vanilla save manifest is not compared around the lane probe.'
    Assert-Contract ($source.Contains('scenario-save-lane-evidence.json')) 'Save-lane evidence artifact is missing.'
}

Invoke-Contract 'agentic pressure covers navigation authoring and live playtest' {
    foreach ($route in @('/scenario-selection/open', '/scenario-book/open', '/scenario-book/inspect', '/flow/custom-draft', '/authoring/action', '/game/spawn-object', '/game/family-move')) {
        Assert-Contract ($source.Contains($route)) "Missing stress route '$route'."
    }
    foreach ($action in @('editor.playtest.toggle', 'editor.playtest.restart', 'editor.save')) {
        Assert-Contract ($source.Contains($action)) "Missing authoring action '$action'."
    }
}

Invoke-Contract 'health and resource evidence is retained' {
    foreach ($artifact in @('observations.csv', 'process-samples.csv', 'metrics.json', 'health-start.json', 'health-final.json', 'events.jsonl')) {
        Assert-Contract ($source.Contains($artifact)) "Missing evidence artifact '$artifact'."
    }
}

Invoke-Contract 'final screenshot failures fail the campaign' {
    Assert-Contract ($source -match 'final\.png[\s\S]*final screenshot') 'Final screenshot failures are no longer surfaced by the campaign.'
}

Invoke-Contract 'stress evidence uses foreground-free harness capture' {
    $harnessSource = Get-Content (Join-Path $PSScriptRoot '..\performance\ShelteredBenchmark.Harness.psm1') -Raw
    $captureStart = $harnessSource.IndexOf('function Save-ShelteredHarnessScreenshot')
    $captureEnd = $harnessSource.IndexOf('function Get-SmoothFpsSummary', $captureStart)
    Assert-Contract ($captureStart -ge 0 -and $captureEnd -gt $captureStart) 'Screenshot helper boundaries could not be inspected.'
    $captureSource = $harnessSource.Substring($captureStart, $captureEnd - $captureStart)
    Assert-Contract ($captureSource -match "activate\s*=\s*'false'") 'Stress screenshots no longer explicitly prevent foreground activation.'
    Assert-Contract ($captureSource -notmatch "activate\s*=\s*'true'") 'Stress screenshots can steal desktop focus again.'
}

Invoke-Contract 'open sort dropdown is captured for layering review' {
    Assert-Contract ($source -match "sort-open[\s\S]*scenario-book-sort-open\.png") 'The campaign no longer captures the open scenario-book sort menu.'
}

Invoke-Contract 'plugin load errors fail the campaign' {
    Assert-Contract ($source.Contains("'\[PLUGIN-ERROR\]'") -and $source.Contains('plugin load error(s).')) 'Plugin errors no longer fail the campaign.'
}

if ($failures.Count -gt 0) {
    throw "$($failures.Count) stability contract(s) failed: $($failures -join ' | ')"
}
Write-Host 'All Sheltered agent stress contracts passed.'
