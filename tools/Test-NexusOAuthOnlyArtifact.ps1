param(
    [string]$ArtifactPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Dist\SMM\Manager.exe')
)

$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $ArtifactPath).Path)
$flags = [Reflection.BindingFlags]'Public, NonPublic, Instance, Static'
$config = $assembly.GetType('Manager.Core.Services.NexusOAuthConfiguration', $true)
if ($config.GetProperty('IsRegistered', $flags).GetValue($null, $null)) {
    throw 'The shipping Manager must disable unissued OAuth sign-in.'
}

$credentialType = $assembly.GetType('Manager.Core.Services.NexusRequestCredential', $true)
$headers = $assembly.GetType('Manager.Core.Services.NexusRequestHeaders', $true).GetMethod('ApplyJsonHeaders', $flags)
$credential = [Activator]::CreateInstance($credentialType, $true)
$credentialType.GetField('ApiKey', $flags).SetValue($credential, 'synthetic-artifact-key')
$request = [Net.WebRequest]::Create('https://api.nexusmods.com/v1/users/validate')
$headers.Invoke($null, @($request, $credential))
if ($request.Headers['apikey'] -ne 'synthetic-artifact-key' -or $request.Headers['Authorization'] -or $request.AllowAutoRedirect) {
    throw 'The shipping artifact must send the API key only as an API header and block redirects.'
}
$credentialType.GetField('BearerToken', $flags).SetValue($credential, 'synthetic-oauth-token')
$request = [Net.WebRequest]::Create('https://api.nexusmods.com/v1/users/validate')
$headers.Invoke($null, @($request, $credential))
if ($request.Headers['Authorization'] -ne 'Bearer synthetic-oauth-token' -or $request.Headers['apikey']) {
    throw 'The dormant OAuth path must send only bearer authentication.'
}
Write-Host 'Nexus shipping artifact checks passed.'
