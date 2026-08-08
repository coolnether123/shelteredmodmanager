[CmdletBinding()]
param(
    [string]$RepoRoot
)

# Compatibility entry point for existing CI and local workflows. The canonical
# contract now belongs to the standalone ShelteredScenarioEditor lifecycle.
$contractScript = Join-Path $PSScriptRoot 'Test-ShelteredScenarioEditorContracts.ps1'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    & $contractScript
}
else {
    & $contractScript -RepoRoot $RepoRoot
}
exit $LASTEXITCODE
