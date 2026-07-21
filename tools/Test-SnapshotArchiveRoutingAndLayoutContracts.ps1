[CmdletBinding()]
param(
    [string]$RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
} else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

function Read-Source {
    param([string]$RelativePath)

    return Get-Content -LiteralPath (Join-Path $RepoRoot $RelativePath) -Raw
}

function Assert-Contains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Pattern
    )

    if (-not [regex]::IsMatch($Text, $Pattern, [Text.RegularExpressions.RegexOptions]::Singleline)) {
        throw "${Name}: expected source contract was not found."
    }
}

function Assert-NotContains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Pattern
    )

    if ([regex]::IsMatch($Text, $Pattern, [Text.RegularExpressions.RegexOptions]::Singleline)) {
        throw "${Name}: forbidden source pattern was found."
    }
}

$controls = Read-Source "ShelteredAPI\Saves\Paging\SaveSnapshotSlotControls.cs"
$browserState = Read-Source "ShelteredAPI\Saves\Paging\SaveSnapshotBrowserState.cs"
$coordinator = Read-Source "ShelteredAPI\Saves\Paging\SlotSelectionPatchCoordinator.cs"
$verification = Read-Source "ShelteredAPI\Saves\Paging\SaveVerification.cs"

$vanillaBlock = [regex]::Match(
    $controls,
    'if\s*\(visible\.IsVanillaPage\)\s*\{(?<body>.*?)\n\s*\}\s*\n\s*if\s*\(visible\.Entry',
    [Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $vanillaBlock.Success) {
    throw "vanilla timeline routing: vanilla-page branch was not found."
}

$vanillaBody = $vanillaBlock.Groups["body"].Value
$vanillaTimelineIndex = $vanillaBody.IndexOf("TryGetVanillaTimelineKey", [StringComparison]::Ordinal)
$customTimelineIndex = $vanillaBody.IndexOf("TryGetCustomTimelineKey", [StringComparison]::Ordinal)
$customLookupIndex = $vanillaBody.IndexOf("TryFindTimelineKey", [StringComparison]::Ordinal)
if ($vanillaTimelineIndex -lt 0) {
    throw "vanilla timeline routing: vanilla timeline lookup is missing."
}
if (($customTimelineIndex -ge 0 -and $vanillaTimelineIndex -gt $customTimelineIndex) -or
    ($customLookupIndex -ge 0 -and $vanillaTimelineIndex -gt $customLookupIndex)) {
    throw "vanilla timeline routing: a custom timeline lookup precedes the vanilla timeline lookup."
}
Assert-Contains `
    "vanilla timeline must contain snapshots before custom fallback is skipped" `
    $vanillaBody `
    'TryGetVanillaTimelineKey\(.*?&&\s*SaveBackupService\.CountSnapshots\(timelineKey\)\s*>\s*0'
Assert-Contains `
    "custom fallback clears vanilla routing metadata" `
    $vanillaBody `
    'timelineKey\s*=\s*null;\s*vanillaSaveType\s*=\s*SaveManager\.SaveType\.Invalid;\s*if\s*\(visible\.Entry'

Assert-Contains `
    "source transport state" `
    $browserState `
    'SourceTransportSaveType.*SourceTransportSlotNumber.*SourceTransportSaveType\s*=\s*sourceTransportSaveType.*SourceTransportSlotNumber\s*=\s*sourceTransportSlotNumber'

$queueMethod = [regex]::Match(
    $coordinator,
    'private static void QueueSnapshotLoad\(.*?\n\s*\}\s*\n\s*private static bool HandleVanillaSlotChosen',
    [Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $queueMethod.Success) {
    throw "custom snapshot routing: QueueSnapshotLoad method was not found."
}

Assert-Contains `
    "custom snapshot uses source transport save type" `
    $queueMethod.Value `
    'PlatformSaveProxy\.SetNextLoad\(\s*session\.SourceTransportSaveType'
Assert-Contains `
    "custom snapshot uses source transport slot" `
    $queueMethod.Value `
    'slotToLoad\s*=\s*session\.SourceTransportSlotNumber'
Assert-NotContains `
    "custom snapshot ignores selected archive row for transport" `
    $queueMethod.Value `
    'GetTransport(?:SaveType|SlotNumber)\(\s*chosenSlotIndex\s*\)'

Assert-Contains `
    "slot controls define a collider gap" `
    $controls `
    'SnapshotButtonX\s*=\s*VerificationButtonX\s*-\s*\(\(ColliderWidth\s*\+\s*VerificationColliderSize\)\s*/\s*2f\)\s*-\s*ControlGap'

$gapMatch = [regex]::Match($controls, 'ControlGap\s*=\s*(?<gap>\d+)')
if (-not $gapMatch.Success -or [int]$gapMatch.Groups["gap"].Value -le 0) {
    throw "slot control layout: collider gap must be positive."
}

Assert-Contains `
    "verification controls share horizontal alignment" `
    $verification `
    'localPosition\s*=\s*new Vector3\(\s*SaveSnapshotSlotControls\.VerificationButtonX,\s*0,\s*-20\)'
Assert-Contains `
    "verification controls share collider dimensions" `
    $verification `
    'col\.size\s*=\s*new Vector3\(\s*SaveSnapshotSlotControls\.VerificationColliderSize,\s*SaveSnapshotSlotControls\.VerificationColliderSize,\s*1\)'
Assert-Contains `
    "verification controls share visual dimensions" `
    $verification `
    'bgTex\.width\s*=\s*SaveSnapshotSlotControls\.VerificationButtonSize;\s*bgTex\.height\s*=\s*SaveSnapshotSlotControls\.VerificationButtonSize'

Write-Host "Snapshot archive routing and layout contracts passed."
