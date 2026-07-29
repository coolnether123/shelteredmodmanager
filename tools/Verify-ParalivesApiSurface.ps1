[CmdletBinding()]
param(
    [string]$RepoRoot,
    [switch]$ListCurrent,
    [switch]$FailOnRawGameTypes,
    [string[]]$AllowedRawNamespaces = @("ParalivesAPI.Native", "ParalivesAPI.Unsafe"),
    [string[]]$AllowedStableRawSignaturePatterns = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
} else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

$ApiRoot = Join-Path $RepoRoot "ParalivesAPI"
$DeclarationPattern = "(?m)^\s*public\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*(class|interface|enum|struct)\s+([A-Za-z_][A-Za-z0-9_]*)"
$NamespacePattern = "(?m)^\s*namespace\s+([A-Za-z0-9_.]+)"
$PublicStartPattern = "^\s*public\s+"
$RawGameTypes = @(
    "ActionUnit",
    "AddStatusEffectData",
    "AssetCharacter",
    "AssetCharacterOccupationData",
    "AssetCharacterOccupationUnlockableData",
    "AssetLot",
    "ContextRequirement",
    "InteractionGroup",
    "InteractionGroupItem",
    "InteractionUnit",
    "InteractionUsabilityRule",
    "ItemObjectRoot",
    "LifeStage",
    "MemoryData",
    "MemoryLogType",
    "Need",
    "Notification",
    "NotificationData",
    "OccupiedHours",
    "Occupation",
    "OccupationScheduleType",
    "OccupationUnlockable",
    "PersonalityTrait",
    "Player",
    "RelationshipLabel",
    "ScheduleDaysOfWeek",
    "SettingBase",
    "Skill",
    "SchoolJobTypes",
    "SocialGroup",
    "StatusEffect",
    "TogetherCard",
    "TogetherCardCategory",
    "TranslationItem",
    "Want"
)
$StableRawTypePatterns = @(
    "\bglobal::Asset[A-Za-z0-9_]*\b",
    "\bglobal::Setting\.[A-Za-z0-9_.]+\b",
    "\bSetting\.[A-Za-z0-9_.]+\b",
    "\bUI[A-Z][A-Za-z0-9_]*\b",
    "\bScheduleDaysOfWeek\b",
    "\bOccupiedHours\b",
    "\bSchoolJobTypes\b"
)

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

function New-RawFinding {
    param(
        [string]$Namespace,
        [string]$Path,
        [int]$Line,
        [string]$RawType,
        [string]$Signature
    )

    [pscustomobject]@{
        Namespace = $Namespace
        Path = $Path
        Line = $Line
        RawType = $RawType
        Signature = $Signature
    }
}

function New-HomeschoolFinding {
    param(
        [string]$Namespace,
        [string]$Path,
        [int]$Line,
        [string]$Signature
    )

    [pscustomobject]@{
        Namespace = $Namespace
        Path = $Path
        Line = $Line
        Signature = $Signature
    }
}

function New-StableRawFinding {
    param(
        [string]$Path,
        [int]$Line,
        [string]$RawType,
        [string]$Signature
    )

    [pscustomobject]@{
        Path = $Path
        Line = $Line
        RawType = $RawType
        Signature = $Signature
    }
}

function ConvertTo-SurfaceTsvLine {
    param($Entry)
    return "{0}`t{1}`t{2}`t{3}" -f $Entry.Kind, $Entry.Namespace, $Entry.Name, $Entry.Path
}

function ConvertTo-RawTsvLine {
    param($Finding)
    return "{0}`t{1}`t{2}`t{3}`t{4}" -f $Finding.Path, $Finding.Line, $Finding.Namespace, $Finding.RawType, $Finding.Signature
}

function ConvertTo-HomeschoolTsvLine {
    param($Finding)
    return "{0}`t{1}`t{2}`t{3}" -f $Finding.Path, $Finding.Line, $Finding.Namespace, $Finding.Signature
}

function ConvertTo-StableRawTsvLine {
    param($Finding)
    return "{0}`t{1}`t{2}`t{3}" -f $Finding.Path, $Finding.Line, $Finding.RawType, $Finding.Signature
}

function Get-SourceFiles {
    Get-ChildItem -LiteralPath $ApiRoot -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
        Sort-Object FullName
}

function Get-Namespace {
    param([string]$Text)

    $namespaceMatch = [System.Text.RegularExpressions.Regex]::Match($Text, $NamespacePattern)
    if ($namespaceMatch.Success) {
        return $namespaceMatch.Groups[1].Value
    }

    return ""
}

