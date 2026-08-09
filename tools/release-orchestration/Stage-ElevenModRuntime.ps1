[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Steam', 'Epic')]
    [string]$Lane,

    [Parameter(Mandatory = $true)]
    [string]$EvidenceRoot,

    [string]$WorkspaceRoot,
    [switch]$DeployCurrentManager
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrWhiteSpace($WorkspaceRoot)) { $WorkspaceRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..\..\..')) }

$gameRoot = if ($Lane -eq 'Steam') {
    'A:\SteamLibrary\steamapps\common\Sheltered'
} else {
    'D:\Epic Games Games\Sheltered'
}
$gameRoot = [IO.Path]::GetFullPath($gameRoot).TrimEnd('\')
$allowedRoots = @(
    'A:\SteamLibrary\steamapps\common\Sheltered',
    'D:\Epic Games Games\Sheltered'
)
if ($allowedRoots -notcontains $gameRoot) {
    throw "Refusing unapproved game root: $gameRoot"
}

$gameExe = if ($Lane -eq 'Steam') { 'Sheltered.exe' } else { 'ShelteredWindows64_EOS.exe' }
if (-not (Test-Path -LiteralPath (Join-Path $gameRoot $gameExe))) {
    throw "Game executable is missing from $gameRoot"
}
$gameExePath = [IO.Path]::GetFullPath((Join-Path $gameRoot $gameExe))
$gameProcessName = [IO.Path]::GetFileNameWithoutExtension($gameExe)
$ownedProcesses = @(Get-Process -Name $gameProcessName -ErrorAction SilentlyContinue | Where-Object {
    try { [IO.Path]::GetFullPath($_.Path) -eq $gameExePath } catch { $false }
})
if ($ownedProcesses.Count -gt 0) { throw "$Lane Sheltered is running; refusing to replace live mod folders." }

$modsRoot = Join-Path $gameRoot 'mods'
$loadOrderPath = Join-Path $modsRoot 'loadorder.json'
$packageRoot = Join-Path $WorkspaceRoot 'release\2.0\artifacts\mods'
$manifestPath = Join-Path $packageRoot 'mod-manifest.fragment.json'
if (-not (Test-Path -LiteralPath $loadOrderPath)) { throw "Missing load order: $loadOrderPath" }
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Missing package manifest: $manifestPath" }

$specs = @(
    [pscustomobject]@{ Project='ExpeditionFour'; Folder='Four Person Expeditions'; Id='Coolnether123.FourPersonExpeditions' },
    [pscustomobject]@{ Project='TradingAmount'; Folder='Trading Amount'; Id='Coolnether123.TradingAmount' },
    [pscustomobject]@{ Project='Better-AI-Queue'; Folder='Better AI Queue'; Id='coolnether123.betteraiqueue' },
    [pscustomobject]@{ Project='Lifespan'; Folder='Lifespan'; Id='coolnether123.Lifespan' },
    [pscustomobject]@{ Project='Procreation-Framework'; Folder='Family Expansion'; Id='com.procreation.framework' },
    [pscustomobject]@{ Project='BunkerRandomLocation'; Folder='Bunker Random Location'; Id='coolnether123.BunkerRandomLocation' },
    [pscustomobject]@{ Project='Deep-Expansion'; Folder='Deep Expansion'; Id='coolnether123.deepexpansion' },
    [pscustomobject]@{ Project='Sheltered-Vanilla-Fixes'; Folder='Sheltered Vanilla Fixes'; Id='Coolnether123.ShelteredVanillaFixes' },
    [pscustomobject]@{ Project='Shelter-Systems-Expansion'; Folder='Shelter Systems Expansion'; Id='coolnether123.ShelteredSystemsExpansion' },
    [pscustomobject]@{ Project='Sheltered-Expanded-Map-Sizes'; Folder='Expanded Map Sizes'; Id='expandedmapsizes' },
    [pscustomobject]@{ Project='Sheltered-Display-Fixes'; Folder='Sheltered Display Fixes'; Id='Coolnether123.ShelteredDisplayFixes' }
)

$parsedManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest = @(foreach ($manifestEntry in $parsedManifest) { $manifestEntry })
if ($manifest.Count -ne $specs.Count) {
    throw "Expected exactly eleven canonical package entries; found $($manifest.Count)."
}

$evidence = [IO.Path]::GetFullPath($EvidenceRoot)
$original = Join-Path $evidence 'original-state'
if (Test-Path -LiteralPath $original) {
    throw "Original-state backup already exists; refusing to overwrite it: $original"
}
New-Item -ItemType Directory -Path $original -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $original 'mods') -Force | Out-Null

Copy-Item -LiteralPath $loadOrderPath -Destination (Join-Path $original 'loadorder.json')

$folderState = @()
foreach ($spec in $specs) {
    $liveFolder = Join-Path $modsRoot $spec.Folder
    $exists = Test-Path -LiteralPath $liveFolder
    if ($exists) {
        $backupFolder = Join-Path (Join-Path $original 'mods') $spec.Folder
        New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
        Copy-Item -Path (Join-Path $liveFolder '*') -Destination $backupFolder -Recurse -Force
    }
    $folderState += [pscustomobject]@{ folder=$spec.Folder; id=$spec.Id; originallyExisted=$exists }
}

$modApiFolder = Join-Path $modsRoot 'ModAPI'
$modApiExisted = Test-Path -LiteralPath $modApiFolder
if ($modApiExisted) {
    $modApiBackup = Join-Path $original 'ModAPI'
    New-Item -ItemType Directory -Path $modApiBackup -Force | Out-Null
    Copy-Item -Path (Join-Path $modApiFolder '*') -Destination $modApiBackup -Recurse -Force
}

$saveRecords = @()
foreach ($saveBase in @($gameRoot, (Join-Path $gameRoot 'Saves'))) {
    if (-not (Test-Path -LiteralPath $saveBase)) { continue }
    $relativeBase = if ($saveBase -eq $gameRoot) { 'root-saves' } else { 'Saves' }
    $saveBackup = Join-Path $original $relativeBase
    New-Item -ItemType Directory -Path $saveBackup -Force | Out-Null
    foreach ($save in Get-ChildItem -LiteralPath $saveBase -File -Filter 'savedata*.dat') {
        Copy-Item -LiteralPath $save.FullName -Destination (Join-Path $saveBackup $save.Name)
        $saveRecords += [pscustomobject]@{
            relativeBase=$relativeBase
            name=$save.Name
            bytes=$save.Length
            sha256=(Get-FileHash -LiteralPath $save.FullName -Algorithm SHA256).Hash
        }
    }
}
$saveRecords | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $original 'save-hashes-before.json') -Encoding UTF8

