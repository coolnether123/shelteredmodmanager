[CmdletBinding()]
param(
    [string]$RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}
else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

$failures = New-Object 'System.Collections.Generic.List[string]'
function Add-Failure([string]$Message) { $failures.Add($Message) }

$editorProjectPath = Join-Path $RepoRoot 'ShelteredScenarioEditor\ShelteredScenarioEditor.csproj'
$apiProjectPath = Join-Path $RepoRoot 'ShelteredAPI\ShelteredAPI.csproj'
$managerProjectPath = Join-Path $RepoRoot 'Manager\ManagerGUI.csproj'
$solutionPath = Join-Path $RepoRoot 'ShelteredModManager.sln'
foreach ($requiredPath in @($editorProjectPath, $apiProjectPath, $managerProjectPath, $solutionPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        Add-Failure "Missing architecture input '$requiredPath'."
    }
}

if ($failures.Count -eq 0) {
    [xml]$editorProject = Get-Content -LiteralPath $editorProjectPath -Raw
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($editorProject.NameTable)
    $namespaceManager.AddNamespace('msb', 'http://schemas.microsoft.com/developer/msbuild/2003')

    $frameworks = @($editorProject.SelectNodes('//msb:TargetFrameworkVersion', $namespaceManager) | ForEach-Object { $_.InnerText })
    if ($frameworks.Count -eq 0 -or @($frameworks | Where-Object { $_ -ne 'v3.5' }).Count -gt 0) {
        Add-Failure 'ShelteredScenarioEditor must target the game-compatible .NET Framework v3.5 surface in every configuration.'
    }

    $outputPaths = @($editorProject.SelectNodes('//msb:OutputPath', $namespaceManager) | ForEach-Object { $_.InnerText.Replace('/', '\') })
    if ($outputPaths.Count -eq 0 -or @($outputPaths | Where-Object { $_ -ne '..\Dist\SMM\bin\' }).Count -gt 0) {
        Add-Failure 'ShelteredScenarioEditor build output must remain Dist\SMM\bin for every configuration.'
    }

    $projectReferences = @($editorProject.SelectNodes('//msb:ProjectReference', $namespaceManager))
    $referenceNames = @($projectReferences | ForEach-Object { $_.Name })
    foreach ($requiredReference in @('ModAPI', 'ShelteredAPI')) {
        if ($referenceNames -notcontains $requiredReference) {
            Add-Failure "ShelteredScenarioEditor must reference $requiredReference."
        }
    }
    foreach ($reference in $projectReferences) {
        if ($reference.Private -ne 'False') {
            Add-Failure "Project reference '$($reference.Name)' must use Private=False so the editor does not duplicate shared runtime assemblies."
        }
    }

    $editorProjectSource = Get-Content -LiteralPath $editorProjectPath -Raw
    $apiProjectSource = Get-Content -LiteralPath $apiProjectPath -Raw
    $managerProjectSource = Get-Content -LiteralPath $managerProjectPath -Raw
    if ($apiProjectSource -match 'ShelteredScenarioEditor') {
        Add-Failure 'ShelteredAPI.csproj must not reference ShelteredScenarioEditor; dependency direction is editor -> API only.'
    }

    $pixelEditingFiles = @(
        'PixelClipboard.cs',
        'PixelDocument.cs',
        'PixelEditHistory.cs',
        'PixelEditorContracts.cs',
        'PixelEditorSession.cs',
        'PixelSelection.cs',
        'Rgba32.cs'
    )
    foreach ($pixelEditingFile in $pixelEditingFiles) {
        $pixelInclude = 'Shared\PixelEditing\' + $pixelEditingFile
        if (-not $editorProjectSource.Contains($pixelInclude)) {
            Add-Failure "ShelteredScenarioEditor must compile its owned pixel-editing source '$pixelEditingFile'."
        }
        if (-not $managerProjectSource.Contains($pixelInclude)) {
            Add-Failure "Manager must compile its owned pixel-editing source '$pixelEditingFile'."
        }
        if ($apiProjectSource.Contains($pixelInclude)) {
            Add-Failure "ShelteredAPI must not compile editor/manager pixel implementation '$pixelEditingFile'."
        }

        $pixelSourcePath = Join-Path $RepoRoot $pixelInclude
        $pixelSource = Get-Content -LiteralPath $pixelSourcePath -Raw
        if ($pixelSource -match '(?m)^\s*public\s+(?:(?:sealed|static|abstract)\s+)?(?:class|enum|interface|struct)\s+') {
            Add-Failure "Linked pixel implementation '$pixelEditingFile' must remain internal so the editor DLL exports no pixel API."
        }
    }

    $solutionSource = Get-Content -LiteralPath $solutionPath -Raw
    if ($solutionSource -notmatch '(?m)^Project\([^\r\n]+\) = "ShelteredScenarioEditor", "ShelteredScenarioEditor\\ShelteredScenarioEditor\.csproj"') {
        Add-Failure 'ShelteredScenarioEditor is not registered as a first-class project in ShelteredModManager.sln.'
    }
    foreach ($platform in @('Any CPU', 'x64', 'x86')) {
        $escapedPlatform = [Text.RegularExpressions.Regex]::Escape($platform)
        foreach ($mapping in @('ActiveCfg', 'Build\.0')) {
            $releaseMapping = '(?m)^\s*\{D819807F-394C-4AA4-92C0-A214BF5FBF62\}\.Verbose_Release\|' +
                $escapedPlatform + '\.' + $mapping + '\s*=\s*Release\|Any CPU\s*$'
            if ($solutionSource -notmatch $releaseMapping) {
                Add-Failure "ShelteredScenarioEditor Verbose_Release|$platform $($mapping.Replace('\', '')) is not mapped to Release|Any CPU."
            }
        }
    }

    $apiSources = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'ShelteredAPI') -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })
    foreach ($sourceFile in $apiSources) {
        $source = Get-Content -LiteralPath $sourceFile.FullName -Raw
        if ($source -match '(?m)^\s*using\s+ShelteredScenarioEditor(?:\.|;)' -or $source -match '\bShelteredScenarioEditor\.[A-Za-z_]') {
            Add-Failure "ShelteredAPI source references the optional editor assembly: $($sourceFile.FullName.Substring($RepoRoot.Length + 1))."
        }
        if ($source -match 'InternalsVisibleTo\s*\(\s*"ShelteredScenarioEditor"') {
            Add-Failure 'ShelteredAPI must not grant internals access to ShelteredScenarioEditor; required modder/editor hooks belong in explicit public API contracts.'
        }
        if ($source -match '(?m)^\s*using\s+ShelteredModManager\.Shared\.PixelEditing\s*;') {
            Add-Failure "ShelteredAPI source imports editor/manager pixel implementation: $($sourceFile.FullName.Substring($RepoRoot.Length + 1))."
        }
    }

    $editorSources = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'ShelteredScenarioEditor') -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })
    foreach ($sourceFile in $editorSources) {
        $source = Get-Content -LiteralPath $sourceFile.FullName -Raw
        if ($source -match '(?m)^\s*namespace\s+ShelteredAPI(?:\.|\s|\{)') {
            Add-Failure "Editor-owned source still declares a ShelteredAPI namespace: $($sourceFile.FullName.Substring($RepoRoot.Length + 1))."
        }
        if ($source -match '(?m)^\s*using\s+ShelteredAPI\.Scenarios\.(Application|Infrastructure)\.') {
            Add-Failure "Editor source imports a ShelteredAPI implementation namespace instead of a documented facade: $($sourceFile.FullName.Substring($RepoRoot.Length + 1))."
        }
        if ($source -match 'ShelteredAPI\.ScenarioAuthoring\.EntryFlow') {
            Add-Failure "Editor runtime object names must identify ShelteredScenarioEditor ownership: $($sourceFile.FullName.Substring($RepoRoot.Length + 1))."
        }
        if ($source -match '(?m)^\s*public\s+(?:(?:sealed|static|abstract)\s+)?(?:class|enum|interface|struct)\s+') {
            Add-Failure "The standalone editor implementation must not export public types: $($sourceFile.FullName.Substring($RepoRoot.Length + 1))."
        }
    }

    foreach ($removedPolicy in @(
        'Application\Authoring\ScenarioDefinitionIndex.cs',
        'Domain\Validation\ScenarioStoryFlowValidationAnalyzer.cs',
        'Application\Runtime\ScenarioPlayStartReadiness.cs',
        'Domain\Map\ScenarioMapProjectionFieldCatalog.cs',
        'Application\Authoring\ScenarioStationUpgradePropertyService.cs',
        'Infrastructure\Unity\ScenarioEditorWorldReady.cs',
        'Infrastructure\Unity\ScenarioEditorGridSnapService.cs',
        'Domain\ScenarioAuthoringDefaults.cs',
        'Domain\Map\ScenarioMapAuthoringPolicy.cs'
    )) {
        if (Test-Path -LiteralPath (Join-Path (Join-Path $RepoRoot 'ShelteredScenarioEditor') $removedPolicy) -PathType Leaf) {
            Add-Failure "Editor still owns duplicated ShelteredAPI policy '$removedPolicy'."
        }
    }

    $apiOwnedScenarioPolicies = @{
        'Scenarios\Definitions\ScenarioMetadataDefaults.cs' = 'ScenarioMetadataDefaults.cs'
        'Scenarios\Domain\Map\ScenarioMapIconCatalog.cs' = 'ScenarioMapIconCatalog.cs'
        'Scenarios\Domain\Map\ScenarioMapTerrainModes.cs' = 'ScenarioMapTerrainModes.cs'
        'Scenarios\Domain\People\ScenarioFutureSurvivorActorReference.cs' = 'ScenarioFutureSurvivorActorReference.cs'
        'Scenarios\Infrastructure\Unity\ScenarioGridSnapService.cs' = 'ScenarioGridSnapService.cs'
    }
    foreach ($relativePolicyPath in $apiOwnedScenarioPolicies.Keys) {
        $fileName = $apiOwnedScenarioPolicies[$relativePolicyPath]
        if (-not (Test-Path -LiteralPath (Join-Path (Join-Path $RepoRoot 'ShelteredAPI') $relativePolicyPath) -PathType Leaf)) {
            Add-Failure "ShelteredAPI is missing canonical scenario policy '$relativePolicyPath'."
        }
        if (-not $apiProjectSource.Contains($relativePolicyPath)) {
            Add-Failure "ShelteredAPI.csproj does not compile canonical scenario policy '$relativePolicyPath'."
        }
        if ($editorProjectSource.Contains($fileName)) {
            Add-Failure "ShelteredScenarioEditor must consume '$fileName' through ShelteredAPI rather than compiling a second copy."
        }
        if (Test-Path -LiteralPath (Join-Path $RepoRoot ('Shared\Scenarios\' + $fileName)) -PathType Leaf) {
            Add-Failure "Scenario policy '$fileName' must have one ShelteredAPI owner, not a Shared dual-compilation path."
        }
    }

    $editorSourceText = ($editorSources | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
    $editorSpriteResolver = Get-Content -LiteralPath (Join-Path $RepoRoot 'ShelteredScenarioEditor\Infrastructure\Assets\ScenarioEditorSpriteRuntimeResolver.cs') -Raw
    if ($editorSpriteResolver -match 'ExternalRootCache|FindTransformByPath|CreateResolvedTarget') {
        Add-Failure 'The editor sprite adapter must not reimplement or cache ShelteredAPI runtime-target resolution.'
    }
    if ($editorSpriteResolver -notmatch 'ShelteredScenarioRuntime\.TryResolveRuntimeSpriteTarget') {
        Add-Failure 'The editor sprite adapter must consume the canonical coarse runtime-target facade.'
    }

    $apiAppearanceSource = Get-Content -LiteralPath (Join-Path $RepoRoot 'ShelteredAPI\Scenarios\Infrastructure\Assets\ScenarioCharacterAppearanceService.cs') -Raw
    foreach ($editorOnlyAppearanceMethod in @(
        'CapturePreview', 'RestorePreview', 'TryCreateEditableTexture', 'ApplyPreviewTexture',
        'BuildColorLabel', 'CaptureAppearance', 'CycleTextureId', 'RandomTextureId',
        'CycleColorHex', 'RandomColorHex'
    )) {
        if ($apiAppearanceSource -match ('\b' + [Text.RegularExpressions.Regex]::Escape($editorOnlyAppearanceMethod) + '\s*\(')) {
            Add-Failure "ShelteredAPI still owns editor-only appearance method '$editorOnlyAppearanceMethod'."
        }
    }

    foreach ($requiredFacadeCall in @(
        'ShelteredScenarioAuthoring.IndexDefinition',
        'ShelteredScenarioAuthoring.AnalyzeStoryFlow',
        'ShelteredScenarioAuthoring.CanStartPlay',
        'ShelteredScenarioAuthoring.GetMapEncounterProjectionFields',
        'ShelteredScenarioAuthoring.BumpVersion',
        'ShelteredScenarioAuthoring.GetKnownMapIconIds',
        'ShelteredScenarioAuthoring.IsKnownMapIconId',
        'ShelteredScenarioAuthoring.ResolveFutureSurvivorActorReference',
        'ShelteredScenarioRuntime.IsWorldReady',
        'ShelteredScenarioRuntime.ResolveConfiguredAppearanceColors',
        'ShelteredScenarioRuntime.TryResolveRuntimeSpriteTarget',
        '_previewSession.GetStationUpgradeSnapshot'
    )) {
        if ($editorSourceText -notmatch [Text.RegularExpressions.Regex]::Escape($requiredFacadeCall)) {
            Add-Failure "Editor does not consume canonical policy through '$requiredFacadeCall'."
        }
    }

    $releaseSource = Get-Content -LiteralPath (Join-Path $RepoRoot 'tools\New-ReleasePackages.ps1') -Raw
    $whitelistMatch = [Text.RegularExpressions.Regex]::Match(
        $releaseSource,
        '(?s)\$smmWhitelist\s*=\s*@\((?<Body>.*?)\)\s*\$contractRequired')
    $requiredMatch = [Text.RegularExpressions.Regex]::Match(
        $releaseSource,
        '(?s)\$contractRequired\s*=\s*@\((?<Body>.*?)\)\s*# The scenario editor')
    if (-not $whitelistMatch.Success -or $whitelistMatch.Groups['Body'].Value -notmatch 'bin\\ShelteredScenarioEditor\.dll') {
        Add-Failure 'The complete release-package whitelist must include ShelteredScenarioEditor.dll.'
    }
    if (-not $requiredMatch.Success -or $requiredMatch.Groups['Body'].Value -match 'ShelteredScenarioEditor') {
        Add-Failure 'ShelteredScenarioEditor.dll must not become a required base/update package contract file.'
    }
    if ($releaseSource -notmatch '\$optionalPackageFiles\s*=\s*@\(''bin\\ShelteredScenarioEditor\.dll''\)') {
        Add-Failure 'Release packaging must explicitly identify ShelteredScenarioEditor.dll as an optional runtime component.'
    }
    $basePackageContract = Get-Content -LiteralPath (Join-Path $RepoRoot 'Shared\ManagerPackageContract.cs') -Raw
    if ($basePackageContract -match 'ShelteredScenarioEditor') {
        Add-Failure 'ManagerPackageContract must allow ShelteredAPI-only installs without ShelteredScenarioEditor.dll.'
    }

    $deploymentSource = Get-Content -LiteralPath (Join-Path $RepoRoot 'tools\performance\Deploy-ShelteredBenchmarkBuild.ps1') -Raw
    if ($deploymentSource -notmatch 'ShelteredScenarioEditor\\obj\\\$Configuration\\ShelteredScenarioEditor\.dll' -or
        $deploymentSource -notmatch 'Copy-VerifiedArtifact \$scenarioEditorOutput .*bin\\ShelteredScenarioEditor\.dll') {
        Add-Failure 'Benchmark deployment must copy and hash-verify the editor DLL independently of ShelteredAPI.'
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Host "FAIL $failure" }
    exit 1
}

Write-Host 'Scenario editor assembly-boundary contracts passed.'
