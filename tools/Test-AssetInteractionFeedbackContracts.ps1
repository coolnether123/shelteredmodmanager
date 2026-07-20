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
$spriteTargetResolver = Read-Source 'ShelteredAPI\Scenarios\Infrastructure\Assets\ScenarioSpriteRuntimeResolver.cs'
$spriteSwapRenderer = Read-Source 'ShelteredAPI\Scenarios\Infrastructure\Assets\ScenarioSpriteSwapRenderer.cs'
$camera = Read-Source 'ShelteredAPI\Scenarios\Infrastructure\Unity\ScenarioAuthoringEditorCameraService.cs'
$spriteCatalog = Read-Source 'ShelteredAPI\Scenarios\Infrastructure\Assets\ScenarioSpriteCatalogService.cs'
$spriteReferenceLibrary = Read-Source 'ShelteredAPI\Scenarios\Infrastructure\Assets\ScenarioSpriteReferenceLibrary.cs'
$toolRail = Read-Source 'ShelteredAPI\Scenarios\Presentation\Authoring\Shell\Rendering\ScenarioAuthoringShellToolRailImguiRenderer.cs'
$bounds = Read-Source 'ModAPI\Inspector\BoundsHighlighter.cs'
$interaction = Read-Source 'ShelteredAPI\Scenarios\Infrastructure\Unity\ScenarioVanillaInteractionRuntimeService.cs'
$editorController = Read-Source 'ShelteredAPI\Scenarios\Application\Authoring\ScenarioEditorController.cs'
$authoringBackend = Read-Source 'ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthoringBackendService.cs'
$shellWindowRenderer = Read-Source 'ShelteredAPI\Scenarios\Presentation\Authoring\Shell\Rendering\ScenarioAuthoringShellWindowImguiRenderer.cs'
$shellRenderer = Read-Source 'ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioAuthoringShellImguiRenderModule.cs'
$surfaceGate = Read-Source 'ShelteredAPI\Scenarios\Presentation\Authoring\Shell\Rendering\ScenarioAuthoringShellVisualSurfaceGate.cs'
$layoutService = Read-Source 'ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioAuthoringLayoutService.cs'
$timelineRibbon = Read-Source 'ShelteredAPI\Scenarios\Presentation\Authoring\Shell\Rendering\ScenarioAuthoringShellTimelineRibbonImguiRenderer.cs'
$entryFlow = Read-Source 'ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthoringEntryFlowService.cs'
$assetBrowserRenderer = Read-Source 'ShelteredAPI\Scenarios\Presentation\Authoring\Shell\Rendering\ScenarioAuthoringShellAssetBrowserImguiRenderer.cs'
$scenarioDefinitionService = Read-Source 'ShelteredAPI\Scenarios\Definitions\ScenarioDefinitionService.cs'
$scenarioNameRegistry = Read-Source 'ShelteredAPI\Scenarios\Infrastructure\Runtime\ScenarioCharacterRuntimeNameRegistry.cs'
$customScenarioPatches = Read-Source 'ShelteredAPI\Scenarios\Infrastructure\Harmony\ShelteredCustomScenarioPatches.cs'
$scenarioDefBuilder = Read-Source 'ShelteredAPI\Scenarios\Infrastructure\Unity\ShelteredScenarioDefBuilder.cs'

