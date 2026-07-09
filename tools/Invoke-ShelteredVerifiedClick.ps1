[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$BaseUrl = 'http://127.0.0.1:37422',
    [Parameter(ValueFromRemainingArguments = $true)]
    [object[]]$Arguments
)

$ErrorActionPreference = 'Stop'

# Keep the shared harness helper as the single implementation. This repo-local
# entry point supplies only the dedicated Epic workflow default.
$helperPath = 'A:\Dev\Projects\ShelteredAgentInterface\tools\Invoke-ShelteredVerifiedClick.ps1'
if (-not (Test-Path -LiteralPath $helperPath)) {
    throw "Sheltered Agent Interface click helper was not found at '$helperPath'."
}

& $helperPath -BaseUrl $BaseUrl @Arguments
exit $LASTEXITCODE
