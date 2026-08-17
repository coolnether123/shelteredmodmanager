param(
    [string]$ArtifactPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Dist\SMM\Manager.exe')
)

$ErrorActionPreference = 'Stop'
$failures = New-Object System.Collections.Generic.List[string]
function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { $failures.Add($Message) }
}

$resolved = (Resolve-Path -LiteralPath $ArtifactPath).Path
$assembly = [Reflection.Assembly]::LoadFile($resolved)
$allFlags = [Reflection.BindingFlags]'Public, NonPublic, Instance, Static'

$settingsType = $assembly.GetType('Manager.Core.Models.AppSettings', $true)
$credentialType = $assembly.GetType('Manager.Core.Services.NexusRequestCredential', $true)
$serviceType = $assembly.GetType('Manager.Core.Services.NexusModsService', $true)
$graphQlType = $assembly.GetType('Manager.Core.Services.NexusGraphQlClient', $true)
$v3Type = $assembly.GetType('Manager.Core.Services.NexusV3RestClient', $true)
$settingsUiType = $assembly.GetType('Manager.Views.SettingsTab', $true)

Assert-True ($null -eq $assembly.GetType('Manager.Core.Security.NexusApiKeyProtector', $false)) 'Public artifact still contains the obsolete personal-key protector type.'
Assert-True ($null -eq $assembly.GetType('Manager.Core.Services.StaticNexusCredentialProvider', $false)) 'Public artifact still contains a static personal-credential provider.'
Assert-True ($null -eq $settingsType.GetProperty('NexusApiKey', $allFlags)) 'Public settings model still exposes a NexusApiKey property.'
Assert-True ($null -eq $credentialType.GetField('ApiKey', $allFlags)) 'Production request credential still has an API-key field.'
Assert-True ($null -eq $settingsUiType.GetField('_nexusApiKeyTextBox', $allFlags)) 'Production UI still contains a personal-key input control.'

$stringConstructor = [Type[]]@([string])
Assert-True ($null -eq $serviceType.GetConstructor($allFlags, $null, $stringConstructor, $null)) 'NexusModsService can still be constructed from a personal key string.'
Assert-True ($null -eq $graphQlType.GetConstructor($allFlags, $null, $stringConstructor, $null)) 'NexusGraphQlClient can still be constructed from a personal key string.'
Assert-True ($null -eq $v3Type.GetConstructor($allFlags, $null, $stringConstructor, $null)) 'NexusV3RestClient can still be constructed from a personal key string.'

$downloadMethod = $serviceType.GetMethods($allFlags) | Where-Object { $_.Name -eq 'GetDownloadUrl' } | Select-Object -First 1
$authorizedDownloadMethod = $serviceType.GetMethods($allFlags) | Where-Object { $_.Name -eq 'GetDownloadUrlWithAuthorization' } | Select-Object -First 1
Assert-True ($null -ne $downloadMethod -and $downloadMethod.GetParameters().Count -eq 4) 'Download URL API still exposes an extra credential override parameter.'
Assert-True ($null -ne $authorizedDownloadMethod -and $authorizedDownloadMethod.GetParameters().Count -eq 6) 'Authorized download URL API still exposes an extra credential override parameter.'

$bytes = [IO.File]::ReadAllBytes($resolved)
$ascii = [Text.Encoding]::ASCII.GetString($bytes)
$unicode = [Text.Encoding]::Unicode.GetString($bytes)
foreach ($forbidden in @('APIKEY', 'X-API-Key', 'Legacy API Key:', 'Get API Key', 'Reveal Key', 'personal API key fallback')) {
    Assert-True (-not $ascii.Contains($forbidden) -and -not $unicode.Contains($forbidden)) "Public artifact contains forbidden personal-key UI/header text: $forbidden"
}
$headersType = $assembly.GetType('Manager.Core.Services.NexusRequestHeaders', $true)
$credential = [Activator]::CreateInstance($credentialType, $true)
$credentialType.GetField('BearerToken', $allFlags).SetValue($credential, 'artifact-oauth-token')
$request = [Net.HttpWebRequest][Net.WebRequest]::Create('https://api.nexusmods.com/v1/users/validate')
$applyHeaders = $headersType.GetMethod('ApplyJsonHeaders', $allFlags, $null, [Type[]]@([Net.HttpWebRequest], $credentialType), $null)
$applyHeaders.Invoke($null, @($request, $credential)) | Out-Null
Assert-True ($request.Headers['Authorization'] -eq 'Bearer artifact-oauth-token') 'Public artifact did not apply OAuth bearer authentication at runtime.'
Assert-True ([string]::IsNullOrEmpty($request.Headers['APIKEY'])) 'Public artifact emitted a personal API-key header at runtime.'

$resourceNames = $assembly.GetManifestResourceNames()
Assert-True (-not ($resourceNames | Where-Object { $_ -match '(?i)apikey|api-key' })) 'Public artifact embeds an API-key-specific resource.'

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "OAuth-only artifact checks passed: $resolved"
