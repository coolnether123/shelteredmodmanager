Set-StrictMode -Version 2.0

function Resolve-HarnessUri {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [Parameter(Mandatory = $true)][string]$Route,
        [hashtable]$Query = @{}
    )
    if (-not $Route.StartsWith('/')) { $Route = '/' + $Route }
    $pairs = New-Object 'System.Collections.Generic.List[string]'
    foreach ($key in @($Query.Keys | Sort-Object)) {
        if ($null -eq $Query[$key]) { continue }
        $pairs.Add(('{0}={1}' -f [Uri]::EscapeDataString([string]$key), [Uri]::EscapeDataString([string]$Query[$key])))
    }
    $suffix = if ($pairs.Count -gt 0) { '?' + ($pairs -join '&') } else { '' }
    return "http://127.0.0.1:$Port$Route$suffix"
}

function Invoke-ShelteredHarnessRequest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [Parameter(Mandatory = $true)][string]$Route,
        [hashtable]$Query = @{},
        [int]$TimeoutSeconds = 10
    )
    $uri = Resolve-HarnessUri -Port $Port -Route $Route -Query $Query
    return Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec $TimeoutSeconds
}

function Save-ShelteredHarnessJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [Parameter(Mandatory = $true)][string]$Route,
        [Parameter(Mandatory = $true)][string]$Path,
        [hashtable]$Query = @{},
        [int]$TimeoutSeconds = 10
    )
    $response = Invoke-ShelteredHarnessRequest -Port $Port -Route $Route -Query $Query -TimeoutSeconds $TimeoutSeconds
    $response | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $Path -Encoding UTF8
    return $response
}

function Wait-ShelteredHarness {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [int]$TimeoutSeconds = 90,
        [int]$PollMilliseconds = 250
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            $status = Invoke-ShelteredHarnessRequest -Port $Port -Route '/status' -TimeoutSeconds 1
            if ($null -ne $status) { return $status }
        }
        catch { }
        Start-Sleep -Milliseconds $PollMilliseconds
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Harness did not bind port $Port within $TimeoutSeconds seconds."
}

function Test-HarnessMenuReady {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]$Status,
        [string]$MenuReadyRegex = 'MenuScene'
    )
    return (($Status | ConvertTo-Json -Depth 20 -Compress) -match $MenuReadyRegex)
}

function Wait-HarnessMenuReady {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [string]$MenuReadyRegex = 'MenuScene',
        [int]$TimeoutSeconds = 120,
        [int]$PollMilliseconds = 100
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            $status = Invoke-ShelteredHarnessRequest -Port $Port -Route '/status' -TimeoutSeconds 2
            if (Test-HarnessMenuReady -Status $status -MenuReadyRegex $MenuReadyRegex) { return $status }
        }
        catch { }
        Start-Sleep -Milliseconds $PollMilliseconds
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Harness on port $Port did not report a menu matching '$MenuReadyRegex' within $TimeoutSeconds seconds."
}

function Acquire-ShelteredHarnessLease {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][int]$Port, [Parameter(Mandatory = $true)][string]$Owner)
    $response = Invoke-ShelteredHarnessRequest -Port $Port -Route '/agent/lease' -Query @{ owner = $Owner; action = 'acquire' }
    $json = $response | ConvertTo-Json -Depth 10 -Compress
    if ($json -notmatch '"ok"\s*:\s*true') { throw "Harness lease was not acquired: $json" }
    return $response
}

function Release-ShelteredHarnessLease {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][int]$Port, [Parameter(Mandatory = $true)][string]$Owner)
    try {
        return Invoke-ShelteredHarnessRequest -Port $Port -Route '/agent/lease' -Query @{ owner = $Owner; action = 'release' } -TimeoutSeconds 5
    }
    catch { return $null }
}

function Enable-ShelteredBackgroundRun {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][int]$Port)
    return Invoke-ShelteredHarnessRequest -Port $Port -Route '/static-member' -Query @{
        type = 'Application'; name = 'runInBackground'; value = 'true'; write = 'true'
    } -TimeoutSeconds 10
}

