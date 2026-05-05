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
$scenarioSmoke = Read-RepoFile "ShelteredAPI\Scenarios\Diagnostics\ScenarioPipelineSmokeTest.cs"
$actorContracts = Read-RepoFile "ModAPI\Actors\Abstractions\IActorSystem.cs"
$actorImpl = Read-RepoFile "ShelteredAPI\Actors\Internal\ActorSystemImpl.cs"
$actorModels = Read-RepoFile "ModAPI\Actors\Models\ActorModels.cs"
$saveRuntime = Read-RepoFile "ModAPI\Core\ISaveRuntimeAdapter.cs"
$saveAdapter = Read-RepoFile "ShelteredAPI\Core\ShelteredSaveRuntimeAdapter.cs"
$bootstrap = Read-RepoFile "ShelteredAPI\Core\ShelteredApiRuntimeBootstrap.cs"
$apiIds = Read-RepoFile "ModAPI\Core\IGameHelper.cs"
$scenarioSaves = Read-RepoFile "ShelteredAPI\Saves\ScenarioSaves.cs"
$scenarioSaveGuards = Read-RepoFile "ShelteredAPI\Saves\ScenarioSaveIdGuards.cs"
$scenarioSerializer = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Serialization\ScenarioDefinitionSerializer.cs"
$scenarioVerification = Read-RepoFile "ShelteredAPI\Scenarios\Diagnostics\ScenarioFrameworkVerification.cs"
$runtimeOrchestrator = Read-RepoFile "ShelteredAPI\Scenarios\Application\Runtime\ScenarioRuntimeOrchestrator.cs"
$runtimeContracts = Read-RepoFile "ShelteredAPI\Scenarios\Application\Runtime\ScenarioRuntimeContracts.cs"
$catalogRefreshCoordinator = Read-RepoFile "ShelteredAPI\Scenarios\Definitions\ScenarioDefinitionCatalogRefreshCoordinator.cs"
$scenarioDefinitionModule = Read-RepoFile "ShelteredAPI\Scenarios\Composition\ScenarioDefinitionModule.cs"
$deferredPatchCoordinator = Read-RepoFile "ModAPI\Harmony\DeferredPatchCoordinator.cs"
$unityLogFilter = Read-RepoFile "ModAPI\Core\UnityLogFilter.cs"
$pluginRunner = Read-RepoFile "ModAPI\Core\PluginRunner.cs"
$shelteredLogNormalizers = Read-RepoFile "ShelteredAPI\Core\ShelteredUnityLogNormalizers.cs"
$nexusInstallService = Read-RepoFile "Manager\Core\Services\NexusInstallService.cs"
$nexusModsTab = Read-RepoFile "Manager\Views\NexusModsTab.cs"
$pluginManager = Read-RepoFile "ModAPI\Core\PluginManager.cs"
$runtimeApiCompatibility = Read-RepoFile "ModAPI\Core\RuntimeApiCompatibility.cs"
$modAbout = Read-RepoFile "ModAPI\Core\ModAbout.cs"
$shelteredSaves = Read-RepoFile "ShelteredAPI\Saves\ShelteredSaves.cs"

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

