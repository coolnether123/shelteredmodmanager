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
$managerUpdatePath = Join-Path $RepoRoot 'Manager\Core\Services\ManagerSelfUpdateService.cs'
$nexusTabPath = Join-Path $RepoRoot 'Manager\Views\NexusModsTab.cs'
$installPath = Join-Path $RepoRoot 'Manager\Core\Services\NexusInstallService.cs'

Assert-Contains $headersPath 'Application-Name' 'Nexus requests must include Application-Name.'
Assert-Contains $headersPath 'Application-Version' 'Nexus requests must include Application-Version.'
Assert-Contains $servicePath 'GetDownloadUrlWithAuthorization' 'Non-premium download authorization must be supported.'
Assert-Contains $servicePath '_cacheGeneration\+\+' 'Cache invalidation must advance a generation.'
Assert-Contains $servicePath 'generation\s*!=\s*_cacheGeneration' 'Responses started before invalidation must not repopulate caches.'
Assert-Contains $servicePath 'IsInstallCandidate' 'File selection must use active-file eligibility before asking Nexus for authoritative download access.'
Assert-NotContains $servicePath 'file\.Manager\s*>\s*0' 'Legacy manager metadata must not block otherwise authorized downloads.'
Assert-Contains $servicePath '"/mod-files/"\s*\+\s*Escape\(draft\.ExistingModFileId\)\s*\+\s*"/versions"' 'v3 updates must use the current mod-file versions endpoint.'
Assert-NotContains $servicePath 'mod-file-update-groups|/file-update-groups|/file-update-groups' 'Deprecated update-group endpoints must not be used.'
Assert-Contains $settingsPath 'settings\.EnableExperimentalPublishTab\s*=\s*false' 'Experimental Nexus publishing must remain disabled in public builds.'
Assert-Contains $protocolPath 'ProtectedData\.Protect' 'Relayed nxm links must be encrypted at rest.'
Assert-Contains $protocolPath 'RestorePreviousHandler' 'Temporary nxm registration must restore the previous handler.'
Assert-Contains $managerUpdatePath 'MaximumDownloadBytes' 'Manager updates must enforce a download-size limit.'
Assert-Contains $managerUpdatePath 'VerifyDownloadedArchiveHashes' 'Manager update archives must be cryptographically verified when Nexus supplies hashes.'
Assert-Contains $managerUpdatePath 'MD5\.Create\(\)' 'Manager updates must verify the Nexus-provided MD5 when present.'
Assert-Contains $managerUpdatePath 'SHA256\.Create\(\)' 'Manager updates must support release metadata SHA-256 verification when present.'
Assert-Contains $managerUpdatePath 'DeleteStagedArchive' 'Manager update hash failures must delete the staged archive.'
Assert-Contains $managerUpdatePath 'ValidateStagedPackage' 'Manager update archives must be validated before replacement.'
Assert-Contains $managerUpdatePath 'ManagerUpdater\.exe' 'Manager updates must be applied by a detached updater.'
Assert-Contains $managerUpdatePath 'SpecialFolder\.LocalApplicationData' 'Protected installations must support user-writable staging.'
Assert-Contains $managerUpdatePath 'start\.Verb\s*=\s*"runas"' 'Protected installations must request Windows elevation for replacement.'
Assert-Contains (Join-Path $RepoRoot 'ManagerUpdater\Program.cs') 'reported a healthy UI startup' 'Updater success must require a manager health handshake.'
Assert-Contains (Join-Path $RepoRoot 'Manager\Program.cs') '--update-health-file' 'Manager startup must acknowledge updater health checks.'
Assert-Contains $nexusTabPath '_pendingManagerUpdateFileId' 'Manager nxm authorization must match a pending file request.'
Assert-Contains $nexusTabPath 'link\.FileId\s*==\s*_pendingManagerUpdateFileId' 'Manager nxm authorization must match the exact requested file.'
Assert-Contains $nexusTabPath 'BuildManagerUpdateHashExpectation' 'Manager updates must carry Nexus hash metadata into archive staging.'
Assert-Contains $nexusTabPath 'if\s*\(forceRefresh\)\s*_nexusService\.ClearCachedResponses' 'Manual Discover refresh must bypass service caches.'
Assert-Contains $nexusTabPath 'if\s*\(userInitiated\)\s*_nexusService\.ClearCachedResponses' 'Manual manager update checks must bypass service caches.'
$modManagerTabPath = Join-Path $RepoRoot 'Manager\Views\ModManagerTab.cs'
Assert-Contains $modManagerTabPath 'NexusVersionComparer\.IsRemoteNewer\(\s*mod\.Version,\s*cached\.RemoteVersion\)' 'Disk refresh must recompute update state against the current local version.'
Assert-Contains $modManagerTabPath 'settings\.ModsPath' 'Nexus UI cache scope must include the active Mods path.'
Assert-Contains $modManagerTabPath 'nexusServiceChanged' 'Replacing the Nexus service must invalidate UI cache state.'
Assert-Contains $installPath 'Path\.Combine\(modsPath, "_smm_backup"\)' 'Installer backups must stay on the target mods volume.'
Assert-NotContains $installPath 'Path\.Combine\(binRoot, "_smm_backup"\)' 'Installer must not require cross-volume directory moves.'

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Nexus Manager contract checks passed.'
