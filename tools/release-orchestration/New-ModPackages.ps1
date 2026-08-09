param(
    [string]$ShelteredRoot,
    [string]$OutputRoot,
    [string]$Projects
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrWhiteSpace($ShelteredRoot)) { $ShelteredRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..\..\..')) }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $ShelteredRoot 'release\2.0\artifacts\mods' }
$stageRoot = Join-Path $OutputRoot '_stage'
if (Test-Path $stageRoot) { Remove-Item $stageRoot -Recurse -Force }
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null

# Mode 'package': copy PackageBase wholesale (it already holds About/Config/Assemblies), then overwrite the DLL.
# Mode 'compose': build the package from AboutDir + optional ConfigDir + DLL.
$mods = @(
    @{ Repo='ExpeditionFour';               Mode='compose'; About='About';                                              Config=$null;                                   Dll='ExpeditionFour\bin\Release\ExpeditionFour.dll';                                        DllName='ExpeditionFour.dll' },
    @{ Repo='TradingAmount';                Mode='package'; Base='TradingAmount Mod\TradingAmount';                     Dll='TradingAmount Mod\TradingAmount\Assemblies\TradingAmount.dll';                        DllName='TradingAmount.dll' },
    @{ Repo='Better-AI-Queue';              Mode='package'; Base='Decompiled\Better AI Queue Mod\Better AI Queue';     Dll='Better AI Queue Mod\Better AI Queue\Assemblies\Better AI Queue.dll';                  DllName='Better AI Queue.dll' },
    @{ Repo='Lifespan';                     Mode='package'; Base='Lifespan Mod\Lifespan';                              Dll='Lifespan\bin\Release\Lifespan.dll';                                                   DllName='Lifespan.dll' },
    @{ Repo='BunkerRandomLocation';         Mode='compose'; About='About';                                              Config='Config';                                Dll='BunkerRandomLocation\bin\Release\BunkerRandomLocation.dll';                            DllName='BunkerRandomLocation.dll' },
    @{ Repo='Procreation-Framework';        Mode='compose'; About='Procreation Framework\About';                        Config='Procreation Framework\Config';          Dll='Procreation Framework\bin\Release\Family Expansion.dll';                               DllName='Family Expansion.dll' },
    @{ Repo='Deep-Expansion';               Mode='compose'; About='Deep Expansion\About';                               Config='Deep Expansion\Config';                 Dll='Deep Expansion\bin\Release\Deep Expansion.dll';                                        DllName='Deep Expansion.dll'; Extras=@(@{Src='Deep Expansion\Assets'; Dest='Assets'}) },
    @{ Repo='Sheltered-Vanilla-Fixes';      Mode='package'; Base='Sheltered Vanilla Fixes Mod\Sheltered Vanilla Fixes'; Dll='Sheltered Vanilla Fixes\bin\Release\Sheltered Vanilla Fixes.dll';                     DllName='Sheltered Vanilla Fixes.dll' },
    @{ Repo='Shelter-Systems-Expansion';    Mode='package'; Base='ShelteredSystemsExpansion Mod\Shelter Systems Expansion'; Dll='ShelteredSystemsExpansion Mod\Shelter Systems Expansion\Assemblies\ShelteredSystemsExpansion.dll'; DllName='ShelteredSystemsExpansion.dll' },
    @{ Repo='Sheltered-Expanded-Map-Sizes'; Mode='package'; Base='Decompiled\Expanded Map Sizes Mod\Expanded Map Sizes'; Dll='Decompiled\Expanded Map Sizes Mod\Expanded Map Sizes\Assemblies\Expanded Map Sizes.dll'; DllName='Expanded Map Sizes.dll' },
    @{ Repo='Sheltered-Display-Fixes';      Mode='package'; Base='Sheltered Display Fixes Mod\Sheltered Display Fixes'; Dll='Sheltered Display Fixes Mod\Sheltered Display Fixes\Assemblies\Sheltered Display Fixes.dll'; DllName='Sheltered Display Fixes.dll' }
)

$requestedProjects = @()
if (-not [string]::IsNullOrWhiteSpace($Projects)) {
    $requestedProjects = @(
        $Projects -split '[,;]' |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -Unique
    )
}

$knownProjects = @($mods | ForEach-Object { [string]$_.Repo })
$unknownProjects = @($requestedProjects | Where-Object { $_ -notin $knownProjects })
if ($unknownProjects.Count -gt 0) {
    throw "Unknown release project(s): $($unknownProjects -join ', '). Known projects: $($knownProjects -join ', ')."
}

$selectedMods = if ($requestedProjects.Count -eq 0) {
    @($mods)
} else {
    @($mods | Where-Object { [string]$_.Repo -in $requestedProjects })
}

$fragmentPath = Join-Path $OutputRoot 'mod-manifest.fragment.json'
$existingEntries = @()
if ($selectedMods.Count -lt $mods.Count) {
    if (-not (Test-Path -LiteralPath $fragmentPath)) {
        throw "Selective packaging requires the existing canonical package fragment: $fragmentPath"
    }
    $parsedEntries = Get-Content -LiteralPath $fragmentPath -Raw | ConvertFrom-Json
    $existingEntries = @(foreach ($parsedEntry in $parsedEntries) { $parsedEntry })
    $missingExisting = @($knownProjects | Where-Object { $_ -notin @($existingEntries.project) })
    if ($missingExisting.Count -gt 0) {
        throw "Selective packaging cannot preserve missing package entries: $($missingExisting -join ', '). Run a full package build once."
    }
}

Write-Host ("Selected package projects: {0}" -f (($selectedMods | ForEach-Object { $_.Repo }) -join ', '))
$builtEntries = New-Object System.Collections.Generic.List[object]
$failures = @()

foreach ($mod in $selectedMods) {
    $repoPath = Join-Path $ShelteredRoot $mod.Repo
    try {
        if ($mod.Mode -eq 'package') {
            $base = Join-Path $repoPath $mod.Base
            $aboutPath = Join-Path $base 'About\About.json'
        } else {
            $aboutPath = Join-Path (Join-Path $repoPath $mod.About) 'About.json'
        }
        if (-not (Test-Path $aboutPath)) { throw "About.json not found: $aboutPath" }
        $about = Get-Content $aboutPath -Raw | ConvertFrom-Json
        $name = $about.name; $version = $about.version; $id = $about.id
        if ([string]::IsNullOrEmpty($about.requiredModApiVersion) -or [string]::IsNullOrEmpty($about.requiredShelteredApiVersion)) {
            throw "About.json for '$name' lacks required 2.0 API version fields."
        }

        $dllSource = Join-Path $repoPath $mod.Dll
        if (-not (Test-Path $dllSource)) { throw "Fresh DLL not found: $dllSource" }
        $dllInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($dllSource)
        $dllVer = [Version]$dllInfo.FileVersion
        $aboutVer = [Version]$version
        $aboutBuild = [Math]::Max(0, $aboutVer.Build)
        $aboutVer3 = '{0}.{1}.{2}' -f $aboutVer.Major, $aboutVer.Minor, $aboutBuild
        $dllVer3 = '{0}.{1}.{2}' -f $dllVer.Major, $dllVer.Minor, $dllVer.Build
        if ($dllVer3 -ne $aboutVer3) {
            throw "Version drift for '$name': About.json=$version but DLL FileVersion=$($dllInfo.FileVersion) ($dllSource)"
        }

        $folderName = $name
        $pkgStage = Join-Path $stageRoot $folderName
        New-Item -ItemType Directory -Path $pkgStage -Force | Out-Null
        if ($mod.Mode -eq 'package') {
            Copy-Item (Join-Path $base '*') $pkgStage -Recurse -Force
        } else {
            Copy-Item (Split-Path $aboutPath) (Join-Path $pkgStage 'About') -Recurse
            if ($mod.Config) {
                $cfg = Join-Path $repoPath $mod.Config
                if (Test-Path $cfg) { Copy-Item $cfg (Join-Path $pkgStage 'Config') -Recurse }
            }
            if ($mod.Extras) {
                foreach ($extra in $mod.Extras) {
                    $src = Join-Path $repoPath $extra.Src
                    if (-not (Test-Path $src)) { throw "Extra payload missing: $src" }
                    Copy-Item $src (Join-Path $pkgStage $extra.Dest) -Recurse
                }
            }
        }
        $asmDir = Join-Path $pkgStage 'Assemblies'
        New-Item -ItemType Directory -Path $asmDir -Force | Out-Null
        Copy-Item $dllSource (Join-Path $asmDir $mod.DllName) -Force
        # These eleven packages are single-assembly mods. Historical staging
        # folders can contain framework/reference binaries produced by old
        # build layouts; shipping those can shadow the game's own runtime.
        Get-ChildItem $asmDir -File -Filter '*.dll' |
            Where-Object { $_.Name -ne $mod.DllName } |
            Remove-Item -Force
        # Strip anything that must not ship inside a mod package.
        Get-ChildItem $pkgStage -Recurse -File | Where-Object { $_.Extension -in @('.pdb','.zip','.log') } | Remove-Item -Force

        $zipName = ('{0}-{1}-SMM-2.0.zip' -f ($name -replace ' ', ''), $version)
        $zipPath = Join-Path $OutputRoot $zipName
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        # Zip the mod FOLDER so extraction into mods\ yields mods\<Name>\About...
        Compress-Archive -Path $pkgStage -DestinationPath $zipPath -CompressionLevel Optimal
        $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash
        Set-Content ($zipPath + '.sha256') "$hash  $zipName" -Encoding ASCII
        $commit = (& git -C $repoPath rev-parse HEAD 2>$null)
        $builtEntries.Add([pscustomobject]@{
            project = $mod.Repo; modName = $name; packageId = $id; version = $version
            filename = $zipName; bytes = (Get-Item $zipPath).Length; sha256 = $hash
            dllFileVersion = $dllInfo.FileVersion; commit = $commit
            requiredModApi = $about.requiredModApiVersion; requiredShelteredApi = $about.requiredShelteredApiVersion
            builtUtc = [DateTime]::UtcNow.ToString('o')
        })
        Write-Host ("OK  {0}  ({1}, {2})" -f $zipName, $id, $hash.Substring(0,12))
    }
    catch {
        $failures += "{0}: {1}" -f $mod.Repo, $_.Exception.Message
        Write-Host ("FAIL {0}: {1}" -f $mod.Repo, $_.Exception.Message)
    }
}

if ($failures.Count -gt 0) {
    Write-Host "`nFAILURES:"; $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

$manifest = New-Object System.Collections.Generic.List[object]
foreach ($mod in $mods) {
    $entry = @($builtEntries | Where-Object { [string]$_.project -eq [string]$mod.Repo }) | Select-Object -First 1
    if ($null -eq $entry) {
        $entry = @($existingEntries | Where-Object { [string]$_.project -eq [string]$mod.Repo }) | Select-Object -First 1
    }
    if ($null -eq $entry) { throw "No package manifest entry is available for $($mod.Repo)." }
    $manifest.Add($entry)
}

$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $fragmentPath -Encoding UTF8

$canonicalManifestPath = Join-Path $ShelteredRoot 'release\2.0\release-manifest.json'
if (Test-Path -LiteralPath $canonicalManifestPath) {
    $canonicalManifest = Get-Content -LiteralPath $canonicalManifestPath -Raw | ConvertFrom-Json
    if ($null -eq $canonicalManifest.PSObject.Properties['modPackages']) {
        throw "Canonical release manifest does not expose modPackages: $canonicalManifestPath"
    }
    [object[]]$manifestArray = $manifest.ToArray()
    $canonicalManifest.modPackages = $manifestArray
    $canonicalManifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $canonicalManifestPath -Encoding UTF8
    Write-Host 'Canonical release manifest updated from the final package set.'
}
Write-Host ("Package build complete: {0} rebuilt, {1} preserved." -f $builtEntries.Count, ($mods.Count - $builtEntries.Count))
