Set-StrictMode -Version 2.0

function Get-ObjectPropertyValue {
    [CmdletBinding()]
    param(
        [AllowNull()]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name,
        $Default = $null
    )

    if ($null -eq $InputObject) { return $Default }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) { return $Default }
    return $property.Value
}

function ConvertTo-PlainHashtable {
    [CmdletBinding()]
    param([AllowNull()]$InputObject)

    if ($null -eq $InputObject) { return $null }
    if ($InputObject -is [System.Collections.IDictionary]) {
        $result = @{}
        foreach ($key in $InputObject.Keys) {
            $result[[string]$key] = ConvertTo-PlainHashtable $InputObject[$key]
        }
        return $result
    }
    if ($InputObject -is [System.Management.Automation.PSCustomObject]) {
        $result = @{}
        foreach ($property in $InputObject.PSObject.Properties) {
            $result[$property.Name] = ConvertTo-PlainHashtable $property.Value
        }
        return $result
    }
    if (($InputObject -is [System.Collections.IEnumerable]) -and -not ($InputObject -is [string])) {
        return @($InputObject | ForEach-Object { ConvertTo-PlainHashtable $_ })
    }
    return $InputObject
}

function Resolve-ConfiguredPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$BasePath
    )

    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Import-ShelteredBenchmarkConfig {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolved = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    $config = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
    $config | Add-Member -NotePropertyName '_configPath' -NotePropertyValue $resolved -Force
    $config | Add-Member -NotePropertyName '_configRoot' -NotePropertyValue (Split-Path -Parent $resolved) -Force
    return $config
}

function Test-ShelteredBenchmarkConfig {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]$Config,
        [switch]$SkipPathChecks
    )

    $errors = New-Object 'System.Collections.Generic.List[string]'
    $warnings = New-Object 'System.Collections.Generic.List[string]'
    if ([int](Get-ObjectPropertyValue $Config 'schemaVersion' 0) -ne 1) {
        $errors.Add('schemaVersion must be 1.')
    }

    $configRoot = [string](Get-ObjectPropertyValue $Config '_configRoot' (Get-Location).Path)
    $outputRoot = [string](Get-ObjectPropertyValue $Config 'outputRoot' '')
    if ([string]::IsNullOrWhiteSpace($outputRoot)) { $errors.Add('outputRoot is required.') }

    $platforms = @(Get-ObjectPropertyValue $Config 'platforms' @())
    if ($platforms.Count -eq 0) { $errors.Add('At least one platform is required.') }
    $platformNames = @{}
    $platformRoots = @{}
    foreach ($platform in $platforms) {
        $name = [string](Get-ObjectPropertyValue $platform 'name' '')
        if ([string]::IsNullOrWhiteSpace($name)) { $errors.Add('Every platform needs a name.'); continue }
        if ($platformNames.ContainsKey($name.ToLowerInvariant())) { $errors.Add("Duplicate platform name '$name'.") }
        $platformNames[$name.ToLowerInvariant()] = $true
        $installRoot = [string](Get-ObjectPropertyValue $platform 'installRoot' '')
        $executable = [string](Get-ObjectPropertyValue $platform 'executable' '')
        $processName = [string](Get-ObjectPropertyValue $platform 'processName' '')
        if ([string]::IsNullOrWhiteSpace($installRoot)) { $errors.Add("Platform '$name' needs installRoot.") }
        if ([string]::IsNullOrWhiteSpace($executable)) { $errors.Add("Platform '$name' needs executable.") }
        if ([string]::IsNullOrWhiteSpace($processName)) { $errors.Add("Platform '$name' needs processName.") }
        if (-not $SkipPathChecks -and -not [string]::IsNullOrWhiteSpace($installRoot)) {
            $resolvedRoot = Resolve-ConfiguredPath $installRoot $configRoot
            if ($platformRoots.ContainsKey($resolvedRoot.ToLowerInvariant())) {
                $errors.Add("Platforms '$name' and '$($platformRoots[$resolvedRoot.ToLowerInvariant()])' target the same install root.")
            }
            else { $platformRoots[$resolvedRoot.ToLowerInvariant()] = $name }
            if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
                $errors.Add("Platform '$name' installRoot does not exist: $resolvedRoot")
            }
            elseif (-not [string]::IsNullOrWhiteSpace($executable) -and
                    -not (Test-Path -LiteralPath (Join-Path $resolvedRoot $executable) -PathType Leaf)) {
                $errors.Add("Platform '$name' executable does not exist: $(Join-Path $resolvedRoot $executable)")
            }
        }
        $prepare = Get-ObjectPropertyValue $platform 'prepare'
        if ($null -ne $prepare -and [bool](Get-ObjectPropertyValue $prepare 'enabled' $false) -and
            [string]::IsNullOrWhiteSpace([string](Get-ObjectPropertyValue $prepare 'executable' ''))) {
            $errors.Add("Platform '$name' prepare command is enabled but has no executable.")
        }
    }

    $validModes = @('vanilla', 'core', 'enabled', 'all', 'explicit')
    $validExecutionModes = @('auto', 'serial', 'parallel-platforms', 'matched-serial')
    $profiles = @(Get-ObjectPropertyValue $Config 'profiles' @())
    if ($profiles.Count -eq 0) { $errors.Add('At least one profile is required.') }
    $profileNames = @{}
    foreach ($profile in $profiles) {
        $name = [string](Get-ObjectPropertyValue $profile 'name' '')
        $mode = [string](Get-ObjectPropertyValue $profile 'mode' '')
        if ([string]::IsNullOrWhiteSpace($name)) { $errors.Add('Every profile needs a name.'); continue }
        if ($profileNames.ContainsKey($name.ToLowerInvariant())) { $errors.Add("Duplicate profile name '$name'.") }
        $profileNames[$name.ToLowerInvariant()] = $true
        if ($validModes -notcontains $mode.ToLowerInvariant()) {
            $errors.Add("Profile '$name' mode must be vanilla, core, enabled, all, or explicit.")
        }
        if ($mode -eq 'explicit' -and @(Get-ObjectPropertyValue $profile 'include' @()).Count -eq 0) {
            $errors.Add("Explicit profile '$name' needs at least one include id.")
        }
        if ($mode -eq 'vanilla' -and [bool](Get-ObjectPropertyValue $profile 'harness' $false)) {
            $errors.Add("Vanilla profile '$name' cannot enable the harness.")
        }
        $executionMode = ([string](Get-ObjectPropertyValue $profile 'executionMode' 'auto')).ToLowerInvariant()
        if ($validExecutionModes -notcontains $executionMode) {
            $errors.Add("Profile '$name' executionMode must be auto, serial, parallel-platforms, or matched-serial.")
        }
        if ($mode -eq 'vanilla' -and $executionMode -eq 'parallel-platforms') {
            $errors.Add("Vanilla profile '$name' cannot use parallel-platforms because this Unity build pauses when unfocused.")
        }
    }
    $requiresInstrumentation = @($profiles | Where-Object {
        [bool](Get-ObjectPropertyValue $_ 'enabled' $true) -and [bool](Get-ObjectPropertyValue $_ 'harness' (([string](Get-ObjectPropertyValue $_ 'mode' '') -ne 'vanilla')))
    }).Count -gt 0
    if ($requiresInstrumentation) {
        foreach ($platform in $platforms | Where-Object { [bool](Get-ObjectPropertyValue $_ 'enabled' $true) }) {
            $platformName = [string](Get-ObjectPropertyValue $platform 'name' '')
            $gates = @(Get-ObjectPropertyValue $platform 'hashGates' @())
            $roles = @($gates | ForEach-Object { ([string](Get-ObjectPropertyValue $_ 'role' '')).ToLowerInvariant() })
            foreach ($requiredRole in @('modapi', 'shelteredapi', 'harness')) {
                if ($roles -notcontains $requiredRole) { $errors.Add("Platform '$platformName' needs a '$requiredRole' deployment hash gate for harness profiles.") }
            }
            foreach ($gate in $gates) {
                $gateName = [string](Get-ObjectPropertyValue $gate 'name' (Get-ObjectPropertyValue $gate 'role' 'unnamed'))
                if ([string]::IsNullOrWhiteSpace([string](Get-ObjectPropertyValue $gate 'deployedPath' ''))) {
                    $errors.Add("Platform '$platformName' hash gate '$gateName' needs deployedPath.")
                }
                if ([string]::IsNullOrWhiteSpace([string](Get-ObjectPropertyValue $gate 'sourcePath' '')) -and
                    [string]::IsNullOrWhiteSpace([string](Get-ObjectPropertyValue $gate 'sha256' ''))) {
                    $errors.Add("Platform '$platformName' hash gate '$gateName' needs sourcePath or sha256.")
                }
            }
        }
    }

    $sampling = Get-ObjectPropertyValue $Config 'sampling'
    $interval = [int](Get-ObjectPropertyValue $sampling 'processIntervalMilliseconds' 100)
    if ($interval -lt 50) { $errors.Add('sampling.processIntervalMilliseconds must be at least 50.') }
    if ($interval -lt 100) { $warnings.Add('Sampling below 100 ms can materially perturb this Unity build.') }
    if ([int](Get-ObjectPropertyValue $sampling 'startupTimeoutSeconds' 120) -lt 1) { $errors.Add('sampling.startupTimeoutSeconds must be positive.') }
    if ([int](Get-ObjectPropertyValue $sampling 'scenarioTimeoutSeconds' 120) -lt 1) { $errors.Add('sampling.scenarioTimeoutSeconds must be positive.') }
    if ([int](Get-ObjectPropertyValue $sampling 'fpsDurationSeconds' 15) -lt 1) { $errors.Add('sampling.fpsDurationSeconds must be positive.') }
    if ([int](Get-ObjectPropertyValue $sampling 'fpsIntervalMilliseconds' 100) -lt 25) { $errors.Add('sampling.fpsIntervalMilliseconds must be at least 25.') }
    $minimumCoverage = [double](Get-ObjectPropertyValue $sampling 'minimumFpsCoveragePercent' 70)
    if ($minimumCoverage -lt 1 -or $minimumCoverage -gt 100) { $errors.Add('sampling.minimumFpsCoveragePercent must be between 1 and 100.') }
    $iterations = [int](Get-ObjectPropertyValue $Config 'iterations' 1)
    if ($iterations -lt 1) { $errors.Add('iterations must be at least 1.') }
    $build = Get-ObjectPropertyValue $Config 'build'
    if ($null -ne $build -and [bool](Get-ObjectPropertyValue $build 'enabled' $false) -and
        [string]::IsNullOrWhiteSpace([string](Get-ObjectPropertyValue $build 'executable' ''))) {
        $errors.Add('build is enabled but has no executable.')
    }

    return [pscustomobject]@{
        Valid = ($errors.Count -eq 0)
        Errors = $errors.ToArray()
        Warnings = $warnings.ToArray()
    }
}

