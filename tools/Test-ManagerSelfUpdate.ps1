param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$CandidatePackage,
    [string]$PublishedRc6Package
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath($RepoRoot)
$updater = Join-Path $repoRoot 'Dist\SMM\ManagerUpdater.exe'
if (-not (Test-Path -LiteralPath $updater)) {
    throw "ManagerUpdater.exe was not built: $updater"
}

$testRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot ('artifacts\manager-update-test-' + [Guid]::NewGuid().ToString('N'))))
$crossVolumeStageRoot = [IO.Path]::GetFullPath((Join-Path $env:TEMP ('smm-cross-volume-stage-' + [Guid]::NewGuid().ToString('N'))))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
$tempRoot = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + [IO.Path]::DirectorySeparatorChar
if (-not $testRoot.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a self-update test folder outside artifacts: $testRoot"
}
if (-not $crossVolumeStageRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a cross-volume stage folder outside TEMP: $crossVolumeStageRoot"
}

function New-TestTree {
    param(
        [string]$Root,
        [string]$ManagerPath
    )

    New-Item -ItemType Directory -Path (Join-Path $Root 'bin') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $Root 'Doorstop\x86') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $Root 'Doorstop\x64') -Force | Out-Null
    if ([string]::IsNullOrEmpty($ManagerPath)) { $ManagerPath = $script:fakeManagerPath }
    Copy-Item -LiteralPath $ManagerPath -Destination (Join-Path $Root 'Manager.exe')
    @(
        'ManagerUpdater.exe',
        'ModAPI.dll',
        'bin\ShelteredAPI.dll',
        'bin\Doorstop.dll',
        'bin\0Harmony.dll',
        'Doorstop\x86\winhttp.dll',
        'Doorstop\x64\winhttp.dll'
    ) | ForEach-Object {
        Set-Content -LiteralPath (Join-Path $Root $_) -Value 'test payload' -Encoding ASCII
    }
}

function Invoke-Updater {
    param(
        [string]$Current,
        [string]$Staged,
        [string]$Backup
    )

    $arguments = @(
        '--parent-pid', '2147483646',
        '--current', ('"{0}"' -f $Current),
        '--staged', ('"{0}"' -f $Staged),
        '--backup', ('"{0}"' -f $Backup),
        '--restart', ('"{0}"' -f (Join-Path $Current 'Manager.exe'))
    )
    $process = Start-Process -FilePath $updater -ArgumentList $arguments -Wait -PassThru
    return $process.ExitCode
}

function Assert-SameFile([string]$Left, [string]$Right) {
    if ((Get-FileHash -LiteralPath $Left).Hash -ne (Get-FileHash -LiteralPath $Right).Hash) {
        throw "File changed during update: $Right"
    }
}

