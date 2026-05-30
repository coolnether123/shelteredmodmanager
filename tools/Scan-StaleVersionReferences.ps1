[CmdletBinding()]
param(
    [string]$RepoRoot,
    [switch]$FailOnChange
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
} else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

$patterns = @(
    "1\.3",
    "1\.3\.0",
    "1\.3\.0-beta\.3",
    "Beta\.3",
    "v1\.3",
    "SMM 1\.3"
)

$excludeDirectoryPattern = "\\(\.git|\.vs|bin|obj|packages|artifacts|Release|Decompiled)\\"
$excludeFilePattern = "\.(dll|exe|pdb|zip|png|jpg|jpeg|gif|ico|pdf)$"
$regex = [regex]::new(($patterns -join "|"), [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

function ConvertTo-RepoRelativePath {
    param([string]$Path)

    $fullPath = (Resolve-Path -LiteralPath $Path).Path
    $root = $RepoRoot.TrimEnd([char]'\', [char]'/')
    return $fullPath.Substring($root.Length).TrimStart([char]'\', [char]'/') -replace "\\", "/"
}

function Get-Classification {
    param([string]$RelativePath, [string]$Line)

    if ($RelativePath -eq "full-diff.patch") {
        return "keep: historical diff artifact"
    }

    if ($RelativePath -eq "shelteredapi-architecture.html") {
        return "keep: generated architecture artifact"
    }

    if ($RelativePath -eq "tools/Scan-StaleVersionReferences.ps1") {
        return "keep: scanner pattern text"
    }

    if ($RelativePath -eq "Manager/MainForm.cs" -and $Line -match "old 1\.3 beta line") {
        return "keep: intentional first-run migration warning"
    }

    if ($RelativePath -like "documentation/Release_2.0.md" -and $Line -match "supersedes|previous") {
        return "keep: explicitly references superseded release"
    }

    if ($RelativePath -in @(
        "documentation/SMM_2.0_Migration.md",
        "documentation/Known_Issues.md",
        "documentation/For_Modders_2.0_API_Migration.md",
        "documentation/Player_Announcement_2.0.md"
    )) {
        return "keep: intentional 2.0 migration warning"
    }

    if ($RelativePath -like "ShelteredAPI/Scenarios/Diagnostics/*") {
        return "keep: test fixture/example dependency version"
    }

    return "change: release-facing stale version reference"
}

$findings = New-Object "System.Collections.Generic.List[object]"

Get-ChildItem -LiteralPath $RepoRoot -Recurse -File |
    Where-Object {
        $_.FullName -notmatch $excludeDirectoryPattern -and
        $_.FullName -notmatch $excludeFilePattern
    } |
    Sort-Object FullName |
    ForEach-Object {
        $path = $_.FullName
        $relative = ConvertTo-RepoRelativePath $path
        $lineNumber = 0

        foreach ($line in Get-Content -LiteralPath $path) {
            $lineNumber += 1
            if (-not $regex.IsMatch($line)) {
                continue
            }

            $findings.Add([pscustomobject]@{
                Path = $relative
                Line = $lineNumber
                Classification = Get-Classification -RelativePath $relative -Line $line
                Text = $line.Trim()
            })
        }
    }

if ($findings.Count -eq 0) {
    Write-Host "No stale 1.3/Beta.3 references found."
    exit 0
}

foreach ($finding in $findings) {
    Write-Host ("{0}:{1}`t{2}`t{3}" -f $finding.Path, $finding.Line, $finding.Classification, $finding.Text)
}

$changeCount = @($findings | Where-Object { $_.Classification -like "change:*" }).Count
Write-Host ("Stale version scan complete. Findings: {0}. Change candidates: {1}." -f $findings.Count, $changeCount)

if ($FailOnChange -and $changeCount -gt 0) {
    exit 1
}
