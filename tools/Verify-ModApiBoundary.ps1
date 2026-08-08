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
    $BaselinePath = Join-Path $RepoRoot "documentation\ModAPI_Boundary_Baseline.tsv"
}

$ModApiRoot = Join-Path $RepoRoot "ModAPI"
$ModApiProject = Join-Path $ModApiRoot "ModAPI.csproj"

$GameSymbols = @(
    "BasePanel",
    "CraftingManager",
    "EntertainmentManager",
    "ExpeditionMap",
    "FamilyManager",
    "FoodManager",
    "GameModeManager",
    "SaveManager",
    "SaveData",
    "SaveEntry",
    "ISaveable",
    "ExplorationManager",
    "QuestManager",
    "QuestDefBase",
    "ScenarioDef",
    "ScenarioStage",
    "ScenarioSelectionPanel",
    "UIPanelManager",
    "ItemManager",
    "InventoryManager",
    "ObjectInteraction",
    "ObjectManager",
    "WeatherManager",
    "EncounterManager",
    "FamilyMember",
    "ExplorationParty",
    "EncounterCharacter",
    "NpcVisitor",
    "PlatformInput",
    "SettingsPCPanel",
    "SlotSelectionPanel",
    "StoragePanel",
    "RecyclingPanel",
    "TradingPanel",
    "ItemFabricationPanel",
    "Localization",
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
    "UI2DSprite",
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

$ShelteredAssemblyNames = @(
    "Assembly-CSharp",
    "Assembly-CSharp-firstpass",
    "Manager",
    "ShelteredAPI"
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
    return ConvertTo-VerificationTsvLine -Values @($Finding.Rule, $Finding.Path, $Finding.Symbol)
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
    $projectRelativePath = ConvertTo-RepositoryRelativePath -Path $ModApiProject -RepositoryRoot $RepoRoot
    foreach ($reference in $projectXml.GetElementsByTagName("Reference")) {
        $include = [string]$reference.GetAttribute("Include")
        $simpleInclude = ($include -split ",")[0].Trim()
        if ($ShelteredAssemblyNames -contains $simpleInclude) {
            $findings.Add((New-BoundaryFinding -Rule "project-reference" -Path $projectRelativePath -Symbol $simpleInclude -Count 1))
        }
    }

    $gamePattern = "\b(" + (($GameSymbols | ForEach-Object { [System.Text.RegularExpressions.Regex]::Escape($_) }) -join "|") + ")\b"
    $nguiPattern = "\b(" + ($NguiSymbols -join "|") + ")\b"
    $pathPattern = "(" + (($ShelteredPathTerms | ForEach-Object { [System.Text.RegularExpressions.Regex]::Escape($_) }) -join "|") + ")"
    $namespacePattern = "(?m)^\s*namespace\s+([A-Za-z0-9_.]+)"

    foreach ($file in Get-SourceFiles) {
        $relativePath = ConvertTo-RepositoryRelativePath -Path $file.FullName -RepositoryRoot $RepoRoot
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
    return ConvertTo-VerificationTsvLine -Values @($Finding.Rule, $Finding.Path, $Finding.Symbol, $Finding.Count)
}

function Read-Baseline {
    $baseline = @{}
    $rows = Import-VerificationTsvBaseline -Path $BaselinePath -DataColumnCount 4 -KeyColumnIndexes @(0, 1, 2) `
        -JustificationRequirement 'Boundary exceptions require an explicit justification.' `
        -MissingFileGuidance 'Run with -ListCurrent to print the current baseline candidates.'
    foreach ($key in $rows.Keys) {
        $parts = @($rows[$key].Fields)
        $count = 0
        if (-not [int]::TryParse($parts[3], [ref]$count)) {
            throw "Invalid count on baseline line $($rows[$key].LineNumber) in '$BaselinePath': '$($parts[3])'."
        }
        $entry = New-BoundaryFinding -Rule $parts[0] -Path $parts[1] -Symbol $parts[2] -Count $count
        $baseline[$key] = $entry
    }
    return $baseline
}

$currentFindings = @(Get-CurrentFindings)

if ($ListCurrent) {
    "# Rule`tPath`tSymbol`tAllowedCount`tJustification"
    foreach ($finding in $currentFindings) {
        (ConvertTo-TsvLine $finding) + "`t<required justification>"
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