function Dismiss-ShelteredBenchmarkMenuBlockers {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][int]$Port)

    $closePath = 'UI Root/ModAPI_SaveDetailsWindow/SaveDetailsWindow/CloseBtn'
    $ui = Invoke-ShelteredHarnessRequest -Port $Port -Route '/ui' -Query @{
        filter = 'ModAPI_SaveDetailsWindow'; inactive = 'false'; components = 'false'; limit = '200'
    } -TimeoutSeconds 10
    $closeButton = @($ui.objects | Where-Object {
        [string]$_.path -eq $closePath -and [bool]$_.activeInHierarchy
    }) | Select-Object -First 1
    if ($null -eq $closeButton) {
        return [pscustomobject]@{ Observed = $false; Dismissed = $false; Path = $closePath; Reason = 'not-visible' }
    }

    $activation = Invoke-ShelteredHarnessRequest -Port $Port -Route '/activate' -Query @{ path = $closePath } -TimeoutSeconds 10
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(5)
    do {
        $remaining = Invoke-ShelteredHarnessRequest -Port $Port -Route '/ui' -Query @{
            filter = 'ModAPI_SaveDetailsWindow'; inactive = 'false'; components = 'false'; limit = '200'
        } -TimeoutSeconds 10
        $stillVisible = @($remaining.objects | Where-Object {
            [string]$_.path -eq $closePath -and [bool]$_.activeInHierarchy
        }).Count -gt 0
        if (-not $stillVisible) {
            return [pscustomobject]@{ Observed = $true; Dismissed = $true; Path = $closePath; Reason = ''; Activation = $activation }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Known benchmark menu blocker '$closePath' remained visible after activation."
}

function Save-ShelteredHarnessScreenshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [Parameter(Mandatory = $true)][string]$Path,
        [ValidateSet('framebuffer', 'client', 'window')][string]$Mode = 'client'
    )
    # Screenshot evidence must never disturb the desktop. Unity framebuffer
    # capture runs in-process and does not need the Sheltered HWND foregrounded.
    # Explicit false values also prevent a future harness default from silently
    # turning stress/benchmark captures into focus-stealing operations.
    $query = @{ activate = 'false'; preserve = 'true' }
    if ($Mode -ne 'framebuffer') { $query.mode = $Mode }
    $uri = Resolve-HarnessUri -Port $Port -Route '/screenshot' -Query $query
    $response = Invoke-WebRequest -Uri $uri -Method Get -TimeoutSec 30 -OutFile $Path -PassThru
    $headers = [ordered]@{}
    foreach ($key in $response.Headers.Keys) { $headers[[string]$key] = [string]$response.Headers[$key] }
    $headers | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ($Path + '.headers.json') -Encoding UTF8
    $signature = @(Get-Content -LiteralPath $Path -Encoding Byte -TotalCount 8)
    $isPng = $signature.Count -eq 8 -and
        $signature[0] -eq 0x89 -and $signature[1] -eq 0x50 -and $signature[2] -eq 0x4E -and $signature[3] -eq 0x47 -and
        $signature[4] -eq 0x0D -and $signature[5] -eq 0x0A -and $signature[6] -eq 0x1A -and $signature[7] -eq 0x0A
    if (-not $isPng) {
        $errorPath = $Path + '.error.json'
        Move-Item -LiteralPath $Path -Destination $errorPath -Force
        throw "Harness screenshot response was not a PNG. Response retained at '$errorPath'."
    }
    return [pscustomobject]@{ Path = $Path; Length = (Get-Item -LiteralPath $Path).Length; Headers = $headers }
}

