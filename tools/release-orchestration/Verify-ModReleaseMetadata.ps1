[CmdletBinding()]
param(
    [string]$ReleaseRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) {
    $ReleaseRoot = Join-Path $PSScriptRoot '..\..\..\release\2.0'
}

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)] [object]$Expected,
        [Parameter(Mandatory = $true)] [object]$Actual,
        [Parameter(Mandatory = $true)] [string]$Label
    )

    if ([string]$Expected -ne [string]$Actual) {
        throw "$Label mismatch. Expected '$Expected'; actual '$Actual'."
    }
}

$root = [IO.Path]::GetFullPath($ReleaseRoot).TrimEnd('\')
$shelteredRoot = [IO.Path]::GetFullPath((Join-Path $root '..\..')).TrimEnd('\')
$manifestPath = Join-Path $root 'release-manifest.json'
$fragmentPath = Join-Path $root 'artifacts\mods\mod-manifest.fragment.json'
$packageRoot = Join-Path $root 'artifacts\mods'

foreach ($path in @($manifestPath, $fragmentPath, $packageRoot)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required release path is missing: $path"
    }
}

try {
    $canonical = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
} catch {
    throw "Canonical manifest JSON parse failed: $manifestPath. $($_.Exception.Message)"
}

try {
    $fragment = Get-Content -LiteralPath $fragmentPath -Raw | ConvertFrom-Json
} catch {
    throw "Package fragment JSON parse failed: $fragmentPath. $($_.Exception.Message)"
}

# This explicit set is the release-scope contract. It prevents a fragment with
# an accidental extra or omitted package from defining the scope implicitly.
$expectedMods = @(
    [pscustomobject]@{ Project = 'ExpeditionFour'; PackageId = 'Coolnether123.FourPersonExpeditions'; DllPath = 'ExpeditionFour\bin\Release\ExpeditionFour.dll'; DllName = 'ExpeditionFour.dll' },
    [pscustomobject]@{ Project = 'TradingAmount'; PackageId = 'Coolnether123.TradingAmount'; DllPath = 'TradingAmount Mod\TradingAmount\Assemblies\TradingAmount.dll'; DllName = 'TradingAmount.dll' },
    [pscustomobject]@{ Project = 'Lifespan'; PackageId = 'coolnether123.Lifespan'; DllPath = 'Lifespan\bin\Release\Lifespan.dll'; DllName = 'Lifespan.dll' },
    [pscustomobject]@{ Project = 'BunkerRandomLocation'; PackageId = 'coolnether123.BunkerRandomLocation'; DllPath = 'BunkerRandomLocation\bin\Release\BunkerRandomLocation.dll'; DllName = 'BunkerRandomLocation.dll' },
    [pscustomobject]@{ Project = 'Procreation-Framework'; PackageId = 'com.procreation.framework'; DllPath = 'Procreation Framework\bin\Release\Family Expansion.dll'; DllName = 'Family Expansion.dll' },
    [pscustomobject]@{ Project = 'Deep-Expansion'; PackageId = 'coolnether123.deepexpansion'; DllPath = 'Deep Expansion\bin\Release\Deep Expansion.dll'; DllName = 'Deep Expansion.dll' },
    [pscustomobject]@{ Project = 'Sheltered-Vanilla-Fixes'; PackageId = 'Coolnether123.ShelteredVanillaFixes'; DllPath = 'Sheltered Vanilla Fixes\bin\Release\Sheltered Vanilla Fixes.dll'; DllName = 'Sheltered Vanilla Fixes.dll' },
    [pscustomobject]@{ Project = 'Shelter-Systems-Expansion'; PackageId = 'coolnether123.ShelteredSystemsExpansion'; DllPath = 'ShelteredSystemsExpansion Mod\Shelter Systems Expansion\Assemblies\ShelteredSystemsExpansion.dll'; DllName = 'ShelteredSystemsExpansion.dll' },
    [pscustomobject]@{ Project = 'Sheltered-Expanded-Map-Sizes'; PackageId = 'expandedmapsizes'; DllPath = 'Decompiled\Expanded Map Sizes Mod\Expanded Map Sizes\Assemblies\Expanded Map Sizes.dll'; DllName = 'Expanded Map Sizes.dll' },
    [pscustomobject]@{ Project = 'Better-AI-Queue'; PackageId = 'coolnether123.betteraiqueue'; DllPath = 'Better AI Queue Mod\Better AI Queue\Assemblies\Better AI Queue.dll'; DllName = 'Better AI Queue.dll' },
    [pscustomobject]@{ Project = 'Sheltered-Display-Fixes'; PackageId = 'Coolnether123.ShelteredDisplayFixes'; DllPath = 'Sheltered Display Fixes Mod\Sheltered Display Fixes\Assemblies\Sheltered Display Fixes.dll'; DllName = 'Sheltered Display Fixes.dll' }
)

$fragmentMods = @($fragment)
$canonicalMods = @($canonical.modPackages)
Assert-Equal 11 $fragmentMods.Count 'Scoped package count in fragment'
Assert-Equal 11 $canonicalMods.Count 'Scoped package count in canonical manifest'

$expectedProjects = ($expectedMods.Project | Sort-Object) -join '|'
$fragmentProjects = ($fragmentMods.project | ForEach-Object { [string]$_ } | Sort-Object) -join '|'
$canonicalProjects = ($canonicalMods.project | ForEach-Object { [string]$_ } | Sort-Object) -join '|'
Assert-Equal $expectedProjects $fragmentProjects 'Fragment project scope'
Assert-Equal $expectedProjects $canonicalProjects 'Canonical project scope'

$fragmentByProject = @{}
$canonicalByProject = @{}
foreach ($entry in $fragmentMods) {
    $project = [string]$entry.project
    if ($fragmentByProject.ContainsKey($project)) { throw "Duplicate fragment project: $project" }
    $fragmentByProject[$project] = $entry
}
foreach ($entry in $canonicalMods) {
    $project = [string]$entry.project
    if ($canonicalByProject.ContainsKey($project)) { throw "Duplicate canonical project: $project" }
    $canonicalByProject[$project] = $entry
}

$metadataFields = @(
    'project', 'modName', 'packageId', 'version', 'filename', 'bytes',
    'sha256', 'dllFileVersion', 'commit', 'requiredModApi',
    'requiredShelteredApi', 'builtUtc'
)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$verified = New-Object System.Collections.Generic.List[object]

foreach ($expected in $expectedMods) {
    $project = $expected.Project
    if (-not $fragmentByProject.ContainsKey($project)) { throw "Fragment entry is missing: $project" }
    if (-not $canonicalByProject.ContainsKey($project)) { throw "Canonical entry is missing: $project" }

    $entry = $fragmentByProject[$project]
    $canonicalEntry = $canonicalByProject[$project]
    Assert-Equal $expected.PackageId ([string]$entry.packageId) "$project fragment package ID"

    $repoPath = Join-Path $shelteredRoot $project
    if (-not (Test-Path -LiteralPath $repoPath -PathType Container)) { throw "$project repository is missing: $repoPath" }
    $head = ([string](git -C $repoPath rev-parse HEAD 2>$null)).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -notmatch '^[0-9a-fA-F]{40}$') { throw "$project current Git commit cannot be resolved." }
    Assert-Equal $head ([string]$entry.commit) "$project current-main package provenance"

    foreach ($field in $metadataFields) {
        Assert-Equal ([string]$entry.$field) ([string]$canonicalEntry.$field) "$project canonical/fragment $field"
    }

    if ([string]::IsNullOrWhiteSpace([string]$entry.builtUtc)) {
        throw "$project has no package build timestamp. Repackage before release."
    }
    if (-not [regex]::IsMatch([string]$entry.commit, '^[0-9a-fA-F]{40}$')) {
        throw "$project has an invalid 40-character Git commit in the fragment: $($entry.commit)"
    }
    if (-not [regex]::IsMatch([string]$entry.sha256, '^[0-9a-fA-F]{64}$')) {
        throw "$project has an invalid SHA-256 in the fragment: $($entry.sha256)"
    }

    $filename = [string]$entry.filename
    if ([IO.Path]::GetFileName($filename) -ne $filename) {
        throw "$project package filename must be a file name, not a path: $filename"
    }
    $zipPath = Join-Path $packageRoot $filename
    $sidecarPath = $zipPath + '.sha256'
    if (-not (Test-Path -LiteralPath $zipPath)) { throw "Missing package ZIP: $zipPath" }
    if (-not (Test-Path -LiteralPath $sidecarPath)) { throw "Missing package sidecar: $sidecarPath" }

    $zip = Get-Item -LiteralPath $zipPath
    Assert-Equal ([int64]$entry.bytes) ([int64]$zip.Length) "$project ZIP byte count"
    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
    Assert-Equal $zipHash ([string]$entry.sha256).ToUpperInvariant() "$project fragment ZIP hash"

    $sidecarText = (Get-Content -LiteralPath $sidecarPath -Raw).Trim()
    $sidecarMatch = [regex]::Match($sidecarText, '^(?<hash>[0-9a-fA-F]{64})\s+(?<file>.+?)\s*$')
    if (-not $sidecarMatch.Success) { throw "Invalid SHA-256 sidecar format: $sidecarPath" }
    Assert-Equal $filename $sidecarMatch.Groups['file'].Value "$project sidecar filename"
    Assert-Equal $zipHash $sidecarMatch.Groups['hash'].Value.ToUpperInvariant() "$project sidecar hash"

    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $aboutEntries = @($archive.Entries | Where-Object { $_.FullName -match '(?i)[\\/]About[\\/]About\.json$' })
        Assert-Equal 1 $aboutEntries.Count "$project packaged About.json count"
        $assemblyPattern = '(?i)[\\/]Assemblies[\\/]' + [regex]::Escape([string]$expected.DllName) + '$'
        $assemblyEntries = @($archive.Entries | Where-Object { $_.FullName -match $assemblyPattern })
        Assert-Equal 1 $assemblyEntries.Count "$project packaged assembly count"
        $aboutStream = $aboutEntries[0].Open()
        $aboutReader = New-Object IO.StreamReader($aboutStream)
        try {
            $about = $aboutReader.ReadToEnd() | ConvertFrom-Json
        } finally {
            $aboutReader.Dispose()
            $aboutStream.Dispose()
        }
        $assemblyStream = $assemblyEntries[0].Open()
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            $packagedAssemblyHash = ([BitConverter]::ToString($sha256.ComputeHash($assemblyStream))).Replace('-', '')
        } finally {
            $sha256.Dispose()
            $assemblyStream.Dispose()
        }
    } catch {
        throw "$project package-content verification failed: $($_.Exception.Message)"
    } finally {
        $archive.Dispose()
    }

    $sourceDllPath = Join-Path $repoPath ([string]$expected.DllPath)
    if (-not (Test-Path -LiteralPath $sourceDllPath -PathType Leaf)) { throw "$project current Release output is missing: $sourceDllPath" }
    $sourceDll = Get-Item -LiteralPath $sourceDllPath
    $sourceAssemblyHash = (Get-FileHash -LiteralPath $sourceDllPath -Algorithm SHA256).Hash.ToUpperInvariant()
    Assert-Equal ([int64]$sourceDll.Length) ([int64]$assemblyEntries[0].Length) "$project packaged/current assembly byte count"
    Assert-Equal $sourceAssemblyHash $packagedAssemblyHash.ToUpperInvariant() "$project packaged/current assembly hash"
    $sourceVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($sourceDllPath).FileVersion
    Assert-Equal ([string]$entry.dllFileVersion) ([string]$sourceVersion) "$project current assembly file version"

    Assert-Equal ([string]$entry.modName) ([string]$about.name) "$project package name"
    Assert-Equal ([string]$entry.packageId) ([string]$about.id) "$project package ID"
    Assert-Equal ([string]$entry.version) ([string]$about.version) "$project package version"
    Assert-Equal ([string]$entry.requiredModApi) ([string]$about.requiredModApiVersion) "$project package ModAPI requirement"
    Assert-Equal ([string]$entry.requiredShelteredApi) ([string]$about.requiredShelteredApiVersion) "$project package ShelteredAPI requirement"

    $verified.Add([pscustomobject]@{
        project = $project
        version = [string]$entry.version
        filename = $filename
        bytes = [int64]$zip.Length
        sha256 = $zipHash
        commit = [string]$entry.commit
    })
}

Write-Output ('PASS: exactly {0} scoped mods verified.' -f $verified.Count)
Write-Output 'PASS: every current ZIP exists and its byte count, SHA-256, and sidecar match the fragment.'
Write-Output 'PASS: every package About.json version and identity match the fragment.'
Write-Output 'PASS: every packaged assembly is byte-identical to its current repository Release output.'
Write-Output 'PASS: every package commit provenance equals its repository current HEAD.'
Write-Output 'PASS: every canonical manifest mod entry matches its fragment entry, including version, hash, bytes, and commit provenance.'
Write-Output 'NOTE: Git commit is release provenance in the fragment/canonical metadata; current ZIPs do not embed a Git commit field.'
$verified | ConvertTo-Json -Depth 3
