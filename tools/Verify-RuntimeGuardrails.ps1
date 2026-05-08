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

$GuardrailAllowToken = "GuardrailAllow: SilentCatch"
$ScannedRoots = @(
    "ModAPI\Core",
    "ModAPI.Networking",
    "ShelteredAPI\Networking"
)

$UnityProbePattern = [regex]"\bApplication\.(dataPath|platform|unityVersion)\b"
$EmptyCatchPattern = [regex]::new("catch\s*(?:\([^)]*\))?\s*\{\s*\}", [System.Text.RegularExpressions.RegexOptions]::Singleline)

function ConvertTo-RepoRelativePath {
    param([string]$Path)

    $fullPath = (Resolve-Path -LiteralPath $Path).Path
    $root = $RepoRoot.TrimEnd([char]'\', [char]'/')
    if (-not $fullPath.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path '$fullPath' is not under repo root '$root'."
    }

    return $fullPath.Substring($root.Length).TrimStart([char]'\', [char]'/') -replace "\\", "/"
}

function Get-SourceFiles {
    foreach ($root in $ScannedRoots) {
        $fullRoot = Join-Path $RepoRoot $root
        if (-not (Test-Path -LiteralPath $fullRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $fullRoot -Recurse -File -Filter "*.cs" |
            Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" }
    }
}

function Get-LineNumber {
    param(
        [string]$Text,
        [int]$Index
    )

    if ($Index -le 0) {
        return 1
    }

    return ([regex]::Matches($Text.Substring(0, $Index), "`n")).Count + 1
}

function Has-SilentCatchAllowComment {
    param(
        [string[]]$Lines,
        [int]$LineNumber
    )

    $start = [Math]::Max(1, $LineNumber - 4)
    $end = [Math]::Min($Lines.Length, $LineNumber + 4)
    for ($i = $start; $i -le $end; $i++) {
        if ($Lines[$i - 1].IndexOf($GuardrailAllowToken, [System.StringComparison]::Ordinal) -ge 0) {
            return $true
        }
    }

    return $false
}

function New-Finding {
    param(
        [string]$Rule,
        [string]$Path,
        [int]$Line,
        [string]$Detail
    )

    [pscustomobject]@{
        Rule = $Rule
        Path = $Path
        Line = $Line
        Detail = $Detail
    }
}

$findings = New-Object "System.Collections.Generic.List[object]"

foreach ($file in (Get-SourceFiles | Sort-Object FullName)) {
    $relativePath = ConvertTo-RepoRelativePath $file.FullName
    $text = Get-Content -LiteralPath $file.FullName -Raw
    $lines = Get-Content -LiteralPath $file.FullName

    foreach ($match in $EmptyCatchPattern.Matches($text)) {
        $lineNumber = Get-LineNumber -Text $text -Index $match.Index
        if (-not (Has-SilentCatchAllowComment -Lines $lines -LineNumber $lineNumber)) {
            $findings.Add((New-Finding -Rule "silent-catch" -Path $relativePath -Line $lineNumber -Detail "empty catch must be justified with '$GuardrailAllowToken'"))
        }
    }

    if ($relativePath -ne "ModAPI/Core/RuntimeCompat.cs" -and $relativePath -ne "ModAPI/Core/RuntimeEnvironmentInfo.cs") {
        foreach ($match in $UnityProbePattern.Matches($text)) {
            $lineNumber = Get-LineNumber -Text $text -Index $match.Index
            $findings.Add((New-Finding -Rule "direct-unity-probe" -Path $relativePath -Line $lineNumber -Detail $match.Value))
        }
    }
}

if ($findings.Count -gt 0) {
    Write-Host ("Runtime guardrail verifier failed. Findings: " + $findings.Count)
    foreach ($finding in ($findings | Sort-Object Rule, Path, Line)) {
        Write-Host ("{0}`t{1}:{2}`t{3}" -f $finding.Rule, $finding.Path, $finding.Line, $finding.Detail)
    }

    Write-Host ""
    Write-Host "Guardrail guidance:"
    Write-Host " - Use RuntimeCompat for Application.dataPath, Application.platform, and Application.unityVersion probes in core/networking/startup paths."
    Write-Host " - Avoid catch { } in ModAPI/Core, ModAPI.Networking, and ShelteredAPI/Networking."
    Write-Host " - If swallowing is explicitly best-effort compatibility or cleanup behavior, add a nearby comment containing '$GuardrailAllowToken' and explain why."
    Write-Host " - Prefer logging once when a swallowed failure has an actionable owner or runtime impact."
    exit 1
}

Write-Host "Runtime guardrail verifier passed."
Write-Host "Scanned ModAPI/Core, ModAPI.Networking, and ShelteredAPI/Networking for unsafe silent catches and direct Unity runtime probes."