function Get-SmoothFpsSummary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Samples,
        [double]$MaxRequestMilliseconds = 100,
        [ValidateRange(0, 1)][double]$MinimumValidFraction = 0.70
    )
    $valid = @($Samples | Where-Object { $_.Ok -and $_.RequestMs -lt $MaxRequestMilliseconds -and $null -ne $_.SmoothFps })
    $sorted = @($valid.SmoothFps | Sort-Object)
    if ($sorted.Count -eq 0) {
        return [pscustomobject]@{
            Samples = $Samples.Count; ValidSamples = 0; CoveragePercent = 0; CoverageOk = $false
            MinimumCoveragePercent = $MinimumValidFraction * 100; MedianSmoothFps = $null; P05SmoothFps = $null
            MinSmoothFps = $null; MaxSmoothFps = $null; MeanRequestMs = $null
        }
    }
    return [pscustomobject]@{
        Samples = $Samples.Count
        ValidSamples = $valid.Count
        CoveragePercent = [math]::Round(($valid.Count / [double]$Samples.Count) * 100, 2)
        CoverageOk = ($valid.Count / [double]$Samples.Count) -ge $MinimumValidFraction
        MinimumCoveragePercent = $MinimumValidFraction * 100
        MedianSmoothFps = $sorted[[math]::Floor(($sorted.Count - 1) * 0.50)]
        P05SmoothFps = $sorted[[math]::Floor(($sorted.Count - 1) * 0.05)]
        MinSmoothFps = $sorted[0]
        MaxSmoothFps = $sorted[-1]
        MeanRequestMs = [math]::Round(($valid.RequestMs | Measure-Object -Average).Average, 3)
    }
}

function Measure-ShelteredSmoothFps {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [int]$DurationSeconds = 15,
        [int]$IntervalMilliseconds = 100,
        [Parameter(Mandatory = $true)][string]$CsvPath,
        [Parameter(Mandatory = $true)][string]$SummaryPath,
        [double]$MaxRequestMilliseconds = 100,
        [ValidateRange(0, 1)][double]$MinimumValidFraction = 0.70
    )
    $samples = New-Object 'System.Collections.Generic.List[object]'
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($DurationSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $timestamp = [DateTimeOffset]::UtcNow
        $clock = [Diagnostics.Stopwatch]::StartNew()
        try {
            $response = Invoke-ShelteredHarnessRequest -Port $Port -Route '/static-member' -Query @{ type = 'Time'; name = 'smoothDeltaTime' } -TimeoutSeconds 5
            $clock.Stop()
            $seconds = [double]$response.value
            $samples.Add([pscustomobject]@{
                TimestampUtc = $timestamp.ToString('o'); RequestMs = [math]::Round($clock.Elapsed.TotalMilliseconds, 3)
                SmoothDeltaSeconds = $seconds; SmoothFps = if ($seconds -gt 0) { [math]::Round(1 / $seconds, 3) } else { $null }; Ok = $true; Error = ''
            })
        }
        catch {
            $clock.Stop()
            $samples.Add([pscustomobject]@{
                TimestampUtc = $timestamp.ToString('o'); RequestMs = [math]::Round($clock.Elapsed.TotalMilliseconds, 3)
                SmoothDeltaSeconds = $null; SmoothFps = $null; Ok = $false; Error = $_.Exception.Message
            })
        }
        Start-Sleep -Milliseconds $IntervalMilliseconds
    }
    $samples | Export-Csv -LiteralPath $CsvPath -NoTypeInformation -Encoding UTF8
    $summary = Get-SmoothFpsSummary -Samples $samples.ToArray() -MaxRequestMilliseconds $MaxRequestMilliseconds -MinimumValidFraction $MinimumValidFraction
    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $SummaryPath -Encoding UTF8
    return $summary
}

function Measure-ShelteredScenarioTransition {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [int]$TimeoutSeconds = 120
    )
    $started = [DateTimeOffset]::UtcNow
    $clock = [Diagnostics.Stopwatch]::StartNew()
    try {
        $response = Invoke-ShelteredHarnessRequest -Port $Port -Route '/scenario-book/open' -TimeoutSeconds $TimeoutSeconds
        $clock.Stop()
        $json = $response | ConvertTo-Json -Depth 30 -Compress
        $ok = ($json -match '"ok"\s*:\s*true') -and ($json -match '"visible"\s*:\s*true')
        $result = [pscustomobject]@{
            Ok = $ok; StartedAtUtc = $started.ToString('o'); ElapsedMs = [math]::Round($clock.Elapsed.TotalMilliseconds, 1)
            Response = $response; Error = if ($ok) { '' } else { 'Route did not report ok:true and visible:true.' }
        }
    }
    catch {
        $clock.Stop()
        $result = [pscustomobject]@{
            Ok = $false; StartedAtUtc = $started.ToString('o'); ElapsedMs = [math]::Round($clock.Elapsed.TotalMilliseconds, 1)
            Response = $null; Error = $_.Exception.Message
        }
    }
    $result | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    return $result
}