function Test-IsAllowedRawNamespace {
    param([string]$Namespace)

    foreach ($allowed in $AllowedRawNamespaces) {
        if ([string]::IsNullOrWhiteSpace($allowed)) {
            continue
        }

        if (($Namespace.Equals($allowed, [System.StringComparison]::Ordinal)) -or
            ($Namespace.StartsWith($allowed + ".", [System.StringComparison]::Ordinal))) {
            return $true
        }
    }

    return $false
}

function Test-IsBoundaryNamespace {
    param([string]$Namespace)

    $roots = @(
        "ParalivesAPI.Core",
        "ParalivesAPI.Stable",
        "ParalivesAPI.Native",
        "ParalivesAPI.Unsafe"
    )

    foreach ($root in $roots) {
        if (($Namespace.Equals($root, [System.StringComparison]::Ordinal)) -or
            ($Namespace.StartsWith($root + ".", [System.StringComparison]::Ordinal))) {
            return $true
        }
    }

    return $false
}

function Test-IsAllowedStableRawSignature {
    param([string]$Signature)

    foreach ($pattern in $AllowedStableRawSignaturePatterns) {
        if ([string]::IsNullOrWhiteSpace($pattern)) {
            continue
        }

        if ($Signature -match $pattern) {
            return $true
        }
    }

    return $false
}

function Get-BraceDelta {
    param([string]$Line)

    $opens = [System.Text.RegularExpressions.Regex]::Matches($Line, "\{").Count
    $closes = [System.Text.RegularExpressions.Regex]::Matches($Line, "\}").Count
    return $opens - $closes
}

function Get-CurrentSurface {
    $entries = New-Object "System.Collections.Generic.List[object]"

    foreach ($file in Get-SourceFiles) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $namespace = Get-Namespace -Text $text
        $relativePath = ConvertTo-RepoRelativePath $file.FullName

        $matches = [System.Text.RegularExpressions.Regex]::Matches($text, $DeclarationPattern)
        foreach ($match in $matches) {
            $entries.Add((New-SurfaceEntry -Kind $match.Groups[1].Value -Namespace $namespace -Name $match.Groups[2].Value -Path $relativePath))
        }
    }

    return $entries | Sort-Object Kind, Namespace, Name
}

function Get-PublicDeclarationSpans {
    param(
        [string[]]$Lines,
        [string]$Namespace,
        [string]$Path
    )

    $spans = New-Object "System.Collections.Generic.List[object]"
    for ($i = 0; $i -lt $Lines.Length; $i++) {
        $line = $Lines[$i]
        if ($line -notmatch $PublicStartPattern) {
            continue
        }

        $signatureLines = New-Object "System.Collections.Generic.List[string]"
        $signatureLines.Add($line.Trim())
        $endLine = $i

        while (($endLine + 1 -lt $Lines.Length) -and
            ($Lines[$endLine] -notmatch "\{") -and
            ($Lines[$endLine] -notmatch ";") -and
            ($Lines[$endLine] -notmatch "=>")) {
            $endLine += 1
            $signatureLines.Add($Lines[$endLine].Trim())

            if (($Lines[$endLine] -match "\{") -or
                ($Lines[$endLine] -match ";") -or
                ($Lines[$endLine] -match "=>")) {
                break
            }
        }

        $spans.Add([pscustomobject]@{
            Namespace = $Namespace
            Path = $Path
            Line = $i + 1
            Signature = (($signatureLines -join " ") -replace "\s+", " ").Trim()
        })
    }

    return $spans
}

function Get-PublicInterfaceMemberSpans {
    param(
        [string[]]$Lines,
        [string]$Namespace,
        [string]$Path
    )

    $spans = New-Object "System.Collections.Generic.List[object]"
    $insideInterface = $false
    $braceDepth = 0
    $signatureLines = New-Object "System.Collections.Generic.List[string]"

    for ($i = 0; $i -lt $Lines.Length; $i++) {
        $line = $Lines[$i]
        $trimmed = $line.Trim()

        if (-not $insideInterface) {
            if ($line -match "^\s*public\s+(?:partial\s+)?interface\s+[A-Za-z_][A-Za-z0-9_]*") {
                $insideInterface = $true
                $braceDepth = Get-BraceDelta -Line $line
            }

            continue
        }

        $braceDepth += Get-BraceDelta -Line $line

        if ([string]::IsNullOrWhiteSpace($trimmed) -or
            $trimmed.StartsWith("//") -or
            $trimmed.StartsWith("[") -or
            $trimmed -eq "{" -or
            $trimmed -eq "}") {
            if ($braceDepth -le 0) {
                $insideInterface = $false
                $signatureLines.Clear()
            }

            continue
        }

        $signatureLines.Add($trimmed)
        if (($trimmed -match ";") -or ($trimmed -match "=>") -or ($trimmed -match "\{")) {
            $spans.Add([pscustomobject]@{
                Namespace = $Namespace
                Path = $Path
                Line = $i + 1
                Signature = (($signatureLines -join " ") -replace "\s+", " ").Trim()
            })
            $signatureLines.Clear()
        }

        if ($braceDepth -le 0) {
            $insideInterface = $false
            $signatureLines.Clear()
        }
    }

    return $spans
}

