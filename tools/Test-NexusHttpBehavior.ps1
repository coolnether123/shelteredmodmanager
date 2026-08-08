param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path ${env:WINDIR} 'Microsoft.NET\Framework\v3.5\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The .NET Framework 3.5 C# compiler was not found at $compiler."
}

$output = Join-Path ([IO.Path]::GetTempPath()) ("NexusHttpBehavior-" + [Guid]::NewGuid().ToString('N') + '.exe')
$sources = @(
    'Manager\Core\AppVersionInfo.cs',
    'Manager\Core\Services\NexusRequestCredential.cs',
    'Manager\Core\Services\NexusRequestFailurePolicy.cs',
    'Manager\Core\Services\NexusRequestHeaders.cs',
    'Manager\Core\Services\NexusRateLimitTracker.cs',
    'Manager\Core\Services\NexusGraphQlClient.cs',
    'Manager\Core\Services\NexusV3RestClient.cs',
    'Manager\Core\Models\NexusOAuthTokenSet.cs',
    'Manager\Core\Services\NexusOAuthConfiguration.cs',
    'Manager\Core\Services\NexusOAuthProtocol.cs',
    'Manager\Core\Services\NexusOAuthClient.cs',
    'tools\NexusHttpBehaviorHarness.cs'
) | ForEach-Object { Join-Path $RepoRoot $_ }

try {
    & $compiler /nologo /nowarn:0649 /target:exe /out:$output /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll $sources
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & $output
    exit $LASTEXITCODE
}
finally {
    Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue
}
