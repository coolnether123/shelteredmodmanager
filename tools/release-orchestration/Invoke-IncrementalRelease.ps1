[CmdletBinding()]
param(
    [string]$ShelteredRoot,
    [string]$GraphPath,
    [string[]]$ChangedFile,
    [string]$ChangedFilesPath,
    [switch]$DetectGit,
    [string]$BaseRef = 'origin/main',
    [switch]$Execute,
    [switch]$AllowHeavy,
    [string]$HarnessUrl,
    [string]$SteamHarnessUrl,
    [string]$EpicHarnessUrl,
    [string]$HarnessRepo,
    [string]$SteamGameRoot,
    [string]$EpicGameRoot,
    [string]$TransactionRunnerPath,
    [int]$TransactionTimeoutSeconds = 180,
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$Stable,
    [string]$OutputPath,
    [string]$EvidenceRoot,
    [switch]$NoEvidenceReuse
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrWhiteSpace($ShelteredRoot)) { $ShelteredRoot = Join-Path $scriptRoot '..\..\..' }
if ([string]::IsNullOrWhiteSpace($GraphPath)) { $GraphPath = Join-Path $scriptRoot 'incremental-release-graph.json' }
if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) { $EvidenceRoot = Join-Path $ShelteredRoot 'release\2.0\evidence\incremental-gates' }
$resolvedHarnessRepo = $null
$resolvedSteamGameRoot = $null
$resolvedEpicGameRoot = $null
$resolvedTransactionRunnerPath = $null

function Normalize-PathText {
    param([string]$Path)
    if ($null -eq $Path) { return '' }
    return (($Path -replace '\\', '/') -replace '^\./', '').Trim('/')
}

function Get-RelativePathText {
    param([string]$BasePath, [string]$Path)
    $baseFull = [IO.Path]::GetFullPath($BasePath).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $pathFull = [IO.Path]::GetFullPath($Path)
    $baseUri = New-Object Uri($baseFull)
    $pathUri = New-Object Uri($pathFull)
    return Normalize-PathText ([Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()))
}

function Convert-WildcardToRegex {
    param([string]$Pattern)
    $pattern = Normalize-PathText $Pattern
    $builder = New-Object Text.StringBuilder
    for ($i = 0; $i -lt $pattern.Length; $i++) {
        $char = $pattern[$i]
        if ($char -eq '*' -and ($i + 1) -lt $pattern.Length -and $pattern[$i + 1] -eq '*') {
            [void]$builder.Append('.*')
            $i++
        } elseif ($char -eq '*') {
            [void]$builder.Append('[^/]*')
        } elseif ($char -eq '?') {
            [void]$builder.Append('[^/]')
        } else {
            [void]$builder.Append([regex]::Escape([string]$char))
        }
    }
    return '^' + $builder.ToString() + '$'
}

function Test-GraphPattern {
    param([string]$Path, [string]$Pattern)
    $normalizedPath = Normalize-PathText $Path
    $normalizedPattern = Normalize-PathText $Pattern
    if ([regex]::IsMatch($normalizedPath, (Convert-WildcardToRegex $normalizedPattern), [Text.RegularExpressions.RegexOptions]::IgnoreCase)) { return $true }
    if ($normalizedPattern -notmatch '/') {
        return [IO.Path]::GetFileName($normalizedPath) -like $normalizedPattern
    }
    return $false
}

function Add-Unique {
    param([System.Collections.IList]$List, [string]$Value)
    if (-not [string]::IsNullOrWhiteSpace($Value) -and -not $List.Contains($Value)) { [void]$List.Add($Value) }
}

function Get-JsonArray {
    param([object]$Value)
    if ($null -eq $Value) { return @() }
    return @($Value)
}

function Read-ChangedFileInput {
    param([string[]]$Inline, [string]$Path)
    $values = New-Object System.Collections.Generic.List[string]
    foreach ($item in @( $Inline )) { if (-not [string]::IsNullOrWhiteSpace($item)) { [void]$values.Add($item) } }
    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        if (-not (Test-Path -LiteralPath $Path)) { throw "Changed-files input does not exist: $Path" }
        $raw = Get-Content -LiteralPath $Path -Raw
        try {
            $json = $raw | ConvertFrom-Json
            if ($json -is [System.Collections.IEnumerable] -and $json -isnot [string]) {
                foreach ($item in $json) { if (-not [string]::IsNullOrWhiteSpace([string]$item)) { [void]$values.Add([string]$item) } }
            } else { [void]$values.Add([string]$json) }
        } catch {
            foreach ($line in ($raw -split "`r?`n")) { if (-not [string]::IsNullOrWhiteSpace($line)) { [void]$values.Add($line.Trim()) } }
        }
    }
    return @($values | Select-Object -Unique)
}

