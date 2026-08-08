Set-StrictMode -Version 2.0

Import-Module (Join-Path $PSScriptRoot 'ShelteredBenchmark.Core.psm1') -Force -DisableNameChecking
Import-Module (Join-Path $PSScriptRoot 'ShelteredBenchmark.Harness.psm1') -Force -DisableNameChecking

function Join-BenchmarkIssue {
    [CmdletBinding()]
    param([string]$Existing, [Parameter(Mandatory = $true)][string]$Issue)
    if ([string]::IsNullOrWhiteSpace($Existing)) { return $Issue }
    return $Existing + [Environment]::NewLine + $Issue
}

function Start-BenchmarkProcessSampler {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$ProcessId,
        [Parameter(Mandatory = $true)][DateTimeOffset]$ProcessStartUtc,
        [int]$IntervalMilliseconds = 100
    )
    $state = [hashtable]::Synchronized(@{
        Stop = $false
        Phase = 'startup'
        Rows = New-Object System.Collections.ArrayList
        Ready = New-Object System.Threading.ManualResetEventSlim($false)
        InitializationError = ''
    })
    $script = {
        param($TargetPid, $StartedAt, $Interval, $Shared)
        while (-not $Shared.Stop) {
            try {
                $process = [Diagnostics.Process]::GetProcessById($TargetPid)
                if ($process.HasExited) {
                    $Shared.InitializationError = "Process $TargetPid exited before the sampler initialized."
                    if (-not $Shared.Ready.IsSet) { $Shared.Ready.Set() }
                    break
                }
                $now = [DateTimeOffset]::UtcNow
                $row = [pscustomobject]@{
                    TimestampUtc = $now.ToString('o')
                    ElapsedMs = [math]::Round(($now - $StartedAt).TotalMilliseconds, 1)
                    Phase = [string]$Shared.Phase
                    Pid = $TargetPid
                    CpuSeconds = [math]::Round($process.TotalProcessorTime.TotalSeconds, 4)
                    WorkingSetBytes = $process.WorkingSet64
                    PrivateBytes = $process.PrivateMemorySize64
                    Threads = $process.Threads.Count
                    Handles = $process.HandleCount
                    Responding = $process.Responding
                    MainWindowHandle = $process.MainWindowHandle.ToInt64()
                    MainWindowTitle = $process.MainWindowTitle
                }
                [void]$Shared.Rows.Add($row)
                if (-not $Shared.Ready.IsSet) { $Shared.Ready.Set() }
            }
            catch {
                $Shared.InitializationError = $_.Exception.Message
                if (-not $Shared.Ready.IsSet) { $Shared.Ready.Set() }
                break
            }
            Start-Sleep -Milliseconds $Interval
        }
    }
    $powershell = [PowerShell]::Create()
    [void]$powershell.AddScript($script).AddArgument($ProcessId).AddArgument($ProcessStartUtc).AddArgument($IntervalMilliseconds).AddArgument($state)
    $async = $null
    try {
        $async = $powershell.BeginInvoke()
        if (-not $state.Ready.Wait(2000)) { throw "Process sampler initialization timed out for PID $ProcessId." }
        if ($state.Rows.Count -eq 0) {
            $reason = if ([string]::IsNullOrWhiteSpace([string]$state.InitializationError)) { 'no process sample was produced' } else { [string]$state.InitializationError }
            throw "Process sampler initialization failed for PID $ProcessId`: $reason"
        }
        return [pscustomobject]@{ State = $state; PowerShell = $powershell; Async = $async }
    }
    catch {
        $state.Stop = $true
        if ($null -ne $async) {
            try { $powershell.Stop() } catch { }
            try { [void]$powershell.EndInvoke($async) } catch { }
        }
        $powershell.Dispose()
        $state.Ready.Dispose()
        throw
    }
}

function Stop-BenchmarkProcessSampler {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)]$Sampler)
    $Sampler.State.Stop = $true
    try { [void]$Sampler.PowerShell.EndInvoke($Sampler.Async) }
    finally {
        $Sampler.PowerShell.Dispose()
        $Sampler.State.Ready.Dispose()
    }
    return @($Sampler.State.Rows)
}

function Initialize-NativeCaptureType {
    if ('ShelteredBenchmark.NativeWindow' -as [type]) { return }
    Add-Type -ReferencedAssemblies 'System.Drawing' -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace ShelteredBenchmark {
    public static class NativeWindow {
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
        [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    }
}
'@
}

function New-NativeWindowBitmap {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][IntPtr]$Handle)
    Initialize-NativeCaptureType
    $rect = New-Object ShelteredBenchmark.NativeWindow+Rect
    if (-not [ShelteredBenchmark.NativeWindow]::GetWindowRect($Handle, [ref]$rect)) { throw 'GetWindowRect failed.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) { throw "Window has invalid dimensions ${width}x${height}." }
    $bitmap = New-Object Drawing.Bitmap $width, $height, ([Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $rendered = $false
    try {
        $device = $graphics.GetHdc()
        try {
            $rendered = [ShelteredBenchmark.NativeWindow]::PrintWindow($Handle, $device, 2)
        }
        finally { $graphics.ReleaseHdc($device) }
    }
    finally { $graphics.Dispose() }

    # Some Unity/DWM combinations report success for PW_RENDERFULLCONTENT but
    # return an entirely black surface. Retry with the classic PrintWindow mode
    # before rejecting the sample so a transient compositor state cannot poison
    # the complete readiness window.
    $samplePoints = @(
        @(0.1, 0.1), @(0.5, 0.1), @(0.9, 0.1),
        @(0.1, 0.5), @(0.5, 0.5), @(0.9, 0.5),
        @(0.1, 0.9), @(0.5, 0.9), @(0.9, 0.9)
    )
    $hasVisiblePixel = $false
    foreach ($point in $samplePoints) {
        $pixel = $bitmap.GetPixel(
            [math]::Min($width - 1, [math]::Floor($width * [double]$point[0])),
            [math]::Min($height - 1, [math]::Floor($height * [double]$point[1])))
        if ([math]::Max($pixel.R, [math]::Max($pixel.G, $pixel.B)) -gt 8) { $hasVisiblePixel = $true; break }
    }
    if (-not $rendered -or -not $hasVisiblePixel) {
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([Drawing.Color]::Black)
            $device = $graphics.GetHdc()
            try { $rendered = [ShelteredBenchmark.NativeWindow]::PrintWindow($Handle, $device, 0) }
            finally { $graphics.ReleaseHdc($device) }
        }
        finally { $graphics.Dispose() }
    }
    if (-not $rendered) { $bitmap.Dispose(); throw 'PrintWindow failed in both capture modes.' }
    return $bitmap
}

function Get-NativeFrameRmse {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][IntPtr]$Handle,
        [Parameter(Mandatory = $true)][string]$ReferencePath
    )
    Add-Type -AssemblyName System.Drawing
    $capture = New-NativeWindowBitmap -Handle $Handle
    $reference = [Drawing.Bitmap]::FromFile($ReferencePath)
    $smallCapture = New-Object Drawing.Bitmap 64, 36
    $smallReference = New-Object Drawing.Bitmap 64, 36
    try {
        $graphics = [Drawing.Graphics]::FromImage($smallCapture)
        try { $graphics.DrawImage($capture, 0, 0, 64, 36) } finally { $graphics.Dispose() }
        $graphics = [Drawing.Graphics]::FromImage($smallReference)
        try { $graphics.DrawImage($reference, 0, 0, 64, 36) } finally { $graphics.Dispose() }
        [double]$sum = 0
        for ($y = 0; $y -lt 36; $y++) {
            for ($x = 0; $x -lt 64; $x++) {
                $a = $smallCapture.GetPixel($x, $y)
                $b = $smallReference.GetPixel($x, $y)
                $sum += [math]::Pow($a.R - $b.R, 2) + [math]::Pow($a.G - $b.G, 2) + [math]::Pow($a.B - $b.B, 2)
            }
        }
        return [math]::Round([math]::Sqrt($sum / (64 * 36 * 3)), 3)
    }
    finally {
        $smallCapture.Dispose(); $smallReference.Dispose(); $capture.Dispose(); $reference.Dispose()
    }
}

