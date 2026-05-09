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
$DateTimePattern = [regex]"\bDateTime\.(?:UtcNow|Now)\b"
$UnityDeltaTimePattern = [regex]"\bTime\.deltaTime\b"
$BroadcastLocalSamplePattern = [regex]"\bBroadcastLocalSample\s*\("
$FastSlowSharedTickPattern = [regex]"\b(WorldTick|SetWorldTick|AdvanceFixedDelta|ShelteredMultiplayerWorldClock)\b"

$ShelteredDateTimeAllowCounts = @{
    # Connection panel status timestamps are display-only.
    "ShelteredAPI/Networking/MultiplayerConnectionTestService.cs" = 1
    # Diagnostics age formatting is display-only.
    "ShelteredAPI/Networking/MultiplayerDiagnosticsFormatter.cs" = 1
    # Multiplayer timeline timestamps are diagnostic metadata only.
    "ShelteredAPI/Networking/Diagnostics/ShelteredMultiplayerTimeline.cs" = 1
    # Persistence timestamps describe snapshot metadata, not simulation time.
    "ShelteredAPI/Networking/Persistence/ShelteredMultiplayerWorldSnapshot.cs" = 1
    # Save sync timestamps identify files/messages and must not drive shared world ticks.
    "ShelteredAPI/Networking/ShelteredMultiplayerSaveSyncService.cs" = 3
    # Temporary Dev-1.4 bridge: clock samples carry diagnostic UTC metadata only, never tick authority.
    "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs" = 4
    # Temporary Dev-1.4 bridge: missing sample timestamps are normalized for diagnostics only.
    "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClockContracts.cs" = 1
    # Event CreatedUtc is journal metadata; WorldTick comes from the coordinator.
    "ShelteredAPI/Networking/World/ShelteredWorldEventJournal.cs" = 1
}

$UnityDeltaTimeAllowCounts = @{
    # Temporary Dev-1.4 bridge until the signed-off fixed-step scheduler replaces Unity frame delta.
    "ShelteredAPI/Networking/ShelteredMultiplayerRuntimeDriver.cs" = 1
}

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

function Add-CountedFinding {
    param(
        [hashtable]$Counts,
        [hashtable]$Allowed,
        [string]$Rule,
        [string]$Path,
        [int]$Line,
        [string]$Detail
    )

    if (-not $Counts.ContainsKey($Path)) {
        $Counts[$Path] = 0
    }

    $Counts[$Path] = [int]$Counts[$Path] + 1
    if (-not $Allowed.ContainsKey($Path) -or [int]$Counts[$Path] -gt [int]$Allowed[$Path]) {
        $findings.Add((New-Finding -Rule $Rule -Path $Path -Line $Line -Detail $Detail))
    }
}

function Test-NearbyLinesContain {
    param(
        [string[]]$Lines,
        [int]$LineNumber,
        [string]$Value
    )

    $start = [Math]::Max(1, $LineNumber - 4)
    $end = [Math]::Min($Lines.Length, $LineNumber + 4)
    for ($i = $start; $i -le $end; $i++) {
        if ($Lines[$i - 1].IndexOf($Value, [System.StringComparison]::Ordinal) -ge 0) {
            return $true
        }
    }

    return $false
}

$findings = New-Object "System.Collections.Generic.List[object]"
$dateTimeCounts = @{}
$deltaTimeCounts = @{}

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

    if ($relativePath.StartsWith("ShelteredAPI/Networking/", [System.StringComparison]::OrdinalIgnoreCase)) {
        foreach ($match in $DateTimePattern.Matches($text)) {
            $lineNumber = Get-LineNumber -Text $text -Index $match.Index
            Add-CountedFinding -Counts $dateTimeCounts -Allowed $ShelteredDateTimeAllowCounts -Rule "networking-datetime-authority" -Path $relativePath -Line $lineNumber -Detail "DateTime is diagnostics/persistence metadata only"
        }

        foreach ($match in $UnityDeltaTimePattern.Matches($text)) {
            $lineNumber = Get-LineNumber -Text $text -Index $match.Index
            Add-CountedFinding -Counts $deltaTimeCounts -Allowed $UnityDeltaTimeAllowCounts -Rule "shared-world-delta-time" -Path $relativePath -Line $lineNumber -Detail "Time.deltaTime is only allowed at the temporary runtime bridge"
        }

        foreach ($match in $BroadcastLocalSamplePattern.Matches($text)) {
            $lineNumber = Get-LineNumber -Text $text -Index $match.Index
            $lineText = $lines[$lineNumber - 1]
            $isHelperDeclaration = $relativePath -eq "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs" -and $lineText.IndexOf("bool BroadcastLocalSample", [System.StringComparison]::Ordinal) -ge 0
            if (-not $isHelperDeclaration) {
                $findings.Add((New-Finding -Rule "world-clock-sample-loop" -Path $relativePath -Line $lineNumber -Detail "BroadcastLocalSample must not become a periodic clock loop"))
            }
        }

        for ($i = 0; $i -lt $lines.Length; $i++) {
            if ($relativePath -ne "ShelteredAPI/Networking/World/ShelteredMultiplayerWorldClock.cs" `
                -and $lines[$i].IndexOf("BroadcastAuthoritative", [System.StringComparison]::Ordinal) -ge 0 `
                -and ((Test-NearbyLinesContain -Lines $lines -LineNumber ($i + 1) -Value "ShelteredWorldClockSampleCodec") `
                    -or (Test-NearbyLinesContain -Lines $lines -LineNumber ($i + 1) -Value "WorldClockSample"))) {
                $findings.Add((New-Finding -Rule "world-clock-sample-loop" -Path $relativePath -Line ($i + 1) -Detail "World.ClockSample must stay a rare correction event"))
            }

            $isFastSlowFile = $relativePath -eq "ShelteredAPI/Networking/ShelteredMultiplayerTimePolicy.cs" `
                -or $relativePath -eq "ShelteredAPI/Networking/ShelteredMultiplayerTimePatches.cs"
            if ($isFastSlowFile -and $FastSlowSharedTickPattern.IsMatch($lines[$i])) {
                if ($lines[$i].IndexOf("ApplyMultiplayerGameTimeProjection", [System.StringComparison]::Ordinal) -lt 0 `
                    -and $lines[$i].IndexOf("ShelteredWorldTimeProjection", [System.StringComparison]::Ordinal) -lt 0 `
                    -and $lines[$i].IndexOf("context.WorldTick", [System.StringComparison]::Ordinal) -lt 0) {
                    $findings.Add((New-Finding -Rule "fast-slow-worldtick" -Path $relativePath -Line ($i + 1) -Detail "fast/slow policy must not affect shared WorldTick"))
                }
            }
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
    Write-Host " - Keep multiplayer world time on coordinator WorldTick/fixed-step inputs; DateTime and host samples are metadata/corrections only."
    Write-Host " - Do not add Time.deltaTime reads to shared-world systems outside the temporary runtime bridge."
    exit 1
}

Write-Host "Runtime guardrail verifier passed."
Write-Host "Scanned ModAPI/Core, ModAPI.Networking, and ShelteredAPI/Networking for unsafe catches, Unity probes, and deterministic world-time guardrails."
