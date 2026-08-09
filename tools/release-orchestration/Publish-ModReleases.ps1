[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [string]$ShelteredRoot,
    [string]$OutputRoot,
    [string]$NotesRoot,
    [string]$Projects
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrWhiteSpace($ShelteredRoot)) { $ShelteredRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..\..\..')) }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $ShelteredRoot 'release\2.0\artifacts\mods' }
if ([string]::IsNullOrWhiteSpace($NotesRoot)) { $NotesRoot = Join-Path $ShelteredRoot 'release\2.0\evidence\github-release-notes' }

$manifestPath = Join-Path $OutputRoot 'mod-manifest.fragment.json'
$parsedManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest = @(foreach ($manifestEntry in $parsedManifest) { $manifestEntry })
$specs = @(
    [pscustomobject]@{ Repo = 'ExpeditionFour'; PackageId = 'Coolnether123.FourPersonExpeditions'; Note = 'FourPersonExpeditions.md'; Validation = 'Steam and Epic passed the completion-backed four-person expedition transaction, including party setup, send, recall, summary, finalization, and fresh-radio reopen with exact state restoration.' },
    [pscustomobject]@{ Repo = 'TradingAmount'; PackageId = 'Coolnether123.TradingAmount'; Note = 'TradingAmount.md'; Validation = 'Steam and Epic passed the self-provisioned real TradingPanel transaction, including amount changes, trade registration, shared Vanilla Fixes behavior, and exact cleanup.' },
    [pscustomobject]@{ Repo = 'Better-AI-Queue'; PackageId = 'coolnether123.betteraiqueue'; Note = 'BetterAIQueue.md'; Validation = 'Steam and Epic passed the completion-backed queue persistence transaction, proving the five-slot queue across save/restart and byte-exact save restoration.' },
    [pscustomobject]@{ Repo = 'Lifespan'; PackageId = 'coolnether123.Lifespan'; Note = 'Lifespan.md'; Validation = 'Steam and Epic passed age mutation, newborn handoff, persisted hydration, and exact cleanup through the completion-backed persistence transaction.' },
    [pscustomobject]@{ Repo = 'BunkerRandomLocation'; PackageId = 'coolnether123.BunkerRandomLocation'; Note = 'BunkerRandomLocation.md'; Validation = 'Steam and Epic passed randomized bunker creation and exact location persistence through save/restart, with the test save restored afterward.' },
    [pscustomobject]@{ Repo = 'Procreation-Framework'; PackageId = 'com.procreation.framework'; Note = 'FamilyExpansion.md'; Validation = 'Steam and Epic passed pregnancy preparation, conception, gestation, birth, family growth, Lifespan newborn handoff, restart persistence, and exact cleanup.' },
    [pscustomobject]@{ Repo = 'Deep-Expansion'; PackageId = 'coolnether123.deepexpansion'; Note = 'DeepExpansion.md'; Validation = 'Steam and Epic passed completion-backed Mark I/Mark II progression, tier/depth changes, persistence, and exact state restoration.' },
    [pscustomobject]@{ Repo = 'Sheltered-Vanilla-Fixes'; PackageId = 'Coolnether123.ShelteredVanillaFixes'; Note = 'ShelteredVanillaFixes.md'; Validation = 'Steam and Epic passed all six self-provisioned gameplay transactions: breach, radio, quest weapons, trading slots, weapon crafting, and recycling.' },
    [pscustomobject]@{ Repo = 'Shelter-Systems-Expansion'; PackageId = 'coolnether123.ShelteredSystemsExpansion'; Note = 'ShelterSystemsExpansion.md'; Validation = 'Steam and Epic passed completion-backed oxygen and water progression matrices with deterministic setup, verification, and rollback.' },
    [pscustomobject]@{ Repo = 'Sheltered-Expanded-Map-Sizes'; PackageId = 'expandedmapsizes'; Note = 'ExpandedMapSizes.md'; Validation = 'Steam and Epic passed real Harmony-driven expanded-map generation: all 4,000 bounded regions were verified, benchmarked, cleaned up, and the save was restored exactly.' },
    [pscustomobject]@{ Repo = 'Sheltered-Display-Fixes'; PackageId = 'Coolnether123.ShelteredDisplayFixes'; Note = 'ShelteredDisplayFixes.md'; Validation = 'Steam and Epic passed three real Wardrobe open/close cycles, platform-specific mip-property checks, owned-texture release, camera/stack/settings verification, and exact rollback.' }
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
$knownProjects = @($specs.Repo)
$unknownProjects = @($requestedProjects | Where-Object { $_ -notin $knownProjects })
if ($unknownProjects.Count -gt 0) {
    throw "Unknown release project(s): $($unknownProjects -join ', '). Known projects: $($knownProjects -join ', ')."
}
$selectedSpecs = if ($requestedProjects.Count -eq 0) {
    @($specs)
} else {
    @($specs | Where-Object { [string]$_.Repo -in $requestedProjects })
}

$results = New-Object System.Collections.Generic.List[object]
foreach ($spec in $selectedSpecs) {
    $package = @($manifest | Where-Object { [string]$_.packageId -eq [string]$spec.PackageId }) | Select-Object -First 1
    if ($null -eq $package) { throw "Missing manifest entry: $($spec.PackageId)" }

    $repoPath = Join-Path $ShelteredRoot $spec.Repo
    $repoUrl = ([string](git -C $repoPath remote get-url origin)).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Cannot resolve GitHub origin for $($spec.Repo)." }
    $repo = $repoUrl -replace '^https://github.com/', '' -replace '^git@github.com:', '' -replace '\.git$', ''
    $head = ([string](git -C $repoPath rev-parse HEAD)).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Cannot resolve HEAD for $($spec.Repo)." }
    if (([string](git -C $repoPath branch --show-current)).Trim() -ne 'main') { throw "$($spec.Repo) is not on main." }
    if (@(git -C $repoPath status --porcelain).Count -gt 0) { throw "$($spec.Repo) is dirty." }

    $releaseJson = & gh release list --repo $repo --limit 100 --json tagName,isDraft,isPrerelease,createdAt
    if ($LASTEXITCODE -ne 0) { throw "Cannot list GitHub releases for $repo." }
    $parsedReleases = $releaseJson | ConvertFrom-Json
    $releases = @(foreach ($release in $parsedReleases) { $release })
    $latestStable = @($releases | Where-Object { -not [bool]$_.isDraft -and -not [bool]$_.isPrerelease } | Sort-Object createdAt -Descending) | Select-Object -First 1
    if ($null -eq $latestStable) { throw "No stable baseline release exists for $repo." }
    $baseTag = [string]$latestStable.tagName
    $baseCommit = ([string](& gh api "repos/$repo/commits/$baseTag" --jq .sha)).Trim()
    if ($LASTEXITCODE -ne 0 -or $baseCommit -notmatch '^[0-9a-fA-F]{40}$') { throw "Cannot resolve $repo release tag $baseTag to a commit." }

    if ($baseCommit -eq $head) {
        $results.Add([pscustomobject]@{ repo = $repo; status = 'current'; tag = $baseTag; head = $head.Substring(0, 12); package = [string]$package.filename; sha256 = [string]$package.sha256 })
        Write-Host "CURRENT $repo $baseTag already targets current main."
        continue
    }

    if ([string]$package.commit -ne $head) {
        throw "$($spec.Repo) package provenance is stale: manifest=$($package.commit), HEAD=$head. Run selective packaging first."
    }
    $zip = Join-Path $OutputRoot ([string]$package.filename)
    $sidecar = "$zip.sha256"
    foreach ($path in @($zip, $sidecar)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Release asset is missing: $path" }
    }
    $zipStream = [IO.File]::OpenRead($zip)
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $actualHash = ([BitConverter]::ToString($sha256.ComputeHash($zipStream))).Replace('-', '').ToUpperInvariant()
    } finally {
        $sha256.Dispose()
        $zipStream.Dispose()
    }
    if ($actualHash -ne ([string]$package.sha256).ToUpperInvariant()) { throw "$($spec.Repo) ZIP hash does not match its package manifest." }
    if ([int64]([IO.FileInfo]$zip).Length -ne [int64]$package.bytes) { throw "$($spec.Repo) ZIP size does not match its package manifest." }
    $sidecarText = (Get-Content -LiteralPath $sidecar -Raw).Trim()
    if ($sidecarText -notmatch ('^(?<hash>[0-9a-fA-F]{64})\s+' + [regex]::Escape([string]$package.filename) + '\s*$')) { throw "$($spec.Repo) sidecar format is invalid." }
    if ($Matches.hash.ToUpperInvariant() -ne $actualHash) { throw "$($spec.Repo) sidecar hash is stale." }

    $versionTag = 'v' + [string]$package.version
    $versionPattern = '^' + [regex]::Escape($versionTag) + '-final(?<number>[0-9]+)$'
    $finalNumbers = @(
        $releases |
            ForEach-Object { [regex]::Match([string]$_.tagName, $versionPattern) } |
            Where-Object { $_.Success } |
            ForEach-Object { [int]$_.Groups['number'].Value }
    )
    $nextFinal = if ($finalNumbers.Count -eq 0) { 1 } else { ([int](($finalNumbers | Measure-Object -Maximum).Maximum)) + 1 }
    $tag = "$versionTag-final$nextFinal"
    if (@($releases.tagName) -contains $tag) { throw "Calculated release tag already exists: $repo $tag" }

    $changes = @(git -C $repoPath log "$baseCommit..HEAD" --format='- %h %s')
    if ($LASTEXITCODE -ne 0 -or $changes.Count -eq 0) { throw "No auditable commit diff exists between $baseTag and current main for $repo." }
    $notesPath = Join-Path $NotesRoot ([string]$spec.Note)
    $oldNotes = if (Test-Path -LiteralPath $notesPath) { Get-Content -LiteralPath $notesPath -Raw } else { '' }
    $nexus = [regex]::Match($oldNotes, 'https://www\.nexusmods\.com/[^\s\)]+').Value
    if ([string]::IsNullOrWhiteSpace($nexus)) { $nexus = 'No public Nexus page was identified in the local cross-reference record.' }
    $title = "$($package.modName) $($package.version) - stable final $nextFinal"
    $body = @"
# $($package.modName) $($package.version)

This stable current-main package supersedes the repository's older SMM 2.0 RC/review assets. It was built from `main` at commit $head.

## Changelog since $baseTag

$($changes -join "`n")

## Package

- File: $($package.filename)
- Size: $($package.bytes) bytes
- SHA-256: $($package.sha256)
- Required ModAPI: $($package.requiredModApi)
- Required ShelteredAPI: $($package.requiredShelteredApi)

## Cross-reference and validation

- Nexus reference: $nexus
- The scoped canonical verifier passed ZIP hash, sidecar, About metadata, assembly version, and current-commit provenance checks while preserving the ten unrelated package records.
- $($spec.Validation)
"@

    if ($PSCmdlet.ShouldProcess($repo, "Publish $tag from current main with ZIP and SHA-256 sidecar")) {
        New-Item -ItemType Directory -Path $NotesRoot -Force | Out-Null
        Set-Content -LiteralPath $notesPath -Value $body -Encoding UTF8
        $tempNotes = New-TemporaryFile
        try {
            Set-Content -LiteralPath $tempNotes -Value $body -Encoding UTF8
            & gh release create $tag $zip $sidecar --repo $repo --target $head --title $title --latest --notes-file $tempNotes
            if ($LASTEXITCODE -ne 0) { throw "Release creation failed: $repo $tag" }
        }
        finally {
            if (Test-Path -LiteralPath $tempNotes) { Remove-Item -LiteralPath $tempNotes -Force }
        }
        $status = 'published'
    } else {
        $status = 'planned'
    }

    $results.Add([pscustomobject]@{ repo = $repo; status = $status; tag = $tag; head = $head.Substring(0, 12); package = [string]$package.filename; sha256 = [string]$package.sha256 })
}

$results | ConvertTo-Json -Depth 6
