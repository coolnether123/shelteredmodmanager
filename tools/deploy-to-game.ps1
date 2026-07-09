[CmdletBinding()]
param(
    [string]$GameRoot = 'D:\Epic Games Games\Sheltered',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipBuild,
    [switch]$SkipHarness
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$gameRoot = [IO.Path]::GetFullPath($GameRoot)
$epicExe = Join-Path $gameRoot 'ShelteredWindows64_EOS.exe'
$steamExe = Join-Path $gameRoot 'Sheltered.exe'

if (Test-Path $epicExe) {
    $gameExe = $epicExe
    $managedDir = Join-Path $gameRoot 'ShelteredWindows64_EOS_Data\Managed'
    $proxyArchitecture = 'x64'
}
elseif (Test-Path $steamExe) {
    $gameExe = $steamExe
    $managedDir = Join-Path $gameRoot 'Sheltered_Data\Managed'
    $proxyArchitecture = 'x86'
}
else {
    throw "No supported Sheltered executable was found in '$gameRoot'."
}

if (-not (Test-Path (Join-Path $managedDir 'Assembly-CSharp.dll'))) {
    throw "Sheltered managed assemblies were not found at '$managedDir'."
}

$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $msbuild)) {
    $msbuild = (Get-Command msbuild.exe -ErrorAction Stop).Source
}

$previousInstallDir = $env:ShelteredInstallDir
$previousManagedDir = $env:ShelteredManagedDir
try {
    $env:ShelteredInstallDir = "$gameRoot\"
    $env:ShelteredManagedDir = "$managedDir\"

    if (-not $SkipBuild) {
        & $msbuild (Join-Path $repoRoot 'ShelteredModManager.sln') /t:Build "/p:Configuration=$Configuration" '/p:Platform=Any CPU' /v:minimal
        if ($LASTEXITCODE -ne 0) { throw "SMM build failed with exit code $LASTEXITCODE." }
    }

    $distRoot = Join-Path $repoRoot 'Dist'
    $distSmm = Join-Path $distRoot 'SMM'
    # Mod packages live under the repository's versioned mods directory. The
    # Manager build may create an empty Dist\mods staging directory when no
    # optional plugin project is present, so do not deploy from that staging
    # directory and silently omit the plugin set.
    $sourceMods = Join-Path $repoRoot 'mods'
    foreach ($source in @($distSmm, $sourceMods)) {
        if (-not (Test-Path $source)) { throw "Build output is missing: '$source'." }
    }

    $targetSmm = Join-Path $gameRoot 'SMM'
    $targetMods = Join-Path $gameRoot 'mods'
    foreach ($target in @($targetSmm, $targetMods)) {
        if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target -Force | Out-Null }
    }
    Copy-Item -Path (Join-Path $distSmm '*') -Destination $targetSmm -Recurse -Force
    Copy-Item -Path (Join-Path $sourceMods '*') -Destination $targetMods -Recurse -Force
    Copy-Item -Path (Join-Path $repoRoot "libs\$proxyArchitecture\winhttp.dll") -Destination (Join-Path $gameRoot 'winhttp.dll') -Force
    Copy-Item -Path (Join-Path $repoRoot 'libs\doorstop_config.ini') -Destination (Join-Path $gameRoot 'doorstop_config.ini') -Force

    if (-not $SkipHarness) {
        $harnessRoot = 'A:\Dev\Projects\ShelteredAgentInterface'
        $harnessProject = Join-Path $harnessRoot 'ShelteredAgentInterface\ShelteredAgentInterface.csproj'
        if (Test-Path $harnessProject) {
            $env:ShelteredManagedRoot = "$managedDir\"
            $env:ShelteredSmmRoot = Join-Path $gameRoot 'SMM\bin\'
            $env:ShelteredModApiRoot = Join-Path $gameRoot 'SMM\'
            $env:ShelteredApiRoot = Join-Path $gameRoot 'SMM\bin\'
            & $msbuild $harnessProject /t:Build "/p:Configuration=$Configuration" /p:Platform=AnyCPU /v:minimal
            if ($LASTEXITCODE -ne 0) { throw "Harness build failed with exit code $LASTEXITCODE." }

            $harnessOutput = Join-Path $harnessRoot 'Assemblies\Sheltered Agent Interface.dll'
            $harnessDestination = Join-Path $gameRoot 'mods\Sheltered Agent Interface\Assemblies'
            if (-not (Test-Path $harnessDestination)) { New-Item -ItemType Directory -Path $harnessDestination -Force | Out-Null }
            Copy-Item -Path $harnessOutput -Destination $harnessDestination -Force
            $harnessPdb = [IO.Path]::ChangeExtension($harnessOutput, '.pdb')
            if (Test-Path $harnessPdb) { Copy-Item -Path $harnessPdb -Destination $harnessDestination -Force }

            $harnessAbout = Join-Path $harnessRoot 'About\About.json'
            $harnessAboutDestination = Join-Path $gameRoot 'mods\Sheltered Agent Interface\About'
            if (-not (Test-Path $harnessAboutDestination)) { New-Item -ItemType Directory -Path $harnessAboutDestination -Force | Out-Null }
            if (Test-Path $harnessAbout) { Copy-Item -Path $harnessAbout -Destination $harnessAboutDestination -Force }

            $loadOrderPath = Join-Path $gameRoot 'mods\loadorder.json'
            $loadOrder = Get-Content -Raw $loadOrderPath | ConvertFrom-Json
            $agentModId = 'coolnether123.shelteredagentinterface'
            # Keep legacy v1 plugin packages available for archival inspection but
            # do not activate them: they target the retired ModAPI v1 contract.
            # The live Epic stack is the current shared Harmony runtime plus the
            # rebuilt agent harness.
            $activeModIds = @('com.harmony.0harmony', $agentModId)
            if ((@($loadOrder.order) -join '|') -ne ($activeModIds -join '|')) {
                $loadOrder.order = $activeModIds
                $loadOrder | ConvertTo-Json -Depth 8 | Set-Content -Path $loadOrderPath -Encoding UTF8
            }
        }
        else {
            Write-Warning "Harness project was not found at '$harnessProject'; deployed SMM only."
        }
    }
}
finally {
    $env:ShelteredInstallDir = $previousInstallDir
    $env:ShelteredManagedDir = $previousManagedDir
}

Write-Host "Deployed $Configuration artifacts to '$gameRoot'."
Write-Host "Executable: $gameExe"
Write-Host "Doorstop proxy: $proxyArchitecture winhttp.dll"
