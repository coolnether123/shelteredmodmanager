#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$ConfigPath = '',
    [string[]]$Platform = @('steam', 'epic'),
    [string]$Profile = 'all-supported-mods',
    [ValidateRange(1, 1440)][int]$DurationMinutes = 10,
    [ValidateRange(1, 10000)][int]$RapidUiActions = 150,
    [ValidateRange(0, 5000)][int]$SpawnAttempts = 100,
    [ValidateRange(1, 100)][int]$SimulationScale = 12,
    [ValidateRange(0, 1440)][int]$RestartEveryMinutes = 3,
    [switch]$SkipBuild,
    [string]$RunLabel = 'agent-stress'
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..\..'))
$performanceRoot = Join-Path $repositoryRoot 'tools\performance'
if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $performanceRoot 'benchmark.config.example.json'
}

Import-Module (Join-Path $performanceRoot 'ShelteredBenchmark.Runner.psm1') -Force -DisableNameChecking
Import-Module (Join-Path $performanceRoot 'ShelteredBenchmark.Core.psm1') -Force -DisableNameChecking
Import-Module (Join-Path $performanceRoot 'ShelteredBenchmark.Harness.psm1') -Force -DisableNameChecking

function Write-StabilityJson {
    param([Parameter(Mandatory = $true)]$Value, [Parameter(Mandatory = $true)][string]$Path)
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $Value | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Invoke-StabilityHarness {
    param(
        [Parameter(Mandatory = $true)]$Context,
        [Parameter(Mandatory = $true)][string]$Route,
        [hashtable]$Query = @{},
        [int]$TimeoutSeconds = 30
    )
    return Invoke-ShelteredHarnessRequest -Port $Context.Port -Route $Route -Query $Query -TimeoutSeconds $TimeoutSeconds
}

function Get-StabilityPathManifest {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    $resolved = [IO.Path]::GetFullPath($Path)
    if (Test-Path -LiteralPath $resolved -PathType Leaf) {
        return @([pscustomobject]@{ RelativePath = '.'; Sha256 = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash })
    }
    return @(Get-ChildItem -LiteralPath $resolved -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{
            RelativePath = $_.FullName.Substring($resolved.TrimEnd('\').Length).TrimStart('\')
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
}

function New-StabilityPathSnapshot {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$InstallRoot,
        [Parameter(Mandatory = $true)][string]$BackupRoot,
        [Parameter(Mandatory = $true)][string]$Name
    )
    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullInstallRoot = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($fullInstallRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Snapshot target '$fullPath' is outside install '$fullInstallRoot'."
    }
    $backupPath = Join-Path $BackupRoot $Name
    $exists = Test-Path -LiteralPath $fullPath
    if ($exists) {
        $parent = Split-Path -Parent $backupPath
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        Copy-Item -LiteralPath $fullPath -Destination $backupPath -Recurse -Force
    }
    return [pscustomobject]@{
        Path = $fullPath; InstallRoot = $fullInstallRoot.TrimEnd('\'); BackupPath = $backupPath
        Existed = $exists; Name = $Name
        OriginalManifest = @(Get-StabilityPathManifest -Path $fullPath)
    }
}

function Restore-StabilityPathSnapshot {
    param([Parameter(Mandatory = $true)]$Snapshot, [Parameter(Mandatory = $true)][string]$MutationArchiveRoot)
    $target = [IO.Path]::GetFullPath([string]$Snapshot.Path)
    $installPrefix = [IO.Path]::GetFullPath([string]$Snapshot.InstallRoot).TrimEnd('\') + '\'
    if (-not $target.StartsWith($installPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Restore target '$target' is outside install '$installPrefix'."
    }
    if (Test-Path -LiteralPath $target) {
        if (-not (Test-Path -LiteralPath $MutationArchiveRoot)) { New-Item -ItemType Directory -Path $MutationArchiveRoot -Force | Out-Null }
        $changedPath = Join-Path $MutationArchiveRoot ([string]$Snapshot.Name)
        Move-Item -LiteralPath $target -Destination $changedPath -Force
    }
    if ([bool]$Snapshot.Existed) {
        $parent = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        Copy-Item -LiteralPath ([string]$Snapshot.BackupPath) -Destination $target -Recurse -Force
    }
}

function Add-StabilityObservation {
    param(
        [Parameter(Mandatory = $true)]$Rows,
        [Parameter(Mandatory = $true)]$Context,
        [Parameter(Mandatory = $true)][string]$Phase,
        $Snapshot,
        $Pump,
        $Events
    )
    $process = Get-Process -Id $Context.Process.Id -ErrorAction SilentlyContinue
    $Rows.Add([pscustomobject]@{
        TimestampUtc = [DateTimeOffset]::UtcNow.ToString('o')
        Platform = $Context.PlatformName
        Phase = $Phase
        Alive = $null -ne $process
        Responding = if ($null -ne $process) { $process.Responding } else { $false }
        CpuSeconds = if ($null -ne $process) { [math]::Round($process.CPU, 3) } else { $null }
        WorkingSetMiB = if ($null -ne $process) { [math]::Round($process.WorkingSet64 / 1MB, 2) } else { $null }
        PrivateMiB = if ($null -ne $process) { [math]::Round($process.PrivateMemorySize64 / 1MB, 2) } else { $null }
        Threads = if ($null -ne $process) { $process.Threads.Count } else { $null }
        Handles = if ($null -ne $process) { $process.HandleCount } else { $null }
        Scene = if ($null -ne $Snapshot) { [string]$Snapshot.scene } else { '' }
        PumpOk = if ($null -ne $Pump) { [bool]$Pump.ok } else { $false }
        EventCount = if ($null -ne $Events -and $null -ne $Events.events) { @($Events.events).Count } else { 0 }
        CursorExpired = if ($null -ne $Events) { [bool]$Events.cursorExpired } else { $false }
    })
}

function Assert-StabilityResponse {
    param(
        [Parameter(Mandatory = $true)]$Response,
        [Parameter(Mandatory = $true)][string]$Description
    )
    if ($null -eq $Response -or -not [bool]$Response.ok) {
        throw "$Description failed: $($Response | ConvertTo-Json -Compress -Depth 12)"
    }
    if ($Response.PSObject.Properties.Name -contains 'result' -and -not [bool]$Response.result) {
        throw "$Description was rejected: $([string]$Response.reason) $([string]$Response.statusMessage)"
    }
}

function Invoke-StabilityAuthoringAction {
    param(
        [Parameter(Mandatory = $true)]$Context,
        [Parameter(Mandatory = $true)][string]$ActionId,
        [int]$TimeoutSeconds = 60
    )
    $response = Invoke-StabilityHarness $Context '/authoring/action' @{ id = $ActionId } $TimeoutSeconds
    Assert-StabilityResponse -Response $response -Description "Authoring action '$ActionId'"
    return $response
}

function Open-StabilityDraftHistory {
    param([Parameter(Mandatory = $true)]$Context)
    $openWindowMenu = Invoke-StabilityAuthoringAction $Context 'shell.menu.windows'
    $showHistory = Invoke-StabilityAuthoringAction $Context 'editor.history.show'
    return [pscustomobject]@{ OpenWindowMenu = $openWindowMenu; ShowHistory = $showHistory }
}

function Wait-StabilityAuthoringState {
    param(
        [Parameter(Mandatory = $true)]$Context,
        [Parameter(Mandatory = $true)][scriptblock]$Predicate,
        [int]$TimeoutSeconds = 180
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            $response = Invoke-StabilityHarness $Context '/authoring/state' @{} 15
            if (& $Predicate $response.state) { return $response }
        }
        catch { }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Authoring state did not reach the requested condition within $TimeoutSeconds seconds."
}

function Test-StabilitySnapshotPairs {
    param([Parameter(Mandatory = $true)][string]$DraftScenarioPath)
    $draftRoot = Split-Path -Parent $DraftScenarioPath
    $historyRoot = Join-Path $draftRoot '.history'
    $pairRoots = @('autosaves', 'versions') | ForEach-Object { Join-Path $historyRoot $_ }
    $scenarioFiles = @($pairRoots | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object {
        Get-ChildItem -LiteralPath $_ -File -Filter '*.xml' -Recurse | Where-Object { $_.Name -notlike '*.editor.xml' }
    })
    $missing = New-Object 'System.Collections.Generic.List[string]'
    foreach ($scenarioFile in $scenarioFiles) {
        $sidecar = Join-Path $scenarioFile.DirectoryName ($scenarioFile.BaseName + '.editor.xml')
        if (-not (Test-Path -LiteralPath $sidecar -PathType Leaf)) { $missing.Add($sidecar) }
    }
    $pending = @($pairRoots | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object {
        Get-ChildItem -LiteralPath $_ -File -Recurse | Where-Object { $_.Name -like '*.pairpending-*' }
    })
    return [pscustomobject]@{
        DraftRoot = $draftRoot
        ScenarioFiles = $scenarioFiles.Count
        MissingSidecars = $missing.ToArray()
        PendingPairFiles = @($pending | ForEach-Object FullName)
        NamedVersions = @($scenarioFiles | Where-Object { $_.FullName -like '*\versions\*' }).Count
        Autosaves = @($scenarioFiles | Where-Object { $_.FullName -like '*\autosaves\*' }).Count
        Ok = $missing.Count -eq 0 -and $pending.Count -eq 0
    }
}

$config = Import-ShelteredBenchmarkConfig -Path $ConfigPath
$validation = Test-ShelteredBenchmarkConfig -Config $config
if (-not $validation.Valid) { throw "Invalid benchmark configuration: $($validation.Errors -join '; ')" }
$selectedNames = @($Platform | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$platforms = @($config.platforms | Where-Object { $selectedNames -contains [string]$_.name })
if ($platforms.Count -ne $selectedNames.Count) { throw 'One or more requested platforms are unknown or disabled.' }
$profileConfig = $config.profiles | Where-Object name -IEQ $Profile | Select-Object -First 1
if ($null -eq $profileConfig) { throw "Profile '$Profile' was not found." }
if (-not [bool](Get-ObjectPropertyValue $profileConfig 'harness' $false)) { throw "Profile '$Profile' must enable the harness." }

$stamp = [DateTime]::Now.ToString('yyyy-MM-dd_HHmmss')
$safeLabel = $RunLabel -replace '[^A-Za-z0-9_.-]', '_'
$outputRoot = Resolve-ConfiguredPath -Path ([string]$config.outputRoot) -BasePath ([string]$config._configRoot)
$runRoot = Join-Path $outputRoot ($stamp + '_' + $safeLabel)
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$config | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath (Join-Path $runRoot 'stability.config.json') -Encoding UTF8

$contexts = New-Object 'System.Collections.Generic.List[object]'
$pathSnapshots = New-Object 'System.Collections.Generic.List[object]'
$observations = New-Object 'System.Collections.Generic.List[object]'
$failures = New-Object 'System.Collections.Generic.List[string]'
$restoreVerification = New-Object 'System.Collections.Generic.List[object]'
$editorStageCoverage = New-Object 'System.Collections.Generic.List[object]'
$saveLaneEvidence = New-Object 'System.Collections.Generic.List[object]'
$locks = @()

try {
    if (-not $SkipBuild -and $null -ne $config.build) {
        [void](Invoke-BenchmarkCommand -Command $config.build -ConfigRoot ([string]$config._configRoot) `
            -DefaultWorkingDirectory $repositoryRoot -LogPath (Join-Path $runRoot 'build.log'))
    }
    $locks = @(Enter-BenchmarkInstallLocks -InstallRoots @($platforms | ForEach-Object { [string]$_.installRoot }))

    foreach ($platformConfig in $platforms) {
        $name = [string]$platformConfig.name
        $installRoot = [IO.Path]::GetFullPath([string]$platformConfig.installRoot)
        $platformRoot = Join-Path $runRoot $name
        New-Item -ItemType Directory -Path $platformRoot -Force | Out-Null
        $saveBackupRoot = Join-Path $platformRoot 'mutable-state-before'
        $mutablePaths = @(
            @{ Path = (Join-Path $installRoot 'saves'); Name = 'vanilla-saves' },
            @{ Path = (Join-Path $installRoot 'mods\ModAPI\Saves'); Name = 'modapi-saves' },
            @{ Path = (Join-Path $installRoot 'mods\ModAPI\User\Saves'); Name = 'modapi-user-saves' },
            @{ Path = (Join-Path $installRoot 'mods\ModAPI\User\Scenarios'); Name = 'modapi-user-scenarios' },
            @{ Path = (Join-Path $installRoot 'mods\ModAPI\User\ScenarioAuthoringExports'); Name = 'modapi-user-authoring-exports' },
            @{ Path = (Join-Path $installRoot 'mods\ModAPI\Backups\Saves'); Name = 'modapi-save-backups' },
            @{ Path = (Join-Path $installRoot 'mods\Cortex\Data'); Name = 'cortex-data' }
        )
        $modsRoot = Join-Path $installRoot 'mods'
        if (Test-Path -LiteralPath $modsRoot -PathType Container) {
            $modIndex = 0
            foreach ($modDirectory in @(Get-ChildItem -LiteralPath $modsRoot -Directory | Sort-Object Name)) {
                $modIndex++
                $safeModName = $modDirectory.Name -replace '[^A-Za-z0-9_.-]', '_'
                $snapshotPrefix = 'mod-{0:D3}-{1}' -f $modIndex, $safeModName
                $mutablePaths += @(
                    @{ Path = (Join-Path $modDirectory.FullName 'ScenarioAuthoringDrafts'); Name = "$snapshotPrefix-authoring-drafts" },
                    @{ Path = (Join-Path $modDirectory.FullName 'ScenarioAuthoringExports'); Name = "$snapshotPrefix-authoring-exports" },
                    @{ Path = (Join-Path $modDirectory.FullName 'Scenarios'); Name = "$snapshotPrefix-scenarios" }
                )
            }
        }
        foreach ($item in $mutablePaths) {
            $pathSnapshots.Add((New-StabilityPathSnapshot -Path $item.Path -InstallRoot $installRoot -BackupRoot $saveBackupRoot -Name $item.Name))
        }

        $existingState = Get-LoadOrderState -InstallRoot $installRoot
        $existingEnabled = @(Get-EnabledLoadOrderIds $existingState)
        $session = Start-ShelteredPlatformSession -Platform $platformConfig -Profile $profileConfig -Config $config `
            -StateRoot (Join-Path $platformRoot 'install-state-before') -ArtifactRoot $platformRoot `
            -LeaseOwner "stability-$stamp-$name" -InstallLocks $locks `
            -ProcessIntervalMilliseconds 250 -ExistingEnabledIds $existingEnabled `
            -DeploymentHashFileName 'deployment-hashes.json'
        $contexts.Add($session)
    }

    foreach ($context in $contexts) {
        [void](Wait-ShelteredPlatformSessionReady -Session $context -TimeoutSeconds 180 -MenuBlockersFileName 'menu-blockers.json')
        Write-StabilityJson (Invoke-StabilityHarness $context '/tools') (Join-Path $context.ArtifactRoot 'tools.json')
        Write-StabilityJson (Invoke-StabilityHarness $context '/state/health') (Join-Path $context.ArtifactRoot 'health-start.json')
        Write-StabilityJson ([pscustomobject]@{ SelectedModIds = $context.SelectedModIds }) (Join-Path $context.ArtifactRoot 'selected-mods.json')
        try { [void](Save-ShelteredHarnessScreenshot -Port $context.Port -Path (Join-Path $context.ArtifactRoot 'menu.png') -Mode client) } catch { }
    }

    foreach ($context in $contexts) {
        $context | Add-Member -NotePropertyName ScenarioBookSetupSucceeded -NotePropertyValue $false -Force
        try {
            $selection = Invoke-StabilityHarness $context '/scenario-selection/open' @{} 180
            Write-StabilityJson $selection (Join-Path $context.ArtifactRoot 'scenario-selection.json')
            Assert-StabilityResponse $selection 'Open vanilla scenario selection'
            $book = Invoke-StabilityHarness $context '/scenario-book/open' @{} 180
            Write-StabilityJson $book (Join-Path $context.ArtifactRoot 'scenario-book.json')
            Assert-StabilityResponse $book 'Open custom scenario book'
            $context.ScenarioBookSetupSucceeded = $true
        }
        catch {
            $failures.Add("$($context.PlatformName) scenario-book setup: $($_.Exception.Message)")
        }
    }

    $uiResults = New-Object 'System.Collections.Generic.List[object]'
    for ($i = 0; $i -lt $RapidUiActions; $i++) {
        foreach ($context in $contexts) {
            if (-not [bool]$context.ScenarioBookSetupSucceeded) {
                continue
            }
            try {
                $action = switch ($i % 5) {
                    0 { @{ action = 'sort-open'; value = '' } }
                    1 { @{ action = 'sort-close'; value = '' } }
                    2 { @{ action = 'search-set'; value = ('stress-' + ($i % 17)) } }
                    3 { @{ action = 'search-blur'; value = '' } }
                    default { @{ action = 'search-set'; value = '' } }
                }
                $response = Invoke-StabilityHarness $context '/scenario-book/inspect' $action 15
                if ($action.action -eq 'sort-open') {
                    [void](Save-ShelteredHarnessScreenshot -Port $context.Port -Path (Join-Path $context.ArtifactRoot 'scenario-book-sort-open.png') -Mode client)
                }
                $uiResults.Add([pscustomobject]@{ Platform = $context.PlatformName; Iteration = $i; Action = $action.action; Ok = [bool]$response.ok; Error = '' })
            }
            catch {
                $uiResults.Add([pscustomobject]@{ Platform = $context.PlatformName; Iteration = $i; Action = $action.action; Ok = $false; Error = $_.Exception.Message })
            }
        }
    }
    $uiResults | Export-Csv -LiteralPath (Join-Path $runRoot 'rapid-ui-actions.csv') -NoTypeInformation -Encoding UTF8

    $editorStages = @('Bunker', 'BunkerBackground', 'BunkerInside', 'InventoryStorage', 'People', 'Events', 'Quests', 'Map', 'Test', 'Publish', 'Assets')
    $layoutResolutions = @(
        [pscustomobject]@{ Width = 1280; Height = 720 },
        [pscustomobject]@{ Width = 1600; Height = 900 },
        [pscustomobject]@{ Width = 1920; Height = 1080 }
    )
    $editorUiResults = New-Object 'System.Collections.Generic.List[object]'
    foreach ($context in $contexts) {
        $context | Add-Member -NotePropertyName AuthoringSetupSucceeded -NotePropertyValue $false -Force
        try {
            $draft = Invoke-StabilityHarness $context '/flow/custom-draft' @{
                action = 'create'; template = 'small-survival-challenge'
            } 180
            Write-StabilityJson $draft (Join-Path $context.ArtifactRoot 'draft-create.json')
            $draftReady = $null
            $draftDeadline = [DateTimeOffset]::UtcNow.AddSeconds(180)
            do {
                $draftReady = Invoke-StabilityHarness $context '/flow/custom-draft' @{ action = 'status' } 30
                if ([bool]$draftReady.failed) {
                    throw "Disposable starter-template flow failed: $([string]$draftReady.error)"
                }
                if ([bool]$draftReady.completed -and -not [bool]$draftReady.running) { break }
                Start-Sleep -Milliseconds 500
            } while ([DateTimeOffset]::UtcNow -lt $draftDeadline)
            Write-StabilityJson $draftReady (Join-Path $context.ArtifactRoot 'draft-ready.json')
            if ($null -eq $draftReady -or -not [bool]$draftReady.completed -or [bool]$draftReady.running) {
                throw 'Disposable starter-template flow did not complete within 180 seconds.'
            }
            $authoring = Wait-StabilityAuthoringState $context { param($state) [bool]$state.isActive }
            Write-StabilityJson $authoring (Join-Path $context.ArtifactRoot 'authoring-state.json')
            $context | Add-Member -NotePropertyName ActiveDraftId -NotePropertyValue ([string]$authoring.state.activeDraftId) -Force
            $context | Add-Member -NotePropertyName ActiveScenarioFilePath -NotePropertyValue ([string]$authoring.state.activeScenarioFilePath) -Force
            if ([string]::IsNullOrWhiteSpace($context.ActiveDraftId) -or [string]::IsNullOrWhiteSpace($context.ActiveScenarioFilePath)) {
                throw 'Active authoring state did not expose the disposable draft id and scenario path.'
            }

            $screen = Invoke-StabilityHarness $context '/state/snapshot' @{} 15
            $context | Add-Member -NotePropertyName OriginalScreenWidth -NotePropertyValue ([int]$screen.screen.width) -Force
            $context | Add-Member -NotePropertyName OriginalScreenHeight -NotePropertyValue ([int]$screen.screen.height) -Force
            $homeStage = Invoke-StabilityAuthoringAction $context 'shell.window.toggle.scenario' 60
            Write-StabilityJson $homeStage (Join-Path $context.ArtifactRoot 'editor-home-stage.json')
            Assert-StabilityResponse $homeStage 'Open the rendered Home workspace before editing its fields'
            $longTitle = "Agentic $($context.PlatformName) stability scenario - " + ('rapid-editor-title-' * 5)
            $longDescription = ("[$($context.PlatformName)] Long-form editor stability description with punctuation and XML-sensitive characters. " * 8).Trim()
            $longTags = (1..24 | ForEach-Object { "stress-tag-$($_.ToString('00'))" }) -join ', '
            $longChecklistNote = ("Verified required mods on $($context.PlatformName); simultaneous storefront run; " * 6).Trim()
            foreach ($field in @(
                @{ Id = 'editor.draft.title.'; Value = $longTitle; Name = 'title' },
                @{ Id = 'editor.draft.description.'; Value = $longDescription; Name = 'description' },
                @{ Id = 'editor.draft.tags.'; Value = $longTags; Name = 'tags' }
            )) {
                $fieldResponse = Invoke-StabilityHarness $context '/authoring/field' @{ id = $field.Id; value = $field.Value } 60
                Write-StabilityJson $fieldResponse (Join-Path $context.ArtifactRoot "field-$($field.Name).json")
                Assert-StabilityResponse $fieldResponse "Long $($field.Name) edit"
            }
            $testStage = Invoke-StabilityAuthoringAction $context 'stage.select.Test' 60
            Write-StabilityJson $testStage (Join-Path $context.ArtifactRoot 'editor-test-stage.json')
            Assert-StabilityResponse $testStage 'Open the rendered Test workspace before editing its checklist'
            $checklistNoteResponse = Invoke-StabilityHarness $context '/authoring/field' @{
                id = 'testchecklist.note.verified_required_mods.'
                value = $longChecklistNote
            } 60
            Write-StabilityJson $checklistNoteResponse (Join-Path $context.ArtifactRoot 'field-checklist-note.json')
            Assert-StabilityResponse $checklistNoteResponse 'Long checklist-note edit'
            $checkRequiredMods = Invoke-StabilityAuthoringAction $context 'testchecklist.toggle.verified_required_mods'
            Assert-StabilityResponse $checkRequiredMods 'Required-mod checklist toggle'
            Write-StabilityJson $checkRequiredMods (Join-Path $context.ArtifactRoot 'checklist-required-mods.json')
            $initialSave = Invoke-StabilityAuthoringAction $context 'editor.save'
            Write-StabilityJson $initialSave (Join-Path $context.ArtifactRoot 'editor-save-initial.json')
            Assert-StabilityResponse $initialSave 'Initial editor save'

            $scenarioPath = [IO.Path]::GetFullPath($context.ActiveScenarioFilePath)
            $sidecarPath = Join-Path (Split-Path -Parent $scenarioPath) (([IO.Path]::GetFileNameWithoutExtension($scenarioPath)) + '.editor.xml')
            if (-not (Test-Path -LiteralPath $scenarioPath -PathType Leaf)) { throw "Saved draft scenario is missing: $scenarioPath" }
            if (-not (Test-Path -LiteralPath $sidecarPath -PathType Leaf)) { throw "Saved editor sidecar is missing: $sidecarPath" }
            [xml]$sidecarXml = Get-Content -LiteralPath $sidecarPath -Raw
            if ($null -eq $sidecarXml.DocumentElement -or
                $null -eq $sidecarXml.DocumentElement.SelectSingleNode("AuthorTestChecklist/Item[@id='verified_required_mods' and @checked='True']")) {
                throw 'Editor sidecar is not well-formed or did not persist the completed required-mod checklist item.'
            }

            $openHistory = Open-StabilityDraftHistory $context
            Write-StabilityJson $openHistory (Join-Path $context.ArtifactRoot 'history-open.json')
            $saveVersion = Invoke-StabilityAuthoringAction $context 'editor.history.save_version'
            Write-StabilityJson $saveVersion (Join-Path $context.ArtifactRoot 'history-save-version.json')
            Assert-StabilityResponse $saveVersion 'Create protected draft version'
            $closeHistory = Invoke-StabilityAuthoringAction $context 'editor.history.close'
            Assert-StabilityResponse $closeHistory 'Close draft history before editing metadata'
            $homeMetadata = Invoke-StabilityAuthoringAction $context 'shell.window.toggle.scenario'
            Assert-StabilityResponse $homeMetadata 'Return to the rendered Home workspace before mutating the title'
            $mutatedTitle = $longTitle + ' MUTATED BEFORE RESTORE'
            $mutateResponse = Invoke-StabilityHarness $context '/authoring/field' @{ id = 'editor.draft.title.'; value = $mutatedTitle } 60
            Assert-StabilityResponse $mutateResponse 'Pre-restore draft mutation'
            $reopenHistory = Open-StabilityDraftHistory $context
            Write-StabilityJson $reopenHistory (Join-Path $context.ArtifactRoot 'history-reopen.json')
            $historyCatalog = Invoke-StabilityHarness $context '/actions/catalog' @{} 60
            $restoreAction = @($historyCatalog.actions | Where-Object {
                [bool]$_.enabled -and [string]$_.id -like 'editor.history.restore.*' -and [string]$_.id -eq 'editor.history.restore.0'
            }) | Select-Object -First 1
            if ($null -eq $restoreAction) { throw 'History window exposed no enabled snapshot restore action.' }
            Write-StabilityJson (Invoke-StabilityAuthoringAction $context ([string]$restoreAction.id)) (Join-Path $context.ArtifactRoot 'history-restore-select.json')
            Write-StabilityJson (Invoke-StabilityAuthoringAction $context 'editor.history.confirm_restore') (Join-Path $context.ArtifactRoot 'history-restore-confirm.json')
            [void](Invoke-StabilityAuthoringAction $context 'editor.save')
            [xml]$restoredSidecarXml = Get-Content -LiteralPath $sidecarPath -Raw
            if ($null -eq $restoredSidecarXml.DocumentElement.SelectSingleNode("AuthorTestChecklist/Item[@id='verified_required_mods' and @checked='True']")) {
                throw 'Named-version restore did not retain the persisted required-mod checklist completion.'
            }
            [void](Invoke-StabilityAuthoringAction $context 'editor.history.close')
            $pairEvidence = Test-StabilitySnapshotPairs -DraftScenarioPath $scenarioPath
            Write-StabilityJson $pairEvidence (Join-Path $context.ArtifactRoot 'snapshot-pairs.json')
            if (-not [bool]$pairEvidence.Ok -or $pairEvidence.NamedVersions -lt 1 -or $pairEvidence.Autosaves -lt 1) {
                throw 'Snapshot restore did not retain complete named-version/autosave scenario-sidecar pairs.'
            }

            try {
                foreach ($resolution in $layoutResolutions) {
                    $resolutionResponse = Invoke-StabilityHarness $context '/resolution' @{
                        width = $resolution.Width; height = $resolution.Height; fullscreen = 'false'
                    } 30
                    Assert-StabilityResponse $resolutionResponse "$($resolution.Width)x$($resolution.Height) resolution change"
                    Start-Sleep -Milliseconds 250
                    foreach ($stage in $editorStages) {
                        [void](Invoke-StabilityAuthoringAction $context ("stage.select.$stage"))
                        $stageState = Wait-StabilityAuthoringState $context {
                            param($state)
                            [string]$state.activeStage -eq $stage -or
                                ($stage -eq 'Bunker' -and [string]$state.activeStage -in @('BunkerBackground', 'BunkerSurface', 'BunkerInside'))
                        } 30
                        $shell = Invoke-StabilityHarness $context '/authoring/shell' @{ fields = 'nav,actions,sections.titles' } 60
                        $catalog = Invoke-StabilityHarness $context '/actions/catalog' @{} 60
                        Assert-StabilityResponse $shell "Shell projection for $stage"
                        Assert-StabilityResponse $catalog "Action catalog for $stage"
                        if (@($catalog.actions).Count -eq 0 -or -not @($catalog.actions | Where-Object id -EQ 'editor.save')) {
                            throw "Stage '$stage' exposed no actions or lost the canonical save action."
                        }
                        $layoutRoot = Join-Path $context.ArtifactRoot ("editor-layout\{0}x{1}" -f $resolution.Width, $resolution.Height)
                        if (-not (Test-Path -LiteralPath $layoutRoot)) { New-Item -ItemType Directory -Path $layoutRoot -Force | Out-Null }
                        $screenshotPath = Join-Path $layoutRoot ($stage + '.png')
                        [void](Save-ShelteredHarnessScreenshot -Port $context.Port -Path $screenshotPath -Mode client)
                        Write-StabilityJson $shell (Join-Path $layoutRoot ($stage + '.shell.json'))
                        Write-StabilityJson $catalog (Join-Path $layoutRoot ($stage + '.actions.json'))
                        $editorStageCoverage.Add([pscustomobject]@{
                            Platform = $context.PlatformName; Width = $resolution.Width; Height = $resolution.Height
                            Stage = $stage; ActiveStage = [string]$stageState.state.activeStage
                            ActionCount = @($catalog.actions).Count; Screenshot = $screenshotPath; Ok = $true
                        })
                    }
                }
            }
            finally {
                if ($context.OriginalScreenWidth -gt 0 -and $context.OriginalScreenHeight -gt 0) {
                    [void](Invoke-StabilityHarness $context '/resolution' @{
                        width = $context.OriginalScreenWidth; height = $context.OriginalScreenHeight; fullscreen = 'false'
                    } 30)
                }
            }

            for ($i = 0; $i -lt $RapidUiActions; $i++) {
                $rapidActionId = "stage.select.$($editorStages[$i % $editorStages.Count])"
                try {
                    $rapidResponse = Invoke-StabilityAuthoringAction $context $rapidActionId 15
                    $editorUiResults.Add([pscustomobject]@{ Platform = $context.PlatformName; Iteration = $i; Action = $rapidActionId; Ok = $true; Error = '' })
                }
                catch {
                    $editorUiResults.Add([pscustomobject]@{ Platform = $context.PlatformName; Iteration = $i; Action = $rapidActionId; Ok = $false; Error = $_.Exception.Message })
                }
            }

            $laneScenarioId = "agentic.stability.$stamp.$($context.PlatformName)"
            $vanillaManifestBeforeProbe = @(Get-StabilityPathManifest -Path (Join-Path $context.InstallRoot 'saves'))
            $laneProbe = Invoke-StabilityHarness $context '/scenario-save-lanes' @{ action = 'probe'; scenarioId = $laneScenarioId } 60
            $vanillaManifestAfterProbe = @(Get-StabilityPathManifest -Path (Join-Path $context.InstallRoot 'saves'))
            $vanillaUnchanged = (($vanillaManifestBeforeProbe | ConvertTo-Json -Compress -Depth 8) -eq ($vanillaManifestAfterProbe | ConvertTo-Json -Compress -Depth 8))
            $saveLaneEvidence.Add([pscustomobject]@{
                Platform = $context.PlatformName; ScenarioId = $laneScenarioId; Probe = $laneProbe
                StockVanillaFilesUnchanged = $vanillaUnchanged
            })
            Write-StabilityJson $laneProbe (Join-Path $context.ArtifactRoot 'scenario-save-lanes.json')
            if (-not [bool]$laneProbe.ok -or -not $vanillaUnchanged -or @($laneProbe.lanes | Where-Object { -not [bool]$_.ok }).Count -gt 0) {
                throw 'Stock vanilla, unlimited built-in, and modded save-lane transaction proof failed.'
            }

            $testWorkspace = Invoke-StabilityAuthoringAction $context 'stage.select.Test' 60
            Assert-StabilityResponse $testWorkspace 'Open the rendered Test workspace before starting playtest'
            $playtest = Invoke-StabilityAuthoringAction $context 'editor.playtest.toggle' 180
            Write-StabilityJson $playtest (Join-Path $context.ArtifactRoot 'playtest-start.json')
            $playtestState = Wait-StabilityAuthoringState $context { param($state) [bool]$state.isPlaytesting } 60
            Write-StabilityJson $playtestState (Join-Path $context.ArtifactRoot 'playtest-state-started.json')
            $context.AuthoringSetupSucceeded = $true
        }
        catch {
            $failures.Add("$($context.PlatformName) authoring/playtest setup: $($_.Exception.Message)")
        }
    }
    $editorUiResults | Export-Csv -LiteralPath (Join-Path $runRoot 'rapid-editor-actions.csv') -NoTypeInformation -Encoding UTF8

    foreach ($context in $contexts) {
        try { Write-StabilityJson (Invoke-StabilityHarness $context '/game/time' @{ action = 'set'; scale = $SimulationScale } 15) (Join-Path $context.ArtifactRoot 'time-scale.json') }
        catch { $failures.Add("$($context.PlatformName) time-scale setup: $($_.Exception.Message)") }
    }

    $spawnRows = New-Object 'System.Collections.Generic.List[object]'
    for ($i = 0; $i -lt $SpawnAttempts; $i++) {
        foreach ($context in $contexts) {
            try {
                $x = (($i % 20) - 10) * 2
                $y = -1 * ([math]::Floor($i / 20) % 5)
                $spawn = Invoke-StabilityHarness $context '/game/spawn-object' @{ type = 'Bed'; level = 0; x = $x; y = $y } 15
                $spawnRows.Add([pscustomobject]@{ Platform = $context.PlatformName; Iteration = $i; X = $x; Y = $y; Ok = [bool]$spawn.ok; Error = [string]$spawn.error })
            }
            catch {
                $spawnRows.Add([pscustomobject]@{ Platform = $context.PlatformName; Iteration = $i; X = $x; Y = $y; Ok = $false; Error = $_.Exception.Message })
            }
        }
    }
    $spawnRows | Export-Csv -LiteralPath (Join-Path $runRoot 'spawn-attempts.csv') -NoTypeInformation -Encoding UTF8

    $deadline = [DateTimeOffset]::UtcNow.AddMinutes($DurationMinutes)
    $soakIteration = 0
    $restartEveryIterations = if ($RestartEveryMinutes -gt 0) { [math]::Max(1, $RestartEveryMinutes * 30) } else { 0 }
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        foreach ($context in $contexts) {
            $snapshot = $null; $pump = $null; $events = $null
            try { $snapshot = Invoke-StabilityHarness $context '/state/snapshot' @{} 10 } catch { $failures.Add("$($context.PlatformName) snapshot: $($_.Exception.Message)") }
            try { $pump = Invoke-StabilityHarness $context '/health/pump' @{} 10 } catch { $failures.Add("$($context.PlatformName) pump: $($_.Exception.Message)") }
            try {
                $events = Invoke-StabilityHarness $context '/events' @{ since = $context.EventCursor; limit = 100 } 10
                if ($null -ne $events.nextCursor) { $context.EventCursor = [long]$events.nextCursor }
                if ($null -ne $events.events -and @($events.events).Count -gt 0) {
                    $eventPath = Join-Path $context.ArtifactRoot 'events.jsonl'
                    foreach ($event in @($events.events)) { ($event | ConvertTo-Json -Compress -Depth 20) | Add-Content -LiteralPath $eventPath -Encoding UTF8 }
                }
            }
            catch { $failures.Add("$($context.PlatformName) events: $($_.Exception.Message)") }
            if ($null -ne $snapshot -and $soakIteration % 5 -eq 0 -and $null -ne $snapshot.family -and @($snapshot.family.members).Count -gt 0) {
                try {
                    $member = @($snapshot.family.members)[0]
                    $moveX = 8 + (($soakIteration / 5) % 12)
                    [void](Invoke-StabilityHarness $context '/game/family-move' @{
                        action = 'go'; memberId = [int]$member.id; x = $moveX; y = -7.63
                    } 15)
                }
                catch { $failures.Add("$($context.PlatformName) family movement: $($_.Exception.Message)") }
            }
            if ([bool]$context.AuthoringSetupSucceeded -and $soakIteration -gt 0 -and $soakIteration % 15 -eq 0) {
                try {
                    $saveResult = Invoke-StabilityHarness $context '/authoring/action' @{ id = 'editor.save' } 60
                    ($saveResult | ConvertTo-Json -Compress -Depth 20) | Add-Content -LiteralPath (Join-Path $context.ArtifactRoot 'save-actions.jsonl') -Encoding UTF8
                    if (-not [bool]$saveResult.ok) { $failures.Add("$($context.PlatformName) periodic authoring save was rejected.") }
                }
                catch { $failures.Add("$($context.PlatformName) periodic authoring save: $($_.Exception.Message)") }
            }
            if ([bool]$context.AuthoringSetupSucceeded -and $restartEveryIterations -gt 0 -and $soakIteration -gt 0 -and $soakIteration % $restartEveryIterations -eq 0) {
                try {
                    $restartResult = Invoke-StabilityHarness $context '/authoring/action' @{ id = 'editor.playtest.restart' } 180
                    ($restartResult | ConvertTo-Json -Compress -Depth 20) | Add-Content -LiteralPath (Join-Path $context.ArtifactRoot 'restart-actions.jsonl') -Encoding UTF8
                    if (-not [bool]$restartResult.ok -or ($null -ne $restartResult.result -and -not [bool]$restartResult.result)) {
                        $failures.Add("$($context.PlatformName) playtest restart was rejected: $([string]$restartResult.reason)")
                    }
                }
                catch { $failures.Add("$($context.PlatformName) playtest restart: $($_.Exception.Message)") }
            }
            Add-StabilityObservation -Rows $observations -Context $context -Phase 'soak' -Snapshot $snapshot -Pump $pump -Events $events
        }
        $soakIteration++
        Start-Sleep -Seconds 2
    }

    foreach ($context in $contexts) {
        if (-not [bool]$context.AuthoringSetupSucceeded) {
            continue
        }
        try {
            $authoringBeforeStop = Invoke-StabilityHarness $context '/authoring/state' @{} 30
            if ($null -ne $authoringBeforeStop.state -and [bool]$authoringBeforeStop.state.isPlaytesting) {
                Write-StabilityJson (Invoke-StabilityAuthoringAction $context 'editor.playtest.toggle' 180) (Join-Path $context.ArtifactRoot 'playtest-stop.json')
            }
            $stoppedState = Wait-StabilityAuthoringState $context { param($state) [bool]$state.isActive -and -not [bool]$state.isPlaytesting } 60
            Write-StabilityJson $stoppedState (Join-Path $context.ArtifactRoot 'playtest-state-stopped.json')
            [void](Invoke-StabilityAuthoringAction $context 'editor.save')

            [void](Invoke-StabilityAuthoringAction $context 'stage.select.Publish')
            $exportStartedUtc = [DateTime]::UtcNow
            $exportResponse = Invoke-StabilityAuthoringAction $context 'publish.export' 180
            Write-StabilityJson $exportResponse (Join-Path $context.ArtifactRoot 'publish-export.json')
            $exportCandidates = @(Get-ChildItem -LiteralPath (Join-Path $context.InstallRoot 'mods') -File -Filter 'scenario.xml' -Recurse -Force |
                Where-Object { $_.FullName -like '*\ScenarioAuthoringExports\*' -and $_.LastWriteTimeUtc -ge $exportStartedUtc.AddSeconds(-2) } |
                Sort-Object LastWriteTimeUtc -Descending)
            $exportScenario = $exportCandidates | Select-Object -First 1
            if ($null -eq $exportScenario) {
                throw "Validated export produced no ScenarioAuthoringExports package. Status: $([string]$exportResponse.statusMessage)"
            }
            $exportRoot = $exportScenario.Directory.FullName
            $packageSidecars = @(Get-ChildItem -LiteralPath $exportRoot -File -Recurse -Force | Where-Object {
                $_.Name -like '*.editor.xml' -or $_.Name -like '*.pairpending-*'
            })
            if ($packageSidecars.Count -gt 0) { throw 'Published package leaked editor sidecars or pair-pending files.' }
            $readmePath = Join-Path $exportRoot 'README.txt'
            if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf) -or (Get-Content -LiteralPath $readmePath -Raw) -notmatch 'Author verified:.*required mods') {
                throw 'Published README did not include the persisted author-test checklist honesty summary.'
            }
            Write-StabilityJson ([pscustomobject]@{
                ExportRoot = $exportRoot
                Files = @(Get-ChildItem -LiteralPath $exportRoot -File -Recurse | ForEach-Object { $_.FullName.Substring($exportRoot.Length).TrimStart('\') })
                EditorSidecarsExcluded = $packageSidecars.Count -eq 0
                ReadmeChecklistSummary = $true
            }) (Join-Path $context.ArtifactRoot 'publish-package-evidence.json')

            $installResponse = Invoke-StabilityHarness $context '/authoring/action' @{ id = 'publish.export.install' } 180
            if (-not [bool]$installResponse.ok -or -not [bool]$installResponse.result) {
                $installText = ([string]$installResponse.reason + ' ' + [string]$installResponse.statusMessage)
                if ($installText -match 'confirm|overwrite|already exists') {
                    $installResponse = Invoke-StabilityAuthoringAction $context 'publish.export.install_confirm' 180
                }
                else {
                    Assert-StabilityResponse $installResponse 'Install exported scenario package'
                }
            }
            Write-StabilityJson $installResponse (Join-Path $context.ArtifactRoot 'publish-install.json')
            [void](Invoke-StabilityAuthoringAction $context 'editor.save')
            $sidecarTextAfterInstall = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $context.ActiveScenarioFilePath) (([IO.Path]::GetFileNameWithoutExtension($context.ActiveScenarioFilePath)) + '.editor.xml')) -Raw
            if ($sidecarTextAfterInstall -notmatch 'installed_export') { throw 'Export reinstall did not persist its checklist completion to the editor sidecar.' }
            $finalPairEvidence = Test-StabilitySnapshotPairs -DraftScenarioPath $context.ActiveScenarioFilePath
            Write-StabilityJson $finalPairEvidence (Join-Path $context.ArtifactRoot 'snapshot-pairs-final.json')
            if (-not [bool]$finalPairEvidence.Ok) { throw 'Final editor save left an incomplete scenario-sidecar snapshot pair.' }

            $uninstall = Invoke-StabilityAuthoringAction $context 'publish.export.uninstall' 180
            Assert-StabilityResponse $uninstall 'Uninstall imported disposable scenario package through the editor lifecycle'
            Write-StabilityJson $uninstall (Join-Path $context.ArtifactRoot 'publish-uninstall.json')

            [void](Invoke-StabilityAuthoringAction $context 'shell.menu.windows')
            Write-StabilityJson (Invoke-StabilityAuthoringAction $context 'editor.close' 180) (Join-Path $context.ArtifactRoot 'editor-close.json')
            $closedState = Wait-StabilityAuthoringState $context { param($state) $null -eq $state -or -not [bool]$state.isActive } 180
            Write-StabilityJson $closedState (Join-Path $context.ArtifactRoot 'editor-state-closed.json')
            $book = Invoke-StabilityHarness $context '/scenario-book/open' @{} 180
            Assert-StabilityResponse $book 'Open scenario book after editor close'
            $duplicate = Invoke-StabilityHarness $context '/flow/custom-draft' @{ action = 'duplicate'; draftId = $context.ActiveDraftId } 180
            Assert-StabilityResponse $duplicate 'Duplicate disposable scenario draft'
            Write-StabilityJson $duplicate (Join-Path $context.ArtifactRoot 'draft-duplicate.json')
            $duplicateId = [string]$duplicate.duplicate.id
            $duplicatePath = [string]$duplicate.duplicate.filePath
            if ([string]::IsNullOrWhiteSpace($duplicateId) -or -not (Test-Path -LiteralPath $duplicatePath -PathType Leaf)) {
                throw 'Draft duplicate did not expose a concrete id and scenario file.'
            }
            $duplicateSidecar = Join-Path (Split-Path -Parent $duplicatePath) (([IO.Path]::GetFileNameWithoutExtension($duplicatePath)) + '.editor.xml')
            if (-not (Test-Path -LiteralPath $duplicateSidecar -PathType Leaf)) { throw 'Draft duplicate did not copy its editor sidecar.' }
            $duplicateDelete = Invoke-StabilityHarness $context '/flow/custom-draft' @{ action = 'delete'; draftId = $duplicateId; confirm = 'true' } 180
            Assert-StabilityResponse $duplicateDelete 'Delete duplicated disposable draft'
            Write-StabilityJson $duplicateDelete (Join-Path $context.ArtifactRoot 'draft-duplicate-delete.json')
            $draftDelete = Invoke-StabilityHarness $context '/flow/custom-draft' @{ action = 'delete'; draftId = $context.ActiveDraftId; confirm = 'true' } 180
            Assert-StabilityResponse $draftDelete 'Delete original disposable draft'
            Write-StabilityJson $draftDelete (Join-Path $context.ArtifactRoot 'draft-original-delete.json')
        }
        catch {
            $failures.Add("$($context.PlatformName) explicit editor cleanup/export/duplicate lifecycle: $($_.Exception.Message)")
        }
    }

    foreach ($context in $contexts) {
        try { Write-StabilityJson (Invoke-StabilityHarness $context '/state/health' @{} 15) (Join-Path $context.ArtifactRoot 'health-final.json') } catch { }
        try { Write-StabilityJson (Invoke-StabilityHarness $context '/state/snapshot' @{} 15) (Join-Path $context.ArtifactRoot 'snapshot-final.json') } catch { }
        try { [void](Save-ShelteredHarnessScreenshot -Port $context.Port -Path (Join-Path $context.ArtifactRoot 'final.png') -Mode client) }
        catch { $failures.Add("$($context.PlatformName) final screenshot: $($_.Exception.Message)") }
        $logPath = Join-Path $context.InstallRoot 'SMM\mod_manager.log'
        if (Test-Path -LiteralPath $logPath) {
            $copiedLog = Join-Path $context.ArtifactRoot 'mod_manager.log'
            Copy-Item -LiteralPath $logPath -Destination $copiedLog -Force
            $pluginErrors = @(Select-String -LiteralPath $copiedLog -Pattern '\[PLUGIN-ERROR\]' -CaseSensitive:$false)
            if ($pluginErrors.Count -gt 0) { $failures.Add("$($context.PlatformName) logged $($pluginErrors.Count) plugin load error(s).") }
        }
    }
}
catch {
    $failures.Add($_.Exception.Message)
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $runRoot 'failure.txt') -Encoding UTF8
}
finally {
    foreach ($context in $contexts) {
        foreach ($cleanupError in @(Stop-ShelteredPlatformSession -Session $context)) { $failures.Add("$($context.PlatformName) $cleanupError") }
        try { @($context.Samples) | Export-Csv -LiteralPath (Join-Path $context.ArtifactRoot 'process-samples.csv') -NoTypeInformation -Encoding UTF8 }
        catch { $failures.Add("$($context.PlatformName) sample export: $($_.Exception.Message)") }
    }
    foreach ($snapshot in @($pathSnapshots.ToArray() | Sort-Object Path -Descending)) {
        $context = $contexts | Where-Object InstallRoot -EQ $snapshot.InstallRoot | Select-Object -First 1
        $platformConfig = $platforms | Where-Object { [IO.Path]::GetFullPath([string]$_.installRoot) -eq [string]$snapshot.InstallRoot } | Select-Object -First 1
        $platformName = if ($null -ne $context) { $context.PlatformName } elseif ($null -ne $platformConfig) { [string]$platformConfig.name } else { 'unknown-platform' }
        if ($null -eq $context -or -not [bool]$context.ProcessStopped) {
            $blockedReason = "Mutable-state restore blocked because platform session '$platformName' is not positively stopped; current state and evidence were preserved."
            $actualExists = Test-Path -LiteralPath ([string]$snapshot.Path)
            $actualManifest = @(Get-StabilityPathManifest -Path ([string]$snapshot.Path))
            $restoreVerification.Add([pscustomobject]@{
                Platform = $platformName; Name = [string]$snapshot.Name; Path = [string]$snapshot.Path
                Attempted = $false; BlockedReason = $blockedReason
                ExpectedExists = [bool]$snapshot.Existed; ActualExists = $actualExists
                ExpectedFiles = @($snapshot.OriginalManifest).Count; ActualFiles = $actualManifest.Count; Ok = $false
            })
            if (-not $failures.Contains($blockedReason)) { $failures.Add($blockedReason) }
            continue
        }
        try {
            Restore-StabilityPathSnapshot -Snapshot $snapshot -MutationArchiveRoot (Join-Path $runRoot "$platformName\mutable-state-after")
            $actualExists = Test-Path -LiteralPath ([string]$snapshot.Path)
            $actualManifest = @(Get-StabilityPathManifest -Path ([string]$snapshot.Path))
            $expectedByPath = @{}
            foreach ($entry in @($snapshot.OriginalManifest)) { $expectedByPath[[string]$entry.RelativePath] = [string]$entry.Sha256 }
            $actualByPath = @{}
            foreach ($entry in $actualManifest) { $actualByPath[[string]$entry.RelativePath] = [string]$entry.Sha256 }
            $manifestOk = ([bool]$snapshot.Existed -eq $actualExists) -and $expectedByPath.Count -eq $actualByPath.Count
            if ($manifestOk) {
                foreach ($relativePath in $expectedByPath.Keys) {
                    if (-not $actualByPath.ContainsKey($relativePath) -or $actualByPath[$relativePath] -ne $expectedByPath[$relativePath]) {
                        $manifestOk = $false
                        break
                    }
                }
            }
            $restoreVerification.Add([pscustomobject]@{
                Platform = $platformName; Name = [string]$snapshot.Name; Path = [string]$snapshot.Path
                Attempted = $true; BlockedReason = ''
                ExpectedExists = [bool]$snapshot.Existed; ActualExists = $actualExists
                ExpectedFiles = $expectedByPath.Count; ActualFiles = $actualByPath.Count; Ok = $manifestOk
            })
            if (-not $manifestOk) { $failures.Add("Mutable-state restore verification failed for '$($snapshot.Path)'.") }
        }
        catch { $failures.Add("Mutable-state restore '$($snapshot.Path)': $($_.Exception.Message)") }
    }
    foreach ($context in $contexts) {
        try { Restore-ShelteredPlatformSession -Session $context }
        catch { $failures.Add("Install-state restore '$($context.InstallRoot)': $($_.Exception.Message)") }
    }
    if ($locks.Count -gt 0) { try { Exit-BenchmarkInstallLocks -Locks $locks } catch { $failures.Add("Install lock cleanup: $($_.Exception.Message)") } }
}

$observations | Export-Csv -LiteralPath (Join-Path $runRoot 'observations.csv') -NoTypeInformation -Encoding UTF8
$editorStageCoverage | Export-Csv -LiteralPath (Join-Path $runRoot 'editor-stage-layout-coverage.csv') -NoTypeInformation -Encoding UTF8
Write-StabilityJson $saveLaneEvidence.ToArray() (Join-Path $runRoot 'scenario-save-lane-evidence.json')
Write-StabilityJson $restoreVerification.ToArray() (Join-Path $runRoot 'restore-verification.json')
$metrics = @($observations | Group-Object Platform | ForEach-Object {
    $items = @($_.Group)
    $workingSets = @($items | Where-Object { $null -ne $_.WorkingSetMiB } | ForEach-Object { [double]$_.WorkingSetMiB })
    [pscustomobject]@{
        Platform = $_.Name
        Samples = $items.Count
        AliveFailures = @($items | Where-Object { -not [bool]$_.Alive }).Count
        RespondingFailures = @($items | Where-Object { -not [bool]$_.Responding }).Count
        StartWorkingSetMiB = if ($workingSets.Count -gt 0) { $workingSets[0] } else { $null }
        EndWorkingSetMiB = if ($workingSets.Count -gt 0) { $workingSets[-1] } else { $null }
        WorkingSetGrowthMiB = if ($workingSets.Count -gt 0) { [math]::Round($workingSets[-1] - $workingSets[0], 2) } else { $null }
        PeakWorkingSetMiB = if ($workingSets.Count -gt 0) { [math]::Round(($workingSets | Measure-Object -Maximum).Maximum, 2) } else { $null }
        HarnessEvents = [int](($items | Measure-Object EventCount -Sum).Sum)
    }
})
foreach ($metric in $metrics) {
    if ($metric.AliveFailures -gt 0) { $failures.Add("$($metric.Platform) exited during soak sampling.") }
    if ($metric.RespondingFailures -gt 0) { $failures.Add("$($metric.Platform) was non-responsive during $($metric.RespondingFailures) soak sample(s).") }
}
Write-StabilityJson $metrics (Join-Path $runRoot 'metrics.json')
$summary = [pscustomobject]@{
    RunRoot = $runRoot
    Platforms = @($contexts | ForEach-Object { $_.PlatformName })
    Profile = $Profile
    DurationMinutes = $DurationMinutes
    RapidUiActionsPerPlatform = $RapidUiActions
    RapidEditorActionsPerPlatform = $RapidUiActions
    EditorStagesPerPlatform = $editorStages.Count
    LayoutResolutionsPerPlatform = $layoutResolutions.Count
    EditorStageLayoutEvidenceCount = $editorStageCoverage.Count
    SpawnAttemptsPerPlatform = $SpawnAttempts
    RestartEveryMinutes = $RestartEveryMinutes
    ObservationCount = $observations.Count
    Metrics = $metrics
    Failures = $failures.ToArray()
    CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
}
Write-StabilityJson $summary (Join-Path $runRoot 'summary.json')
Write-Host "Stability campaign complete: $runRoot"
if ($failures.Count -gt 0) { throw "Stability campaign recorded $($failures.Count) failure(s). See summary.json." }
