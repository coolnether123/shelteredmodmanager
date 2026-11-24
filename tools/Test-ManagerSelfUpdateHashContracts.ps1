param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$StageProbeRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath($RepoRoot)
$failures = New-Object System.Collections.Generic.List[string]

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { $failures.Add($Message) }
}

function Get-HashHex([string]$Path, [string]$Algorithm) {
    return (Get-FileHash -LiteralPath $Path -Algorithm $Algorithm).Hash.ToLowerInvariant()
}

function Get-MethodBody([string]$Source, [string]$Pattern) {
    $match = [regex]::Match($Source, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) {
        return $null
    }

    $start = $match.Index + $match.Length
    $braceDepth = 0
    $foundOpen = $false
    $bodyEnd = -1
    for ($i = $match.Index; $i -lt $Source.Length; $i++) {
        $ch = $Source[$i]
        if ($ch -eq '{') {
            $braceDepth++
            $foundOpen = $true
        } elseif ($ch -eq '}') {
            $braceDepth--
            if ($foundOpen -and $braceDepth -eq 0) {
                $bodyEnd = $i
                break
            }
        }
    }

    if ($bodyEnd -lt 0) {
        return $null
    }

    return $Source.Substring($start, $bodyEnd - $start)
}

function Test-Staging([string]$testRoot) {
    $install = Join-Path $testRoot 'installed\SMM'
    New-Item -ItemType Directory -Path $install -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'Dist\SMM\Manager.exe') -Destination $install
    Set-Content -LiteralPath (Join-Path $install 'ManagerUpdater.exe') -Value 'detached updater fixture'
    $managerAssembly = [Reflection.Assembly]::LoadFile((Join-Path $install 'Manager.exe'))
    $builtService = $managerAssembly.GetType('Manager.Core.Services.ManagerSelfUpdateService', $true)
    $builtExpectation = $managerAssembly.GetType('Manager.Core.Services.ManagerUpdateArchiveHashExpectation', $true)
    $stage = [Activator]::CreateInstance($builtService)
    $isOffered = $builtService.GetMethod('IsSameVersionUpdateAvailable', [Reflection.BindingFlags]'Static, NonPublic')
    $receipt = Join-Path $install 'manager-update.hashes'
    Set-Content -LiteralPath $receipt -Encoding ASCII -Value ('sha256:' + ('a' * 64))

    $package = Join-Path $testRoot 'payload\SMM'
    Copy-Item -LiteralPath $install -Destination (New-Item -ItemType Directory (Split-Path -Parent $package)).FullName -Recurse
    Remove-Item -LiteralPath (Join-Path $package 'manager-update.hashes')
    foreach ($relative in @('ModAPI.dll', 'bin\ShelteredAPI.dll', 'bin\Doorstop.dll', 'bin\0Harmony.dll',
        'Doorstop\x86\winhttp.dll', 'Doorstop\x64\winhttp.dll')) {
        $file = Join-Path $package $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null
        Set-Content -LiteralPath $file -Value 'required package file fixture'
    }
    $candidate = Join-Path $testRoot 'candidate.zip'
    Compress-Archive -LiteralPath $package -DestinationPath $candidate
    $candidateHash = Get-HashHex $candidate 'SHA256'
    $candidateHashes = [Activator]::CreateInstance($builtExpectation)
    $candidateHashes.ReleaseMetadata = "sha256:$candidateHash"

    Assert-True ([bool]$isOffered.Invoke($null, @('2.0.0', $candidateHashes))) 'Offer a same-version ZIP with a different advertised hash.'
    $stageError = $null
    $result = $stage.StageLocalPackage($candidate, '2.0.0', $candidateHashes, [ref]$stageError)
    Assert-True ($null -ne $result) "A verified same-version update must stage. $stageError"
    if ($result) {
        $stagedReceipt = Get-Content -LiteralPath (Join-Path $result.StagedSmmPath 'manager-update.hashes')
        Assert-True ($stagedReceipt -contains "sha256:$candidateHash") 'Promote the accepted hash with the staged binaries.'
    }
    $result = $stage.StageLocalPackage($candidate, '2.0.0', [ref]$stageError)
    Assert-True ($null -eq $result -and $stageError -match 'verified package hash') 'Require a verified hash for a same-version local ZIP.'
    Set-Content -LiteralPath $receipt -Encoding ASCII -Value "sha256:$candidateHash"
    Assert-True (-not [bool]$isOffered.Invoke($null, @('2.0.0', $candidateHashes))) 'Do not offer an already installed package.'
    $result = $stage.StageLocalPackage($candidate, '2.0.0', $candidateHashes, [ref]$stageError)
    Assert-True ($null -eq $result -and $stageError -match 'different from the installed package') 'Reject staging the already installed archive.'

    $oldManager = Join-Path $testRoot 'OlderManager.dll'
    Add-Type -TypeDefinition '[assembly: System.Reflection.AssemblyVersion("1.9.0.0")] public class OlderManager {}' -OutputAssembly $oldManager
    Copy-Item -LiteralPath $oldManager -Destination (Join-Path $package 'Manager.exe') -Force
    $older = Join-Path $testRoot 'older.zip'
    Compress-Archive -LiteralPath $package -DestinationPath $older
    $candidateHashes.ReleaseMetadata = 'sha256:' + (Get-HashHex $older 'SHA256')
    Assert-True (-not [bool]$isOffered.Invoke($null, @('1.9.0', $candidateHashes))) 'Never offer a verified older version as a same-version update.'
    $result = $stage.StageLocalPackage($older, '1.9.0', $candidateHashes, [ref]$stageError)
    Assert-True ($null -eq $result -and $stageError -match 'is older than') 'Reject a verified older version before promotion.'
}

