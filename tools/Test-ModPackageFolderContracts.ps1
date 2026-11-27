param(
    [string]$WorkRoot = [IO.Path]::GetTempPath()
)

$ErrorActionPreference = 'Stop'
$toolsRoot = $PSScriptRoot
$source = Join-Path $toolsRoot 'release-orchestration'
$previous = Get-Content -LiteralPath (Join-Path $source 'previous-mod-folders.json') -Raw | ConvertFrom-Json
# Literal top-level folders from the eleven r3 ZIPs.
$r3 = @{
    'Coolnether123.FourPersonExpeditions' = 'Four Person Expeditions'
    'Coolnether123.TradingAmount' = 'Trading Amount'
    'coolnether123.betteraiqueue' = 'Better AI Queue'
    'coolnether123.Lifespan' = 'Lifespan'
    'coolnether123.BunkerRandomLocation' = 'Bunker Random Location'
    'com.procreation.framework' = 'Family Expansion'
    'coolnether123.deepexpansion' = 'Deep Expansion'
    'Coolnether123.ShelteredVanillaFixes' = 'Sheltered Vanilla Fixes'
    'Coolnether123.ShelteredSystemsExpansion' = 'Shelter Systems Expansion'
    'expandedmapsizes' = 'Expanded Map Sizes'
    'Coolnether123.ShelteredDisplayFixes' = 'Sheltered Display Fixes'
}
if (@($previous.PSObject.Properties).Count -ne $r3.Count) { throw 'Expected exactly eleven frozen r3 roots.' }
foreach ($id in $r3.Keys) {
    if ($previous.$id -cne $r3[$id]) { throw "Frozen r3 root changed for $id. Expected '$($r3[$id])'." }
}
Write-Output 'PASS frozen table has all eleven literal r3 roots'

$fixture = Join-Path (Resolve-Path -LiteralPath $WorkRoot).Path ('package-folder-contracts-' + [Guid]::NewGuid().ToString('N'))
$fixtureTools = Join-Path $fixture 'tools'
$fixtureSource = Join-Path $fixtureTools 'release-orchestration'
New-Item -ItemType Directory -Path $fixtureSource -Force | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $toolsRoot 'ReleaseZip.ps1') -Destination $fixtureTools
    $packager = Join-Path $fixtureSource 'New-ModPackages.ps1'
    Copy-Item -LiteralPath (Join-Path $source 'New-ModPackages.ps1') -Destination $packager
    $projects = [ordered]@{
        ExpeditionFour = 'Coolnether123.FourPersonExpeditions'
        BunkerRandomLocation = 'Coolnether123.BunkerRandomLocation'
    }
    foreach ($project in $projects.Keys) {
        $id = $projects[$project]
        $about = New-Item -ItemType Directory -Path (Join-Path $fixture "$project\About") -Force
        @{
            id = $id; name = $project; version = '1.0.0'
            requiredModApiVersion = '2.0'; requiredShelteredApiVersion = '2.0'
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $about.FullName 'About.json')
        $previous.$id = "Old $project"
    }
    $previous | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixtureSource 'previous-mod-folders.json')

    $runner = Join-Path $fixture 'Run.ps1'
    @'
param([string]$Overrides)
$arguments = @{
    ShelteredRoot = $PSScriptRoot
    Projects = 'ExpeditionFour,BunkerRandomLocation'
    DryRun = $true
}
if ($Overrides) { $arguments.AllowFolderChange = $Overrides.Split(',') }
& (Join-Path $PSScriptRoot 'tools\release-orchestration\New-ModPackages.ps1') @arguments
exit $LASTEXITCODE
'@ | Set-Content -LiteralPath $runner
    $powershell = Join-Path $PSHOME 'powershell.exe'

    $log = & $powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File $runner
    if ($LASTEXITCODE -ne 1 -or ($log -join "`n") -notmatch 'FAIL ExpeditionFour' -or
        ($log -join "`n") -notmatch 'FAIL BunkerRandomLocation') { throw "Both mismatches must fail. $log" }
    Write-Output 'PASS folder mismatches fail by default'

    $log = & $powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File $runner -Overrides ExpeditionFour
    if ($LASTEXITCODE -ne 1 -or ($log -join "`n") -notmatch 'WARNING: Folder change for .ExpeditionFour.' -or
        ($log -join "`n") -notmatch 'FAIL BunkerRandomLocation') { throw "A named override must leave other mismatches blocked. $log" }
    Write-Output 'PASS one override warns and leaves the other mismatch blocked'

    $log = & $powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File $runner -Overrides 'ExpeditionFour,BunkerRandomLocation'
    if ($LASTEXITCODE -ne 0 -or ($log -join "`n") -notmatch 'WARNING: Folder change for .ExpeditionFour.' -or
        ($log -join "`n") -notmatch 'WARNING: Folder change for .BunkerRandomLocation.') { throw "Each overridden mismatch must warn. $log" }
    Write-Output 'PASS a list of overrides warns for every mismatch'
}
finally {
    # The root is resolved above and the only child removed is this run's GUID directory.
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
