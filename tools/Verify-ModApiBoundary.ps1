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
    $BaselinePath = Join-Path $RepoRoot "documentation\ModAPI_Boundary_Baseline.tsv"
}

$ModApiRoot = Join-Path $RepoRoot "ModAPI"
$ModApiProject = Join-Path $ModApiRoot "ModAPI.csproj"

$GameSymbols = @(
    "FamilyManager",
    "SaveManager",
    "ExplorationManager",
    "QuestManager",
    "ScenarioDef",
    "UIPanelManager",
    "ItemManager",
    "InventoryManager",
    "WeatherManager",
    "EncounterManager",
    "FamilyMember",
    "ExplorationParty",
    "EncounterCharacter",
    "ShelteredAPI"
)

$NguiSymbols = @(
    "NGUI[A-Za-z0-9_]*",
    "UIAtlas",
    "UIButton",
    "UICamera",
    "UICenterOnChild",
    "UICheckbox",
    "UIDrawCall",
    "UIEventListener",
    "UIFont",
    "UIGrid",
    "UIInput",
    "UIKeyNavigation",
    "UILabel",
    "UIPanel",
    "UIPopupList",
    "UIProgressBar",
    "UIRect",
    "UIRoot",
    "UIScrollView",
    "UISlider",
    "UISprite",
    "UITable",
    "UITexture",
    "UIToggle",
    "UIWidget"
)

