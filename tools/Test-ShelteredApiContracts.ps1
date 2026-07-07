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
$saveSystem = Read-RepoFile "ModAPI\Persistence\SaveSystemImpl.cs"
$persistenceLifecycle = Read-RepoFile "ModAPI\Persistence\IModPersistenceLogic.cs"
$modRandom = Read-RepoFile "ModAPI\Core\ModRandom.cs"
$modRandomState = Read-RepoFile "ModAPI\Core\ModRandomState.cs"
$modManagerBase = Read-RepoFile "ModAPI\Core\ModManagerBase.cs"
$saveAdapter = Read-RepoFile "ShelteredAPI\Core\ShelteredSaveRuntimeAdapter.cs"
$saveProtection = Read-RepoFile "ShelteredAPI\Core\SaveProtection.cs"
$saveModels = Read-RepoFile "ShelteredAPI\Saves\Models.cs"
$saveMetadataReader = Read-RepoFile "ShelteredAPI\Saves\SaveInfoXmlMetadataReader.cs"
$saveRegistry = Read-RepoFile "ShelteredAPI\Saves\SaveRegistryCore.cs"
$saveManifestFacts = Read-RepoFile "ShelteredAPI\Saves\SaveManifestFacts.cs"
$saveRuntimeState = Read-RepoFile "ShelteredAPI\Saves\Runtime\SaveRuntimeState.cs"
$platformSaveOperationService = Read-RepoFile "ShelteredAPI\Saves\Runtime\PlatformSaveOperationService.cs"
$slotSelectionPatchCoordinator = Read-RepoFile "ShelteredAPI\Saves\Paging\SlotSelectionPatchCoordinator.cs"
$supportBundle = Read-RepoFile "ShelteredAPI\Debugging\ShelteredSupportBundle.cs"
$vanillaSaveRouting = Read-RepoFile "ShelteredAPI\Saves\Runtime\VanillaSaveRouting.cs"
$bootstrap = Read-RepoFile "ShelteredAPI\Core\ShelteredApiRuntimeBootstrap.cs"
$apiIds = Read-RepoFile "ModAPI\Core\IGameHelper.cs"
$scenarioSaves = Read-RepoFile "ShelteredAPI\Saves\ScenarioSaves.cs"
$scenarioSaveGuards = Read-RepoFile "ShelteredAPI\Saves\ScenarioSaveIdGuards.cs"
$scenarioSaveLibrary = Read-RepoFile "ShelteredAPI\Scenarios\Application\Selection\ScenarioSaveLibrary.cs"
$scenarioSerializer = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Serialization\ScenarioDefinitionSerializer.cs"
$scenarioDefinitionModel = Read-RepoFile "ShelteredAPI\Scenarios\Definitions\ScenarioDefinition.cs"
$familyScenarioSerializer = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Serialization\FamilyScenarioSectionSerializer.cs"
$scenarioActorXmlSerializer = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Serialization\ScenarioActorXmlSerializer.cs"
$scenarioConditionRefModel = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Conditions\ScenarioConditionRef.cs"
$scenarioEffectDefinitionModel = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Effects\ScenarioEffectDefinition.cs"
$scenarioActorResolver = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScenarioActorResolver.cs"
$scenarioApplyCoordinator = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScenarioApplyCoordinator.cs"
$scenarioCharacterEditorAuthoringService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioCharacterEditorAuthoringService.cs"
$scenarioAuthoringCaptureService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthoringCaptureService.cs"
$scenarioGameplayScheduleAuthoringService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioGameplayScheduleAuthoringService.cs"
$scenarioEditorController = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioEditorController.cs"
$familyApplyService = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\FamilyApplyService.cs"
$scheduledSurvivorRuntimeService = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScheduledSurvivorRuntimeService.cs"
$scenarioVerification = Read-RepoFile "ShelteredAPI\Scenarios\Diagnostics\ScenarioFrameworkVerification.cs"
$scenarioPlayStartReadiness = Read-RepoFile "ShelteredAPI\Scenarios\Application\Runtime\ScenarioPlayStartReadiness.cs"
$scenarioAuthoringPresentationBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioAuthoringPresentationBuilder.cs"
$scenarioOverviewAuthoringContentBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioOverviewAuthoringContentBuilder.cs"
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
$runtimeUiContracts = Read-RepoFile "ShelteredAPI\UI\Runtime\RuntimeUiContracts.cs"
$runtimeObjectPanels = Read-RepoFile "ShelteredAPI\UI\Internal\Runtime\RuntimeObjectPanelRegistry.cs"
$uiFacade = Read-RepoFile "ShelteredAPI\UI\ShelteredUI.cs"
$uiExtensionContracts = Read-RepoFile "ShelteredAPI\UI\UIExtensionContracts.cs"
$uiExtensionService = Read-RepoFile "ShelteredAPI\UI\Internal\UIExtensionService.cs"
$uiTakeover = Read-RepoFile "ShelteredAPI\UI\UITakeover.cs"
$shelteredStores = Read-RepoFile "ShelteredAPI\Storage\ShelteredStores.cs"
$characterItemContracts = Read-RepoFile "ShelteredAPI\Storage\CharacterItemAssignmentContracts.cs"
$characterItemAssignments = Read-RepoFile "ShelteredAPI\Storage\CharacterItemAssignments.cs"
$shelteredCharacterItems = Read-RepoFile "ShelteredAPI\Storage\ShelteredCharacterItems.cs"
$shelteredCooking = Read-RepoFile "ShelteredAPI\Workstations\ShelteredCooking.cs"
$cookingContracts = Read-RepoFile "ShelteredAPI\Workstations\CookingWorkstationContracts.cs"
$runtimeTimedWorkJob = Read-RepoFile "ShelteredAPI\Workstations\RuntimeTimedWorkJob.cs"
$playerQueueContracts = Read-RepoFile "ShelteredAPI\Queues\PlayerQueueContracts.cs"
$shelteredQueues = Read-RepoFile "ShelteredAPI\Queues\ShelteredQueues.cs"
$playerQueueRuntime = Read-RepoFile "ShelteredAPI\Queues\Internal\PlayerQueueRuntime.cs"
$playerQueuePatches = Read-RepoFile "ShelteredAPI\Queues\Internal\PlayerQueuePatches.cs"

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
Assert-Contains "persistence lifecycle behavior" $persistenceLifecycle "interface IModPersistenceLifecycle" "neutral persistence must expose the opt-in complete lifecycle contract."
Assert-Contains "persistence lifecycle behavior" $persistenceLifecycle "PrepareForSave\(IModSaveContext context\).*RestoreAfterLoad\(IModSaveContext context\).*ValidateAfterLoad\(IModSaveContext context,\s*out string diagnosticMessage\)" "complete lifecycle contract must retain prepare, restore, and validation hooks."
Assert-Contains "persistence lifecycle behavior" $saveSystem "state\.Loaded \|\| state\.Migrated \|\| state\.Defaulted" "restore and validation must cover loaded, migrated, and registered-default data."
Assert-Contains "persistence lifecycle behavior" $saveSystem "_preparedLoadKey.*BuildLoadKey\(saveContext, rootPath\).*_afterLoadCallbacksApplied" "load callbacks must remain scoped and suppressed by active save context."
Assert-Contains "persistence lifecycle behavior" $saveSystem "status=skipped-no-active-save-context" "persistence diagnostics must report skipped operations when no save context is active."
Assert-Contains "persistence lifecycle behavior" $saveSystem 'statuses\.Add\("loaded"\).*statuses\.Add\("missing"\).*statuses\.Add\("migrated"\).*statuses\.Add\("defaulted"\).*statuses\.Add\("failed-deserialize"\).*statuses\.Add\("validation-passed"\).*statuses\.Add\("validation-failed"\).*statuses\.Add\("callback-failure"\)' "load diagnostics must report stable per-key lifecycle states."
Assert-Contains "save API behavior" $saveAdapter "GetCurrentSaveContext\(\)" "Sheltered adapter must implement current save context resolution."
Assert-Contains "save API behavior" $saveAdapter "new ModSaveContext" "Sheltered adapter must return neutral ModSaveContext DTOs."
Assert-Contains "save API behavior" $saveAdapter "TryCreateVanillaSaveContext" "Sheltered adapter must route vanilla save types through explicit context mapping."
Assert-Contains "save API behavior" $vanillaSaveRouting "SaveManager\.SaveType\.Slot1.*StandardStorageScenarioId.*1.*Slot1" "standard vanilla slot 1 must continue to use Standard/Slot_1 context."
Assert-Contains "save API behavior" $vanillaSaveRouting "SaveManager\.SaveType\.SlotSurrounded.*VanillaSurroundedStorageScenarioId.*1.*VanillaSurroundedSaveId.*4" "Surrounded vanilla save must use Surrounded/Slot_1 sidecar context and vanilla file slot 4."
Assert-Contains "save API behavior" $vanillaSaveRouting "SaveManager\.SaveType\.SlotStasis.*VanillaStasisStorageScenarioId.*1.*VanillaStasisSaveId.*5" "Stasis vanilla save must use Stasis/Slot_1 sidecar context and vanilla file slot 5."
Assert-Contains "save API behavior" $saveAdapter "currentType == SaveManager\.SaveType\.Invalid \|\| currentType == SaveManager\.SaveType\.GlobalData\).*return null" "GlobalData and Invalid save contexts must remain null."
Assert-NotContains "save API behavior" $saveAdapter "SaveTypeToSlotIndex|\(int\)saveType" "vanilla context routing must not derive Surrounded/Stasis slots from SaveType numeric values."
Assert-Contains "save API behavior" $saveAdapter "VanillaSaveRouting\.TryGetRoute" "Sheltered adapter must use centralized vanilla save routing."
Assert-Contains "save protection behavior" $saveProtection "ResolveVanillaManifestContext" "save protection must explicitly map vanilla save types to manifest contexts."
Assert-Contains "save protection behavior" $saveProtection "VanillaSaveRouting\.TryGetRoute" "save protection must use centralized vanilla save routing."
Assert-Contains "save protection behavior" $saveProtection "SaveStorageRouter\.UpdateSlotManifest\(context\.ScenarioId,\s*context\.AbsoluteSlot" "save protection must write manifests through the resolved scenario context."
Assert-Contains "save manifest facts" $saveModels 'public string saveScopeId;.*public string saveId;.*public string customScenarioId;.*public string modApiVersion;.*public string shelteredApiVersion;.*public bool hasMapSize;.*public int mapSize;.*public string queueFactsStatus\s*=.*public string restoreFactsStatus\s*=' "slot manifests must retain concrete save, API, map, queue, and restore fact fields."
Assert-Contains "save manifest facts" $saveModels "public string requiredModApiVersion;.*public string requiredShelteredApiVersion;" "saved mod records must retain effective API compatibility declarations."
Assert-Contains "save manifest facts" $saveMetadataReader "target\.hasMapSizeMetadata\s*=\s*HasElement\(document,\s*""mapSize""\)" "map-size facts must be recorded only when map metadata exists in the saved XML."
Assert-Contains "save manifest facts" $saveRegistry "SaveManifestFacts\.ApplyStorageIdentityFacts\(manifest,\s*scenarioId,\s*absoluteSlot\)" "all persisted slot manifests must receive routed save identity and restore facts."
Assert-Contains "save manifest facts" $saveRegistry 'root\.Set\("modApiVersion".*root\.Set\("shelteredApiVersion".*root\.Set\("queueFactsStatus".*root\.Set\("restoreFactsStatus"' "new manifest fields must be serialized additively."
Assert-Contains "save manifest facts" $saveModels 'public string source;.*public int sourceSlot;.*public uint sourceVanillaCrc32;.*public string sourceVanillaLastWriteUtc;' "slot manifests must retain mirrored-vanilla source metadata fields."
Assert-Contains "save manifest facts" $saveRegistry 'root\.Set\("source".*root\.Set\("sourceSlot".*root\.Set\("sourceVanillaCrc32".*root\.Set\("sourceVanillaLastWriteUtc"' "mirrored-vanilla source metadata must be serialized additively."
Assert-Contains "vanilla mirror divergence" $saveRegistry "CompareStandardVanillaMirror.*MissingMirror.*InSync.*Diverged" "standard vanilla mirror comparison must distinguish missing mirrors, matching mirrors, and divergent mirrors."
Assert-Contains "vanilla mirror divergence" $saveRegistry "ByteArraysEqual\(result\.VanillaXmlBytes,\s*result\.MirrorXmlBytes\)" "vanilla mirror comparison must compare decrypted vanilla XML bytes against mirror XML bytes."
Assert-Contains "vanilla mirror divergence" $slotSelectionPatchCoordinator "VanillaMirrorConflictDialog\.Show" "divergent vanilla mirrors must prompt before loading either state."
Assert-Contains "vanilla mirror divergence" $slotSelectionPatchCoordinator "WriteStandardVanillaMirrorFromVanilla\(.*true.*load-vanilla-state" "choosing vanilla state must overwrite the XML mirror only after a backup path is requested."
Assert-Contains "vanilla mirror divergence" $slotSelectionPatchCoordinator "BackupVanillaBeforeOverwrite\(comparison\.SaveType\).*QueueVerifiedVanillaMirrorLoad" "choosing edited XML must back up the vanilla dat before queuing the mirrored load."
Assert-Contains "vanilla mirror save sync" $saveRuntimeState "SetActiveMirroredVanillaSession" "runtime state must record active mirrored vanilla sessions."
Assert-Contains "vanilla mirror save sync" $platformSaveOperationService "TryGetActiveMirroredVanillaSessionFor.*SaveActiveMirroredVanillaSession" "platform saves must route active mirrored vanilla sessions through the dual-write path."
Assert-Contains "vanilla mirror save sync" $platformSaveOperationService "SaveStorageRouter\.Overwrite\(scenarioId,\s*active\.id.*_inner\.PlatformSave\(type" "mirrored vanilla saves must write both the SMM XML mirror and the original platform save path."
Assert-Contains "save manifest facts" $saveManifestFacts "ScenarioSaveIdGuards\.IsReservedStorageId\(scopeId\)" "custom scenario identity must be derived from concrete non-reserved save scopes."
Assert-Contains "save manifest facts" $saveManifestFacts '"ShelteredAPI\.Map\.ShelteredMap".*"GetCurrentContext"' "runtime map facts must be safely probed when the optional map facade is present."
Assert-Contains "save manifest facts" $saveRegistry 'root\.Set\("runtimeMapFactsStatus".*root\.Set\("runtimeMapScaleFactor".*root\.Set\("mapSeed"' "runtime map size, scale, and seed facts must be persisted additively."