function Resolve-BenchmarkExecution {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]$Profile,
        [bool]$ParallelPlatforms,
        [int]$PlatformCount = 1,
        [switch]$ForceMatchedSerial
    )

    $mode = ([string](Get-ObjectPropertyValue $Profile 'mode' '')).ToLowerInvariant()
    $configured = ([string](Get-ObjectPropertyValue $Profile 'executionMode' 'auto')).ToLowerInvariant()
    if ($ForceMatchedSerial) { $configured = 'matched-serial' }

    $executionMode = switch ($configured) {
        'matched-serial' { 'matched-serial'; break }
        'serial' { 'serial'; break }
        'parallel-platforms' {
            if ($mode -eq 'vanilla') { throw 'Vanilla cannot use parallel-platforms execution.' }
            if ($PlatformCount -gt 1) { 'parallel-platforms' } else { 'serial' }
            break
        }
        'auto' {
            $harnessEnabled = [bool](Get-ObjectPropertyValue $Profile 'harness' ($mode -ne 'vanilla'))
            if ($mode -ne 'vanilla' -and $harnessEnabled -and $ParallelPlatforms -and $PlatformCount -gt 1) {
                'parallel-platforms'
            }
            else { 'serial' }
            break
        }
        default { throw "Unknown benchmark execution mode '$configured'." }
    }

    $comparisonLane = [string](Get-ObjectPropertyValue $Profile 'comparisonLane' '')
    if ($ForceMatchedSerial) { $comparisonLane = 'matched-serial' }
    elseif ([string]::IsNullOrWhiteSpace($comparisonLane) -and $executionMode -eq 'matched-serial') { $comparisonLane = 'matched-serial' }

    return [pscustomobject]@{
        ExecutionMode = $executionMode
        ComparisonLane = $comparisonLane
    }
}

function Get-InstalledModCatalog {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$InstallRoot)

    $modsRoot = Join-Path $InstallRoot 'mods'
    if (-not (Test-Path -LiteralPath $modsRoot -PathType Container)) { return @() }
    $catalog = New-Object 'System.Collections.Generic.List[object]'
    foreach ($directory in @(Get-ChildItem -LiteralPath $modsRoot -Directory | Sort-Object Name)) {
        if ($directory.Name.StartsWith('_', [StringComparison]::Ordinal)) { continue }
        $aboutPath = Join-Path $directory.FullName 'About\About.json'
        if (-not (Test-Path -LiteralPath $aboutPath -PathType Leaf)) { continue }
        try {
            $about = Get-Content -LiteralPath $aboutPath -Raw | ConvertFrom-Json
            $id = [string](Get-ObjectPropertyValue $about 'id' '')
            if ([string]::IsNullOrWhiteSpace($id)) { continue }
            $catalog.Add([pscustomobject]@{
                Id = $id
                Name = [string](Get-ObjectPropertyValue $about 'name' $directory.Name)
                Version = [string](Get-ObjectPropertyValue $about 'version' '')
                Directory = $directory.FullName
                AboutPath = $aboutPath
                DependsOn = @((Get-ObjectPropertyValue $about 'dependsOn' @()) | ForEach-Object { [string]$_ })
                LoadAfter = @((Get-ObjectPropertyValue $about 'loadAfter' @()) | ForEach-Object { [string]$_ })
                LoadBefore = @((Get-ObjectPropertyValue $about 'loadBefore' @()) | ForEach-Object { [string]$_ })
            })
        }
        catch {
            throw "Invalid mod manifest '$aboutPath': $($_.Exception.Message)"
        }
    }
    return $catalog.ToArray()
}

