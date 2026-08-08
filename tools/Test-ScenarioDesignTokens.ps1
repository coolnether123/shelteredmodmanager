param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$palettePath = Join-Path $repo 'ShelteredScenarioEditor\Presentation\UiKit\Theme\ScenarioUiPalette.cs'
$stylePath = Join-Path $repo 'ShelteredScenarioEditor\Presentation\UiKit\ScenarioUiStyleSheet.cs'
$palette = Get-Content -LiteralPath $palettePath -Raw
$styles = Get-Content -LiteralPath $stylePath -Raw

$expected = [ordered]@{
    SurfacePage='23, 26, 28'; SurfaceCard='36, 40, 43'; SurfaceCardHover='45, 51, 55'
    SurfaceCardSelected='58, 52, 40'; SurfaceInset='17, 20, 22'; SurfaceChrome='30, 35, 38'
    SurfaceDisabled='42, 46, 48'; SurfaceViewport='13, 15, 16'; DepthShadow='7, 8, 9'
    BorderDefault='75, 83, 88'; BorderStrong='113, 123, 128'; BorderHighlight='170, 178, 181'
    BorderFocus='214, 168, 75'; TextPrimary='241, 238, 230'; TextSecondary='195, 199, 200'
    TextMuted='150, 157, 159'; TextInverse='241, 238, 230'; TextInverseMuted='174, 181, 183'
    TextDisabled='105, 113, 117'; TextOnAccent='23, 19, 10'; AccentGold='214, 168, 75'
    SemanticReady='37, 76, 56'; SemanticReadyStrong='63, 140, 98'; SemanticWarning='91, 67, 30'
    SemanticWarningStrong='154, 107, 36'; SemanticError='90, 41, 39'; SemanticErrorStrong='168, 71, 64'
    SemanticInfo='38, 69, 85'; SemanticInfoStrong='57, 123, 153'; ControlPressed='20, 23, 25'
    WorkspaceStory='113, 62, 52'; WorkspaceCast='94, 70, 103'; WorkspaceSupplies='72, 90, 53'
    WorkspaceMap='61, 88, 112'; WorkspaceTest='62, 98, 97'; WorkspacePublish='115, 90, 39'
}

foreach ($entry in $expected.GetEnumerator()) {
    $pattern = 'public Color ' + [regex]::Escape($entry.Key) + ' \{ get \{ return Token\(' + [regex]::Escape($entry.Value) + '\); \} \}'
    if ($palette -notmatch $pattern) { throw "Design token mismatch: $($entry.Key)." }
}

if ($palette -notmatch 'return new Color32\(red, green, blue, 255\)') { throw 'Opaque token helper must force alpha 255.' }
if ($palette -notmatch 'SurfaceScrim.*new Color32\(0, 0, 0, 184\)') { throw 'SurfaceScrim must be the exact #000000B8 token.' }
if ($styles -match 'WithPanelOpacity|WithRaisedOpacity|WithActiveOpacity') { throw 'Material style construction must not apply legacy opacity helpers.' }
if ($styles -notmatch 'Texture2D card = ProceduralTextureLibrary\.MaterialSurface\(MaterialSurfaceTier\.RaisedCard') { throw 'Card material must use the opaque raised-card generator.' }
if ($styles -notmatch 'ButtonEmphasized = BuildButton\(gold, warning, pressed, p\.TextOnAccent, p\.TextPrimary, p\.TextPrimary') { throw 'Emphasized buttons must use the dedicated readable text-on-accent token.' }
if ($styles -notmatch 'TabActive = BuildButton\(gold, pressed, pressed, p\.TextOnAccent, p\.TextPrimary, p\.TextPrimary') { throw 'Active tabs must use the dedicated readable text-on-accent token.' }

Write-Host "PASS: Scenario design tokens are byte-exact; all material tokens, including card, are alpha 255."