function Wait-NativeMenuReady {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][Diagnostics.Process]$Process,
        [string]$ReferencePath,
        [double]$RmseThreshold = 15,
        [int]$FallbackDelaySeconds = 16,
        [int]$TimeoutSeconds = 90,
        [int]$PollMilliseconds = 250
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $firstWindowAt = $null
    $consecutiveMatches = 0
    $captureAttempts = 0
    $captureFailures = 0
    $bestRmse = [double]::PositiveInfinity
    $lastRmse = $null
    $lastCaptureError = ''
    do {
        $Process.Refresh()
        if ($Process.HasExited) { throw "Game process exited with code $($Process.ExitCode) before menu readiness." }
        if ($Process.MainWindowHandle -ne [IntPtr]::Zero -and $Process.Responding) {
            if ($null -eq $firstWindowAt) { $firstWindowAt = [DateTimeOffset]::UtcNow }
            if (-not [string]::IsNullOrWhiteSpace($ReferencePath) -and (Test-Path -LiteralPath $ReferencePath -PathType Leaf)) {
                try {
                    $rmse = Get-NativeFrameRmse -Handle $Process.MainWindowHandle -ReferencePath $ReferencePath
                    $captureAttempts++
                    $lastRmse = $rmse
                    if ($rmse -lt $bestRmse) { $bestRmse = $rmse }
                    if ($rmse -le $RmseThreshold) { $consecutiveMatches++ } else { $consecutiveMatches = 0 }
                    if ($consecutiveMatches -ge 3) {
                        return [pscustomobject]@{
                            Method = 'native-reference-frame'; Rmse = $rmse; BestRmse = $bestRmse
                            CaptureAttempts = $captureAttempts; CaptureFailures = $captureFailures
                            ObservedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
                        }
                    }
                }
                catch {
                    $captureFailures++
                    $lastCaptureError = $_.Exception.Message
                    $consecutiveMatches = 0
                }
            }
            elseif (([DateTimeOffset]::UtcNow - $firstWindowAt).TotalSeconds -ge $FallbackDelaySeconds) {
                return [pscustomobject]@{ Method = 'window-delay-fallback'; Rmse = $null; ObservedAtUtc = [DateTimeOffset]::UtcNow.ToString('o') }
            }
        }
        Start-Sleep -Milliseconds $PollMilliseconds
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    $bestText = if ([double]::IsPositiveInfinity($bestRmse)) { 'none' } else { [string]$bestRmse }
    $lastText = if ($null -eq $lastRmse) { 'none' } else { [string]$lastRmse }
    throw "Native menu readiness timed out after $TimeoutSeconds seconds (attempts=$captureAttempts, failures=$captureFailures, bestRmse=$bestText, lastRmse=$lastText, threshold=$RmseThreshold, lastCaptureError='$lastCaptureError')."
}

function Start-NativeMenuReadyProbe {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$ProcessId,
        [string]$ReferencePath,
        [double]$RmseThreshold = 15,
        [int]$FallbackDelaySeconds = 16,
        [int]$TimeoutSeconds = 90
    )
    $modulePath = $MyInvocation.MyCommand.Module.Path
    $script = {
        param($RunnerModule, $TargetPid, $Reference, $Threshold, $DelaySeconds, $Timeout)
        Import-Module $RunnerModule -Force -DisableNameChecking
        $target = Get-Process -Id $TargetPid -ErrorAction Stop
        Wait-NativeMenuReady -Process $target -ReferencePath $Reference -RmseThreshold $Threshold `
            -FallbackDelaySeconds $DelaySeconds -TimeoutSeconds $Timeout
    }
    $powershell = [PowerShell]::Create()
    [void]$powershell.AddScript($script).AddArgument($modulePath).AddArgument($ProcessId).AddArgument($ReferencePath).AddArgument($RmseThreshold).AddArgument($FallbackDelaySeconds).AddArgument($TimeoutSeconds)
    $async = $powershell.BeginInvoke()
    return [pscustomobject]@{ PowerShell = $powershell; Async = $async }
}

function Complete-NativeMenuReadyProbe {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)]$Probe)
    try {
        $output = @($Probe.PowerShell.EndInvoke($Probe.Async))
        $readiness = $output | Where-Object { $_.PSObject.Properties['Method'] -and $_.PSObject.Properties['ObservedAtUtc'] } | Select-Object -Last 1
        if ($null -eq $readiness) { throw 'Native menu readiness probe completed without a readiness result.' }
        return $readiness
    }
    finally { $Probe.PowerShell.Dispose() }
}

function Stop-NativeMenuReadyProbe {
    [CmdletBinding()]
    param([AllowNull()]$Probe)
    if ($null -eq $Probe -or $null -eq $Probe.PowerShell) { return }
    try {
        if (-not $Probe.Async.IsCompleted) { $Probe.PowerShell.Stop() }
    }
    catch { }
    finally { $Probe.PowerShell.Dispose() }
}

function Save-HarnessFailureDiagnostics {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [Parameter(Mandatory = $true)][string]$RawRoot,
        [Parameter(Mandatory = $true)][string]$ScreenshotRoot,
        [Parameter(Mandatory = $true)][string]$Prefix
    )
    try { [void](Save-ShelteredHarnessScreenshot -Port $Port -Path (Join-Path $ScreenshotRoot "$Prefix.png") -Mode client) } catch { }
    foreach ($probe in @(
        @{ Name = 'status'; Route = '/status'; Query = @{} },
        @{ Name = 'health'; Route = '/state/health'; Query = @{} },
        @{ Name = 'events'; Route = '/events'; Query = @{ since = '0'; limit = '200' } },
        @{ Name = 'flow'; Route = '/flow/custom-draft'; Query = @{ action = 'status' } },
        @{ Name = 'scenario_ui'; Route = '/ui'; Query = @{ filter = 'Scenario'; inactive = 'true'; components = 'true' } }
    )) {
        try {
            [void](Save-ShelteredHarnessJson -Port $Port -Route $probe.Route -Query $probe.Query `
                -Path (Join-Path $RawRoot ("{0}_{1}.json" -f $Prefix, $probe.Name)) -TimeoutSeconds 15)
        }
        catch {
            $_.Exception.Message | Set-Content -LiteralPath (Join-Path $RawRoot ("{0}_{1}.error.txt" -f $Prefix, $probe.Name)) -Encoding UTF8
        }
    }
}

