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

function Get-NamespaceSource([string]$Path) {
    $text = Get-Content -LiteralPath $Path -Raw
    $index = $text.IndexOf('namespace ')
    if ($index -lt 0) { throw "No namespace declaration found in $Path" }
    return $text.Substring($index)
}

$configurationPath = Join-Path $RepoRoot 'Manager\Core\Services\NexusOAuthConfiguration.cs'
$protocolPath = Join-Path $RepoRoot 'Manager\Core\Services\NexusOAuthProtocol.cs'
$listenerPath = Join-Path $RepoRoot 'Manager\Core\Services\NexusLoopbackCallbackListener.cs'
$clientPath = Join-Path $RepoRoot 'Manager\Core\Services\NexusOAuthClient.cs'
$servicePath = Join-Path $RepoRoot 'Manager\Core\Services\NexusOAuthService.cs'
$headersPath = Join-Path $RepoRoot 'Manager\Core\Services\NexusRequestHeaders.cs'
$settingsPath = Join-Path $RepoRoot 'Manager\Core\Services\SettingsService.cs'
$tokenProtectorPath = Join-Path $RepoRoot 'Manager\Core\Security\NexusOAuthTokenProtector.cs'
$dpapiPath = Join-Path $RepoRoot 'Manager\Core\Security\DpapiSecretProtector.cs'
$settingsUiPath = Join-Path $RepoRoot 'Manager\Views\SettingsTab.cs'

$protocolSource = @"
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
$(Get-NamespaceSource $configurationPath)
$(Get-NamespaceSource $protocolPath)
"@
Add-Type -TypeDefinition $protocolSource -ReferencedAssemblies @('System.dll', 'System.Core.dll') -IgnoreWarnings

$oauthAssembly = [AppDomain]::CurrentDomain.GetAssemblies() |
    Where-Object { $_.GetType('Manager.Core.Services.NexusOAuthProtocol', $false) -ne $null } |
    Select-Object -First 1
$protocolType = $oauthAssembly.GetType('Manager.Core.Services.NexusOAuthProtocol', $true)
$configurationType = $oauthAssembly.GetType('Manager.Core.Services.NexusOAuthConfiguration', $true)
$staticNonPublic = [Reflection.BindingFlags]'Static, NonPublic'
$instanceNonPublic = [Reflection.BindingFlags]'Instance, NonPublic'

$createMethod = $protocolType.GetMethod('CreateAuthorizationRequest', $staticNonPublic)
$request = $createMethod.Invoke($null, @())
$state = [string]$request.GetType().GetField('State', $instanceNonPublic).GetValue($request)
$verifier = [string]$request.GetType().GetField('CodeVerifier', $instanceNonPublic).GetValue($request)
$challenge = [string]$request.GetType().GetField('CodeChallenge', $instanceNonPublic).GetValue($request)
$authorizationUri = [Uri]$request.GetType().GetField('AuthorizationUri', $instanceNonPublic).GetValue($request)

Assert-True ($verifier.Length -ge 43 -and $verifier.Length -le 128) 'PKCE verifier must satisfy RFC 7636 length limits.'
Assert-True ($challenge -match '^[A-Za-z0-9_-]{43}$') 'PKCE S256 challenge must be unpadded base64url.'
Assert-True ($state.Length -ge 32 -and $state -match '^[A-Za-z0-9_-]+$') 'OAuth state must be a high-entropy base64url value.'
Assert-True ($authorizationUri.Scheme -eq 'https' -and $authorizationUri.Host -eq 'users.nexusmods.com') 'Authorization must use the Nexus HTTPS user service.'
Assert-True ($authorizationUri.Query -match 'code_challenge_method=S256') 'Authorization URL must require PKCE S256.'
Assert-True ($authorizationUri.Query -match [regex]::Escape([Uri]::EscapeDataString('http://127.0.0.1:52147/callback'))) 'Authorization URL must use the registered loopback callback.'

