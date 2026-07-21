param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
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
    Assert-True $passed 'Absent Nexus hash metadata must warn and continue.'

    $serviceSource = Get-Content -LiteralPath (Join-Path $repoRoot 'Manager\Core\Services\ManagerSelfUpdateService.cs') -Raw
    Assert-True ($serviceSource -match 'Trace\.TraceWarning[\s\S]*did not include an archive hash') 'Absent hash metadata must log a warning.'
    $downloadAndStageBody = Get-MethodBody -Source $serviceSource -Pattern 'public\s+ManagerUpdateStage\s+DownloadAndStage\s*\(\s*string\s+downloadUrl,\s*string\s+expectedVersion,\s*ManagerUpdateArchiveHashExpectation\s+hashExpectation,\s*out\s+string\s+errorMessage\s*\)\s*\{'
    Assert-True ($null -ne $downloadAndStageBody) 'DownloadAndStage staging path must exist and be parseable.'
    $downloadIndex = $downloadAndStageBody.IndexOf('DownloadBounded(')
    $verifyIndex = $downloadAndStageBody.IndexOf('VerifyDownloadedArchiveHashes(')
    $extractIndex = $downloadAndStageBody.IndexOf('ExtractAndValidateStage(')
    $deleteIndex = $downloadAndStageBody.IndexOf('DeleteStagedArchive(')
    Assert-True ($downloadIndex -ge 0 -and $verifyIndex -ge 0 -and $extractIndex -ge 0) 'Downloaded manager archive pipeline must include download, verify, and extraction calls.'
    Assert-True ($downloadIndex -lt $verifyIndex -and $verifyIndex -lt $extractIndex) 'Downloaded manager archives must be hash-verified before extraction.'
    Assert-True ($verifyIndex -lt $deleteIndex -and $deleteIndex -ge 0) 'Hash verification failures must delete the staged download before returning.'
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