function Get-PhaseProcessSummaries {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Samples)
    $summaries = New-Object 'System.Collections.Generic.List[object]'
    $phaseNames = @($Samples | ForEach-Object { [string]$_.Phase } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    foreach ($phaseName in $phaseNames) {
        $phaseSamples = @($Samples | Where-Object Phase -EQ $phaseName)
        $summary = Get-ProcessSampleSummary -Samples $phaseSamples
        if ($null -eq $summary) { continue }
        $summaries.Add([pscustomobject]@{
            Phase = $phaseName
            FirstElapsedMs = [double]$phaseSamples[0].ElapsedMs
            LastElapsedMs = [double]$phaseSamples[-1].ElapsedMs
            DurationMs = $summary.DurationMs
            Samples = $summary.Samples
            CpuSeconds = $summary.CpuSeconds
            MeanWorkingSetMiB = $summary.MeanWorkingSetMiB
            PeakWorkingSetMiB = $summary.PeakWorkingSetMiB
            MeanPrivateMiB = $summary.MeanPrivateMiB
            PeakPrivateMiB = $summary.PeakPrivateMiB
            PeakThreads = $summary.PeakThreads
            PeakHandles = $summary.PeakHandles
        })
    }
    return $summaries.ToArray()
}

function Stop-BenchmarkGameProcess {
    [CmdletBinding()]
    param(
        [AllowNull()][Diagnostics.Process]$Process,
        [AllowNull()][Nullable[DateTimeOffset]]$ExpectedStartUtc
    )
    if ($null -eq $Process) { return }
    $canDispose = $false
    try {
        $current = Get-Process -Id $Process.Id -ErrorAction SilentlyContinue
        if ($null -eq $current) { $canDispose = $true; return }
        if ($null -ne $ExpectedStartUtc) {
            $actualStart = [DateTimeOffset]$current.StartTime.ToUniversalTime()
            if ([math]::Abs(($actualStart - $ExpectedStartUtc).TotalSeconds) -ge 1) {
                throw "Refusing to stop reused PID $($Process.Id)."
            }
        }
        [void]$current.CloseMainWindow()
        if (-not $current.WaitForExit(5000)) {
            Stop-Process -Id $current.Id -Force -ErrorAction Stop
            if (-not $current.WaitForExit(5000)) { throw "Process $($current.Id) remained alive after forced termination." }
        }
        $canDispose = $true
    }
    finally { if ($canDispose) { $Process.Dispose() } }
}