function Resolve-ShelteredModProfile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]$Profile,
        [Parameter(Mandatory = $true)][object[]]$Catalog,
        [string[]]$ExistingOrder = @(),
        [string[]]$CoreModIds = @('com.harmony.0harmony', 'coolnether123.shelteredagentinterface'),
        [string[]]$ExistingEnabledIds = @()
    )

    $mode = ([string](Get-ObjectPropertyValue $Profile 'mode' '')).ToLowerInvariant()
    if ($mode -eq 'vanilla') { return @() }
    $byId = @{}
    foreach ($mod in $Catalog) { $byId[$mod.Id.ToLowerInvariant()] = $mod }
    $requested = New-Object 'System.Collections.Generic.List[string]'
    if ($mode -eq 'all') {
        foreach ($mod in $Catalog) { $requested.Add($mod.Id) }
    }
    elseif ($mode -eq 'enabled') {
        $enabledSource = if ($ExistingEnabledIds.Count -gt 0) { $ExistingEnabledIds } else { $ExistingOrder }
        foreach ($id in $enabledSource) { $requested.Add([string]$id) }
    }
    elseif ($mode -eq 'explicit') {
        foreach ($id in @(Get-ObjectPropertyValue $Profile 'include' @())) { $requested.Add([string]$id) }
    }
    else {
        foreach ($id in $CoreModIds) { $requested.Add($id) }
    }
    if ([bool](Get-ObjectPropertyValue $Profile 'harness' ($mode -ne 'vanilla'))) {
        foreach ($id in $CoreModIds) { $requested.Add($id) }
    }
    foreach ($id in @(Get-ObjectPropertyValue $Profile 'include' @())) { $requested.Add([string]$id) }

    $excluded = @{}
    foreach ($id in @(Get-ObjectPropertyValue $Profile 'exclude' @())) { $excluded[[string]$id.ToLowerInvariant()] = $true }
    $selected = @{}
    $pending = New-Object 'System.Collections.Generic.Queue[string]'
    foreach ($id in $requested) { if (-not [string]::IsNullOrWhiteSpace($id)) { $pending.Enqueue($id) } }
    while ($pending.Count -gt 0) {
        $id = $pending.Dequeue()
        $key = $id.ToLowerInvariant()
        if ($excluded.ContainsKey($key) -or $selected.ContainsKey($key)) { continue }
        if (-not $byId.ContainsKey($key)) { throw "Profile references mod '$id', but it is not installed." }
        $selected[$key] = $byId[$key]
        if ([bool](Get-ObjectPropertyValue $Profile 'includeDependencies' $true)) {
            foreach ($dependency in $byId[$key].DependsOn) { $pending.Enqueue($dependency) }
        }
    }

    foreach ($coreId in $CoreModIds) {
        if ([bool](Get-ObjectPropertyValue $Profile 'harness' ($mode -ne 'vanilla')) -and $excluded.ContainsKey($coreId.ToLowerInvariant())) {
            throw "Profile excludes required harness/core mod '$coreId'. Set harness=false if that is intentional."
        }
    }

    $ordered = New-Object 'System.Collections.Generic.List[string]'
    foreach ($id in @($ExistingOrder) + @($CoreModIds) + @($requested) + @($Catalog | ForEach-Object Id)) {
        $key = ([string]$id).ToLowerInvariant()
        if ($selected.ContainsKey($key) -and -not ($ordered | Where-Object { $_ -ieq $selected[$key].Id })) {
            $ordered.Add($selected[$key].Id)
        }
    }

    # Stable dependency relaxation keeps familiar existing order while honoring declared relationships.
    for ($pass = 0; $pass -lt ($ordered.Count * $ordered.Count); $pass++) {
        $changed = $false
        for ($index = 0; $index -lt $ordered.Count; $index++) {
            $id = $ordered[$index]
            $mod = $byId[$id.ToLowerInvariant()]
            foreach ($predecessor in @($mod.DependsOn) + @($mod.LoadAfter)) {
                $predecessorId = $ordered | Where-Object { $_ -ieq $predecessor } | Select-Object -First 1
                $before = if ($null -ne $predecessorId) { $ordered.IndexOf($predecessorId) } else { -1 }
                $current = $ordered.IndexOf($id)
                if ($before -ge 0 -and $before -gt $current) {
                    $ordered.RemoveAt($before)
                    $ordered.Insert($current, $predecessor)
                    $changed = $true
                }
            }
            foreach ($successor in $mod.LoadBefore) {
                $afterId = $ordered | Where-Object { $_ -ieq $successor } | Select-Object -First 1
                if ($null -ne $afterId) {
                    $current = $ordered.IndexOf($id)
                    $after = $ordered.IndexOf($afterId)
                    if ($after -lt $current) {
                        $ordered.RemoveAt($current)
                        $ordered.Insert($after, $id)
                        $changed = $true
                    }
                }
            }
        }
        if (-not $changed) { break }
    }
    return $ordered.ToArray()
}

function Get-LoadOrderState {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$InstallRoot)
    $path = Join-Path $InstallRoot 'mods\loadorder.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Get-EnabledLoadOrderIds {
    [CmdletBinding()]
    param([AllowNull()]$State)
    if ($null -eq $State) { return @() }
    $mods = Get-ObjectPropertyValue $State 'mods'
    if ($null -eq $mods) { return @() }
    $enabled = @{}
    foreach ($property in $mods.PSObject.Properties) {
        if ([bool](Get-ObjectPropertyValue $property.Value 'enabled' $false)) { $enabled[$property.Name.ToLowerInvariant()] = $property.Name }
    }
    $result = New-Object 'System.Collections.Generic.List[string]'
    foreach ($id in @(Get-ObjectPropertyValue $State 'order' @())) {
        $key = ([string]$id).ToLowerInvariant()
        if ($enabled.ContainsKey($key)) { $result.Add($enabled[$key]); $enabled.Remove($key) }
    }
    foreach ($id in @($enabled.Values | Sort-Object)) { $result.Add($id) }
    return $result.ToArray()
}

function Set-ShelteredLoadOrder {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$InstallRoot,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$ModIds
    )
    $modsRoot = Join-Path $InstallRoot 'mods'
    if (-not (Test-Path -LiteralPath $modsRoot)) { New-Item -ItemType Directory -Path $modsRoot -Force | Out-Null }
    $states = [ordered]@{}
    foreach ($id in $ModIds) { $states[$id] = [ordered]@{ enabled = $true } }
    $document = [ordered]@{ order = @($ModIds); mods = $states }
    $document | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $modsRoot 'loadorder.json') -Encoding UTF8
}

function Set-DoorstopEnabled {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$InstallRoot,
        [Parameter(Mandatory = $true)][bool]$Enabled
    )
    $path = Join-Path $InstallRoot 'doorstop_config.ini'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing Doorstop configuration: $path" }
    $content = Get-Content -LiteralPath $path -Raw
    $replacement = if ($Enabled) { 'enabled=true' } else { 'enabled=false' }
    if ($content -notmatch '(?im)^\s*enabled\s*=') { throw "Doorstop configuration has no enabled setting: $path" }
    $updated = [regex]::Replace($content, '(?im)^\s*enabled\s*=\s*(true|false)\s*$', $replacement, 1)
    Set-Content -LiteralPath $path -Value $updated -Encoding UTF8
}

function Set-ShelteredManagerOptions {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$InstallRoot,
        [Parameter(Mandatory = $true)]$Overrides
    )
    $table = ConvertTo-PlainHashtable $Overrides
    if ($null -eq $table -or $table.Count -eq 0) { return }
    $path = Join-Path $InstallRoot 'SMM\bin\manager_options.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing manager options: $path" }
    $document = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $seen = @{}
    foreach ($option in @(Get-ObjectPropertyValue $document 'booleans' @())) {
        $id = [string](Get-ObjectPropertyValue $option 'id' '')
        if ($table.ContainsKey($id)) {
            $option.value = [bool]$table[$id]
            $seen[$id] = $true
        }
    }
    foreach ($id in $table.Keys) {
        if (-not $seen.ContainsKey($id)) { throw "Manager option '$id' is not present in $path" }
    }
    $document | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $path -Encoding UTF8
}