function Get-GitChangedFiles {
    param([string]$RepoPath, [string]$RelativeRepoPath, [string]$Ref)
    if (-not (Test-Path -LiteralPath $RepoPath)) { throw "Configured repository does not exist: $RepoPath" }
    $inside = ([string](git -C $RepoPath rev-parse --is-inside-work-tree 2>$null)).Trim()
    if ($LASTEXITCODE -ne 0 -or $inside -ne 'true') { throw "Configured repository is not a Git worktree: $RepoPath" }
    if (-not [string]::IsNullOrWhiteSpace($Ref)) {
        $null = git -C $RepoPath rev-parse --verify "$Ref^{commit}" 2>$null
        if ($LASTEXITCODE -ne 0) { throw "BaseRef '$Ref' is unavailable in $RepoPath." }
    }
    $files = New-Object System.Collections.Generic.List[string]
    $status = @(git -C $RepoPath status --porcelain=v1 --untracked-files=all 2>$null)
    if ($LASTEXITCODE -ne 0) { throw "git status failed in $RepoPath." }
    foreach ($line in $status) {
        if ($line.Length -gt 3) {
            $path = $line.Substring(3).Trim()
            if ($path -match ' -> ') { $path = ($path -split ' -> ')[-1] }
            [void]$files.Add((Normalize-PathText "$RelativeRepoPath/$path"))
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($Ref)) {
        $diff = @(git -C $RepoPath diff --name-only "$Ref...HEAD" 2>$null)
        if ($LASTEXITCODE -ne 0) { throw "git diff against '$Ref' failed in $RepoPath." }
        foreach ($path in $diff) { if (-not [string]::IsNullOrWhiteSpace($path)) { [void]$files.Add((Normalize-PathText "$RelativeRepoPath/$path")) } }
    }
    return @($files | Select-Object -Unique)
}

function Find-Owner {
    param([string]$Path, [object]$Graph)
    $normalized = Normalize-PathText $Path
    foreach ($repo in @($Graph.repositories)) {
        $repoPath = Normalize-PathText ([string]$repo.path)
        if ($normalized -eq $repoPath -or $normalized.StartsWith($repoPath + '/', [StringComparison]::OrdinalIgnoreCase)) {
            return [pscustomobject]@{ Kind = 'mod'; Id = [string]$repo.id; Repo = $repo; Relative = $normalized.Substring($repoPath.Length).Trim('/') }
        }
    }
    $manager = $Graph.manager
    $managerPath = Normalize-PathText ([string]$manager.path)
    if ($normalized -eq $managerPath -or $normalized.StartsWith($managerPath + '/', [StringComparison]::OrdinalIgnoreCase)) {
        return [pscustomobject]@{ Kind = 'manager'; Id = [string]$manager.id; Repo = $manager; Relative = $normalized.Substring($managerPath.Length).Trim('/') }
    }
    return $null
}

function Get-Effects {
    param([string]$FullPath, [string]$RelativePath, [object]$Graph)
    $effects = New-Object System.Collections.Generic.List[string]
    $matched = New-Object System.Collections.Generic.List[string]
    $isDoc = $false
    $isTest = $false
    $isCode = $false
    foreach ($rule in @($Graph.sharedRules)) {
        $matches = $false
        foreach ($pattern in @($rule.patterns)) {
            if (Test-GraphPattern $FullPath ([string]$pattern) -or Test-GraphPattern $RelativePath ([string]$pattern)) { $matches = $true; break }
        }
        if ($matches) {
            Add-Unique $matched ([string]$rule.id)
            foreach ($effect in @($rule.effects)) { Add-Unique $effects ([string]$effect) }
            if ([string]$rule.id -eq 'documentation') { $isDoc = $true }
            if ([string]$rule.id -eq 'test-only') { $isTest = $true }
            if ([string]$rule.id -eq 'source' -or [string]$rule.id -eq 'runtime-data') { $isCode = $true }
        }
    }
    if ($isDoc -and -not $isCode) {
        $effects = New-Object System.Collections.Generic.List[string]
        Add-Unique $effects 'documentation-only'
    } elseif ($isTest -and -not $isCode) {
        $effects = New-Object System.Collections.Generic.List[string]
        Add-Unique $effects 'contracts-only'
    } elseif ($effects.Count -eq 0) {
        Add-Unique $effects 'source'
        Add-Unique $effects 'build'
        Add-Unique $effects 'contracts'
        Add-Unique $effects 'gameplay'
        Add-Unique $effects 'platform-smoke'
        Add-Unique $effects 'package'
        Add-Unique $effects 'promotion'
    }
    return [pscustomobject]@{ Effects = @($effects); Rules = @($matched) }
}

function New-RepoPlan {
    param([string]$Id, [string]$Kind, [object]$Definition)
    return [pscustomobject]@{
        id = $Id; kind = $Kind; definition = $Definition; changedFiles = New-Object System.Collections.Generic.List[string]
        changeClasses = New-Object System.Collections.Generic.List[string]; rules = New-Object System.Collections.Generic.List[string]
        build = $false; contract = $false; gameplay = New-Object System.Collections.Generic.List[string]
        platformSmoke = $false; package = $false; promotion = $false; reasons = New-Object System.Collections.Generic.List[string]
    }
}

function Get-Scenario {
    param([object]$Graph, [string]$Id)
    return @($Graph.scenarios | Where-Object { [string]$_.id -eq $Id })[0]
}

function Add-ScenarioToPlan {
    param([object]$Plan, [string]$ScenarioId, [string]$Reason)
    Add-Unique $Plan.gameplay $ScenarioId
    Add-Unique $Plan.reasons $Reason
}

function Add-ScenariosForChange {
    param([object]$Plan, [string]$RelativePath, [string]$Reason)
    $matched = $false
    if ($Plan.definition.PSObject.Properties['scenarioRules']) {
        foreach ($rule in @($Plan.definition.scenarioRules)) {
            $ruleMatches = $false
            foreach ($pattern in @($rule.patterns)) {
                if (Test-GraphPattern $RelativePath ([string]$pattern)) { $ruleMatches = $true; break }
            }
            if ($ruleMatches) {
                $matched = $true
                foreach ($scenario in @($rule.scenarios)) { Add-ScenarioToPlan $Plan ([string]$scenario) "$Reason; matched scenario rule $($rule.patterns -join ', ')" }
            }
        }
    }
    if (-not $matched -and $Plan.definition.PSObject.Properties['scenarios']) {
        foreach ($scenario in @($Plan.definition.scenarios)) { Add-ScenarioToPlan $Plan ([string]$scenario) "$Reason; shared/core change uses repository fallback" }
    }
}

function Get-HarnessUrlForPlatform {
    param([string]$Platform)
    switch ($Platform) {
        'Steam' { if ($SteamHarnessUrl) { return $SteamHarnessUrl } }
        'Epic' { if ($EpicHarnessUrl) { return $EpicHarnessUrl } }
    }
    if ($HarnessUrl) { return $HarnessUrl }
    return $null
}

function Resolve-DirectoryPath {
    param([string]$ExplicitPath, [string]$Label, [string[]]$Candidates)
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $full = [IO.Path]::GetFullPath($ExplicitPath)
        if (-not (Test-Path -LiteralPath $full -PathType Container)) { throw "$Label does not exist: $full" }
        return $full
    }
    foreach ($candidate in @($Candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $full = [IO.Path]::GetFullPath($candidate)
        if (Test-Path -LiteralPath $full -PathType Container) { return $full }
    }
    throw "$Label was not supplied and no safe default was discovered. Checked: $($Candidates -join '; ')"
}

function Get-AutomaticHarnessRepo {
    if ($null -eq $script:resolvedHarnessRepo) {
        $environmentRoot = [Environment]::GetEnvironmentVariable('SHELTERED_AGENT_INTERFACE_ROOT')
        $script:resolvedHarnessRepo = Resolve-DirectoryPath $HarnessRepo 'Sheltered Agent Interface harness repository' @(
            $environmentRoot,
            (Join-Path $root '..\..\..\..\Projects\ShelteredAgentInterface'),
            'A:\Dev\Projects\ShelteredAgentInterface'
        )
    }
    return $script:resolvedHarnessRepo
}

function Get-AutomaticGameRoot {
    param([string]$Platform)
    if ($Platform -eq 'Steam') {
        if ($null -eq $script:resolvedSteamGameRoot) {
            $environmentRoot = [Environment]::GetEnvironmentVariable('SHELTERED_STEAM_GAME_ROOT')
            $script:resolvedSteamGameRoot = Resolve-DirectoryPath $SteamGameRoot 'Steam Sheltered game root' @(
                $environmentRoot,
                'A:\SteamLibrary\steamapps\common\Sheltered',
                'C:\Program Files (x86)\Steam\steamapps\common\Sheltered',
                'C:\Program Files\Steam\steamapps\common\Sheltered'
            )
        }
        return $script:resolvedSteamGameRoot
    }
    if ($Platform -eq 'Epic') {
        if ($null -eq $script:resolvedEpicGameRoot) {
            $environmentRoot = [Environment]::GetEnvironmentVariable('SHELTERED_EPIC_GAME_ROOT')
            $script:resolvedEpicGameRoot = Resolve-DirectoryPath $EpicGameRoot 'Epic Sheltered game root' @(
                $environmentRoot,
                'D:\Epic Games Games\Sheltered',
                'C:\Program Files\Epic Games\Sheltered',
                'C:\Program Files (x86)\Epic Games\Sheltered'
            )
        }
        return $script:resolvedEpicGameRoot
    }
    throw "Automatic transaction mode does not support platform '$Platform'."
}

function Get-AutomaticTransactionRunnerPath {
    if ($null -eq $script:resolvedTransactionRunnerPath) {
        $harnessRepo = Get-AutomaticHarnessRepo
        $candidate = if ([string]::IsNullOrWhiteSpace($TransactionRunnerPath)) { Join-Path $harnessRepo 'tools\Invoke-TransactionalReleaseScenario.ps1' } else { $TransactionRunnerPath }
        $full = [IO.Path]::GetFullPath($candidate)
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "Transactional release runner is missing: $full" }
        $script:resolvedTransactionRunnerPath = $full
    }
    return $script:resolvedTransactionRunnerPath
}

function Get-HarnessExecutionMode {
    param([string]$Platform)
    if (Get-HarnessUrlForPlatform $Platform) { return 'url' }
    return 'transaction-runner'
}