$ShelteredPathTerms = @(
    "Sheltered",
    "SaveManager",
    "FamilyManager",
    "ExplorationManager",
    "QuestManager",
    "ScenarioDef",
    "UIPanelManager",
    "ItemManager",
    "InventoryManager",
    "WeatherManager",
    "EncounterManager",
    "FamilyMember",
    "ExplorationParty",
    "EncounterCharacter",
    "NGUI"
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

function New-BoundaryFinding {
    param(
        [string]$Rule,
        [string]$Path,
        [string]$Symbol,
        [int]$Count
    )

    [pscustomobject]@{
        Rule = $Rule
        Path = $Path
        Symbol = $Symbol
        Count = $Count
    }
}

function New-Key {
    param($Finding)
    return "{0}`t{1}`t{2}" -f $Finding.Rule, $Finding.Path, $Finding.Symbol
}

function Get-SourceFiles {
    Get-ChildItem -LiteralPath $ModApiRoot -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
        Sort-Object FullName
}

function Add-MatchFindings {
    param(
        [System.Collections.Generic.List[object]]$Findings,
        [string]$Rule,
        [string]$RelativePath,
        [string]$Text,
        [string]$Pattern
    )

    $matches = [System.Text.RegularExpressions.Regex]::Matches($Text, $Pattern)
    if ($matches.Count -eq 0) {
        return
    }

    $groups = @{}
    foreach ($match in $matches) {
        $symbol = $match.Value
        if (-not $groups.ContainsKey($symbol)) {
            $groups[$symbol] = 0
        }

        $groups[$symbol] += 1
    }

    foreach ($symbol in ($groups.Keys | Sort-Object)) {
        $Findings.Add((New-BoundaryFinding -Rule $Rule -Path $RelativePath -Symbol $symbol -Count $groups[$symbol]))
    }
}

function Get-CurrentFindings {
    $findings = New-Object "System.Collections.Generic.List[object]"

    if (-not (Test-Path -LiteralPath $ModApiProject)) {
        throw "Missing project file: $ModApiProject"
    }

    [xml]$projectXml = Get-Content -LiteralPath $ModApiProject -Raw
    $projectRelativePath = ConvertTo-RepoRelativePath $ModApiProject
    foreach ($reference in $projectXml.GetElementsByTagName("Reference")) {
        $include = [string]$reference.GetAttribute("Include")
        if ($include -eq "Assembly-CSharp" -or $include -eq "Manager") {
            $findings.Add((New-BoundaryFinding -Rule "project-reference" -Path $projectRelativePath -Symbol $include -Count 1))
        }
    }

    $gamePattern = "\b(" + (($GameSymbols | ForEach-Object { [System.Text.RegularExpressions.Regex]::Escape($_) }) -join "|") + ")\b"
    $nguiPattern = "\b(" + ($NguiSymbols -join "|") + ")\b"
    $pathPattern = "(" + (($ShelteredPathTerms | ForEach-Object { [System.Text.RegularExpressions.Regex]::Escape($_) }) -join "|") + ")"
    $namespacePattern = "(?m)^\s*namespace\s+([A-Za-z0-9_.]+)"

    foreach ($file in Get-SourceFiles) {
        $relativePath = ConvertTo-RepoRelativePath $file.FullName
        $text = Get-Content -LiteralPath $file.FullName -Raw

        Add-MatchFindings -Findings $findings -Rule "source-symbol" -RelativePath $relativePath -Text $text -Pattern $gamePattern
        Add-MatchFindings -Findings $findings -Rule "ngui-symbol" -RelativePath $relativePath -Text $text -Pattern $nguiPattern

        $pathMatches = [System.Text.RegularExpressions.Regex]::Matches($relativePath, $pathPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        $pathTerms = @{}
        foreach ($match in $pathMatches) {
            if (-not $pathTerms.ContainsKey($match.Value)) {
                $pathTerms[$match.Value] = 0
            }

            $pathTerms[$match.Value] += 1
        }

        foreach ($term in ($pathTerms.Keys | Sort-Object)) {
            $findings.Add((New-BoundaryFinding -Rule "sheltered-filename" -Path $relativePath -Symbol $term -Count $pathTerms[$term]))
        }

        $namespaceMatches = [System.Text.RegularExpressions.Regex]::Matches($text, $namespacePattern)
        foreach ($match in $namespaceMatches) {
            $namespace = $match.Groups[1].Value
            $namespaceTerm = [System.Text.RegularExpressions.Regex]::Match($namespace, $pathPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            if ($namespaceTerm.Success) {
                $findings.Add((New-BoundaryFinding -Rule "sheltered-namespace" -Path $relativePath -Symbol $namespace -Count 1))
            }
        }
    }

    $collapsed = @{}
    foreach ($finding in $findings) {
        $key = New-Key $finding
        if (-not $collapsed.ContainsKey($key)) {
            $collapsed[$key] = New-BoundaryFinding -Rule $finding.Rule -Path $finding.Path -Symbol $finding.Symbol -Count 0
        }

        $collapsed[$key].Count += $finding.Count
    }

    return $collapsed.Values | Sort-Object Rule, Path, Symbol
}

function ConvertTo-TsvLine {
    param($Finding)
    return "{0}`t{1}`t{2}`t{3}" -f $Finding.Rule, $Finding.Path, $Finding.Symbol, $Finding.Count
}

function Read-Baseline {
    if (-not (Test-Path -LiteralPath $BaselinePath)) {
        throw "Missing baseline file: $BaselinePath. Run with -ListCurrent to print the current baseline candidates."
    }

    $baseline = @{}
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $BaselinePath) {
        $lineNumber += 1
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith("#")) {
            continue
        }

        $parts = $line -split "`t"
        if ($parts.Length -ne 4) {
            throw "Invalid baseline line $lineNumber in '$BaselinePath'. Expected 4 tab-separated fields."
        }

        $count = 0
        if (-not [int]::TryParse($parts[3], [ref]$count)) {
            throw "Invalid count on baseline line $lineNumber in '$BaselinePath': '$($parts[3])'."
        }

        $entry = New-BoundaryFinding -Rule $parts[0] -Path $parts[1] -Symbol $parts[2] -Count $count
        $key = New-Key $entry
        if ($baseline.ContainsKey($key)) {
            throw "Duplicate baseline entry on line ${lineNumber}: $key"
        }

        $baseline[$key] = $entry
    }

    return $baseline
}

$currentFindings = @(Get-CurrentFindings)

if ($ListCurrent) {
    "# Rule`tPath`tSymbol`tAllowedCount"
    foreach ($finding in $currentFindings) {
        ConvertTo-TsvLine $finding
    }

    exit 0
}

$baselineEntries = Read-Baseline
$unbaselined = New-Object "System.Collections.Generic.List[object]"
$stale = New-Object "System.Collections.Generic.List[object]"
$currentByKey = @{}

foreach ($finding in $currentFindings) {
    $key = New-Key $finding
    $currentByKey[$key] = $finding
    if (-not $baselineEntries.ContainsKey($key)) {
        $unbaselined.Add($finding)
        continue
    }

    $allowed = $baselineEntries[$key].Count
    if ($finding.Count -gt $allowed) {
        $unbaselined.Add((New-BoundaryFinding -Rule $finding.Rule -Path $finding.Path -Symbol $finding.Symbol -Count ($finding.Count - $allowed)))
    } elseif ($finding.Count -lt $allowed) {
        $stale.Add((New-BoundaryFinding -Rule $finding.Rule -Path $finding.Path -Symbol $finding.Symbol -Count ($allowed - $finding.Count)))
    }
}

foreach ($key in $baselineEntries.Keys) {
    if (-not $currentByKey.ContainsKey($key)) {
        $stale.Add($baselineEntries[$key])
    }
}

if ($unbaselined.Count -gt 0) {
    Write-Host ("ModAPI boundary verifier failed. New or increased violations: " + $unbaselined.Count)
    foreach ($finding in ($unbaselined | Sort-Object Rule, Path, Symbol)) {
        Write-Host ("NEW`t" + (ConvertTo-TsvLine $finding))
    }

    exit 1
}

Write-Host "ModAPI boundary verifier passed."
Write-Host ("Current findings within baseline: " + $currentFindings.Count)
Write-Host ("Baseline entries: " + $baselineEntries.Count)

if ($stale.Count -gt 0) {
    Write-Host ("Baseline entries that can be reduced or removed: " + $stale.Count)
    foreach ($finding in ($stale | Sort-Object Rule, Path, Symbol | Select-Object -First 20)) {
        Write-Host ("STALE`t" + (ConvertTo-TsvLine $finding))
    }

    if ($stale.Count -gt 20) {
        Write-Host ("STALE`t... " + ($stale.Count - 20) + " more")
    }
}
