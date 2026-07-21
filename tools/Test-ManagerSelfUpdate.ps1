param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
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
        '--current', $Current,
        '--staged', $Staged,
        '--backup', $Backup,
        '--restart', (Join-Path $Current 'Manager.exe')
    )
    $process = Start-Process -FilePath $updater -ArgumentList $arguments -Wait -PassThru
    return $process.ExitCode
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

    $exitCode = Invoke-Updater -Current $current -Staged $staged -Backup $backup
    if ($exitCode -ne 0) { throw "Successful update returned exit code $exitCode." }
    if (-not (Test-Path -LiteralPath (Join-Path $current 'new-version.dll'))) { throw 'Staged payload was not promoted.' }
    if (Test-Path -LiteralPath (Join-Path $current 'old-stale.dll')) { throw 'Stale binaries survived the directory replacement.' }
    if ((Get-Content -LiteralPath (Join-Path $current 'bin\mod_manager.ini') -Raw).Trim() -ne 'private-key-data') { throw 'INI settings were not preserved.' }
    if ((Get-Content -LiteralPath (Join-Path $current 'bin\manager_options.json') -Raw).Trim() -ne '{"dark":true}') { throw 'Manager options were not preserved.' }
    if ((Get-Content -LiteralPath (Join-Path $current 'mod_manager.log') -Raw).Trim() -ne 'existing-log') { throw 'Manager log was not preserved.' }
    if (-not (Test-Path -LiteralPath (Join-Path $current 'Feedback\report.txt'))) { throw 'Feedback files were not preserved.' }
    if (-not (Test-Path -LiteralPath (Join-Path $backup 'old-stale.dll'))) { throw 'Rollback backup was not retained.' }

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

    $crossRoot = Join-Path $testRoot 'cross-volume'
    $crossCurrent = Join-Path $crossRoot 'SMM'
    $crossBackup = Join-Path $crossRoot 'backup-SMM'
    New-TestTree -Root $crossCurrent
    New-TestTree -Root $crossVolumeStageRoot
    Set-Content -LiteralPath (Join-Path $crossVolumeStageRoot 'cross-volume.dll') -Value 'promoted' -Encoding ASCII

    $exitCode = Invoke-Updater -Current $crossCurrent -Staged $crossVolumeStageRoot -Backup $crossBackup
    if ($exitCode -ne 0) { throw "Cross-volume-capable update returned exit code $exitCode." }
    if (-not (Test-Path -LiteralPath (Join-Path $crossCurrent 'cross-volume.dll'))) { throw 'Cross-volume staging payload was not promoted.' }

    Write-Host 'Manager self-update process tests passed.'
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