function Invoke-HarnessRoute {
    param([string]$BaseUrl, [string]$Path, [hashtable]$Query)
    $pairs = New-Object System.Collections.Generic.List[string]
    foreach ($key in $Query.Keys) {
        if ($null -ne $Query[$key] -and [string]$Query[$key].Length -gt 0) {
            [void]$pairs.Add(('{0}={1}' -f [Uri]::EscapeDataString([string]$key), [Uri]::EscapeDataString([string]$Query[$key])))
        }
    }
    $uri = $BaseUrl.TrimEnd('/') + $Path
    if ($pairs.Count -gt 0) { $uri += '?' + ($pairs -join '&') }
    $response = Invoke-RestMethod -Method Get -Uri $uri -TimeoutSec 180
    return [pscustomobject]@{ Uri = $uri; Response = $response }
}

function Assert-ScopedPackageProvenance {
    param([object]$Gate, [string]$Root)
    if ([string]$Gate.scope -eq 'mod-rc') {
        $manifestPath = Join-Path $Root 'release\2.0\release-manifest.json'
        if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Canonical release manifest is missing: $manifestPath" }
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $entry = @($manifest.modPackages | Where-Object { [string]$_.project -eq [string]$Gate.owner })[0]
        if ($null -eq $entry) { throw "Canonical manifest has no mod entry for $($Gate.owner)." }
        $zipPath = Join-Path $Root ('release\2.0\artifacts\mods\' + [string]$entry.filename)
        $sidecarPath = $zipPath + '.sha256'
        foreach ($path in @($zipPath, $sidecarPath)) { if (-not (Test-Path -LiteralPath $path)) { throw "Scoped package artifact is missing: $path" } }
        $actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
        if ([int64](Get-Item -LiteralPath $zipPath).Length -ne [int64]$entry.bytes) { throw "Scoped package byte count is stale for $($Gate.owner)." }
        if ($actualHash -ne ([string]$entry.sha256).ToUpperInvariant()) { throw "Scoped package hash is stale for $($Gate.owner)." }
        $sidecar = (Get-Content -LiteralPath $sidecarPath -Raw).Trim()
        if ($sidecar -notmatch ('^(?<hash>[0-9a-fA-F]{64})\s+' + [regex]::Escape([string]$entry.filename) + '\s*$')) { throw "Scoped package sidecar is invalid for $($Gate.owner)." }
        if ($Matches.hash.ToUpperInvariant() -ne $actualHash) { throw "Scoped package sidecar hash is stale for $($Gate.owner)." }
        $repoPath = Join-Path $Root ([string](@($graph.repositories | Where-Object { [string]$_.id -eq [string]$Gate.owner })[0].path))
        $head = ([string](git -C $repoPath rev-parse HEAD 2>$null)).Trim()
        if ($head -ne ([string]$entry.commit).Trim()) { throw "Scoped package commit provenance is stale for $($Gate.owner)." }
        $branch = ([string](git -C $repoPath branch --show-current 2>$null)).Trim()
        if ($branch -ne 'main') { throw "Scoped package owner is not on main: $($Gate.owner) ($branch)." }
        if (@(git -C $repoPath status --porcelain 2>$null).Count -gt 0) { throw "Scoped package owner is dirty: $($Gate.owner)." }
        return
    }
    if ([string]$Gate.scope -eq 'manager-rc') {
        $managerRoot = Join-Path $Root ([string]$graph.manager.path)
        $fragmentPath = Join-Path $managerRoot 'artifacts\release-packages\release-manifest.fragment.json'
        if (-not (Test-Path -LiteralPath $fragmentPath)) { throw "Manager release fragment is missing: $fragmentPath" }
        $fragment = @(Get-Content -LiteralPath $fragmentPath -Raw | ConvertFrom-Json)
        if ($fragment.Count -ne 3) { throw "Manager release fragment must contain three archives; found $($fragment.Count)." }
        foreach ($entry in $fragment) {
            $zipPath = Join-Path (Split-Path -Parent $fragmentPath) ([string]$entry.filename)
            $sidecarPath = $zipPath + '.sha256'
            foreach ($path in @($zipPath, $sidecarPath)) { if (-not (Test-Path -LiteralPath $path)) { throw "Manager artifact is missing: $path" } }
            $actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
            if ([int64](Get-Item -LiteralPath $zipPath).Length -ne [int64]$entry.bytes) { throw "Manager artifact byte count is stale: $($entry.filename)." }
            if ($actualHash -ne ([string]$entry.sha256).ToUpperInvariant()) { throw "Manager artifact hash is stale: $($entry.filename)." }
        }
        $head = ([string](git -C $managerRoot rev-parse HEAD 2>$null)).Trim()
        if (@($fragment | Where-Object { [string]$_.commit -ne $head }).Count -gt 0) { throw 'Manager artifact commit provenance is stale.' }
        if (([string](git -C $managerRoot branch --show-current 2>$null)).Trim() -ne 'main') { throw 'Manager release owner is not on main.' }
        if (@(git -C $managerRoot status --porcelain 2>$null).Count -gt 0) { throw 'Manager release owner is dirty.' }
    }
}

function Get-Sha256Text {
    param([string]$Text)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes([string]$Text)
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    } finally { $sha.Dispose() }
}

function Get-RepositoryDefinitionsForGate {
    param([object]$Gate, [object]$Graph)
    $owners = New-Object System.Collections.Generic.List[object]
    $ownerDefinition = @($Graph.repositories | Where-Object { [string]$_.id -eq [string]$Gate.owner }) | Select-Object -First 1
    if ($null -eq $ownerDefinition -and [string]$Gate.owner -eq [string]$Graph.manager.id) { $ownerDefinition = $Graph.manager }
    if ($null -ne $ownerDefinition) { [void]$owners.Add($ownerDefinition) }
    if ($Gate.PSObject.Properties['owners']) {
        foreach ($ownerId in @($Gate.owners)) {
            $definition = @($Graph.repositories | Where-Object { [string]$_.id -eq [string]$ownerId }) | Select-Object -First 1
            if ($null -ne $definition -and -not (@($owners | ForEach-Object { [string]$_.id }) -contains [string]$definition.id)) { [void]$owners.Add($definition) }
        }
    }

    if ($Gate.kind -eq 'gameplay' -and $Gate.PSObject.Properties['scenario']) {
        foreach ($edge in @($Graph.dependencyEdges | Where-Object { [string]$_.scenario -eq [string]$Gate.scenario })) {
            foreach ($edgeOwner in @([string]$edge.provider, [string]$edge.consumer)) {
                $definition = @($Graph.repositories | Where-Object { [string]$_.id -eq $edgeOwner }) | Select-Object -First 1
                if ($null -ne $definition -and -not (@($owners | ForEach-Object { [string]$_.id }) -contains [string]$definition.id)) { [void]$owners.Add($definition) }
            }
        }
    }
    return @($owners | Sort-Object id)
}