function New-InstallStateSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$InstallRoot,
        [Parameter(Mandatory = $true)][string]$BackupRoot
    )
    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
    $relativePaths = @('doorstop_config.ini', 'mods\loadorder.json', 'SMM\bin\manager_options.json')
    $entries = New-Object 'System.Collections.Generic.List[object]'
    foreach ($relativePath in $relativePaths) {
        $source = Join-Path $InstallRoot $relativePath
        $safeName = $relativePath.Replace('\', '__').Replace('/', '__')
        $backup = Join-Path $BackupRoot $safeName
        $exists = Test-Path -LiteralPath $source -PathType Leaf
        if ($exists) { Copy-Item -LiteralPath $source -Destination $backup -Force }
        $entries.Add([pscustomobject]@{
            RelativePath = $relativePath
            Existed = $exists
            BackupPath = $backup
            Sha256 = if ($exists) { (Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash } else { $null }
        })
    }
    $snapshot = [pscustomobject]@{
        InstallRoot = $InstallRoot
        CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        Entries = $entries.ToArray()
    }
    $snapshot | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $BackupRoot 'snapshot.json') -Encoding UTF8
    return $snapshot
}

function Restore-InstallStateSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)]$Snapshot)
    $errors = New-Object 'System.Collections.Generic.List[string]'
    foreach ($entry in @($Snapshot.Entries)) {
        $target = Join-Path $Snapshot.InstallRoot $entry.RelativePath
        try {
            if ([bool]$entry.Existed) {
                $parent = Split-Path -Parent $target
                if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
                Copy-Item -LiteralPath $entry.BackupPath -Destination $target -Force
                $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
                if ($actual -ne [string]$entry.Sha256) { throw "Hash mismatch after restore. Expected $($entry.Sha256), got $actual." }
            }
            elseif (Test-Path -LiteralPath $target) {
                Remove-Item -LiteralPath $target -Force
                if (Test-Path -LiteralPath $target) { throw 'Originally absent file still exists after restore.' }
            }
        }
        catch { $errors.Add("$($entry.RelativePath): $($_.Exception.Message)") }
    }
    if ($errors.Count -gt 0) {
        throw "Install state restoration left residual differences: $($errors.ToArray() -join '; ')"
    }
}

function Enter-BenchmarkInstallLocks {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string[]]$InstallRoots)
    $locks = New-Object 'System.Collections.Generic.List[object]'
    try {
        foreach ($root in @($InstallRoots | ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\').ToLowerInvariant() } | Sort-Object -Unique)) {
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($root)))).Replace('-', '') }
            finally { $sha.Dispose() }
            $name = 'Global\ShelteredBenchmark_' + $hash
            $mutex = New-Object Threading.Mutex($false, $name)
            $acquired = $false
            try { $acquired = $mutex.WaitOne(0) }
            catch [Threading.AbandonedMutexException] { $acquired = $true }
            if (-not $acquired) { $mutex.Dispose(); throw "Another benchmark owns install '$root' (mutex $name)." }
            $locks.Add([pscustomobject]@{ InstallRoot = $root; Name = $name; Mutex = $mutex })
        }
        return $locks.ToArray()
    }
    catch {
        foreach ($lock in $locks) { try { $lock.Mutex.ReleaseMutex(); $lock.Mutex.Dispose() } catch { } }
        throw
    }
}

function Exit-BenchmarkInstallLocks {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Locks)
    foreach ($lock in @($Locks | Sort-Object Name -Descending)) {
        try { $lock.Mutex.ReleaseMutex() } finally { $lock.Mutex.Dispose() }
    }
}

function Test-BenchmarkDeploymentHashes {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][object[]]$Gates,
        [Parameter(Mandatory = $true)][string]$InstallRoot,
        [Parameter(Mandatory = $true)][string]$ConfigRoot,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )
    $rows = New-Object 'System.Collections.Generic.List[object]'
    foreach ($gate in $Gates) {
        $name = [string](Get-ObjectPropertyValue $gate 'name' 'unnamed')
        $deployedSetting = [string](Get-ObjectPropertyValue $gate 'deployedPath' '')
        $sourceSetting = [string](Get-ObjectPropertyValue $gate 'sourcePath' '')
        $expectedSetting = [string](Get-ObjectPropertyValue $gate 'sha256' '')
        $deployed = if ([IO.Path]::IsPathRooted($deployedSetting)) { $deployedSetting } else { Join-Path $InstallRoot $deployedSetting }
        $source = if ([string]::IsNullOrWhiteSpace($sourceSetting)) { $null } elseif ([IO.Path]::IsPathRooted($sourceSetting)) { $sourceSetting } else { Resolve-ConfiguredPath $sourceSetting $ConfigRoot }
        $deployedExists = Test-Path -LiteralPath $deployed -PathType Leaf
        $sourceExists = $null -ne $source -and (Test-Path -LiteralPath $source -PathType Leaf)
        $deployedHash = if ($deployedExists) { (Get-FileHash -LiteralPath $deployed -Algorithm SHA256).Hash } else { $null }
        $sourceHash = if ($sourceExists) { (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash } else { $null }
        $expectedHash = if (-not [string]::IsNullOrWhiteSpace($expectedSetting)) { $expectedSetting.ToUpperInvariant() } else { $sourceHash }
        $ok = $deployedExists -and -not [string]::IsNullOrWhiteSpace($expectedHash) -and $deployedHash -eq $expectedHash
        $reason = if (-not $deployedExists) { 'deployed file missing' } elseif ($null -ne $source -and -not $sourceExists) { 'source file missing' } elseif ([string]::IsNullOrWhiteSpace($expectedHash)) { 'no sourcePath or sha256 expectation' } elseif (-not $ok) { 'deployed hash differs from required hash' } else { '' }
        $rows.Add([pscustomobject]@{
            Name = $name; Ok = $ok; Reason = $reason; DeployedPath = $deployed; DeployedSha256 = $deployedHash
            SourcePath = $source; SourceSha256 = $sourceHash; RequiredSha256 = $expectedHash
        })
    }
    $result = [pscustomobject]@{ Ok = @($rows | Where-Object { -not $_.Ok }).Count -eq 0; Gates = $rows.ToArray() }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    if (-not $result.Ok) {
        $reasons = @($rows | Where-Object { -not $_.Ok } | ForEach-Object { "$($_.Name): $($_.Reason)" }) -join '; '
        throw "Deployment hash gate failed: $reasons. See $OutputPath"
    }
    return $result
}

function Get-FileFingerprint {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path, [string]$Role = '')
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $item = Get-Item -LiteralPath $Path
    $version = $item.VersionInfo
    return [pscustomobject]@{
        Role = $Role
        Path = $item.FullName
        Length = $item.Length
        LastWriteUtc = $item.LastWriteTimeUtc.ToString('o')
        Sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
        FileVersion = $version.FileVersion
        ProductVersion = $version.ProductVersion
    }
}