$parseMethod = $protocolType.GetMethod('ParseCallback', $staticNonPublic)
$validTarget = '/callback?code=review-code&state=' + [Uri]::EscapeDataString($state)
$validResult = $parseMethod.Invoke($null, @($validTarget, $state))
Assert-True ([bool]$validResult.GetType().GetField('Success', $instanceNonPublic).GetValue($validResult)) 'Matching callback code/state must be accepted.'

$wrongStateResult = $parseMethod.Invoke($null, @('/callback?code=review-code&state=wrong', $state))
Assert-True (-not [bool]$wrongStateResult.GetType().GetField('Success', $instanceNonPublic).GetValue($wrongStateResult)) 'Mismatched OAuth state must be rejected.'

$providerErrorTarget = '/callback?error=access_denied&state=' + [Uri]::EscapeDataString($state)
$providerErrorResult = $parseMethod.Invoke($null, @($providerErrorTarget, $state))
Assert-True (-not [bool]$providerErrorResult.GetType().GetField('Success', $instanceNonPublic).GetValue($providerErrorResult)) 'Provider authorization errors must not be treated as success.'

Assert-Contains $listenerPath 'new TcpListener\(IPAddress\.Loopback,\s*NexusOAuthConfiguration\.CallbackPort\)' 'OAuth callback listener must bind only to loopback.'
Assert-Contains $listenerPath 'IPAddress\.IsLoopback' 'OAuth callback requests must be verified as local.'
Assert-Contains $listenerPath 'Cache-Control: no-store' 'OAuth browser response must not be cached.'
Assert-Contains $listenerPath 'MaximumRequestLineLength' 'OAuth callback input must have a bounded request line.'
Assert-Contains $listenerPath 'MaximumHeaderLength' 'OAuth callback input must have bounded headers.'
Assert-Contains $clientPath 'code_verifier' 'Authorization-code exchange must include the PKCE verifier.'
Assert-Contains $clientPath 'grant_type.*refresh_token' 'OAuth refresh-token flow must be implemented.'
Assert-NotContains $clientPath 'client_secret' 'A public desktop client must not contain or transmit a client secret.'
Assert-Contains $headersPath '"Authorization"\]\s*=\s*"Bearer "' 'Nexus API requests must support OAuth bearer authorization.'
Assert-Contains $settingsPath 'NexusOAuthAccessTokenProtected' 'OAuth access tokens must use a protected settings key.'
Assert-Contains $settingsPath 'NexusOAuthRefreshTokenProtected' 'OAuth refresh tokens must use a protected settings key.'
Assert-NotContains $settingsPath 'data\["NexusOAuthAccessToken"\]|data\["NexusOAuthRefreshToken"\]' 'OAuth tokens must never be persisted in plaintext settings keys.'
Assert-Contains $tokenProtectorPath 'NexusOAuth\.AccessToken\.v1' 'OAuth access tokens must use purpose-specific DPAPI entropy.'
Assert-Contains $tokenProtectorPath 'NexusOAuth\.RefreshToken\.v1' 'OAuth refresh tokens must use purpose-specific DPAPI entropy.'
Assert-Contains $servicePath 'IsAccessTokenUsable\(DateTime\.UtcNow,\s*RefreshBuffer\)' 'Expired OAuth access tokens must be refreshed before API use.'
Assert-Contains $servicePath 'NexusApiKey' 'Legacy personal API keys must remain an explicit fallback.'
Assert-Contains $settingsUiPath 'Sign in with Nexus' 'Settings must expose Nexus OAuth sign-in.'
Assert-Contains $settingsUiPath 'NexusOAuthSignOutRequested' 'Settings must expose Nexus OAuth sign-out.'
Assert-True ([string]::IsNullOrEmpty([string]$configurationType.GetField('ClientId', $staticNonPublic).GetRawConstantValue())) 'Review source must not invent an unissued Nexus client ID.'

