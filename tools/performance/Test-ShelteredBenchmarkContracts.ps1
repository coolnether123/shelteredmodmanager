#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ShelteredBenchmark.Runner.psm1') -Force -DisableNameChecking
Import-Module (Join-Path $PSScriptRoot 'ShelteredBenchmark.Core.psm1') -Force -DisableNameChecking
Import-Module (Join-Path $PSScriptRoot 'ShelteredBenchmark.Harness.psm1') -Force -DisableNameChecking

$script:passed = 0
function Assert-True($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) { throw "$Message Expected '$Expected', got '$Actual'." }
}
function Invoke-Contract([string]$Name, [scriptblock]$Body) {
    & $Body
    $script:passed++
    Write-Host "PASS $Name"
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('sheltered-benchmark-contract-' + [Guid]::NewGuid().ToString('N'))
$resolvedTempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$resolvedTempRoot = [IO.Path]::GetFullPath($tempRoot)
if (-not $resolvedTempRoot.StartsWith($resolvedTempBase, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to create test workspace outside the system temp root: $resolvedTempRoot"
}
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
try {
    $install = Join-Path $tempRoot 'install'
    New-Item -ItemType Directory -Path (Join-Path $install 'mods'), (Join-Path $install 'SMM\bin') -Force | Out-Null
    @('[General]', 'enabled=true', 'target_assembly=SMM\\bin\\Doorstop.dll') | Set-Content (Join-Path $install 'doorstop_config.ini') -Encoding UTF8
    @{
        version = 1
        booleans = @(@{ id = 'ShelteredScenarioEditor.Enabled'; value = $true })
    } | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $install 'SMM\bin\manager_options.json') -Encoding UTF8

    $manifests = @(
        @{ Folder = 'Harmony'; Id = 'com.harmony.0harmony'; Depends = @(); After = @() },
        @{ Folder = 'Harness'; Id = 'coolnether123.shelteredagentinterface'; Depends = @(); After = @('com.harmony.0harmony') },
        @{ Folder = 'Feature'; Id = 'example.feature'; Depends = @('example.library'); After = @() },
        @{ Folder = 'Library'; Id = 'example.library'; Depends = @(); After = @() }
    )
    foreach ($manifest in $manifests) {
        $aboutRoot = Join-Path $install ('mods\' + $manifest.Folder + '\About')
        New-Item -ItemType Directory -Path $aboutRoot -Force | Out-Null
        @{
            id = $manifest.Id; name = $manifest.Folder; version = '1.0.0'
            dependsOn = $manifest.Depends; loadAfter = $manifest.After
        } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $aboutRoot 'About.json') -Encoding UTF8
    }

    Invoke-Contract 'configuration accepts schema version 1' {
        $config = [pscustomobject]@{
            schemaVersion = 1; outputRoot = 'out'; iterations = 1
            platforms = @([pscustomobject]@{ name = 'test'; installRoot = 'unused'; executable = 'game.exe'; processName = 'game' })
            profiles = @([pscustomobject]@{ name = 'vanilla'; mode = 'vanilla'; harness = $false })
            sampling = [pscustomobject]@{ processIntervalMilliseconds = 100 }
        }
        $result = Test-ShelteredBenchmarkConfig $config -SkipPathChecks
        Assert-True $result.Valid ($result.Errors -join '; ')
    }

    Invoke-Contract 'configuration rejects duplicates and invalid explicit profile' {
        $config = [pscustomobject]@{
            schemaVersion = 1; outputRoot = 'out'; iterations = 1
            platforms = @(
                [pscustomobject]@{ name = 'same'; installRoot = 'x'; executable = 'a'; processName = 'a' },
                [pscustomobject]@{ name = 'same'; installRoot = 'y'; executable = 'b'; processName = 'b' }
            )
            profiles = @([pscustomobject]@{ name = 'broken'; mode = 'explicit'; include = @() })
            sampling = [pscustomobject]@{ processIntervalMilliseconds = 100 }
        }
        $result = Test-ShelteredBenchmarkConfig $config -SkipPathChecks
        Assert-True (-not $result.Valid) 'Invalid configuration unexpectedly passed.'
        Assert-True (($result.Errors -join ' ') -match 'Duplicate platform') 'Duplicate platform error was not reported.'
    }

    Invoke-Contract 'live runner freezes verified deployment identities' {
        $entrypointSource = Get-Content (Join-Path $PSScriptRoot 'Invoke-ShelteredBenchmark.ps1') -Raw
        Assert-True ($entrypointSource -match 'Test-BenchmarkDeploymentHashes[\s\S]*Add-Member -NotePropertyName sha256[\s\S]*frozen_deployment_hashes\.json') 'The live runner no longer freezes its preflight-verified hashes before collecting cases.'
    }

    Invoke-Contract 'scenario editor deployment and profiles use one canonical identity' {
        $configSource = Get-Content (Join-Path $PSScriptRoot 'benchmark.config.example.json') -Raw
        $benchmarkConfig = $configSource | ConvertFrom-Json
        $retiredEditorOptionId = @('ShelteredAPI', 'PatchCustomScenarioEditor') -join '.'
        Assert-True (-not $configSource.Contains($retiredEditorOptionId)) 'Retired ShelteredAPI editor toggle remains in benchmark configuration.'
        foreach ($platform in @($benchmarkConfig.platforms)) {
            $editorGates = @($platform.hashGates | Where-Object role -EQ 'scenarioeditor')
            Assert-Equal 1 $editorGates.Count "Platform '$($platform.name)' does not have exactly one scenario-editor hash gate."
            Assert-Equal 'SMM\bin\ShelteredScenarioEditor.dll' ([string]$editorGates[0].deployedPath) "Platform '$($platform.name)' editor gate targets the wrong deployment."
        }
        $offProfile = $benchmarkConfig.profiles | Where-Object name -EQ 'smm-scenario-editor-off' | Select-Object -First 1
        $onProfile = $benchmarkConfig.profiles | Where-Object name -EQ 'smm-core' | Select-Object -First 1
        Assert-True ($null -ne $offProfile -and $null -ne $onProfile) 'Editor off/on benchmark profiles are missing.'
        Assert-True ([bool]$offProfile.scenarioTransition) 'Editor-off profile no longer verifies custom-scenario browsing.'
        Assert-True (-not [bool]$offProfile.managerOptions.'ShelteredScenarioEditor.Enabled') 'Editor-off profile does not disable the canonical option.'
        Assert-True ([bool]$onProfile.managerOptions.'ShelteredScenarioEditor.Enabled') 'Editor-on profile does not enable the canonical option.'
        Assert-True ([array]::IndexOf(@($benchmarkConfig.profiles.name), 'smm-scenario-editor-off') -lt [array]::IndexOf(@($benchmarkConfig.profiles.name), 'smm-core')) 'Configuration no longer exercises editor off before editor on.'

        $deploySource = Get-Content (Join-Path $PSScriptRoot 'Deploy-ShelteredBenchmarkBuild.ps1') -Raw
        Assert-True ($deploySource.Contains('ShelteredScenarioEditor\obj\$Configuration\ShelteredScenarioEditor.dll')) 'Deployment does not consume the current editor build output.'
        Assert-True ($deploySource.Contains("Copy-VerifiedArtifact `$scenarioEditorOutput (Join-Path `$targetSmm 'bin\ShelteredScenarioEditor.dll')")) 'Deployment does not hash-verify the editor DLL for each storefront.'

        $coreSource = Get-Content (Join-Path $PSScriptRoot 'ShelteredBenchmark.Core.psm1') -Raw
        Assert-True ($coreSource.Contains("SMM\bin\ShelteredScenarioEditor.dll'); Role = 'scenarioeditor'")) 'Environment evidence no longer fingerprints the editor DLL.'
    }

    Invoke-Contract 'Git diff fingerprint ignores autocrlf advice' {
        $coreSource = Get-Content (Join-Path $PSScriptRoot 'ShelteredBenchmark.Core.psm1') -Raw
        Assert-True ($coreSource -match 'git -c core\.autocrlf=false -C \$RepositoryRoot diff --binary --no-ext-diff 2>\$null') 'Environment fingerprinting can again fail a case on harmless Git line-ending advice.'
    }

    Invoke-Contract 'installed catalog and dependency profile are deterministic' {
        $catalog = Get-InstalledModCatalog $install
        Assert-Equal 4 $catalog.Count 'Catalog count mismatch.'
        $profile = [pscustomobject]@{
            name = 'feature'; mode = 'explicit'; harness = $true
            include = @('example.feature'); exclude = @(); includeDependencies = $true
        }
        $ids = Resolve-ShelteredModProfile $profile $catalog @('example.feature', 'example.library') @('com.harmony.0harmony', 'coolnether123.shelteredagentinterface')
        Assert-True ($ids -contains 'example.library') 'Declared dependency was not included.'
        Assert-True ([array]::IndexOf($ids, 'example.library') -lt [array]::IndexOf($ids, 'example.feature')) 'Dependency did not precede dependent mod.'
        Assert-True ([array]::IndexOf($ids, 'com.harmony.0harmony') -lt [array]::IndexOf($ids, 'coolnether123.shelteredagentinterface')) 'Core ordering was not preserved.'
    }

    Invoke-Contract 'vanilla profile resolves as an empty collection' {
        $profile = [pscustomobject]@{ name = 'vanilla'; mode = 'vanilla'; harness = $false }
        $ids = @(Resolve-ShelteredModProfile -Profile $profile -Catalog (Get-InstalledModCatalog $install) -ExistingOrder @() -CoreModIds @())
        Assert-Equal 0 $ids.Count 'Vanilla mod selection did not retain an empty collection shape.'
    }

    Invoke-Contract 'load order writes only selected enabled mods' {
        Set-ShelteredLoadOrder $install @('com.harmony.0harmony', 'example.library')
        $state = Get-Content (Join-Path $install 'mods\loadorder.json') -Raw | ConvertFrom-Json
        Assert-Equal 2 $state.order.Count 'Load-order count mismatch.'
        Assert-True $state.mods.'example.library'.enabled 'Selected mod was not enabled.'
        Assert-True ($null -eq $state.mods.'example.feature') 'Unselected mod leaked into state.'
    }

    Invoke-Contract 'enabled profile starts from captured enabled state' {
        $state = Get-LoadOrderState $install
        $enabledIds = Get-EnabledLoadOrderIds $state
        $profile = [pscustomobject]@{ name = 'enabled'; mode = 'enabled'; harness = $true; include = @(); exclude = @() }
        $ids = Resolve-ShelteredModProfile -Profile $profile -Catalog (Get-InstalledModCatalog $install) -ExistingOrder @($state.order) `
            -CoreModIds @('com.harmony.0harmony', 'coolnether123.shelteredagentinterface') -ExistingEnabledIds $enabledIds
        Assert-True ($ids -contains 'example.library') 'Previously enabled mod was not selected.'
        Assert-True (-not ($ids -contains 'example.feature')) 'Previously disabled mod was selected.'
    }

    Invoke-Contract 'Doorstop and manager-option mutations are targeted' {
        Set-DoorstopEnabled $install $false
        Assert-True ((Get-Content (Join-Path $install 'doorstop_config.ini') -Raw) -match '(?m)^enabled=false$') 'Doorstop was not disabled.'
        $retiredEditorOptionId = @('ShelteredAPI', 'PatchCustomScenarioEditor') -join '.'
        [pscustomobject]@{
            version = 1
            booleans = @(
                [pscustomobject]@{ id = $retiredEditorOptionId; value = $true },
                [pscustomobject]@{ id = 'ModAPI.DisableUnityLogSuppression'; value = $true }
            )
        } | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $install 'SMM\bin\manager_options.json') -Encoding UTF8
        Set-ShelteredManagerOptions $install ([pscustomobject]@{ 'ShelteredScenarioEditor.Enabled' = $false })
        $options = Get-Content (Join-Path $install 'SMM\bin\manager_options.json') -Raw | ConvertFrom-Json
        $canonicalEditorOption = @($options.booleans | Where-Object id -EQ 'ShelteredScenarioEditor.Enabled')
        Assert-Equal 1 $canonicalEditorOption.Count 'Canonical editor option was not seeded exactly once.'
        Assert-True (-not [bool]$canonicalEditorOption[0].value) 'Canonical editor option override was not applied.'
        Assert-Equal 0 @($options.booleans | Where-Object id -EQ $retiredEditorOptionId).Count 'Retired editor option was not removed.'
        Assert-True ([bool]($options.booleans | Where-Object id -EQ 'ModAPI.DisableUnityLogSuppression').value) 'Unrelated manager option changed.'
    }

    Invoke-Contract 'install snapshot restores changed and originally absent files' {
        $loadOrderPath = Join-Path $install 'mods\loadorder.json'
        $originalDoorstop = Get-Content (Join-Path $install 'doorstop_config.ini') -Raw
        $snapshotRoot = Join-Path $tempRoot 'snapshot'
        $snapshot = New-InstallStateSnapshot $install $snapshotRoot
        'corrupted' | Set-Content (Join-Path $install 'doorstop_config.ini')
        Remove-Item -LiteralPath $loadOrderPath -Force
        Restore-InstallStateSnapshot $snapshot
        Assert-Equal $originalDoorstop (Get-Content (Join-Path $install 'doorstop_config.ini') -Raw) 'Doorstop snapshot was not restored.'
        Assert-True (Test-Path $loadOrderPath) 'Deleted pre-existing load order was not restored.'

        Remove-Item -LiteralPath $loadOrderPath -Force
        $snapshot2 = New-InstallStateSnapshot $install (Join-Path $tempRoot 'snapshot-missing')
        '{}' | Set-Content $loadOrderPath
        Restore-InstallStateSnapshot $snapshot2
        Assert-True (-not (Test-Path $loadOrderPath)) 'Originally absent file was not removed during restore.'
    }

    Invoke-Contract 'restore hard-fails a non-identical snapshot payload' {
        $isolated = Join-Path $tempRoot 'restore-integrity'
        New-Item -ItemType Directory -Path $isolated | Out-Null
        'enabled=true' | Set-Content (Join-Path $isolated 'doorstop_config.ini') -Encoding UTF8
        $snapshot = New-InstallStateSnapshot $isolated (Join-Path $tempRoot 'snapshot-integrity')
        $doorstopEntry = $snapshot.Entries | Where-Object RelativePath -EQ 'doorstop_config.ini'
        'tampered-backup' | Set-Content $doorstopEntry.BackupPath -Encoding UTF8
        $rejected = $false
        try { Restore-InstallStateSnapshot $snapshot } catch { $rejected = $_.Exception.Message -match 'residual differences' }
        Assert-True $rejected 'Restore did not hard-fail a hash mismatch.'
    }

    Invoke-Contract 'deployment hash gate accepts identity and rejects stale deployment' {
        $source = Join-Path $tempRoot 'source.dll'
        $deployed = Join-Path $install 'deployed.dll'
        'same-build' | Set-Content $source -Encoding UTF8
        Copy-Item $source $deployed
        $gate = [pscustomobject]@{ name = 'test'; role = 'modapi'; deployedPath = 'deployed.dll'; sourcePath = $source }
        $accepted = Test-BenchmarkDeploymentHashes @($gate) $install $tempRoot (Join-Path $tempRoot 'gate-pass.json')
        Assert-True $accepted.Ok 'Identical deployment was rejected.'
        'stale-build' | Set-Content $deployed -Encoding UTF8
        $rejected = $false
        try { [void](Test-BenchmarkDeploymentHashes @($gate) $install $tempRoot (Join-Path $tempRoot 'gate-fail.json')) }
        catch { $rejected = $true }
        Assert-True $rejected 'Stale deployment was not rejected.'
    }

    Invoke-Contract 'post-case file manifest rejects changed and added mod files' {
        $manifestRoot = Join-Path $tempRoot 'manifest-mod'
        New-Item -ItemType Directory -Path $manifestRoot -Force | Out-Null
        $assembly = Join-Path $manifestRoot 'feature.dll'
        'stable' | Set-Content -LiteralPath $assembly -Encoding UTF8
        $expected = @(Get-FileFingerprint -Path $assembly -Role 'mod:feature')
        $accepted = Test-BenchmarkFileManifest -ExpectedFiles $expected -SelectedModDirectories @($manifestRoot) -OutputPath (Join-Path $tempRoot 'manifest-pass.json')
        Assert-True $accepted.Ok 'An unchanged case manifest was rejected.'
        $runtimeData = Join-Path $manifestRoot 'Data\change-intelligence'
        New-Item -ItemType Directory -Path $runtimeData -Force | Out-Null
        'runtime-cache' | Set-Content -LiteralPath (Join-Path $runtimeData 'generated.json') -Encoding UTF8
        $mutableAccepted = Test-BenchmarkFileManifest -ExpectedFiles $expected -SelectedModDirectories @($manifestRoot) `
            -MutableModRelativePathPatterns @('Data/change-intelligence/*') -OutputPath (Join-Path $tempRoot 'manifest-mutable-pass.json')
        Assert-True $mutableAccepted.Ok 'An explicitly declared runtime-writable mod path was treated as deployment drift.'
        'added' | Set-Content -LiteralPath (Join-Path $manifestRoot 'late.json') -Encoding UTF8
        $rejected = $false
        try { [void](Test-BenchmarkFileManifest -ExpectedFiles $expected -SelectedModDirectories @($manifestRoot) -MutableModRelativePathPatterns @('Data/change-intelligence/*') -OutputPath (Join-Path $tempRoot 'manifest-fail.json')) }
        catch { $rejected = $_.Exception.Message -match 'unexpected selected-mod file' }
        Assert-True $rejected 'A selected-mod file added during the case was not rejected.'
    }

    Invoke-Contract 'cross-process install mutex rejects a second runner' {
        $locks = Enter-BenchmarkInstallLocks @($install)
        try {
            Assert-True (-not [string]::IsNullOrWhiteSpace([string]$locks[0].OwnershipToken)) 'Install lock has no transferable ownership token.'
            Assert-Equal $PID ([int]$locks[0].OwnerProcessId) 'Install lock owner PID is not recorded.'
            $modulePath = Join-Path $PSScriptRoot 'ShelteredBenchmark.Core.psm1'
            $job = Start-Job -ScriptBlock {
                param($module, $root)
                Import-Module $module -Force -DisableNameChecking
                try { $taken = Enter-BenchmarkInstallLocks @($root); Exit-BenchmarkInstallLocks $taken; 'unexpected' }
                catch { 'blocked' }
            } -ArgumentList $modulePath, $install
            [void](Wait-Job $job)
            $outcome = Receive-Job $job
            Remove-Job $job -Force
            Assert-Equal 'blocked' $outcome 'Second process acquired an owned install mutex.'

            $envelope = New-BenchmarkInstallLockEnvelope -Locks $locks
            $runnerModule = Join-Path $PSScriptRoot 'ShelteredBenchmark.Runner.psm1'
            $authorizationJob = Start-Job -ScriptBlock {
                param($module, $authorization, $root, $scratch)
                Import-Module $module -Force -DisableNameChecking
                try {
                    [void](Start-ShelteredPlatformSession `
                        -Platform ([pscustomobject]@{ name = 'job-contract'; installRoot = $root; processName = 'powershell'; executable = 'unused.exe'; agentPort = 0 }) `
                        -Profile ([pscustomobject]@{ name = 'vanilla'; mode = 'vanilla'; harness = $false }) `
                        -Config ([pscustomobject]@{ _configRoot = $scratch }) -StateRoot (Join-Path $scratch 'job-state') -ArtifactRoot $scratch `
                        -LeaseOwner 'job-contract' -InstallLocks @($authorization.Locks))
                    'unexpected'
                }
                catch { $_.Exception.Message }
            } -ArgumentList $runnerModule, $envelope, $install, $tempRoot
            [void](Wait-Job $authorizationJob)
            $authorizationOutcome = [string](Receive-Job $authorizationJob)
            Remove-Job $authorizationJob -Force
            Assert-True ($authorizationOutcome -match 'Refusing to launch because job-contract already has process') 'A real Start-Job did not preserve parent-issued install-lock authorization.'

            $preflightState = Join-Path $tempRoot 'preflight-must-not-snapshot'
            $preflightRejected = $false
            try {
                [void](Start-ShelteredPlatformSession `
                    -Platform ([pscustomobject]@{ name = 'contract'; installRoot = $install; processName = 'powershell'; executable = 'unused.exe'; agentPort = 0 }) `
                    -Profile ([pscustomobject]@{ name = 'vanilla'; mode = 'vanilla'; harness = $false }) `
                    -Config ([pscustomobject]@{ _configRoot = $tempRoot }) -StateRoot $preflightState -ArtifactRoot $tempRoot `
                    -LeaseOwner 'contract-preflight' -InstallLocks $locks)
            }
            catch { $preflightRejected = $_.Exception.Message -match 'Refusing to launch because contract already has process' }
            Assert-True $preflightRejected 'A valid ownership set did not reach the existing-process preflight.'
            Assert-True (-not (Test-Path -LiteralPath $preflightState)) 'Existing-process preflight created an install snapshot.'

            $invalidAuthorization = @([pscustomobject]@{ InstallRoot = $locks[0].InstallRoot; Name = $locks[0].Name; OwnershipToken = ''; OwnerProcessId = $PID })
            $invalidRejected = $false
            try { [void](Assert-BenchmarkInstallLockAuthorization -InstallRoot $install -InstallLocks $invalidAuthorization) }
            catch { $invalidRejected = $_.Exception.Message -match 'invalid or expired' }
            Assert-True $invalidRejected 'Malformed install-lock authorization was accepted.'

            $deadOwnerAuthorization = @([pscustomobject]@{ InstallRoot = $locks[0].InstallRoot; Name = $locks[0].Name; OwnershipToken = $locks[0].OwnershipToken; OwnerProcessId = [int]::MaxValue })
            $deadOwnerRejected = $false
            try { [void](Assert-BenchmarkInstallLockAuthorization -InstallRoot $install -InstallLocks $deadOwnerAuthorization) }
            catch { $deadOwnerRejected = $_.Exception.Message -match 'invalid or expired' }
            Assert-True $deadOwnerRejected 'Dead-owner install-lock authorization was accepted.'
        }
        finally { Exit-BenchmarkInstallLocks $locks }
    }

    Invoke-Contract 'suite cancellation and manual recovery preserve restoration boundaries' {
        $entrySource = Get-Content (Join-Path $PSScriptRoot 'Invoke-ShelteredBenchmark.ps1') -Raw
        $restoreSource = Get-Content (Join-Path $PSScriptRoot 'Restore-ShelteredBenchmarkState.ps1') -Raw
        Assert-True ($entrySource -match 'activeJobs[\s\S]*Stop-Job[\s\S]*Receive-Job[\s\S]*Restore-InstallStateSnapshot') 'Suite cancellation no longer drains active platform jobs before install restoration.'
        Assert-True ($restoreSource -match 'Enter-BenchmarkInstallLocks[\s\S]*Refusing to restore.*process\(es\)[\s\S]*Restore-InstallStateSnapshot') 'Manual recovery no longer locks installs, refuses active games, and uses SHA-verified restoration.'
    }

    Invoke-Contract 'manual recovery restores a selected temp install' {
        $recoveryRoot = Join-Path $tempRoot 'recovery-run'
        $recoveryInstall = Join-Path $tempRoot 'recovery-install'
        New-Item -ItemType Directory -Path $recoveryRoot, $recoveryInstall -Force | Out-Null
        'enabled=false' | Set-Content -LiteralPath (Join-Path $recoveryInstall 'doorstop_config.ini') -Encoding UTF8
        $snapshotRoot = Join-Path $recoveryRoot 'suite-install-state-before\mock'
        [void](New-InstallStateSnapshot -InstallRoot $recoveryInstall -BackupRoot $snapshotRoot)
        [pscustomobject]@{
            platforms = @([pscustomobject]@{ name = 'mock'; installRoot = $recoveryInstall; processName = 'ShelteredBenchmarkRecoveryContract_NoProcess' })
        } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $recoveryRoot 'benchmark.config.json') -Encoding UTF8
        'enabled=true' | Set-Content -LiteralPath (Join-Path $recoveryInstall 'doorstop_config.ini') -Encoding UTF8
        & (Join-Path $PSScriptRoot 'Restore-ShelteredBenchmarkState.ps1') -RunRoot $recoveryRoot -Platform mock -Force
        Assert-Equal 'enabled=false' ((Get-Content -LiteralPath (Join-Path $recoveryInstall 'doorstop_config.ini') -Raw).Trim()) 'Selected recovery did not restore the snapshotted payload.'
        Assert-True (Test-Path -LiteralPath (Join-Path $recoveryRoot 'manual_restore_result.json')) 'Selected recovery did not write evidence.'
    }

    Invoke-Contract 'harness URI escaping and ordering are stable' {
        $uri = Resolve-HarnessUri 37421 '/route' @{ value = 'a b'; action = 'open' }
        Assert-Equal 'http://127.0.0.1:37421/route?action=open&value=a%20b' $uri 'Harness URI mismatch.'
    }

    Invoke-Contract 'scenario navigation uses the completion-backed harness route' {
        $harnessSource = Get-Content (Join-Path $PSScriptRoot 'ShelteredBenchmark.Harness.psm1') -Raw
        Assert-True ($harnessSource -match 'ShelteredBenchmarkNativeNavigation[\s\S]*Route ''/scenario-selection/open''[\s\S]*scenarioSlotDispatchToSelectionReadyMs[\s\S]*SelectionResponse = \$response') 'Scenario timing no longer serializes the completion-backed hybrid click route and records its dispatch-to-ready milestone.'
    }

    Invoke-Contract 'matched serial execution is explicit and comparable' {
        $profile = [pscustomobject]@{ name = 'core'; mode = 'core'; harness = $true }
        $execution = Resolve-BenchmarkExecution -Profile $profile -ParallelPlatforms $true -PlatformCount 2 -ForceMatchedSerial
        Assert-Equal 'matched-serial' $execution.ExecutionMode 'Matched comparison did not force serial execution.'
        Assert-Equal 'matched-serial' $execution.ComparisonLane 'Matched comparison did not carry its comparison lane.'
        $entrySource = Get-Content (Join-Path $PSScriptRoot 'Invoke-ShelteredBenchmark.ps1') -Raw
        Assert-True ($entrySource -match 'owned_process\.json[\s\S]*PID was reused by a different process identity[\s\S]*owned benchmark orphan stopped') 'Suite cancellation no longer cleans only exact PID/start-time-owned game processes.'
    }

    Invoke-Contract 'instrumented startup uses the vanilla native readiness gate' {
        $runnerSource = Get-Content (Join-Path $PSScriptRoot 'ShelteredBenchmark.Runner.psm1') -Raw
        Assert-True ($runnerSource -match 'Wait-HarnessMenuReady[\s\S]*HarnessMenuReadyMs[\s\S]*Wait-NativeMenuReady[\s\S]*harness-status\+\$\(\$readiness\.Method\)') 'Instrumented startup no longer records its semantic milestone and then passes the same native readiness gate as vanilla.'
    }

    Invoke-Contract 'failed semantic routes retain live diagnostics' {
        $runnerSource = Get-Content (Join-Path $PSScriptRoot 'ShelteredBenchmark.Runner.psm1') -Raw
        Assert-True ($runnerSource -match 'function Save-HarnessFailureDiagnostics[\s\S]*''/events''[\s\S]*''/flow/custom-draft''[\s\S]*''/ui''') 'Semantic-route failures no longer preserve the event, flow, and UI evidence needed for diagnosis.'
        Assert-True ($runnerSource -match "Prefix 'scenario_selection_failure'[\s\S]*Prefix 'scenario_book_failure'") 'Selection and book failures no longer share the diagnostic capture path.'
    }

    Invoke-Contract 'screenshot persistence rejects non-PNG error bodies' {
        $harnessSource = Get-Content (Join-Path $PSScriptRoot 'ShelteredBenchmark.Harness.psm1') -Raw
        Assert-True ($harnessSource -match 'signature\[0\][\s\S]*0x89[\s\S]*signature\[7\][\s\S]*0x0A[\s\S]*\.error\.json') 'Harness screenshots can again save an error response under a PNG filename.'
    }

    Invoke-Contract 'automated screenshots never activate a game window' {
        $harnessSource = Get-Content (Join-Path $PSScriptRoot 'ShelteredBenchmark.Harness.psm1') -Raw
        $captureStart = $harnessSource.IndexOf('function Save-ShelteredHarnessScreenshot')
        $captureEnd = $harnessSource.IndexOf('function Get-SmoothFpsSummary', $captureStart)
        Assert-True ($captureStart -ge 0 -and $captureEnd -gt $captureStart) 'Screenshot helper boundaries could not be inspected.'
        $captureSource = $harnessSource.Substring($captureStart, $captureEnd - $captureStart)
        Assert-True ($captureSource -match "activate\s*=\s*'false'") 'Automated screenshots no longer explicitly disable foreground activation.'
        Assert-True ($captureSource -notmatch "activate\s*=\s*'true'") 'Automated screenshots can steal foreground focus again.'
    }

    Invoke-Contract 'smooth FPS summary filters perturbing requests' {
        $samples = @(
            [pscustomobject]@{ Ok = $true; RequestMs = 10; SmoothFps = 60 },
            [pscustomobject]@{ Ok = $true; RequestMs = 20; SmoothFps = 30 },
            [pscustomobject]@{ Ok = $true; RequestMs = 150; SmoothFps = 1 },
            [pscustomobject]@{ Ok = $false; RequestMs = 2; SmoothFps = $null }
        )
        $summary = Get-SmoothFpsSummary $samples 100
        Assert-Equal 2 $summary.ValidSamples 'Valid FPS count mismatch.'
        Assert-Equal 30 $summary.MedianSmoothFps 'Lower-median contract changed.'
        Assert-Equal 50 $summary.CoveragePercent 'FPS coverage mismatch.'
        Assert-True (-not $summary.CoverageOk) 'Low-coverage sample was not flagged.'
    }

    Invoke-Contract 'process sampler records without launching a game' {
        $current = Get-Process -Id $PID
        $sampler = Start-BenchmarkProcessSampler $PID ([DateTimeOffset]$current.StartTime.ToUniversalTime()) 50
        Assert-True ($sampler.State.Rows.Count -ge 1) 'Sampler returned before its first successful row.'
        Start-Sleep -Milliseconds 220
        $rows = Stop-BenchmarkProcessSampler $sampler
        Assert-True ($rows.Count -ge 2) 'Sampler did not collect enough rows.'
        Assert-True ($rows[0].WorkingSetBytes -gt 0) 'Sampler did not record working set.'
    }

    Invoke-Contract 'process sampler reports initialization failure explicitly' {
        $rejected = $false
        try { [void](Start-BenchmarkProcessSampler ([int]::MaxValue) ([DateTimeOffset]::UtcNow) 50) }
        catch { $rejected = $_.Exception.Message -match 'initialization failed' }
        Assert-True $rejected 'Invalid PID did not produce an explicit sampler initialization failure.'
    }

    Invoke-Contract 'shared session refuses PID reuse and remains retryable' {
        $owned = Start-Process -FilePath powershell.exe -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 30') -PassThru
        $ownedId = $owned.Id
        try {
            $actualStart = [DateTimeOffset]$owned.StartTime.ToUniversalTime()
            $session = [pscustomobject]@{
                NativeReadinessProbe = $null; LeaseAcquired = $false; Port = 0; LeaseOwner = 'contract'
                ProcessStopped = $false; Process = $owned; ProcessStartUtc = $actualStart.AddMinutes(-5)
                Sampler = $null; Samples = @()
            }
            $errors = @(Stop-ShelteredPlatformSession -Session $session)
            Assert-True (($errors -join ' ') -match 'Refusing to stop reused PID') 'Mismatched process identity was not refused.'
            Assert-True ($null -ne (Get-Process -Id $ownedId -ErrorAction SilentlyContinue)) 'Identity refusal stopped the unrelated process.'
            Assert-True (-not $session.ProcessStopped) 'Identity refusal incorrectly marked ProcessStopped true.'
            $session.ProcessStartUtc = $actualStart
            $errors = @(Stop-ShelteredPlatformSession -Session $session)
            Assert-Equal 0 $errors.Count 'A valid retry did not stop the owned process cleanly.'
            Assert-True $session.ProcessStopped 'Session did not record successful process cleanup.'
        }
        finally {
            $remaining = Get-Process -Id $ownedId -ErrorAction SilentlyContinue
            if ($null -ne $remaining) { Stop-Process -Id $remaining.Id -Force }
        }
    }

    Invoke-Contract 'session boundary requires owned lock and preflights processes before mutation' {
        $runnerSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ShelteredBenchmark.Runner.psm1') -Raw
        Assert-True ($runnerSource -match 'Start-ShelteredPlatformSession[\s\S]*\[object\[\]\]\$InstallLocks') 'Session start no longer requires an install-lock ownership set.'
        $startIndex = $runnerSource.IndexOf('function Start-ShelteredPlatformSession')
        $preflightIndex = $runnerSource.IndexOf('$existingProcesses = @(Get-Process', $startIndex)
        $snapshotIndex = $runnerSource.IndexOf('$session.Snapshot = New-InstallStateSnapshot', $startIndex)
        $mutationIndex = $runnerSource.IndexOf('Set-DoorstopEnabled', $startIndex)
        Assert-True ($preflightIndex -ge 0 -and $preflightIndex -lt $snapshotIndex -and $preflightIndex -lt $mutationIndex) 'Existing-process preflight occurs after snapshot or install mutation.'
        Assert-True ($runnerSource -match 'Transactional cleanup also failed:[\s\S]*transactionErrors') 'Startup cleanup failures are no longer aggregated with the initiating failure.'
    }

    Invoke-Contract 'single process sample retains collection shape' {
        $one = [pscustomobject]@{ ElapsedMs = 10; CpuSeconds = 1; WorkingSetBytes = 1MB; PrivateBytes = 2MB; Threads = 3; Handles = 4 }
        $summary = Get-ProcessSampleSummary -Samples @($one)
        Assert-Equal 1 $summary.Samples 'A one-row sample set collapsed to a scalar.'
        Assert-Equal 0 $summary.CpuSeconds 'Single-row CPU delta must be zero.'
    }

    Invoke-Contract 'runner emits phase and startup-scoped resource metrics' {
        $runnerSource = Get-Content (Join-Path $PSScriptRoot 'ShelteredBenchmark.Runner.psm1') -Raw
        Assert-True ($runnerSource -match 'phase_process_summaries\.csv[\s\S]*StartupPeakWorkingSetMiB[\s\S]*StartupPeakPrivateMiB') 'The runner no longer separates startup attribution from whole-case and per-phase resources.'
        $phaseSamples = @(
            [pscustomobject]@{ Phase = 'startup'; ElapsedMs = 0; CpuSeconds = 1; WorkingSetBytes = 10MB; PrivateBytes = 20MB; Threads = 2; Handles = 3 },
            [pscustomobject]@{ Phase = 'startup'; ElapsedMs = 100; CpuSeconds = 2; WorkingSetBytes = 12MB; PrivateBytes = 22MB; Threads = 4; Handles = 5 },
            [pscustomobject]@{ Phase = 'menu'; ElapsedMs = 200; CpuSeconds = 3; WorkingSetBytes = 11MB; PrivateBytes = 21MB; Threads = 3; Handles = 4 }
        )
        $phaseSummary = @(Get-PhaseProcessSummaries -Samples $phaseSamples)
        Assert-Equal 2 $phaseSummary.Count 'Per-phase sample groups collapsed or disappeared.'
        Assert-Equal 4 $phaseSummary[0].PeakThreads 'Per-phase peak thread attribution is incorrect.'
    }

    Invoke-Contract 'startup timing parser extracts and ranks hotspots' {
        $log = Join-Path $tempRoot 'startup.log'
        @(
            '[19:09:04.424] [INFO ] [StartupTiming] Child operation took 10ms.'
            '[19:09:05.859] [INFO ] [StartupTiming] Parent operation took 1460ms.'
            '[19:09:05.900] [INFO ] [Other] ignored took 9999ms.'
        ) | Set-Content $log -Encoding UTF8
        $timings = Export-ShelteredStartupTimings $log (Join-Path $tempRoot 'timings.csv') (Join-Path $tempRoot 'timings.json')
        Assert-Equal 2 $timings.Count 'Timing count mismatch.'
        Assert-Equal 'Parent operation' (($timings | Sort-Object ElapsedMs -Descending | Select-Object -First 1).Operation) 'Hotspot rank mismatch.'
    }

    Invoke-Contract 'small-sample percentiles retain tail evidence' {
        Assert-Equal 11 (Get-Percentile -Values @(10, 20, 100) -Fraction 0.05) 'P05 flooring hid the lower-tail interpolation.'
        Assert-Equal 92 (Get-Percentile -Values @(10, 20, 100) -Fraction 0.95) 'P95 flooring hid the maximum-side outlier.'
    }

    Invoke-Contract 'aggregate writer emits JSON CSV and Markdown' {
        $reportRoot = Join-Path $tempRoot 'report'
        New-Item -ItemType Directory -Path $reportRoot | Out-Null
        $case = [pscustomobject]@{
            Platform = 'steam'; Profile = 'vanilla'; Iteration = 1; ReadinessMethod = 'native-reference-frame'
            StartupMs = 18000; StartupCpuSeconds = 3.2; PeakWorkingSetMiB = 400
            ScenarioTransitionMs = $null; MenuMedianSmoothFps = $null; MenuP05SmoothFps = $null; Status = 'passed'
        }
        $run = [pscustomobject]@{ RunId = 'contract'; CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o'); ConfigPath = 'test.json' }
        Write-BenchmarkAggregateReport @($case) $reportRoot $run
        foreach ($name in @('README.md', 'results.csv', 'summary.csv', 'manifest.json')) {
            Assert-True (Test-Path (Join-Path $reportRoot $name)) "Missing report artifact $name."
        }
        $markdown = Get-Content (Join-Path $reportRoot 'README.md') -Raw
        Assert-True ($markdown -match 'Native startup median.*Harness-ready median.*Click-to-ready') 'Aggregate Markdown no longer distinguishes comparable startup and scenario-navigation phases.'
    }

    Invoke-Contract 'aggregate filters failed transitions and unmatched execution modes' {
        $reportRoot = Join-Path $tempRoot 'aggregate-validity'
        New-Item -ItemType Directory -Path $reportRoot | Out-Null
        $cases = @(
            [pscustomobject]@{ Platform='steam'; Profile='vanilla'; Mode='vanilla'; ExecutionMode='matched-serial'; ComparisonLane='matched-serial'; Iteration=1; Status='passed'; StartupMs=10000; ScenarioSelectionTransitionOk=$null; ScenarioTransitionOk=$null },
            [pscustomobject]@{ Platform='steam'; Profile='vanilla'; Mode='vanilla'; ExecutionMode='matched-serial'; ComparisonLane='matched-serial'; Iteration=2; Status='passed'; StartupMs=10000; ScenarioSelectionTransitionOk=$null; ScenarioTransitionOk=$null },
            [pscustomobject]@{ Platform='steam'; Profile='core'; Mode='core'; ExecutionMode='matched-serial'; ComparisonLane='matched-serial'; Iteration=1; Status='passed'; StartupMs=11000; ScenarioSelectionTransitionOk=$true; ScenarioSelectionRouteMs=400; ScenarioSelectionTransitionMs=120; ScenarioTransitionOk=$true; ScenarioTransitionMs=130 },
            [pscustomobject]@{ Platform='steam'; Profile='core'; Mode='core'; ExecutionMode='matched-serial'; ComparisonLane='matched-serial'; Iteration=2; Status='partial'; StartupMs=11200; ScenarioSelectionTransitionOk=$false; ScenarioSelectionFailureElapsedMs=120000; ScenarioTransitionOk=$false; ScenarioTransitionFailureElapsedMs=120000 },
            [pscustomobject]@{ Platform='steam'; Profile='parallel-core'; Mode='core'; ExecutionMode='parallel-platforms'; ComparisonLane=''; Iteration=1; Status='passed'; StartupMs=9000; ScenarioSelectionTransitionOk=$null; ScenarioTransitionOk=$null }
        )
        Write-BenchmarkAggregateReport -Results $cases -OutputRoot $reportRoot -RunMetadata ([pscustomobject]@{ RunId='validity'; CreatedAtUtc=[DateTimeOffset]::UtcNow.ToString('o'); ConfigPath='test' })
        $summary = @(Import-Csv -LiteralPath (Join-Path $reportRoot 'summary.csv'))
        $serial = $summary | Where-Object { $_.Profile -eq 'core' -and $_.ExecutionMode -eq 'matched-serial' }
        $parallel = $summary | Where-Object Profile -EQ 'parallel-core'
        Assert-Equal '1' $serial.ScenarioSelectionRouteSamples 'Failed selection duration contaminated the successful route sample count.'
        Assert-Equal '120' $serial.ScenarioSelectionMedianMs 'Failed selection duration contaminated the successful route median.'
        Assert-Equal '1' $serial.ScenarioSelectionFailureSamples 'Selection failure evidence was not retained separately.'
        Assert-Equal '2' $serial.StartupPairedDeltaSamples 'Matched startup deltas were not paired by iteration.'
        Assert-Equal '1100' $serial.StartupDeltaVsVanillaMs 'Matched serial vanilla delta was not emitted.'
        Assert-True ([string]::IsNullOrWhiteSpace($parallel.StartupDeltaVsVanillaMs)) 'Unmatched parallel execution received a serial vanilla delta.'
    }

    Write-Host "All $script:passed Sheltered benchmark contracts passed."
}
finally {
    $verified = [IO.Path]::GetFullPath($tempRoot)
    if ($verified.StartsWith($resolvedTempBase, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $verified)) {
        Remove-Item -LiteralPath $verified -Recurse -Force
    }
}