function Test-Rc6Upgrade {
    $rc6Zip = $PublishedRc6Package
    if (-not $rc6Zip) {
        $asset = 'ShelteredModManager-2.0.0-Update.zip'
        $url = "https://github.com/coolnether123/shelteredmodmanager/releases/download/v2.0.0-rc.6/$asset"
        $rc6Zip = Join-Path $testRoot $asset
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $rc6Zip
        Invoke-WebRequest -UseBasicParsing -Uri "$url.sha256" -OutFile "$rc6Zip.sha256"
    }
    $publishedHash = 'B3B7336A76D83521587AE5758ADF77861C065B995CC093B1252C85BEF03362C3'
    $sidecarHash = ((Get-Content -LiteralPath "$rc6Zip.sha256" -Raw).Trim() -split '\s+')[0]
    if ($sidecarHash -ne $publishedHash -or (Get-FileHash -LiteralPath $rc6Zip).Hash -ne $publishedHash) {
        throw 'The RC6 ZIP and sidecar must match the checksum on the GitHub release page.'
    }
    $rc6Extract = Join-Path $testRoot 'published-rc6'
    Expand-Archive -LiteralPath $rc6Zip -DestinationPath $rc6Extract
    $original = Join-Path $rc6Extract 'SMM'
    $rc6Manager = Join-Path $original 'Manager.exe'
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($rc6Manager).Version
    $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($rc6Manager).FileVersion
    if ($assemblyVersion -ne [Version]'2.0.0.0') { throw "Unexpected RC6 assembly version: $assemblyVersion" }
    Write-Host "Verified published RC6 ZIP $publishedHash. Assembly version $assemblyVersion, file version $fileVersion."

    $insideSmm = @('SMM\bin\mod_manager.ini', 'SMM\bin\manager_options.json',
        'SMM\nexus_uploads\custom.zip', 'SMM\bin\_nxm_inbox\pending.txt')
    $outsideSmm = @('mods\loadorder.json', 'mods\ModAPI\Saves\Standard\Slot_1\SaveData.xml',
        'mods\ModAPI\Saves\custom-scenario\Slot_2\SaveData.xml', 'save\savedata_01.dat')
    $fixture = Join-Path $testRoot 'fixture'
    New-Item -ItemType Directory -Path $fixture | Out-Null
    Copy-Item -LiteralPath $original -Destination $fixture -Recurse
    foreach ($relative in $insideSmm + $outsideSmm) {
        $file = Join-Path $fixture $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null
        Set-Content -LiteralPath $file -Value "synthetic fixture for $relative"
    }

    # The success case uses the built final updater and a headless stand-in
    # for the final manager's health handshake. The rollback case uses the
    # unmodified candidate ZIP and fails before starting either manager.
    $headlessExtract = Join-Path $testRoot 'headless-candidate'
    Expand-Archive -LiteralPath $CandidatePackage -DestinationPath $headlessExtract
    $headlessSmm = Join-Path $headlessExtract 'SMM'
    $headlessManager = Join-Path $testRoot 'FakeFinalManager.exe'
    Add-Type -TypeDefinition ($fakeManagerSource.Replace('FakeHealthyManager', 'FakeFinalManager').Replace(
        'public static class', '[assembly: System.Reflection.AssemblyVersion("2.0.0.0")]' + "`r`npublic static class")) `
        -OutputAssembly $headlessManager -OutputType WindowsApplication
    Copy-Item -LiteralPath $headlessManager -Destination (Join-Path $headlessSmm 'Manager.exe') -Force
    $headlessZip = Join-Path $testRoot 'headless-final.zip'
    Compress-Archive -LiteralPath $headlessSmm -DestinationPath $headlessZip
    $hash = (Get-FileHash -LiteralPath $headlessZip).Hash
    Set-Content -LiteralPath "$headlessZip.sha256" -Value $hash -Encoding ASCII

    New-Item -ItemType Directory -Path $crossVolumeStageRoot -Force | Out-Null
    $upgrade = Join-Path $repoRoot 'tools\Upgrade-SmmRc6.ps1'
    foreach ($mode in @('rollback', 'success')) {
        $install = Join-Path $testRoot "RC6 $mode"
        Copy-Item -LiteralPath $fixture -Destination $install -Recurse
        $currentSmm = Join-Path $install 'SMM'
        $zip = if ($mode -eq 'success') { $headlessZip } else { $CandidatePackage }
        if ($mode -eq 'rollback') { $env:SMM_UPDATER_TEST_FAIL_BEFORE_RESTART = '1' }
        $upgradeError = $null
        $previousTemp = $env:TEMP
        $previousTmp = $env:TMP
        $env:TEMP = $crossVolumeStageRoot
        $env:TMP = $crossVolumeStageRoot
        try { & $upgrade -SmmPath $currentSmm -PackagePath $zip -StagingRoot $crossVolumeStageRoot }
        catch { $upgradeError = $_ }
        finally {
            $env:TEMP = $previousTemp
            $env:TMP = $previousTmp
            Remove-Item Env:SMM_UPDATER_TEST_FAIL_BEFORE_RESTART -ErrorAction SilentlyContinue
        }

        if ($mode -eq 'success') {
            if ($upgradeError) { throw $upgradeError }
        }
        elseif (-not $upgradeError -or $upgradeError.Exception.Message -notmatch 'updater returned exit code') {
            throw "Expected the candidate updater to fail after promotion: $upgradeError"
        }
        $backups = @(Get-ChildItem -LiteralPath (Join-Path $install '_smm_update') -Directory -Filter 'rc6-backup-*')
        if ($mode -eq 'success') {
            Get-Process -Name Manager -ErrorAction SilentlyContinue |
                Where-Object { $_.Path -eq (Join-Path $currentSmm 'Manager.exe') } |
                ForEach-Object { $_.WaitForExit() }
            if ($backups.Count -ne 1) { throw 'The RC6 rollback copy was not retained.' }
            Assert-SameFile $rc6Manager (Join-Path $backups[0].FullName 'Manager.exe')
            Assert-SameFile $headlessManager (Join-Path $currentSmm 'Manager.exe')
            if ((Get-Content -LiteralPath (Join-Path $currentSmm 'manager-update.hashes')) -notcontains "sha256:$hash") {
                throw 'The upgraded manager did not record its verified package hash.'
            }
        }
        else {
            $logs = Get-ChildItem -LiteralPath (Join-Path $crossVolumeStageRoot 'ShelteredModManager') -Filter 'ManagerUpdater-*.log'
            $rollbackLog = Get-Content -LiteralPath $logs.FullName -Raw
            if ($backups.Count -ne 0 -or $rollbackLog -notmatch 'Injected updater restart failure' -or
                $rollbackLog -notmatch 'Restored the previous installation') {
                throw 'RC6 was not restored after the injected promotion failure.'
            }
            Assert-SameFile $rc6Manager (Join-Path $currentSmm 'Manager.exe')
            Assert-SameFile (Join-Path $original 'ManagerUpdater.exe') (Join-Path $currentSmm 'ManagerUpdater.exe')
            if (Test-Path -LiteralPath (Join-Path $currentSmm 'manager-update.hashes')) {
                throw 'Rollback left the candidate package receipt in RC6.'
            }
        }
        foreach ($relative in $insideSmm + $outsideSmm) {
            Assert-SameFile (Join-Path $fixture $relative) (Join-Path $install $relative)
        }
    }
    $volumeKind = if ([IO.Path]::GetPathRoot($testRoot) -ne [IO.Path]::GetPathRoot($crossVolumeStageRoot)) {
        'different drive letters'
    } else { 'same drive letter' }
    Write-Host "Published RC6 migration passed: 2 scenarios, staging on $volumeKind."
}

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
    $script:fakeManagerPath = Join-Path $testRoot 'FakeHealthyManager.exe'
    $fakeManagerSource = @'
using System;
using System.IO;

public static class FakeHealthyManager
{
    public static int Main(string[] args)
    {
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (string.Equals(args[i], "--update-health-file", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(args[i + 1], "ready");
                System.Threading.Thread.Sleep(2000);
                return 0;
            }
        }
        return 0;
    }
}
'@
    Add-Type -TypeDefinition $fakeManagerSource -OutputAssembly $script:fakeManagerPath -OutputType ConsoleApplication
    $unhealthyManagerPath = Join-Path $testRoot 'FakeUnhealthyManager.exe'
    Add-Type -TypeDefinition 'public static class FakeUnhealthyManager { public static int Main(string[] args) { return 0; } }' -OutputAssembly $unhealthyManagerPath -OutputType ConsoleApplication
    $earlyExitManagerPath = Join-Path $testRoot 'FakeEarlyExitManager.exe'
    Add-Type -TypeDefinition 'public static class FakeEarlyExitManager { public static void Main(string[] args) { for (int i=0; i+1<args.Length; i++) if(args[i]=="--update-health-file") { System.IO.File.WriteAllText(args[i+1], "ready"); return; } } }' -OutputAssembly $earlyExitManagerPath -OutputType ConsoleApplication
    $env:SMM_UPDATER_NO_UI = '1'

    $successRoot = Join-Path $testRoot 'success'
    $current = Join-Path $successRoot 'SMM'
    $staged = Join-Path $successRoot 'staged-SMM'
    $backup = Join-Path $successRoot 'backup-SMM'
    New-TestTree -Root $current
    New-TestTree -Root $staged
    Set-Content -LiteralPath (Join-Path $current 'old-stale.dll') -Value 'old' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $staged 'new-version.dll') -Value 'new' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $current 'bin\mod_manager.ini') -Value 'private-key-data' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $current 'bin\manager_options.json') -Value '{"dark":true}' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $current 'mod_manager.log') -Value 'existing-log' -Encoding ASCII
    New-Item -ItemType Directory -Path (Join-Path $current 'Feedback') | Out-Null
    Set-Content -LiteralPath (Join-Path $current 'Feedback\report.txt') -Value 'feedback' -Encoding ASCII

    foreach ($relative in @('nexus_uploads', 'bin\_nxm_inbox')) {
        New-Item -ItemType Directory -Path (Join-Path $current $relative) | Out-Null
        Set-Content -LiteralPath (Join-Path (Join-Path $current $relative) 'pending.txt') -Value 'pending'
    }
    foreach ($prefix in @('failed-update-', 'promotion-')) {
        foreach ($age in @('old', 'new')) {
            $folder = New-Item -ItemType Directory -Path (Join-Path $successRoot ($prefix + $age))
            Set-Content -LiteralPath (Join-Path $folder.FullName 'recovery.txt') -Value 'recovery'
            $folder.LastWriteTimeUtc = if ($age -eq 'old') { [DateTime]::UtcNow.AddDays(-2) } else { [DateTime]::UtcNow.AddDays(-1) }
        }
    }
    $exitCode = Invoke-Updater -Current $current -Staged $staged -Backup $backup
    if ($exitCode -ne 0) { throw "Successful update returned exit code $exitCode." }
    if (-not (Test-Path -LiteralPath (Join-Path $current 'new-version.dll'))) { throw 'Staged payload was not promoted.' }
    if (Test-Path -LiteralPath (Join-Path $current 'old-stale.dll')) { throw 'Stale binaries survived the directory replacement.' }
    if ((Get-Content -LiteralPath (Join-Path $current 'bin\mod_manager.ini') -Raw).Trim() -ne 'private-key-data') { throw 'INI settings were not preserved.' }
    if ((Get-Content -LiteralPath (Join-Path $current 'bin\manager_options.json') -Raw).Trim() -ne '{"dark":true}') { throw 'Manager options were not preserved.' }
    if ((Get-Content -LiteralPath (Join-Path $current 'mod_manager.log') -Raw).Trim() -ne 'existing-log') { throw 'Manager log was not preserved.' }
    if (-not (Test-Path -LiteralPath (Join-Path $current 'Feedback\report.txt'))) { throw 'Feedback files were not preserved.' }
    if (-not (Test-Path -LiteralPath (Join-Path $backup 'old-stale.dll'))) { throw 'Rollback backup was not retained.' }

    foreach ($relative in @('nexus_uploads', 'bin\_nxm_inbox')) {
        if ((Get-Content -LiteralPath (Join-Path (Join-Path $current $relative) 'pending.txt') -Raw).Trim() -ne 'pending') { throw "Local state lost: $relative" }
    }
    foreach ($prefix in @('failed-update-', 'promotion-')) {
        if (Test-Path -LiteralPath (Join-Path $successRoot ($prefix + 'old'))) { throw "Stale $prefix folder survived successful startup." }
        if (-not (Test-Path -LiteralPath (Join-Path $successRoot ($prefix + 'new')))) { throw "Newest $prefix recovery folder was removed." }
    }
    $rollbackRoot = Join-Path $testRoot 'rollback'
    $rollbackCurrent = Join-Path $rollbackRoot 'SMM'
    $rollbackStaged = Join-Path $rollbackRoot 'staged-SMM'
    $rollbackBackup = Join-Path $rollbackRoot 'backup-SMM'
    New-TestTree -Root $rollbackCurrent
    New-TestTree -Root $rollbackStaged
    Set-Content -LiteralPath (Join-Path $rollbackCurrent 'known-good.dll') -Value 'good' -Encoding ASCII

    $env:SMM_UPDATER_TEST_FAIL_BEFORE_RESTART = '1'
    $exitCode = Invoke-Updater -Current $rollbackCurrent -Staged $rollbackStaged -Backup $rollbackBackup
    Remove-Item Env:SMM_UPDATER_TEST_FAIL_BEFORE_RESTART -ErrorAction SilentlyContinue
    if ($exitCode -eq 0) { throw 'An injected restart failure unexpectedly succeeded.' }
    if (-not (Test-Path -LiteralPath (Join-Path $rollbackCurrent 'known-good.dll'))) { throw 'Rollback did not restore the known-good installation.' }
    if (Test-Path -LiteralPath $rollbackBackup) { throw 'Rollback left the old installation stranded at the backup path.' }

    $healthRoot = Join-Path $testRoot 'health-rollback'
    $healthCurrent = Join-Path $healthRoot 'SMM'
    $healthStaged = Join-Path $healthRoot 'staged-SMM'
    $healthBackup = Join-Path $healthRoot 'backup-SMM'
    New-TestTree -Root $healthCurrent
    New-TestTree -Root $healthStaged -ManagerPath $unhealthyManagerPath
    Set-Content -LiteralPath (Join-Path $healthCurrent 'known-healthy.dll') -Value 'healthy' -Encoding ASCII

    $exitCode = Invoke-Updater -Current $healthCurrent -Staged $healthStaged -Backup $healthBackup
    if ($exitCode -eq 0) { throw 'A manager that omitted the health handshake unexpectedly succeeded.' }
    if (-not (Test-Path -LiteralPath (Join-Path $healthCurrent 'known-healthy.dll'))) { throw 'Health-check failure did not restore the previous manager.' }

    $earlyRoot = Join-Path $testRoot 'early-exit'
    $earlyCurrent = Join-Path $earlyRoot 'SMM'
    $earlyStage = Join-Path $earlyRoot 'staged'
    $earlyBackup = Join-Path $earlyRoot 'backup'
    New-TestTree -Root $earlyCurrent
    New-TestTree -Root $earlyStage -ManagerPath $earlyExitManagerPath
    Set-Content -LiteralPath (Join-Path $earlyCurrent 'known-good.dll') -Value 'good'
    $exitCode = Invoke-Updater -Current $earlyCurrent -Staged $earlyStage -Backup $earlyBackup
    if ($exitCode -eq 0 -or -not (Test-Path -LiteralPath (Join-Path $earlyCurrent 'known-good.dll'))) { throw 'An immediate exit after readiness must restore the old installation.' }

    $crossRoot = Join-Path $testRoot 'cross-volume'
    $crossCurrent = Join-Path $crossRoot 'SMM'
    $crossBackup = Join-Path $crossRoot 'backup-SMM'
    New-TestTree -Root $crossCurrent
    New-TestTree -Root $crossVolumeStageRoot
    Set-Content -LiteralPath (Join-Path $crossVolumeStageRoot 'cross-volume.dll') -Value 'promoted' -Encoding ASCII

    $exitCode = Invoke-Updater -Current $crossCurrent -Staged $crossVolumeStageRoot -Backup $crossBackup
    if ($exitCode -ne 0) { throw "Cross-volume-capable update returned exit code $exitCode." }
    if (-not (Test-Path -LiteralPath (Join-Path $crossCurrent 'cross-volume.dll'))) { throw 'Cross-volume staging payload was not promoted.' }

    Write-Host 'Manager self-update process tests passed: 5 scenarios.'
    if ($CandidatePackage) { Test-Rc6Upgrade }
}
finally {
    Remove-Item Env:SMM_UPDATER_NO_UI -ErrorAction SilentlyContinue
    Remove-Item Env:SMM_UPDATER_TEST_FAIL_BEFORE_RESTART -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $crossVolumeStageRoot) {
        Remove-Item -LiteralPath $crossVolumeStageRoot -Recurse -Force
    }
}
