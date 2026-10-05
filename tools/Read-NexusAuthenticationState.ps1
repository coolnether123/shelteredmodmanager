#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string[]]$SettingsPath)

$ErrorActionPreference = 'Stop'
foreach ($path in $SettingsPath) {
    $resolved = [IO.Path]::GetFullPath($path)
    $state = [ordered]@{
        SettingsPath = $resolved
        ObservedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        SettingsFile = 'absent'
        NexusEnabled = $null
        ProtectedAccessToken = 'absent'
        ProtectedRefreshToken = 'absent'
        Expiry = 'absent'
        SessionValidity = 'not-checked'
    }
    if (Test-Path -LiteralPath $resolved -PathType Leaf) {
        $state.SettingsFile = 'present'
        # Do not call SettingsService.Load: it can sanitize and rewrite settings.
        # Only public configuration fields enter strings. Protected values are
        # consumed as presence flags, never retained, decrypted or returned.
        $reader = [IO.StreamReader]::new($resolved)
        try {
            while (-not $reader.EndOfStream) {
                $key = ''
                $hasSeparator = $false
                while (($character = $reader.Read()) -ge 0) {
                    if ($character -eq 10) { break }
                    if ($character -eq 61) { $hasSeparator = $true; break }
                    if ($key.Length -lt 128) { $key += [char]$character }
                }
                $key = $key.Trim()
                if (-not $hasSeparator) { continue }
                $isProtected = $key -in @('NexusOAuthAccessTokenProtected', 'NexusOAuthRefreshTokenProtected')
                $isPublic = $key -in @('EnableNexusIntegration', 'NexusOAuthExpiresAtUtc')
                $present = $false
                $value = ''
                while (($character = $reader.Read()) -ge 0 -and $character -ne 10) {
                    if ($isProtected -and -not [char]::IsWhiteSpace([char]$character)) { $present = $true }
                    if ($isPublic -and $value.Length -lt 128) { $value += [char]$character }
                }
                switch ($key) {
                    'NexusOAuthAccessTokenProtected' { $state.ProtectedAccessToken = if ($present) { 'present' } else { 'absent' } }
                    'NexusOAuthRefreshTokenProtected' { $state.ProtectedRefreshToken = if ($present) { 'present' } else { 'absent' } }
                    'EnableNexusIntegration' {
                        $enabled = $false
                        $state.NexusEnabled = if ([bool]::TryParse($value.Trim(), [ref]$enabled)) { $enabled } else { $null }
                    }
                    'NexusOAuthExpiresAtUtc' {
                        $expires = [DateTime]::MinValue
                        $state.Expiry = if ([string]::IsNullOrWhiteSpace($value)) { 'absent' }
                            elseif (-not [DateTime]::TryParse($value.Trim(), [Globalization.CultureInfo]::InvariantCulture,
                                [Globalization.DateTimeStyles]'AdjustToUniversal, AssumeUniversal', [ref]$expires)) { 'invalid' }
                            elseif ($expires.ToUniversalTime() -le [DateTime]::UtcNow) { 'expired' }
                            else { 'future' }
                    }
                }
            }
        }
        finally { $reader.Dispose() }
    }
    [pscustomobject]$state
}