function New-BenchmarkDeploymentAbsenceTransaction {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]$Platform,
        [Parameter(Mandatory = $true)]$Profile,
        [Parameter(Mandatory = $true)][string]$ConfigRoot,
        [Parameter(Mandatory = $true)][string]$InstallRoot,
        [Parameter(Mandatory = $true)][string]$BackupRoot,
        [Parameter(Mandatory = $true)][string]$ArtifactRoot
    )

    $absentRoles = @((Get-ObjectPropertyValue $Profile 'physicallyAbsentDeploymentRoles' @()) | ForEach-Object {
        ([string]$_).Trim().ToLowerInvariant()
    } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($absentRoles.Count -eq 0) { return $null }

    $gates = @(Get-ObjectPropertyValue $Platform 'hashGates' @())
    $selectedGates = New-Object 'System.Collections.Generic.List[object]'
    foreach ($role in $absentRoles) {
        $matches = @($gates | Where-Object { ([string](Get-ObjectPropertyValue $_ 'role' '')).ToLowerInvariant() -eq $role })
        if ($matches.Count -ne 1) { throw "Physical-absence role '$role' must resolve to exactly one deployment hash gate." }
        $selectedGates.Add($matches[0])
    }

    $preflightPath = Join-Path $ArtifactRoot 'physical_absence_preflight_hashes.json'
    $verified = Test-BenchmarkDeploymentHashes -Gates $selectedGates.ToArray() -InstallRoot $InstallRoot `
        -ConfigRoot $ConfigRoot -OutputPath $preflightPath
    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
    $entries = New-Object 'System.Collections.Generic.List[object]'
    $installPrefix = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\') + '\'
    try {
        for ($index = 0; $index -lt $selectedGates.Count; $index++) {
            $gate = $selectedGates[$index]
            $role = ([string](Get-ObjectPropertyValue $gate 'role' '')).ToLowerInvariant()
            $relativePath = [string](Get-ObjectPropertyValue $gate 'deployedPath' '')
            if ([IO.Path]::IsPathRooted($relativePath)) { throw "Physical-absence gate '$role' must use an install-relative deployedPath." }
            $target = [IO.Path]::GetFullPath((Join-Path $InstallRoot $relativePath))
            if (-not $target.StartsWith($installPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Physical-absence target '$target' escapes install '$InstallRoot'."
            }
            $backupPath = Join-Path $BackupRoot (($role -replace '[^A-Za-z0-9_.-]', '_') + '.dll.backup')
            Copy-Item -LiteralPath $target -Destination $backupPath -Force
            $originalHash = [string]$verified.Gates[$index].DeployedSha256
            $backupHash = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
            if ($backupHash -ne $originalHash) { throw "Physical-absence backup hash mismatch for '$role'." }
            $entry = [pscustomobject]@{
                Role = $role; RelativePath = $relativePath; TargetPath = $target; BackupPath = $backupPath
                OriginalSha256 = $originalHash; BackupSha256 = $backupHash; AbsentObserved = $false
            }
            $entries.Add($entry)
            Remove-Item -LiteralPath $target -Force
            $entry.AbsentObserved = -not (Test-Path -LiteralPath $target)
            if (-not $entry.AbsentObserved) { throw "Physical-absence target '$target' still exists after removal." }
        }
    }
    catch {
        $failure = $_
        $rollbackErrors = New-Object 'System.Collections.Generic.List[string]'
        foreach ($entry in @($entries.ToArray())) {
            try {
                Copy-Item -LiteralPath $entry.BackupPath -Destination $entry.TargetPath -Force
                $restoredHash = (Get-FileHash -LiteralPath $entry.TargetPath -Algorithm SHA256).Hash
                if ($restoredHash -ne [string]$entry.OriginalSha256) { throw 'rollback hash mismatch' }
            }
            catch { $rollbackErrors.Add("$($entry.Role): $($_.Exception.Message)") }
        }
        if ($rollbackErrors.Count -gt 0) {
            throw "Physical-absence preparation failed: $($failure.Exception.Message) Rollback also failed: $($rollbackErrors.ToArray() -join '; ')"
        }
        throw $failure
    }

    $transaction = [pscustomobject]@{
        InstallRoot = $InstallRoot; CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        Entries = $entries.ToArray(); Restored = $false
    }
    $transaction | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ArtifactRoot 'physical_absence.json') -Encoding UTF8
    return $transaction
}

function Restore-BenchmarkDeploymentAbsenceTransaction {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]$Transaction,
        [Parameter(Mandatory = $true)][string]$ArtifactRoot
    )
    if ([bool]$Transaction.Restored) { return }
    $rows = New-Object 'System.Collections.Generic.List[object]'
    $errors = New-Object 'System.Collections.Generic.List[string]'
    $archiveRoot = Join-Path $ArtifactRoot 'physical-absence-unexpected-after'
    foreach ($entry in @($Transaction.Entries)) {
        try {
            $unexpectedHash = $null
            if (Test-Path -LiteralPath $entry.TargetPath -PathType Leaf) {
                $unexpectedHash = (Get-FileHash -LiteralPath $entry.TargetPath -Algorithm SHA256).Hash
                New-Item -ItemType Directory -Path $archiveRoot -Force | Out-Null
                Move-Item -LiteralPath $entry.TargetPath -Destination (Join-Path $archiveRoot ([IO.Path]::GetFileName([string]$entry.TargetPath))) -Force
            }
            $parent = Split-Path -Parent ([string]$entry.TargetPath)
            if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
            Copy-Item -LiteralPath $entry.BackupPath -Destination $entry.TargetPath -Force
            $restoredHash = (Get-FileHash -LiteralPath $entry.TargetPath -Algorithm SHA256).Hash
            $ok = $restoredHash -eq [string]$entry.OriginalSha256
            $rows.Add([pscustomobject]@{
                Role = [string]$entry.Role; TargetPath = [string]$entry.TargetPath
                ExpectedSha256 = [string]$entry.OriginalSha256; RestoredSha256 = $restoredHash
                UnexpectedReplacementSha256 = $unexpectedHash; Ok = $ok
            })
            if (-not $ok) { throw "restored hash mismatch for '$($entry.Role)'" }
        }
        catch { $errors.Add("$($entry.Role): $($_.Exception.Message)") }
    }
    $rows.ToArray() | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $ArtifactRoot 'physical_absence_restore.json') -Encoding UTF8
    if ($errors.Count -gt 0) { throw "Physical-absence restoration failed: $($errors.ToArray() -join '; ')" }
    $Transaction.Restored = $true
}

function Start-ShelteredPlatformSession {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]$Platform,
        [Parameter(Mandatory = $true)]$Profile,
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][string]$StateRoot,
        [Parameter(Mandatory = $true)][string]$ArtifactRoot,
        [Parameter(Mandatory = $true)][string]$LeaseOwner,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][object[]]$InstallLocks,
        [int]$ProcessIntervalMilliseconds = 100,
        [AllowNull()][object[]]$ExistingEnabledIds,
        [string]$DeploymentHashFileName = 'deployment_hash_gates.json',
        [switch]$StartNativeReadiness
    )

    $platformName = [string]$Platform.name
    $profileName = [string]$Profile.name
    $installRoot = [IO.Path]::GetFullPath([string]$Platform.installRoot)
    $isVanilla = ([string]$Profile.mode).ToLowerInvariant() -eq 'vanilla'
    $harnessEnabled = [bool](Get-ObjectPropertyValue $Profile 'harness' (-not $isVanilla))
    $session = [pscustomobject]@{
        PlatformName = $platformName; ProfileName = $profileName; Platform = $Platform; Profile = $Profile
        InstallRoot = $installRoot; Port = [int](Get-ObjectPropertyValue $Platform 'agentPort' 0)
        LeaseOwner = $LeaseOwner; HarnessEnabled = $harnessEnabled; LeaseAcquired = $false
        Snapshot = $null; SelectedModIds = @(); Process = $null; ProcessStartUtc = $null; Sampler = $null
        NativeReadinessProbe = $null; ProcessStopped = $true; Restored = $false; Samples = @()
        DeploymentAbsenceTransaction = $null
        ReadinessMethod = ''; HarnessMenuReadyMs = $null; StartupMs = $null; ArtifactRoot = $ArtifactRoot; EventCursor = 0
    }

    try {
        [void](Assert-BenchmarkInstallLockAuthorization -InstallRoot $installRoot -InstallLocks $InstallLocks)

        $existingProcesses = @(Get-Process -Name ([string]$Platform.processName) -ErrorAction SilentlyContinue)
        if ($existingProcesses.Count -gt 0) { throw "Refusing to launch because $platformName already has process(es): $($existingProcesses.Id -join ', ')." }

        $session.Snapshot = New-InstallStateSnapshot -InstallRoot $installRoot -BackupRoot $StateRoot
        $catalog = Get-InstalledModCatalog -InstallRoot $installRoot
        $existingState = Get-LoadOrderState -InstallRoot $installRoot
        $existingOrder = if ($null -ne $existingState) { @($existingState.order) } else { @() }
        $enabled = if ($null -ne $ExistingEnabledIds) { @($ExistingEnabledIds) } else { @(Get-ObjectPropertyValue $Platform '_benchmarkEnabledIds' (Get-EnabledLoadOrderIds $existingState)) }
        $coreIds = @((Get-ObjectPropertyValue $Config 'coreModIds' @('com.harmony.0harmony', 'coolnether123.shelteredagentinterface')) | ForEach-Object { [string]$_ })
        $session.SelectedModIds = @(Resolve-ShelteredModProfile -Profile $Profile -Catalog $catalog -ExistingOrder $existingOrder -CoreModIds $coreIds -ExistingEnabledIds $enabled)

        Set-DoorstopEnabled -InstallRoot $installRoot -Enabled (-not $isVanilla)
        if (-not $isVanilla) {
            Set-ShelteredLoadOrder -InstallRoot $installRoot -ModIds $session.SelectedModIds
            Set-ShelteredManagerOptions -InstallRoot $installRoot -Overrides (Get-ObjectPropertyValue $Profile 'managerOptions' ([pscustomobject]@{}))
        }
        if ($harnessEnabled) {
            $managerIniPath = Join-Path $installRoot 'SMM\bin\mod_manager.ini'
            if (Test-Path -LiteralPath $managerIniPath -PathType Leaf) {
                $managerIni = Get-Content -LiteralPath $managerIniPath -Raw
                if ($managerIni -match '(?m)^AutoLoadSaveSlot=') {
                    $managerIni = $managerIni -replace '(?m)^AutoLoadSaveSlot=.*$', 'AutoLoadSaveSlot=0'
                }
                else {
                    $managerIni = $managerIni.TrimEnd([char[]]"`r`n") + "`r`nAutoLoadSaveSlot=0`r`n"
                }
                Set-Content -LiteralPath $managerIniPath -Value $managerIni -Encoding UTF8
            }
        }
        $session.DeploymentAbsenceTransaction = New-BenchmarkDeploymentAbsenceTransaction -Platform $Platform -Profile $Profile `
            -ConfigRoot ([string]$Config._configRoot) -InstallRoot $installRoot `
            -BackupRoot (Join-Path $StateRoot 'deployment-absence-before') -ArtifactRoot $ArtifactRoot
        if ($harnessEnabled) {
            $absentRoles = @((Get-ObjectPropertyValue $Profile 'physicallyAbsentDeploymentRoles' @()) | ForEach-Object { ([string]$_).Trim().ToLowerInvariant() })
            $activeGates = @((Get-ObjectPropertyValue $Platform 'hashGates' @()) | Where-Object {
                $absentRoles -notcontains ([string](Get-ObjectPropertyValue $_ 'role' '')).ToLowerInvariant()
            })
            [void](Test-BenchmarkDeploymentHashes -Gates $activeGates -InstallRoot $installRoot `
                -ConfigRoot ([string]$Config._configRoot) -OutputPath (Join-Path $ArtifactRoot $DeploymentHashFileName))
        }

        $executablePath = Join-Path $installRoot ([string]$Platform.executable)
        $arguments = @((Get-ObjectPropertyValue $Platform 'launchArguments' @()) | ForEach-Object { [string]$_ })
        $session.Process = Start-Process -FilePath $executablePath -WorkingDirectory $installRoot -ArgumentList $arguments -PassThru
        $session.ProcessStartUtc = [DateTimeOffset]$session.Process.StartTime.ToUniversalTime()
        $session.ProcessStopped = $false
        [pscustomobject]@{ Pid = $session.Process.Id; ProcessName = [string]$Platform.processName; ExecutablePath = $executablePath; StartTimeUtc = $session.ProcessStartUtc.ToString('o') } |
            ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $ArtifactRoot 'owned_process.json') -Encoding UTF8
        $session.Sampler = Start-BenchmarkProcessSampler -ProcessId $session.Process.Id -ProcessStartUtc $session.ProcessStartUtc -IntervalMilliseconds $ProcessIntervalMilliseconds

        if ($StartNativeReadiness) {
            $reference = [string](Get-ObjectPropertyValue $Platform 'vanillaMenuReferenceImage' '')
            if (-not [string]::IsNullOrWhiteSpace($reference)) { $reference = Resolve-ConfiguredPath -Path $reference -BasePath ([string]$Config._configRoot) }
            $sampling = Get-ObjectPropertyValue $Config 'sampling'
            $session.NativeReadinessProbe = Start-NativeMenuReadyProbe -ProcessId $session.Process.Id -ReferencePath $reference `
                -RmseThreshold ([double](Get-ObjectPropertyValue $Platform 'vanillaMenuRmseThreshold' 15)) `
                -FallbackDelaySeconds ([int](Get-ObjectPropertyValue $Platform 'vanillaWindowDelaySeconds' 16)) `
                -TimeoutSeconds ([int](Get-ObjectPropertyValue $sampling 'startupTimeoutSeconds' 120))
        }
        return $session
    }
    catch {
        $failure = $_
        $transactionErrors = New-Object 'System.Collections.Generic.List[string]'
        try {
            foreach ($cleanupError in @(Stop-ShelteredPlatformSession -Session $session)) {
                $transactionErrors.Add([string]$cleanupError)
            }
        }
        catch { $transactionErrors.Add("Session stop failed: $($_.Exception.Message)") }
        try { Restore-ShelteredPlatformSession -Session $session }
        catch { $transactionErrors.Add("Install restoration failed: $($_.Exception.Message)") }
        if ($transactionErrors.Count -gt 0) {
            throw "Platform session startup failed: $($failure.Exception.Message) Transactional cleanup also failed: $($transactionErrors.ToArray() -join '; ')"
        }
        throw $failure
    }
}

function Wait-ShelteredPlatformSessionReady {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]$Session,
        [int]$TimeoutSeconds = 120,
        [string]$MenuBlockersFileName = 'menu_blockers.json'
    )
    if ($Session.HarnessEnabled) {
        if ($Session.Port -le 0) { throw "Harness profile '$($Session.ProfileName)' requires a positive agentPort for '$($Session.PlatformName)'." }
        $bind = Wait-ShelteredHarness -Port $Session.Port -TimeoutSeconds $TimeoutSeconds
        $bind | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $Session.ArtifactRoot 'harness_bind_status.json') -Encoding UTF8
        [void](Acquire-ShelteredHarnessLease -Port $Session.Port -Owner $Session.LeaseOwner)
        $Session.LeaseAcquired = $true
        [void](Enable-ShelteredBackgroundRun -Port $Session.Port)
        $menu = Wait-HarnessMenuReady -Port $Session.Port -MenuReadyRegex ([string](Get-ObjectPropertyValue $Session.Platform 'menuReadyRegex' 'MenuScene')) -TimeoutSeconds $TimeoutSeconds
        $menu | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $Session.ArtifactRoot 'menu_status.json') -Encoding UTF8
        $Session.HarnessMenuReadyMs = [math]::Round(([DateTimeOffset]::UtcNow - $Session.ProcessStartUtc).TotalMilliseconds, 1)
        Dismiss-ShelteredBenchmarkMenuBlockers -Port $Session.Port | ConvertTo-Json -Depth 20 |
            Set-Content -LiteralPath (Join-Path $Session.ArtifactRoot $MenuBlockersFileName) -Encoding UTF8
    }

    if ($null -ne $Session.NativeReadinessProbe) {
        # Complete the same Wait-NativeMenuReady gate used by vanilla; its probe
        # starts at process launch so harness setup cannot delay observation.
        $readiness = Complete-NativeMenuReadyProbe -Probe $Session.NativeReadinessProbe
        $Session.NativeReadinessProbe = $null
        $readiness | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Session.ArtifactRoot 'native_menu_readiness.json') -Encoding UTF8
        $Session.ReadinessMethod = if ($Session.HarnessEnabled) { "harness-status+$($readiness.Method)" } else { $readiness.Method }
        $Session.StartupMs = [math]::Round(([DateTimeOffset]::Parse([string]$readiness.ObservedAtUtc) - $Session.ProcessStartUtc).TotalMilliseconds, 1)
    }
    elseif ($Session.HarnessEnabled) {
        $Session.ReadinessMethod = 'harness-status'
        $Session.StartupMs = $Session.HarnessMenuReadyMs
    }
    if ($null -ne $Session.Sampler) { $Session.Sampler.State.Phase = 'menu-idle' }
    return $Session
}

function Stop-ShelteredPlatformSession {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)]$Session)
    $errors = New-Object 'System.Collections.Generic.List[string]'
    if ($null -ne $Session.NativeReadinessProbe) {
        try { Stop-NativeMenuReadyProbe -Probe $Session.NativeReadinessProbe } catch { $errors.Add("Readiness cleanup failed: $($_.Exception.Message)") }
        $Session.NativeReadinessProbe = $null
    }
    if ($Session.LeaseAcquired) {
        try { [void](Release-ShelteredHarnessLease -Port $Session.Port -Owner $Session.LeaseOwner); $Session.LeaseAcquired = $false }
        catch { $errors.Add("Lease release failed: $($_.Exception.Message)") }
    }
    if (-not $Session.ProcessStopped) {
        try { Stop-BenchmarkGameProcess -Process $Session.Process -ExpectedStartUtc $Session.ProcessStartUtc; $Session.ProcessStopped = $true }
        catch { $errors.Add("Process close failed: $($_.Exception.Message)") }
    }
    if ($null -ne $Session.Sampler) {
        try { $Session.Samples = @(Stop-BenchmarkProcessSampler -Sampler $Session.Sampler); $Session.Sampler = $null }
        catch { $errors.Add("Sampler cleanup failed: $($_.Exception.Message)") }
    }
    return $errors.ToArray()
}

function Restore-ShelteredPlatformSession {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)]$Session)
    if ($Session.Restored -or $null -eq $Session.Snapshot) { return }
    if (-not $Session.ProcessStopped) { throw "Refusing to restore '$($Session.InstallRoot)' while its owned process is still active." }
    $errors = New-Object 'System.Collections.Generic.List[string]'
    try { Restore-InstallStateSnapshot -Snapshot $Session.Snapshot }
    catch { $errors.Add("Install configuration: $($_.Exception.Message)") }
    if ($null -ne $Session.DeploymentAbsenceTransaction) {
        try { Restore-BenchmarkDeploymentAbsenceTransaction -Transaction $Session.DeploymentAbsenceTransaction -ArtifactRoot $Session.ArtifactRoot }
        catch { $errors.Add($_.Exception.Message) }
    }
    if ($errors.Count -gt 0) { throw "Platform restoration left residual differences: $($errors.ToArray() -join '; ')" }
    $Session.Restored = $true
}

function Invoke-ShelteredBenchmarkCase {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]$Platform,
        [Parameter(Mandatory = $true)]$Profile,
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$CaseRoot,
        [Parameter(Mandatory = $true)][int]$Iteration,
        [Parameter(Mandatory = $true)][string]$RunId,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][object[]]$InstallLocks,
        [string]$ExecutionMode = 'serial',
        [string]$ComparisonLane = ''
    )
    New-Item -ItemType Directory -Path $CaseRoot -Force | Out-Null
    $rawRoot = Join-Path $CaseRoot 'raw'
    $screenshotRoot = Join-Path $CaseRoot 'screenshots'
    $stateRoot = Join-Path $rawRoot 'install-state-before'
    New-Item -ItemType Directory -Path $rawRoot, $screenshotRoot -Force | Out-Null

    $platformName = [string]$Platform.name
    $profileName = [string]$Profile.name
    $installRoot = [string]$Platform.installRoot
    $mode = ([string]$Profile.mode).ToLowerInvariant()
    $isVanilla = $mode -eq 'vanilla'
    $harnessEnabled = [bool](Get-ObjectPropertyValue $Profile 'harness' (-not $isVanilla))
    $port = [int](Get-ObjectPropertyValue $Platform 'agentPort' 0)
    $sampling = Get-ObjectPropertyValue $Config 'sampling'
    $fileIntegrity = Get-ObjectPropertyValue $Config 'fileIntegrity' ([pscustomobject]@{})
    $mutableModPathPatterns = @((Get-ObjectPropertyValue $fileIntegrity 'mutableModRelativePathPatterns' @()) | ForEach-Object { [string]$_ })
    $startupTimeout = [int](Get-ObjectPropertyValue $sampling 'startupTimeoutSeconds' 120)
    $processInterval = [int](Get-ObjectPropertyValue $sampling 'processIntervalMilliseconds' 100)
    $fpsDuration = [int](Get-ObjectPropertyValue $sampling 'fpsDurationSeconds' 15)
    $fpsInterval = [int](Get-ObjectPropertyValue $sampling 'fpsIntervalMilliseconds' 100)
    $minimumFpsCoverage = [double](Get-ObjectPropertyValue $sampling 'minimumFpsCoveragePercent' 70) / 100.0
    $idleDuration = [int](Get-ObjectPropertyValue $sampling 'vanillaIdleSeconds' $fpsDuration)
    $leaseOwner = "sheltered-benchmark-$RunId-$platformName-$profileName-$Iteration"
    $session = $null
    $environment = $null
    $process = $null
    $sampler = $null
    $samples = @()
    $selected = @()
    $readinessMethod = ''
    $harnessMenuReadyMs = $null
    $startupMs = $null
    $transition = $null
    $selectionTransition = $null
    $menuFps = $null
    $selectionFps = $null
    $scenarioFps = $null
    $status = 'passed'
    $errorMessage = ''
    $cleanupErrors = New-Object 'System.Collections.Generic.List[string]'
    $startedAt = [DateTimeOffset]::UtcNow
    try {
        $session = Start-ShelteredPlatformSession -Platform $Platform -Profile $Profile -Config $Config -StateRoot $stateRoot `
            -ArtifactRoot $rawRoot -LeaseOwner $leaseOwner -InstallLocks $InstallLocks `
            -ProcessIntervalMilliseconds $processInterval -StartNativeReadiness
        $selected = @($session.SelectedModIds)
        $process = $session.Process
        $sampler = $session.Sampler
        $environment = Get-BenchmarkEnvironment -RepositoryRoot $RepositoryRoot -Platform $Platform -SelectedModIds $selected `
            -MutableModRelativePathPatterns $mutableModPathPatterns
        $environment | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $rawRoot 'environment.json') -Encoding UTF8
        $caseConfiguration = [pscustomobject]@{ Platform = $Platform; Profile = $Profile; ResolvedModIds = $selected; Iteration = $Iteration }
        $caseConfiguration | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $rawRoot 'case.json') -Encoding UTF8

        [void](Wait-ShelteredPlatformSessionReady -Session $session -TimeoutSeconds $startupTimeout)
        $readinessMethod = $session.ReadinessMethod
        $harnessMenuReadyMs = $session.HarnessMenuReadyMs
        $startupMs = $session.StartupMs

        if ($harnessEnabled) {
            foreach ($probe in @(
                @{ Name = 'status'; Route = '/status' }, @{ Name = 'health'; Route = '/state/health' },
                @{ Name = 'pump'; Route = '/health/pump' }, @{ Name = 'instances'; Route = '/instances' },
                @{ Name = 'tools'; Route = '/tools' }, @{ Name = 'loadorder'; Route = '/state/loadorder' },
                @{ Name = 'apis'; Route = '/state/apis' }
            )) {
                try { [void](Save-ShelteredHarnessJson -Port $port -Route $probe.Route -Path (Join-Path $rawRoot ("harness_{0}.json" -f $probe.Name)) -TimeoutSeconds 15) }
                catch { $_.Exception.Message | Set-Content -LiteralPath (Join-Path $rawRoot ("harness_{0}.error.txt" -f $probe.Name)) -Encoding UTF8 }
            }
            try { [void](Save-ShelteredHarnessScreenshot -Port $port -Path (Join-Path $screenshotRoot 'menu.png') -Mode client) } catch { }
            $menuFps = Measure-ShelteredSmoothFps -Port $port -DurationSeconds $fpsDuration -IntervalMilliseconds $fpsInterval `
                -CsvPath (Join-Path $rawRoot 'menu_fps_samples.csv') -SummaryPath (Join-Path $rawRoot 'menu_fps_summary.json') -MinimumValidFraction $minimumFpsCoverage
            if (-not $menuFps.CoverageOk) {
                $status = 'partial'; $errorMessage = Join-BenchmarkIssue $errorMessage "Menu FPS coverage $($menuFps.CoveragePercent)% is below $($menuFps.MinimumCoveragePercent)%."
            }

            if ([bool](Get-ObjectPropertyValue $Profile 'scenarioSelectionTransition' $true)) {
                $sampler.State.Phase = 'scenario-transition'
                $selectionTransition = Measure-ShelteredScenarioSelectionTransition -Port $port -OutputPath (Join-Path $rawRoot 'scenario_selection_transition.json') -TimeoutSeconds ([int](Get-ObjectPropertyValue $sampling 'scenarioTimeoutSeconds' 120))
                if ($selectionTransition.Ok) {
                    try { [void](Save-ShelteredHarnessScreenshot -Port $port -Path (Join-Path $screenshotRoot 'scenario_selection.png') -Mode client) } catch { }
                    $sampler.State.Phase = 'scenario-selection-idle'
                    $selectionFps = Measure-ShelteredSmoothFps -Port $port -DurationSeconds $fpsDuration -IntervalMilliseconds $fpsInterval `
                        -CsvPath (Join-Path $rawRoot 'scenario_selection_fps_samples.csv') -SummaryPath (Join-Path $rawRoot 'scenario_selection_fps_summary.json') -MinimumValidFraction $minimumFpsCoverage
                    if (-not $selectionFps.CoverageOk) {
                        $status = 'partial'; $errorMessage = Join-BenchmarkIssue $errorMessage "Scenario-selection FPS coverage $($selectionFps.CoveragePercent)% is below $($selectionFps.MinimumCoveragePercent)%."
                    }
                }
                else {
                    $status = 'partial'; $errorMessage = Join-BenchmarkIssue $errorMessage ([string]$selectionTransition.Error)
                    Save-HarnessFailureDiagnostics -Port $port -RawRoot $rawRoot -ScreenshotRoot $screenshotRoot -Prefix 'scenario_selection_failure'
                }
            }

            if ([bool](Get-ObjectPropertyValue $Profile 'scenarioTransition' $true) -and ($null -eq $selectionTransition -or $selectionTransition.Ok)) {
                $sampler.State.Phase = 'scenario-book-transition'
                $transition = Measure-ShelteredScenarioTransition -Port $port -OutputPath (Join-Path $rawRoot 'scenario_transition.json') -TimeoutSeconds ([int](Get-ObjectPropertyValue $sampling 'scenarioTimeoutSeconds' 120))
                if ($transition.Ok) {
                    try { [void](Save-ShelteredHarnessJson -Port $port -Route '/scenario-book/rows' -Query @{ fields = 'id,title,detail' } -Path (Join-Path $rawRoot 'scenario_rows.json') -TimeoutSeconds 15) } catch { }
                    try { [void](Save-ShelteredHarnessScreenshot -Port $port -Path (Join-Path $screenshotRoot 'scenario_book.png') -Mode client) } catch { }
                    $sampler.State.Phase = 'scenario-idle'
                    $scenarioFps = Measure-ShelteredSmoothFps -Port $port -DurationSeconds $fpsDuration -IntervalMilliseconds $fpsInterval `
                        -CsvPath (Join-Path $rawRoot 'scenario_fps_samples.csv') -SummaryPath (Join-Path $rawRoot 'scenario_fps_summary.json') -MinimumValidFraction $minimumFpsCoverage
                    if (-not $scenarioFps.CoverageOk) {
                        $status = 'partial'; $errorMessage = Join-BenchmarkIssue $errorMessage "Scenario-book FPS coverage $($scenarioFps.CoveragePercent)% is below $($scenarioFps.MinimumCoveragePercent)%."
                    }
                }
                else {
                    $status = 'partial'; $errorMessage = Join-BenchmarkIssue $errorMessage ([string]$transition.Error)
                    Save-HarnessFailureDiagnostics -Port $port -RawRoot $rawRoot -ScreenshotRoot $screenshotRoot -Prefix 'scenario_book_failure'
                }
            }
        }
        else {
            Start-Sleep -Seconds $idleDuration
            $process.Refresh()
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                try {
                    $bitmap = New-NativeWindowBitmap -Handle $process.MainWindowHandle
                    try { $bitmap.Save((Join-Path $screenshotRoot 'menu_native.png'), [Drawing.Imaging.ImageFormat]::Png) } finally { $bitmap.Dispose() }
                }
                catch { }
            }
        }
    }
    catch {
        $status = 'failed'
        $errorMessage = $_.Exception.Message
        $_ | Out-String | Set-Content -LiteralPath (Join-Path $rawRoot 'failure.txt') -Encoding UTF8
    }
    finally {
        try {
            if (-not $isVanilla -and $null -ne $process) {
                $runtimeLog = Join-Path $installRoot 'SMM\mod_manager.log'
                if (Test-Path -LiteralPath $runtimeLog -PathType Leaf) {
                    Copy-Item -LiteralPath $runtimeLog -Destination (Join-Path $rawRoot 'mod_manager.log') -Force
                }
            }
        }
        catch { $cleanupErrors.Add("Log copy failed: $($_.Exception.Message)") }
        if ($null -ne $session) {
            foreach ($cleanupError in @(Stop-ShelteredPlatformSession -Session $session)) { $cleanupErrors.Add([string]$cleanupError) }
            $samples = @($session.Samples)
            try { $samples | Export-Csv -LiteralPath (Join-Path $rawRoot 'process_samples.csv') -NoTypeInformation -Encoding UTF8 }
            catch { $cleanupErrors.Add("Sample export failed: $($_.Exception.Message)") }
        }
        if ($null -ne $environment) {
            try {
                [void](Test-BenchmarkFileManifest -ExpectedFiles @($environment.Files) -SelectedModDirectories @($environment.SelectedModDirectories) `
                    -MutableModRelativePathPatterns @($environment.MutableModRelativePathPatterns) `
                    -OutputPath (Join-Path $rawRoot 'post_case_file_manifest.json'))
            }
            catch { $cleanupErrors.Add("FILE MANIFEST VERIFICATION FAILED: $($_.Exception.Message)") }
        }
        if ($null -ne $session) {
            try { Restore-ShelteredPlatformSession -Session $session }
            catch { $cleanupErrors.Add("INSTALL RESTORATION FAILED: $($_.Exception.Message)") }
        }
        if ($cleanupErrors.Count -gt 0) {
            $status = 'failed'
            $cleanupMessage = $cleanupErrors.ToArray() -join [Environment]::NewLine
            $cleanupMessage | Set-Content -LiteralPath (Join-Path $rawRoot 'cleanup_failure.txt') -Encoding UTF8
            $errorMessage = (@($errorMessage, $cleanupMessage) | Where-Object { $_ }) -join [Environment]::NewLine
        }
    }

    $startupTimings = Export-ShelteredStartupTimings -LogPath (Join-Path $rawRoot 'mod_manager.log') `
        -CsvPath (Join-Path $rawRoot 'startup_hotspots.csv') -SummaryPath (Join-Path $rawRoot 'startup_hotspots_top20.json')
    $topStartupTiming = $startupTimings | Sort-Object ElapsedMs -Descending | Select-Object -First 1
    $allSummary = Get-ProcessSampleSummary -Samples $samples
    $startupSamples = @($samples | Where-Object { [double]$_.ElapsedMs -le [double]$startupMs })
    $startupSummary = if ($startupSamples.Count -gt 0) { Get-ProcessSampleSummary -Samples $startupSamples } else { $null }
    $phaseSummaries = @(Get-PhaseProcessSummaries -Samples $samples)
    $phaseSummaries | Export-Csv -LiteralPath (Join-Path $rawRoot 'phase_process_summaries.csv') -NoTypeInformation -Encoding UTF8
    $phaseSummaries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $rawRoot 'phase_process_summaries.json') -Encoding UTF8
    $firstWindow = $samples | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    $result = [pscustomobject]@{
        Platform = $platformName
        Profile = $profileName
        Mode = $mode
        ExecutionMode = $ExecutionMode
        ComparisonLane = $ComparisonLane
        Iteration = $Iteration
        Status = $status
        Error = $errorMessage
        SelectedModCount = @($selected).Count
        SelectedModIds = ($selected -join ';')
        ReadinessMethod = $readinessMethod
        HarnessMenuReadyMs = $harnessMenuReadyMs
        StartupMs = $startupMs
        FirstWindowMs = if ($null -ne $firstWindow) { $firstWindow.ElapsedMs } else { $null }
        StartupCpuSeconds = if ($null -ne $startupSummary) { $startupSummary.CpuSeconds } else { $null }
        StartupPeakWorkingSetMiB = if ($null -ne $startupSummary) { $startupSummary.PeakWorkingSetMiB } else { $null }
        StartupPeakPrivateMiB = if ($null -ne $startupSummary) { $startupSummary.PeakPrivateMiB } else { $null }
        TopStartupTiming = if ($null -ne $topStartupTiming) { $topStartupTiming.Operation } else { $null }
        TopStartupTimingMs = if ($null -ne $topStartupTiming) { $topStartupTiming.ElapsedMs } else { $null }
        MeanWorkingSetMiB = if ($null -ne $allSummary) { $allSummary.MeanWorkingSetMiB } else { $null }
        PeakWorkingSetMiB = if ($null -ne $allSummary) { $allSummary.PeakWorkingSetMiB } else { $null }
        MeanPrivateMiB = if ($null -ne $allSummary) { $allSummary.MeanPrivateMiB } else { $null }
        PeakPrivateMiB = if ($null -ne $allSummary) { $allSummary.PeakPrivateMiB } else { $null }
        ScenarioTransitionMs = if ($null -ne $transition -and $transition.Ok) { $transition.ElapsedMs } else { $null }
        ScenarioTransitionFailureElapsedMs = if ($null -ne $transition -and -not $transition.Ok) { $transition.ElapsedMs } else { $null }
        ScenarioTransitionOk = if ($null -ne $transition) { $transition.Ok } else { $null }
        ScenarioSelectionRouteMs = if ($null -ne $selectionTransition -and $selectionTransition.Ok) { $selectionTransition.TotalElapsedMs } else { $null }
        ScenarioSelectionFailureElapsedMs = if ($null -ne $selectionTransition -and -not $selectionTransition.Ok) { $selectionTransition.TotalElapsedMs } else { $null }
        ScenarioSelectionNativeWaitMs = if ($null -ne $selectionTransition) { $selectionTransition.NativeNavigationWaitMs } else { $null }
        ScenarioSelectionTransitionMs = if ($null -ne $selectionTransition -and $selectionTransition.Ok) { $selectionTransition.ScenarioRootAfterClickMs } else { $null }
        ScenarioSelectionTransitionOk = if ($null -ne $selectionTransition) { $selectionTransition.Ok } else { $null }
        MenuMedianSmoothFps = if ($null -ne $menuFps) { $menuFps.MedianSmoothFps } else { $null }
        MenuP05SmoothFps = if ($null -ne $menuFps) { $menuFps.P05SmoothFps } else { $null }
        MenuFpsCoveragePercent = if ($null -ne $menuFps) { $menuFps.CoveragePercent } else { $null }
        ScenarioSelectionMedianSmoothFps = if ($null -ne $selectionFps) { $selectionFps.MedianSmoothFps } else { $null }
        ScenarioSelectionP05SmoothFps = if ($null -ne $selectionFps) { $selectionFps.P05SmoothFps } else { $null }
        ScenarioSelectionFpsCoveragePercent = if ($null -ne $selectionFps) { $selectionFps.CoveragePercent } else { $null }
        ScenarioMedianSmoothFps = if ($null -ne $scenarioFps) { $scenarioFps.MedianSmoothFps } else { $null }
        ScenarioP05SmoothFps = if ($null -ne $scenarioFps) { $scenarioFps.P05SmoothFps } else { $null }
        ScenarioFpsCoveragePercent = if ($null -ne $scenarioFps) { $scenarioFps.CoveragePercent } else { $null }
        StartedAtUtc = $startedAt.ToString('o')
        CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        ArtifactRoot = $CaseRoot
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $CaseRoot 'result.json') -Encoding UTF8
    return $result
}

Export-ModuleMember -Function @(
    'Complete-NativeMenuReadyProbe', 'Get-NativeFrameRmse', 'Get-PhaseProcessSummaries', 'Invoke-ShelteredBenchmarkCase', 'New-NativeWindowBitmap',
    'Restore-ShelteredPlatformSession', 'Start-BenchmarkProcessSampler', 'Start-NativeMenuReadyProbe', 'Start-ShelteredPlatformSession',
    'Stop-BenchmarkProcessSampler', 'Stop-NativeMenuReadyProbe', 'Stop-ShelteredPlatformSession', 'Wait-NativeMenuReady', 'Wait-ShelteredPlatformSessionReady'
)