function Get-PublicEnumMemberSpans {
    param(
        [string[]]$Lines,
        [string]$Namespace,
        [string]$Path
    )

    $spans = New-Object "System.Collections.Generic.List[object]"
    $insideEnum = $false
    $braceDepth = 0

    for ($i = 0; $i -lt $Lines.Length; $i++) {
        $line = $Lines[$i]
        $trimmed = $line.Trim()

        if (-not $insideEnum) {
            if ($line -match "^\s*public\s+(?:partial\s+)?enum\s+[A-Za-z_][A-Za-z0-9_]*") {
                $insideEnum = $true
                $braceDepth = Get-BraceDelta -Line $line
            }

            continue
        }

        $braceDepth += Get-BraceDelta -Line $line

        if ([string]::IsNullOrWhiteSpace($trimmed) -or
            $trimmed.StartsWith("//") -or
            $trimmed.StartsWith("[") -or
            $trimmed -eq "{" -or
            $trimmed -eq "}") {
            if ($braceDepth -le 0) {
                $insideEnum = $false
            }

            continue
        }

        $spans.Add([pscustomobject]@{
            Namespace = $Namespace
            Path = $Path
            Line = $i + 1
            Signature = ($trimmed -replace "\s+", " ").Trim()
        })

        if ($braceDepth -le 0) {
            $insideEnum = $false
        }
    }

    return $spans
}

function Get-RawGameTypeFindings {
    $findings = New-Object "System.Collections.Generic.List[object]"
    $rawPattern = "\b(" + (($RawGameTypes | ForEach-Object { [System.Text.RegularExpressions.Regex]::Escape($_) }) -join "|") + ")\b"

    foreach ($file in Get-SourceFiles) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $namespace = Get-Namespace -Text $text
        if (Test-IsAllowedRawNamespace -Namespace $namespace) {
            continue
        }

        $relativePath = ConvertTo-RepoRelativePath $file.FullName
        $lines = Get-Content -LiteralPath $file.FullName
        $spans = Get-PublicDeclarationSpans -Lines $lines -Namespace $namespace -Path $relativePath

        foreach ($span in $spans) {
            $matches = [System.Text.RegularExpressions.Regex]::Matches($span.Signature, $rawPattern)
            if ($matches.Count -eq 0) {
                continue
            }

            $seen = @{}
            foreach ($match in $matches) {
                $rawType = $match.Groups[1].Value
                if ($seen.ContainsKey($rawType)) {
                    continue
                }

                $seen[$rawType] = $true
                $findings.Add((New-RawFinding -Namespace $span.Namespace -Path $span.Path -Line $span.Line -RawType $rawType -Signature $span.Signature))
            }
        }
    }

    return $findings | Sort-Object Path, Line, RawType
}

function Get-PublicApiSpans {
    $spans = New-Object "System.Collections.Generic.List[object]"

    foreach ($file in Get-SourceFiles) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $namespace = Get-Namespace -Text $text
        if (-not (Test-IsBoundaryNamespace -Namespace $namespace)) {
            continue
        }

        $relativePath = ConvertTo-RepoRelativePath $file.FullName
        $lines = Get-Content -LiteralPath $file.FullName
        foreach ($span in Get-PublicDeclarationSpans -Lines $lines -Namespace $namespace -Path $relativePath) {
            $spans.Add($span)
        }

        foreach ($span in Get-PublicInterfaceMemberSpans -Lines $lines -Namespace $namespace -Path $relativePath) {
            $spans.Add($span)
        }

        foreach ($span in Get-PublicEnumMemberSpans -Lines $lines -Namespace $namespace -Path $relativePath) {
            $spans.Add($span)
        }
    }

    return $spans | Sort-Object Path, Line, Signature -Unique
}

function Get-HomeschoolNameFindings {
    $findings = New-Object "System.Collections.Generic.List[object]"

    foreach ($span in Get-PublicApiSpans) {
        if ($span.Signature -match "(?i)Homeschool") {
            $findings.Add((New-HomeschoolFinding `
                -Namespace $span.Namespace `
                -Path $span.Path `
                -Line $span.Line `
                -Signature $span.Signature))
        }
    }

    return $findings | Sort-Object Path, Line, Signature
}

