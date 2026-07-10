param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$failures = New-Object System.Collections.Generic.List[string]

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { $failures.Add($Message) }
}

function Assert-Contains([string]$Path, [string]$Pattern, [string]$Message) {
    $text = Get-Content -LiteralPath $Path -Raw
    Assert-True ([regex]::IsMatch($text, $Pattern)) $Message
}

function Assert-NotContains([string]$Path, [string]$Pattern, [string]$Message) {
    $text = Get-Content -LiteralPath $Path -Raw
    Assert-True (-not [regex]::IsMatch($text, $Pattern)) $Message
}

$parserPath = Join-Path $RepoRoot 'Manager\Core\Models\NexusNxmLink.cs'
Add-Type -Path $parserPath

$future = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() + 900
$validUrl = "nxm://sheltered/mods/10/files/42?key=test_key-123&expires=$future&user_id=7"
$link = $null
$errorMessage = $null
$parsed = [Manager.Core.Models.NexusNxmLink]::TryParse($validUrl, [ref]$link, [ref]$errorMessage)
Assert-True $parsed 'A valid Nexus manager-download URL must parse.'
Assert-True ($null -ne $link -and $link.GameDomain -eq 'sheltered' -and $link.ModId -eq 10 -and $link.FileId -eq 42) 'Parsed Nexus link identity is incorrect.'
Assert-True ($null -ne $link -and $link.DownloadKey -eq 'test_key-123' -and $link.UserId -eq 7) 'Parsed Nexus authorization values are incorrect.'

$expired = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() - 1
$link = $null
$errorMessage = $null
Assert-True (-not [Manager.Core.Models.NexusNxmLink]::TryParse("nxm://sheltered/mods/10/files/42?key=expired&expires=$expired", [ref]$link, [ref]$errorMessage)) 'Expired Nexus authorization must be rejected.'

$link = $null
$errorMessage = $null
Assert-True (-not [Manager.Core.Models.NexusNxmLink]::TryParse('https://example.com/mods/10/files/42?key=x&expires=9999999999', [ref]$link, [ref]$errorMessage)) 'Non-nxm URLs must be rejected.'

$link = $null
$errorMessage = $null
Assert-True (-not [Manager.Core.Models.NexusNxmLink]::TryParse('nxm://sheltered/mods/10/files/42?key=%ZZ&expires=9999999999', [ref]$link, [ref]$errorMessage)) 'Malformed Nexus authorization encoding must be rejected without throwing.'

$servicePath = Join-Path $RepoRoot 'Manager\Core\Services\NexusModsService.cs'
$headersPath = Join-Path $RepoRoot 'Manager\Core\Services\NexusRequestHeaders.cs'
$settingsPath = Join-Path $RepoRoot 'Manager\Core\Services\SettingsService.cs'
$protocolPath = Join-Path $RepoRoot 'Manager\Core\Services\NexusProtocolHandlerService.cs'
$installPath = Join-Path $RepoRoot 'Manager\Core\Services\NexusInstallService.cs'

Assert-Contains $headersPath 'Application-Name' 'Nexus requests must include Application-Name.'
Assert-Contains $headersPath 'Application-Version' 'Nexus requests must include Application-Version.'
Assert-Contains $servicePath 'GetDownloadUrlWithAuthorization' 'Non-premium download authorization must be supported.'
Assert-Contains $servicePath 'IsInstallCandidate' 'File selection must use active-file eligibility before asking Nexus for authoritative download access.'
Assert-NotContains $servicePath 'file\.Manager\s*>\s*0' 'Legacy manager metadata must not block otherwise authorized downloads.'
Assert-Contains $servicePath '"/mod-files/"\s*\+\s*Escape\(draft\.ExistingModFileId\)\s*\+\s*"/versions"' 'v3 updates must use the current mod-file versions endpoint.'
Assert-NotContains $servicePath 'mod-file-update-groups|/file-update-groups|/file-update-groups' 'Deprecated update-group endpoints must not be used.'
Assert-Contains $settingsPath 'settings\.EnableExperimentalPublishTab\s*=\s*false' 'Experimental Nexus publishing must remain disabled in public builds.'
Assert-Contains $protocolPath 'ProtectedData\.Protect' 'Relayed nxm links must be encrypted at rest.'
Assert-Contains $protocolPath 'RestorePreviousHandler' 'Temporary nxm registration must restore the previous handler.'
Assert-Contains $installPath 'Path\.Combine\(modsPath, "_smm_backup"\)' 'Installer backups must stay on the target mods volume.'
Assert-NotContains $installPath 'Path\.Combine\(binRoot, "_smm_backup"\)' 'Installer must not require cross-volume directory moves.'

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Nexus Manager contract checks passed.'
