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

$failures = New-Object "System.Collections.Generic.List[string]"

function Read-RepoFile {
    param([string]$RelativePath)
    return Get-Content -LiteralPath (Join-Path $RepoRoot $RelativePath) -Raw
}

function Assert-Contains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if (-not [System.Text.RegularExpressions.Regex]::IsMatch($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $failures.Add("${Name}: ${Message}")
    }
}

function Assert-NotContains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ([System.Text.RegularExpressions.Regex]::IsMatch($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $failures.Add("${Name}: ${Message}")
    }
}

$contentRegistry = Read-RepoFile "ShelteredAPI\Content\ContentRegistry.cs"
$scenarioSmoke = Read-RepoFile "ShelteredAPI\Scenarios\ScenarioPipelineSmokeTest.cs"
$actorContracts = Read-RepoFile "ModAPI\Actors\Abstractions\IActorSystem.cs"
$actorImpl = Read-RepoFile "ShelteredAPI\Actors\Internal\ActorSystemImpl.cs"
$actorModels = Read-RepoFile "ModAPI\Actors\Models\ActorModels.cs"
$saveRuntime = Read-RepoFile "ModAPI\Core\ISaveRuntimeAdapter.cs"
$saveAdapter = Read-RepoFile "ShelteredAPI\Core\ShelteredSaveRuntimeAdapter.cs"
$bootstrap = Read-RepoFile "ShelteredAPI\Core\ShelteredApiRuntimeBootstrap.cs"
$apiIds = Read-RepoFile "ModAPI\Core\IGameHelper.cs"

Assert-Contains "content ID stability" $contentRegistry "StableContentIdHash" "custom content IDs must use an explicit deterministic hash helper."
Assert-NotContains "content ID stability" $contentRegistry "seed\.GetHashCode\(" "custom content IDs must not use string.GetHashCode because it is not a stable public contract."
Assert-Contains "content ID stability" $contentRegistry "CustomItemTypeStart\s*=\s*10000" "custom item ID range start changed or is missing."
Assert-Contains "content ID stability" $contentRegistry "CustomItemTypeRange\s*=\s*900000" "custom item ID range width changed or is missing."

Assert-Contains "scenario XML round-trip" $scenarioSmoke "serializer\.ToXml\(definition\).*serializer\.FromXml\(xml\)" "smoke harness must serialize and deserialize the same scenario definition."
Assert-Contains "scenario XML round-trip" $scenarioSmoke "ScenarioDefinitionComparer\.AreEquivalent" "round-trip smoke test must compare the original and deserialized definitions."

Assert-Contains "actor component ownership" $actorContracts "Set\(ActorId actorId, IActorComponent component, string sourceModId\)" "component writes must include sourceModId ownership."
Assert-Contains "actor component ownership" $actorContracts "Remove\(ActorId actorId, string componentId, string sourceModId\)" "component removals must include sourceModId ownership."
Assert-Contains "actor component ownership" $actorImpl "IsOwnedComponentId\(componentId, sourceModId\)" "implementation must validate component ID ownership."
Assert-Contains "actor component ownership" $actorModels "OwnerModId" "serialized component entries must preserve owner mod IDs."

Assert-Contains "actor serialization migration" $actorContracts "int CurrentSchemaVersion" "serialization service must expose a schema version."
Assert-Contains "actor serialization migration" $actorImpl "envelope\.SchemaVersion\s*=\s*CurrentSchemaVersion" "exports must stamp the current schema version."
Assert-Contains "actor serialization migration" $actorImpl "ImportJson\(string json\)" "actor serialization must keep an import path for migration."
Assert-Contains "actor serialization migration" $actorImpl "serializer\.Deserialize\(entry\.PayloadJson.*entry\.Version\)" "component import must pass stored component versions to serializers."

Assert-Contains "save API behavior" $saveRuntime "NullSaveRuntimeAdapter" "save runtime must have a null adapter for unavailable runtime behavior."
Assert-Contains "saveRuntime behavior" $saveRuntime "GetCurrentSaveContext\(\).*return null" "null save runtime context must be explicit."
Assert-Contains "save API behavior" $saveAdapter "GetCurrentSaveContext\(\)" "Sheltered adapter must implement current save context resolution."
Assert-Contains "save API behavior" $saveAdapter "new ModSaveContext" "Sheltered adapter must return neutral ModSaveContext DTOs."

Assert-Contains "bootstrap registration diagnostics" $apiIds "GameRuntime\." "canonical GameRuntime IDs must remain defined in ModAPI."
Assert-Contains "bootstrap registration diagnostics" $bootstrap "RegisterApi\(GameRuntimeApiIds\." "bootstrap must register canonical GameRuntime IDs."
Assert-Contains "bootstrap registration diagnostics" $bootstrap "RegisterApi\(ShelteredApiAliasIds\." "bootstrap must register ShelteredAPI aliases for compatibility."
Assert-Contains "bootstrap registration diagnostics" $bootstrap "ShelteredContent\.Service" "bootstrap must register the facade-backed Sheltered content service."

if ($failures.Count -gt 0) {
    Write-Host ("ShelteredAPI contract tests failed: " + $failures.Count)
    foreach ($failure in $failures) {
        Write-Host ("FAIL`t" + $failure)
    }
    exit 1
}

Write-Host "ShelteredAPI contract tests passed."
