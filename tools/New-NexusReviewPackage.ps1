param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\nexus-review'))
$stageRoot = [IO.Path]::GetFullPath((Join-Path $artifactRoot 'stage'))
$expectedPrefix = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $stageRoot.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean review stage outside the repository artifacts folder: $stageRoot"
}

$msbuildCandidates = @(
    'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe'
)
$msbuild = $msbuildCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio MSBuild was not found.' }

& $msbuild (Join-Path $repoRoot 'ShelteredModManager.sln') /t:Rebuild "/p:Configuration=$Configuration" /m /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE." }

& (Join-Path $PSScriptRoot 'Test-NexusManagerContracts.ps1') -RepoRoot $repoRoot
if ($LASTEXITCODE -ne 0) { throw "Nexus contract checks failed with exit code $LASTEXITCODE." }
& (Join-Path $PSScriptRoot 'Test-ManagerSelfUpdate.ps1') -RepoRoot $repoRoot
if ($LASTEXITCODE -ne 0) { throw "Manager self-update tests failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'Dist\SMM\ManagerUpdater.exe'))) {
    throw 'Release output is missing ManagerUpdater.exe.'
}

if (Test-Path -LiteralPath $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force }
New-Item -ItemType Directory -Path $stageRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'Dist\SMM') -Destination (Join-Path $stageRoot 'SMM') -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'documentation\Nexus_Registration_Submission.md') -Destination (Join-Path $stageRoot 'NEXUS_REVIEW.md')

$smmStage = Join-Path $stageRoot 'SMM'
Get-ChildItem -LiteralPath $smmStage -Recurse -File | Where-Object {
    $_.Extension -in @('.pdb', '.log', '.nxm') -or
    $_.Name -in @('mod_manager.ini', 'manager_options.json')
} | Remove-Item -Force
@('_nxm_inbox','_smm_temp','_smm_backup') | ForEach-Object {
    $path = Join-Path (Join-Path $smmStage 'bin') $_
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}

$zipPath = Join-Path $artifactRoot 'Sheltered-Mod-Manager-2.0.0-Nexus-Review.zip'
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path (Join-Path $stageRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Set-Content -LiteralPath ($zipPath + '.sha256') -Value "$hash  $([IO.Path]::GetFileName($zipPath))" -Encoding ASCII

Write-Host "Nexus review package: $zipPath"
Write-Host "SHA256: $hash"