Assert-Contains "support bundle" $supportBundle "public static SupportBundleSnapshot Capture\(\)" "support bundle facade must expose structured snapshot capture."
Assert-Contains "support bundle" $supportBundle "public static string ExportJson\(SupportBundleRequest request\)" "support bundle facade must expose JSON export."
Assert-Contains "support bundle" $supportBundle "MMLog\.GetRecentEntries" "support bundle must gather recent runtime logs."
Assert-Contains "support bundle" $supportBundle "UnityLogFilter" "support bundle must safely probe Unity log suppression counts."
Assert-Contains "support bundle" $supportBundle '"ModAPI\.Core\.ModRandom".*"CurrentSeed".*"CurrentStep"' "support bundle must gather available public random seed facts without requiring optional members."
Assert-Contains "support bundle" $supportBundle '"GetLatestReport"' "support bundle must consume a public patch report snapshot when present."
Assert-Contains "support bundle" $supportBundle '"GetDiagnostics"' "support bundle must consume public background-work diagnostics when present."
Assert-Contains "support bundle" $supportBundle '"ShelteredAPI\.Queues\.ShelteredQueues"' "support bundle must report when owner-scoped queue snapshots are installed without inventing save-wide queue facts."
Assert-Contains "support bundle" $supportBundle 'No public patch report snapshot is available' "support bundle must explicitly degrade when patch reports are unavailable."
Assert-Contains "support bundle" $supportBundle 'No public background-work diagnostics snapshot is available' "support bundle must explicitly degrade when background diagnostics are unavailable."
Assert-NotContains "support bundle" $supportBundle "CapabilityRegistry|Capabilities" "support diagnostics must not introduce a broad capability registry."

