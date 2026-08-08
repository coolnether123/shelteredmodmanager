#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'VerificationSupport.psm1') -Force

function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Throws([scriptblock]$Action, [string]$Pattern, [string]$Message) {
    try { & $Action; throw $Message }
    catch { if ($_.Exception.Message -eq $Message -or $_.Exception.Message -notmatch $Pattern) { throw } }
}

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$tempRoot = Join-Path $tempBase ('smm-verification-contract-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $tempRoot 'nested') -Force | Out-Null
try {
    $source = Join-Path $tempRoot 'nested\source.cs'
    Set-Content -LiteralPath $source -Value '// fixture' -Encoding ASCII
    $relative = ConvertTo-RepositoryRelativePath -Path $source -RepositoryRoot $tempRoot
    Assert-True ($relative -eq 'nested/source.cs') 'Repository-relative path normalization changed.'
    Assert-Throws { ConvertTo-RepositoryRelativePath -Path $env:WINDIR -RepositoryRoot $tempRoot } 'not under repository root' 'Out-of-root path was accepted.'

    $valid = Join-Path $tempRoot 'valid.tsv'
    @('# header', '', "kind`tnamespace`tname`tpath`tbecause") | Set-Content -LiteralPath $valid -Encoding UTF8
    $rows = Import-VerificationTsvBaseline -Path $valid -DataColumnCount 4 -KeyColumnIndexes @(0, 1, 2) -JustificationRequirement 'Justification required.'
    Assert-True ($rows.Count -eq 1) 'Comments or blank baseline lines produced entries.'
    Assert-True ($rows.ContainsKey("kind`tnamespace`tname")) 'Composite baseline key changed.'

    $badColumns = Join-Path $tempRoot 'bad-columns.tsv'
    Set-Content -LiteralPath $badColumns -Value "a`tb`tc" -Encoding UTF8
    Assert-Throws { Import-VerificationTsvBaseline -Path $badColumns -DataColumnCount 4 -KeyColumnIndexes @(0, 1, 2) -JustificationRequirement 'Justification required.' } 'Expected 5 tab-separated fields' 'Wrong baseline width was accepted.'

    $blankReason = Join-Path $tempRoot 'blank-reason.tsv'
    Set-Content -LiteralPath $blankReason -Value "a`tb`tc`td`t" -Encoding UTF8
    Assert-Throws { Import-VerificationTsvBaseline -Path $blankReason -DataColumnCount 4 -KeyColumnIndexes @(0, 1, 2) -JustificationRequirement 'Justification required.' } 'Justification required' 'Blank justification was accepted.'

    $duplicate = Join-Path $tempRoot 'duplicate.tsv'
    @("a`tb`tc`td`tone", "a`tb`tc`te`ttwo") | Set-Content -LiteralPath $duplicate -Encoding UTF8
    Assert-Throws { Import-VerificationTsvBaseline -Path $duplicate -DataColumnCount 4 -KeyColumnIndexes @(0, 1, 2) -JustificationRequirement 'Justification required.' } 'Duplicate baseline entry' 'Duplicate key was accepted.'
    Write-Host 'Verification support contracts passed.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($tempRoot)
    if ($resolved.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
