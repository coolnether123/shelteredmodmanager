[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory = $true)]
    [string]$Intent,

    [Parameter(Mandatory = $true)]
    [int]$X,

    [Parameter(Mandatory = $true)]
    [int]$Y,

    [Parameter(Mandatory = $true)]
    [int]$Width,

    [Parameter(Mandatory = $true)]
    [int]$Height,

    [string]$Label,
    [string]$BaseUrl = 'http://127.0.0.1:37422',
    [bool]$ResolveWindowFromStatus = $true,
    [bool]$UseExternalNativeInput = $true,
    [bool]$CapturePostScreenshot = $true,
    [bool]$BestEffortScreenshots = $true,
    [string]$StateRoute,
    [string]$ExpectedContains,
    [string]$PostPredicateRoute,
    [string]$PostPredicateContains,
    [string]$EvidenceDir,
    [string]$ClickLogPath,
    [switch]$AbsoluteCoordinates,
    [int]$RetryOffsetX,
    [int]$RetryOffsetY,
    [int]$PostDelayMs,
    [ValidateRange(1, 2)]
    [int]$MaxAttempts
)

$ErrorActionPreference = 'Stop'

# Keep the shared harness helper as the single implementation. This repo-local
# entry point supplies only the dedicated Epic workflow default.
$helperPath = 'A:\Dev\Projects\ShelteredAgentInterface\tools\Invoke-ShelteredVerifiedClick.ps1'
if (-not (Test-Path -LiteralPath $helperPath)) {
    throw "Sheltered Agent Interface click helper was not found at '$helperPath'."
}

$helperParameters = @{}
foreach ($key in $PSBoundParameters.Keys) {
    if ($key -ne 'BaseUrl' -and $key -ne 'ResolveWindowFromStatus' -and $key -ne 'UseExternalNativeInput' -and $key -ne 'CapturePostScreenshot' -and $key -ne 'BestEffortScreenshots') {
        $helperParameters[$key] = $PSBoundParameters[$key]
    }
}

$helperParameters['BaseUrl'] = $BaseUrl
if ($ResolveWindowFromStatus) {
    $helperParameters['ResolveWindowFromStatus'] = $true
}
if ($UseExternalNativeInput) {
    $helperParameters['ExternalNativeInput'] = $true
}
if ($CapturePostScreenshot) {
    $helperParameters['CapturePostScreenshot'] = $true
}
if ($BestEffortScreenshots) {
    $helperParameters['BestEffortScreenshots'] = $true
}

& $helperPath @helperParameters
exit $LASTEXITCODE