function Get-RepositoryFilesForGate {
    param([object]$Gate, [object]$Graph, [string]$Root)
    $paths = New-Object System.Collections.Generic.List[string]
    $owners = @(Get-RepositoryDefinitionsForGate $Gate $Graph)

    foreach ($definition in $owners) {
        $repoRoot = Join-Path $Root ([string]$definition.path)
        if (-not (Test-Path -LiteralPath $repoRoot)) { throw "Evidence input repository is missing: $repoRoot" }
        $patterns = New-Object System.Collections.Generic.List[string]
        if ($Gate.kind -eq 'gameplay' -and $definition.PSObject.Properties['scenarioRules']) {
            foreach ($rule in @($definition.scenarioRules)) {
                if (@($rule.scenarios | ForEach-Object { [string]$_ }) -contains [string]$Gate.scenario) {
                    foreach ($pattern in @($rule.patterns)) { Add-Unique $patterns ([string]$pattern) }
                }
            }
        }
        $gitFiles = @(git -C $repoRoot ls-files --cached --others --exclude-standard 2>$null)
        if ($LASTEXITCODE -ne 0) { throw "Unable to enumerate evidence inputs in $repoRoot." }
        foreach ($relative in $gitFiles) {
            $normalized = Normalize-PathText ([string]$relative)
            if ($normalized -match '(^|/)(bin|obj|artifacts|packages)(/|$)') { continue }
            if ($patterns.Count -gt 0) {
                $included = $false
                foreach ($pattern in $patterns) { if (Test-GraphPattern $normalized $pattern) { $included = $true; break } }
                if (-not $included) { continue }
            }
            $absolute = Join-Path $repoRoot $relative
            if (Test-Path -LiteralPath $absolute -PathType Leaf) { [void]$paths.Add([IO.Path]::GetFullPath($absolute)) }
        }
    }
    return @($paths | Select-Object -Unique | Sort-Object)
}

