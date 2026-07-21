[CmdletBinding()]
param(
    [string]$AgentInterfaceRoot = 'A:\Dev\Projects\ShelteredAgentInterface'
)

$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $AgentInterfaceRoot 'ShelteredAgentInterface\Core\UnityAgentUtil.cs'
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Sheltered Agent Interface source was not found: $sourcePath"
}

$source = Get-Content -LiteralPath $sourcePath -Raw
foreach ($required in @(
    'HasShellField(fields, "workspace")',
    'AppendWorkspaceProperty(sb, "workspaceBody"',
    'AppendWorkspaceSubtabArrayProperty',
    'AppendNavigatorGroupArrayProperty',
    'AppendNavigatorRowArrayProperty',
    'AppendBreadcrumbArrayProperty',
    'AppendStatusChipArrayProperty',
    'AppendSectionArrayProperty(sb, "sections"')) {
    if ($source.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Authoring shell workspace projection contract is missing: $required"
    }
}

Write-Host 'AUTHORING SHELL WORKSPACE PROJECTION PASS'
