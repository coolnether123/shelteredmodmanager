[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Steam', 'Epic')]
    [string]$Lane,

    [Parameter(Mandatory = $true)]
    [string]$EvidenceRoot
)

$ErrorActionPreference = 'Stop'
$gameRoot = if ($Lane -eq 'Steam') { 'A:\SteamLibrary\steamapps\common\Sheltered' } else { 'D:\Epic Games Games\Sheltered' }
$gameRoot = [IO.Path]::GetFullPath($gameRoot).TrimEnd('\')
$gameExe = if ($Lane -eq 'Steam') { 'Sheltered.exe' } else { 'ShelteredWindows64_EOS.exe' }
$gameExePath = [IO.Path]::GetFullPath((Join-Path $gameRoot $gameExe))
$gameProcessName = [IO.Path]::GetFileNameWithoutExtension($gameExe)
$ownedProcesses = @(Get-Process -Name $gameProcessName -ErrorAction SilentlyContinue | Where-Object {
    try { [IO.Path]::GetFullPath($_.Path) -eq $gameExePath } catch { $false }
})
if ($ownedProcesses.Count -gt 0) { throw "$Lane Sheltered is running; refusing to restore live mod folders." }
$modsRoot = Join-Path $gameRoot 'mods'
$evidence = [IO.Path]::GetFullPath($EvidenceRoot)
$original = Join-Path $evidence 'original-state'
$statePath = Join-Path $evidence 'deployment-state.json'
if (-not (Test-Path -LiteralPath $statePath) -or -not (Test-Path -LiteralPath $original)) {
    throw 'Deployment state or original-state backup is missing.'
}
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
if ($state.gameRoot -ne $gameRoot -or $state.lane -ne $Lane) { throw 'Restore state targets a different lane.' }

foreach ($folder in $state.folders) {
    $liveFolder = [IO.Path]::GetFullPath((Join-Path $modsRoot $folder.folder))
    if (-not $liveFolder.StartsWith(([IO.Path]::GetFullPath($modsRoot).TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing out-of-mods target: $liveFolder"
    }
    if (Test-Path -LiteralPath $liveFolder) { Remove-Item -LiteralPath $liveFolder -Recurse -Force }
    if ($folder.originallyExisted) {
        $backupFolder = Join-Path (Join-Path $original 'mods') $folder.folder
        New-Item -ItemType Directory -Path $liveFolder -Force | Out-Null
        Copy-Item -Path (Join-Path $backupFolder '*') -Destination $liveFolder -Recurse -Force
    }
}

$modApiLive = Join-Path $modsRoot 'ModAPI'
if (Test-Path -LiteralPath $modApiLive) { Remove-Item -LiteralPath $modApiLive -Recurse -Force }
if ($state.modApiOriginallyExisted) {
    New-Item -ItemType Directory -Path $modApiLive -Force | Out-Null
    Copy-Item -Path (Join-Path (Join-Path $original 'ModAPI') '*') -Destination $modApiLive -Recurse -Force
}

Copy-Item -LiteralPath (Join-Path $original 'loadorder.json') -Destination (Join-Path $modsRoot 'loadorder.json') -Force
$saveRecords = Get-Content -LiteralPath (Join-Path $original 'save-hashes-before.json') -Raw | ConvertFrom-Json
foreach ($record in $saveRecords) {
    $targetBase = if ($record.relativeBase -eq 'root-saves') { $gameRoot } else { Join-Path $gameRoot 'Saves' }
    New-Item -ItemType Directory -Path $targetBase -Force | Out-Null
    $backup = Join-Path (Join-Path $original $record.relativeBase) $record.name
    Copy-Item -LiteralPath $backup -Destination (Join-Path $targetBase $record.name) -Force
}

$verification = foreach ($record in $saveRecords) {
    $targetBase = if ($record.relativeBase -eq 'root-saves') { $gameRoot } else { Join-Path $gameRoot 'Saves' }
    $target = Join-Path $targetBase $record.name
    $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    [pscustomobject]@{ relativeBase=$record.relativeBase; name=$record.name; expected=$record.sha256; actual=$hash; match=($hash -eq $record.sha256) }
}
$verification | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'restore-save-verification.json') -Encoding UTF8
if (@($verification | Where-Object { -not $_.match }).Count -gt 0) { throw 'One or more restored save hashes do not match.' }

Write-Host "Restored runtime lane: $Lane"
