[CmdletBinding()]
param([string]$RepoRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

$failures = New-Object 'System.Collections.Generic.List[string]'
function Read-Source([string]$relativePath) {
    return Get-Content -LiteralPath (Join-Path $RepoRoot $relativePath) -Raw
}
function Assert-Match([string]$name, [string]$text, [string]$pattern) {
    if (-not [regex]::IsMatch($text, $pattern, [Text.RegularExpressions.RegexOptions]::Singleline)) {
        $failures.Add($name)
    }
}

$bootstrap = Read-Source 'ShelteredAPI\Core\ShelteredApiRuntimeBootstrap.cs'
$inputCapture = Read-Source 'ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioAuthoringInputCaptureService.cs'
$inputPatches = Read-Source 'ShelteredAPI\Harmony\OverlayInputCapturePatches.cs'
$platformInput = Read-Source 'ShelteredAPI\Harmony\PlatformInputKeybindPatches.cs'
$spriteAuthoring = Read-Source 'ShelteredAPI\Scenarios\Application\Assets\ScenarioSpriteSwapAuthoringService.cs'
$spriteRuntime = Read-Source 'ShelteredAPI\Scenarios\Infrastructure\Assets\ScenarioSpriteRuntimeMutationService.cs'
$camera = Read-Source 'ShelteredAPI\Scenarios\Infrastructure\Unity\ScenarioAuthoringEditorCameraService.cs'
$toolRail = Read-Source 'ShelteredAPI\Scenarios\Presentation\Authoring\Shell\Rendering\ScenarioAuthoringShellToolRailImguiRenderer.cs'
$bounds = Read-Source 'ModAPI\Inspector\BoundsHighlighter.cs'
$interaction = Read-Source 'ShelteredAPI\Scenarios\Infrastructure\Unity\ScenarioVanillaInteractionRuntimeService.cs'
$editorController = Read-Source 'ShelteredAPI\Scenarios\Application\Authoring\ScenarioEditorController.cs'
$authoringBackend = Read-Source 'ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthoringBackendService.cs'

Assert-Match 'overlay API registration' $bootstrap 'RegisterApi\(OverlayInputCaptureApi\.Name, new ShelteredOverlayInputCaptureService\(\)\)'
Assert-Match 'editor keyboard ownership' $inputCapture 'editorKeyboardCaptured.*state\.ShellVisible.*!ScenarioAuthoringRuntimeGuards\.IsPlaytesting\(\).*UpdateOverlayInputCapture\(ShouldSuppressWorldInputNow\(\), editorKeyboardCaptured\)'
Assert-Match 'editor capture release' $inputCapture 'Clear\(\).*UpdateOverlayInputCapture\(false, false\)'
Assert-Match 'NGUI keyboard suppression' $inputPatches 'HarmonyPatch\(typeof\(UICamera\), "ProcessOthers"\).*ShouldSuppressKeyboardInput'
Assert-Match 'gameplay input suppression' $platformInput 'InputButtonDownPrefix.*TrySuppressButton.*InputAxisRawPrefix.*TrySuppressAxis'
Assert-Match 'animated frame world preview driver' $spriteRuntime 'TryPreviewEditedFrame.*ConfigurePreview\(new\[\] \{ frame \}, null, 1f\)'
Assert-Match 'pixel edits route to current frame preview' $spriteAuthoring 'ApplyCustomEditorPreview.*IsAnimationEditor\(\).*TryPreviewEditedFrame'
Assert-Match 'pixel editor close restores preview' $spriteAuthoring 'ClearCustomEditorSession.*StopWorldAnimationPreview'
Assert-Match 'pixel editor close zoom' $camera 'PixelEditorMinZoom = 0\.75f.*fitHeight \* 0\.62f'
Assert-Match 'pixel editor suppresses tool rail' $toolRail 'PixelEditorChromeSuppressed.*ZeroRect'
Assert-Match 'inactive upgrade renderers excluded' $bounds '!spriteRenderer\.gameObject\.activeInHierarchy'
Assert-Match 'authoring hover feeds vanilla interaction' $interaction 'state\.HoveredTarget.*ResolveObjBase\(authoringObject\).*SelectInteractionObject'
Assert-Match 'right click restores a usable family selection' $interaction 'EnsureSelectedFamilyMember.*GetFamilyMemberByIndex.*SelectFamilyMemberByIndex'
Assert-Match 'saved draft metadata is republished immediately' $editorController '_serializer\.Save\(session\.WorkingDefinition, path\);.*?_serializer\.LoadInfo\(path, ScenarioAuthoringDraftRepository\.DraftOwnerId\);'
Assert-Match 'explicit selection mutates backend-owned state' $authoringBackend 'TrySelectRuntimeObject\(.*?lock \(_sync\).*?_selectionService\.TrySelectRuntimeObject\(_state, gameObject, out target, out message\)'

if ($failures.Count -gt 0) {
    throw ('Asset/interaction feedback contracts failed: ' + ($failures -join ', '))
}

Write-Host 'Asset/interaction feedback contracts passed.'
