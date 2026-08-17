param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The .NET Framework 3.5 C# compiler was not found at $compiler."
}

$output = Join-Path ([IO.Path]::GetTempPath()) ("NexusOAuthSession-" + [Guid]::NewGuid().ToString('N') + '.exe')
$runtimeRef = Join-Path ${env:WINDIR} 'Microsoft.NET\Framework\v2.0.50727'
$frameworkRef = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\v3.5'
$references = @()
$references += '/r:' + (Join-Path $runtimeRef 'mscorlib.dll')
$references += '/r:' + (Join-Path $runtimeRef 'System.dll')
$references += '/r:' + (Join-Path $frameworkRef 'System.Web.Extensions.dll')
$references += '/r:' + (Join-Path $frameworkRef 'System.Core.dll')
$references += '/r:' + (Join-Path $runtimeRef 'System.Security.dll')
$sources = @(
    'Manager\Core\AppVersionInfo.cs',
    'Manager\Core\Models\AppSettings.cs',
    'Manager\Core\Models\NexusOAuthTokenSet.cs',
    'Manager\Core\Security\DpapiSecretProtector.cs',
    'Manager\Core\Security\NexusOAuthTokenProtector.cs',
    'Manager\Core\Services\SettingsService.cs',
    'Manager\Core\Services\NexusRequestCredential.cs',
    'Manager\Core\Services\NexusRequestHeaders.cs',
    'Manager\Core\Services\NexusOAuthConfiguration.cs',
    'Manager\Core\Services\NexusOAuthProtocol.cs',
    'Manager\Core\Services\NexusLoopbackCallbackListener.cs',
    'Manager\Core\Services\NexusOAuthClient.cs',
    'Manager\Core\Services\NexusOAuthService.cs',
    'tools\NexusOAuthSessionHarness.cs'
) | ForEach-Object { Join-Path $RepoRoot $_ }

try {
    & $compiler /noconfig /nologo /nowarn:0649 /target:exe /nostdlib+ /out:$output $references $sources
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & $output
    exit $LASTEXITCODE
}
finally {
    Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue
}