function Test-BenchmarkMutableModPath {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$RelativePath, [string[]]$Patterns = @())
    $normalized = $RelativePath.Replace('\', '/')
    foreach ($pattern in $Patterns) {
        if ([string]::IsNullOrWhiteSpace($pattern)) { continue }
        if ($normalized -like $pattern.Replace('\', '/')) { return $true }
    }
    return $false
}

function Get-BenchmarkEnvironment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)]$Platform,
        [string[]]$SelectedModIds = @(),
        [string[]]$MutableModRelativePathPatterns = @()
    )
    $installRoot = [string]$Platform.installRoot
    $files = New-Object 'System.Collections.Generic.List[object]'
    $candidates = @(
        @{ Path = (Join-Path $installRoot ([string]$Platform.executable)); Role = 'game-executable' },
        @{ Path = (Join-Path $installRoot 'doorstop_config.ini'); Role = 'doorstop-config' },
        @{ Path = (Join-Path $installRoot 'winhttp.dll'); Role = 'doorstop-proxy' },
        @{ Path = (Join-Path $installRoot 'SMM\bin\Doorstop.dll'); Role = 'doorstop-managed' },
        @{ Path = (Join-Path $installRoot 'SMM\ModAPI.dll'); Role = 'modapi' },
        @{ Path = (Join-Path $installRoot 'SMM\bin\ShelteredAPI.dll'); Role = 'shelteredapi' },
        @{ Path = (Join-Path $installRoot 'mods\loadorder.json'); Role = 'loadorder' }
    )
    foreach ($candidate in $candidates) {
        $fingerprint = Get-FileFingerprint -Path $candidate.Path -Role $candidate.Role
        if ($null -ne $fingerprint) { $files.Add($fingerprint) }
    }
    $catalog = Get-InstalledModCatalog $installRoot
    $selectedModDirectories = New-Object 'System.Collections.Generic.List[string]'
    foreach ($id in $SelectedModIds) {
        $mod = $catalog | Where-Object Id -IEQ $id | Select-Object -First 1
        if ($null -eq $mod) { continue }
        $selectedModDirectories.Add([IO.Path]::GetFullPath($mod.Directory))
        foreach ($path in @(Get-ChildItem -LiteralPath $mod.Directory -Recurse -File | Where-Object { $_.Extension -in @('.dll', '.json') } | Select-Object -ExpandProperty FullName)) {
            $relativePath = $path.Substring($mod.Directory.Length).TrimStart('\', '/')
            if (Test-BenchmarkMutableModPath -RelativePath $relativePath -Patterns $MutableModRelativePathPatterns) { continue }
            $fingerprint = Get-FileFingerprint -Path $path -Role "mod:$id"
            if ($null -ne $fingerprint) { $files.Add($fingerprint) }
        }
    }
    $gitCommit = (& git -C $RepositoryRoot rev-parse HEAD 2>$null | Select-Object -First 1)
    $gitBranch = (& git -C $RepositoryRoot branch --show-current 2>$null | Select-Object -First 1)
    $gitStatus = @(& git -C $RepositoryRoot status --short 2>$null)
    # Fingerprinting must be byte-stable and must not promote autocrlf advice
    # written to native stderr into a PowerShell ErrorAction=Stop failure.
    $diffText = (& git -c core.autocrlf=false -C $RepositoryRoot diff --binary --no-ext-diff 2>$null) -join "`n"
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $diffHash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($diffText)))).Replace('-', '') }
    finally { $sha.Dispose() }
    $os = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue
    $cpu = Get-CimInstance Win32_Processor -ErrorAction SilentlyContinue | Select-Object -First 1
    $gpu = @(Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue | Select-Object Name, DriverVersion, AdapterRAM)
    return [pscustomobject]@{
        CapturedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        Platform = [string]$Platform.name
        Architecture = [string](Get-ObjectPropertyValue $Platform 'architecture' '')
        InstallRoot = $installRoot
        Git = [pscustomobject]@{ Commit = $gitCommit; Branch = $gitBranch; Status = $gitStatus; WorkingDiffSha256 = $diffHash }
        System = [pscustomobject]@{
            Os = if ($null -ne $os) { "$($os.Caption) $($os.Version)" } else { $null }
            Cpu = if ($null -ne $cpu) { $cpu.Name } else { $null }
            LogicalProcessors = if ($null -ne $cpu) { $cpu.NumberOfLogicalProcessors } else { $null }
            MemoryBytes = if ($null -ne $os) { [long]$os.TotalVisibleMemorySize * 1KB } else { $null }
            Gpu = $gpu
        }
        Files = $files.ToArray()
        SelectedModIds = @($SelectedModIds)
        SelectedModDirectories = $selectedModDirectories.ToArray()
        MutableModRelativePathPatterns = @($MutableModRelativePathPatterns)
    }
}

function Test-BenchmarkFileManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][object[]]$ExpectedFiles,
        [string[]]$SelectedModDirectories = @(),
        [string[]]$MutableModRelativePathPatterns = @(),
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    $expectedByPath = @{}
    foreach ($file in $ExpectedFiles) {
        $path = [IO.Path]::GetFullPath([string]$file.Path)
        $expectedByPath[$path.ToLowerInvariant()] = $file
    }
    $actualPaths = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $ExpectedFiles) { [void]$actualPaths.Add([IO.Path]::GetFullPath([string]$file.Path)) }
    foreach ($directory in $SelectedModDirectories) {
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) { continue }
        foreach ($item in @(Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object { $_.Extension -in @('.dll', '.json') })) {
            $relativePath = $item.FullName.Substring($directory.Length).TrimStart('\', '/')
            if (Test-BenchmarkMutableModPath -RelativePath $relativePath -Patterns $MutableModRelativePathPatterns) { continue }
            [void]$actualPaths.Add($item.FullName)
        }
    }

    $rows = New-Object 'System.Collections.Generic.List[object]'
    foreach ($path in @($actualPaths | Sort-Object)) {
        $key = $path.ToLowerInvariant()
        $expected = if ($expectedByPath.ContainsKey($key)) { $expectedByPath[$key] } else { $null }
        $exists = Test-Path -LiteralPath $path -PathType Leaf
        $actualHash = if ($exists) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { $null }
        $reason = if ($null -eq $expected) { 'unexpected selected-mod file' } elseif (-not $exists) { 'file missing' } elseif ($actualHash -ne [string]$expected.Sha256) { 'hash changed' } else { '' }
        $rows.Add([pscustomobject]@{
            Path = $path
            Role = if ($null -ne $expected) { [string]$expected.Role } else { 'selected-mod-unexpected' }
            ExpectedSha256 = if ($null -ne $expected) { [string]$expected.Sha256 } else { $null }
            ActualSha256 = $actualHash
            Ok = [string]::IsNullOrWhiteSpace($reason)
            Reason = $reason
        })
    }
    foreach ($expected in $ExpectedFiles) {
        $path = [IO.Path]::GetFullPath([string]$expected.Path)
        if ($actualPaths.Contains($path)) { continue }
        $rows.Add([pscustomobject]@{ Path = $path; Role = [string]$expected.Role; ExpectedSha256 = [string]$expected.Sha256; ActualSha256 = $null; Ok = $false; Reason = 'file missing' })
    }
    $result = [pscustomobject]@{ Ok = @($rows | Where-Object { -not $_.Ok }).Count -eq 0; Files = $rows.ToArray() }
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    if (-not $result.Ok) {
        $reasons = @($rows | Where-Object { -not $_.Ok } | ForEach-Object { "$($_.Role): $($_.Reason) ($($_.Path))" }) -join '; '
        throw "Benchmark file manifest changed during the case: $reasons. See $OutputPath"
    }
    return $result
}

