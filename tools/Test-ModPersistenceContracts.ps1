[CmdletBinding()]
param([string]$RepoRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}
$pluginManager = Get-Content -LiteralPath (Join-Path $RepoRoot 'ModAPI/Core/PluginManager.cs') -Raw
if ($pluginManager -notmatch '(?s)public void OnNewGame\(\)\s*\{\s*SaveSystemImpl\.ResetForNewGame\(\);\s*InvokeStaticLifecycleSubscribers\("OnNewGame"') {
    throw 'Registered data must reset before New Game callbacks initialize it.'
}
$paths = @(
    'ModAPI/Util/ManualJson.cs',
    'ModAPI/Util/AtomicTextFile.cs',
    'ModAPI/Core/ModPersistenceData.cs',
    'ModAPI/Core/ISaveRuntimeAdapter.cs',
    'ModAPI/Core/IGameLifecycleSource.cs',
    'ModAPI/Persistence/IModPersistenceLogic.cs',
    'ModAPI/Persistence/PersistenceFieldGraphSerializer.cs',
    'ModAPI/Persistence/ModPersistenceStore.cs',
    'ModAPI/Persistence/SaveSystemImpl.cs',
    'tools/fixtures/ModPersistenceContracts.cs'
)
$bodies = @($paths | ForEach-Object { Get-Content -LiteralPath (Join-Path $RepoRoot $_) -Raw })
$usings = @($bodies | ForEach-Object { [regex]::Matches($_, '(?m)^using [^;]+;') } | ForEach-Object { $_.Value } | Sort-Object -Unique)
$source = ($usings -join "`n") + "`n" + (($bodies | ForEach-Object { $_ -replace '(?m)^using [^;]+;', '' }) -join "`n")
Add-Type -TypeDefinition $source -Language CSharp
$testRoot = Join-Path $RepoRoot ('.persistence-contracts-' + [guid]::NewGuid().ToString('N'))
try {
    [ModPersistenceContracts]::Run($testRoot)
    Write-Output 'New Game dispatch order: 1 passed, 0 failed.'
} finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