Assert-Contains "deterministic random API" $modRandom "GetStream\(string modId,\s*string featureId\)" "ModRandom must expose canonical per-mod feature streams."
Assert-Contains "deterministic random API" $modRandom "ResetForSaveSeed\(int seed,\s*RandomnessMode mode" "ModRandom must expose explicit save-seed reset semantics."
Assert-Contains "deterministic random persistence" $modRandom "snapshot\.StreamNames.*snapshot\.StreamStates.*snapshot\.StreamSteps" "ModRandom snapshots must preserve named stream identities and state."
Assert-Contains "deterministic random persistence" $modRandomState "ModRandom\.ResetForSaveSeed\(data\.masterSeed,\s*mode\)" "seed-reuse loads must explicitly reset named streams from the saved seed."
Assert-Contains "deterministic random manager routing" $modManagerBase 'string featureId\s*=\s*"manager:"' "ModManagerBase streams must remain distinct for separate manager types."
Assert-Contains "deterministic random manager routing" $modManagerBase "ModRandom\.GetStream\(modId,\s*featureId\)" "ModManagerBase must use canonical named streams."
Assert-NotContains "deterministic random manager routing" $modManagerBase "GetHashCode\(" "ModManagerBase deterministic streams must not use process-dependent string hashing."

Assert-Contains "bootstrap registration diagnostics" $apiIds "GameRuntime\." "canonical GameRuntime IDs must remain defined in ModAPI."
Assert-Contains "bootstrap registration diagnostics" $bootstrap "RegisterApi\(GameRuntimeApiIds\." "bootstrap must register canonical GameRuntime IDs."
Assert-Contains "bootstrap registration diagnostics" $bootstrap "RegisterApi\(ShelteredApiAliasIds\." "bootstrap must register ShelteredAPI aliases for compatibility."
Assert-Contains "bootstrap registration diagnostics" $bootstrap "ShelteredContent\.Service" "bootstrap must register the facade-backed Sheltered content service."

