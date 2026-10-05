param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ManagerExe = ''
)

# Behavioural tests for the desktop manager: load order fixing, undo,
# profiles, shared mod lists, local installs, crash reports, ZIP output and
# Content Workshop IDs. The harness drives a built Manager.exe, so build the
# manager first (or pass -ManagerExe to test another build). Nothing here
# touches the network, a game install or the user's Documents folder.

$ErrorActionPreference = 'Stop'
$compiler = Join-Path ${env:WINDIR} 'Microsoft.NET\Framework\v3.5\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The .NET Framework 3.5 C# compiler was not found at $compiler."
}
if (-not $ManagerExe) {
    $ManagerExe = Join-Path $RepoRoot 'Dist\SMM\Manager.exe'
}
if (-not (Test-Path -LiteralPath $ManagerExe)) {
    throw "Manager.exe was not found at $ManagerExe. Build the manager first or pass -ManagerExe."
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('ModManagerBehavior-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    # A private copy, so a running manager never locks the file under test.
    $manager = Join-Path $work 'Manager.exe'
    Copy-Item -LiteralPath $ManagerExe -Destination $manager
    $output = Join-Path $work 'ModManagerBehavior.exe'
    & $compiler /nologo /target:exe /out:$output "/r:$manager" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll (Join-Path $RepoRoot 'tools\ModManagerBehaviorHarness.cs')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $samples = Join-Path $work 'samples'
    & $output $samples
    $harnessExit = $LASTEXITCODE

    # Read the manager's ZIPs with a reader that shares no code with it.
    $independentFailures = 0
    try {
        Add-Type -AssemblyName System.IO.Compression
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $checks = @(
            @{ Zip = 'deflated.zip'; Entry = 'logs/big.log'; Expected = [IO.File]::ReadAllText((Join-Path $samples 'expected-big.log')) },
            @{ Zip = 'deflated.zip'; Entry = 'empty.txt'; Expected = '' },
            @{ Zip = 'deflated.zip'; Entry = ([string][char]0x00FC + 'nic' + [char]0x00F8 + 'de/' + [char]0x6587 + '.txt'); Expected = 'unicode' },
            @{ Zip = 'stored.zip'; Entry = 'About/About.json'; Expected = $null }
        )
        foreach ($check in $checks) {
            $archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $samples $check.Zip))
            try {
                $entry = $archive.GetEntry($check.Entry)
                if ($null -eq $entry) { throw "missing entry $($check.Entry)" }
                $reader = New-Object IO.StreamReader($entry.Open(), (New-Object Text.UTF8Encoding($false)))
                try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
                if ($null -ne $check.Expected -and $text -ne $check.Expected) { throw "content differs for $($check.Entry)" }
                if ($null -eq $check.Expected -and $text -notmatch 'tester\.sample') { throw "content differs for $($check.Entry)" }
                Write-Output ("PASS independent reader opens {0}!{1}" -f $check.Zip, $check.Entry)
            }
            catch {
                $independentFailures++
                Write-Output ("FAIL independent reader {0}: {1}" -f $check.Zip, $_.Exception.Message)
            }
            finally {
                $archive.Dispose()
            }
        }
    }
    catch {
        Write-Output ("SKIP independent ZIP check: System.IO.Compression is unavailable ({0})" -f $_.Exception.Message)
    }

    if ($harnessExit -ne 0) { exit $harnessExit }
    if ($independentFailures -gt 0) { exit 1 }
    exit 0
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
