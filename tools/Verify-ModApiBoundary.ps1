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
$ModApiNetworkingRoot = Join-Path $RepoRoot "ModAPI.Networking"
$ModApiNetworkingProject = Join-Path $ModApiNetworkingRoot "ModAPI.Networking.csproj"

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

$HostNeutralForbiddenAssemblyNames = @(
    "0Harmony",
    "Assembly-CSharp",
    "Assembly-CSharp-firstpass",
    "Harmony",
    "HarmonyLib",
    "Manager",
    "ShelteredAPI",
    "UnityEngine",
    "UnityEngine.CoreModule",
    "UnityEngine.UI"
)

$HostNeutralForbiddenSourceSymbols = @(
    "Assembly-CSharp",
    "Assembly_CSharp",
    "HarmonyLib",
    "ShelteredAPI",
    "UnityEngine"
)

$ShelteredGameplayTerms = @(
    "Bunker",
    "Bunkers",
    "EncounterCharacter",
    "EncounterManager",
    "Expedition",
    "ExpeditionMap",
    "ExplorationParty",
    "Faction",
    "FamilyManager",
    "FamilyMember",
    "GameTime",
    "InventoryManager",
    "ItemManager",
    "Loot",
    "MapRegion",
    "NpcVisitor",
    "Raid",
    "Raids",
    "SaveData",
    "SaveEntry",
    "SaveManager",
    "Settlement",
    "Settlements",
    "ShelterDefense",
    "TradingPanel",
    "WeatherManager"
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
    param([string]$Root)

    Get-ChildItem -LiteralPath $Root -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
        Sort-Object FullName
}

function Add-ProjectReferenceFindings {
    param(
        [System.Collections.Generic.List[object]]$Findings,
        [string]$ProjectPath,
        [string]$Rule,
        [string[]]$ForbiddenNames
    )

    if (-not (Test-Path -LiteralPath $ProjectPath)) {
        throw "Missing project file: $ProjectPath"
    }

    [xml]$projectXml = Get-Content -LiteralPath $ProjectPath -Raw
    $projectRelativePath = ConvertTo-RepoRelativePath $ProjectPath
    foreach ($reference in $projectXml.GetElementsByTagName("Reference")) {
        $include = [string]$reference.GetAttribute("Include")
        $simpleInclude = ($include -split ",")[0].Trim()
        if ($ForbiddenNames -contains $simpleInclude) {
            $Findings.Add((New-BoundaryFinding -Rule $Rule -Path $projectRelativePath -Symbol $simpleInclude -Count 1))
        }
    }

    foreach ($reference in $projectXml.GetElementsByTagName("ProjectReference")) {
        $include = [string]$reference.GetAttribute("Include")
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($include)
        if ($ForbiddenNames -contains $projectName) {
            $Findings.Add((New-BoundaryFinding -Rule $Rule -Path $projectRelativePath -Symbol $projectName -Count 1))
        }
    }
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

    Add-ProjectReferenceFindings `
        -Findings $findings `
        -ProjectPath $ModApiProject `
        -Rule "project-reference" `
        -ForbiddenNames $ShelteredAssemblyNames

    Add-ProjectReferenceFindings `
        -Findings $findings `
        -ProjectPath $ModApiNetworkingProject `
        -Rule "networking-project-reference" `
        -ForbiddenNames $HostNeutralForbiddenAssemblyNames

    $gamePattern = "\b(" + (($GameSymbols | ForEach-Object { [System.Text.RegularExpressions.Regex]::Escape($_) }) -join "|") + ")\b"
    $nguiPattern = "\b(" + ($NguiSymbols -join "|") + ")\b"
    $pathPattern = "(" + (($ShelteredPathTerms | ForEach-Object { [System.Text.RegularExpressions.Regex]::Escape($_) }) -join "|") + ")"
    $namespacePattern = "(?m)^\s*namespace\s+([A-Za-z0-9_.]+)"

    foreach ($file in Get-SourceFiles $ModApiRoot) {
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

    $neutralReferencePattern = "\b(" + (($HostNeutralForbiddenSourceSymbols | ForEach-Object { [System.Text.RegularExpressions.Regex]::Escape($_) }) -join "|") + ")\b"
    $gameplayTermPattern = "\b(" + (($ShelteredGameplayTerms | ForEach-Object { [System.Text.RegularExpressions.Regex]::Escape($_) }) -join "|") + ")\b"
    $gameplayPathPattern = "(" + (($ShelteredGameplayTerms | ForEach-Object { [System.Text.RegularExpressions.Regex]::Escape($_) }) -join "|") + ")"

    foreach ($file in Get-SourceFiles $ModApiNetworkingRoot) {
        $relativePath = ConvertTo-RepoRelativePath $file.FullName
        $text = Get-Content -LiteralPath $file.FullName -Raw

        Add-MatchFindings -Findings $findings -Rule "networking-host-reference" -RelativePath $relativePath -Text $text -Pattern $neutralReferencePattern
        Add-MatchFindings -Findings $findings -Rule "networking-gameplay-term" -RelativePath $relativePath -Text $text -Pattern $gameplayTermPattern

        $pathMatches = [System.Text.RegularExpressions.Regex]::Matches($relativePath, $gameplayPathPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        $pathTerms = @{}
        foreach ($match in $pathMatches) {
            if (-not $pathTerms.ContainsKey($match.Value)) {
                $pathTerms[$match.Value] = 0
            }

            $pathTerms[$match.Value] += 1
        }

        foreach ($term in ($pathTerms.Keys | Sort-Object)) {
            $findings.Add((New-BoundaryFinding -Rule "networking-gameplay-filename" -Path $relativePath -Symbol $term -Count $pathTerms[$term]))
        }

        $namespaceMatches = [System.Text.RegularExpressions.Regex]::Matches($text, $namespacePattern)
        foreach ($match in $namespaceMatches) {
            $namespace = $match.Groups[1].Value
            $namespaceTerm = [System.Text.RegularExpressions.Regex]::Match($namespace, $gameplayPathPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            if ($namespaceTerm.Success) {
                $findings.Add((New-BoundaryFinding -Rule "networking-gameplay-namespace" -Path $relativePath -Symbol $namespace -Count 1))
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
        if ($parts.Length -lt 5) {
            throw "Invalid baseline line $lineNumber in '$BaselinePath'. Expected 5 tab-separated fields including a non-empty justification."
        }

        if ($parts.Length -gt 5) {
            throw "Invalid baseline line $lineNumber in '$BaselinePath'. Expected 5 tab-separated fields."
        }

        if ([string]::IsNullOrWhiteSpace($parts[4])) {
            throw "Invalid baseline line $lineNumber in '$BaselinePath'. Boundary exceptions require an explicit justification."
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

    Write-Host "Move Sheltered/Unity/Harmony/gameplay code out of ModAPI.Networking. If a legacy ModAPI exception is intentional, run with -ListCurrent and add a justified baseline entry."
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