Assert-Contains "scenario save guard" $scenarioSaveGuards 'StandardStorageScenarioId\s*=\s*"Standard"' "reserved Standard save storage id must be centralized."
Assert-Contains "scenario save guard" $scenarioSaveGuards 'VanillaSurroundedScenarioId\s*=\s*"Vanilla\.Surrounded"' "reserved Surrounded scenario id must be centralized."
Assert-Contains "scenario save guard" $scenarioSaveGuards 'VanillaStasisScenarioId\s*=\s*"Vanilla\.Stasis"' "reserved Stasis scenario id must be centralized."
Assert-Contains "scenario save guard" $scenarioSaveGuards 'VanillaSurroundedStorageScenarioId\s*=\s*"Surrounded"' "reserved Surrounded storage id must be centralized."
Assert-Contains "scenario save guard" $scenarioSaveGuards 'VanillaStasisStorageScenarioId\s*=\s*"Stasis"' "reserved Stasis storage id must be centralized."
Assert-Contains "scenario save guard" $scenarioSaveGuards 'VanillaSurroundedSaveId\s*=\s*"vanilla_surrounded"' "reserved Surrounded save id must be centralized."
Assert-Contains "scenario save guard" $scenarioSaveGuards 'VanillaStasisSaveId\s*=\s*"vanilla_stasis"' "reserved Stasis save id must be centralized."
Assert-Contains "scenario save guard" $scenarioSaveGuards 'ScenarioAuthoringDraftStorageScenarioId\s*=\s*"ScenarioAuthoringDrafts"' "draft scenario storage id must be guarded."
Assert-Contains "scenario save guard" $scenarioSaves 'RequireCustomScenarioId\(scenarioId,\s*"ScenarioSaves\.Delete"' "DeleteScenario must reject reserved custom-scenario ids before resolving paths."
Assert-Contains "scenario save guard" $scenarioSaves 'RequireCustomScenarioId\(scenarioId,\s*"ScenarioSaves\.Overwrite"' "OverwriteScenario must reject reserved custom-scenario ids before resolving paths."
Assert-Contains "scenario save guard" $scenarioSaves 'RequireCustomScenarioId\(scenarioId,\s*"ScenarioSaves\.Get"' "GetScenario must share the centralized custom-scenario id guard."
Assert-Contains "scenario starting cast source of truth" $scenarioPlayStartReadiness "public static bool HasStartingSurvivor\(ScenarioDefinition definition\)" "scenario readiness must expose a shared authored starting-cast helper."
Assert-Contains "scenario starting cast source of truth" $scenarioAuthoringPresentationBuilder "ScenarioPlayStartReadiness\.HasStartingSurvivor\(definition\)" "cast-page empty-state guidance must consume the shared starting-cast helper."
Assert-Contains "scenario starting cast source of truth" $scenarioOverviewAuthoringContentBuilder "ScenarioPlayStartReadiness\.HasStartingSurvivor\(definition\)" "home checklist first-survivor status must consume the shared starting-cast helper."
Assert-NotContains "scenario starting cast source of truth" $scenarioOverviewAuthoringContentBuilder "HasNamedStartingSurvivor\(" "home checklist should not use stale name-based starting survivor logic."
Assert-Contains "scenario save guard" $scenarioSaveLibrary "TryResolveStorageSaveEntry" "built-in scenario display saves must be resolvable back to their storage entry before mutation."
Assert-Contains "scenario save guard" $scenarioSaveLibrary "Delete display slot result" "built-in scenario display-slot deletes must route through resolved storage save ids."
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

