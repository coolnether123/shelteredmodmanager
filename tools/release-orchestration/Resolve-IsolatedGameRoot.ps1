function Resolve-IsolatedGameRoot {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ($Path -notmatch '^[AF]:\\') {
        throw 'Use an absolute game copy path under A:\Dev\Worktrees or F:\Dev\Worktrees.'
    }
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if ($fullPath -notmatch '^[AF]:\\Dev\\Worktrees\\.+$' -or
        $fullPath -match '(?i)\\(?:SteamLibrary|steamapps|Epic Games(?: Games)?|GOG Games)(?:\\|$)') {
        throw "Refusing game root outside the isolated worktrees or under a storefront: $fullPath"
    }

    $root = Get-Item -LiteralPath $fullPath -Force
    if (-not $root.PSIsContainer) { throw "Game root is not a directory: $fullPath" }
    for ($item = $root; $null -ne $item; $item = $item.Parent) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Refusing a linked game root or ancestor: $($item.FullName)"
        }
    }
    $linkedChild = Get-ChildItem -LiteralPath $root.FullName -Recurse -Force |
        Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } |
        Select-Object -First 1
    if ($null -ne $linkedChild) { throw "Refusing a linked entry in the game copy: $($linkedChild.FullName)" }
    return $root.FullName
}
