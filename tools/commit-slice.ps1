<#
.SYNOPSIS
    COMMITGUARD - safe commit for concurrent autonomous agents.

.DESCRIPTION
    Commits ONLY the explicit files you name, without ever disturbing another
    agent's staged files, without amending, without resetting, and without
    racing the branch pointer.

    How it stays safe:
      * Builds the commit through a TEMPORARY index (GIT_INDEX_FILE) seeded from
        HEAD's tree plus only the given paths. The real index is never used to
        create the commit, so another agent's staged files are never swept in.
      * Advances the branch with an ATOMIC compare-and-swap
        (git update-ref <ref> <new> <old>). If another agent moved the branch
        first, the CAS is rejected and we re-parent the commit onto the new tip
        and try again -- so a slice can never be orphaned onto a stale HEAD.
      * After a successful update it VERIFIES the commit is an ancestor of the
        branch, waits 2 seconds for the ref to settle, and verifies again. If the
        branch was moved past it (force-reset by another agent), it re-applies.

    It never runs `git add -A`, never amends, never resets, and never touches any
    ref other than fast-forwarding the current branch to the commit it creates.

.PARAMETER Message
    Commit message (required).

.PARAMETER Paths
    Explicit list of files to commit (required). Wildcards are allowed but must
    expand to at least one real path; a wildcard that expands to nothing is a
    hard error.

.PARAMETER Retries
    Number of re-apply attempts when the branch races out from under us. Default 3.

.PARAMETER RepoRoot
    Repository root. Defaults to the parent of this script's folder.

.OUTPUTS
    One machine-readable line:
      COMMITGUARD OK <hash> <branch>
      COMMITGUARD FAIL <reason>
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Message,

    [Parameter(Mandatory = $true)]
    [string[]]$Paths,

    [int]$Retries = 3,

    [string]$RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Protected paths: these belong to other agents / must never be committed here.
# ---------------------------------------------------------------------------
$ProtectedRegexes = @(
    '^manager/views/settingstab.*\.cs$',          # live-owned SettingsTab*.cs
    '^decompiled/',                               # gitignored handoff area
    '^tools/invoke-shelteredverifiedclick\.ps1$'  # live agent owns this
)

function Write-Result { param([string]$Line) Write-Host $Line }

function Fail {
    param([string]$Reason)
    Write-Result "COMMITGUARD FAIL $Reason"
    exit 1
}

# ---------------------------------------------------------------------------
# git invocation helper.
#   * stderr is redirected to a temp file so stdout stays CLEAN for hash
#     parsing (and so git's CRLF warnings never trip PowerShell 5.1's
#     NativeCommandError-on-stderr behaviour).
#   * optionally binds a temporary index for the call.
#   Returns: Code (exit code), Out (clean stdout), Err (stderr), All (both).
# ---------------------------------------------------------------------------
function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string[]]$GitArgs,
        [string]$IndexFile
    )
    $hadIndex = Test-Path Env:\GIT_INDEX_FILE
    $prevIndex = if ($hadIndex) { $env:GIT_INDEX_FILE } else { $null }
    if ($PSBoundParameters.ContainsKey('IndexFile') -and $IndexFile) {
        $env:GIT_INDEX_FILE = $IndexFile
    }
    $errFile = [System.IO.Path]::GetTempFileName()
    $prevEAP = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $stdout = & git @GitArgs 2> $errFile
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $prevEAP
        if ($hadIndex) { $env:GIT_INDEX_FILE = $prevIndex }
        elseif (Test-Path Env:\GIT_INDEX_FILE) { Remove-Item Env:\GIT_INDEX_FILE }
    }
    $errText = ''
    if (Test-Path -LiteralPath $errFile) {
        $errText = (Get-Content -Raw -LiteralPath $errFile -ErrorAction SilentlyContinue)
        Remove-Item -LiteralPath $errFile -Force -ErrorAction SilentlyContinue
    }
    $outText = ($stdout | Out-String).Trim()
    if ($null -eq $errText) { $errText = '' } else { $errText = $errText.Trim() }
    $all = (@($outText, $errText) | Where-Object { $_ }) -join ' | '
    return [pscustomobject]@{ Code = $code; Out = $outText; Err = $errText; All = $all }
}

function Git-OrFail {
    param([string[]]$GitArgs, [string]$What)
    $r = Invoke-Git -GitArgs $GitArgs
    if ($r.Code -ne 0) { Fail "$What ($($r.All))" }
    return $r.Out
}

