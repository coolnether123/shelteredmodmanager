Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

function New-ReleaseZip {
    param(
        [Parameter(Mandatory)][string]$SourceDirectory,
        [Parameter(Mandatory)][string]$DestinationPath,
        [switch]$IncludeRoot
    )

    $source = Get-Item -LiteralPath $SourceDirectory
    $basePath = $source.FullName
    if ($IncludeRoot) { $basePath = $source.Parent.FullName }

    $archive = [IO.Compression.ZipFile]::Open($DestinationPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($item in Get-ChildItem -LiteralPath $source.FullName -Recurse -Force) {
            $entryName = $item.FullName.Substring($basePath.Length + 1).Replace('\', '/')
            if ($item.PSIsContainer) {
                if (-not (Get-ChildItem -LiteralPath $item.FullName -Force | Select-Object -First 1)) {
                    $archive.CreateEntry($entryName + '/') | Out-Null
                }
                continue
            }
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $item.FullName, $entryName, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }

    $archive = [IO.Compression.ZipFile]::OpenRead($DestinationPath)
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.Contains('\')) {
                throw "ZIP '$DestinationPath' contains a backslash entry: '$($entry.FullName)'."
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}