Assert-Contains "scenario actor contract" $scenarioDefinitionModel "public sealed class ScenarioActorRef.*public string Kind.*public int LocalId.*public string Domain.*public string BindingType.*public string BindingKey.*public string DisplayNameFallback.*public string RequiredModId" "ScenarioActorRef must retain the minimal actor identity and binding contract."
Assert-Contains "scenario actor contract" $scenarioDefinitionModel "public sealed class ScenarioActorComponentDefinition.*public string ComponentId.*public string OwnerModId.*public int Version.*public string PayloadJson" "ScenarioActorComponentDefinition must preserve owner/version/payload for unknown component round-trips."
Assert-Contains "scenario actor attachment" $scenarioDefinitionModel "class ScenarioNpcDefinition.*ActorComponents = new List<ScenarioActorComponentDefinition>.*public ScenarioActorRef ActorRef.*public List<ScenarioActorComponentDefinition> ActorComponents" "Scenario NPCs must carry actor refs and component payloads."
Assert-Contains "scenario actor attachment" $scenarioDefinitionModel "class FamilyMemberConfig.*ActorComponents = new List<ScenarioActorComponentDefinition>.*public ScenarioActorRef ActorRef.*public List<ScenarioActorComponentDefinition> ActorComponents" "Starting family configs must carry actor refs and component payloads."
Assert-Contains "scenario actor attachment" $scenarioDefinitionModel "class FutureSurvivorDefinition.*ActorComponents = new List<ScenarioActorComponentDefinition>.*public ScenarioActorRef ActorRef.*public List<ScenarioActorComponentDefinition> ActorComponents" "Future survivors must carry actor refs and component payloads."
Assert-Contains "scenario actor attachment" $scenarioConditionRefModel "public ScenarioActorRef ActorRef" "Scenario conditions must carry actor refs for actor-bound gates."
Assert-Contains "scenario actor attachment" $scenarioEffectDefinitionModel "public ScenarioActorRef ActorRef" "Scenario effects must carry actor refs for actor-bound effects."
Assert-Contains "actor-ref XML round-trip" $scenarioActorXmlSerializer "ReadActorRef\(XmlElement parent\).*ScenarioXmlSerializerUtil\.Child\(parent,\s*""Actor""\).*actorRef\.Kind.*actorRef\.LocalId.*actorRef\.BindingType.*actorRef\.BindingKey" "Actor refs must deserialize from the <Actor> XML shape."
Assert-Contains "actor-ref XML round-trip" $scenarioActorXmlSerializer "WriteActorRef\(XmlWriter writer,\s*ScenarioActorRef actorRef\).*WriteStartElement\(""Actor""\).*""kind"".*""localId"".*""bindingType"".*""bindingKey""" "Actor refs must serialize to the <Actor> XML shape."
Assert-Contains "actor component XML preservation" $scenarioActorXmlSerializer "ReadActorComponents\(.*""ActorComponents"".*""Component"".*component\.ComponentId.*component\.OwnerModId.*component\.Version.*component\.PayloadJson" "Actor component envelopes must deserialize owner/version/payload."
Assert-Contains "actor component XML preservation" $scenarioActorXmlSerializer "WriteActorComponents\(.*WriteStartElement\(""ActorComponents""\).*WriteStartElement\(""Component""\).*""ownerModId"".*""version"".*""PayloadJson""" "Actor component envelopes must serialize unknown payload JSON for round-trip preservation."
Assert-Contains "actor-ref family XML" $familyScenarioSerializer "survivor\.ActorRef = ScenarioActorXmlSerializer\.ReadActorRef\(futureElement\).*ReadActorComponents\(futureElement,\s*survivor\.ActorComponents\).*member\.ActorRef = ScenarioActorXmlSerializer\.ReadActorRef\(memberElement\).*ReadActorComponents\(memberElement,\s*member\.ActorComponents\)" "Family serializer must read actor refs/components for future and starting survivors."
Assert-Contains "actor-ref family XML" $familyScenarioSerializer "WriteActorRef\(writer,\s*survivor\.ActorRef\).*WriteActorComponents\(writer,\s*survivor\.ActorComponents\).*WriteActorRef\(writer,\s*member\.ActorRef\).*WriteActorComponents\(writer,\s*member\.ActorComponents\)" "Family serializer must write actor refs/components for future and starting survivors."
Assert-Contains "actor-ref scenario XML" $scenarioSerializer "character\.ActorRef = ScenarioActorXmlSerializer\.ReadActorRef\(node\).*ReadActorComponents\(node,\s*character\.ActorComponents\).*condition\.ActorRef = ScenarioActorXmlSerializer\.ReadActorRef\(node\).*effect\.ActorRef = ScenarioActorXmlSerializer\.ReadActorRef\(node\)" "Scenario serializer must read actor refs for NPCs, conditions, and effects."
Assert-Contains "actor-ref scenario XML" $scenarioSerializer "WriteActorRef\(writer,\s*character\.ActorRef\).*WriteActorComponents\(writer,\s*character\.ActorComponents\).*WriteActorRef\(writer,\s*condition\.ActorRef\).*WriteActorRef\(writer,\s*effect\.ActorRef\)" "Scenario serializer must write actor refs for NPCs, conditions, and effects."
Assert-Contains "legacy actor load" $scenarioActorXmlSerializer "if \(element == null\)\s*return null" "Legacy XML without <Actor> must load without synthesizing serialized refs."
Assert-Contains "deterministic synthetic actor migration" $scenarioActorResolver "BuildLegacyStartingMemberRef\(.*DeterministicLocalId\(domain \+ ""\|member\|"" \+ Math\.Max\(0,\s*memberIndex\)\.ToString\(\)\)" "Starting survivor legacy migration IDs must be deterministic from scenario id and member index."
Assert-Contains "deterministic synthetic actor migration" $scenarioActorResolver "BuildLegacyFutureSurvivorRef\(.*DeterministicLocalId\(domain \+ ""\|future\|"" \+ id\)" "Future survivor legacy migration IDs must be deterministic from scenario id and FutureSurvivorDefinition.Id."
Assert-Contains "deterministic synthetic actor migration" $scenarioActorResolver "uint hash = 2166136261u.*hash \*= 16777619u" "Scenario actor migration IDs must use a stable hash, not string.GetHashCode."
Assert-NotContains "deterministic synthetic actor migration" $scenarioActorResolver "GetHashCode\(" "Scenario actor migration IDs must not use process-randomized string.GetHashCode."
Assert-Contains "scenario actor resolver ladder" $scenarioActorResolver "TryBuildActorId\(actorRef,\s*out requestedId\).*_actors\.TryGet\(requestedId,\s*out exact\).*_actors\.TryResolve\(actorRef\.BindingType,\s*actorRef\.BindingKey,\s*out boundId\).*_actors\.Ensure\(new ActorCreateRequest" "Resolver must try exact ActorId, then ActorBinding, then scenario-owned synthetic Ensure."
Assert-Contains "scenario actor resolver behavior" $scenarioActorResolver "IsScenarioOwnedSynthetic\(definition,\s*actorRef,\s*requestedId\).*ActorPresenceState\.Offscreen.*ActorFlags\.Persistent \| ActorFlags\.Synthetic" "Resolver synthetic fallback must be scenario-owned, persistent, synthetic, and offscreen."
Assert-Contains "scenario actor resolver behavior" $scenarioActorResolver "BuildProfile\(actorRef,\s*familyMember,\s*npc\).*BuildAttributes\(familyMember,\s*npc\).*HydrateSerializedComponents\(actorId,\s*components\)" "Resolver must centralize built-in profile/attribute and registered component hydration."
Assert-Contains "scenario actor resolver behavior" $scenarioActorResolver "GetDisplayName\(ActorId actorId,\s*string fallback\).*TryGet<ActorProfileComponent>" "Resolver must centralize display/profile lookup."
Assert-Contains "scenario actor apply hook" $scenarioApplyCoordinator "ApplyStep\(""actors"",\s*result,\s*delegate \{ _actorResolver\.EnsureScenarioActors\(definition\); \}\)" "Runtime apply must minimally ensure actor-backed cast records before legacy materialization."
Assert-Contains "scenario cast actor authoring" $scenarioCharacterEditorAuthoringService "EnsureStartingMemberRef\(session\.WorkingDefinition,\s*config,\s*family\.Members\.Count\)" "New starting survivors must receive a scenario-owned synthetic actor ref at creation time."
Assert-Contains "scenario cast actor authoring" $scenarioGameplayScheduleAuthoringService "EnsureFutureSurvivorRef\(session\.WorkingDefinition,\s*survivor,\s*family\.FutureSurvivors\.Count\)" "New future survivors must receive a scenario-owned synthetic actor ref at creation time."
Assert-Contains "scenario cast actor save" $scenarioEditorController "AssignMissingCastActorRefs\(session\.WorkingDefinition\).*_serializer\.Save\(session\.WorkingDefinition,\s*path\)" "Editor save must assign missing cast actor refs before writing scenario XML."
Assert-Contains "scenario cast live identity capture" $scenarioAuthoringPresentationBuilder "ActionLiveSurvivorAddToStartingPrefix \+ actorLocalId\.ToString" "Live-world add-to-cast actions must target FamilyMember actor identity instead of live list index."
Assert-Contains "scenario cast live identity capture" $scenarioAuthoringCaptureService "CreateLiveFamilyMemberRef\(member\).*FindFamilyByActorRef" "Family capture must persist and diff live survivors by actor identity before legacy name matching."
Assert-Contains "scenario family materialization binding" $familyApplyService "ResolveAuthoredMember\(definition,\s*config,\s*i,\s*members\).*TryResolveFamilyMember\(definition,\s*config\.ActorRef" "Family apply must resolve authored members by actor ref before legacy index fallback."
Assert-Contains "scenario family materialization binding" $familyApplyService "BindMaterializedMember\(definition,\s*config\.ActorRef,\s*member,\s*result\)" "Family apply must bind materialized starting members after FamilyMember.GetId is available."
Assert-Contains "scenario survivor actor runtime" $scheduledSurvivorRuntimeService "FindFutureSurvivor\(definition,\s*effect\).*ReferencesSameActor" "Future survivor effects must resolve actor refs before legacy survivor string IDs."
Assert-Contains "scenario survivor actor runtime" $scheduledSurvivorRuntimeService "condition\.ActorRef.*TryResolveFamilyMember\(definition,\s*condition\.ActorRef.*FindPresentSurvivorByName" "Survivor conditions must resolve actor refs first while retaining legacy first-name fallback."
Assert-Contains "scenario survivor actor runtime" $scheduledSurvivorRuntimeService "BindFutureSurvivor\(definition,\s*survivor,\s*spawned\)" "Future survivor immediate materialization must bind spawned FamilyMember identities."

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

