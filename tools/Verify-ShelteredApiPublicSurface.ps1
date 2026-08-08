[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$BaselinePath,
    [switch]$ListCurrent
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "VerificationSupport.psm1") -Force

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
} else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

if ([string]::IsNullOrWhiteSpace($BaselinePath)) {
    $BaselinePath = Join-Path $RepoRoot "documentation\ShelteredAPI_PublicSurface_Baseline.tsv"
}

$ApiRoot = Join-Path $RepoRoot "ShelteredAPI"
$DeclarationPattern = "(?m)^\s*public\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*(class|interface|enum|struct)\s+([A-Za-z_][A-Za-z0-9_]*)"
$NamespacePattern = "(?m)^\s*namespace\s+([A-Za-z0-9_.]+)"

function New-SurfaceEntry {
    param(
        [string]$Kind,
        [string]$Namespace,
        [string]$Name,
        [string]$Path
    )

    [pscustomobject]@{
        Kind = $Kind
        Namespace = $Namespace
        Name = $Name
        Path = $Path
    }
}

function New-Key {
    param($Entry)
    return ConvertTo-VerificationTsvLine -Values @($Entry.Kind, $Entry.Namespace, $Entry.Name)
}

function ConvertTo-TsvLine {
    param($Entry)
    return ConvertTo-VerificationTsvLine -Values @($Entry.Kind, $Entry.Namespace, $Entry.Name, $Entry.Path)
}

function Get-CurrentSurface {
    $entries = New-Object "System.Collections.Generic.List[object]"
    $files = Get-ChildItem -LiteralPath $ApiRoot -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
        Sort-Object FullName

    foreach ($file in $files) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $namespaceMatch = [System.Text.RegularExpressions.Regex]::Match($text, $NamespacePattern)
        $namespace = if ($namespaceMatch.Success) { $namespaceMatch.Groups[1].Value } else { "" }
        $relativePath = ConvertTo-RepositoryRelativePath -Path $file.FullName -RepositoryRoot $RepoRoot

        $matches = [System.Text.RegularExpressions.Regex]::Matches($text, $DeclarationPattern)
        foreach ($match in $matches) {
            $entries.Add((New-SurfaceEntry -Kind $match.Groups[1].Value -Namespace $namespace -Name $match.Groups[2].Value -Path $relativePath))
        }
    }

    return $entries | Sort-Object Kind, Namespace, Name
}

function Read-Baseline {
    $baseline = @{}
    $rows = Import-VerificationTsvBaseline -Path $BaselinePath -DataColumnCount 4 -KeyColumnIndexes @(0, 1, 2) `
        -JustificationRequirement 'Public API entries require a justification.' `
        -MissingFileGuidance 'Run with -ListCurrent to print current candidates.'
    foreach ($key in $rows.Keys) {
        $parts = @($rows[$key].Fields)
        $entry = New-SurfaceEntry -Kind $parts[0] -Namespace $parts[1] -Name $parts[2] -Path $parts[3]
        $baseline[$key] = $entry
    }
    return $baseline
}

$current = @(Get-CurrentSurface)

if ($ListCurrent) {
    "# Kind`tNamespace`tName`tPath`tJustification"
    foreach ($entry in $current) {
        (ConvertTo-TsvLine $entry) + "`t<required justification>"
    }
    exit 0
}

$baseline = Read-Baseline
$currentByKey = @{}
$newEntries = New-Object "System.Collections.Generic.List[object]"
$staleEntries = New-Object "System.Collections.Generic.List[object]"

foreach ($entry in $current) {
    $key = New-Key $entry
    $currentByKey[$key] = $entry
    if (-not $baseline.ContainsKey($key)) {
        $newEntries.Add($entry)
    }
}

foreach ($key in $baseline.Keys) {
    if (-not $currentByKey.ContainsKey($key)) {
        $staleEntries.Add($baseline[$key])
    }
}

if ($newEntries.Count -gt 0) {
    Write-Host ("ShelteredAPI public-surface verifier failed. New public entries: " + $newEntries.Count)
    foreach ($entry in $newEntries) {
        Write-Host ("NEW`t" + (ConvertTo-TsvLine $entry))
    }
    exit 1
}

Write-Host "ShelteredAPI public-surface verifier passed."
Write-Host ("Current public entries within baseline: " + $current.Count)
Write-Host ("Baseline entries: " + $baseline.Count)

if ($staleEntries.Count -gt 0) {
    Write-Host ("Baseline entries that can be removed: " + $staleEntries.Count)
    foreach ($entry in ($staleEntries | Sort-Object Kind, Namespace, Name | Select-Object -First 20)) {
        Write-Host ("STALE`t" + (ConvertTo-TsvLine $entry))
    }
}
