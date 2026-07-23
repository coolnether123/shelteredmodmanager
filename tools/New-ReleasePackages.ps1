param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrEmpty($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot 'artifacts\release-packages'
}
$stageRoot = Join-Path $OutputRoot 'stage'
$expectedPrefix = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force }

function Get-PEArchitecture {
    param([string]$Path)
    $bytes = [IO.File]::ReadAllBytes($Path)
    $peOffset = [BitConverter]::ToInt32($bytes, 60)
    $machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
    switch ($machine) {
        0x14c  { return 'x86' }
        0x8664 { return 'x64' }
        default { return ('unknown-0x{0:X}' -f $machine) }
    }
}

function Assert-PEArchitecture {
    param([string]$Path, [string]$Expected)
    $actual = Get-PEArchitecture -Path $Path
    if ($actual -ne $Expected) {
        throw "Architecture mismatch for '$Path': expected $Expected, found $actual."
    }
}

if (-not $SkipBuild) {
    $msbuildCandidates = @(
        'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe'
    )
    $msbuild = $msbuildCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $msbuild) { throw 'Visual Studio MSBuild was not found.' }
    & $msbuild (Join-Path $repoRoot 'ShelteredModManager.sln') /t:Rebuild /restore "/p:Configuration=$Configuration" /m /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
}

if (-not $SkipTests) {
    & (Join-Path $PSScriptRoot 'Test-NexusManagerContracts.ps1') -RepoRoot $repoRoot
    if ($LASTEXITCODE -ne 0) { throw "Nexus contract checks failed ($LASTEXITCODE)." }
    & (Join-Path $PSScriptRoot 'Test-NexusOAuthContracts.ps1') -RepoRoot $repoRoot
    if ($LASTEXITCODE -ne 0) { throw "Nexus OAuth contract checks failed ($LASTEXITCODE)." }
    & (Join-Path $PSScriptRoot 'Test-ManagerSelfUpdate.ps1') -RepoRoot $repoRoot
    if ($LASTEXITCODE -ne 0) { throw "Manager self-update tests failed ($LASTEXITCODE)." }
}

$distSmm = Join-Path $repoRoot 'Dist\SMM'
$managerExe = Join-Path $distSmm 'Manager.exe'
if (-not (Test-Path -LiteralPath $managerExe)) { throw "Missing build output: $managerExe" }
$fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($managerExe).FileVersion
$parsed = [Version]$fileVersion
$version = '{0}.{1}.{2}' -f $parsed.Major, $parsed.Minor, $parsed.Build
Write-Host "Manager.exe file version: $fileVersion -> package version $version"

# Exact SMM payload whitelist. Anything in Dist\SMM that is not listed here is
# rejected from packaging and reported, so local state or stray build junk can
# never ship.
$smmWhitelist = @(
    'Manager.exe',
    'ManagerUpdater.exe',
    'ModAPI.dll',
    'ModAPI.xml',
    'bin\0Harmony.dll',
    'bin\Doorstop.dll',
    'bin\ShelteredAPI.dll',
    'bin\ShelteredAPI.xml',
    'Doorstop\x86\winhttp.dll',
    'Doorstop\x64\winhttp.dll'
)
$contractRequired = @(
    'Manager.exe','ManagerUpdater.exe','ModAPI.dll',
    'bin\ShelteredAPI.dll','bin\Doorstop.dll','bin\0Harmony.dll',
    'Doorstop\x86\winhttp.dll','Doorstop\x64\winhttp.dll'
)