function Add-FingerprintInput {
    param([System.Collections.IList]$Inputs, [string]$Label, [string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required fingerprint input is missing: $Path" }
    [void]$Inputs.Add([pscustomobject]@{ label = (Normalize-PathText $Label); path = [IO.Path]::GetFullPath($Path) })
}

function Add-RuntimeDirectoryFingerprintInputs {
    param([System.Collections.IList]$Inputs, [string]$GameRoot, [string[]]$Directories)
    foreach ($directory in @($Directories | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } | Select-Object -Unique | Sort-Object)) {
        $relative = Normalize-PathText ([string]$directory)
        $full = Join-Path (Join-Path $GameRoot 'mods') $directory
        if (-not (Test-Path -LiteralPath $full -PathType Container)) { throw "Required runtime mod directory is missing: $full" }
        [void]$Inputs.Add([pscustomobject]@{ label = "runtime/$relative/.directory"; path = $null; marker = 'present' })
        foreach ($file in @(Get-ChildItem -LiteralPath $full -Recurse -File -ErrorAction Stop | Sort-Object FullName)) {
            [void]$Inputs.Add([pscustomobject]@{ label = "runtime/$relative/" + (Get-RelativePathText $full $file.FullName); path = $file.FullName })
        }
    }
}

function Get-FingerprintInputLines {
    param([System.Collections.IEnumerable]$Inputs)
    foreach ($input in @($Inputs | Sort-Object label, path)) {
        if ($null -ne $input.path) {
            "file=$($input.label)|$((Get-FileHash -LiteralPath $input.path -Algorithm SHA256).Hash.ToLowerInvariant())"
        } else {
            "marker=$($input.label)|$($input.marker)"
        }
    }
}

function Get-LiveHarnessFingerprint {
    param([string]$BaseUrl, [object]$Gate, [object]$Graph, [string]$Root)
    if ([string]::IsNullOrWhiteSpace($BaseUrl)) { throw 'A live harness URL is required to fingerprint gameplay evidence.' }
    $status = Invoke-RestMethod -Method Get -Uri ($BaseUrl.TrimEnd('/') + '/status') -TimeoutSec 15
    if ($null -eq $status -or -not ($status.PSObject.Properties.Name -contains 'ok') -or -not [bool]$status.ok) { throw "Harness status failed at $BaseUrl." }
    $gameRoot = [string]$status.gameRoot
    if ([string]::IsNullOrWhiteSpace($gameRoot) -or -not (Test-Path -LiteralPath $gameRoot)) { throw "Harness did not expose a readable game root at $BaseUrl." }
    $inputs = New-Object System.Collections.Generic.List[object]
    $scenario = if ($Gate.kind -eq 'gameplay' -and $Gate.PSObject.Properties['scenario']) { Get-Scenario $Graph ([string]$Gate.scenario) } else { $null }
    $harnessRepo = Get-AutomaticHarnessRepo
    foreach ($relative in @($Graph.defaults.harnessSharedInputs)) {
        Add-FingerprintInput $inputs ("harness/" + (Normalize-PathText $relative)) (Join-Path $harnessRepo ($relative -replace '/', '\'))
    }
    if ($null -ne $scenario -and $scenario.PSObject.Properties['harnessInputs']) {
        foreach ($relative in @($scenario.harnessInputs)) {
            Add-FingerprintInput $inputs ("harness/" + (Normalize-PathText $relative)) (Join-Path $harnessRepo ($relative -replace '/', '\'))
        }
    }
    $managed = Join-Path $gameRoot 'Sheltered_Data\Managed\Assembly-CSharp.dll'
    if (-not (Test-Path -LiteralPath $managed)) { $managed = Join-Path $gameRoot 'ShelteredWindows64_EOS_Data\Managed\Assembly-CSharp.dll' }
    Add-FingerprintInput $inputs 'game/Assembly-CSharp.dll' $managed
    $loadOrder = Join-Path $gameRoot 'mods\loadorder.json'
    Add-FingerprintInput $inputs 'game/mods/loadorder.json' $loadOrder
    $runtimeDirectories = New-Object System.Collections.Generic.List[string]
    foreach ($directory in @($Graph.defaults.runtimeSharedModDirectories)) { Add-Unique $runtimeDirectories ([string]$directory) }
    foreach ($definition in @(Get-RepositoryDefinitionsForGate $Gate $Graph)) {
        foreach ($directory in @($definition.runtimeModDirectories)) { Add-Unique $runtimeDirectories ([string]$directory) }
    }
    Add-RuntimeDirectoryFingerprintInputs $inputs $gameRoot $runtimeDirectories
    $content = ((Get-FingerprintInputLines $inputs) -join "`n") + "`nurl=" + $BaseUrl.TrimEnd('/')
    return Get-Sha256Text $content
}

function Get-AutomaticHarnessFingerprint {
    param([object]$Gate, [object]$Scenario, [object]$Graph, [string]$Root)
    $harnessRepo = Get-AutomaticHarnessRepo
    $gameRoot = Get-AutomaticGameRoot ([string]$Gate.platform)
    $runner = Get-AutomaticTransactionRunnerPath
    $inputs = New-Object System.Collections.Generic.List[object]
    Add-FingerprintInput $inputs 'harness/transaction-runner.ps1' $runner
    foreach ($relative in @($Graph.defaults.harnessSharedInputs)) {
        Add-FingerprintInput $inputs ("harness/" + (Normalize-PathText $relative)) (Join-Path $harnessRepo ($relative -replace '/', '\'))
    }
    if ($Scenario.PSObject.Properties['harnessInputs']) {
        foreach ($relative in @($Scenario.harnessInputs)) {
            Add-FingerprintInput $inputs ("harness/" + (Normalize-PathText $relative)) (Join-Path $harnessRepo ($relative -replace '/', '\'))
        }
    }
    $managed = Join-Path $gameRoot 'Sheltered_Data\Managed\Assembly-CSharp.dll'
    if (-not (Test-Path -LiteralPath $managed)) { $managed = Join-Path $gameRoot 'ShelteredWindows64_EOS_Data\Managed\Assembly-CSharp.dll' }
    Add-FingerprintInput $inputs 'game/Assembly-CSharp.dll' $managed
    Add-FingerprintInput $inputs 'game/mods/loadorder.json' (Join-Path $gameRoot 'mods\loadorder.json')
    $runtimeDirectories = New-Object System.Collections.Generic.List[string]
    foreach ($directory in @($Graph.defaults.runtimeSharedModDirectories)) { Add-Unique $runtimeDirectories ([string]$directory) }
    foreach ($definition in @(Get-RepositoryDefinitionsForGate $Gate $Graph)) {
        foreach ($directory in @($definition.runtimeModDirectories)) { Add-Unique $runtimeDirectories ([string]$directory) }
    }
    Add-RuntimeDirectoryFingerprintInputs $inputs $gameRoot $runtimeDirectories
    $content = ((Get-FingerprintInputLines $inputs) -join "`n") + "`nplatform=" + [string]$Gate.platform + "`nscenario=" + [string]$Scenario.id
    return Get-Sha256Text $content
}

function Get-RelevantGraphSlice {
    param([object]$Gate, [object]$Graph)
    $definitions = @(Get-RepositoryDefinitionsForGate $Gate $Graph)
    $scenario = if ($Gate.PSObject.Properties['scenario']) { Get-Scenario $Graph ([string]$Gate.scenario) } else { $null }
    $edges = if ($Gate.PSObject.Properties['scenario']) { @($Graph.dependencyEdges | Where-Object { [string]$_.scenario -eq [string]$Gate.scenario } | Sort-Object provider, consumer, scenario) } else { @() }
    $defaults = [ordered]@{
        configuration = [string]$Graph.defaults.configuration
        platforms = @($Graph.defaults.platforms)
        runtimeSharedModDirectories = @($Graph.defaults.runtimeSharedModDirectories)
        harnessSharedInputs = @($Graph.defaults.harnessSharedInputs)
    }
    return [ordered]@{
        defaults = $defaults
        repositories = @($definitions | ForEach-Object { $_ | ConvertTo-Json -Depth 8 -Compress } | Sort-Object)
        dependencyEdges = @($edges | ForEach-Object { $_ | ConvertTo-Json -Depth 8 -Compress })
        scenario = if ($null -ne $scenario) { $scenario | ConvertTo-Json -Depth 8 -Compress } else { $null }
    } | ConvertTo-Json -Depth 12 -Compress
}

function Get-GateFingerprint {
    param([object]$Gate, [object]$Graph, [string]$Root)
    $parts = New-Object System.Collections.Generic.List[string]
    [void]$parts.Add('configuration=' + $Configuration)
    [void]$parts.Add('gate=' + ($Gate | ConvertTo-Json -Depth 8 -Compress))
    if ([string]$Gate.owner -eq 'release-layer') {
        [void]$parts.Add('graph-validation=' + ((Get-FileHash -LiteralPath $GraphPath -Algorithm SHA256).Hash.ToLowerInvariant()))
    } else {
        [void]$parts.Add('graph-slice=' + (Get-RelevantGraphSlice $Gate $Graph))
    }
    foreach ($path in @(Get-RepositoryFilesForGate $Gate $Graph $Root)) {
        [void]$parts.Add(('file={0}|{1}' -f (Get-RelativePathText $Root $path), ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant())))
    }
    if ($Gate.kind -eq 'gameplay') {
        $baseUrl = Get-HarnessUrlForPlatform ([string]$Gate.platform)
        if ($baseUrl) { [void]$parts.Add('harness=' + (Get-LiveHarnessFingerprint $baseUrl $Gate $Graph $Root)) }
        else {
            $scenario = Get-Scenario $Graph ([string]$Gate.scenario)
            [void]$parts.Add('harness=' + (Get-AutomaticHarnessFingerprint $Gate $scenario $Graph $Root))
        }
    } elseif ($Gate.kind -eq 'platform-smoke') {
        $baseUrl = Get-HarnessUrlForPlatform ([string]$Gate.platform)
        [void]$parts.Add('harness=' + $(if ($baseUrl) { Get-LiveHarnessFingerprint $baseUrl $Gate $Graph $Root } else { 'missing-url' }))
    }
    return Get-Sha256Text ($parts -join "`n")
}

function Get-EvidencePath {
    param([object]$Gate)
    $safe = ([string]$Gate.id -replace '[^A-Za-z0-9._-]', '_')
    return Join-Path ([IO.Path]::GetFullPath($EvidenceRoot)) ($safe + '.json')
}

function Get-AutomaticTransactionEvidenceRoot {
    param([object]$Gate, [string]$Fingerprint)
    $safe = ([string]$Gate.id -replace '[^A-Za-z0-9._-]', '_')
    return Join-Path (Join-Path ([IO.Path]::GetFullPath($EvidenceRoot)) 'transactions') ($safe + '-' + $Fingerprint)
}

function Get-ReusableEvidence {
    param([object]$Gate, [string]$Fingerprint)
    if ($NoEvidenceReuse -or $Gate.kind -eq 'promotion') { return $null }
    $path = Get-EvidencePath $Gate
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    try { $receipt = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json } catch { return $null }
    if ([string]$receipt.gateId -ne [string]$Gate.id -or [string]$receipt.status -ne 'passed' -or [string]$receipt.fingerprint -ne $Fingerprint -or [string]$receipt.configuration -ne $Configuration) { return $null }
    if ($Gate.kind -eq 'gameplay' -and (Get-HarnessExecutionMode ([string]$Gate.platform)) -eq 'transaction-runner') {
        if (-not $receipt.PSObject.Properties['transactionReportPath'] -or [string]::IsNullOrWhiteSpace([string]$receipt.transactionReportPath)) { return $null }
        $expectedRoot = Get-AutomaticTransactionEvidenceRoot $Gate $Fingerprint
        try {
            if ([IO.Path]::GetFullPath((Split-Path -Parent ([string]$receipt.transactionReportPath))) -ne [IO.Path]::GetFullPath($expectedRoot)) { return $null }
            Assert-TransactionReport $Gate (Get-Scenario $graph ([string]$Gate.scenario)) ([string]$receipt.transactionReportPath) $expectedRoot
        } catch { return $null }
    }
    return $receipt
}

function Save-GateEvidence {
    param([object]$Gate, [string]$Fingerprint, [object]$Result)
    $path = Get-EvidencePath $Gate
    $directory = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $receipt = [ordered]@{
        schemaVersion = 1
        gateId = [string]$Gate.id
        owner = [string]$Gate.owner
        platform = if ($Gate.PSObject.Properties['platform']) { [string]$Gate.platform } else { $null }
        scenario = if ($Gate.PSObject.Properties['scenario']) { [string]$Gate.scenario } else { $null }
        configuration = $Configuration
        fingerprint = $Fingerprint
        status = 'passed'
        passedUtc = [DateTime]::UtcNow.ToString('o')
        detail = [string]$Result.detail
        transactionReportPath = if ($Result.PSObject.Properties.Name -contains 'transactionReportPath') { [string]$Result.transactionReportPath } else { $null }
        harnessMode = if ($Gate.kind -eq 'gameplay') { Get-HarnessExecutionMode ([string]$Gate.platform) } else { $null }
    }
    $receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Assert-TransactionReport {
    param([object]$Gate, [object]$Scenario, [string]$ReportPath, [string]$ExpectedEvidenceRoot)
    if ([string]::IsNullOrWhiteSpace($ReportPath) -or -not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) { throw "Transaction report is missing: $ReportPath" }
    $report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
    if ([int]$report.schemaVersion -ne 1) { throw "Unsupported transaction report schema: $ReportPath" }
    if (-not [bool]$report.ok -or [string]$report.platform -ne [string]$Gate.platform -or [string]$report.scenario -ne [string]$Gate.scenario) { throw "Transaction report identity/result is invalid: $ReportPath" }
    if ([string]$report.route -ne [string]$Scenario.fixturePath) { throw "Transaction report route does not match the release graph: $ReportPath" }
    if ($null -eq $report.health -or -not [bool]$report.health.ok) { throw "Transaction report health evidence is invalid: $ReportPath" }
    if ($null -eq $report.result -or -not [bool]$report.result.ok) { throw "Transaction report scenario result is invalid: $ReportPath" }
    if ($null -eq $report.restoration -or -not [bool]$report.restoration.ok) { throw "Transaction report restoration evidence is invalid: $ReportPath" }
    $reportRoot = [IO.Path]::GetFullPath([string]$report.evidenceRoot)
    $expectedRoot = [IO.Path]::GetFullPath($ExpectedEvidenceRoot)
    if ($reportRoot -ne $expectedRoot -or [IO.Path]::GetFullPath((Split-Path -Parent $ReportPath)) -ne $expectedRoot) { throw "Transaction report evidence root is not the expected manager-owned root: $ReportPath" }
    return $report
}

function Invoke-AutomaticTransaction {
    param([object]$Gate, [object]$Graph, [string]$Root, [string]$Fingerprint)
    $scenario = Get-Scenario $Graph ([string]$Gate.scenario)
    if ($null -eq $scenario -or [string]::IsNullOrWhiteSpace([string]$scenario.fixturePath)) { throw "Release scenario is not defined: $($Gate.scenario)" }
    if (@('/release-scenario/interaction', '/release-scenario/progression') -notcontains [string]$scenario.fixturePath) { throw "Automatic transaction mode requires a completion-backed scenario route; '$($scenario.fixturePath)' is not supported." }
    $steps = @($scenario.steps)
    if ($steps.Count -ne 1 -or -not $steps[0].PSObject.Properties['scenario'] -or [string]$steps[0].scenario -ne [string]$scenario.id) { throw "Automatic transaction mode requires one completion-backed step with scenario=$($scenario.id)." }
    $argument = if ($steps[0].PSObject.Properties['argument'] -and $steps[0].argument) { [string]$steps[0].argument } else { 'confirm=true' }
    if ($argument -ne 'confirm=true') { throw "Automatic transaction mode requires confirm=true for $($scenario.id)." }
    $harnessRepo = Get-AutomaticHarnessRepo
    $evidencePath = Get-AutomaticTransactionEvidenceRoot $Gate $Fingerprint
    $runnerArgs = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Get-AutomaticTransactionRunnerPath),
        '-Platform', [string]$Gate.platform,
        '-GameRoot', (Get-AutomaticGameRoot ([string]$Gate.platform)),
        '-HarnessBuildRoot', (Join-Path $harnessRepo 'Assemblies'),
        '-Route', [string]$scenario.fixturePath,
        '-Scenario', [string]$scenario.id,
        '-Argument', $argument,
        '-EvidenceRoot', $evidencePath,
        '-TimeoutSeconds', [string]$TransactionTimeoutSeconds
    )
    $runnerOutput = @(& powershell @runnerArgs 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Transactional runner exited $LASTEXITCODE for $($Gate.id): $($runnerOutput | Select-Object -Last 3 | ForEach-Object { [string]$_ } -join ' ')" }
    $reportPath = Join-Path $evidencePath 'transaction-report.json'
    $null = Assert-TransactionReport $Gate $scenario $reportPath $evidencePath
    return $reportPath
}

function Invoke-Gate {
    param([object]$Gate, [object]$Graph, [string]$Root)
    $result = [ordered]@{ id = $Gate.id; phase = $Gate.phase; kind = $Gate.kind; status = 'planned'; detail = $Gate.detail; fingerprint = $null; evidencePath = $null }
    if (-not $Execute) { return [pscustomobject]$result }
    if ([bool]$Gate.heavy -and -not $AllowHeavy) {
        $result.status = 'skipped-heavy'; $result.detail = 'Heavy gameplay gate requires -AllowHeavy; no state-heavy matrix was run.'
        return [pscustomobject]$result
    }
    try {
        $fingerprint = Get-GateFingerprint $Gate $Graph $Root
        $result.fingerprint = $fingerprint
        $receipt = Get-ReusableEvidence $Gate $fingerprint
        if ($null -ne $receipt) {
            $result.status = 'reused-evidence'
            $result.detail = "Reused validated passed evidence from $($receipt.passedUtc)."
            $result.evidencePath = Get-EvidencePath $Gate
            return [pscustomobject]$result
        }
        if ($Gate.kind -eq 'build') {
            $msbuild = @('C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe', 'C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe') | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
            if (-not $msbuild) { throw 'MSBuild was not found.' }
            $project = Join-Path $Root ([string]$Gate.project)
            $null = & $msbuild $project /t:Rebuild /restore "/p:Configuration=$Configuration" /m /v:minimal
            if ($LASTEXITCODE -ne 0) { throw "Build exited $LASTEXITCODE." }
        } elseif ($Gate.kind -eq 'contract') {
            foreach ($script in @($Gate.scripts)) {
                $scriptPath = Join-Path $Root ([string]$script)
                if (-not (Test-Path -LiteralPath $scriptPath)) { throw "Contract script not found: $scriptPath" }
                $null = & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath
                if ($LASTEXITCODE -ne 0) { throw ("Contract script exited {0}: {1}" -f $LASTEXITCODE, $script) }
            }
            foreach ($project in @($Gate.projects)) {
                $projectPath = Join-Path $Root ([string]$project)
                if (-not (Test-Path -LiteralPath $projectPath)) { throw "Contract test project not found: $projectPath" }
                $null = & dotnet test $projectPath --configuration $Configuration --no-restore
                if ($LASTEXITCODE -ne 0) { throw ("Contract test project exited {0}: {1}" -f $LASTEXITCODE, $project) }
            }
            if (@($Gate.scripts).Count -eq 0 -and @($Gate.projects).Count -eq 0) {
                $result.status = 'not-configured'; $result.detail = 'No repository-local contract command is registered; gameplay/platform/package gates remain selected.'; return [pscustomobject]$result
            }
        } elseif ($Gate.kind -eq 'gameplay' -or $Gate.kind -eq 'platform-smoke') {
            $baseUrl = Get-HarnessUrlForPlatform ([string]$Gate.platform)
            if (-not $baseUrl -and $Gate.kind -eq 'platform-smoke') {
                $result.status = 'blocked'; $result.detail = "No harness URL supplied for $($Gate.platform); platform smoke requires -SteamHarnessUrl/-EpicHarnessUrl."; return [pscustomobject]$result
            }
            if (-not $baseUrl) {
                $result.transactionReportPath = Invoke-AutomaticTransaction $Gate $Graph $Root $fingerprint
                $result.detail = "Validated transaction-report.json evidence from $($result.transactionReportPath)."
            } else {
                $scenario = Get-Scenario $Graph ([string]$Gate.scenario)
                foreach ($step in @($scenario.steps)) {
                    $path = if ($step.route) { [string]$step.route } else { [string]$scenario.fixturePath }
                    $query = @{}
                    if ($step.action) { $query.action = [string]$step.action }
                    if ($step.PSObject.Properties['scenario'] -and $step.scenario) { $query.scenario = [string]$step.scenario }
                    if ($scenario.fixture) { $query.fixture = [string]$scenario.fixture }
                    if ($step.argument) {
                        if ($path -eq '/release-fixture/family-persistence') { $query.args = [string]$step.argument }
                        else { $query.argument = [string]$step.argument }
                    }
                    $call = Invoke-HarnessRoute $baseUrl $path $query
                    if ($null -eq $call.Response -or -not ($call.Response.PSObject.Properties.Name -contains 'ok')) { throw "Harness response omitted required ok=true contract: $($call.Uri)." }
                    if (-not [bool]$call.Response.ok) { throw "Harness refused $($call.Uri)." }
                    if ($call.Response.PSObject.Properties.Name -contains 'supported' -and -not [bool]$call.Response.supported) { throw "Harness reported unsupported state for $($call.Uri)." }
                }
            }
        } elseif ($Gate.kind -eq 'package') {
            $scriptPath = Join-Path $Root ([string]$Gate.script)
            if (-not (Test-Path -LiteralPath $scriptPath)) { throw "Package script not found: $scriptPath" }
            if ($Gate.scope -eq 'mods') {
                $selectedProjects = @($Gate.owners | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ';'
                if ([string]::IsNullOrWhiteSpace($selectedProjects)) { throw 'The mod package gate selected no project owners.' }
                $null = & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -ShelteredRoot $Root -OutputRoot (Join-Path $Root 'release\2.0\artifacts\mods') -Projects $selectedProjects
            }
            else { $null = & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -Configuration $Configuration }
            if ($LASTEXITCODE -ne 0) { throw "Package script exited $LASTEXITCODE." }
        } elseif ($Gate.kind -eq 'promotion') {
            if ([string]$Gate.scope -eq 'manager-stable-live') { throw 'Stable Manager promotion remains a live Nexus/account gate; this orchestrator never simulates or publishes it.' }
            Assert-ScopedPackageProvenance $Gate $Root
            $result.detail = 'Local promotion preflight selected; publication remains a separate explicit action.'
        }
        $result.status = 'passed'
        $result.evidencePath = Save-GateEvidence $Gate $fingerprint $result
    } catch {
        $result.status = 'failed'; $result.detail = $_.Exception.Message
    }
    return [pscustomobject]$result
}

$root = [IO.Path]::GetFullPath($ShelteredRoot)
$graph = Get-Content -LiteralPath $GraphPath -Raw | ConvertFrom-Json
$inputFiles = @(Read-ChangedFileInput $ChangedFile $ChangedFilesPath)
$explicitInputCount = $inputFiles.Count
if ($DetectGit) {
    foreach ($repo in @($graph.repositories)) {
        $repoRoot = Join-Path $root ([string]$repo.path)
        $inputFiles += @(Get-GitChangedFiles $repoRoot ([string]$repo.path) $BaseRef)
    }
    $managerRoot = Join-Path $root ([string]$graph.manager.path)
    $inputFiles += @(Get-GitChangedFiles $managerRoot ([string]$graph.manager.path) $BaseRef)
}
$inputFiles = @($inputFiles | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { Normalize-PathText $_ } | Select-Object -Unique)
if ($inputFiles.Count -eq 0 -and -not $DetectGit -and $explicitInputCount -eq 0) { throw 'No changed files supplied. Use -ChangedFile, -ChangedFilesPath, or -DetectGit.' }

$plans = @{}
foreach ($repo in @($graph.repositories)) { $plans[[string]$repo.id] = New-RepoPlan ([string]$repo.id) 'mod' $repo }
$plans[[string]$graph.manager.id] = New-RepoPlan ([string]$graph.manager.id) 'manager' $graph.manager
$unmapped = New-Object System.Collections.Generic.List[string]
$releaseGraphChanged = $false
$releasePackagesChanged = $false

foreach ($file in $inputFiles) {
    $owner = Find-Owner $file $graph
    $fullForRule = $file
    if ($null -eq $owner) {
        $effects = Get-Effects $fullForRule $file $graph
        if ($effects.Effects -contains 'release-graph') { $releaseGraphChanged = $true }
        if ($effects.Effects -contains 'release-packages') { $releasePackagesChanged = $true }
        if ($effects.Effects -notcontains 'documentation-only' -and $effects.Effects -notcontains 'release-graph' -and $effects.Effects -notcontains 'release-packages') { [void]$unmapped.Add($file) }
        continue
    }
    $plan = $plans[$owner.Id]
    [void]$plan.changedFiles.Add($owner.Relative)
    $effectInfo = Get-Effects $file $owner.Relative $graph
    foreach ($rule in @($effectInfo.Rules)) { Add-Unique $plan.rules $rule }
    foreach ($effect in @($effectInfo.Effects)) { Add-Unique $plan.changeClasses $effect }
    if ($effectInfo.Effects -contains 'release-graph') { $releaseGraphChanged = $true; continue }
    if ($effectInfo.Effects -contains 'documentation-only') { continue }
    if ($effectInfo.Effects -contains 'contracts-only') {
        $plan.contract = $true
        Add-Unique $plan.reasons 'test-only change: contracts only'
        continue
    }
    $plan.build = $true; $plan.contract = $true; $plan.platformSmoke = $true; $plan.package = [bool]$plan.definition.package; $plan.promotion = $true
    Add-ScenariosForChange $plan $owner.Relative "changed $($owner.Id) source/data"
}

if ($unmapped.Count -gt 0) {
    throw "Changed files are not mapped to a release owner or release-layer rule: $($unmapped -join ', ')"
}

foreach ($edge in @($graph.dependencyEdges)) {
    $provider = $plans[[string]$edge.provider]
    if ($provider -and ($provider.build -or $provider.package)) {
        $consumer = $plans[[string]$edge.consumer]
        Add-ScenarioToPlan $consumer ([string]$edge.scenario) ([string]$edge.reason)
        $consumer.platformSmoke = $true
    }
}

$gates = New-Object System.Collections.Generic.List[object]
$modPackageOwners = New-Object System.Collections.Generic.List[string]
foreach ($plan in $plans.Values) {
    if ($plan.build) {
        $project = if ($plan.kind -eq 'manager') { [string]$plan.definition.solution } else { [string]$plan.definition.project }
        [void]$gates.Add([pscustomobject]@{ id = "build.$($plan.id)"; phase = 'build'; kind = 'build'; owner = $plan.id; project = $project; heavy = $false; detail = "Rebuild changed $($plan.id) only." })
    }
    if ($plan.contract) {
        $scripts = @($plan.definition.contracts)
        $contractProjects = if ($plan.definition.PSObject.Properties['contractProjects']) { @($plan.definition.contractProjects) } else { @() }
        if ($plan.contract) { [void]$gates.Add([pscustomobject]@{ id = "contracts.$($plan.id)"; phase = 'contracts'; kind = 'contract'; owner = $plan.id; scripts = $scripts; projects = $contractProjects; heavy = $false; detail = "Run contracts owned by $($plan.id) only." }) }
    }
    foreach ($scenarioId in @($plan.gameplay)) {
        $scenario = Get-Scenario $graph $scenarioId
        foreach ($platform in @($scenario.platforms)) {
            [void]$gates.Add([pscustomobject]@{ id = "gameplay.$platform.$scenarioId"; phase = 'gameplay'; kind = 'gameplay'; owner = $plan.id; scenario = $scenarioId; platform = $platform; heavy = [bool]$scenario.heavy; detail = "Run harness fixture $scenarioId on $platform; no manual click sequence." })
        }
    }
    if ($plan.platformSmoke) {
        foreach ($platform in @($graph.defaults.platforms)) {
            [void]$gates.Add([pscustomobject]@{ id = "platform.$platform.$($plan.id)"; phase = 'platform-smoke'; kind = 'platform-smoke'; owner = $plan.id; scenario = 'manager-platform-smoke'; platform = $platform; heavy = $false; detail = "Check live health/API/load state on $platform after the targeted change." })
        }
    }
    if ($plan.package -and $plan.kind -eq 'mod') {
        Add-Unique $modPackageOwners ([string]$plan.id)
        [void]$gates.Add([pscustomobject]@{ id = "promotion.$($plan.id)"; phase = 'promotion'; kind = 'promotion'; owner = $plan.id; scope = 'mod-rc'; heavy = $false; detail = "Verify $($plan.id) commit/package/release provenance; do not publish." })
    }
    if ($plan.kind -eq 'manager' -and $plan.package) {
        [void]$gates.Add([pscustomobject]@{ id = 'package.shelteredmodmanager'; phase = 'packaging'; kind = 'package'; owner = $plan.id; scope = 'manager'; script = 'shelteredmodmanager/tools/New-ReleasePackages.ps1'; heavy = $false; detail = 'Rebuild Manager Release archives only after selected Manager gates pass.' })
        [void]$gates.Add([pscustomobject]@{ id = 'promotion.manager-rc'; phase = 'promotion'; kind = 'promotion'; owner = $plan.id; scope = 'manager-rc'; heavy = $false; detail = 'Verify RC artifacts and release provenance; no GitHub publication.' })
        if ($Stable) { [void]$gates.Add([pscustomobject]@{ id = 'promotion.manager-stable-live'; phase = 'promotion'; kind = 'promotion'; owner = $plan.id; scope = 'manager-stable-live'; heavy = $false; detail = 'Requires issued Nexus client ID/slug and real-account OAuth/download/rate-limit evidence; publication is intentionally not automated.' }) }
    }
}
if ($modPackageOwners.Count -gt 0) {
    [void]$gates.Add([pscustomobject]@{ id = 'package.mods'; phase = 'packaging'; kind = 'package'; owner = 'content-packages'; scope = 'mods'; owners = $modPackageOwners.ToArray(); script = 'release/2.0/tools/New-ModPackages.ps1'; heavy = $false; detail = "Run the shared mod packaging script once for: $($modPackageOwners -join ', '). Scoped promotion verification remains per changed mod." })
}
if ($releasePackagesChanged) {
    $allModOwners = @($graph.repositories | Where-Object { [bool]$_.package } | ForEach-Object { [string]$_.id })
    [void]$gates.Add([pscustomobject]@{ id = 'package.mods'; phase = 'packaging'; kind = 'package'; owner = 'content-packages'; scope = 'mods'; owners = $allModOwners; script = 'release/2.0/tools/New-ModPackages.ps1'; heavy = $false; detail = 'Release package metadata changed; regenerate the canonical package set before provenance checks.' })
    foreach ($ownerId in $allModOwners) {
        [void]$gates.Add([pscustomobject]@{ id = "promotion.$ownerId"; phase = 'promotion'; kind = 'promotion'; owner = $ownerId; scope = 'mod-rc'; heavy = $false; detail = "Revalidate $ownerId package provenance after release metadata changed; do not publish." })
    }
}
if ($releaseGraphChanged) { [void]$gates.Add([pscustomobject]@{ id = 'contracts.release-graph'; phase = 'contracts'; kind = 'contract'; owner = 'release-layer'; scripts = @('shelteredmodmanager/tools/release-orchestration/Test-IncrementalReleaseOrchestrator.ps1'); projects = @(); heavy = $false; detail = 'Validate graph JSON and selector self-tests.' }) }
$phaseOrder = @{ build = 1; contracts = 2; gameplay = 3; 'platform-smoke' = 4; packaging = 5; promotion = 6 }
$orderedGates = @($gates | Sort-Object @{Expression = { $phaseOrder[[string]$_.phase] }}, id | Group-Object id | ForEach-Object { $_.Group[0] })
$execution = New-Object System.Collections.Generic.List[object]
$executionBlocked = $false
foreach ($gate in $orderedGates) {
    if ($Execute -and $executionBlocked -and ([string]$gate.phase -eq 'packaging' -or [string]$gate.phase -eq 'promotion')) {
        [void]$execution.Add([pscustomobject]@{ id = $gate.id; phase = $gate.phase; kind = $gate.kind; status = 'dependency-blocked'; detail = 'A required selected build, contract, gameplay, platform, or packaging gate did not pass; downstream packaging/promotion was not run.'; fingerprint = $null; evidencePath = $null })
        continue
    }
    $gateResult = Invoke-Gate $gate $graph $root
    [void]$execution.Add($gateResult)
    if ($Execute -and @('passed', 'reused-evidence') -notcontains [string]$gateResult.status) { $executionBlocked = $true }
}

$repoReport = New-Object System.Collections.Generic.List[object]
$selectedOwnerList = New-Object System.Collections.Generic.List[string]
$reused = New-Object System.Collections.Generic.List[object]
foreach ($repoPlan in @($plans.Values)) {
    [void]$repoReport.Add([pscustomobject]@{ id = $repoPlan.id; kind = $repoPlan.kind; changedFiles = @($repoPlan.changedFiles); changeClasses = @($repoPlan.changeClasses); matchedRules = @($repoPlan.rules); build = $repoPlan.build; contracts = $repoPlan.contract; gameplayScenarios = @($repoPlan.gameplay); platformSmoke = $repoPlan.platformSmoke; package = $repoPlan.package; promotion = $repoPlan.promotion; reasons = @($repoPlan.reasons) })
    $hasImpact = $repoPlan.build -or $repoPlan.contract -or $repoPlan.platformSmoke -or $repoPlan.package -or $repoPlan.promotion -or $repoPlan.gameplay.Count -gt 0
    if ($hasImpact) { [void]$selectedOwnerList.Add([string]$repoPlan.id) }
    if (-not $hasImpact) { [void]$reused.Add([pscustomobject]@{ owner = $repoPlan.id; status = 'not-selected'; reason = 'No dependency or file impact selected. No evidence claim is made until a concrete gate fingerprint is evaluated.' }) }
}
$gateReport = @($orderedGates | ForEach-Object {
    $gatePlatform = if ($_.PSObject.Properties['platform']) { $_.platform } else { $null }
    $gateScenario = if ($_.PSObject.Properties['scenario']) { $_.scenario } else { $null }
    $gateOwners = if ($_.PSObject.Properties['owners']) { @($_.owners) } else { @() }
    [pscustomobject]@{ id = $_.id; phase = $_.phase; kind = $_.kind; owner = $_.owner; owners = $gateOwners; platform = $gatePlatform; scenario = $gateScenario; heavy = $_.heavy; detail = $_.detail }
})
$report = [ordered]@{
    schemaVersion = 1; generatedUtc = [DateTime]::UtcNow.ToString('o'); mode = if ($Execute) { 'execute' } else { 'dry-run' }; configuration = $Configuration
    baseRef = $BaseRef; changedFiles = $inputFiles; selectedOwners = $selectedOwnerList.ToArray(); unmappedFiles = @($unmapped); releaseGraphChanged = $releaseGraphChanged; releasePackagesChanged = $releasePackagesChanged
    repoPlans = $repoReport.ToArray()
    gates = $gateReport
    reusedScopes = $reused.ToArray()
    execution = $execution.ToArray()
    succeeded = -not $executionBlocked
    policy = [ordered]@{ unrelatedStateHeavyMatrices = 'not selected'; manualClicks = 'not used; harness routes are completion-backed'; publication = 'never performed by this tool'; stableOAuth = 'external live gate' }
}
$json = $report | ConvertTo-Json -Depth 12
if ($OutputPath) { Set-Content -LiteralPath $OutputPath -Value $json -Encoding UTF8 }
Write-Output $json
if ($Execute -and $executionBlocked) { exit 1 }
