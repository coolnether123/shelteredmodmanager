Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-RepositoryRelativePath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot
    )

    $fullPath = (Resolve-Path -LiteralPath $Path).Path
    $root = (Resolve-Path -LiteralPath $RepositoryRoot).Path.TrimEnd([char]'\', [char]'/')
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -and
        -not [string]::Equals($fullPath, $root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path '$fullPath' is not under repository root '$root'."
    }

    return $fullPath.Substring($root.Length).TrimStart([char]'\', [char]'/') -replace '\\', '/'
}

function ConvertTo-VerificationTsvLine {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Values)
    return ($Values | ForEach-Object { [string]$_ }) -join "`t"
}

function Import-VerificationTsvBaseline {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][ValidateRange(1, 64)][int]$DataColumnCount,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][int[]]$KeyColumnIndexes,
        [Parameter(Mandatory = $true)][string]$JustificationRequirement,
        [string]$MissingFileGuidance = ''
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        $suffix = if ([string]::IsNullOrWhiteSpace($MissingFileGuidance)) { '' } else { ' ' + $MissingFileGuidance }
        throw "Missing baseline file: $Path.$suffix"
    }

    $entries = @{}
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $Path) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) {
            continue
        }

        $parts = @($line -split "`t")
        $expectedColumns = $DataColumnCount + 1
        if ($parts.Count -ne $expectedColumns) {
            throw "Invalid baseline line $lineNumber in '$Path'. Expected $expectedColumns tab-separated fields."
        }
        if ([string]::IsNullOrWhiteSpace($parts[$DataColumnCount])) {
            throw "Invalid baseline line $lineNumber in '$Path'. $JustificationRequirement"
        }

        $keyValues = @($KeyColumnIndexes | ForEach-Object {
            if ($_ -lt 0 -or $_ -ge $DataColumnCount) {
                throw "Key column index $_ is outside the $DataColumnCount data columns."
            }
            $parts[$_]
        })
        $key = ConvertTo-VerificationTsvLine -Values $keyValues
        if ($entries.ContainsKey($key)) {
            throw "Duplicate baseline entry on line ${lineNumber}: $key"
        }

        $entries[$key] = [pscustomobject]@{
            Fields = @($parts[0..($DataColumnCount - 1)])
            Justification = [string]$parts[$DataColumnCount]
            LineNumber = $lineNumber
        }
    }

    return $entries
}

Export-ModuleMember -Function @(
    'ConvertTo-RepositoryRelativePath',
    'ConvertTo-VerificationTsvLine',
    'Import-VerificationTsvBaseline'
)