function Get-Percentile {
    [CmdletBinding()]
    param([double[]]$Values, [ValidateRange(0, 1)][double]$Fraction)
    $sorted = @($Values | Where-Object { $null -ne $_ } | Sort-Object)
    if ($sorted.Count -eq 0) { return $null }
    if ($sorted.Count -eq 1) { return $sorted[0] }
    $position = ($sorted.Count - 1) * $Fraction
    $lowerIndex = [int][math]::Floor($position)
    $upperIndex = [int][math]::Ceiling($position)
    if ($lowerIndex -eq $upperIndex) { return $sorted[$lowerIndex] }
    $weight = $position - $lowerIndex
    return [math]::Round(([double]$sorted[$lowerIndex] * (1 - $weight)) + ([double]$sorted[$upperIndex] * $weight), 3)
}

function Get-ProcessSampleSummary {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Samples, [string]$Phase = '')
    $selected = @(if ([string]::IsNullOrWhiteSpace($Phase)) { $Samples } else { $Samples | Where-Object Phase -EQ $Phase })
    if ($selected.Count -eq 0) { return $null }
    $cpuDelta = [double]$selected[-1].CpuSeconds - [double]$selected[0].CpuSeconds
    return [pscustomobject]@{
        Phase = if ($Phase) { $Phase } else { 'all' }
        Samples = $selected.Count
        DurationMs = [math]::Round(([double]$selected[-1].ElapsedMs - [double]$selected[0].ElapsedMs), 1)
        CpuSeconds = [math]::Round($cpuDelta, 3)
        MeanWorkingSetMiB = [math]::Round((($selected.WorkingSetBytes | Measure-Object -Average).Average / 1MB), 1)
        PeakWorkingSetMiB = [math]::Round((($selected.WorkingSetBytes | Measure-Object -Maximum).Maximum / 1MB), 1)
        MeanPrivateMiB = [math]::Round((($selected.PrivateBytes | Measure-Object -Average).Average / 1MB), 1)
        PeakPrivateMiB = [math]::Round((($selected.PrivateBytes | Measure-Object -Maximum).Maximum / 1MB), 1)
        PeakThreads = ($selected.Threads | Measure-Object -Maximum).Maximum
        PeakHandles = ($selected.Handles | Measure-Object -Maximum).Maximum
    }
}

function Invoke-BenchmarkCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]$Command,
        [Parameter(Mandatory = $true)][string]$ConfigRoot,
        [Parameter(Mandatory = $true)][string]$DefaultWorkingDirectory,
        [Parameter(Mandatory = $true)][string]$LogPath
    )
    if (-not [bool](Get-ObjectPropertyValue $Command 'enabled' $true)) { return $null }
    $executable = [string](Get-ObjectPropertyValue $Command 'executable' '')
    if ([string]::IsNullOrWhiteSpace($executable)) { throw 'Configured command requires executable.' }
    $arguments = @((Get-ObjectPropertyValue $Command 'arguments' @()) | ForEach-Object { [string]$_ })
    $workingSetting = [string](Get-ObjectPropertyValue $Command 'workingDirectory' $DefaultWorkingDirectory)
    $workingDirectory = Resolve-ConfiguredPath -Path $workingSetting -BasePath $ConfigRoot
    @(
        "Executable: $executable"
        "Arguments: $($arguments -join ' ')"
        "Working directory: $workingDirectory"
        "Started UTC: $([DateTimeOffset]::UtcNow.ToString('o'))"
    ) | Set-Content -LiteralPath $LogPath -Encoding UTF8
    Push-Location $workingDirectory
    try {
        $output = & $executable @arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally { Pop-Location }
    $output | Add-Content -LiteralPath $LogPath -Encoding UTF8
    if ($exitCode -ne 0) { throw "Configured command failed with exit code $exitCode. See $LogPath" }
    return [pscustomobject]@{ ExitCode = $exitCode; LogPath = $LogPath }
}

function Export-ShelteredStartupTimings {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][string]$CsvPath,
        [Parameter(Mandatory = $true)][string]$SummaryPath
    )
    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) { return @() }
    $rows = New-Object 'System.Collections.Generic.List[object]'
    $pattern = '^\[(?<time>[^\]]+)\].*\[StartupTiming\]\s+(?<operation>.+?)\s+took\s+(?<milliseconds>[0-9]+(?:\.[0-9]+)?)ms\.?\s*$'
    foreach ($line in Get-Content -LiteralPath $LogPath) {
        if ($line -match $pattern) {
            $rows.Add([pscustomobject]@{
                LocalTime = $Matches.time
                Operation = $Matches.operation
                ElapsedMs = [double]$Matches.milliseconds
            })
        }
    }
    $rows.ToArray() | Export-Csv -LiteralPath $CsvPath -NoTypeInformation -Encoding UTF8
    $top = @($rows | Sort-Object ElapsedMs -Descending | Select-Object -First 20)
    [pscustomobject]@{
        Count = $rows.Count
        Note = 'StartupTiming entries can be nested; do not add parent and child durations.'
        Top = $top
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $SummaryPath -Encoding UTF8
    return $rows.ToArray()
}