Assert-Match 'overlay API registration' $bootstrap 'RegisterApi\(OverlayInputCaptureApi\.Name, new ShelteredOverlayInputCaptureService\(\)\)'
Assert-Match 'editor keyboard ownership' $inputCapture 'editorKeyboardCaptured.*state\.ShellVisible.*!ScenarioAuthoringRuntimeGuards\.IsPlaytesting\(\).*UpdateOverlayInputCapture\(ShouldSuppressWorldInputNow\(\), editorKeyboardCaptured\)'
Assert-Match 'editor capture release' $inputCapture 'Clear\(\).*UpdateOverlayInputCapture\(false, false\)'
Assert-Match 'NGUI keyboard suppression' $inputPatches 'HarmonyPatch\(typeof\(UICamera\), "ProcessOthers"\).*ShouldSuppressKeyboardInput'
Assert-Match 'gameplay input suppression' $platformInput 'InputButtonDownPrefix.*TrySuppressButton.*InputAxisRawPrefix.*TrySuppressAxis'
Assert-Match 'animated frame world preview driver' $spriteRuntime 'TryPreviewEditedFrame.*ConfigurePreview\(new\[\] \{ frame \}, null, 1f\)'
Assert-Match 'pixel edits route to current frame preview' $spriteAuthoring 'ApplyCustomEditorPreview.*IsAnimationEditor\(\).*TryPreviewEditedFrame'
Assert-Match 'pixel editor close restores preview' $spriteAuthoring 'ClearCustomEditorSession.*StopWorldAnimationPreview'
Assert-Match 'pixel editor close zoom' $camera 'PixelEditorMinZoom = 0\.60f.*PixelEditorFitScale = 0\.55f.*PixelEditorMaxZoom = 0\.85f.*fitHeight \* PixelEditorFitScale'
Assert-Match 'pixel editor zoom lock survives easing' $camera '_cameraLockActive \? PixelEditorMinZoom : MinZoom.*ClampZoom\(camera, basicCamera, _targetOrthographicSize, minZoom\).*ClampZoom\(camera, basicCamera, camera\.orthographicSize, minZoom\)'
Assert-Match 'pixel editor camera bypasses gameplay bounds' $camera '_cameraLockActive\).*ApplyAssetFrame\(camera, basicCamera\);.*ApplyEasedZoom\(camera, basicCamera\);.*return;.*camera\.transform\.position = PreserveCameraZ\(camera, position\)'
Assert-Match 'pixel editor suspends vanilla camera owner' $camera '_savedBasicCameraEnabled = basicCamera != null && basicCamera\.enabled.*basicCamera\.enabled = false.*_savedBasicCamera\.enabled = _savedBasicCameraEnabled'
Assert-Match 'pixel editor camera lock updates the displayed main camera' $camera 'BasicCamera basicCamera = ResolveBasicCamera\(\);.*Camera camera = Camera\.main;.*if \(camera == null && basicCamera != null\).*camera = basicCamera\.GetComponent<Camera>\(\);'
Assert-Match 'pixel editor camera lock overrides blocking-panel early exit' $camera 'if \(!_cameraLockActive && !CanRunCameraUpdate\(\)\).*_targetOrthographicSize = -1f;.*if \(_cameraLockActive\).*ApplyAssetFrame\(camera, basicCamera\);.*ApplyEasedZoom\(camera, basicCamera\);'
Assert-Match 'pixel editor hides and restores the survivor details card' $camera 'Object\.FindObjectsOfType<UI_Avatar>\(\).*avatarObject\.SetActive\(false\).*savedAvatar\.GameObject\.SetActive\(savedAvatar\.ActiveSelf\).*_savedAvatarVisibility\.Clear\(\)'
Assert-Match 'pixel editor frames the edited runtime sprite rather than displaced preview bounds' $camera 'TryResolveTargetBounds\(target, runtimeTargetPath, out bounds\).*float frameCenterX = target\.WorldPosition\.x.*ResolveGameObject\(target, runtimeTargetPath\).*frameCenterX = targetObject\.transform\.position\.x.*new Vector3\(frameCenterX, bounds\.center\.y'
Assert-Match 'pixel editor frames a placeable owning object instead of a nested hit proxy' $camera 'target\.Kind == ScenarioAuthoringTargetKind\.PlaceableObject.*GetComponentInParent<Obj_Base>\(\).*return owningObject\.gameObject'
Assert-Match 'pixel editor vertically centers low-mounted assets' $camera 'float desiredScreenY = Screen\.height \* 0\.5f'
Assert-Match 'pixel editor ignores auxiliary overlay renderers when framing' $camera 'GetComponentsInChildren<SpriteRenderer>\(true\).*candidate\.gameObject\.activeInHierarchy.*candidateBounds\.size\.x \* candidateBounds\.size\.y.*bounds = primarySprite\.bounds'
Assert-Match 'persisted pixel patches prefer the visible runtime sprite base' $spriteReferenceLibrary 'Resources\.FindObjectsOfTypeAll<SpriteRenderer>\(\).*renderer\.gameObject\.activeInHierarchy.*CreateRuntimeSpriteKey\(renderedSprite\).*sprite = renderedSprite'
Assert-Match 'legacy runtime-key patches choose the non-transparent base deterministically' $spriteReferenceLibrary 'Resources\.FindObjectsOfTypeAll<Sprite>\(\).*double bestAlphaScore = -1d.*candidate\.texture\.GetPixels.*alphaScore \+= pixels\[pixelIndex\]\.a.*sprite = bestMatchingSprite'
Assert-Match 'new pixel patches persist an exact baseline asset' $spriteAuthoring 'Assets/PixelPatchBases/.*baselineTexture\.EncodeToPNG\(\).*File\.WriteAllBytes\(baselinePath, encodedBaseline\).*baseSpriteId = null;.*baseRuntimeSpriteKey = null;'
Assert-Match 'sprite catalog cache never crosses target or scenario scope' $spriteCatalog 'sameCatalogScope = _cachedCatalog != null.*string\.Equals\(_cachedTargetPath, targetPath.*string\.Equals\(_cachedScenarioFilePath, scenarioFilePath.*if \(sameCatalogScope\).*ScheduleCatalogRefresh'
Assert-Match 'sprite resolution prefers durable target path over transient runtime proxy' $spriteTargetResolver 'Transform transform = FindTransformByPath\(targetPath\);.*if \(transform == null\).*Transform runtimeTransform = ResolveTransform\(authoringTarget\);'
Assert-Match 'sprite resolution reconciles descendant hit proxies to durable ancestor' $spriteTargetResolver 'Transform runtimeTransform = ResolveTransform\(authoringTarget\);.*while \(current != null\).*BuildTransformPath\(current\), targetPath.*transform = current.*current = current\.parent.*transform = runtimeTransform'
Assert-Match 'sprite path resolution caches live roots outside the active scene' $spriteTargetResolver 'ExternalRootCache.*Resources\.FindObjectsOfTypeAll<Transform>\(\).*candidate\.parent != null.*ExternalRootCache\[segments\[0\]\] = externalRoot.*FindChildByName\(current, segments\[segmentIndex\]\)'
Assert-Match 'live pixel preview prefers the nearest then largest active object sprite' $spriteTargetResolver 'GetComponentsInChildren<SpriteRenderer>\(true\).*candidate\.gameObject\.activeInHierarchy.*candidateDepth < nearestActiveDepth.*candidateDepth == nearestActiveDepth && candidateArea > largestActiveArea.*spriteRenderer = candidate'
Assert-Match 'saved sprite rules retain the exact rendered child' $spriteTargetResolver 'TargetPath = BuildTransformPath\(spriteRenderer\.transform\).*Transform = spriteRenderer\.transform.*TargetPath = BuildTransformPath\(ui2DSprite\.transform\).*TargetPath = BuildTransformPath\(particleRenderer\.transform\)'
Assert-Match 'scenario sprite replacement preserves the live target geometry' $spriteSwapRenderer 'ResolveAlignedReplacement\(entry\.TargetPath, entry\.Sprite\).*baseline\.Sprite\.pivot\.x / baselineRect\.width.*baseline\.Sprite\.pixelsPerUnit'
Assert-Match 'pixel editor suppresses tool rail' $toolRail 'PixelEditorChromeSuppressed.*ZeroRect'
Assert-Match 'pixel editor tools fit without scrollbars' $shellWindowRenderer 'DrawCustomSpriteEditorDedicated\([^{]+\)\s*\{(?:(?!GUILayout\.BeginScrollView).)*DrawPixelCanvasViewport'
Assert-Match 'pixel editor status stays outside the canvas' $shellWindowRenderer 'const float statusHeight.*Rect inner = new Rect\(.*viewportRect\.height - statusHeight - 20f.*Rect statusRect = new Rect\(.*GUI\.Label\(statusRect'
Assert-Match 'inactive upgrade renderers excluded' $bounds '!spriteRenderer\.gameObject\.activeInHierarchy'
Assert-Match 'authoring hover feeds vanilla interaction' $interaction 'state\.HoveredTarget.*ResolveObjBase\(authoringObject\).*SelectInteractionObject'
Assert-Match 'right click restores a usable family selection' $interaction 'EnsureSelectedFamilyMember.*GetFamilyMemberByIndex.*SelectFamilyMemberByIndex'
Assert-Match 'saved draft metadata is republished immediately' $editorController '_serializer\.Save\(session\.WorkingDefinition, path\);.*?_serializer\.LoadInfo\(path, ScenarioAuthoringDraftRepository\.DraftOwnerId\);'
Assert-Match 'explicit selection mutates backend-owned state' $authoringBackend 'TrySelectRuntimeObject\(.*?lock \(_sync\).*?_selectionService\.TrySelectRuntimeObject\(_state, gameObject, out target, out message\)'
Assert-Match 'build palette card text reserves badge space' $shellWindowRenderer 'cardHeight = buildPaletteSection \? 88f : 94f.*DrawCandidateCard\(rect, item\.Action, false, false, buildPaletteSection\).*rect\.height - 28f.*suppressSecondaryText\s*\? null.*badgeTop = !string\.IsNullOrEmpty\(action\.Badge\) \? rect\.yMax - 22f.*detailHeight = badgeTop - detailTop - 4f.*detailHeight >= 14f'
Assert-Match 'transient authoring utilities start closed' $layoutService 'LoadLayout\(state\);.*HideStartupUtilityWindows\(state\);.*TilesPalette.*PixelEditor'
Assert-Match 'window controls obey global z order' ($shellRenderer + $surfaceGate) 'DrawWindowCoreWithInputGate.*GUI\.enabled = previousEnabled.*IsVisualSurfaceTopmost\(surfaceId, rect\).*IsVisualSurfaceTopmost\(VisualSurfaceIdForWindow\(window\.Id\), rect\)'
Assert-Match 'floating window drag persists only on release' $shellRenderer 'eventPrimaryUp.*UpdateFloatingWindowDrag\(window, mouse, contentRect, true\).*MouseDrag.*UpdateFloatingWindowDrag\(window, mouse, contentRect, false\).*_dragLastRect = next;.*if \(persist\).*CommitFloatingWindowFrame'
Assert-Match 'resize grip clips inside its window' ($shellRenderer + $shellWindowRenderer) '_resizeGripStyle\.alignment = TextAnchor\.LowerRight.*_resizeGripStyle\.clipping = TextClipping\.Clip.*GUI\.Label\(gripRect, "///", _resizeGripStyle'
Assert-Match 'quick settings use one responsive card state' $entryFlow 'GUI\.Button\(rect, content, _cardButtonStyle\).*DrawCardStateOverlay\(rect, true, toggle != null && toggle\.On\)'
Assert-Match 'story title preserves descenders' $timelineRibbon 'float titleHeight = Mathf\.Clamp\(.*_timelineRibbonTitleStyle\.CalcHeight.*labelRect\.y \+ titleHeight'
Assert-Match 'story notes receive measured full width rows' $shellWindowRenderer 'bool fullWidthText = item\.Action == null.*if \(fullWidthText\).*float textHeight = Mathf\.Clamp\(.*_textStyle\.CalcHeight.*180f\);.*DrawFactCell\(noteRect, item\)'
Assert-Match 'object details reserve a separate badge row' $shellWindowRenderer 'float detailTop = textRect\.y \+ labelHeight \+ 2f;.*float detailHeight = badgeTop - detailTop - 4f;.*GUI\.Label\(new Rect\(textRect\.x, detailTop, textRect\.width, detailHeight\)'
Assert-Match 'survivor condition is explicit starting state' $shellWindowRenderer '"Starting Condition"'
Assert-Match 'asset cards reserve detail for selected pane' $assetBrowserRenderer 'Selected Asset pane owns technical/source.*DrawCandidateCard\(cardRect, item\.Action, armPlacementOnCardClick, true, true\)'
Assert-Match 'playable definitions register authored scenario character names' $scenarioDefinitionService 'ScenarioCharacterRuntimeNameRegistry\.Register\(definition\)'
Assert-Match 'scenario visitor names map by scenario and character id' $scenarioNameRegistry 'NamesByScenario.*character\.CharacterId.*character\.DisplayName.*instance\.stage\.characterIds.*GetCharacterInfo\(characterId\).*visitorCharacterInfo.*ApplyDisplayName\(visitorInfo, displayName\).*return appliedCount.*m_preset\.m_firstName = displayName.*m_randomizeName = false'
Assert-Match 'scenario visitor names apply after stage assignment and before visitor spawn' $customScenarioPatches 'HarmonyPatch\(typeof\(NpcVisitManager\), "AddNewScenario"\).*HarmonyPrefix.*AddNewScenarioPrefix\(.*List<QuestManager\.QuestCharacterInfo> charInfo,.*int scenarioId\).*ScenarioCharacterRuntimeNameRegistry\.ApplyToPendingStage\(scenarioId, charInfo\).*ScenarioCharacterRuntimeNames.*authored visitor preset name'
Assert-Match 'invalid authored quest presets resolve to a deterministic vanilla preset' $scenarioDefBuilder 'QuestCharacterPresetsField.*ResolveQuestCharacterPresetId\(character\.PresetId\).*string\.Equals\(preset\.m_id, requestedPresetId, StringComparison\.OrdinalIgnoreCase\).*return fallback != null \? fallback\.m_id : requestedPresetId'

if ($failures.Count -gt 0) {
    throw ('Asset/interaction feedback contracts failed: ' + ($failures -join ', '))
}

Write-Host 'Asset/interaction feedback contracts passed.'
