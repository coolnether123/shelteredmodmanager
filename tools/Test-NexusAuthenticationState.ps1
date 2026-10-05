#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('smm-auth-state-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$probe = Join-Path $PSScriptRoot 'Read-NexusAuthenticationState.ps1'
$passed = 0
function Assert-State($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:passed++
}
try {
    $path = Join-Path $root 'synthetic.ini'
    $absent = & $probe -SettingsPath $path
    Assert-State ($absent.SettingsFile -eq 'absent' -and $absent.SessionValidity -eq 'not-checked') 'Missing settings must not imply a valid session.'
    Assert-State (-not (Test-Path -LiteralPath $path)) 'The read-only probe created settings.'
    @(
        '# Synthetic fixture, not an account session.'
        'EnableNexusIntegration=true'
        'NexusOAuthAccessTokenProtected=synthetic-access-canary'
        'NexusOAuthRefreshTokenProtected=synthetic-refresh-canary'
        'NexusOAuthExpiresAtUtc=2999-01-01T00:00:00Z'
    ) | Set-Content -LiteralPath $path -Encoding UTF8
    $before = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    $state = & $probe -SettingsPath $path
    Assert-State ($state.NexusEnabled -eq $true -and $state.Expiry -eq 'future') 'Public configuration categories were incorrect.'
    Assert-State ($state.ProtectedAccessToken -eq 'present' -and $state.ProtectedRefreshToken -eq 'present') 'Protected-field presence was incorrect.'
    Assert-State (($state | ConvertTo-Json) -notmatch 'synthetic-access-canary|synthetic-refresh-canary') 'Probe returned protected values.'
    Assert-State ($state.SessionValidity -eq 'not-checked') 'Stored fields were treated as live authentication.'
    Assert-State ($before -eq (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash) 'The probe changed settings.'
    @('NexusOAuthAccessTokenProtected= ', 'NexusOAuthRefreshTokenProtected=',
        'EnableNexusIntegration=false', 'NexusOAuthExpiresAtUtc=2000-01-01T00:00:00Z') |
        Set-Content -LiteralPath $path -Encoding UTF8
    $state = & $probe -SettingsPath $path
    Assert-State ($state.ProtectedAccessToken -eq 'absent' -and $state.ProtectedRefreshToken -eq 'absent') 'Empty fields must not imply stored credentials.'
    Assert-State ($state.NexusEnabled -eq $false -and $state.Expiry -eq 'expired') 'Expired and disabled configuration was misclassified.'
    Add-Content -LiteralPath $path -Value 'NexusOAuthExpiresAtUtc=invalid'
    $state = & $probe -SettingsPath $path
    Assert-State ($state.Expiry -eq 'invalid') 'The last duplicate key must match SettingsService semantics.'
    Write-Host "All $passed Nexus authentication-state checks passed. Synthetic fixtures only."
}
finally {
    if ([IO.Path]::GetFullPath($root).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