function Write-BenchmarkAggregateReport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][object[]]$Results,
        [Parameter(Mandatory = $true)][string]$OutputRoot,
        [Parameter(Mandatory = $true)]$RunMetadata
    )
    $csvPath = Join-Path $OutputRoot 'results.csv'
    $Results | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
    $summaryRows = New-Object 'System.Collections.Generic.List[object]'
    foreach ($group in @($Results | Group-Object -Property Platform, Profile, ExecutionMode, ComparisonLane)) {
        $items = @($group.Group)
        $validCaseItems = @($items | Where-Object { [string](Get-ObjectPropertyValue $_ 'Status' '') -ne 'failed' })
        $startup = @($validCaseItems | ForEach-Object { Get-ObjectPropertyValue $_ 'StartupMs' } | Where-Object { $null -ne $_ })
        $harnessStartup = @($validCaseItems | ForEach-Object { Get-ObjectPropertyValue $_ 'HarnessMenuReadyMs' } | Where-Object { $null -ne $_ })
        $cpu = @($validCaseItems | ForEach-Object { Get-ObjectPropertyValue $_ 'StartupCpuSeconds' } | Where-Object { $null -ne $_ })
        $startupWorking = @($validCaseItems | ForEach-Object { Get-ObjectPropertyValue $_ 'StartupPeakWorkingSetMiB' } | Where-Object { $null -ne $_ })
        $working = @($validCaseItems | ForEach-Object { Get-ObjectPropertyValue $_ 'PeakWorkingSetMiB' } | Where-Object { $null -ne $_ })
        $selectionOkItems = @($items | Where-Object { (Get-ObjectPropertyValue $_ 'ScenarioSelectionTransitionOk') -eq $true })
        $selectionFailedItems = @($items | Where-Object { (Get-ObjectPropertyValue $_ 'ScenarioSelectionTransitionOk') -eq $false })
        $transitionOkItems = @($items | Where-Object { (Get-ObjectPropertyValue $_ 'ScenarioTransitionOk') -eq $true })
        $transitionFailedItems = @($items | Where-Object { (Get-ObjectPropertyValue $_ 'ScenarioTransitionOk') -eq $false })
        $selectionRoute = @($selectionOkItems | ForEach-Object { Get-ObjectPropertyValue $_ 'ScenarioSelectionRouteMs' } | Where-Object { $null -ne $_ })
        $selectionWait = @($selectionOkItems | ForEach-Object { Get-ObjectPropertyValue $_ 'ScenarioSelectionNativeWaitMs' } | Where-Object { $null -ne $_ })
        $selection = @($selectionOkItems | ForEach-Object { Get-ObjectPropertyValue $_ 'ScenarioSelectionTransitionMs' } | Where-Object { $null -ne $_ })
        $selectionFailure = @($selectionFailedItems | ForEach-Object { Get-ObjectPropertyValue $_ 'ScenarioSelectionFailureElapsedMs' } | Where-Object { $null -ne $_ })
        $transition = @($transitionOkItems | ForEach-Object { Get-ObjectPropertyValue $_ 'ScenarioTransitionMs' } | Where-Object { $null -ne $_ })
        $transitionFailure = @($transitionFailedItems | ForEach-Object { Get-ObjectPropertyValue $_ 'ScenarioTransitionFailureElapsedMs' } | Where-Object { $null -ne $_ })
        $menuFps = @($validCaseItems | ForEach-Object { Get-ObjectPropertyValue $_ 'MenuMedianSmoothFps' } | Where-Object { $null -ne $_ })
        $coverage = @($validCaseItems | ForEach-Object {
            Get-ObjectPropertyValue $_ 'MenuFpsCoveragePercent'
            Get-ObjectPropertyValue $_ 'ScenarioSelectionFpsCoveragePercent'
            Get-ObjectPropertyValue $_ 'ScenarioFpsCoveragePercent'
        } | Where-Object { $null -ne $_ })
        $summaryRows.Add([pscustomobject]@{
            Platform = [string](Get-ObjectPropertyValue $items[0] 'Platform' '')
            Profile = [string](Get-ObjectPropertyValue $items[0] 'Profile' '')
            Mode = [string](Get-ObjectPropertyValue $items[0] 'Mode' '')
            ExecutionMode = [string](Get-ObjectPropertyValue $items[0] 'ExecutionMode' '')
            ComparisonLane = [string](Get-ObjectPropertyValue $items[0] 'ComparisonLane' '')
            Runs = $items.Count
            Passed = @($items | Where-Object Status -EQ 'passed').Count
            StartupSamples = $startup.Count
            StartupMinMs = if ($startup.Count) { ($startup | Measure-Object -Minimum).Minimum } else { $null }
            StartupMedianMs = Get-Percentile $startup 0.5
            StartupMaxMs = if ($startup.Count) { ($startup | Measure-Object -Maximum).Maximum } else { $null }
            StartupP05Ms = Get-Percentile $startup 0.05
            StartupP95Ms = Get-Percentile $startup 0.95
            HarnessMenuReadyMedianMs = Get-Percentile $harnessStartup 0.5
            HarnessMenuReadySamples = $harnessStartup.Count
            StartupCpuMedianSeconds = Get-Percentile $cpu 0.5
            StartupCpuSamples = $cpu.Count
            StartupPeakWorkingSetMedianMiB = Get-Percentile $startupWorking 0.5
            StartupPeakWorkingSetSamples = $startupWorking.Count
            PeakWorkingSetMedianMiB = Get-Percentile $working 0.5
            PeakWorkingSetSamples = $working.Count
            ScenarioSelectionRouteMedianMs = Get-Percentile $selectionRoute 0.5
            ScenarioSelectionRouteSamples = $selectionRoute.Count
            ScenarioSelectionNativeWaitMedianMs = Get-Percentile $selectionWait 0.5
            ScenarioSelectionNativeWaitSamples = $selectionWait.Count
            ScenarioSelectionMedianMs = Get-Percentile $selection 0.5
            ScenarioSelectionSamples = $selection.Count
            ScenarioSelectionFailureMedianMs = Get-Percentile $selectionFailure 0.5
            ScenarioSelectionFailureSamples = $selectionFailure.Count
            ScenarioTransitionMedianMs = Get-Percentile $transition 0.5
            ScenarioTransitionSamples = $transition.Count
            ScenarioTransitionFailureMedianMs = Get-Percentile $transitionFailure 0.5
            ScenarioTransitionFailureSamples = $transitionFailure.Count
            MenuSmoothFpsMedian = Get-Percentile $menuFps 0.5
            MenuSmoothFpsSamples = $menuFps.Count
            MinimumFpsCoveragePercent = if ($coverage.Count) { ($coverage | Measure-Object -Minimum).Minimum } else { $null }
            FpsCoverageSamples = $coverage.Count
            StartupDeltaVsVanillaMs = $null
            StartupDeltaVsVanillaPercent = $null
            StartupPairedDeltaSamples = 0
            StartupPairedDeltaMinMs = $null
            StartupPairedDeltaMaxMs = $null
            PeakWorkingSetDeltaVsVanillaMiB = $null
            PeakWorkingSetPairedDeltaSamples = 0
        })
    }
    foreach ($row in $summaryRows) {
        $rowCases = @($Results | Where-Object {
            [string](Get-ObjectPropertyValue $_ 'Platform' '') -eq $row.Platform -and
            [string](Get-ObjectPropertyValue $_ 'Profile' '') -eq $row.Profile -and
            [string](Get-ObjectPropertyValue $_ 'ExecutionMode' '') -eq $row.ExecutionMode -and
            [string](Get-ObjectPropertyValue $_ 'ComparisonLane' '') -eq $row.ComparisonLane -and
            [string](Get-ObjectPropertyValue $_ 'Status' '') -ne 'failed'
        })
        $startupDeltas = New-Object 'System.Collections.Generic.List[double]'
        $startupPercentDeltas = New-Object 'System.Collections.Generic.List[double]'
        $workingDeltas = New-Object 'System.Collections.Generic.List[double]'
        foreach ($case in $rowCases) {
            $baselineCase = $Results | Where-Object {
                [string](Get-ObjectPropertyValue $_ 'Platform' '') -eq $row.Platform -and
                ([string](Get-ObjectPropertyValue $_ 'Mode' '') -eq 'vanilla' -or [string](Get-ObjectPropertyValue $_ 'Profile' '') -eq 'vanilla') -and
                [string](Get-ObjectPropertyValue $_ 'ExecutionMode' '') -eq $row.ExecutionMode -and
                [string](Get-ObjectPropertyValue $_ 'ComparisonLane' '') -eq $row.ComparisonLane -and
                [int](Get-ObjectPropertyValue $_ 'Iteration' 0) -eq [int](Get-ObjectPropertyValue $case 'Iteration' 0) -and
                [string](Get-ObjectPropertyValue $_ 'Status' '') -ne 'failed'
            } | Select-Object -First 1
            if ($null -eq $baselineCase) { continue }
            $caseStartup = Get-ObjectPropertyValue $case 'StartupMs'
            $baselineStartup = Get-ObjectPropertyValue $baselineCase 'StartupMs'
            if ($null -ne $caseStartup -and $null -ne $baselineStartup) {
                $delta = [double]$caseStartup - [double]$baselineStartup
                $startupDeltas.Add($delta)
                if ([double]$baselineStartup -ne 0) { $startupPercentDeltas.Add(($delta / [double]$baselineStartup) * 100) }
            }
            $caseWorking = Get-ObjectPropertyValue $case 'StartupPeakWorkingSetMiB'
            $baselineWorking = Get-ObjectPropertyValue $baselineCase 'StartupPeakWorkingSetMiB'
            if ($null -ne $caseWorking -and $null -ne $baselineWorking) { $workingDeltas.Add([double]$caseWorking - [double]$baselineWorking) }
        }
        if ($startupDeltas.Count -gt 0) {
            $row.StartupPairedDeltaSamples = $startupDeltas.Count
            $row.StartupDeltaVsVanillaMs = [math]::Round((Get-Percentile $startupDeltas.ToArray() 0.5), 1)
            $row.StartupPairedDeltaMinMs = [math]::Round(($startupDeltas | Measure-Object -Minimum).Minimum, 1)
            $row.StartupPairedDeltaMaxMs = [math]::Round(($startupDeltas | Measure-Object -Maximum).Maximum, 1)
            if ($startupPercentDeltas.Count -gt 0) { $row.StartupDeltaVsVanillaPercent = [math]::Round((Get-Percentile $startupPercentDeltas.ToArray() 0.5), 2) }
        }
        if ($workingDeltas.Count -gt 0) {
            $row.PeakWorkingSetPairedDeltaSamples = $workingDeltas.Count
            $row.PeakWorkingSetDeltaVsVanillaMiB = [math]::Round((Get-Percentile $workingDeltas.ToArray() 0.5), 1)
        }
    }
    $summaryRows.ToArray() | Export-Csv -LiteralPath (Join-Path $OutputRoot 'summary.csv') -NoTypeInformation -Encoding UTF8
    $manifest = [pscustomobject]@{ Run = $RunMetadata; Cases = @($Results) }
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputRoot 'manifest.json') -Encoding UTF8
    $lines = New-Object 'System.Collections.Generic.List[string]'
    $lines.Add('# Sheltered automated performance benchmark')
    $lines.Add('')
    $lines.Add("Run: $($RunMetadata.RunId)  ")
    $lines.Add("Created: $($RunMetadata.CreatedAtUtc)  ")
    $lines.Add("Configuration: ``$($RunMetadata.ConfigPath)``")
    $lines.Add('')
    $lines.Add('## Results')
    $lines.Add('')
    $lines.Add('| Platform | Profile | Execution | Passed | Native startup median [min/max] ms (n) | Harness-ready median ms | vs matched vanilla | Startup CPU s | Startup peak WS MiB | Case peak WS MiB | Scenario route ms (n) | Click-to-ready ms (n) | Book ms (n) | Min FPS coverage |')
    $lines.Add('|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|')
    foreach ($row in $summaryRows) {
        $delta = if ($null -ne $row.StartupDeltaVsVanillaMs) { "$($row.StartupDeltaVsVanillaMs) ms ($($row.StartupDeltaVsVanillaPercent)%)" } else { 'n/a' }
        $lines.Add("| $($row.Platform) | $($row.Profile) | $($row.ExecutionMode) | $($row.Passed)/$($row.Runs) | $($row.StartupMinMs)/$($row.StartupMedianMs)/$($row.StartupMaxMs) ($($row.StartupSamples)) | $($row.HarnessMenuReadyMedianMs) | $delta | $($row.StartupCpuMedianSeconds) | $($row.StartupPeakWorkingSetMedianMiB) | $($row.PeakWorkingSetMedianMiB) | $($row.ScenarioSelectionRouteMedianMs) ($($row.ScenarioSelectionRouteSamples)) | $($row.ScenarioSelectionMedianMs) ($($row.ScenarioSelectionSamples)) | $($row.ScenarioTransitionMedianMs) ($($row.ScenarioTransitionSamples)) | $($row.MinimumFpsCoveragePercent)% |")
    }
    $lines.Add('')
    $lines.Add('## Individual cases')
    $lines.Add('')
    $lines.Add('| Platform | Profile | Iteration | Readiness | Native startup ms | Harness-ready ms | CPU s | Peak WS MiB | Route ms | Mutex wait ms | Click-to-ready ms | Book ms | FPS median | FPS p05 | Result |')
    $lines.Add('|---|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|')
    foreach ($result in $Results) {
        $readinessMethod = Get-ObjectPropertyValue $result 'ReadinessMethod' ''
        $startupMs = Get-ObjectPropertyValue $result 'StartupMs'
        $harnessReady = Get-ObjectPropertyValue $result 'HarnessMenuReadyMs'
        $startupCpu = Get-ObjectPropertyValue $result 'StartupCpuSeconds'
        $peakWorking = Get-ObjectPropertyValue $result 'PeakWorkingSetMiB'
        $selectionRouteMs = Get-ObjectPropertyValue $result 'ScenarioSelectionRouteMs'
        $selectionWaitMs = Get-ObjectPropertyValue $result 'ScenarioSelectionNativeWaitMs'
        $selectionTransitionMs = Get-ObjectPropertyValue $result 'ScenarioSelectionTransitionMs'
        $bookMs = Get-ObjectPropertyValue $result 'ScenarioTransitionMs'
        $menuFps = Get-ObjectPropertyValue $result 'MenuMedianSmoothFps'
        $menuP05Fps = Get-ObjectPropertyValue $result 'MenuP05SmoothFps'
        $lines.Add("| $($result.Platform) | $($result.Profile) | $($result.Iteration) | $readinessMethod | $startupMs | $harnessReady | $startupCpu | $peakWorking | $selectionRouteMs | $selectionWaitMs | $selectionTransitionMs | $bookMs | $menuFps | $menuP05Fps | $($result.Status) |")
    }
    $lines.Add('')
    $lines.Add('## Interpretation notes')
    $lines.Add('')
    $lines.Add('- Harness `Time.smoothDeltaTime` values are Unity loop-rate probes, not presented/display FPS.')
    $lines.Add('- Native startup comparisons use the same platform-specific stable reference frame and are emitted only when execution mode and comparison lane match vanilla. `HarnessMenuReadyMs` is retained as the earlier semantic milestone.')
    $lines.Add('- Successful route/book medians exclude failed attempts. Failure elapsed values and per-metric sample counts remain in summary.csv for diagnosis.')
    $lines.Add('- Scenario route time includes menu navigation; click-to-ready isolates the requested scenario-slot transition. Mutex wait is scheduling overhead caused by one shared Windows foreground/cursor and is excluded from both timings.')
    $lines.Add('- Startup memory deltas use samples only through the common ready milestone. Whole-case peaks and per-phase CPU/memory summaries are retained separately.')
    $lines.Add('- CPU and memory include the harness for instrumented profiles. Each case records its exact selected-mod and file-hash manifest.')
    $lines.Add('- Install configuration is snapshotted before each case and restored in cleanup, including failure paths.')
    $lines | Set-Content -LiteralPath (Join-Path $OutputRoot 'README.md') -Encoding UTF8
}

Export-ModuleMember -Function @(
    'ConvertTo-PlainHashtable', 'Get-BenchmarkEnvironment', 'Get-FileFingerprint',
    'Enter-BenchmarkInstallLocks', 'Exit-BenchmarkInstallLocks', 'Get-EnabledLoadOrderIds', 'Get-InstalledModCatalog', 'Get-LoadOrderState', 'Get-ObjectPropertyValue',
    'Export-ShelteredStartupTimings', 'Get-Percentile', 'Get-ProcessSampleSummary', 'Import-ShelteredBenchmarkConfig', 'Invoke-BenchmarkCommand',
    'New-InstallStateSnapshot', 'Resolve-BenchmarkExecution', 'Resolve-ConfiguredPath', 'Resolve-ShelteredModProfile',
    'Restore-InstallStateSnapshot', 'Set-DoorstopEnabled', 'Set-ShelteredLoadOrder', 'Test-BenchmarkDeploymentHashes',
    'Set-ShelteredManagerOptions', 'Test-BenchmarkFileManifest', 'Test-BenchmarkMutableModPath', 'Test-ShelteredBenchmarkConfig', 'Write-BenchmarkAggregateReport'
)