Assert-Contains "scenario save guard" $scenarioSaveGuards 'StandardStorageScenarioId\s*=\s*"Standard"' "reserved Standard save storage id must be centralized."
Assert-Contains "scenario save guard" $scenarioSaveGuards 'VanillaSurroundedScenarioId\s*=\s*"Vanilla\.Surrounded"' "reserved Surrounded scenario id must be centralized."
Assert-Contains "scenario save guard" $scenarioSaveGuards 'VanillaStasisScenarioId\s*=\s*"Vanilla\.Stasis"' "reserved Stasis scenario id must be centralized."
Assert-Contains "scenario save guard" $scenarioSaveGuards 'ScenarioAuthoringDraftStorageScenarioId\s*=\s*"ScenarioAuthoringDrafts"' "draft scenario storage id must be guarded."
Assert-Contains "scenario save guard" $scenarioSaves 'RequireCustomScenarioId\(scenarioId,\s*"ScenarioSaves\.Delete"' "DeleteScenario must reject reserved custom-scenario ids before resolving paths."
Assert-Contains "scenario save guard" $scenarioSaves 'RequireCustomScenarioId\(scenarioId,\s*"ScenarioSaves\.Overwrite"' "OverwriteScenario must reject reserved custom-scenario ids before resolving paths."
Assert-Contains "scenario save guard" $scenarioSaves 'RequireCustomScenarioId\(scenarioId,\s*"ScenarioSaves\.Get"' "GetScenario must share the centralized custom-scenario id guard."
Assert-Contains "standard save facade" $shelteredSaves 'public static SaveEntry\[\]\s+ListStandard\(\)' "ShelteredSaves must expose an easy built-in standard save list API."
Assert-Contains "standard save facade" $shelteredSaves 'public static SaveEntry\s+GetStandard\(string saveId\)' "ShelteredSaves must expose an easy built-in standard save get API."
Assert-Contains "standard save facade" $shelteredSaves 'public static SaveEntry\s+OverwriteStandard\(string saveId,\s*SaveOverwriteOptions options,\s*byte\[\] xmlBytes\)' "ShelteredSaves must expose an easy built-in standard save overwrite API."
Assert-Contains "standard save facade" $shelteredSaves 'public static bool\s+DeleteStandard\(string saveId\)' "ShelteredSaves must expose an easy built-in standard save delete API."
Assert-Contains "scenario save guard" $scenarioVerification 'VerifyScenarioSaveIdGuards' "scenario verification harness must exercise reserved and valid custom scenario save ids."
Assert-Contains "scenario save guard" $scenarioVerification 'DeleteScenario\(ScenarioSaveIdGuards\.StandardStorageScenarioId' "verification harness must prove DeleteScenario rejects Standard before path routing."
Assert-Contains "scenario save guard" $scenarioVerification 'OverwriteScenario\(ScenarioSaveIdGuards\.StandardStorageScenarioId' "verification harness must prove OverwriteScenario rejects Standard before path routing."
Assert-Contains "scenario save guard" $scenarioVerification 'com\.example\.scenario\.valid' "verification harness must prove a valid custom scenario id remains accepted."

Assert-Contains "scenario XML atomic save" $scenarioSerializer "BuildTempPath\(filePath\)" "scenario.xml writes must go through a same-directory temp path."
Assert-Contains "scenario XML atomic save" $scenarioSerializer "Load\(tempPath\)" "scenario.xml temp writes must be parsed before replacing the live file."
Assert-Contains "scenario XML atomic save" $scenarioSerializer "File\.Replace\(tempPath,\s*filePath,\s*backupPath,\s*false\)" "existing scenario.xml must be replaced with File.Replace and a backup."
Assert-Contains "scenario XML atomic save" $scenarioSerializer 'filePath\s*\+\s*"\.bak"' "scenario.xml replacement must retain a .bak recovery file."
Assert-Contains "scenario XML atomic verification" $scenarioVerification "VerifyAtomicScenarioWrites" "scenario verification harness must cover atomic scenario.xml replacement."
Assert-Contains "scenario XML atomic verification" $scenarioVerification "FileShare\.None" "scenario verification must simulate a failed replace and assert the original remains intact."

Assert-Contains "scenario runtime retry" $runtimeContracts "int CatalogRevision" "definition catalog service must expose a revision for same-session retry gating."
Assert-Contains "scenario runtime retry" $runtimeOrchestrator "MarkApplyBlocked" "failed definition resolution must be tracked as blocked instead of applied."
Assert-Contains "scenario runtime retry" $runtimeOrchestrator "CatalogRevision" "blocked scenario bindings must be reconsidered after catalog refresh."
Assert-Contains "scenario runtime retry" $runtimeOrchestrator "MissingDefinition" "missing scenario definitions must be a retryable blocked state."
Assert-Contains "scenario runtime retry" $catalogRefreshCoordinator "UpdateActiveScenarioApply" "definition catalog refresh must actively ask the runtime orchestrator to retry the active binding in the same session."
Assert-Contains "scenario runtime retry" $scenarioDefinitionModule "ScenarioDefinitionCatalogRefreshCoordinator" "the catalog refresh coordinator must wrap the registered definition catalog service."
Assert-Contains "scenario runtime retry" $scenarioVerification "VerifyMissingDefinitionRefreshRetry" "scenario verification harness must cover missing definition, blocked apply, restored definition, catalog refresh, then success."
Assert-Contains "scenario runtime retry" $scenarioVerification "Catalog refresh did not cause the blocked active binding to retry and apply" "scenario verification harness must assert refresh-driven retry success."

