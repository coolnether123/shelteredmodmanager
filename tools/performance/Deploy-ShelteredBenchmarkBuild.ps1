#requires -Version 5.1
<#
.SYNOPSIS
Builds one Sheltered Mod Manager stack and deploys byte-identical artifacts to benchmark installs.
.DESCRIPTION
The benchmark runner calls this once while it owns both install mutexes. The runtime libraries and harness
are compiled once against the Steam managed surface (the older compatibility floor), then the exact same
artifacts are copied to every selected storefront.
Machine-local manager options and credentials are preserved byte-for-byte.
#>
[CmdletBinding()]
param(
    [string[]]$Platform = @('steam', 'epic'),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipManagerBuild,
    [switch]$SkipHarnessBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$harnessRoot = 'A:\Dev\Projects\ShelteredAgentInterface'
$harnessProject = Join-Path $harnessRoot 'ShelteredAgentInterface\ShelteredAgentInterface.csproj'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msbuild)) { $msbuild = (Get-Command msbuild.exe -ErrorAction Stop).Source }

$targets = @{
    steam = [pscustomobject]@{
        Name = 'steam'; Root = 'A:\SteamLibrary\steamapps\common\Sheltered'; ProcessName = 'Sheltered'
        ManagedRoot = 'A:\SteamLibrary\steamapps\common\Sheltered\Sheltered_Data\Managed'
        Architecture = 'x86'; AgentPort = 37421
    }
    epic = [pscustomobject]@{
        Name = 'epic'; Root = 'D:\Epic Games Games\Sheltered'; ProcessName = 'ShelteredWindows64_EOS'
        ManagedRoot = 'D:\Epic Games Games\Sheltered\ShelteredWindows64_EOS_Data\Managed'
        Architecture = 'x64'; AgentPort = 37422
    }
}
$platformNames = @($Platform | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
$unknownPlatforms = @($platformNames | Where-Object { -not $targets.ContainsKey($_) })
if ($unknownPlatforms.Count -gt 0) { throw "Unknown platform(s): $($unknownPlatforms -join ', ')" }
$selectedTargets = @($platformNames | Select-Object -Unique | ForEach-Object { $targets[$_] })
$forbiddenRuntimeAssemblyNames = @(
    'mscorlib',
    'System',
    'netstandard',
    'Microsoft.CSharp',
    'Mono.Security',
    'UnityEngine',
    'Assembly-CSharp',
    'Assembly-CSharp-firstpass',
    'Pathfinding.ClipperLib',
    'Pathfinding.Poly2Tri'
)
$removedForbiddenArtifacts = New-Object 'System.Collections.Generic.List[string]'

function Test-ForbiddenRuntimeAssemblyName {
    param([Parameter(Mandatory = $true)][string]$SimpleName)

    foreach ($name in $forbiddenRuntimeAssemblyNames) {
        if ([string]::Equals($name, $SimpleName, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }

    return $SimpleName.StartsWith('System.', [StringComparison]::OrdinalIgnoreCase) `
        -or $SimpleName.StartsWith('UnityEngine.', [StringComparison]::OrdinalIgnoreCase) `
        -or $SimpleName.StartsWith('Assembly-CSharp-', [StringComparison]::OrdinalIgnoreCase)
}

function Remove-ForbiddenRuntimeAssemblies {
    param([Parameter(Mandatory = $true)][string]$Directory)

    $fullDirectory = [IO.Path]::GetFullPath($Directory)
    if (-not (Test-Path -LiteralPath $fullDirectory -PathType Container)) { return }

    foreach ($file in @(Get-ChildItem -LiteralPath $fullDirectory -Filter '*.dll' -File)) {
        if (-not (Test-ForbiddenRuntimeAssemblyName $file.BaseName)) { continue }
        Remove-Item -LiteralPath $file.FullName -Force
        $removedForbiddenArtifacts.Add($file.FullName)
    }
}

foreach ($target in $selectedTargets) {
    if (-not (Test-Path -LiteralPath $target.Root -PathType Container)) { throw "Missing $($target.Name) install: $($target.Root)" }
    if (-not (Test-Path -LiteralPath (Join-Path $target.ManagedRoot 'Assembly-CSharp.dll') -PathType Leaf)) {
        throw "Missing $($target.Name) managed game assemblies: $($target.ManagedRoot)"
    }
    $active = @(Get-Process -Name $target.ProcessName -ErrorAction SilentlyContinue)
    if ($active.Count -gt 0) { throw "Refusing to deploy while $($target.Name) process(es) are active: $($active.Id -join ', ')" }
}

if (-not $SkipManagerBuild) {
    $compatibilityTarget = $targets.steam
    & $msbuild (Join-Path $repoRoot 'ShelteredModManager.sln') /t:Rebuild "/p:Configuration=$Configuration" '/p:Platform=Any CPU' /m /v:minimal `
        "/p:ShelteredInstallDir=$($compatibilityTarget.Root)\" `
        "/p:ShelteredManagedDir=$($compatibilityTarget.ManagedRoot)\"
    if ($LASTEXITCODE -ne 0) { throw "SMM rebuild failed with exit code $LASTEXITCODE." }
}

$distSmm = Join-Path $repoRoot 'Dist\SMM'
Remove-ForbiddenRuntimeAssemblies $distSmm
Remove-ForbiddenRuntimeAssemblies (Join-Path $distSmm 'bin')

$modApiOutput = Join-Path $repoRoot "ModAPI\obj\$Configuration\ModAPI.dll"
$shelteredApiOutput = Join-Path $repoRoot "ShelteredAPI\obj\$Configuration\ShelteredAPI.dll"
foreach ($required in @($distSmm, $modApiOutput, $shelteredApiOutput, $harnessProject)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required build input is missing: $required" }
}

if (-not $SkipHarnessBuild) {
    $compatibilityTarget = $targets.steam
    & $msbuild $harnessProject /t:Rebuild "/p:Configuration=$Configuration" /p:Platform=AnyCPU /v:minimal `
        "/p:ShelteredManagedRoot=$($compatibilityTarget.ManagedRoot)\" `
        "/p:ShelteredSmmRoot=$distSmm\bin\" `
        "/p:ShelteredModApiRoot=$(Split-Path -Parent $modApiOutput)\" `
        "/p:ShelteredApiRoot=$(Split-Path -Parent $shelteredApiOutput)\"
    if ($LASTEXITCODE -ne 0) { throw "Agent harness rebuild failed with exit code $LASTEXITCODE." }
}

$harnessOutput = Join-Path $harnessRoot "ShelteredAgentInterface\obj\$Configuration\Sheltered Agent Interface.dll"
if (-not (Test-Path -LiteralPath $harnessOutput -PathType Leaf)) { throw "Harness output is missing: $harnessOutput" }

function Copy-VerifiedArtifact {
    param([Parameter(Mandatory = $true)][string]$Source, [Parameter(Mandatory = $true)][string]$Destination)
    $destinationDirectory = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $destinationDirectory)) { New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null }
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
    $sourceHash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash
    $destinationHash = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
    if ($sourceHash -ne $destinationHash) { throw "Deployment hash mismatch: $Source -> $Destination" }
    [pscustomobject]@{ Source = $Source; Destination = $Destination; Sha256 = $sourceHash }
}

$deploymentRows = New-Object 'System.Collections.Generic.List[object]'
foreach ($target in $selectedTargets) {
    $targetSmm = Join-Path $target.Root 'SMM'
    if (-not (Test-Path -LiteralPath $targetSmm)) { New-Item -ItemType Directory -Path $targetSmm -Force | Out-Null }
    Remove-ForbiddenRuntimeAssemblies $targetSmm
    Remove-ForbiddenRuntimeAssemblies (Join-Path $targetSmm 'bin')

    $localSettings = @{}
    foreach ($relativePath in @('bin\mod_manager.ini', 'bin\manager_options.json')) {
        $settingsPath = Join-Path $targetSmm $relativePath
        if (Test-Path -LiteralPath $settingsPath -PathType Leaf) { $localSettings[$settingsPath] = [IO.File]::ReadAllBytes($settingsPath) }
    }
    Copy-Item -Path (Join-Path $distSmm '*') -Destination $targetSmm -Recurse -Force
    Remove-ForbiddenRuntimeAssemblies $targetSmm
    Remove-ForbiddenRuntimeAssemblies (Join-Path $targetSmm 'bin')
    foreach ($settingsPath in $localSettings.Keys) { [IO.File]::WriteAllBytes($settingsPath, $localSettings[$settingsPath]) }

    $deploymentRows.Add((Copy-VerifiedArtifact $modApiOutput (Join-Path $targetSmm 'ModAPI.dll')))
    $deploymentRows.Add((Copy-VerifiedArtifact $shelteredApiOutput (Join-Path $targetSmm 'bin\ShelteredAPI.dll')))
    foreach ($pdb in @([IO.Path]::ChangeExtension($modApiOutput, '.pdb'), [IO.Path]::ChangeExtension($shelteredApiOutput, '.pdb'))) {
        if (Test-Path -LiteralPath $pdb) {
            $relative = if ($pdb -like '*ShelteredAPI*') { 'bin\ShelteredAPI.pdb' } else { 'ModAPI.pdb' }
            $deploymentRows.Add((Copy-VerifiedArtifact $pdb (Join-Path $targetSmm $relative)))
        }
    }

    $harnessDirectory = Join-Path $target.Root 'mods\Sheltered Agent Interface\Assemblies'
    $deploymentRows.Add((Copy-VerifiedArtifact $harnessOutput (Join-Path $harnessDirectory 'Sheltered Agent Interface.dll')))
    $harnessPdb = [IO.Path]::ChangeExtension($harnessOutput, '.pdb')
    if (Test-Path -LiteralPath $harnessPdb) { $deploymentRows.Add((Copy-VerifiedArtifact $harnessPdb (Join-Path $harnessDirectory 'Sheltered Agent Interface.pdb'))) }
    Set-Content -LiteralPath (Join-Path (Split-Path -Parent $harnessDirectory) 'agent-port.txt') -Value $target.AgentPort -Encoding Ascii -NoNewline

    $deploymentRows.Add((Copy-VerifiedArtifact (Join-Path $repoRoot "libs\$($target.Architecture)\winhttp.dll") (Join-Path $target.Root 'winhttp.dll')))
    $deploymentRows.Add((Copy-VerifiedArtifact (Join-Path $repoRoot 'libs\doorstop_config.ini') (Join-Path $target.Root 'doorstop_config.ini')))
}

[pscustomobject]@{
    BuiltAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    Configuration = $Configuration
    HarnessCompatibilityFloor = 'steam-x86-unity-5.3.3'
    Platforms = @($selectedTargets.Name)
    RemovedForbiddenRuntimeArtifacts = $removedForbiddenArtifacts.ToArray()
    Artifacts = $deploymentRows.ToArray()
} | ConvertTo-Json -Depth 6
