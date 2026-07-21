param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$palettePath = Join-Path $repo 'ShelteredAPI\Scenarios\Presentation\UiKit\Theme\ScenarioUiPalette.cs'
$stylePath = Join-Path $repo 'ShelteredAPI\Scenarios\Presentation\UiKit\ScenarioUiStyleSheet.cs'
$palette = Get-Content -LiteralPath $palettePath -Raw
$styles = Get-Content -LiteralPath $stylePath -Raw

$expected = [ordered]@{
    SurfacePage='35, 23, 45'; SurfaceCard='209, 198, 165'; SurfaceCardHover='229, 217, 184'
    SurfaceCardSelected='169, 162, 115'; SurfaceInset='190, 180, 155'; SurfaceChrome='90, 55, 28'
    SurfaceDisabled='198, 190, 167'; SurfaceViewport='22, 18, 15'; DepthShadow='22, 18, 15'
    BorderDefault='89, 88, 88'; BorderStrong='74, 41, 33'; BorderHighlight='234, 224, 195'
    BorderFocus='209, 102, 202'; TextPrimary='32, 30, 30'; TextSecondary='89, 88, 88'
    TextMuted='122, 95, 72'; TextInverse='234, 224, 195'; TextInverseMuted='194, 194, 194'
    TextDisabled='122, 95, 72'; AccentGold='156, 120, 52'; SemanticReady='137, 245, 116'
    SemanticReadyStrong='75, 180, 60'; SemanticWarning='219, 192, 134'; SemanticWarningStrong='156, 120, 52'
    SemanticError='250, 148, 143'; SemanticErrorStrong='197, 54, 46'; SemanticInfo='144, 153, 161'
    SemanticInfoStrong='102, 140, 163'; ControlPressed='122, 95, 72'; WorkspaceStory='145, 73, 70'
    WorkspaceCast='147, 96, 124'; WorkspaceSupplies='88, 123, 66'; WorkspaceMap='66, 102, 136'
    WorkspaceTest='127, 145, 145'; WorkspacePublish='156, 120, 52'
}

foreach ($entry in $expected.GetEnumerator()) {
    $pattern = 'public Color ' + [regex]::Escape($entry.Key) + ' \{ get \{ return Token\(' + [regex]::Escape($entry.Value) + '\); \} \}'
    if ($palette -notmatch $pattern) { throw "Design token mismatch: $($entry.Key)." }
}

if ($palette -notmatch 'return new Color32\(red, green, blue, 255\)') { throw 'Opaque token helper must force alpha 255.' }
if ($palette -notmatch 'SurfaceScrim.*new Color32\(0, 0, 0, 184\)') { throw 'SurfaceScrim must be the exact #000000B8 token.' }
if ($styles -match 'WithPanelOpacity|WithRaisedOpacity|WithActiveOpacity') { throw 'Material style construction must not apply legacy opacity helpers.' }
if ($styles -notmatch 'Texture2D card = ProceduralTextureLibrary\.MaterialSurface\(MaterialSurfaceTier\.RaisedCard') { throw 'Card material must use the opaque raised-card generator.' }

Write-Host "PASS: Scenario design tokens are byte-exact; all material tokens, including card, are alpha 255."
