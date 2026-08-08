[CmdletBinding()]
param(
    [string]$RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
} else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

$allowed = @(
    "ModAPI/Core/RuntimeCompat.cs"
)

$roots = @(
    (Join-Path $RepoRoot "ModAPI"),
    (Join-Path $RepoRoot "ShelteredAPI"),
    (Join-Path $RepoRoot "ShelteredScenarioEditor")
)

$failures = New-Object "System.Collections.Generic.List[string]"

foreach ($root in $roots) {
    if (-not (Test-Path -LiteralPath $root)) {
        continue
    }

    Get-ChildItem -LiteralPath $root -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
        ForEach-Object {
            $relative = $_.FullName.Substring($RepoRoot.TrimEnd([char]'\', [char]'/').Length).TrimStart([char]'\', [char]'/') -replace "\\", "/"
            if (-not ($allowed -contains $relative)) {
                $text = Get-Content -LiteralPath $_.FullName -Raw
                if ([System.Text.RegularExpressions.Regex]::IsMatch($text, "\bRect\.zero\b")) {
                    $failures.Add($relative)
                }
            }
        }
}

if ($failures.Count -gt 0) {
    Write-Host ("RuntimeCompat Rect verifier failed. Direct Rect.zero usage found: " + $failures.Count)
    foreach ($failure in $failures) {
        Write-Host ("FAIL`t" + $failure)
    }
    exit 1
}

Write-Host "RuntimeCompat Rect verifier passed."
