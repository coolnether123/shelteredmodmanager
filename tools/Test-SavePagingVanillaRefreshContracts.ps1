$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$coordinator = Get-Content -Raw (Join-Path $repoRoot 'ShelteredAPI\Saves\Paging\SlotSelectionPatchCoordinator.cs')
$registry = Get-Content -Raw (Join-Path $repoRoot 'ShelteredAPI\Saves\SaveRegistryCore.cs')

$refresh = [regex]::Match(
    $coordinator,
    '(?s)private static void RefreshVanillaSaveSlotInfo\(SlotSelectionPanel panel\)\s*\{(?<body>.*?)\n\s*\}\s*\n\s*private static object CreateSlotInfo'
)
if (-not $refresh.Success) {
    throw 'FAIL: Could not locate the vanilla-page refresh body.'
}
if ($refresh.Groups['body'].Value -match 'ImportStandardVanillaSlotsIfNeeded') {
    throw 'FAIL: Vanilla-page refresh performs a duplicate bulk mirror-import pass.'
}
if ($refresh.Groups['body'].Value -notmatch 'SlotSelectionSaveEntryResolver\.Resolve\(panel\)') {
    throw 'FAIL: Vanilla-page refresh does not resolve one shared visible-save list.'
}
if ($refresh.Groups['body'].Value -notmatch 'UpdateSaveSlotAuxiliaryControls\(panel, visibleSaves\)') {
    throw 'FAIL: Auxiliary controls do not reuse the shared visible-save list.'
}

$populate = [regex]::Match(
    $coordinator,
    '(?s)private static void PopulateVanillaSlotInfo\(object slotInfo, int slotNumber, SaveEntry imported\)\s*\{(?<body>.*?)\n\s*\}\s*\n\s*private static void ClearSlotInfo'
)
if (-not $populate.Success) {
    throw 'FAIL: Could not locate per-slot vanilla population.'
}
$perSlotCalls = [regex]::Matches($populate.Groups['body'].Value, 'ImportStandardVanillaSlotIfNeeded\(').Count
if ($perSlotCalls -ne 0) {
    throw "FAIL: Per-slot population repeats mirror import/comparison $perSlotCalls time(s)."
}
if ($registry -match 'void ImportStandardVanillaSlotsIfNeeded\(') {
    throw 'FAIL: The unused duplicate bulk-import helper remains in SaveRegistryCore.'
}

Write-Output 'PASS: page-0 refresh resolves each visible vanilla slot once and reuses the result.'