Assert-Contains "runtime object panels" $runtimeObjectPanels "CreateContext\(registration,\s*target,\s*member\)" "object panel callbacks must receive a context built from the clicked target object."
Assert-Contains "runtime object panels" $runtimeObjectPanels "target\.objectId\.ToString\(\)" "object panel context ObjectId must resolve to the clicked object's stable objectId when available."
Assert-Contains "runtime object panels" $runtimeUiContracts "public ObjectManager\.ObjectType ObjectType" "object panel context must expose the clicked object's ObjectType for store and workstation routing."

Assert-Contains "UI extension facade" $uiFacade "public static UICloneResult CloneElement\(GameObject template,\s*Transform parent,\s*UICloneOptions options\)" "ShelteredUI must expose focused safe-clone options and result DTOs."
Assert-Contains "UI extension facade" $uiFacade "BindButtonClick<TContext>" "ShelteredUI must support safe per-item button context capture."
Assert-Contains "UI extension results" $uiExtensionContracts "ReadOnlyCollection<string> Warnings" "best-effort UI operations must expose warnings."
Assert-Contains "UI extension listener stripping" $uiExtensionService "listener\.onClick\s*=\s*null.*listener\.onHover\s*=\s*null" "safe clone/reset must clear inherited UIEventListener delegates."
Assert-Contains "UI extension button binding" $uiExtensionService "UIButtonBindingMode\.Replace.*EventDelegate\.Set\(button\.onClick,\s*callback\).*EventDelegate\.Add\(button\.onClick,\s*callback\)" "button binding must deliberately distinguish replacement from append behavior."
Assert-Contains "UI extension colors" $uiExtensionService "TweenColorRecord.*record\.Tween\.from\s*=\s*record\.From.*record\.Widget\.color\s*=\s*record\.Color" "color restore must restore tween and widget state."
Assert-Contains "UI extension lifecycle" $uiExtensionService "PanelLifecycleSubscription<TPanel>.*UIEvents\.OnPanelClosed\s*\+=\s*OnClosed.*UIEvents\.OnPanelClosed\s*-=\s*OnClosed" "typed panel lifecycle subscriptions must be disposable."
Assert-Contains "UI tooltip restoration" $uiTakeover "RestoreHover\(go,\s*bindingKey,\s*previous\).*ModTooltip\.Hide\(\)" "tooltip bindings must release or restore hover state when a takeover is restored."

