[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$BaselinePath,
    [switch]$ListCurrent
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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

function ConvertTo-RepoRelativePath {
    param([string]$Path)

    $fullPath = (Resolve-Path -LiteralPath $Path).Path
    $root = $RepoRoot.TrimEnd([char]'\', [char]'/')
    if (-not $fullPath.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path '$fullPath' is not under repo root '$root'."
    }

    return $fullPath.Substring($root.Length).TrimStart([char]'\', [char]'/') -replace "\\", "/"
}

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
    return "{0}`t{1}`t{2}" -f $Entry.Kind, $Entry.Namespace, $Entry.Name
}

function ConvertTo-TsvLine {
    param($Entry)
    return "{0}`t{1}`t{2}`t{3}" -f $Entry.Kind, $Entry.Namespace, $Entry.Name, $Entry.Path
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
        $relativePath = ConvertTo-RepoRelativePath $file.FullName

        $matches = [System.Text.RegularExpressions.Regex]::Matches($text, $DeclarationPattern)
        foreach ($match in $matches) {
            $entries.Add((New-SurfaceEntry -Kind $match.Groups[1].Value -Namespace $namespace -Name $match.Groups[2].Value -Path $relativePath))
        }
    }

    return $entries | Sort-Object Kind, Namespace, Name
}

function Read-Baseline {
    if (-not (Test-Path -LiteralPath $BaselinePath)) {
        throw "Missing baseline file: $BaselinePath. Run with -ListCurrent to print current candidates."
    }

    $baseline = @{}
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $BaselinePath) {
        $lineNumber += 1
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith("#")) {
            continue
        }

        $parts = $line -split "`t"
        if ($parts.Length -ne 5) {
            throw "Invalid baseline line $lineNumber in '$BaselinePath'. Expected 5 tab-separated fields."
        }

        if ([string]::IsNullOrWhiteSpace($parts[4])) {
            throw "Invalid baseline line $lineNumber in '$BaselinePath'. Public API entries require a justification."
        }

        $entry = New-SurfaceEntry -Kind $parts[0] -Namespace $parts[1] -Name $parts[2] -Path $parts[3]
        $key = New-Key $entry
        if ($baseline.ContainsKey($key)) {
            throw "Duplicate baseline entry on line ${lineNumber}: $key"
        }

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
    Write-Host "Make accidental API additions internal. If the public API is intentional, add it to the baseline with a justification and update the relevant documentation."
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
