[CmdletBinding()]
param(
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

if ([string]::IsNullOrEmpty($RepoRoot)) {
    $scriptPath = $MyInvocation.MyCommand.Path
    $RepoRoot = Split-Path -Parent (Split-Path -Parent $scriptPath)
}

$sourceRoot = Join-Path $RepoRoot 'Shared\ContentPacks'
$sources = @(
    (Join-Path $sourceRoot 'ContentPackSchema.cs'),
    (Join-Path $sourceRoot 'ContentPackSerialization.cs'),
    (Join-Path $sourceRoot 'ContentPackValidation.cs'),
    (Join-Path $sourceRoot 'ContentPackPathPolicy.cs'),
    (Join-Path $sourceRoot 'ContentPackValidator.cs')
)

foreach ($source in $sources) {
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required content-pack contract source was not found: $source"
    }
}

Add-Type -Path $sources -ReferencedAssemblies 'System.Web.Extensions'

$failures = New-Object System.Collections.Generic.List[string]

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        $failures.Add($Message)
    }
}

function Has-Issue($Validation, [string]$Code) {
    foreach ($issue in $Validation.Issues) {
        if ($issue.Code -eq $Code) {
            return $true
        }
    }
    return $false
}

function New-ValidDocument {
    $document = New-Object ShelteredModManager.ContentPacks.ContentPackDocument
    $document.modId = 'com.example.fieldkit'

    $item = New-Object ShelteredModManager.ContentPacks.ContentPackItem
    $item.id = 'com.example.fieldkit.item.ration'
    $item.displayName = 'Field Ration'
    $item.description = 'A compact emergency meal.'
    $item.iconPath = 'Assets/Icons/ration.png'
    $item.category = 'Food'
    [void]$document.items.Add($item)

    $recipe = New-Object ShelteredModManager.ContentPacks.ContentPackRecipe
    $recipe.id = 'com.example.fieldkit.recipe.ration'
    $recipe.resultItemId = $item.id
    $ingredient = New-Object ShelteredModManager.ContentPacks.ContentPackIngredient
    $ingredient.itemId = 'Plastic'
    $ingredient.count = 1
    [void]$recipe.ingredients.Add($ingredient)
    [void]$document.recipes.Add($recipe)

    return $document
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('SMMContentPackContracts_' + [Guid]::NewGuid().ToString('N'))
try {
    $iconRoot = Join-Path $tempRoot 'Assets\Icons'
    [void][IO.Directory]::CreateDirectory($iconRoot)

    # The path policy only needs the PNG signature and IHDR dimensions. Pixel decoding
    # remains the responsibility of the runtime/manager asset adapter.
    [byte[]]$pngHeader = @(
        137, 80, 78, 71, 13, 10, 26, 10,
        0, 0, 0, 13, 73, 72, 68, 82,
        0, 0, 0, 16, 0, 0, 0, 16
    )
    [IO.File]::WriteAllBytes((Join-Path $iconRoot 'ration.png'), $pngHeader)

    $context = New-Object ShelteredModManager.ContentPacks.ContentPackValidationContext
    $context.ExpectedModId = 'com.example.fieldkit'
    $context.ModRootPath = $tempRoot
    $context.ValidateAssetFiles = $true

    $valid = New-ValidDocument
    $validResult = [ShelteredModManager.ContentPacks.ContentPackValidator]::Validate($valid, $context)
    Assert-True $validResult.IsValid 'A valid content pack must pass validation.'
    Assert-True ($validResult.WarningCount -eq 1) 'A vanilla/external ingredient must produce one warning.'
    Assert-True (Has-Issue $validResult 'reference.external') 'External item references must be identified.'

    $serialized = [ShelteredModManager.ContentPacks.ContentPackJsonSerializer]::Serialize($valid)
    Assert-True $serialized.Success 'A valid content pack must serialize.'
    $roundTrip = [ShelteredModManager.ContentPacks.ContentPackJsonSerializer]::Deserialize($serialized.Json)
    Assert-True $roundTrip.Success 'Serialized content-pack JSON must deserialize.'
    Assert-True ($roundTrip.Document.items.Count -eq 1 -and $roundTrip.Document.recipes.Count -eq 1) 'Round-trip must preserve items and recipes.'

    $badSchema = New-ValidDocument
    $badSchema.schemaVersion = 99
    $badSchemaResult = [ShelteredModManager.ContentPacks.ContentPackValidator]::Validate($badSchema, $context)
    Assert-True (-not $badSchemaResult.IsValid -and (Has-Issue $badSchemaResult 'schema.unsupported')) 'Unknown schema versions must fail closed.'

    $mismatch = New-ValidDocument
    $mismatch.modId = 'com.example.other'
    $mismatchResult = [ShelteredModManager.ContentPacks.ContentPackValidator]::Validate($mismatch, $context)
    Assert-True (Has-Issue $mismatchResult 'mod_id.mismatch') 'About/content mod ID mismatch must fail.'

    $duplicate = New-ValidDocument
    $copy = New-Object ShelteredModManager.ContentPacks.ContentPackItem
    $copy.id = $duplicate.items[0].id
    $copy.displayName = 'Duplicate'
    [void]$duplicate.items.Add($copy)
    $duplicateResult = [ShelteredModManager.ContentPacks.ContentPackValidator]::Validate($duplicate, $context)
    Assert-True (Has-Issue $duplicateResult 'item.id.duplicate') 'Duplicate item IDs must fail.'

    $badValues = New-ValidDocument
    $badValues.items[0].category = 'Mystery'
    $badValues.items[0].stackSize = 0
    $badValues.items[0].ration.contamination = 2.0
    $badValues.recipes[0].station = 'Stove'
    $badValues.recipes[0].level = 6
    $badValues.recipes[0].ingredients[0].count = 0
    $badValuesResult = [ShelteredModManager.ContentPacks.ContentPackValidator]::Validate($badValues, $context)
    Assert-True (Has-Issue $badValuesResult 'item.category.invalid') 'Unknown item categories must fail.'
    Assert-True (Has-Issue $badValuesResult 'item.stack_size') 'Invalid stack bounds must fail.'
    Assert-True (Has-Issue $badValuesResult 'item.ration.contamination') 'Invalid contamination bounds must fail.'
    Assert-True (Has-Issue $badValuesResult 'recipe.station.invalid') 'Unknown stations must fail.'
    Assert-True (Has-Issue $badValuesResult 'recipe.level') 'Invalid recipe levels must fail.'
    Assert-True (Has-Issue $badValuesResult 'ingredient.count') 'Non-positive ingredient counts must fail.'

    $missingLocal = New-ValidDocument
    $missingLocal.recipes[0].resultItemId = 'com.example.fieldkit.item.missing'
    $missingLocalResult = [ShelteredModManager.ContentPacks.ContentPackValidator]::Validate($missingLocal, $context)
    Assert-True (Has-Issue $missingLocalResult 'reference.local_missing') 'Missing local references must be errors.'

    $traversal = [ShelteredModManager.ContentPacks.ContentPackPathPolicy]::ValidateIcon(
        $tempRoot,
        'Assets/../secret.png',
        $false,
        0,
        0)
    Assert-True (-not $traversal.Success) 'Traversal asset paths must be rejected.'

    $rooted = [ShelteredModManager.ContentPacks.ContentPackPathPolicy]::ValidateIcon(
        $tempRoot,
        'C:\temp\secret.png',
        $false,
        0,
        0)
    Assert-True (-not $rooted.Success) 'Rooted asset paths must be rejected.'

    $wrongFolder = [ShelteredModManager.ContentPacks.ContentPackPathPolicy]::ValidateIcon(
        $tempRoot,
        'Icons/ration.png',
        $false,
        0,
        0)
    Assert-True (-not $wrongFolder.Success) 'Assets outside Assets/ must be rejected.'

    $oversizedDimension = [ShelteredModManager.ContentPacks.ContentPackPathPolicy]::ValidateIcon(
        $tempRoot,
        'Assets/Icons/ration.png',
        $true,
        1024,
        8)
    Assert-True (-not $oversizedDimension.Success) 'PNG dimensions above the configured limit must fail.'

    $invalidJson = [ShelteredModManager.ContentPacks.ContentPackJsonSerializer]::Deserialize('{')
    Assert-True (-not $invalidJson.Success) 'Malformed JSON must return a failure result.'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'CONTENT PACK CONTRACTS PASS'