function Measure-ShelteredScenarioSelectionTransition {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [int]$TimeoutSeconds = 60
    )
    $started = [DateTimeOffset]::UtcNow
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $navigationMutex = [Threading.Mutex]::new($false, 'Global\ShelteredBenchmarkNativeNavigation')
    $navigationLockAcquired = $false
    $navigationWait = [Diagnostics.Stopwatch]::StartNew()
    try {
        try {
            $navigationLockAcquired = $navigationMutex.WaitOne(($TimeoutSeconds + 15) * 1000)
        }
        catch [Threading.AbandonedMutexException] {
            $navigationLockAcquired = $true
        }
        $navigationWait.Stop()
        if (-not $navigationLockAcquired) {
            throw "Timed out waiting for the single-desktop native navigation lease."
        }

        $response = Invoke-ShelteredHarnessRequest -Port $Port -Route '/scenario-selection/open' -TimeoutSeconds $TimeoutSeconds
        $clock.Stop()
        $timing = $response.timing
        $ok = [bool]$response.ok -and [bool]$response.visible
        $reason = [string]$response.reason
        if ([string]::IsNullOrWhiteSpace($reason)) {
            $reason = 'Scenario selection did not become visible.'
        }
        $result = [pscustomobject]@{
            Ok = $ok; StartedAtUtc = $started.ToString('o')
            TotalElapsedMs = if ($null -ne $timing -and $null -ne $timing.elapsedMs) { [double]$timing.elapsedMs } else { [math]::Round($clock.Elapsed.TotalMilliseconds, 1) }
            NativeNavigationWaitMs = [math]::Round($navigationWait.Elapsed.TotalMilliseconds, 1)
            Stage = [string]$response.stage
            ScenarioRootAfterClickMs = if ($null -ne $timing) { $timing.scenarioSlotDispatchToSelectionReadyMs } else { $null }
            SelectionResponse = $response
            Error = if ($ok) { '' } else { $reason }
        }
    }
    catch {
        if ($clock.IsRunning) { $clock.Stop() }
        if ($navigationWait.IsRunning) { $navigationWait.Stop() }
        $result = [pscustomobject]@{
            Ok = $false; StartedAtUtc = $started.ToString('o'); TotalElapsedMs = [math]::Round($clock.Elapsed.TotalMilliseconds, 1)
            NativeNavigationWaitMs = [math]::Round($navigationWait.Elapsed.TotalMilliseconds, 1)
            Stage = 'request'; ScenarioRootAfterClickMs = $null; SelectionResponse = $null; Error = $_.Exception.Message
        }
    }
    finally {
        if ($navigationLockAcquired) {
            try { $navigationMutex.ReleaseMutex() } catch { }
        }
        $navigationMutex.Dispose()
    }
    $result | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    return $result
}

Export-ModuleMember -Function @(
    'Acquire-ShelteredHarnessLease', 'Dismiss-ShelteredBenchmarkMenuBlockers', 'Enable-ShelteredBackgroundRun', 'Get-SmoothFpsSummary',
    'Invoke-ShelteredHarnessRequest', 'Measure-ShelteredScenarioSelectionTransition', 'Measure-ShelteredScenarioTransition', 'Measure-ShelteredSmoothFps',
    'Release-ShelteredHarnessLease', 'Resolve-HarnessUri', 'Save-ShelteredHarnessJson',
    'Save-ShelteredHarnessScreenshot', 'Test-HarnessMenuReady', 'Wait-HarnessMenuReady', 'Wait-ShelteredHarness'
)
