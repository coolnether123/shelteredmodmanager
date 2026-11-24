[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SmmPath,
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [string]$StagingRoot = [IO.Path]::GetTempPath()
)

$ErrorActionPreference = 'Stop'
$current = (Get-Item -LiteralPath $SmmPath).FullName
$package = (Get-Item -LiteralPath $PackagePath).FullName
$manager = Join-Path $current 'Manager.exe'
if ((Split-Path -Leaf $current) -ne 'SMM' -or
    -not (Test-Path -LiteralPath (Join-Path $current 'ManagerUpdater.exe') -PathType Leaf) -or
    [Reflection.AssemblyName]::GetAssemblyName($manager).Version -ne [Version]'2.0.0.0' -or
    (Test-Path -LiteralPath (Join-Path $current 'manager-update.hashes'))) {
    throw 'Select the SMM folder of your RC6 installation.'
}
if ((Get-Item -LiteralPath $current).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'Select the actual SMM folder on disk.'
}
if (Get-Process -Name 'Manager', 'Sheltered', 'ShelteredWindows64_EOS' -ErrorAction SilentlyContinue) {
    throw 'Close Sheltered and all Sheltered Mod Manager windows before upgrading.'
}

$checksum = Get-Content -LiteralPath ($package + '.sha256') -Raw
if ($checksum -notmatch '\A([a-fA-F0-9]{64})(?:[ \t]+[^\r\n]+)?\s*\z') {
    throw 'The update ZIP needs its published .sha256 file beside it.'
}
$expected = $Matches[1]
$game = Split-Path -Parent $current
$updateRoot = Join-Path $game '_smm_update'
$id = [Guid]::NewGuid().ToString('N')
$backup = Join-Path $updateRoot "rc6-backup-$id"
$workspace = Join-Path (Get-Item -LiteralPath $StagingRoot).FullName "smm-rc6-upgrade-$id"
if ($workspace.StartsWith($current + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The staging folder must be outside SMM.'
}

New-Item -ItemType Directory -Path $workspace | Out-Null
try {
    $archive = Join-Path $workspace 'update.zip'
    Copy-Item -LiteralPath $package -Destination $archive
    $sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    if ($sha256 -ne $expected) {
        throw 'The update ZIP does not match its published SHA-256 checksum.'
    }
    if ($sha256 -eq 'B3B7336A76D83521587AE5758ADF77861C065B995CC093B1252C85BEF03362C3') {
        throw 'You selected the RC6 update ZIP. Download the final 2.0.0 update ZIP.'
    }

    $extract = Join-Path $workspace 'extract'
    Expand-Archive -LiteralPath $archive -DestinationPath $extract
    $staged = Join-Path $extract 'SMM'
    $topLevel = @(Get-ChildItem -LiteralPath $extract -Force)
    if ($topLevel.Count -ne 1 -or $topLevel[0].Name -ne 'SMM' -or
        [Reflection.AssemblyName]::GetAssemblyName((Join-Path $staged 'Manager.exe')).Version -ne [Version]'2.0.0.0') {
        throw 'Select the final 2.0.0 Update ZIP from the release.'
    }

    $md5 = (Get-FileHash -LiteralPath $archive -Algorithm MD5).Hash
    Set-Content -LiteralPath (Join-Path $staged 'manager-update.hashes') -Encoding ASCII -Value @("sha256:$sha256", "md5:$md5")
    $updater = Join-Path $workspace 'ManagerUpdater.exe'
    Copy-Item -LiteralPath (Join-Path $staged 'ManagerUpdater.exe') -Destination $updater

    $arguments = @(
        '--parent-pid', '0',
        '--current', ('"{0}"' -f $current),
        '--staged', ('"{0}"' -f $staged),
        '--backup', ('"{0}"' -f $backup),
        '--restart', ('"{0}"' -f $manager),
        '--restart-on-rollback', 'false'
    )
    # Wait for the updater alone. Its restarted Manager window may stay open.
    $process = Start-Process -FilePath $updater -ArgumentList $arguments -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "The updater returned exit code $($process.ExitCode). If SMM is missing, check the rollback folder at $backup."
    }
    Write-Host "Upgrade complete. Keep this RC6 backup: $backup"
}
finally {
    Remove-Item -LiteralPath $workspace -Recurse -Force
}