function Get-StableInterfaceRawFindings {
    $findings = New-Object "System.Collections.Generic.List[object]"
    $rawPatterns = New-Object "System.Collections.Generic.List[string]"

    foreach ($rawType in $RawGameTypes) {
        $rawPatterns.Add("\b" + [System.Text.RegularExpressions.Regex]::Escape($rawType) + "\b")
    }

    foreach ($pattern in $StableRawTypePatterns) {
        $rawPatterns.Add($pattern)
    }

    foreach ($file in Get-SourceFiles) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $namespace = Get-Namespace -Text $text
        if (-not (($namespace.Equals("ParalivesAPI.Stable", [System.StringComparison]::Ordinal)) -or
            ($namespace.StartsWith("ParalivesAPI.Stable.", [System.StringComparison]::Ordinal)))) {
            continue
        }

        $relativePath = ConvertTo-RepoRelativePath $file.FullName
        $lines = Get-Content -LiteralPath $file.FullName
        $spans = Get-PublicInterfaceMemberSpans -Lines $lines -Namespace $namespace -Path $relativePath

        foreach ($span in $spans) {
            if (Test-IsAllowedStableRawSignature -Signature $span.Signature) {
                continue
            }

            foreach ($pattern in $rawPatterns) {
                $match = [System.Text.RegularExpressions.Regex]::Match($span.Signature, $pattern)
                if (-not $match.Success) {
                    continue
                }

                $findings.Add((New-StableRawFinding `
                    -Path $span.Path `
                    -Line $span.Line `
                    -RawType $match.Value `
                    -Signature $span.Signature))
                break
            }
        }
    }

    return $findings | Sort-Object Path, Line, RawType
}

$currentSurface = @(Get-CurrentSurface)
$rawFindings = @(Get-RawGameTypeFindings)
$homeschoolFindings = @(Get-HomeschoolNameFindings)
$stableRawFindings = @(Get-StableInterfaceRawFindings)

if ($ListCurrent) {
    "# Kind`tNamespace`tName`tPath"
    foreach ($entry in $currentSurface) {
        ConvertTo-SurfaceTsvLine $entry
    }

    if ($rawFindings.Count -gt 0) {
        ""
        "# RawGameTypePath`tLine`tNamespace`tRawType`tSignature"
        foreach ($finding in $rawFindings) {
            ConvertTo-RawTsvLine $finding
        }
    }

    if ($homeschoolFindings.Count -gt 0) {
        ""
        "# HomeschoolApiNamePath`tLine`tNamespace`tSignature"
        foreach ($finding in $homeschoolFindings) {
            ConvertTo-HomeschoolTsvLine $finding
        }
    }

    if ($stableRawFindings.Count -gt 0) {
        ""
        "# StableRawInterfacePath`tLine`tRawType`tSignature"
        foreach ($finding in $stableRawFindings) {
            ConvertTo-StableRawTsvLine $finding
        }
    }

    exit 0
}

Write-Host "ParalivesAPI public-surface scan completed."
Write-Host ("Public type declarations: " + $currentSurface.Count)
Write-Host ("Public raw game type exposures outside allowed namespaces: " + $rawFindings.Count)
Write-Host ("Public Homeschool-specific API names: " + $homeschoolFindings.Count)
Write-Host ("Stable interface raw game type exposures: " + $stableRawFindings.Count)

if ($rawFindings.Count -gt 0) {
    Write-Host "Raw game type exposure samples:"
    foreach ($finding in ($rawFindings | Select-Object -First 25)) {
        Write-Host ("RAW`t" + (ConvertTo-RawTsvLine $finding))
    }

    if ($rawFindings.Count -gt 25) {
        Write-Host ("RAW`t... " + ($rawFindings.Count - 25) + " more")
    }

    Write-Host "Use -FailOnRawGameTypes to make these findings fail the command after Native/Unsafe seams are ready."
}

if ($homeschoolFindings.Count -gt 0) {
    Write-Host "Homeschool-specific public API name samples:"
    foreach ($finding in ($homeschoolFindings | Select-Object -First 25)) {
        Write-Host ("HOMESCHOOL`t" + (ConvertTo-HomeschoolTsvLine $finding))
    }

    if ($homeschoolFindings.Count -gt 25) {
        Write-Host ("HOMESCHOOL`t... " + ($homeschoolFindings.Count - 25) + " more")
    }
}

if ($stableRawFindings.Count -gt 0) {
    Write-Host "Stable interface raw game type exposure samples:"
    foreach ($finding in ($stableRawFindings | Select-Object -First 25)) {
        Write-Host ("STABLE_RAW`t" + (ConvertTo-StableRawTsvLine $finding))
    }

    if ($stableRawFindings.Count -gt 25) {
        Write-Host ("STABLE_RAW`t... " + ($stableRawFindings.Count - 25) + " more")
    }
}

if ($FailOnRawGameTypes -and (($rawFindings.Count -gt 0) -or ($stableRawFindings.Count -gt 0))) {
    exit 1
}
