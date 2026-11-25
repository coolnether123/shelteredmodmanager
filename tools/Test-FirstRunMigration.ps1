[CmdletBinding()]
param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $repo 'out\first-run-tests'
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $output | Out-Null
$env:TEMP = $output
$env:TMP = $output
$compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
$harmony = Join-Path $repo 'mods\0Harmony\Assemblies\0Harmony.dll'
Copy-Item -LiteralPath $harmony -Destination $output
$sources = @(
    'Shared\FirstRun\FirstRunFiles.cs'
    'Shared\FirstRun\PreUpgradeSaveBackup.cs'
    'Manager\Core\Services\LegacyInstallMigration.cs'
    'ShelteredAPI\Core\FirstRunSaveGuard.cs'
    'tools\FirstRunMigration.ContractFixture.cs'
) | ForEach-Object { Join-Path $repo $_ }
$fixture = Join-Path $output 'FirstRunMigrationTests.dll'
$compileLog = Join-Path $output 'compile.log'
$testLog = Join-Path $output 'tests.log'
if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture }
& $compiler /nologo /warnaserror /target:library "/out:$fixture" "/reference:$harmony" $sources *> $compileLog
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $fixture -PathType Leaf)) {
    Get-Content -LiteralPath $compileLog
    throw 'The first-run fixture did not compile.'
}

$null = [Reflection.Assembly]::LoadFrom((Join-Path $output '0Harmony.dll'))
$null = [Reflection.Assembly]::LoadFrom($fixture)
$report = New-Object IO.StreamWriter($testLog)
try {
    $testExit = [FirstRunMigrationFixture]::Run($output, $report)
} finally {
    $report.Dispose()
    Get-Content -LiteralPath $testLog
}
exit $testExit