$managerAssemblyPath = Join-Path $RepoRoot 'Dist\SMM\Manager.exe'
if (Test-Path -LiteralPath $managerAssemblyPath) {
    $managerAssembly = [Reflection.Assembly]::LoadFile($managerAssemblyPath)
    $oauthProtectorType = $managerAssembly.GetType('Manager.Core.Security.NexusOAuthTokenProtector', $true)
    $protectAccess = $oauthProtectorType.GetMethod('ProtectAccessToken', $staticNonPublic)
    $unprotectAccess = $oauthProtectorType.GetMethod('TryUnprotectAccessToken', $staticNonPublic)
    $sampleToken = 'oauth-contract-token-' + [Guid]::NewGuid().ToString('N')
    $protectedToken = [string]$protectAccess.Invoke($null, @($sampleToken))
    $unprotectArguments = @($protectedToken, $null)
    $unprotected = [bool]$unprotectAccess.Invoke($null, $unprotectArguments)
    Assert-True ($protectedToken.Length -gt 0 -and $protectedToken -ne $sampleToken) 'OAuth token DPAPI protection must not return plaintext.'
    Assert-True ($unprotected -and [string]$unprotectArguments[1] -eq $sampleToken) 'OAuth token DPAPI protection must round-trip for the current Windows user.'

    $tempSettingsPath = Join-Path ([IO.Path]::GetTempPath()) ('smm-oauth-settings-' + [Guid]::NewGuid().ToString('N') + '.ini')
    try {
        $settingsServiceType = $managerAssembly.GetType('Manager.Core.Services.SettingsService', $true)
        $appSettingsType = $managerAssembly.GetType('Manager.Core.Models.AppSettings', $true)
        $settingsServiceConstructor = $settingsServiceType.GetConstructor([Type[]]@([string]))
        $constructorArguments = New-Object 'object[]' 1
        $constructorArguments[0] = [string]$tempSettingsPath
        $settingsService = $settingsServiceConstructor.Invoke($constructorArguments)
        $settings = [Activator]::CreateInstance($appSettingsType)
        $tokens = $appSettingsType.GetProperty('NexusOAuthTokens').GetValue($settings, $null)
        $tokens.GetType().GetProperty('AccessToken').SetValue($tokens, $sampleToken, $null)
        $sampleRefreshToken = 'oauth-refresh-token-' + [Guid]::NewGuid().ToString('N')
        $tokens.GetType().GetProperty('RefreshToken').SetValue($tokens, $sampleRefreshToken, $null)
        $tokens.GetType().GetProperty('ExpiresAtUtc').SetValue($tokens, [DateTime]::UtcNow.AddHours(1), $null)
        $settingsServiceType.GetMethod('Save').Invoke($settingsService, @($settings)) | Out-Null

        $persistedText = Get-Content -LiteralPath $tempSettingsPath -Raw
        Assert-True (-not $persistedText.Contains($sampleToken) -and -not $persistedText.Contains($sampleRefreshToken)) 'OAuth tokens must never appear in plaintext in the INI file.'
        Assert-True ($persistedText.Contains('NexusOAuthAccessTokenProtected=') -and $persistedText.Contains('NexusOAuthRefreshTokenProtected=')) 'Protected OAuth token settings must be persisted.'

        $loaded = $settingsServiceType.GetMethod('Load').Invoke($settingsService, @())
        $loadedTokens = $appSettingsType.GetProperty('NexusOAuthTokens').GetValue($loaded, $null)
        Assert-True ([string]$loadedTokens.GetType().GetProperty('AccessToken').GetValue($loadedTokens, $null) -eq $sampleToken) 'Settings service must recover the OAuth access token for the current user.'
        Assert-True ([string]$loadedTokens.GetType().GetProperty('RefreshToken').GetValue($loadedTokens, $null) -eq $sampleRefreshToken) 'Settings service must recover the OAuth refresh token for the current user.'
    }
    finally {
        if (Test-Path -LiteralPath $tempSettingsPath) { Remove-Item -LiteralPath $tempSettingsPath -Force }
    }
}
else {
    $failures.Add("Manager build output is missing; DPAPI OAuth token round-trip was not exercised: $managerAssemblyPath")
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Nexus OAuth/PKCE contract checks passed.'