# ---------------------------------------------------------------------------
# Resolve repo root and move into it.
# ---------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
} else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}
Push-Location -LiteralPath $RepoRoot
try {
    $inside = Invoke-Git -GitArgs @('rev-parse', '--is-inside-work-tree')
    if ($inside.Code -ne 0 -or $inside.Out -ne 'true') { Fail "not-a-git-worktree:$RepoRoot" }
    $topLevel = (Git-OrFail -GitArgs @('rev-parse', '--show-toplevel') -What 'resolve-toplevel').Trim()

    # -----------------------------------------------------------------------
    # Determine current branch (must be on a branch, not detached).
    # -----------------------------------------------------------------------
    $branchRes = Invoke-Git -GitArgs @('symbolic-ref', '--short', 'HEAD')
    if ($branchRes.Code -ne 0) { Fail "detached-HEAD-or-no-branch" }
    $branch = $branchRes.Out.Trim()
    $branchRef = "refs/heads/$branch"

    # -----------------------------------------------------------------------
    # Expand + validate paths.
    #   * wildcards must expand to >=1 path
    #   * every path must exist in the worktree OR be tracked in HEAD (deletion)
    #   * none may hit the protected set
    # -----------------------------------------------------------------------
    $relPaths = New-Object System.Collections.Generic.List[string]
    $wildChars = @('*', '?', '[')

    foreach ($p in $Paths) {
        if ([string]::IsNullOrWhiteSpace($p)) { continue }
        $isWild = $false
        foreach ($wc in $wildChars) { if ($p.Contains($wc)) { $isWild = $true; break } }

        $expanded = @()
        if ($isWild) {
            $matched = @(Get-ChildItem -Path (Join-Path $RepoRoot $p) -File -ErrorAction SilentlyContinue)
            if ($matched.Count -eq 0) {
                $matched = @(Get-ChildItem -Path $p -File -ErrorAction SilentlyContinue)
            }
            if ($matched.Count -eq 0) { Fail "wildcard-expands-to-nothing:$p" }
            $expanded = $matched | ForEach-Object { $_.FullName }
        } else {
            $full = if ([System.IO.Path]::IsPathRooted($p)) { $p } else { Join-Path $RepoRoot $p }
            if (Test-Path -LiteralPath $full) {
                $expanded = @((Resolve-Path -LiteralPath $full).Path)
            } else {
                # allow committing a deletion of a tracked file
                $relForCheck = $p -replace '\\', '/'
                $tracked = Invoke-Git -GitArgs @('ls-files', '--error-unmatch', '--', $relForCheck)
                if ($tracked.Code -ne 0) { Fail "path-not-found:$p" }
                $expanded = @($full)
            }
        }

        foreach ($fp in $expanded) {
            # make repo-relative, forward-slashed.
            # Compare in normalized (forward-slash, lower-case) space: git's
            # --show-toplevel yields forward slashes while Resolve-Path yields
            # backslashes.
            $rel = $fp -replace '\\', '/'
            if ([System.IO.Path]::IsPathRooted($fp)) {
                $rootFwd = ($topLevel -replace '\\', '/').TrimEnd([char]'/')
                $fpFwd = $fp -replace '\\', '/'
                if ($fpFwd.Length -gt $rootFwd.Length -and
                    $fpFwd.Substring(0, $rootFwd.Length).ToLowerInvariant() -eq $rootFwd.ToLowerInvariant()) {
                    $rel = $fpFwd.Substring($rootFwd.Length).TrimStart([char]'/')
                } else {
                    Fail "path-outside-repo:$fp"
                }
            }
            $rel = $rel -replace '\\', '/'
            $relLower = $rel.ToLowerInvariant()

            foreach ($rx in $ProtectedRegexes) {
                if ($relLower -match $rx) { Fail "protected-path-refused:$rel" }
            }
            if (-not $relPaths.Contains($rel)) { $relPaths.Add($rel) | Out-Null }
        }
    }

    if ($relPaths.Count -eq 0) { Fail "no-valid-paths" }

    # -----------------------------------------------------------------------
    # Warn (but continue) if the real index already holds OTHER staged files.
    # Our temp-index commit will NOT include them.
    # -----------------------------------------------------------------------
    $stagedRes = Invoke-Git -GitArgs @('diff', '--cached', '--name-only')
    if ($stagedRes.Code -eq 0 -and $stagedRes.Out) {
        $stagedFiles = @($stagedRes.Out -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        $others = @($stagedFiles | Where-Object { -not $relPaths.Contains($_) })
        if ($others.Count -gt 0) {
            Write-Host "COMMITGUARD WARN other-staged-files-present-will-not-be-committed: $($others -join ', ')"
        }
    }

    # -----------------------------------------------------------------------
    # Retry loop: build commit via temp index, CAS the branch, verify.
    # -----------------------------------------------------------------------
    $tempIndex = Join-Path ([System.IO.Path]::GetTempPath()) ("commitguard-idx-" + [System.Guid]::NewGuid().ToString('N'))
    $attempt = 0
    $maxAttempts = [Math]::Max(1, $Retries)
    $newHash = $null

    while ($attempt -lt $maxAttempts) {
        $attempt++

        # current branch tip = parent (fresh each attempt so we never build on stale HEAD)
        $parentRes = Invoke-Git -GitArgs @('rev-parse', '--verify', "$branchRef^{commit}")
        if ($parentRes.Code -ne 0) { Fail "cannot-resolve-branch-tip:$branchRef" }
        $parent = $parentRes.Out.Trim()

        # seed temp index from parent tree
        if (Test-Path -LiteralPath $tempIndex) { Remove-Item -LiteralPath $tempIndex -Force }
        $rt = Invoke-Git -GitArgs @('read-tree', $parent) -IndexFile $tempIndex
        if ($rt.Code -ne 0) { Fail "read-tree-failed ($($rt.All))" }

        # stage ONLY our paths into the temp index (handles adds/mods/deletes)
        $addArgs = @('add', '--all', '--') + $relPaths
        $addRes = Invoke-Git -GitArgs $addArgs -IndexFile $tempIndex
        if ($addRes.Code -ne 0) { Fail "stage-into-temp-index-failed ($($addRes.All))" }

        # write tree from temp index
        $treeRes = Invoke-Git -GitArgs @('write-tree') -IndexFile $tempIndex
        if ($treeRes.Code -ne 0) { Fail "write-tree-failed ($($treeRes.All))" }
        $treeHash = $treeRes.Out.Trim()

        # nothing to commit for these paths?
        $parentTreeRes = Invoke-Git -GitArgs @('rev-parse', "$parent^{tree}")
        if ($parentTreeRes.Code -eq 0 -and $parentTreeRes.Out.Trim() -eq $treeHash) {
            Fail "no-changes-in-given-paths"
        }

        # create the commit object (does NOT move any ref)
        $ct = Invoke-Git -GitArgs @('commit-tree', $treeHash, '-p', $parent, '-m', $Message)
        if ($ct.Code -ne 0) { Fail "commit-tree-failed ($($ct.All))" }
        $candidate = $ct.Out.Trim()

        # ATOMIC compare-and-swap: only advances branch if it still equals $parent
        $cas = Invoke-Git -GitArgs @('update-ref', $branchRef, $candidate, $parent)
        if ($cas.Code -ne 0) {
            Write-Host "COMMITGUARD RETRY attempt=$attempt branch-raced (CAS rejected); re-applying on new tip"
            Start-Sleep -Milliseconds 150
            continue
        }

        # verify reachable from branch
        $anc1 = Invoke-Git -GitArgs @('merge-base', '--is-ancestor', $candidate, $branchRef)
        if ($anc1.Code -ne 0) {
            Write-Host "COMMITGUARD RETRY attempt=$attempt post-commit not-ancestor; re-applying"
            continue
        }

        # settle, then re-verify (guards against another agent force-moving the ref)
        Start-Sleep -Seconds 2
        $anc2 = Invoke-Git -GitArgs @('merge-base', '--is-ancestor', $candidate, $branchRef)
        if ($anc2.Code -ne 0) {
            Write-Host "COMMITGUARD RETRY attempt=$attempt commit-orphaned-after-settle; re-applying"
            continue
        }

        $newHash = $candidate
        break
    }

    if (Test-Path -LiteralPath $tempIndex) { Remove-Item -LiteralPath $tempIndex -Force -ErrorAction SilentlyContinue }

    if (-not $newHash) { Fail "exhausted-retries branch kept racing after $maxAttempts attempts" }

    # -----------------------------------------------------------------------
    # Sync ONLY our paths in the REAL index so the worktree is clean for them
    # (index==HEAD for these files). Never touches other agents' staged files.
    # Non-fatal: if it fails (e.g. index.lock contention) the commit is safe.
    # -----------------------------------------------------------------------
    $syncArgs = @('add', '--all', '--') + $relPaths
    $sync = Invoke-Git -GitArgs $syncArgs
    if ($sync.Code -ne 0) {
        Write-Host "COMMITGUARD WARN real-index-sync-failed (commit is safe): $($sync.All)"
    }

    $shortHash = $newHash.Substring(0, [Math]::Min(12, $newHash.Length))
    Write-Result "COMMITGUARD OK $shortHash $branch"
    exit 0
}
finally {
    Pop-Location
}
