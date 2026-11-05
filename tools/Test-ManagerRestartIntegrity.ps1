param([string]$RepoRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$work = Join-Path ([IO.Path]::GetTempPath()) ('smm-restart-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $fixture = Join-Path $work 'RestartFixture.exe'
    $marker = Join-Path $work 'integrity.txt'
    Add-Type -OutputAssembly $fixture -OutputType WindowsApplication -TypeDefinition @'
using System;
using System.IO;
using System.Security.Principal;
public static class RestartFixture {
    public static void Main(string[] args) {
        File.WriteAllText(args[0], new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator).ToString());
        System.Threading.Thread.Sleep(1500);
    }
}
'@
    $assembly = [Reflection.Assembly]::LoadFile((Join-Path $RepoRoot 'Dist\SMM\ManagerUpdater.exe'))
    $log = $assembly.GetType('ManagerUpdater.UpdateLog').GetConstructor([type[]]@([string])).Invoke([object[]]@([string](Join-Path $work 'restart.log')))
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $fixture
    $start.WorkingDirectory = $work
    $start.Arguments = '"' + $marker + '"'
    $start.UseShellExecute = $false
    try {
        $process = $assembly.GetType('ManagerUpdater.ManagerProcessLauncher').GetMethod('Start').Invoke($null, [object[]]@($start.PSObject.BaseObject,$log.PSObject.BaseObject))
        try {
            if (-not $process.WaitForExit(10000)) { throw 'Synthetic restart did not exit.' }
            if ($process.ExitCode -ne 0) { throw 'Synthetic restart failed.' }
        } finally { $process.Dispose() }
    } finally { $log.Dispose() }
    if ([IO.File]::ReadAllText($marker) -ne 'False') { throw 'Restarted process was elevated.' }
    $source = Get-Content -LiteralPath (Join-Path $RepoRoot 'ManagerUpdater\Program.cs') -Raw
    if ([regex]::Matches($source, 'ManagerProcessLauncher.Start\(start(?:Info)?, log\)').Count -ne 2) { throw 'Both restart paths must use the integrity-aware launcher.' }
    $elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    Write-Output "Restart integrity checks passed: 4 assertions, 0 failed. Elevated caller: $elevated."
} finally {
    if ([IO.Path]::GetFullPath($work).StartsWith([IO.Path]::GetTempPath(), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $work -Recurse -Force
    }
}
