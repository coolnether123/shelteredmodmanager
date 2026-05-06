param(
    [switch]$IncludeSolutionBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$failures = New-Object System.Collections.Generic.List[string]

function Resolve-MSBuild {
    $command = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $result = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($result) {
            return $result
        }
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        return $dotnet.Source
    }

    throw "Could not find MSBuild or dotnet."
}

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Body
    )

    Write-Host ""
    Write-Host "==> $Name"
    try {
        & $Body
        Write-Host "[PASS] $Name"
    }
    catch {
        $failures.Add($Name)
        Write-Host "[FAIL] $Name"
        Write-Host $_
    }
}

function Invoke-Build {
    param(
        [string]$Project,
        [string]$Configuration = "Debug"
    )

    if ($script:MSBuildPath.EndsWith("dotnet.exe", [System.StringComparison]::OrdinalIgnoreCase)) {
        & $script:MSBuildPath msbuild $Project /p:Configuration=$Configuration /p:Platform="AnyCPU" /v:minimal
    }
    else {
        & $script:MSBuildPath $Project /p:Configuration=$Configuration /p:Platform="AnyCPU" /v:minimal
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $Project"
    }
}

function Invoke-ToolScript {
    param([string]$Path)

    $fullPath = Join-Path $repoRoot $Path
    if (!(Test-Path $fullPath)) {
        Write-Host "[SKIP] Missing $Path"
        return
    }

    & powershell -NoProfile -ExecutionPolicy Bypass -File $fullPath
    if ($LASTEXITCODE -ne 0) {
        throw "$Path failed."
    }
}

$script:MSBuildPath = Resolve-MSBuild
Write-Host "Using build tool: $script:MSBuildPath"

Set-Location $repoRoot

Invoke-Step "Build ModAPI.Networking tests" {
    Invoke-Build "Tests\ModAPI.Networking.Tests\ModAPI.Networking.Tests.csproj"
}

Invoke-Step "Run ModAPI.Networking tests" {
    & "Dist\Tests\ModAPI.Networking.Tests\ModAPI.Networking.Tests.exe"
    if ($LASTEXITCODE -ne 0) {
        throw "Networking test executable failed."
    }
}

Invoke-Step "Sheltered API contract checks" {
    Invoke-ToolScript "tools\Test-ShelteredApiContracts.ps1"
}

Invoke-Step "ModAPI boundary checks" {
    Invoke-ToolScript "tools\Verify-ModApiBoundary.ps1"
}

Invoke-Step "Runtime compatibility rectangle checks" {
    Invoke-ToolScript "tools\Verify-RuntimeCompatRect.ps1"
}

Invoke-Step "Sheltered API public surface checks" {
    Invoke-ToolScript "tools\Verify-ShelteredApiPublicSurface.ps1"
}

if ($IncludeSolutionBuild) {
    Invoke-Step "Build ShelteredModManager solution" {
        Invoke-Build "ShelteredModManager.sln"
    }
}

Write-Host ""
if ($failures.Count -gt 0) {
    Write-Host "Repo tests failed:"
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }
    exit 1
}

Write-Host "Repo tests passed."