$smmStage = Join-Path $stageRoot 'SMM'
$excluded = New-Object System.Collections.Generic.List[string]
$allDistFiles = Get-ChildItem -LiteralPath $distSmm -Recurse -File
foreach ($file in $allDistFiles) {
    $relative = $file.FullName.Substring($distSmm.Length + 1)
    if ($smmWhitelist -contains $relative) {
        $target = Join-Path $smmStage $relative
        $targetDir = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
    else {
        $excluded.Add($relative)
    }
}
# 0Harmony's canonical source is the tracked shared-runtime mod package; the
# Manager post-build does not stage it into Dist\SMM\bin, so stage it here.
$harmonySource = Join-Path $repoRoot 'mods\0Harmony\Assemblies\0Harmony.dll'
$harmonyTarget = Join-Path $smmStage 'bin\0Harmony.dll'
if (-not (Test-Path -LiteralPath $harmonyTarget)) {
    if (-not (Test-Path -LiteralPath $harmonySource)) { throw "0Harmony.dll not found at $harmonySource" }
    Copy-Item -LiteralPath $harmonySource -Destination $harmonyTarget
    Write-Host "Staged 0Harmony.dll from tracked mods package."
}
foreach ($required in $contractRequired) {
    if (-not (Test-Path -LiteralPath (Join-Path $smmStage $required))) {
        throw "Package contract violation: required file '$required' missing from staged SMM."
    }
}
$unexpectedBinaries = $excluded | Where-Object { $_ -match '\.(exe|dll)$' -and $_ -notmatch '\.pdb$' -and $_ -notmatch '^bin\\(decompiler|_nxm_inbox)\\' }
Write-Host ("Excluded {0} non-whitelisted files from Dist\SMM." -f $excluded.Count)
$excluded | ForEach-Object { Write-Host "  excluded: $_" }
if ($unexpectedBinaries.Count -gt 0) {
    throw "Unexpected executable content in Dist\SMM (investigate before packaging): $($unexpectedBinaries -join ', ')"
}

# Native doorstop proxies come only from libs\x86 and libs\x64 (never libs root),
# and their PE headers are verified rather than trusting folder names.
$proxyX86 = Join-Path $repoRoot 'libs\x86\winhttp.dll'
$proxyX64 = Join-Path $repoRoot 'libs\x64\winhttp.dll'
$doorstopConfig = Join-Path $repoRoot 'libs\doorstop_config.ini'
foreach ($p in @($proxyX86, $proxyX64, $doorstopConfig)) {
    if (-not (Test-Path -LiteralPath $p)) { throw "Missing packaging input: $p" }
}
Assert-PEArchitecture -Path $proxyX86 -Expected 'x86'
Assert-PEArchitecture -Path $proxyX64 -Expected 'x64'
Assert-PEArchitecture -Path (Join-Path $smmStage 'Doorstop\x86\winhttp.dll') -Expected 'x86'
Assert-PEArchitecture -Path (Join-Path $smmStage 'Doorstop\x64\winhttp.dll') -Expected 'x64'

$packages = @(
    @{ Name = "ShelteredModManager-$version-Steam-GOG-x86.zip"; Proxy = $proxyX86; ProxyArch = 'x86'; Kind = 'first-install'; Storefronts = @('Steam','GOG') },
    @{ Name = "ShelteredModManager-$version-Epic-x64.zip";      Proxy = $proxyX64; ProxyArch = 'x64'; Kind = 'first-install'; Storefronts = @('Epic') },
    @{ Name = "ShelteredModManager-$version-Update.zip";        Proxy = $null;      ProxyArch = 'neutral'; Kind = 'update'; Storefronts = @('Steam','GOG','Epic') }
)

$commit = (& git -C $repoRoot rev-parse HEAD 2>$null)
$dirty = (& git -C $repoRoot status --porcelain 2>$null | Measure-Object).Count -gt 0
$manifest = New-Object System.Collections.Generic.List[object]

foreach ($pkg in $packages) {
    $pkgStage = Join-Path $stageRoot ([IO.Path]::GetFileNameWithoutExtension($pkg.Name))
    New-Item -ItemType Directory -Path $pkgStage -Force | Out-Null
    Copy-Item -LiteralPath $smmStage -Destination (Join-Path $pkgStage 'SMM') -Recurse
    if ($pkg.Kind -eq 'first-install') {
        Copy-Item -LiteralPath $pkg.Proxy -Destination (Join-Path $pkgStage 'winhttp.dll')
        Copy-Item -LiteralPath $doorstopConfig -Destination (Join-Path $pkgStage 'doorstop_config.ini')
        Assert-PEArchitecture -Path (Join-Path $pkgStage 'winhttp.dll') -Expected $pkg.ProxyArch
    }
    $zipPath = Join-Path $OutputRoot $pkg.Name
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $pkgStage '*') -DestinationPath $zipPath -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    Set-Content -LiteralPath ($zipPath + '.sha256') -Value "$hash  $($pkg.Name)" -Encoding ASCII
    $size = (Get-Item -LiteralPath $zipPath).Length
    $manifest.Add([pscustomobject]@{
        project        = 'ShelteredModManager'
        version        = $version
        kind           = $pkg.Kind
        architecture   = $pkg.ProxyArch
        storefronts    = $pkg.Storefronts
        filename       = $pkg.Name
        bytes          = $size
        sha256         = $hash
        commit         = $commit
        builtFromDirtyTree = $dirty
        configuration  = $Configuration
        builtUtc       = [DateTime]::UtcNow.ToString('o')
    })
    Write-Host ("{0}  {1} bytes  sha256 {2}" -f $pkg.Name, $size, $hash)
}

$manifestPath = Join-Path $OutputRoot 'release-manifest.fragment.json'
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Host "Manifest fragment: $manifestPath"
Write-Host 'Release packaging complete.'
