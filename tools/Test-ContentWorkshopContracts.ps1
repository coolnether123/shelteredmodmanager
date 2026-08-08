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

$project = Get-Content -LiteralPath (Join-Path $RepoRoot "Manager\ManagerGUI.csproj") -Raw
$mainForm = Get-Content -LiteralPath (Join-Path $RepoRoot "Manager\MainForm.cs") -Raw
$workshop = Get-Content -LiteralPath (Join-Path $RepoRoot "Manager\Views\ContentWorkshopTab.cs") -Raw
$service = Get-Content -LiteralPath (Join-Path $RepoRoot "Manager\Core\Services\ContentWorkshopProjectService.cs") -Raw
$canvas = Get-Content -LiteralPath (Join-Path $RepoRoot "Manager\Controls\PixelEditorCanvas.cs") -Raw
$scenarioAdapter = Get-Content -LiteralPath (Join-Path $RepoRoot "ShelteredScenarioEditor\Infrastructure\Assets\ScenarioPixelEditorAdapter.cs") -Raw
$failures = New-Object "System.Collections.Generic.List[string]"

$requiredCompileItems = @(
    "Core\Models\ContentWorkshopProject.cs",
    "Core\Services\ContentWorkshopProjectService.cs",
    "Controls\PixelEditorCanvas.cs",
    "Views\ContentWorkshopTab.cs"
)
foreach ($item in $requiredCompileItems) {
    if ($project -notmatch [Regex]::Escape("Include=`"$item`"")) {
        $failures.Add("ManagerGUI.csproj is missing $item.")
    }
}

if ($mainForm -notmatch '_tabControl\.Controls\.Add\(this\._contentWorkshopPage\)' -or
    $mainForm -notmatch '_contentWorkshopTab\.ModsPath\s*=\s*_settings\.ModsPath') {
    $failures.Add("Content Workshop must be a top-level manager tab connected to the configured mods folder.")
}

if ($mainForm -notmatch '_contentWorkshopTab\.ConfirmClose\(\)' -or
    $workshop -notmatch 'ConfirmDiscardChanges\(\)') {
    $failures.Add("Unsaved Content Workshop projects must be protected before replacement or manager exit.")
}

foreach ($action in @("ExportFolder", "ExportZip", "Install", "Validate", "ImportIcon")) {
    if ($service -notmatch ("public\s+ContentWorkshopOperationResult\s+" + $action + "\s*\(") -and
        $action -ne "Validate") {
        $failures.Add("Content Workshop service is missing the $action operation.")
    }
}
if ($service -notmatch 'public\s+ContentPackValidationResult\s+Validate\s*\(') {
    $failures.Add("Content Workshop service is missing shared content-pack validation.")
}
if ($service -notmatch 'ExportRoots\s*=\s*new string\[\]\s*\{\s*"About",\s*"Content",\s*"Assets"\s*\}') {
    $failures.Add("Exports must use an explicit package-root allowlist.")
}
if ($service -notmatch 'A mod with this ID is already installed') {
    $failures.Add("Local install must refuse to overwrite an existing mod.")
}

if ($canvas -notmatch 'PixelEditorSession' -or
    $scenarioAdapter -notmatch 'PixelEditorSession') {
    $failures.Add("Manager icons and scenario sprites must both consume the shared pixel editor core.")
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Error $failure
    }
    exit 1
}

Write-Host "Content Workshop integration contracts passed."