Assert-Contains "deferred patch retry" $deferredPatchCoordinator "ApplyingGroups" "deferred patch coordinator must prevent recursive same-trigger retries."
Assert-Contains "deferred patch retry" $deferredPatchCoordinator "LastFailures" "deferred patch coordinator must record failed groups for diagnostics."
Assert-Contains "deferred patch retry" $deferredPatchCoordinator "AppliedGroups\.Add\(groupKey\)" "deferred patch groups must only be marked applied after ApplyAssembly succeeds."
Assert-Contains "deferred patch retry" $deferredPatchCoordinator "remains retryable on a later trigger" "failed deferred patch groups must log that they remain retryable."

Assert-Contains "Unity log filtering" $unityLogFilter "type == LogType\.Exception \|\| type == LogType\.Error \|\| type == LogType\.Assert" "Unity log filter must never suppress errors, asserts, or exceptions."
Assert-Contains "Unity log filtering" $unityLogFilter "DisableUnityLogSuppression" "Unity log suppression must have a manager/debug override."
Assert-Contains "Unity log filtering" $unityLogFilter "SuppressedCounts" "Unity log suppression must count suppressed messages by category."
Assert-Contains "Unity log filtering" $pluginRunner "UnityLogFilter\.LogSuppressionSummary" "PluginRunner must emit a Unity log suppression summary."
Assert-Contains "Unity log filtering" $shelteredLogNormalizers "type == LogType\.Exception \|\| type == LogType\.Error \|\| type == LogType\.Assert" "Sheltered suppression normalizers must be severity-aware."

Assert-Contains "Nexus verification" $nexusInstallService "VerifyInstalledMod" "Nexus install flow must verify the copied install before reporting success."
Assert-Contains "Nexus verification" $nexusInstallService "RestoreBackupAfterFailedInstall\(targetPath,\s*backupPath" "Nexus verification failure must roll back the failed install."
Assert-Contains "Nexus verification" $nexusInstallService "Removed failed install" "Nexus verification failure without a backup must report that it removed the failed install."
Assert-Contains "Nexus verification" $nexusInstallService "VerifyCopiedFiles" "Nexus install verification must compare extracted files to installed files."
Assert-Contains "Nexus verification" $nexusModsTab "Install verified:" "Nexus UI must surface install verification details."

Assert-Contains "runtime API compatibility gate" $modAbout "requiredModApiVersion" "runtime About.json metadata must include ModAPI compatibility fields."
Assert-Contains "runtime API compatibility gate" $modAbout "requiredShelteredApiVersion" "runtime About.json metadata must include ShelteredAPI compatibility fields."
Assert-Contains "runtime API compatibility gate" $pluginManager "IsRuntimeApiCompatible" "PluginManager must gate incompatible mods before activation."
Assert-Contains "runtime API compatibility gate" $pluginManager "before assembly load" "incompatible runtime metadata must block load before type activation."
Assert-Contains "runtime API compatibility gate" $runtimeApiCompatibility "IsVersionAtLeast\(string current,\s*string required\).*return false" "version comparison must fail closed when current or required versions are missing or malformed."
Assert-Contains "runtime API compatibility gate" $runtimeApiCompatibility "declared requirement is malformed" "malformed declared API requirements must block loading."
Assert-Contains "runtime API compatibility gate" $runtimeApiCompatibility "runtime version is.*unavailable" "missing or unreadable runtime API versions must block declared requirements."
Assert-Contains "runtime API compatibility gate" $runtimeApiCompatibility "runtime version '.*malformed" "malformed runtime API versions must block declared requirements."
Assert-Contains "runtime API compatibility gate" $runtimeApiCompatibility "currentVersion\.CompareTo\(requiredVersion\) < 0" "too-old runtime API versions must block declared requirements."
Assert-Contains "runtime API compatibility gate" $pluginManager "FindLoadedRuntimeAssembly\(apiName\)" "ShelteredAPI requirements must fail when ShelteredAPI is not already loaded."

if ($failures.Count -gt 0) {
    Write-Host ("ShelteredAPI contract tests failed: " + $failures.Count)
    foreach ($failure in $failures) {
        Write-Host ("FAIL`t" + $failure)
    }
    exit 1
}

Write-Host "ShelteredAPI contract tests passed."