Assert-Contains "runtime stores" $shelteredStores "public static IItemStore ForObject\(string ownerId,\s*Obj_Base targetObject" "stores must expose object-scoped mod storage for object-attached containers like fridges."
Assert-Contains "runtime stores" $shelteredStores "BuildObjectStoreId\(targetObject\)" "object-scoped stores must use a centralized object store id helper."
Assert-Contains "runtime stores" $shelteredStores "public static IItemStore FindNearestObjectStore" "stores must expose nearest object-store lookup for stove-to-fridge ingredient routing."
Assert-Contains "runtime stores" $shelteredStores "Transfer\(store,\s*transferStore" "container request helper must move items out of the container into the paired transfer store."
Assert-Contains "runtime stores" $shelteredStores "Transfer\(transferStore,\s*store" "container request helper must move items from the paired transfer store into the container."

Assert-Contains "character item actor ownership" $characterItemContracts "public ActorId ActorId" "character item assignments must persist actor IDs as the person identity."
Assert-Contains "character item actor ownership" $characterItemContracts "Assign\(\s*ActorId actorId,\s*IItemStore source" "assignment service must expose actor-first assignment APIs."
Assert-Contains "character item actor ownership" $shelteredCharacterItems "Assign\(\s*ActorId actorId,\s*IItemStore source" "ShelteredCharacterItems facade must expose actor-first assignment APIs."
Assert-Contains "character item actor ownership" $characterItemAssignments "ShelteredActors\.FamilyMemberActorId\(id\)" "FamilyMember convenience APIs must resolve identity through ShelteredActors."
Assert-Contains "character item actor ownership" $characterItemAssignments "ShelteredActors\.Instance\.Ensure\(new ActorCreateRequest" "assignment tracking must ensure identities live in the actor system."
Assert-Contains "character item actor persistence" $characterItemAssignments 'data\.SaveLoad\("actorKind"' "assignment persistence must save actor kind."
Assert-Contains "character item actor persistence" $characterItemAssignments 'data\.SaveLoad\("actorLocalId"' "assignment persistence must save actor local ID."
Assert-Contains "character item actor persistence" $characterItemAssignments 'data\.SaveLoad\("actorDomain"' "assignment persistence must save actor domain."
Assert-NotContains "character item actor ownership" $characterItemContracts "MemberKey" "new character assignment API must not expose separate member keys."
Assert-NotContains "character item actor ownership" $characterItemAssignments "BuildMemberKey|family\.name\.|family\.instance\." "assignment tracking must not maintain a separate member-key identity path."