$exactOrder = @('com.harmony.0harmony', 'coolnether123.shelteredagentinterface') + @($specs.Id)
[pscustomobject]@{
    lane=$Lane
    gameRoot=$gameRoot
    stagedUtc=$null
    state='backup-complete'
    modApiOriginallyExisted=$modApiExisted
    folders=$folderState
    packages=@()
    loadOrder=$exactOrder
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'deployment-state.json') -Encoding UTF8

if ($DeployCurrentManager) {
    $managerDist = Join-Path $WorkspaceRoot 'shelteredmodmanager\Dist\SMM'
    $targetSmm = Join-Path $gameRoot 'SMM'
    $expectedManagerHash = (Get-FileHash -LiteralPath (Join-Path $managerDist 'bin\ShelteredAPI.dll') -Algorithm SHA256).Hash
    $settings = @{}
    foreach ($relative in @('bin\mod_manager.ini', 'bin\manager_options.json')) {
        $path = Join-Path $targetSmm $relative
        if (Test-Path -LiteralPath $path) { $settings[$relative] = [IO.File]::ReadAllBytes($path) }
    }
    Copy-Item -Path (Join-Path $managerDist '*') -Destination $targetSmm -Recurse -Force
    foreach ($relative in $settings.Keys) {
        [IO.File]::WriteAllBytes((Join-Path $targetSmm $relative), $settings[$relative])
    }
    $deployedManagerHash = (Get-FileHash -LiteralPath (Join-Path $targetSmm 'bin\ShelteredAPI.dll') -Algorithm SHA256).Hash
    if ($deployedManagerHash -ne $expectedManagerHash) { throw 'Manager deployment hash mismatch.' }
}

$deployed = @()
foreach ($spec in $specs) {
    $entry = $manifest | Where-Object { $_.project -eq $spec.Project }
    if ($null -eq $entry) { throw "Missing manifest package for $($spec.Project)" }
    $zip = Join-Path $packageRoot $entry.filename
    $zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    if ($zipHash -ne $entry.sha256) { throw "Package hash mismatch: $($entry.filename)" }

    $liveFolder = [IO.Path]::GetFullPath((Join-Path $modsRoot $spec.Folder))
    if (-not $liveFolder.StartsWith(([IO.Path]::GetFullPath($modsRoot).TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing out-of-mods target: $liveFolder"
    }
    if (Test-Path -LiteralPath $liveFolder) { Remove-Item -LiteralPath $liveFolder -Recurse -Force }
    Expand-Archive -LiteralPath $zip -DestinationPath $modsRoot -Force

    $aboutPath = Join-Path $liveFolder 'About\About.json'
    if (-not (Test-Path -LiteralPath $aboutPath)) { throw "Deployed About.json missing: $aboutPath" }
    $about = Get-Content -LiteralPath $aboutPath -Raw | ConvertFrom-Json
    if ($about.id -ne $spec.Id -or $about.version -ne $entry.version) {
        throw "Deployed metadata mismatch for $($spec.Folder)"
    }
    $deployed += [pscustomobject]@{
        project=$spec.Project
        folder=$spec.Folder
        id=$spec.Id
        version=$entry.version
        package=$entry.filename
        sha256=$zipHash
        commit=$entry.commit
    }
}

$loadOrder = Get-Content -LiteralPath $loadOrderPath -Raw | ConvertFrom-Json
$loadOrder.order = $exactOrder
foreach ($property in @($loadOrder.mods.PSObject.Properties)) {
    $property.Value.enabled = $exactOrder -contains $property.Name
}
foreach ($id in $exactOrder) {
    if ($null -eq $loadOrder.mods.PSObject.Properties[$id]) {
        $loadOrder.mods | Add-Member -NotePropertyName $id -NotePropertyValue ([pscustomobject]@{ enabled=$true })
    }
}
$loadOrder | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $loadOrderPath -Encoding UTF8

[pscustomobject]@{
    lane=$Lane
    gameRoot=$gameRoot
    stagedUtc=[DateTime]::UtcNow.ToString('o')
    state='staged'
    modApiOriginallyExisted=$modApiExisted
    folders=$folderState
    packages=$deployed
    loadOrder=$exactOrder
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'deployment-state.json') -Encoding UTF8

Write-Host "Staged exact eleven-mod runtime lane: $Lane"
Write-Host "Evidence backup: $original"
Write-Host "Load order entries: $($exactOrder.Count)"
