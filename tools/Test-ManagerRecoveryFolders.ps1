param([string]$RepoRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$work = Join-Path ([IO.Path]::GetTempPath()) ('smm-recovery-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $assembly = [Reflection.Assembly]::LoadFile((Join-Path $RepoRoot 'Dist\SMM\ManagerUpdater.exe'))
    $flags = [Reflection.BindingFlags]'Static,NonPublic,Public'
    $current = Join-Path $work 'SMM'
    $stage = Join-Path $work 'promotion-active'
    $backup = Join-Path $work 'backup_current'
    $promotion = Join-Path $work 'promotion-current'
    $options = $assembly.GetType('ManagerUpdater.UpdateArguments').GetMethod('Parse', $flags).Invoke($null, [object[]]@(,[string[]]@('--parent-pid','2147483646','--current',$current,'--staged',$stage,'--backup',$backup,'--restart',(Join-Path $current 'Manager.exe'))))
    $logPath = Join-Path $work 'cleanup.log'
    $log = $assembly.GetType('ManagerUpdater.UpdateLog').GetConstructor([type[]]@([string])).Invoke([object[]]@([string]$logPath))
    foreach ($name in @('failed-update-old','failed-update-new','promotion-old','promotion-new','promotion-active','promotion-current','unrelated')) {
        $folder = New-Item -ItemType Directory -Path (Join-Path $work $name)
        Set-Content -LiteralPath (Join-Path $folder.FullName 'data') -Value 'recovery'
        $folder.LastWriteTimeUtc = [DateTime]::UtcNow.AddDays(-2)
        if ($name.EndsWith('-new')) { $folder.LastWriteTimeUtc = [DateTime]::UtcNow.AddDays(-1) }
    }
    try {
        $assembly.GetType('ManagerUpdater.Program').GetMethod('CleanupRecoveryFolders', $flags).Invoke($null, [object[]]@($options.PSObject.BaseObject,[string]$promotion,$log.PSObject.BaseObject))
    } finally { $log.Dispose() }
    foreach ($name in @('failed-update-new','promotion-new','promotion-active','promotion-current','unrelated')) {
        if (-not (Test-Path -LiteralPath (Join-Path $work $name))) { throw "Protected folder removed: $name" }
    }
    foreach ($name in @('failed-update-old','promotion-old')) {
        if (Test-Path -LiteralPath (Join-Path $work $name)) { throw "Stale folder retained: $name" }
    }
    if (@(Select-String -LiteralPath $logPath -Pattern 'Removed stale recovery folder').Count -ne 2) { throw 'Removal log count differs.' }
    Write-Output 'Recovery folder checks passed: 8 assertions, 0 failed.'
} finally {
    if ([IO.Path]::GetFullPath($work).StartsWith([IO.Path]::GetTempPath(), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $work -Recurse -Force
    }
}