Assert-Contains "runtime cooking station" $shelteredCooking "ResolveObjectId\(registration,\s*context\)" "cooking station context must preserve object panel ObjectId instead of falling back to registration metadata."
Assert-Contains "runtime cooking station" $shelteredCooking "RollbackIngredients\(ingredientStore,\s*consumed\)" "cooking crafts must roll back consumed ingredients when output insertion fails."
Assert-Contains "runtime cooking station" $cookingContracts "public CookingStationJobOptions JobOptions" "cooking station requests and registrations must expose timed job options."
Assert-Contains "runtime cooking station" $cookingContracts "public Action<CookingCraftContext> OnCraftQueued" "cooking station API must let mods react when a timed job is queued."
Assert-Contains "runtime cooking station" $shelteredCooking "QueueCraftJob\(recipe,\s*request,\s*ingredientStore,\s*outputStore,\s*context\)" "cooking station crafts with job options must queue a timed work job before applying output."
Assert-Contains "runtime cooking station" $shelteredCooking "CompleteQueuedCraft" "queued cooking jobs must apply recipes only on job completion."
Assert-Contains "runtime cooking station" $shelteredCooking "FindIdleWorker" "cooking stations must provide default idle worker selection for automatic jobs."
Assert-Contains "runtime timed work job" $runtimeTimedWorkJob "class RuntimeTimedWorkJob\s*:\s*Job" "timed workstation work must run through the vanilla Job queue."
Assert-Contains "runtime timed work job" $runtimeTimedWorkJob "InteractionManager\.Instance\.SetInteractionProgress" "timed workstation jobs must surface progress through the vanilla interaction progress UI."

Assert-Contains "player queue facade" $shelteredQueues "public static event Action<PlayerQueueChangedEventArgs> QueueChanged" "ShelteredQueues must expose queue change notifications."
Assert-Contains "player queue facade" $shelteredQueues "GetPlayerQueue\(ActorId owner\).*SnapshotQueue\(ActorId owner\).*RestoreQueue\(PlayerQueueSnapshot snapshot\)" "ShelteredQueues must expose actor-first query, snapshot, and restore operations."
Assert-Contains "player queue identities" $playerQueueContracts "public ActorId ActorId.*CloneActorId" "queue-owner identity must return copied actor identities."
Assert-Contains "player queue DTO boundary" $playerQueueContracts "No live Job, Obj_Base, or FamilyMember reference is exposed" "queue entries must document their detached runtime boundary."
Assert-NotContains "player queue DTO boundary" $playerQueueContracts "public\s+(Job|JobQueue|FamilyMember|Obj_Base)\s" "public queue DTOs must not expose mutable vanilla runtime objects."
Assert-NotContains "player queue capacity policy" ($playerQueueContracts + $shelteredQueues + $playerQueueRuntime) "SetPlayerQueueCapacity" "ShelteredAPI must not publish mod-specific player queue capacity mutation."
Assert-Contains "player queue safe unavailable" $playerQueueRuntime "SaveManager\.instance == null.*No active save session is available" "queue lookup must return unavailable behavior without a live save session."
Assert-Contains "player queue safe restore" $playerQueueRuntime "liveCapacity != snapshot\.Capacity.*capacity policy is not changed by ShelteredAPI" "queue restore must validate rather than mutate player queue capacity."
Assert-Contains "player queue safe restore" $playerQueueRuntime "CanReplaceEmptyQueue\(member,\s*liveQueue\)" "queue restore must not replace a live non-empty player queue."
Assert-Contains "player queue safe restore" $playerQueueRuntime "IsSafelyRestorableType.*Job_GoToLocation" "queue restore must restrict reconstructed vanilla work to safe job shapes."
Assert-Contains "player queue event bridge" $playerQueuePatches "PatchPolicy\(PatchDomain\.Characters,\s*""PlayerQueueChanges"".*JobQueue.*AddJob.*JobQueue.*RemoveAt.*JobQueue.*ForceClear" "vanilla player-queue mutations must feed the facade event bridge."

if ($failures.Count -gt 0) {
    Write-Host ("ShelteredAPI contract tests failed: " + $failures.Count)
    foreach ($failure in $failures) {
        Write-Host ("FAIL`t" + $failure)
    }
    exit 1
}

Write-Host "ShelteredAPI contract tests passed."