if ($StageProbeRoot) {
    Test-Staging $StageProbeRoot
    if ($failures.Count) { throw ($failures -join [Environment]::NewLine) }
    Write-Host 'Manager same-version offer and staging checks passed.'
    exit 0
}

$sourcePaths = @(
    'Shared\ManagerPackageContract.cs',
    'Manager\Core\AppVersionInfo.cs',
    'Manager\Core\Models\NexusRemoteModFile.cs',
    'Manager\Core\Services\NexusReleaseClassifier.cs',
    'Manager\Core\Services\NexusVersionComparer.cs',
    'Manager\Core\Services\ArchiveExtraction.cs',
    'Manager\Core\Services\ManagerSelfUpdateService.cs'
) | ForEach-Object { Join-Path $repoRoot $_ }

Add-Type -Path $sourcePaths -ReferencedAssemblies @('System.IO.Compression.dll')

$serviceType = [Manager.Core.Services.ManagerSelfUpdateService]
$expectationType = [Manager.Core.Services.ManagerUpdateArchiveHashExpectation]
$verifyMethod = $serviceType.GetMethod(
    'VerifyDownloadedArchiveHashes',
    [Reflection.BindingFlags]'NonPublic, Static')
Assert-True ($null -ne $verifyMethod) 'Manager self-update hash verifier must exist.'

$testRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot ('artifacts\manager-update-hash-contract-' + [Guid]::NewGuid().ToString('N'))))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $testRoot.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a hash contract test folder outside artifacts: $testRoot"
}

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
    $archive = Join-Path $testRoot 'manager-update.download'
    Set-Content -LiteralPath $archive -Value 'zip bytes are enough for hash verification' -Encoding ASCII

    $md5 = Get-HashHex $archive 'MD5'
    $sha256 = Get-HashHex $archive 'SHA256'

    $correct = [Activator]::CreateInstance($expectationType)
    $correct.ExpectedMd5 = $md5
    $correct.ReleaseMetadata = "release notes sha256:$sha256"
    [object[]]$invokeArgs = @([string]$archive, $correct, $null)
    $passed = [bool]$verifyMethod.Invoke($null, $invokeArgs)
    Assert-True $passed 'A manager update archive with matching MD5 and SHA-256 must pass verification.'
    Assert-True ([string]::IsNullOrEmpty([string]$invokeArgs[2])) 'Correct hashes must not report an error.'

    $wrongMd5 = [Activator]::CreateInstance($expectationType)
    $wrongMd5.ExpectedMd5 = '00000000000000000000000000000000'
    $wrongMd5.ReleaseMetadata = "release notes sha256:$sha256"
    [object[]]$invokeArgs = @([string]$archive, $wrongMd5, $null)
    $passed = [bool]$verifyMethod.Invoke($null, $invokeArgs)
    Assert-True (-not $passed) 'A present but mismatched Nexus MD5 must fail verification.'
    Assert-True ([string]$invokeArgs[2] -match 'MD5 verification failed') 'Wrong MD5 failures must surface a clear MD5 error.'

    $wrongSha256 = [Activator]::CreateInstance($expectationType)
    $wrongSha256.ExpectedMd5 = $md5
    $wrongSha256.ReleaseMetadata = 'release notes sha256:' + 'aa' * 32
    [object[]]$invokeArgs = @([string]$archive, $wrongSha256, $null)
    $passed = [bool]$verifyMethod.Invoke($null, $invokeArgs)
    Assert-True (-not $passed) 'A mismatched SHA-256 hash must fail verification.'
    Assert-True ([string]$invokeArgs[2] -match 'SHA-256 verification failed') 'Wrong SHA-256 failures must surface a clear SHA-256 error.'

    $malformedMd5 = [Activator]::CreateInstance($expectationType)
    $malformedMd5.ExpectedMd5 = 'not-a-hex-hash'
    $malformedMd5.ReleaseMetadata = "release notes sha256:$sha256"
    [object[]]$invokeArgs = @([string]$archive, $malformedMd5, $null)
    $passed = [bool]$verifyMethod.Invoke($null, $invokeArgs)
    Assert-True (-not $passed) 'A malformed Nexus MD5 must fail verification.'
    Assert-True ([string]$invokeArgs[2] -match 'invalid MD5') 'Malformed MD5 failures must surface a clear error.'

    $absent = [Activator]::CreateInstance($expectationType)
    $absent.ExpectedMd5 = ''
    $absent.ReleaseMetadata = 'release notes without hashes'
    [object[]]$invokeArgs = @([string]$archive, $absent, $null)
    $passed = [bool]$verifyMethod.Invoke($null, $invokeArgs)
    Assert-True (-not $passed) 'Absent Nexus hash metadata must reject the update.'

    $serviceSource = Get-Content -LiteralPath (Join-Path $repoRoot 'Manager\Core\Services\ManagerSelfUpdateService.cs') -Raw
    Assert-True ([string]$invokeArgs[2] -match 'did not include an archive hash') 'Absent hash metadata must explain the rejected update.'
    $downloadAndStageBody = Get-MethodBody -Source $serviceSource -Pattern 'public\s+ManagerUpdateStage\s+DownloadAndStage\s*\(\s*string\s+downloadUrl,\s*string\s+expectedVersion,\s*ManagerUpdateArchiveHashExpectation\s+hashExpectation,\s*out\s+string\s+errorMessage\s*\)\s*\{'
    Assert-True ($null -ne $downloadAndStageBody) 'DownloadAndStage staging path must exist and be parseable.'
    $downloadIndex = $downloadAndStageBody.IndexOf('DownloadBounded(')
    $verifyIndex = $downloadAndStageBody.IndexOf('VerifyDownloadedArchiveHashes(')
    $extractIndex = $downloadAndStageBody.IndexOf('ExtractAndValidateStage(')
    $deleteIndex = $downloadAndStageBody.IndexOf('DeleteStagedArchive(')
    Assert-True ($downloadIndex -ge 0 -and $verifyIndex -ge 0 -and $extractIndex -ge 0) 'Downloaded manager archive pipeline must include download, verify, and extraction calls.'
    Assert-True ($downloadIndex -lt $verifyIndex -and $verifyIndex -lt $extractIndex) 'Downloaded manager archives must be hash-verified before extraction.'
    Assert-True ($verifyIndex -lt $deleteIndex -and $deleteIndex -ge 0) 'Hash verification failures must delete the staged download before returning.'

    & (Join-Path $PSHOME 'powershell.exe') -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File $PSCommandPath -RepoRoot $repoRoot -StageProbeRoot $testRoot
    if ($LASTEXITCODE -ne 0) { throw 'Same-version staging checks failed.' }

}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Manager self-update hash contract checks passed.'
