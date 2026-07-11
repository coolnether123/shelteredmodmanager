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
$actorAuthoringCapabilities = Read-RepoFile "ModAPI\Actors\Authoring\ActorAuthoringCapabilities.cs"
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
$scenarioLaunchPolicy = Read-RepoFile "ShelteredAPI\Scenarios\Application\Selection\ScenarioLaunchSetupPolicy.cs"
$scenarioLaunchCoordinator = Read-RepoFile "ShelteredAPI\Scenarios\Application\Selection\ScenarioLaunchCoordinator.cs"
$scenarioLoadingTransitionGuard = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Unity\ScenarioLoadingTransitionGuard.cs"
$scenarioLaunchSetupUi = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioLaunchSetupAuthoringSectionBuilder.cs"
$scenarioGuidedPatches = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Harmony\ShelteredCustomScenarioPatches.cs"
$scenarioAssetInventory = Read-RepoFile "ShelteredAPI\Scenarios\Application\Assets\ScenarioAssetInventoryService.cs"
$scenarioAssetInventoryMutations = Read-RepoFile "ShelteredAPI\Scenarios\Application\Assets\ScenarioAssetInventoryMutationService.cs"
$scenarioAssetInventoryVerification = Read-RepoFile "ShelteredAPI\Scenarios\Diagnostics\ScenarioAssetInventoryVerification.cs"
$scenarioAssetInventoryContent = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioAssetInventoryContentBuilder.cs"
$scenarioPackagePlan = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioPackagePlan.cs"
$scenarioAuthorTestChecklistModel = Read-RepoFile "ShelteredAPI\Scenarios\Definitions\ScenarioAuthorTestChecklist.cs"
$scenarioAuthorTestChecklistService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthorTestChecklistService.cs"
$scenarioAuthorTestChecklistSerializer = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Serialization\AuthorTestChecklistScenarioSectionSerializer.cs"
$scenarioAuthorTestChecklistSection = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioAuthorTestChecklistSectionBuilder.cs"
$scenarioAuthorTestChecklistVerification = Read-RepoFile "ShelteredAPI\Scenarios\Diagnostics\ScenarioAuthorTestChecklistVerification.cs"
$scenarioPlaytestOrchestrator = Read-RepoFile "ShelteredAPI\Scenarios\Application\Runtime\ScenarioPlaytestOrchestrator.cs"
$scenarioEditorSession = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioEditorSession.cs"
$scenarioRuntimeBindingManager = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Unity\ShelteredScenarioRuntimeBindingManager.cs"
$scenarioWinLossOutcomeService = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScenarioWinLossOutcomeService.cs"
$scenarioEndGamePresenter = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScenarioEndGamePresenter.cs"
$scenarioVictoryPanel = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Runtime\ScenarioVictoryPanel.cs"
$scenarioRuntimeOutcomeVerification = Read-RepoFile "ShelteredAPI\Scenarios\Diagnostics\ScenarioRuntimeOutcomeVerification.cs"
$scenarioStoryAuthoringActions = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioStoryAuthoringActions.cs"
$scenarioBuildPlacement = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioBuildPlacementAuthoringService.cs"
$scenarioJournalDefinition = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Journal\JournalDefinition.cs"
$scenarioJournalProvider = Read-RepoFile "ShelteredAPI\Scenarios\Application\Scheduling\ScenarioJournalScheduledActionProvider.cs"
$scheduledJournalRuntime = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScheduledJournalRuntimeService.cs"
$scenarioJournalPatches = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Harmony\ScenarioJournalPatches.cs"
$scenarioScheduleRuntimeCoordinator = Read-RepoFile "ShelteredAPI\Scenarios\Application\Scheduling\ScenarioScheduleRuntimeCoordinator.cs"
$scenarioSchedulePolicy = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Scheduling\ScenarioSchedulePolicy.cs"
$scenarioSchedulePolicyEvaluator = Read-RepoFile "ShelteredAPI\Scenarios\Application\Scheduling\ScenarioSchedulePolicyEvaluator.cs"
$scheduledWorldEventRuntime = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScheduledWorldEventRuntimeService.cs"
$scenarioWorldEventPatches = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Harmony\ScenarioWorldEventPatches.cs"
$scenarioWorldEventRuntimeState = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScenarioWorldEventRuntimeState.cs"
$scenarioConversationRuntime = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScenarioConversationRuntimeService.cs"
$scenarioConversationPatches = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Harmony\ScenarioConversationChatterPatches.cs"
$scenarioConversationProvider = Read-RepoFile "ShelteredAPI\Scenarios\Application\Scheduling\ScenarioConversationScheduledActionProvider.cs"
$scenarioConversationValidation = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Validation\ScenarioConversationValidationRule.cs"
$scenarioStationUpgradeService = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScenarioStationUpgradePropertyService.cs"
$scenarioBunkerApplyService = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\BunkerApplyService.cs"
$scenarioEffectKind = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Effects\ScenarioEffectKind.cs"
$scenarioServiceCollectionExtensions = Read-RepoFile "ShelteredAPI\Scenarios\Composition\ServiceCollectionExtensions.cs"
$familyScenarioSerializer = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Serialization\FamilyScenarioSectionSerializer.cs"
$scenarioActorXmlSerializer = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Serialization\ScenarioActorXmlSerializer.cs"
$scenarioConditionRefModel = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Conditions\ScenarioConditionRef.cs"
$scenarioEffectDefinitionModel = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Effects\ScenarioEffectDefinition.cs"
$scenarioActorResolver = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScenarioActorResolver.cs"
$scenarioApplyCoordinator = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScenarioApplyCoordinator.cs"
$scenarioInventoryApplyService = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\InventoryApplyService.cs"
$scenarioAuthoringInventoryProjectionService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthoringInventoryProjectionService.cs"
$scenarioCharacterEditorAuthoringService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioCharacterEditorAuthoringService.cs"
$scenarioSurvivorAuthoringOperations = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioSurvivorAuthoringOperations.cs"
$scenarioSurvivorTraitConflictRules = Read-RepoFile "ShelteredAPI\Scenarios\Domain\People\ScenarioSurvivorTraitConflictRules.cs"
$scenarioValidator = Read-RepoFile "ShelteredAPI\Scenarios\Diagnostics\ScenarioValidator.cs"
$scenarioActorAuthoringRegistry = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioActorAuthoringCapabilityRegistry.cs"
$scenarioActorAuthoringStore = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioActorAuthoringFieldStore.cs"
$scenarioDevActorAuthoringProvider = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioDevActorAuthoringCapabilityProvider.cs"
$scenarioAuthoringContracts = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthoringContracts.cs"
$scenarioSpriteSwapRuleEditor = Read-RepoFile "ShelteredAPI\Scenarios\Application\Assets\ScenarioSpriteSwapRuleEditor.cs"
$scenarioSpriteSwapAuthoringService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Assets\ScenarioSpriteSwapAuthoringService.cs"
$scenarioSpriteRuntimeMutationService = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Assets\ScenarioSpriteRuntimeMutationService.cs"
$scenarioAuthoringCaptureService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthoringCaptureService.cs"
$scenarioBunkerDraftService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioBunkerDraftService.cs"
$scenarioGameplayScheduleAuthoringService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioGameplayScheduleAuthoringService.cs"
$scenarioCastMemberReferenceCatalog = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioCastMemberReferenceCatalog.cs"
$scenarioEventAuthoringService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioEventAuthoringService.cs"
$scenarioStoryAuthoringService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioStoryAuthoringService.cs"
$scenarioEditorController = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioEditorController.cs"
$scenarioAuthoringCommandHandlers = Read-RepoFile "ShelteredAPI\Scenarios\Application\Commands\ScenarioAuthoringCommandHandlers.cs"
$scenarioBaseModeReloadService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthoringBaseModeReloadService.cs"
$scenarioOpeningCutsceneAuthoringService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioOpeningCutsceneAuthoringService.cs"
$scenarioAuthoringBootstrapService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthoringBootstrapService.cs"
$familyApplyService = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\FamilyApplyService.cs"
$scheduledSurvivorRuntimeService = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScheduledSurvivorRuntimeService.cs"
$scenarioFamilyMemberFactory = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScenarioFamilyMemberFactory.cs"
$scenarioFutureSurvivorRecruitBindingService = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Runtime\ScenarioFutureSurvivorRecruitBindingService.cs"
$shelteredCustomScenarioPatches = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Harmony\ShelteredCustomScenarioPatches.cs"
$scenarioVerification = Read-RepoFile "ShelteredAPI\Scenarios\Diagnostics\ScenarioFrameworkVerification.cs"
$scenarioSuppliesPresetCatalog = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\Supplies\ScenarioSuppliesPresetCatalog.cs"
$scenarioSuppliesInventoryNormalizer = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\Supplies\ScenarioSuppliesInventoryNormalizer.cs"
$scenarioSuppliesBalanceEstimator = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\Supplies\ScenarioSuppliesBalanceEstimator.cs"
$scenarioSuppliesContentBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioSuppliesAuthoringContentBuilder.cs"
$seamGuard = Read-RepoFile "ShelteredAPI\Infrastructure\SeamGuard.cs"
$scenarioPlayStartReadiness = Read-RepoFile "ShelteredAPI\Scenarios\Application\Runtime\ScenarioPlayStartReadiness.cs"
$scenarioAuthoringPresentationBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioAuthoringPresentationBuilder.cs"
$scenarioAuthoringWindowRenderer = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\Rendering\ScenarioAuthoringShellWindowImguiRenderer.cs"
$scenarioAuthoringShellRenderer = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioAuthoringShellImguiRenderModule.cs"
$scenarioAuthoringTutorialRenderer = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\Rendering\ScenarioAuthoringShellTutorialImguiRenderer.cs"
$scenarioAssetBrowserUx = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioAssetBrowserUx.cs"
$scenarioAssetBrowserRenderer = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\Rendering\ScenarioAuthoringShellAssetBrowserImguiRenderer.cs"
$scenarioGlobalSearchRenderer = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\Rendering\ScenarioAuthoringShellGlobalSearchImguiRenderer.cs"
$scenarioAssetAuthoringContent = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioAssetAuthoringContentBuilder.cs"
$scenarioWeatherEffectCatalog = Read-RepoFile "ShelteredAPI\Scenarios\Infrastructure\Assets\ScenarioWeatherEffectSpriteCatalogService.cs"
$scenarioUiStyleSheet = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\UiKit\ScenarioUiStyleSheet.cs"
$scenarioCastMemberPickerBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioCastMemberPickerBuilder.cs"
$scenarioStoryCharacterActorLinkSectionBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioStoryCharacterActorLinkSectionBuilder.cs"
$scenarioStoryFocusedEditorDocumentBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioStoryFocusedEditorDocumentBuilder.cs"
$scenarioWorldEventFocusedEditorDocumentBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioWorldEventFocusedEditorDocumentBuilder.cs"
$scenarioQuestAuthoringContentBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioQuestAuthoringContentBuilder.cs"
$scenarioPublishAuthoringContentBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioPublishAuthoringContentBuilder.cs"
$scenarioTimelineBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Application\Timeline\ScenarioTimelineBuilder.cs"
$scenarioTimelineNavigationService = Read-RepoFile "ShelteredAPI\Scenarios\Application\Timeline\ScenarioTimelineNavigationService.cs"
$scenarioOverviewAuthoringContentBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioOverviewAuthoringContentBuilder.cs"
$scenarioStatusBarViewModelBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\StatusBarViewModelBuilder.cs"
$gateConditionValidationRule = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Validation\GateConditionValidationRule.cs"
$schedulingValidationRule = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Validation\SchedulingValidationRule.cs"
$scenarioModDependencyDetector = Read-RepoFile "ShelteredAPI\Scenarios\Application\Compatibility\ScenarioModDependencyDetector.cs"
$scenarioModReferenceReason = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Compatibility\ScenarioModReferenceReason.cs"
$runtimeOrchestrator = Read-RepoFile "ShelteredAPI\Scenarios\Application\Runtime\ScenarioRuntimeOrchestrator.cs"
$runtimeContracts = Read-RepoFile "ShelteredAPI\Scenarios\Application\Runtime\ScenarioRuntimeContracts.cs"
$scenarioTestConsole = Read-RepoFile "ShelteredAPI\Scenarios\Application\Runtime\ScenarioTestConsoleService.cs"
$scenarioExecutionLog = Read-RepoFile "ShelteredAPI\Scenarios\Application\Runtime\ScenarioRuntimeExecutionLog.cs"
$scenarioTestConsoleContent = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioTestConsoleAuthoringContentBuilder.cs"
$scenarioTestConsoleCommands = Read-RepoFile "ShelteredAPI\Scenarios\Application\Commands\ScenarioTestConsoleCommandHandler.cs"
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

# TOURCALM: unresolved, off-screen, and full-surface targets collapse to the
# same no-target path before any border, pointer, click, or cutout is rendered.
Assert-Contains "scenario tutorial no-target normalization" $scenarioAuthoringTutorialRenderer "NormalizeSpotlightTargetRect\(.*float\.IsNaN\(targetRect\.x\).*float\.IsInfinity\(targetRect\.height\).*!HasSpotlightTarget\(targetRect\).*!targetRect\.Overlaps\(availableRect\).*IsFullSurfaceSpotlightTarget\(targetRect, availableRect\).*return ZeroRect\(\)" "tutorial/tour targets that cannot identify a visible control must normalize to no target."
Assert-Contains "scenario tutorial large-target suppression" $scenarioAuthoringTutorialRenderer "return \(coverX \* coverY\) >= 0\.35f \|\| \(coverX >= 0\.6f && coverY >= 0\.5f\);" "large tutorial/tour targets must suppress the cutout by area or paired broad dimensions without rejecting tall, narrow controls."
Assert-Contains "scenario tutorial no-target draw gate" $scenarioAuthoringTutorialRenderer "bool hasTarget = HasSpotlightTarget\(targetRect\);.*if \(hasTarget\)\s*DrawSpotlightBorder.*if \(hasTarget\)\s*DrawSpotlightPointer" "highlight and pointer drawing must share the normalized has-target gate."
Assert-Contains "scenario tutorial pointer overlap" $scenarioAuthoringTutorialRenderer "cardRect\.Overlaps\(targetRect\).*return;" "spotlight pointer lines must not draw through overlapping cards and controls."
Assert-Contains "scenario tutorial calm pulse" $scenarioAuthoringTutorialRenderer "float pulse = 0\.70f \+ \(Mathf\.Sin\(Time\.realtimeSinceStartup \* 2\.1f\) \* 0\.15f\)" "spotlight breathing must stay slow and between 0.55 and 0.85 alpha."
Assert-NotContains "scenario tutorial harsh field flash" $scenarioAuthoringTutorialRenderer "GUI\.Box\([^;]*Styles\.Field\)|Time\.realtimeSinceStartup \* 5f|new GUIStyle" "spotlight/card rendering must not use the filled Field box, fast flash, or per-frame GUIStyle allocation."
Assert-Contains "scenario metadata contract" $scenarioSmoke "RunMetadataContract\(\)" "smoke harness must cover saved metadata reload."
Assert-Contains "scenario metadata contract" $scenarioSmoke "Metadata placeholder validation contract failed" "smoke harness must cover placeholder metadata warnings."
Assert-Contains "scenario metadata XML" $scenarioSerializer "Credits.*Tags" "scenario XML must persist credits and optional tags."
Assert-Contains "launch setup schema" $scenarioDefinitionModel 'ScenarioLaunchSetupMode.*FullSetup.*Direct.*Guided.*ScenarioDifficultyCategoryIds.*rain.*resources.*breach.*faction.*mood.*map-size.*fog' "launch setup must expose stable vanilla difficulty category ids and all three launch modes."
Assert-Contains "launch setup XML" $scenarioSerializer 'launchSetupSerializer\.Read\(Child\(root, "LaunchSetup"\)\).*launchSetupSerializer\.Write\(writer, definition\.LaunchSetup\)' "LaunchSetup must round-trip through the section serializer pattern."
Assert-Contains "direct launch policy" $scenarioLaunchCoordinator 'launchMode == ScenarioLaunchSetupMode\.Direct.*BeginDirectScenarioTransition.*directDefinition' "Direct mode must route PLAY through the existing direct scene transition with the authored definition."
Assert-Contains "direct launch fade ownership" $scenarioLaunchCoordinator 'BeginDirectSceneTransition\(sceneName, launchTargetLabel, virtualSaveType, definition, true\).*if \(ownsDirectLaunchFadeHandoff\).*OwnDirectLaunchTransition' "only the published direct PLAY route must register ownership of the fade handoff."
Assert-Contains "direct launch fade completion" $scenarioLoadingTransitionGuard 'OwnsDirectLaunchFadeForScene\(activeSceneName\).*FadeFromBlack\(true\)' "the owned direct launch must complete through vanilla FadeManager fade-in rather than force-popping the panel."
Assert-NotContains "direct launch fade completion" $scenarioLoadingTransitionGuard 'PopPanel\(' "the direct-launch handoff must leave FadePanel removal to vanilla OnFadeFinished."
Assert-Contains "direct authored difficulty" $scenarioLaunchPolicy 'StoreMenuDifficultySettings.*ScenarioDifficultyCategoryIds\.Rain.*ScenarioDifficultyCategoryIds\.Fog' "Direct launch must apply all seven authored vanilla difficulty values."
Assert-Contains "guided category locks" $scenarioGuidedPatches 'TryGetPendingGuidedDefinition.*ApplyFixed.*CanChange.*\(authored\)' "Guided launch must pre-set fixed categories, block their controls, and show the authored note."
Assert-Contains "play experience shell payload" $scenarioLaunchSetupUi 'Id = "publish_play_experience".*Expanded = false' "Publish must expose a compact collapsible Play Experience group."
Assert-Contains "play experience shell actions" $scenarioLaunchSetupUi 'launch_setup\.value\..*launch_setup\.selectable\..*launch_setup\.mode\.' "Play Experience must expose mode, value, and lock actions to the semantic harness manifest."
Assert-Contains "launch policy verification" $scenarioVerification 'Scenario\.LaunchPolicy.*Scenario\.LegacyLaunch.*Unknown launch difficulty category' "framework verification must cover LaunchSetup round-trip, legacy defaulting, and unknown-id warnings."

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
Assert-Contains "scenario save active directory scope" $saveRegistry 'Directory\.GetDirectories\(scenarioRoot, "\*", SearchOption\.TopDirectoryOnly\).*TryGetActiveSlotNumber\(scenarioRoot, dir.*Path\.GetDirectoryName\(candidate\).*directoryName\[0\] == ''_''' "scenario save enumeration must accept only direct active Slot_* children and exclude trash/quarantine descendants."
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
Assert-Contains "scenario draft save resilience" $scenarioEditorController "ValidateForSave\(session\.WorkingDefinition,\s*path\).*_serializer\.Save\(session\.WorkingDefinition,\s*path\)" "draft saves must serialize after validation diagnostics instead of treating validation errors as save blockers."
Assert-NotContains "scenario draft save resilience" $scenarioEditorController "Save blocked because validation returned no result|Save blocked by scenario validation" "ScenarioEditorController.CommitChanges must not block draft serialization on content validation state."
Assert-NotContains "scenario draft save resilience" $scenarioAuthoringCommandHandlers "Scenario draft save failed validation" "the Save command must report saved-with-validation-errors instead of failed validation."
Assert-NotContains "scenario draft save resilience" $scenarioBaseModeReloadService "Base switch save failed validation|Baseline save failed validation|Restart save failed validation" "base-switch and restart save paths must not describe validation as a save failure."
Assert-NotContains "scenario draft save resilience" $scenarioAuthoringBootstrapService "Close-to-menu blocked by draft validation|Fix validation errors, then try Exit Editor" "exit-to-menu must not be blocked or described as blocked by validation after a draft is serialized."
Assert-NotContains "scenario draft save resilience" $scenarioStatusBarViewModelBuilder "Exit is blocked\\. Fix validation errors|Fix validation errors, then close the editor" "status bar copy must not imply content validation blocks draft close/save."
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

Assert-Contains "scenario journal contract" $scenarioDefinitionModel "public JournalDefinition Journal" "ScenarioDefinition must expose the first-class Journal section."
Assert-Contains "scenario journal contract" $scenarioJournalDefinition "class JournalEntryDefinition.*public string Id.*public string Text.*public ScenarioActorRef Writer.*public ScenarioScheduleTime DueTime.*public string TriggerId.*public string GateId.*ScenarioJournalEntryMode Mode.*Conditions" "journal entries must retain id, text, writer, timing, trigger/gate, once/repeat, and condition refs."
Assert-Contains "scenario journal XML round-trip" $scenarioSerializer "definition\.Journal\s*=\s*ReadJournal\(Child\(root,\s*""Journal""\)\)" "serializer must read the <Journal> section."
Assert-Contains "scenario journal XML round-trip" $scenarioSerializer "WriteJournal\(writer,\s*definition\.Journal\)" "serializer must write the <Journal> section."
Assert-Contains "scenario journal XML round-trip" $scenarioSerializer "ReadConditionRefs\(Child\(node,\s*""Conditions""\),\s*entry\.Conditions\).*WriteConditionRefs\(writer,\s*""Conditions"",\s*entry\.Conditions\)" "journal condition refs must round-trip through the existing gate condition machinery."
Assert-Contains "scenario journal XML round-trip" $scenarioSerializer "entry\.Writer\s*=\s*ReadJournalWriter\(node\).*WriteJournalWriter\(writer,\s*entry\.Writer\)" "journal writer actor refs must round-trip."
Assert-Contains "scenario journal XML round-trip" $scenarioSerializer "VanillaPolicy.*Suppress.*TryParseJournalCategory" "vanilla journal suppression policy must round-trip by category."
Assert-Contains "scenario journal once semantics" $scenarioJournalProvider "action\.Id\s*=\s*""journal\.""\s*\+\s*entryId.*action\.Policy\.Repeatable\s*=\s*entry\.Mode\s*==\s*ScenarioJournalEntryMode\.Repeat" "journal entries must compile into stable scheduled actions with once/repeat policy."
Assert-Contains "scenario journal once semantics" $scenarioScheduleRuntimeCoordinator "_journal\.HasExecuted\(action\.Id\)" "scheduled action runtime must preserve once execution through the runtime journal."
Assert-Contains "scenario journal effect adapter" $scheduledJournalRuntime "GetMethod\(\s*""InsertJournalEntry"".*BindingFlags\.Instance.*BindingFlags\.NonPublic.*InsertJournalEntryMethod\.Invoke" "journal writes must use the private vanilla InsertJournalEntry reflection adapter."
Assert-Contains "scenario journal effect adapter" $scheduledJournalRuntime """\[b\]""\s*\+\s*writerName.*\{writer\}.*\{day\}" "journal writes must support writer/day substitutions and writer prefix rendering."
Assert-Contains "scenario journal suppression policy" $scenarioJournalPatches "HarmonyPatch\(typeof\(JournalManager\),\s*""CreateJournalEntry""\).*ShouldSuppressCategory\(type\.ToString\(\)\)" "vanilla journal category suppression must be a CreateJournalEntry prefix."
Assert-Contains "scenario journal suppression policy" $scenarioJournalPatches "m_recordFirstEntry.*m_firstEntryEntered.*HarmonyPatch\(typeof\(JournalManager\),\s*""UpdateManager""\).*ShouldSuppressFirstEntry" "first-entry suppression must gate the vanilla first-entry fields."
Assert-Contains "scenario journal authoring UI" $scenarioAuthoringPresentationBuilder "Journal Entries" "Timeline/Events authoring must expose a Journal Entries section."
Assert-Contains "scenario journal authoring UI" $scenarioAuthoringPresentationBuilder "ActionJournalEntryAdd" "Timeline/Events authoring must expose journal entry creation."
Assert-Contains "scenario journal authoring UI" $scenarioAuthoringPresentationBuilder "ActionJournalVanillaCategoryPrefix" "Timeline/Events authoring must expose vanilla policy toggles."
Assert-Contains "scenario journal timeline" $scenarioTimelineBuilder "ScenarioTimelineEntryKind\.Journal.*Journal\.Entries.*ActionJournalEntryDeletePrefix" "timeline must surface due-time journal entries with the journal domain."

Assert-Contains "scenario random-window policy" $scenarioSchedulePolicy "WindowEndDay.*Chance.*JitterMinutes.*MaxRuns" "scheduled actions must retain window end, chance, jitter, and max-runs policy fields."
Assert-Contains "scenario random-window policy" $scenarioSerializer "windowEndDay.*chance.*jitterMinutes.*maxRuns" "schedule policy XML must round-trip the random-window attributes additively."
Assert-Contains "scenario random-window runtime" $scenarioSchedulePolicyEvaluator "MaxRuns.*Chance.*JitterMinutes.*WindowEndDay" "schedule policy evaluator must enforce the random-window fields."
Assert-Contains "scenario random-window runtime" $scenarioScheduleRuntimeCoordinator "ScenarioExecutedActionStatus\.Skipped.*ScenarioSchedulePolicyEvaluator\.Evaluate" "schedule coordinator must use the evaluator and journal chance/window skips explicitly."
Assert-Contains "scenario random-window verification" $scenarioVerification "VerifySchedulePolicyWindows.*WindowEndDay.*Chance.*MaxRuns" "framework verification must cover window, chance, and maxRuns math."
Assert-Contains "scenario world event contract" $scenarioDefinitionModel "ScenarioVanillaSuppressionDefinition.*RandomVisitors.*Binman.*Raids.*StasisVisitors.*RadioBroadcastOdds" "scenario definitions must carry additive vanilla world-event suppression flags."
Assert-Contains "scenario world event XML" $scenarioSerializer "ReadVanillaSuppression.*WriteVanillaSuppression.*randomVisitors.*binman.*raids.*stasisVisitors.*radioBroadcastOdds" "vanilla world-event suppression flags must round-trip through XML."
Assert-Contains "scenario world event validation" $schedulingValidationRule "ScenarioEffectKind\.WorldEvent.*ValidateWorldEvent.*NpcVisit.*Raid.*Broadcast" "scheduled WorldEvent effects must validate known event types and value ranges."
Assert-Contains "scenario world event runtime" $scheduledWorldEventRuntime "NpcVisitManager.*m_pendingSpawns.*StartBreach.*Obj_Radio.*StartBroadcastingForTraders.*StartBroadcastingForRecruits" "WorldEvent runtime must bridge NPC visits, raids, and broadcast control through the researched vanilla seams."
Assert-Contains "scenario world event runtime" $scenarioServiceCollectionExtensions "ScheduledWorldEventRuntimeService.*ScenarioEffectDispatcher" "WorldEvent runtime service must be registered with the scenario effect dispatcher."
Assert-Contains "scenario world event suppression" $scenarioWorldEventRuntimeState "SuppressRandomVisitors.*SuppressBinman.*SuppressRaids.*SuppressStasisVisitors.*SuppressRadioBroadcastOdds.*IsDispatchingAuthoredRadioBroadcast" "runtime state must expose each vanilla world-event suppression category and the authored radio dispatch guard."
Assert-Contains "authored visitor priority scope" $scenarioScheduleRuntimeCoordinator 'RefreshAuthoredVisitorPriority.*EvaluateSchedule.*ScenarioSchedulePolicyDecision\.Due.*IsGateSatisfied.*AreConditionsSatisfied.*SetAuthoredVisitorPriority' "only due, ready, pending authored actions may claim transient visitor priority."
Assert-Contains "authored visitor classification" $scenarioScheduleRuntimeCoordinator 'ContainsAuthoredVisitorEffect.*ScenarioEffectKind\.WorldEvent.*eventType.*NpcVisit' "transient visitor priority must classify only authored NpcVisit world-event effects."
Assert-Contains "authored visitor priority seam" $scenarioWorldEventRuntimeState '_authoredVisitorPriority.*SuppressRandomVisitors.*SetAuthoredVisitorPriority' "transient authored visitor priority must reuse the narrow random-visitor suppression seam."
Assert-Contains "scenario world event suppression" $scenarioWorldEventPatches "UpdateSurvivial.*UpdateStasis.*UpdateBinManSpawn.*UpdateManager.*StartBroadcastingForTraders.*StartBroadcastingForRecruits" "Harmony suppression must patch visitor, binman, breach, and radio broadcast seams."
Assert-Contains "scenario world event timeline" $scenarioTimelineBuilder "ScenarioEffectKind\.WorldEvent.*World event" "timeline must label scheduled WorldEvent actions generically."
Assert-Contains "scenario world event timeline" $scenarioPublishAuthoringContentBuilder "BuildWorldEventLabel.*world_event.*WEV" "publish timeline chips must give WorldEvent actions a distinct domain and glyph."
Assert-Contains "station upgrade property contract" $scenarioStationUpgradeService "UpgradePropertyPrefix\s*=\s*""upgrade\.""[\s\S]*StatPropertyPrefix\s*=\s*""stat\.""" "Station tiers must keep the ObjectPlacement CustomProperties contract shape."
Assert-Contains "station upgrade runtime apply" $scenarioBunkerApplyService "ScenarioObjectStatePropertyService\.Apply\(spawned,\s*placement\).*ScenarioStationUpgradePropertyService\.Apply\(spawned,\s*placement,\s*result\)" "Bunker object apply must route authored station level, upgrade paths, and stat overrides through the station applicator."
Assert-Contains "station upgrade capture" $scenarioBunkerDraftService "ScenarioObjectStatePropertyService\.Capture\(obj,\s*placement\).*ScenarioStationUpgradePropertyService\.Capture\(obj,\s*placement\)" "Live object placement capture must record station UpgradeObject paths and verified station stats."
Assert-Contains "station upgrade safe stats" $scenarioStationUpgradeService "StatFuelCapacity.*StatPowerOutput.*StatOutputRate.*StatWaterCapacity.*StatWaterGeneration.*StatOxygenMultiplier" "Station stat overrides must stay limited to verified post-apply reads."
Assert-Contains "station upgrade water capacity registration" $scenarioStationUpgradeService "UnRegisterStorage\(tank\).*SetField\(tank,\s*WaterTankCapacityField,\s*capacity\).*RegisterStorage\(tank\)" "Water tank capacity overrides must update WaterManager registration idempotently."
Assert-Contains "station upgrade editor UI" $scenarioAuthoringPresentationBuilder "BuildStationUpgradeSection.*ActionStationLevelPrefix.*ActionStationUpgradePrefix.*BuildStationAdvancedSection.*ActionStationStatPrefix" "World inspector must expose station level/path steppers and advanced stat override controls."
Assert-Contains "station upgrade commands" $scenarioAuthoringCommandHandlers "StationUpgradeCommandHandler.*TrySetObjectLevel.*TrySetUpgradeLevel.*TrySetStat" "Station editor controls must mutate placement CustomProperties and the selected live object."
Assert-Contains "station XML property round-trip" $scenarioSerializer "ReadProperties\(Child\(placementElement,\s*""CustomProperties""\),\s*placement\.CustomProperties\).*WriteProperties\(writer,\s*""CustomProperties"",\s*placement\.CustomProperties\)" "ObjectPlacement CustomProperties must continue to persist station level, upgrade.*, and stat.* overrides."

Assert-Contains "foreground-free placement" $scenarioBuildPlacement "ActionBuildPlacementCommitGridPrefix.*CommitPlacementAtGridCell.*ScenarioGridSnapService\.GetCellCenterWorldPosition.*TryCompletePlacement" "semantic placement must commit an armed asset-browser placement at an explicit grid cell through the normal validation and draft-recording path."

Assert-Contains "scenario conversation contract" $scenarioDefinitionModel "ScenarioConversationAuthoringDefinition.*ScenarioConversationSuppressionDefinition.*SuppressVanillaRandomChatter.*SuppressedVanillaCategories.*SuppressedVanillaTopicKeys" "ScenarioDefinition must expose authored conversations and vanilla chatter suppression settings."
Assert-Contains "scenario conversation contract" $scenarioDefinitionModel "ScenarioConversationDefinition.*ScenarioConversationTriggerDefinition.*ScenarioConversationParticipantDefinition.*ScenarioConversationLineDefinition" "conversation definitions must include trigger, participants, conditions, lines, and tags."
Assert-Contains "scenario conversation XML round-trip" $scenarioSerializer "definition\.Conversations\s*=\s*ReadConversations\(Child\(root,\s*""Conversations""\)\).*WriteConversations\(writer,\s*definition\.Conversations\)" "serializer must read and write the <Conversations> section."
Assert-Contains "scenario conversation XML round-trip" $scenarioSerializer "ReadConversationParticipants.*ReadConversationLines.*WriteConversationParticipants.*WriteConversationLines" "conversation participants and lines must round-trip through XML."
Assert-Contains "scenario conversation effect" $scenarioEffectKind "StartConversation" "ScenarioEffectKind must include StartConversation."
Assert-Contains "scenario conversation effect" $scenarioEffectDefinitionModel "public string ConversationId" "effects must carry a conversationId for StartConversation."
Assert-Contains "scenario conversation runtime" $scenarioConversationRuntime "ScenarioEffectKind\.StartConversation.*FamilyMember.*Say\(ResolveText\(line\)\).*isInCoversation" "runtime must dispatch StartConversation into FamilyMember speech bubbles and conversation flags."
Assert-Contains "scenario conversation diagnostic" $scenarioConversationRuntime "(?=.*unresolvedRequired)(?=.*resolverObservations)(?=.*DescribeFamilyResolutionState)(?=.*RetryPending)" "participant resolution failures must identify unresolved slots, report observed runtime state, and record a retry diagnostic."
Assert-Contains "scenario conversation retry classification" $scenarioConversationRuntime "IScenarioRetryableEffectHandler.*retryable = true.*RecordParticipantResolutionFailure" "participant resolution failures must be explicitly classified as retryable."
Assert-Contains "scenario conversation retry consumption" $scenarioScheduleRuntimeCoordinator "ShouldJournalEffectFailure\(retryableFailure\).*return !retryableFailure.*ShouldLogRetryableFailure" "retryable effect failures must remain out of the execution journal and throttle warnings."
Assert-Contains "scenario once-only skip diagnostic" $scenarioScheduleRuntimeCoordinator "_onceConsumptionLogged\.Add\(action\.Id\).*MMLog\.WriteDebug" "the in-memory once-only skip path must emit one debug diagnostic."
Assert-Contains "scenario conversation random pool" $scenarioConversationRuntime "TryHandleRandomComment.*Weight.*Once.*CooldownDays" "runtime must select random-pool conversations by weight and cooldown/once policy."
Assert-Contains "scenario conversation suppression" $scenarioConversationRuntime "SuppressVanillaRandomChatter.*SuppressedVanillaTopicKeys.*SuppressedVanillaCategories" "runtime must enforce whole/category suppression and retain topic-key policy."
Assert-Contains "scenario conversation suppression" $scenarioConversationPatches "HarmonyPatch\(typeof\(FamilyMember\),\s*""SayRandomComment""\).*TryHandleRandomComment" "random conversation injection must patch FamilyMember.SayRandomComment."
Assert-Contains "scenario conversation suppression" $scenarioConversationPatches "SayRandomCommentPrefix.*TryHandleRandomComment.*__result\s*=\s*string\.Empty" "authored conversations must consume the vanilla idle slot without returning runtime status text as a speech bubble."
Assert-Contains "scenario conversation suppression" $scenarioConversationPatches "GetRandomBantzSpeech.*ShouldSuppressGenericBantz.*GetRandomIllnessSpeech.*ShouldSuppressIllness" "vanilla category suppression must patch generic bantz and illness speech groups."
Assert-Contains "scenario conversation scheduling" $scenarioConversationProvider "ScenarioConversationTriggerSource\.Event.*ScenarioConversationTriggerSource\.Timeline.*ScenarioEffectKind\.StartConversation" "event/timeline conversations must compile to StartConversation scheduled actions."
Assert-Contains "scenario conversation registration" $scenarioServiceCollectionExtensions "ScenarioConversationRuntimeService.*dispatcher\.Register\(resolver\.Get<ScenarioConversationRuntimeService>\(\)\).*ScenarioConversationScheduledActionProvider" "conversation runtime and schedule provider must be registered."
Assert-Contains "scenario conversation validation" $scenarioConversationValidation "dangling|missing story character|empty line|speaker slot|SuppressedVanillaTopicKeys" "validation must flag participant/line issues and document topic-key suppression limits."
Assert-Contains "scenario conversation UI" $scenarioQuestAuthoringContentBuilder "AppendConversationSections.*Suppress Vanilla Random.*Run Preview.*ScenarioCastMemberPickerBuilder\.BuildSection" "Story workshop must expose conversations, suppression settings, actor picking, and preview."

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
Assert-Contains "survivor condition contract" $scenarioDefinitionModel "class FamilyMemberConfig.*Conditions = new FamilyMemberConditionConfig\(\).*public FamilyMemberConditionConfig Conditions" "Starting and future survivor configs must retain authored starting condition overrides."
Assert-Contains "survivor condition contract" $scenarioDefinitionModel "class FamilyMemberConditionConfig.*public int\? Hunger.*public int\? Thirst.*public int\? Fatigue.*public int\? Dirtiness.*public int\? Toilet.*public int\? Stress" "Authored survivor conditions must cover the runtime BehaviourStat fields that can be safely applied."
Assert-Contains "survivor condition XML" $familyScenarioSerializer "ReadFamilyConditions\(ScenarioXmlSerializerUtil\.Child\(memberElement,\s*""Condition""\)\).*ReadFamilyConditions\(XmlElement element\).*""hunger"".*""thirst"".*""fatigue"".*""dirtiness"".*""toilet"".*""stress""" "Family XML must deserialize starting condition overrides from the dedicated Condition element."
Assert-Contains "survivor condition XML" $familyScenarioSerializer "WriteFamilyConditions\(.*WriteStartElement\(""Condition""\).*""hunger"".*""thirst"".*""fatigue"".*""dirtiness"".*""toilet"".*""stress""" "Family XML must serialize starting condition overrides for round-trip persistence."
Assert-Contains "survivor stat authoring" $scenarioFamilyMemberFactory "public const int StatMin = 1.*public const int StatMax = 20.*ClampStat" "Survivor stat editing must use the vanilla family authoring range of 1-20."
Assert-Contains "survivor stat authoring" $scenarioCharacterEditorAuthoringService "stat_set\..*ScenarioFamilyMemberFactory\.ClampStat" "Survivor stat rows must support direct numeric entry through the character editor service."
Assert-Contains "survivor trait picker" $scenarioAuthoringContracts "class ScenarioSurvivorTraitRowViewModel.*PickerKey.*PreviousAction.*NextAction.*PickerAction.*Options" "Survivor trait rows must expose picker metadata plus previous/next actions."
Assert-Contains "survivor trait picker" $scenarioAuthoringPresentationBuilder "BuildTraitOptions\(.*ScenarioSurvivorTraitConflictRules\.ConflictsWithSelection.*GetTraitDescription" "Survivor trait picker options must include effect descriptions and block paired vanilla conflicts through the shared rule source."
Assert-Contains "survivor condition runtime" $scenarioFamilyMemberFactory "ApplyConditions\(FamilyMember member,\s*FamilyMemberConfig config\).*member\.stats\.hunger.*member\.stats\.thirst.*member\.stats\.fatigue.*member\.stats\.dirtiness.*member\.stats\.toilet.*member\.stats\.stress" "Runtime survivor materialization must apply authored BehaviourStat condition values."
Assert-Contains "survivor condition runtime" $familyApplyService "ScenarioFamilyMemberFactory\.ApplyConditions\(member,\s*config\)" "Existing-family apply must route authored condition values through the shared runtime helper."
Assert-Contains "survivor condition runtime" $scenarioFutureSurvivorRecruitBindingService "ScenarioFamilyMemberFactory\.ApplyConditions\(member,\s*pending\.Survivor != null \? pending\.Survivor\.Survivor : null\)" "Accepted ask-to-join recruits must receive authored starting condition values."
Assert-Contains "survivor condition editor UI" $scenarioAuthoringPresentationBuilder "BuildSurvivorConditionRows\(.*ConditionIds.*condition_set" "Focused survivor editor view models must expose editable starting condition rows."
Assert-Contains "survivor condition editor UI" $scenarioAuthoringWindowRenderer "DrawSurvivorConditionRow.*DrawSurvivorInlineTextField.*DrawSurvivorTraitPickerPopup" "Focused survivor editor renderer must draw condition direct-entry fields and the trait picker popup."
Assert-Contains "survivor randomize declared scope" $scenarioSurvivorAuthoringOperations 'RandomizeDisclosure = "Randomizes: name, gender, age, appearance, stats, traits\. Keeps: story links, arrival settings, starting condition, skills, actor identity\."' "Randomize must tell creators exactly what changes and what remains linked."
Assert-Contains "survivor randomize declared scope" $scenarioSurvivorAuthoringOperations "RandomizeDeclaredFields\(FamilyMemberConfig member\)(?:(?!member\.Conditions|member\.Skills|member\.ActorRef).)*member\.Gender.*member\.ExactAge.*member\.Name.*member\.Stats.*member\.Traits.*RandomizeAppearance\(member\)" "Randomize must touch only name, gender, age, appearance, stats, and traits, preserving condition, skills, and actor identity."
Assert-Contains "survivor bulk action undo" $scenarioCharacterEditorAuthoringService 'RecordFamilyUndo\(session, "Randomize survivor"\).*RandomizeDeclaredFields' "Randomize must record a family history snapshot before mutation."
Assert-Contains "survivor bulk action undo" $scenarioCharacterEditorAuthoringService 'RecordFamilyUndo\(session, "Duplicate starting survivor"\).*DuplicateMember' "Duplicate must record a family history snapshot before mutation."
Assert-Contains "survivor duplicate fresh actor" $scenarioSurvivorAuthoringOperations "DuplicateMember\(FamilyMemberConfig source\).*copy\.ActorRef = null.*DuplicateFutureSurvivor.*copy\.ActorRef = null.*copy\.Survivor\.ActorRef = null" "Duplicate must never retain the source member or future-survivor actor reference."
Assert-Contains "survivor duplicate fresh actor" $scenarioCharacterEditorAuthoringService "DuplicateFutureSurvivor\(survivor, survivors\).*EnsureFutureSurvivorRef\(session\.WorkingDefinition, duplicate, duplicateIndex\).*DuplicateMember\(config\).*EnsureStartingMemberRef\(session\.WorkingDefinition, duplicate, duplicateIndex\)" "Every duplicate must receive a newly resolved future or starting actor identity."
Assert-Contains "survivor skills honesty" ($scenarioAuthoringPresentationBuilder + $scenarioAuthoringWindowRenderer) "Skills can't be authored yet - the game doesn't expose a stable way to save them\. Strengths and weaknesses below DO work\..*SkillsLimitationText" "The stats panel must plainly disclose that skill authoring is intentionally unavailable."
Assert-Contains "survivor trait conflict source" $scenarioSurvivorTraitConflictRules "internal static class ScenarioSurvivorTraitConflictRules.*ConflictsWithSelection.*HasConflict" "Trait conflicts must have one shared selection and validation policy."
Assert-Contains "survivor trait picker validator agreement" $scenarioAuthoringPresentationBuilder "ScenarioSurvivorTraitConflictRules\.ConflictsWithSelection" "The trait picker must consume the shared conflict policy."
Assert-Contains "survivor trait picker validator agreement" $scenarioValidator "ScenarioSurvivorTraitConflictRules\.HasConflict" "Validation must consume the same conflict policy as the picker."
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
Assert-Contains "scenario actor editor catalog" $scenarioCastMemberReferenceCatalog "Build\(ScenarioDefinition definition,\s*bool includeStarting,\s*bool includeFuture\).*family\.Members.*family\.FutureSurvivors" "G5 editor actor picker catalog must include both starting and future survivors."
Assert-Contains "scenario actor editor picker" $scenarioCastMemberPickerBuilder "ScenarioAuthoringInspectorSectionLayout\.CastCardGrid.*CompactReference\s*=\s*true.*ScenarioCastPortraitResolver" "G5 cast-member picker must reuse compact cast cards with portraits."
Assert-Contains "scenario actor editor picker" $scenarioAuthoringPresentationBuilder "ActionGateConditionActorPrefix.*ActionScheduledActionEffectActorPrefix" "Focused event editors must expose actor picker sections for survivor conditions and spawn effects."
Assert-Contains "scenario actor editor storage" $scenarioEventAuthoringService "ApplyConditionActorTarget.*condition\.ActorRef\s*=\s*ScenarioCastMemberReferenceCatalog\.CopyActorRef.*condition\.TargetId\s*=\s*candidate\.LegacyTargetId" "Picking a survivor condition cast member must store actor ref and preserve legacy target id."
Assert-Contains "scenario actor editor storage" $scenarioEventAuthoringService "ApplyEffectActorTarget.*effect\.ActorRef\s*=\s*ScenarioCastMemberReferenceCatalog\.CopyActorRef.*effect\.TargetId\s*=\s*candidate\.LegacyTargetId.*effect\.SurvivorId\s*=\s*candidate\.LegacyTargetId" "Picking a future survivor effect cast member must store actor ref and preserve legacy survivor id."
Assert-Contains "scenario actor story link" $scenarioStoryAuthoringService "ActionStoryCharacterActorPrefix.*ScenarioCharacters\[stageIndex\]\.ActorRef\s*=\s*ScenarioCastMemberReferenceCatalog\.CopyActorRef" "Story character actor linking must set only ScenarioNpcDefinition.ActorRef."
Assert-Contains "scenario actor story link" $scenarioStoryCharacterActorLinkSectionBuilder "Advanced: internal id.*It never changes.*ActionStoryCharacterActorClearPrefix.*ScenarioCastMemberPickerBuilder\.BuildSection" "Story actor link UI must be explicit, clearable, and keep CharacterId flow intact."
Assert-Contains "scenario actor story labels" $scenarioStoryFocusedEditorDocumentBuilder "FormatCharacterLabel\(definition,\s*id\)" "Focused story editor must display actor-backed labels while preserving character ids as action tokens."
Assert-Contains "scenario actor story labels" $scenarioQuestAuthoringContentBuilder "FormatCharacterLabel\(definition,\s*id\)" "Quest/story overview must display actor-backed labels while preserving character ids as action tokens."
Assert-Contains "focused timeline editor grouping" $scenarioAuthoringPresentationBuilder "focused_trigger_header.*focused_trigger_what.*focused_trigger_when.*focused_action_header.*focused_action_when.*focused_action_what.*focused_action_conditions.*focused_action_advanced" "Trigger and scheduled-action focused editors must present summary-first WHEN/WHAT/CONDITIONS/ADVANCED groups."
Assert-Contains "focused scheduled action summary" $scenarioAuthoringPresentationBuilder "ScenarioTimelineCreatorText\.ScheduledActionName.*No effects yet - this entry does nothing when it fires" "Scheduled actions must reuse creator-language summaries and explain an empty effect list."
Assert-Contains "focused world event grouping" $scenarioWorldEventFocusedEditorDocumentBuilder "focused_world_event_header.*focused_world_event_type.*focused_world_event_when.*focused_world_event_conditions.*focused_world_event_advanced.*focused_world_event_footer" "World-event focused editors must present summary-first WHEN/WHAT/CONDITIONS/ADVANCED groups and one footer."
Assert-Contains "focused story editor grouping" $scenarioStoryFocusedEditorDocumentBuilder "AppendUsages\(items,\s*definition,\s*ScenarioReferenceTargetKind\.Stage.*AppendUsages\(items,\s*definition,\s*ScenarioReferenceTargetKind\.IntercomStep.*WHAT / DIALOGUE & CHOICES.*CONDITIONS.*ADVANCED / ROUTING.*WHAT / OUTCOMES" "Stage and intercom focused editors must reuse the reference index and group dialogue, conditions, routing, and outcomes."
Assert-Contains "focused conversation grouping" $scenarioQuestAuthoringContentBuilder "(?=.*FormatConversationSummary)(?=.*story_conversation_when_)(?=.*WHAT / PARTICIPANTS)(?=.*WHAT / SCRIPT)(?=.*story_conversation_advanced_)(?=.*story_conversation_footer_)" "Conversation editors must present a plain-language summary, WHEN/WHAT/ADVANCED groups, and one action footer."
Assert-Contains "scenario actor timeline labels" $scenarioTimelineBuilder "ResolveDisplayName\(definition,\s*actorRef,\s*false,\s*true.*ActionFutureSurvivorEditorOpenPrefix" "Timeline survivor entries must use actor-backed names and deep-link future survivors to the cast editor."
Assert-Contains "scenario actor timeline labels" $scenarioPublishAuthoringContentBuilder "effect\.ActorRef.*ScenarioCastMemberReferenceCatalog\.ResolveDisplayName" "Publish timeline chips must prefer actor-backed future survivor names."
Assert-Contains "scenario actor validation" $gateConditionValidationRule "people\.condition\.deleted_actor.*pick an existing cast member" "Gate validation must report deleted cast member actor refs with an actionable fix."
Assert-Contains "scenario actor validation" $schedulingValidationRule "people\.effect\.deleted_actor.*pick an existing future survivor" "Scheduled effect validation must report deleted future-survivor actor refs with an actionable fix."
Assert-Contains "actor authoring capability contract" $actorAuthoringCapabilities "enum ActorAuthoringFieldValueType.*Bool.*Int.*Float.*String.*StringEnum.*Color" "G6 provider contract must keep the supported authoring value types narrow."
Assert-Contains "actor authoring capability contract" $actorAuthoringCapabilities "interface IActorAuthoringCapabilityProvider.*ProviderId.*ProviderModId.*GetFields" "G6 providers must declare actor-authoring fields through a narrow provider contract."
Assert-Contains "actor authoring capability contract" $actorAuthoringCapabilities "class ActorAuthoringFieldDefinition.*public string Id.*public string Label.*public ActorAuthoringFieldValueType ValueType.*public string ComponentId.*public ActorKind\[\] ApplicableActorKinds.*public string RequiredModId.*public string HelpText" "G6 field definitions must expose id, label, value type, component, applicable actor kinds, required mod id, and help text."
Assert-Contains "actor authoring capability registry" $actorAuthoringCapabilities "interface IActorAuthoringCapabilityRegistry.*RegisterProvider\(IActorAuthoringCapabilityProvider provider\).*GetFields\(ActorKind actorKind\)" "G6 registration must stay actor-authoring-specific, not a generic capability registry."
Assert-Contains "actor authoring capability registry" $apiIds "ActorAuthoringCapabilities = ""GameRuntime\.ActorAuthoringCapabilities""" "G6 registry must have a well-known ModAPI registry id."
Assert-Contains "actor authoring capability registry" $bootstrap "new ScenarioActorAuthoringCapabilityRegistry\(\).*RegisterApi\(GameRuntimeApiIds\.ActorAuthoringCapabilities" "ShelteredAPI must register the actor-authoring registry through the existing ModAPIRegistry pattern."
Assert-Contains "actor authoring provider registration" $scenarioActorAuthoringRegistry "_providers\[provider\.ProviderId\] = provider.*GetFields\(ActorKind actorKind\)" "G6 registry must surface registered provider fields by actor kind."
Assert-Contains "actor authoring example provider" $scenarioDevActorAuthoringProvider "(?=.*ScenarioDevActorAuthoringCapabilityProvider)(?=.*ActorAuthoringFieldValueType\.Bool)(?=.*ActorAuthoringFieldValueType\.Int)(?=.*ActorAuthoringFieldValueType\.Float)(?=.*ActorAuthoringFieldValueType\.String)(?=.*ActorAuthoringFieldValueType\.StringEnum)(?=.*ActorAuthoringFieldValueType\.Color)" "A dev-gated provider must prove the full supported value-type set."
Assert-Contains "actor authoring payload round-trip" $scenarioActorAuthoringStore "SetValue\(FamilyMemberConfig member,\s*ActorAuthoringFieldDefinition field,\s*string value\).*ScenarioActorComponentDefinition.*component\.PayloadJson = WriteField" "G6 field writes must round-trip through ScenarioActorComponentDefinition payload envelopes."
Assert-Contains "actor authoring deterministic payload" $scenarioActorAuthoringStore "keys\.Sort\(StringComparer\.Ordinal\).*ManualJson\.Serialize\(next,\s*false\)" "G6 key/value payload JSON must be deterministic."
Assert-Contains "actor authoring editor UI" $scenarioAuthoringContracts "ModFieldList.*ScenarioSurvivorModFieldControlKind.*Toggle.*Stepper.*Text.*Enum.*Color" "Survivor editor contracts must expose the expected mod-field control kinds."
Assert-Contains "actor authoring editor UI" $scenarioAuthoringPresentationBuilder "BuildSurvivorModFieldsSection\(member,\s*actionPrefix,\s*index\).*Title = ""Mod Fields"".*ScenarioAuthoringInspectorSectionLayout\.ModFieldList" "Focused survivor editor must add a Mod Fields section only when provider fields or gated payload notices exist."
Assert-Contains "actor authoring editor UI" ($scenarioAuthoringWindowRenderer + $scenarioAuthoringShellRenderer) "DrawModFieldRow.*DrawEditableProperty.*DrawColorPreview.*ExecuteAction\(row\.ColorRow\.OpenColorPickerActionId\).*SurvivorColorPickerRequestId.*OpenSurvivorColorPicker" "Mod fields must render text controls and route color swatches through the semantic action path before opening the ModAPI color picker."
Assert-Contains "actor authoring commands" $scenarioCharacterEditorAuthoringService "ScenarioActorAuthoringFieldStore\.FieldCommandPrefix.*HandleModFieldCommand.*ScenarioActorAuthoringFieldStore\.SetValue" "Mod field controls must mutate actor component payloads through the shared field store."
Assert-Contains "actor authoring missing provider gate" $scenarioAuthoringPresentationBuilder "Missing provider:.*Payload for.*is preserved but hidden until that mod/API is registered" "Missing provider payloads must be preserved and shown as gated notices instead of editable fields."
Assert-Contains "actor authoring dependency detection" $scenarioModReferenceReason "ActorAuthoringComponent" "Dependency reports must have a reason for mod-owned actor authoring components."
Assert-Contains "actor authoring dependency detection" $scenarioModDependencyDetector "AddActorComponents\(.*ScenarioModReferenceReason\.ActorAuthoringComponent" "ScenarioModDependencyDetector must record provider mod IDs referenced by authored actor components."
Assert-Contains "scenario survivor actor runtime" $scheduledSurvivorRuntimeService "BindFutureSurvivor\(definition,\s*survivor,\s*spawned\)" "Future survivor immediate materialization must bind spawned FamilyMember identities."
Assert-Contains "scenario survivor ask-to-join runtime" $scheduledSurvivorRuntimeService "_recruitBindingService\.ScheduleAskToJoin\(definition,\s*survivor,\s*0f,\s*out message\)" "Future survivor ask-to-join scheduling must route through the recruit binding service."
Assert-Contains "scenario survivor ask-to-join runtime" $scenarioFamilyMemberFactory "ScheduleRecruit\(.*out FamilySpawner\.CharacterAttributes queuedAttributes.*CreateAttributes\(config\).*attributes\.Add\(queuedAttributes\)" "Recruit scheduling must expose the exact queued vanilla attributes object for visitor correlation."
Assert-Contains "scenario survivor ask-to-join runtime" $scenarioFutureSurvivorRecruitBindingService "object\.ReferenceEquals\(pending\.QueuedAttributes,\s*queuedAttributes\)" "Ask-to-join visitor correlation must match the exact queued attributes object."
Assert-Contains "scenario survivor ask-to-join runtime" $scenarioFutureSurvivorRecruitBindingService "_actorResolver\.BindMaterializedFamilyMember\(.*pending\.Definition,\s*actorRef,\s*member" "Accepted ask-to-join visitors must bind the recruited FamilyMember to the scenario actor."
Assert-Contains "scenario survivor ask-to-join runtime" $scenarioFutureSurvivorRecruitBindingService "_actorResolver\.BindMaterializedFamilyMember\(.*pending\.Survivor\.Survivor,\s*pending\.Survivor\.ActorComponents,\s*out bindMessage\)" "Accepted ask-to-join visitors must hydrate authored survivor components onto the live FamilyMember actor."
Assert-Contains "scenario survivor actor runtime" $scenarioActorResolver "BindMaterializedFamilyMember\(.*FamilyMemberConfig authoredConfig.*HydrateActor\(definition,\s*familyActor\.Id,\s*actorRef,\s*authoredConfig,\s*components,\s*null\).*_actors\.Destroy\(requestedId,\s*ActorDestroyReason\.Replaced\)" "Materialized scenario-owned synthetic actors must transfer authored state to the live family actor and be removed as replaced."
Assert-Contains "scenario survivor ask-to-join runtime" $shelteredCustomScenarioPatches "HarmonyPatch\(typeof\(NpcVisitManager\),\s*""CreateNpcVisitor""\).*OnVisitorCreated" "Scenario patches must observe vanilla joiner visitor creation for queued-attribute correlation."
Assert-Contains "scenario survivor ask-to-join runtime" $shelteredCustomScenarioPatches "HarmonyPatch\(typeof\(FamilyManager\),\s*""AdoptNpc""\).*OnNpcAdopted" "Scenario patches must bind accepted joiner visitors after FamilyManager.AdoptNpc creates the FamilyMember."
Assert-Contains "scenario survivor ask-to-join runtime" $shelteredCustomScenarioPatches "HarmonyPatch\(typeof\(NpcVisitManager\),\s*""OnNpcFinished""\).*OnVisitorFinished" "Scenario patches must clear rejected or departed scenario visitors without double-binding."

Assert-Contains "authoring inventory write-through" $scenarioInventoryApplyService "PlanProjectionDeltas\(.*newQuantity - oldQuantity" "Inventory authoring must reconcile by authored-vs-last-projected deltas."
Assert-Contains "authoring inventory write-through" $scenarioInventoryApplyService "BuildProjectionSeed\(.*Math\.Min\(pair\.Value,\s*liveQuantity\)" "Reload/base-switch projection must seed from live storage without claiming extra live-only items."
Assert-Contains "authoring inventory write-through" $scenarioInventoryApplyService "ShouldApplyRandomStartOverride\(.*OverrideRandomStart" "OverrideRandomStart must remain centralized in the inventory apply path."
Assert-Contains "authoring inventory write-through" $scenarioInventoryApplyService "ShouldUseAuthoringProjection\(definition\).*ScenarioAuthoringRuntimeGuards\.IsAuthoringActive" "Playtest apply must reuse the authoring projection baseline only while the draft editor is active."
Assert-Contains "authoring inventory live-truth" $scenarioInventoryApplyService "ReconcileAuthoringLiveTruth\(.*SnapshotsEqual\(previous,\s*authored\).*BuildLiveInventorySnapshot" "Authoring inventory reconciliation must adopt native shelter storage into the draft when live storage changes."
Assert-Contains "authoring inventory live-truth" $scenarioInventoryApplyService "ReplaceStartingInventory\(definition\.StartingInventory,\s*live\)" "Live shelter storage changes must replace the authored starting inventory list."
Assert-Contains "authoring inventory live-truth" $scenarioInventoryApplyService "DraftUpdated\s*=\s*true" "Reverse reconciliation must report draft updates so the editor can mark inventory dirty."
Assert-Contains "authoring inventory live-truth" $scenarioInventoryApplyService "SnapshotsEqual\(IDictionary<string,\s*int> left,\s*IDictionary<string,\s*int> right\)" "Live-truth reconciliation must have a no-op equality guard to prevent feedback loops."
Assert-Contains "authoring inventory write-through" $scenarioAuthoringInventoryProjectionService "IsPlaytesting\(\).*playtest owns the live apply pipeline" "Authoring write-through must be skipped while playtest owns apply."
Assert-Contains "authoring inventory live-truth" $scenarioAuthoringInventoryProjectionService "LiveTruthPollSeconds\s*=\s*1f.*TryReconcileLiveTruth" "Authoring inventory must poll native storage on a modest cadence."
Assert-Contains "authoring inventory live-truth" $scenarioAuthoringInventoryProjectionService "MarkDraftChanged\(ScenarioDirtySection\.Inventory,\s*ScenarioEditCategory\.Inventory\)" "Reverse reconciliation must mark the scenario draft dirty."
Assert-Contains "authoring inventory write-through" $scenarioGameplayScheduleAuthoringService "FinishStartingInventoryMutation\(session.*TryProject" "Starting inventory edit actions must write through to native shelter storage."
Assert-NotContains "authoring inventory live-truth" $scenarioAuthoringCommandHandlers "ActionCaptureInventory|ActionCaptureInventoryConfirm|FocusedKindCaptureInventory" "Explicit inventory capture actions must stay retired."
Assert-Contains "authoring inventory write-through" $scenarioBaseModeReloadService "QueueSavedDraftReload" "Base-mode changes must continue through the saved draft reload path."
Assert-Contains "authoring inventory write-through" $scenarioAuthoringBootstrapService "ResetForCurrentWorld\(editorSession\).*TryProject\(editorSession,\s*""authoring bootstrap""" "Draft reload/base-switch bootstrap must re-project authored starting inventory."
Assert-Contains "authoring inventory write-through" $scenarioAuthoringBootstrapService "UpdateLiveTruth\(_editorService\.CurrentSession\)" "Authoring bootstrap must keep live shelter storage and the draft converged."
Assert-Contains "authoring inventory contracts" $scenarioVerification "VerifyInventoryProjectionReconciliation.*liveAdd.*liveRemove.*SnapshotsEqual.*ShouldApplyRandomStartOverride" "Scenario verification must cover reverse live add/remove, loop prevention, and OverrideRandomStart behavior."
Assert-Contains "map loot projection contracts" $scenarioVerification "VerifyMapLootProjectionContracts.*PlanLootRolls.*BuildLootRollSignature.*replaceGeneratedLoot without a loot table.*VisibleAtStart and HiddenUntilDiscovered" "Scenario verification must cover deterministic map loot rolls, replace semantics validation, and contradictory map flags."
Assert-Contains "seam guard contracts" $seamGuard "enum SeamRecoveryPolicy.*RetryOnce.*DisableSeamAndDegrade.*RestoreState" "SeamGuard must expose the required recovery policies."
Assert-Contains "seam guard contracts" $seamGuard "class SeamHealthSnapshot.*Name.*LastSuccess.*FailureCount.*LastError.*Degraded.*Disabled.*LastPlayerMessage" "SeamGuard health snapshots must expose per-seam health fields."
Assert-Contains "seam guard contracts" $seamGuard "Try<T>.*MarkFailure.*TryRecover.*SetPlayerMessage.*TryExecute<T>.*catch" "SeamGuard must record failure, fire policy recovery, and set an editor-facing message when a wrapped call throws."
Assert-Contains "seam guard contracts" $scenarioVerification "VerifySeamGuardContracts.*throw new InvalidOperationException.*recoveryFired.*FailureCount == 1.*BuildSystemHealthLine" "Scenario verification must cover failure recording, recovery policy firing, and editor health messaging."
Assert-Contains "seam guard adoption" ($scheduledWorldEventRuntime + $scheduledJournalRuntime + $scenarioStationUpgradeService + $scenarioFutureSurvivorRecruitBindingService + $shelteredCustomScenarioPatches + $scenarioOpeningCutsceneAuthoringService + $scenarioAuthoringPresentationBuilder + $scenarioStatusBarViewModelBuilder) "SeamGuard" "High-risk scenario seams and editor health surfaces must adopt SeamGuard."
Assert-Contains "authoring inventory UI" $scenarioAuthoringPresentationBuilder "Open Shelter Storage" "Supplies UI must open the real vanilla Shelter Storage window for starting inventory."
Assert-Contains "authoring inventory UI" $scenarioAuthoringPresentationBuilder "ActionInventoryStorageOpen" "Supplies UI must route starting inventory through the vanilla storage panel action."
Assert-NotContains "authoring inventory UI" $scenarioAuthoringPresentationBuilder "Live Shelter Inventory - Native Storage|Starting Items - Written to Storage|Capture Current Stockpile" "Supplies UI must not expose the old live reference grid or capture button."

Assert-Contains "supplies authored-first layout" $scenarioAuthoringPresentationBuilder "BuildAuthoredFirstSections\(definition\)" "Supplies window must lead with the authored-first sections."
Assert-Contains "supplies authored-first layout" $scenarioAuthoringPresentationBuilder "Id = ""live_shelter_reference"".*Expanded = false" "Supplies live shelter inventory must be a collapsed reference section."
Assert-Contains "supplies authored-first layout" $scenarioSuppliesContentBuilder "authored_starting_items.*starter_loadout_presets.*supplies_balance_check" "Authored-first sections must render starting items, presets, then the balance check."
Assert-Contains "supplies preset preview" $scenarioSuppliesContentBuilder "BuildPresetPreviewDocument.*This preset sets.*ActionSuppliesPresetApplyPrefix.*ActionFocusedEditorCancel" "Preset apply must be previewed with its stacks and an explicit apply/cancel before mutating."
Assert-Contains "supplies preset catalog" $scenarioSuppliesPresetCatalog "PresetScarce.*PresetBalanced.*PresetMedical.*PresetRepair.*PresetEmpty" "Starter loadout catalog must expose the scarce, balanced, medical, repair, and empty presets."
Assert-Contains "supplies preset catalog" $scenarioSuppliesPresetCatalog "GetStableItemId\(stack\.Type\)" "Preset stacks must resolve stable catalog item ids from the vanilla item catalog."
Assert-Contains "supplies normalizer policy" $scenarioSuppliesInventoryNormalizer "entry\.Quantity <= 0.*RemovedStacks\+\+.*existing\.Quantity \+= entry\.Quantity.*MergedStacks\+\+" "Normalizer must drop non-positive stacks and sum duplicate item ids."
Assert-Contains "supplies balance estimator" $scenarioSuppliesBalanceEstimator "WaterPerSurvivorPerDay.*FoodPerSurvivorPerDay.*DefaultSurvivorCount = 4" "Balance estimator must state approximate per-survivor-per-day assumptions and a default cast size."
Assert-Contains "supplies balance estimator" $scenarioSuppliesBalanceEstimator "MissingEssentials\.Add\(""No water""\).*MissingEssentials\.Add\(""No food""\).*MissingEssentials\.Add\(""No first aid""\)" "Balance estimator must flag missing water, food, and first aid essentials."
Assert-Contains "supplies preset write-through" $scenarioGameplayScheduleAuthoringService "ApplyStarterPreset\(session.*RecordAuthoringChange.*Normalize\(inventory\.Items\).*FinishStartingInventoryMutation" "Preset apply must snapshot for undo, normalize, and write through to shelter storage."
Assert-Contains "supplies verification" $scenarioVerification "VerifySuppliesAuthoring.*BuildStacks.*history\.Undo.*Normalize.*Estimate\(fixture, 3\)" "Scenario verification must cover preset stacks, undoability, duplicate merge, and balance math on a fixture."

# BUILDUX: browser personalization is stored in the existing per-user settings document.
Assert-Contains "asset browser persistence round-trip" $scenarioAssetBrowserUx "SerializeList\(IList<string> values\).*Uri\.EscapeDataString.*DeserializeList\(string value\).*Uri\.UnescapeDataString" "Favorites and recents must use a reversible encoding for arbitrary asset action ids."
Assert-Contains "asset browser persistence round-trip" $scenarioAssetBrowserUx "FavoritesKey = ""asset_browser\.favorites"".*RecentKey = ""asset_browser\.recent"".*RecentLimit = 20.*settings\.Save\(state\.Settings\)" "Favorites and capped recents must round-trip through the editor settings store."
Assert-Contains "asset browser contextual defaults" $scenarioAssetBrowserUx "FindSectionForAction\(sections, selectedActionId\).*ScenarioAuthoringTargetKind\.SceneSprite.*ScenarioAuthoringTargetKind\.Wall.*ScenarioAuthoringTargetKind\.PlaceableObject.*ScenarioStageKind\.BunkerBackground.*return RecentFilter" "Browser defaults must prefer the selected asset/target, then stage context, and finally Recent instead of All."
Assert-Contains "asset browser card favorite star" $scenarioAuthoringWindowRenderer "HandleAssetFavoriteStarInput.*ExecuteAction\(.*ActionRendererAssetFavoriteTogglePrefix.*current\.Use\(\).*BuildAssetFavoriteStarRect.*cardRect\.x \+ 5f.*cardRect\.yMax - 29f" "Every browser card must expose a bottom-left favorite star routed through the semantic action path and consumed before card selection or placement."
Assert-NotContains "asset browser card favorite star" $scenarioAssetBrowserRenderer "Add Favorite|Favorited" "The detail-pane favorite button must stay retired so the card star is the only favorite affordance."
Assert-Contains "weather effect preview tint" $scenarioWeatherEffectCatalog "PreviewTint = previewTint.*TryResolveParticleTint.*particles\.startColor.*TryResolveSpriteTint.*renderer\.color.*TryResolveMaterialTint.*_TintColor.*_Color" "Weather previews must derive tint from material color, particle start color, and sprite-renderer color."
Assert-Contains "weather effect preview tint" ($scenarioAssetAuthoringContent + $scenarioAuthoringWindowRenderer) "PreviewTint = target\.PreviewTint.*HasPreviewTint = target\.HasPreviewTint.*DrawSpritePreview\(previewRect, action\.PreviewSprite, action\.Emphasized, action\.HasPreviewTint" "Weather tint metadata must reach asset cards and the shared preview renderer."
Assert-Contains "readable search fields" $scenarioUiStyleSheet "SearchField\s*=\s*BuildField\(accentNeutralCorner, accentActiveCorner, palette\.TextOnLight" "Search inputs must use a light parchment field with dark palette ink and a distinct focused surface."
Assert-Contains "readable search fields" ($scenarioGlobalSearchRenderer + $scenarioAssetBrowserRenderer) "Styles\.SearchField.*DrawSearchPlaceholder.*DrawFieldFocusBorder" "Global and asset browser search fields must share readable placeholder and focus treatment."
Assert-NotContains "ellipsis-free measured labels" $scenarioUiStyleSheet 'FitLabelWithEllipsis|const string ellipsis|"\.\.\."' "The shared measured-label helper must never synthesize truncation dots."

$assetCategoryLabels = @([System.Text.RegularExpressions.Regex]::Matches($scenarioAssetBrowserUx, 'return Label\("([^"]+)"') |
    ForEach-Object { $_.Groups[1].Value })
$duplicateAssetCategoryLabels = @($assetCategoryLabels | Group-Object | Where-Object { $_.Count -gt 1 })
if ($assetCategoryLabels.Count -lt 6 -or $duplicateAssetCategoryLabels.Count -gt 0) {
    $failures.Add("asset browser category labels: short category names must be present and unique; source/subtype belongs in secondary text")
}

Assert-Contains "scenario runtime retry" $runtimeContracts "int CatalogRevision" "definition catalog service must expose a revision for same-session retry gating."
Assert-Contains "scenario runtime retry" $runtimeOrchestrator "MarkApplyBlocked" "failed definition resolution must be tracked as blocked instead of applied."
Assert-Contains "scenario runtime retry" $runtimeOrchestrator "CatalogRevision" "blocked scenario bindings must be reconsidered after catalog refresh."
Assert-Contains "scenario runtime retry" $runtimeOrchestrator "MissingDefinition" "missing scenario definitions must be a retryable blocked state."
Assert-Contains "scenario runtime retry" $catalogRefreshCoordinator "UpdateActiveScenarioApply" "definition catalog refresh must actively ask the runtime orchestrator to retry the active binding in the same session."
Assert-Contains "scenario runtime retry" $scenarioDefinitionModule "ScenarioDefinitionCatalogRefreshCoordinator" "the catalog refresh coordinator must wrap the registered definition catalog service."
Assert-Contains "scenario runtime retry" $scenarioVerification "VerifyMissingDefinitionRefreshRetry" "scenario verification harness must cover missing definition, blocked apply, restored definition, catalog refresh, then success."
Assert-Contains "scenario runtime retry" $scenarioVerification "Catalog refresh did not cause the blocked active binding to retry and apply" "scenario verification harness must assert refresh-driven retry success."

Assert-Contains "test console execution outcomes" $scenarioExecutionLog "Scheduled.*Fired.*SkippedConditionFalse.*FailedWithError.*OnceAlreadyConsumed" "execution log must retain the full authored runtime outcome vocabulary."
Assert-Contains "test console ring buffer" $scenarioExecutionLog "Capacity\s*=\s*128.*_next\s*=\s*\(_next\s*\+\s*1\)\s*%\s*Capacity" "execution log must be a bounded ring buffer."
Assert-Contains "test console closed cost" $scenarioExecutionLog "if \(!Enabled\)\s*return;" "closed test console logging must have a cheap no-allocation branch."
Assert-Contains "test console scheduler outcomes" $scenarioScheduleRuntimeCoordinator "OnceAlreadyConsumed.*SkippedConditionFalse.*FailedWithError.*ManuallyFired" "scheduler must record once, condition, failure, and manual-fire outcomes."
Assert-Contains "test console fire now" $scenarioTestConsole "TryFireNow.*test-console-manual.*TryFireNow" "test console must route manual trigger and scheduled action firing through runtime seams."
Assert-Contains "test console time bound" $scenarioTestConsole "MaximumJumpHours\s*=\s*72.*MaximumMinutesPerRequest.*Math\.Min\(60, remaining\)" "time-control seam must use bounded hourly increments."
Assert-NotContains "test console time safety" $scenarioTestConsole "Time\.timeScale\s*=" "test console time controls must not change Unity time scale."
Assert-Contains "test console controls UI" $scenarioTestConsoleContent "\+1 hour.*\+1 day.*Run until next authored event" "Test stage must expose outcome-oriented safe time controls."
Assert-Contains "test console observability UI" $scenarioTestConsoleContent "Flags / milestones.*Quest states" "Test stage must expose live flags and quest-state observability."
Assert-Contains "test console execution log UI" $scenarioTestConsoleContent "Execution log \(newest first\)" "Test stage must expose the newest-first execution log."
Assert-Contains "test console upcoming UI" $scenarioTestConsoleContent "Next authored events" "Test stage must expose upcoming authored events."
Assert-Contains "test console actions" $scenarioTestConsoleCommands "ActionTestConsoleHour.*TryAdvanceOneHour.*TryFireNow" "authoring command pipeline must execute test console actions."

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

Assert-Contains "animated sprite frame override clear" $scenarioSpriteSwapRuleEditor "ClearActiveRulesForTarget.*ClearAnimationFrameRules" "sprite_swap clear must remove saved animated frame rules as well as static target swaps."
Assert-Contains "animated sprite frame identity" $scenarioSpriteSwapRuleEditor "FrameIdentityMatches.*AnimationFrameIndex.*AnimationFrameRuntimeSpriteKey" "frame override lookup must match by frame index or runtime frame key."
Assert-Contains "animated sprite frame revert" $scenarioSpriteSwapAuthoringService "RevertAnimationFrame.*ClearPersistedAnimationFrameRule" "Revert Frame must remove the persisted frame override for the current frame."
Assert-Contains "animated sprite revert all" $scenarioSpriteSwapAuthoringService "RevertAnimation\(.*ClearPersistedAnimationFrameRules" "Revert Animation must remove all persisted frame overrides for the asset."
Assert-Contains "animated sprite clear reapply" $scenarioSpriteSwapAuthoringService "ClearActiveRulesForTarget.*_spriteSwapEngine\.Activate" "clearing persisted frame overrides must reapply the sprite swap engine immediately."
Assert-Contains "animated sprite runtime restore" $scenarioSpriteRuntimeMutationService "Configure\(.*RestoreRemovedReplacement\(replacements\).*_swaps\.Clear" "frame-swap driver must restore a displayed frame when its override is removed or changed."

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

# === Scenario reference index (Find Usages / safe rename / reference-aware delete) ===
$scenarioReferenceIndex = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Validation\ScenarioReferenceIndex.cs"
$scenarioStoryAuthoring = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioStoryAuthoringService.cs"
$scenarioCharacterLinks = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioStoryCharacterActorLinkSectionBuilder.cs"
$scenarioQuestContent = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioQuestAuthoringContentBuilder.cs"
$scenarioScriptView = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioStoryScriptViewBuilder.cs"
$scenarioStageDisclosure = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioStoryStageDisclosure.cs"

Assert-Contains "scenario reference index" $scenarioReferenceIndex "enum ScenarioReferenceTargetKind\s*\{\s*Stage.*IntercomStep.*StoryCharacter.*Milestone" "reference index must classify usages by target kind (stage, intercom step, story character, milestone)."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "public static List<ScenarioReferenceUsage> Collect\(ScenarioDefinition definition\)" "reference index must expose a single-pass collector over the definition."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "public static List<ScenarioReferenceUsage> FindUsages\(ScenarioDefinition definition,\s*ScenarioReferenceTargetKind kind,\s*string id,\s*int ownerStageScope\)" "reference index must expose stage-scoped usage lookup for stage-local intercom ids."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "public static int RedirectReferences\(ScenarioDefinition definition,\s*ScenarioReferenceTargetKind kind,\s*string oldId,\s*string newId,\s*int ownerStageScope\)" "reference index must repoint every matching reference for safe rename and return the count."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "public static string Summarize\(int count\)" "reference index must produce a plain-language usage summary."
# Representative sample of each reference kind is collected.
Assert-Contains "scenario reference index" $scenarioReferenceIndex "ScenarioReferenceTargetKind\.Stage.*unanswered-call route" "index must collect stage unanswered-call routes as stage references."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "ScenarioReferenceTargetKind\.Stage.*next-stage change" "index must collect intercom stage-change targets as stage references."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "ScenarioReferenceTargetKind\.IntercomStep.*next-step route" "index must collect intercom next/alternate routes as intercom-step references."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "ScenarioReferenceTargetKind\.IntercomStep.*response option.*route" "index must collect dialogue option routes as intercom-step references."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "ScenarioReferenceTargetKind\.StoryCharacter.*stage cast" "index must collect stage cast as story-character references."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "ScenarioReferenceTargetKind\.StoryCharacter.*dialogue line.*speaker" "index must collect dialogue speakers as story-character references."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "ScenarioReferenceTargetKind\.StoryCharacter.*recruit list" "index must collect recruit lists as story-character references."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "ScenarioReferenceTargetKind\.StoryCharacter.*participant slot" "index must collect conversation participants as story-character references."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "ScenarioReferenceTargetKind\.Milestone.*milestone check" "index must collect milestone checks as milestone references."
Assert-Contains "scenario reference index" $scenarioReferenceIndex "ScenarioReferenceTargetKind\.Milestone.*prerequisite milestone" "index must collect selection prerequisite milestones as milestone references."

# Safe rename atomically updates references through the index and participates in undo.
Assert-Contains "scenario safe rename" $scenarioStoryAuthoring "RecordUndo\(session,\s*""Rename story stage""\).*stage\.Id = newId;.*ScenarioReferenceIndex\.RedirectReferences\(definition,\s*ScenarioReferenceTargetKind\.Stage,\s*oldId,\s*newId" "stage rename must record undo then move the id and every stage reference atomically."
Assert-Contains "scenario safe rename" $scenarioStoryAuthoring "RecordUndo\(session,\s*""Rename intercom step""\).*ScenarioReferenceIndex\.RedirectReferences\(definition,\s*ScenarioReferenceTargetKind\.IntercomStep,\s*oldId,\s*newIntercomId,\s*resolvedStageIndex\)" "intercom rename must record undo and repoint stage-scoped intercom references."
Assert-Contains "scenario safe rename" $scenarioStoryAuthoring "ValidateStageRename\(flow,\s*stageIndex,\s*newId,\s*out reason\)" "stage rename must validate the new id (unique, non-empty, format) before applying."

# Reference-aware delete blocks and reports usages through the index.
Assert-Contains "scenario delete guard" $scenarioStoryAuthoring "ScenarioReferenceIndex\.FindUsages\(definition,\s*ScenarioReferenceTargetKind\.Stage,\s*stageId\)" "stage delete guard must count references through the shared index."
Assert-Contains "scenario delete guard" $scenarioStoryAuthoring "ScenarioReferenceIndex\.FindUsages\(definition,\s*ScenarioReferenceTargetKind\.StoryCharacter,\s*characterId\)" "character delete guard must count references through the shared index."
Assert-Contains "scenario delete guard" $scenarioStoryAuthoring "RecordUndo\(session,\s*""Remove story stage""\)" "stage delete must participate in undo."
Assert-Contains "scenario delete guard" $scenarioStoryAuthoring "RecordUndo\(session,\s*""Remove story character""\)" "character delete must participate in undo."

# Find Usages UI affordance reuses the navigation seam.
Assert-Contains "scenario find usages ui" $scenarioCharacterLinks "internal static void AppendUsages\(" "a shared Find Usages affordance must exist for editor surfaces."
Assert-Contains "scenario find usages ui" $scenarioCharacterLinks "ScenarioReferenceIndex\.Summarize\(usages\.Count\)" "Find Usages affordance must show a plain-language 'Used in N places' summary."
Assert-Contains "scenario find usages ui" $scenarioCharacterLinks "ScenarioStoryFocusedEditorActions\.StageOpen\(usage\.NavStageIndex\)" "clicking a usage must navigate via the existing focused-editor open-stage seam."
Assert-Contains "scenario find usages ui" $scenarioCharacterLinks "AppendUsages\(items,\s*definition,\s*ScenarioReferenceTargetKind\.StoryCharacter,\s*character\.CharacterId" "story character editor must show its usages."
Assert-Contains "scenario find usages ui" $scenarioQuestContent "AppendUsages\(items,\s*definition,\s*ScenarioReferenceTargetKind\.Stage,\s*stage\.Id" "story stage inspector must show its usages."

# STORYUX: script read view builds speaker/line/reply/route rows and jumps to the focused editor.
Assert-Contains "story script view" $scenarioScriptView "internal static class ScenarioStoryScriptViewBuilder" "a stage-scoped script-view builder must exist."
Assert-Contains "story script view" $scenarioScriptView "public static ScenarioAuthoringInspectorSection BuildStageScript\(ScenarioDefinition definition,\s*ScenarioFlowStageDefinition stage,\s*int stageIndex\)" "script view must build a read-only section for one stage."
Assert-Contains "story script view" $scenarioScriptView "public static string DescribeOptionRoute\(ScenarioFlowStageDefinition stage,\s*ScenarioDialogueOptionDefinition option\)" "script view must phrase where a player reply leads."
Assert-Contains "story script view" $scenarioScriptView "Ends the conversation" "an empty reply route must read as ending the conversation."
Assert-Contains "story script view" $scenarioScriptView "Continues to " "a reply pointing at another scene must read as continuing to it."
Assert-Contains "story script view" $scenarioScriptView "Starts stage " "a stage-change outcome must read as starting the target stage."
Assert-Contains "story script view" $scenarioScriptView "ScenarioCastPortraitResolver\.Resolve\(candidate\.Member\)" "script view must resolve a speaker portrait through the cast portrait resolver when available."
Assert-Contains "story script view" $scenarioScriptView "ScenarioStoryFocusedEditorActions\.StageOpen\(stageIndex\)" "each script line must offer an Edit affordance that opens the focused stage editor."

# STORYUX: progressive-disclosure rule is a testable pure helper wired into the story page.
Assert-Contains "story stage disclosure" $scenarioStageDisclosure "public static bool HasBasicDialogue\(ScenarioFlowStageDefinition stage\)" "disclosure must decide whether a stage has basic dialogue content."
Assert-Contains "story stage disclosure" $scenarioStageDisclosure "public static bool ShouldRevealAdvancedRouting\(ScenarioFlowStageDefinition stage\)" "disclosure must gate advanced routing behind basic dialogue content."
Assert-Contains "story stage disclosure wiring" $scenarioQuestContent "ScenarioStoryScriptViewBuilder\.BuildStageScript\(definition, stage, index\)" "the story page must render the script read view per stage."
Assert-Contains "story stage disclosure wiring" $scenarioQuestContent "ScenarioStoryStageDisclosure\.ShouldRevealAdvancedRouting\(stage\)" "the story page must hide advanced routing until basic dialogue exists."

# === STORYGRAPH: primary visual story map (graph model + deterministic layout + renderer) ===
$scenarioStoryGraphModel = Read-RepoFile "ShelteredAPI\Scenarios\Domain\Story\ScenarioStoryGraphModel.cs"
$scenarioStoryGraphBuilder = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioStoryGraphBuilder.cs"
$scenarioStoryMapRenderer = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\Rendering\ScenarioAuthoringShellStoryMapImguiRenderer.cs"

# Model shape: stage vs terminal nodes, problem status, edges, and layout positions.
Assert-Contains "story graph model" $scenarioStoryGraphModel "enum ScenarioStoryGraphNodeKind\s*\{\s*Stage.*Terminal" "the graph model must distinguish primary stage nodes from terminal outcome leaves."
Assert-Contains "story graph model" $scenarioStoryGraphModel "enum ScenarioStoryGraphNodeStatus\s*\{\s*Ok.*Unreachable.*Broken" "nodes must carry the shared flow problems (unreachable / broken)."
Assert-Contains "story graph model" $scenarioStoryGraphModel "class ScenarioStoryGraphEdge" "the model must expose route edges between nodes."
Assert-Contains "story graph model" $scenarioStoryGraphModel "class ScenarioStoryGraphModel" "the model must expose the whole graph (nodes, edges, canvas size)."
Assert-Contains "story graph model" $scenarioStoryGraphModel "float X" "layout must record a deterministic X position per node."
Assert-Contains "story graph model" $scenarioStoryGraphModel "float Y" "layout must record a deterministic Y position per node."

# Builder reuses the shared traversal logic instead of writing a third walker.
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "internal static class ScenarioStoryGraphBuilder" "a story graph model builder must exist."
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "public static ScenarioStoryGraphModel Build\(ScenarioDefinition definition, ScenarioStoryFlowIssue\[\] issues\)" "the builder must accept the shared analyzer issues (or run them itself) to build the model."
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "new ScenarioStoryFlowValidationAnalyzer\(\)\.Analyze\(definition\)" "problems and reachability must come from the shared story-flow analyzer, not a new walker."
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "ScenarioReferenceIndex\.Collect\(definition\)" "stage-to-stage route edges must come from the shared reference index (Find Usages)."
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "ScenarioReferenceTargetKind\.Stage" "edges must be built from stage-target references (unanswered routes and stage changes)."
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "ScenarioStoryScriptViewBuilder\.DescribeStepEnding" "hover tooltips must reuse the STORYUX route phrasing."
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "ScenarioStoryFocusedEditorActions\.StageOpen\(i\)" "clicking a stage node must navigate through the shared open-stage seam."

# Terminal outcome leaves.
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "Ends conversation" "dead-end stages must produce an 'ends conversation' terminal leaf."
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "Recruits survivor" "recruiting stages must produce a recruit terminal leaf."
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "Ends scenario" "scenario-completing stages must produce an end-scenario terminal leaf."
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "ScenarioStoryGraphNodeStatus\.Unreachable" "unreachable stages must be flagged on their node status."
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "ScenarioStoryGraphEdgeStatus\.Broken" "routes to missing stages must be flagged as broken edges."

# Deterministic layered layout (BFS depth = column, siblings stacked, capped node count).
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "MaxStageNodes = 50" "the layout must cap the node count for readability."
Assert-Contains "story graph builder" $scenarioStoryGraphBuilder "kids\.Sort\(\)" "BFS must process children in a deterministic order so layout is stable."

# Wiring: the story page publishes a 'story_map' section carrying the model.
Assert-Contains "story map wiring" $scenarioQuestContent "ScenarioStoryGraphBuilder\.Build\(definition, storyIssues\)" "the story page must build the story map model from the shared analyzer issues."
Assert-Contains "story map wiring" $scenarioQuestContent "Id = ""story_map""" "the story map must render as a dedicated 'story_map' section."
Assert-Contains "story map wiring" $scenarioQuestContent "StoryMap = model" "the section must carry the built story graph model for the renderer."

# Renderer draws the visual map: detects the section, draws arrowed edges and a legend.
Assert-Contains "story map renderer" $scenarioStoryMapRenderer "IsStoryMapSection" "the renderer must detect the story map section by id."
Assert-Contains "story map renderer" $scenarioStoryMapRenderer "DrawStoryMapSection" "the renderer must draw the story map surface."
Assert-Contains "story map renderer" $scenarioStoryMapRenderer "DrawStoryMapArrow" "edges must be drawn with arrowheads."
Assert-Contains "story map renderer" $scenarioStoryMapRenderer "DrawStoryMapLegend" "the map must show a legend."
Assert-Contains "story map renderer" $scenarioStoryMapRenderer "ScenarioAuthoringBackendService\.Instance\.ExecuteAction\(node\.NavActionId\)" "clicking a node must execute its navigation action."

# STORYUX: humane story-character labels replace the 'Display name 1' debug steppers.
Assert-Contains "story character labels" $scenarioCharacterLinks "EditableProperty\(""Display name"","  "the display-name field must use plain creator language, not a numbered stepper."
Assert-Contains "story character labels" $scenarioCharacterLinks "Vanilla preset \(optional\)" "optional vanilla preset must be labelled as optional."
Assert-Contains "story character labels" $scenarioCharacterLinks "Advanced: internal id" "the raw CharacterId must move to a secondary Advanced row."
Assert-NotContains "story character labels" $scenarioCharacterLinks "EditableProperty\(""Display name "" \+" "numbered 'Display name N' debug labels must be gone."

# ASSETINV: inventory is export-seam-backed, actionable, undoable, and provenance-aware.
Assert-Contains "asset inventory export parity" $scenarioAssetInventory 'ScenarioPackagePlanner\.CollectAssetPaths\(definition\)' "asset inventory must reuse the package preview's referenced-asset enumeration."
Assert-Contains "asset inventory problem states" $scenarioAssetInventory 'ScenarioAssetInventoryState\.Missing.*ScenarioAssetInventoryState\.Orphan' "inventory must classify referenced-absent and unreferenced-present files."
Assert-Contains "asset inventory relink" $scenarioAssetInventoryMutations 'RecordVisualChange\(definition, "Relink missing asset.*ReplaceAllReferences\(definition, missingPath, newRelativePath\)' "relink must record one undo point before updating every matching reference."
Assert-Contains "asset inventory provenance README" $scenarioPackagePlan 'ASSET CREDITS' "asset credits must feed README generation."
Assert-Contains "asset inventory provenance manifest" $scenarioPackagePlan 'writer\.WriteStartElement\("AssetCredits"\)' "asset credits must feed manifest generation."
Assert-Contains "asset inventory size awareness" $scenarioAssetInventoryContent 'Payload warning threshold.*25 MB.*over 2048 px on either side or 2 MB' "inventory must state payload and per-texture warning thresholds in creator language."
Assert-Contains "asset inventory fixture contracts" $scenarioAssetInventoryVerification 'CollectAssetPaths.*ScenarioAssetInventoryState\.Missing.*ScenarioAssetInventoryState\.Orphan.*RelinkMissing.*history\.Undo.*AssetCredits.*ManifestFileName.*ReadmeFileName' "fixture verification must cover export parity, problem states, undoable relink, and provenance output."
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

# DRAFTROWS: draft rows carry base mode / relative times / validation / recovery facts,
# destructive confirmations name the draft and state the export fact, and interrupted
# launches sit in a labelled "Needs attention" section instead of among real scenarios.
$scenarioBookDraftFacts = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Selection\ScenarioBookDraftFacts.cs"
$scenarioBookDataSource = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Selection\ScenarioBookBrowserDataSource.cs"
$scenarioBookRenderer = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Selection\ScenarioBookBrowserRenderer.cs"
$scenarioBookPanel = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Selection\ScenarioBookBrowserPanel.cs"
$scenarioPublishExport = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioPublishExportService.cs"
$scenarioDraftRepository = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthoringDraftRepository.cs"

Assert-Contains "draft row facts base mode" $scenarioBookDraftFacts "case ScenarioBaseGameMode\.Surrounded: return ""Surrounded"";.*case ScenarioBaseGameMode\.Stasis: return ""Stasis"";.*default: return ""Standard"";" "base mode label must map Surrounded/Stasis and default Survival to Standard."
Assert-Contains "draft row facts relative time" $scenarioBookDraftFacts "ScenarioDraftSnapshotService\.FormatAge" "relative last-edited/export text must reuse the shared FormatAge helper."
Assert-Contains "draft row facts recovery flag" $scenarioBookDraftFacts "HasUnsavedRecovery.*File\.GetLastWriteTimeUtc\(files\[i\]\) > manualUtc" "recovery flag must be set when an autosave is newer than the last manual save."
Assert-Contains "draft row facts validation summary" $scenarioBookDraftFacts "error\(s\).*warning\(s\).*return ""OK"";" "validation summary must report errors, warnings, or OK."
Assert-Contains "draft row facts assembly" $scenarioBookDraftFacts "facts\.BaseModeLabel = BaseModeLabel\(.*facts\.LastEditedText = ResolveLastEdited\(.*facts\.HasRecoveryData = HasUnsavedRecovery\(" "cheap row facts must assemble base mode, last-edited time, and recovery flag."
Assert-Contains "draft detail facts export" $scenarioBookDraftFacts "ScenarioPublishExportService\.TryGetExistingExportInfo\(entry\.ScenarioId, entry\.DisplayName" "detail facts must resolve last-export state for the selected draft."
Assert-Contains "draft detail facts validation" $scenarioBookDraftFacts "ScenarioAuthoringValidationSnapshot\.Evaluate\(validator, definition, scenarioFilePath\)" "detail facts must compute validation lazily for the selected draft."

Assert-Contains "draft delete confirmation naming" $scenarioBookDraftFacts "Delete '"" \+ name \+ ""'" "delete confirmation text must name the draft."
Assert-Contains "draft delete confirmation export fact" $scenarioBookDraftFacts "Its exported package is kept.*No exported package exists for this draft" "delete confirmation must state whether an exported package is kept."
Assert-Contains "draft delete confirmation recovery fact" $scenarioBookDraftFacts "Unsaved recovery data.*will be removed with the draft" "delete confirmation must state whether recovery data is lost."

Assert-Contains "export existing info helper" $scenarioPublishExport "internal static bool TryGetExistingExportInfo\(string scenarioId, string displayName, out string exportRoot, out DateTime lastWriteUtc\)" "export service must expose a session-free last-export probe reusing the export path convention."
Assert-Contains "draft slot path helper" $scenarioDraftRepository "internal static string GetDraftScenarioFilePath\(int slot\)" "draft repository must resolve a draft's scenario.xml by slot without enumerating all drafts."
Assert-Contains "draft durable delete transaction" $scenarioDraftRepository "Directory\.Move\(source, deletedPath\).*Directory\.Delete\(deletedPath, true\)" "confirmed draft deletion must quarantine then purge the owned draft folder."
Assert-Contains "draft delete slot boundary" $scenarioDraftRepository "string\.Equals\(parent, root.*name\.StartsWith\(" "draft deletion must only mutate direct Slot_N children of the draft root."
Assert-Contains "draft delete virtual-save boundary" $scenarioDraftRepository "saveEntry\.absoluteSlot != slot" "draft deletion must refuse a mismatched virtual save entry."
Assert-Contains "draft delete executable contract" $scenarioVerification "VerifyDraftDeleteDurability.*scenario\.xml\.bak.*autosave\.xml.*Slot_23.*Fresh draft catalog scan" "framework verification must prove durable draft deletion removes backups/history, preserves unrelated slots, and survives a fresh catalog scan."

Assert-Contains "draft row detail wiring" $scenarioBookDataSource "baseMode \+ "" base, edited "" \+ edited \+ recovery" "draft rows must show base mode, relative edit time, and a recovery marker."
Assert-Contains "draft row recovery badge" $scenarioBookDataSource "if \(facts != null && facts\.HasRecoveryData\)\s*return ""Recovery"";" "draft rows with unsaved recovery data must badge as Recovery."
Assert-Contains "recovery draft-workshop route" $scenarioBookDataSource "if \(selectedType == ScenarioBookType\.Draft\)\s*AddRecoveryRows\(rows\);" "interrupted launch recovery must remain reachable from the draft workshop without polluting the playable library."
Assert-Contains "recovery needs-attention header" $scenarioBookDataSource "Title = ""Needs attention""" "recovery rows must be grouped under a labelled Needs attention section."
Assert-Contains "direct library tool ordering" $scenarioBookDataSource "drafts\.SectionLabel = ""TOOLS"";.*Title = ""Install Downloads"".*AddLibraryScenarioRows\(rows\);" "draft and install tools must be pinned before the unified scenario library."
Assert-NotContains "active root search filters tools" $scenarioBookDataSource "persistentRootNavigation" "root search must not pin draft and install tools while a query is active."
Assert-Contains "active root search uses row labels" $scenarioBookDataSource "if \(MatchesSearch\(row, searchFilter\)\)\s*filtered\.Add\(row\)" "active root search must filter tool rows through the same title/detail/badge matching contract as scenarios."
Assert-Contains "unified custom scenario library" $scenarioBookDataSource "entry == null \|\| entry\.Source != ScenarioCatalogSource\.Modded.*AddLibraryScenarioRow\(rows, entry\)" "the entry library must include every modded playable scenario without base-mode grouping."
Assert-Contains "vanilla expanded-save library rows" $scenarioBookDataSource "entry\.Source != ScenarioCatalogSource\.Vanilla.*entry\.BaseGameMode != ScenarioBaseGameMode\.Surrounded.*entry\.BaseGameMode != ScenarioBaseGameMode\.Stasis.*AddLibraryScenarioRow\(rows, entry\)" "the unified library must append the vanilla Surrounded and Stasis archive entries."
Assert-Contains "vanilla library visual identity" $scenarioBookDataSource "entry\.Source == ScenarioCatalogSource\.Vanilla\s*\? ""Vanilla""" "vanilla library rows must be visibly distinguished from authored scenarios."
Assert-NotContains "no root Surrounded card" $scenarioBookDataSource "BuildTypeRow\(\s*ScenarioBookType\.Surrounded" "the custom library must not restore a vanilla Surrounded root card."
Assert-NotContains "no root Stasis card" $scenarioBookDataSource "BuildTypeRow\(ScenarioBookType\.Stasis" "the custom library must not restore a vanilla Stasis root card."

Assert-Contains "draft detail pane facts" $scenarioBookRenderer "BuildDraftFacts\(root, model != null \? model\.Facts : null\)" "the draft detail pane must render the assembled draft facts."
Assert-Contains "two-page library spread" $scenarioBookRenderer "BuildLibrarySpread\(spread, selectedScenario, playStats, rows, pageIndex, select\).*BuildLibraryWelcome\(spread\).*BuildLibraryDetails\(spread, selectedScenario, playStats, select\)" "the entry view must keep the scenario list left and selection details right in one spread."
Assert-Contains "library local paging" $scenarioBookRenderer "BuildLibraryPageControls\(spread, pageIndex, pageCount\).*ScenarioBookBrowserPanel\.LibraryRowsPerPage" "only the dense scenario-list region may expose entry paging."
Assert-Contains "library layout scales from book metrics" $scenarioBookRenderer "ReferenceContentWidth = 1080f.*ReferenceContentHeight = 490f.*content\.width / ReferenceContentWidth.*content\.height / ReferenceContentHeight" "library typography and row metrics must scale from the book content rectangle."
Assert-Contains "library uses full searchable viewport" $scenarioBookRenderer "ContentRectLocal\.height - SearchReservedHeight.*_pagedList\.AddRow\(spread, Mathf\.RoundToInt\(viewportHeight\)\)" "the library spread must occupy the full book viewport below search."
Assert-Contains "library details actions" $scenarioBookRenderer "Kind = ScenarioBookRowKind\.StartScenario.*Kind = ScenarioBookRowKind\.OpenScenarioSaves" "selected scenario details must expose the existing play and save-archive routes."
Assert-Contains "library action hover contrast" $scenarioBookRenderer "AttachLibraryActionHover.*StartCardHover.*KeycapInk.*Color\.white.*Palette\.Brass" "dark and light library actions must retain contrasting text and edge colors while hovered."
Assert-Contains "library harness row contract" $scenarioBookRenderer "ScenarioBookRow_Tool_.*ScenarioBookRow_Library_.*ScenarioBookRow_Detail_" "tool rows, scenario rows, and right-page detail facts must remain visible to the scenario-book row harness."
Assert-Contains "back-only scenario book footer" $scenarioBookRenderer 'ScenarioBookBack.*new Vector3\(0f, bottomY, 0f\).*library \? 0f : -460f' "the scenario book footer must expose one Back control, centered at the library root and clear of the nested-view pager."
Assert-NotContains "no scenario book close footer" $scenarioBookRenderer 'ScenarioBookClose|"Close"' "the scenario book must not expose a second Close footer control."
Assert-Contains "library fact action spacing" $scenarioBookRenderer "factLineHeight = Math\.Max.*factLineSpacing = factLineHeight.*playY = y - \(factLineHeight \* 0\.5f\) - actionGap" "library actions must be placed from the enlarged fact metrics instead of stale absolute coordinates."
Assert-Contains "library selection stays in place" $scenarioBookPanel "if \(_view == ScenarioBookBrowserViewKind\.Types\).*BeginSaveRowsRefreshAsync\(scenario\).*RenderCurrentView\(false\)" "clicking a library scenario must update details without navigating away or flipping the book."
Assert-Contains "flash-free book open ordering" $scenarioBookPanel "StartDataRefresh\(""Loading scenarios\.\.\."", false\);\s*StartCoroutine\(SuppressUnderlyingAfterFirstRender\(\)\).*yield return new WaitForEndOfFrame\(\);.*_underlyingSuppression = _adapter\.SuppressUnderlyingChrome\(\);" "the overlay must build and render once over visible vanilla chrome before suppressing the underlying panel."
Assert-Contains "flash-free book close ordering" $scenarioBookPanel "if \(restoreUnderlyingPanel\).*StartCoroutine\(CloseAfterUnderlyingFirstRender\(root, overlay\)\).*CloseAfterUnderlyingFirstRender.*RestoreUnderlyingPanel\(\);.*yield return new WaitForEndOfFrame\(\);.*overlay\.SetActive\(false\);" "closing the book must restore and render vanilla chrome before hiding the overlay."

Assert-Contains "confirmation localize routing" $scenarioBookPanel "MessageBox\.Show\(MessageBoxButtons\.YesNo_Buttons, message,.*, null, null, localize\)" "confirmations must pass an explicit localize flag so custom draft messages render verbatim."
Assert-Contains "draft delete non-localized message" $scenarioBookPanel "ScenarioBookDraftFacts\.BuildDeleteMessage\(draftName, facts\);\s*localize = false;" "draft deletes must use the built draft-named message without localization."
Assert-Contains "duplicate confirmation" $scenarioBookPanel "ScenarioBookDraftFacts\.BuildDuplicateMessage\(draftName, facts\)" "duplicate must confirm with a draft-named message."
Assert-Contains "rename confirmation" $scenarioBookPanel "ScenarioBookDraftFacts\.BuildRenameMessage\(draftName, model\.DraftId, facts\)" "renaming a draft file must confirm with a draft-named message."

# WIZINFO: wizard quick-setting explanations, installed-copy content summary,
# top-issue "next" surfacing on Home, and the optional scenario goal field.
$scenarioEntryFlow = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthoringEntryFlowService.cs"
$scenarioContentSummary = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioContentSummary.cs"
$scenarioTopIssueResolver = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioTopIssueResolver.cs"
$scenarioMetadataActions = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioMetadataAuthoringActions.cs"

Assert-Contains "wizard toggle explanations" $scenarioEntryFlow "Key = SettingSupplies.*?Detail = ""On: shelter starts with only your authored supplies" "supplies quick-setting must state its concrete consequence tied to its toggle."
Assert-Contains "wizard toggle explanations" $scenarioEntryFlow "Key = SettingSuppressRaids.*?Detail = ""On: raiders never breach or attack this shelter\.""" "suppress-raids quick-setting must state raiders never attack."
Assert-Contains "wizard toggle explanations" $scenarioEntryFlow "Key = SettingSuppressVisitors.*?Detail = ""On: wandering NPC visitors never arrive at the shelter\.""" "suppress-visitors quick-setting must state visitors never arrive."

Assert-Contains "installed-copy content summary" $scenarioContentSummary "public int WorldChanges.*public int Cast.*public int StoryStages.*public int TimelineEntries.*public int MapLocations.*public int AssetFiles.*public int RequiredMods" "content summary must derive world/cast/story/timeline/map/assets/mods from the definition."
Assert-Contains "installed-copy content summary" $scenarioContentSummary "ScenarioPackagePlanner\.CollectAssetPaths\(definition\)" "content summary asset-file count must reuse the package asset enumeration."
Assert-Contains "installed-copy content summary wiring" $scenarioEntryFlow "ScenarioContentSummary\.Build\(editableDefinition\)\.ToCardLine\(\)" "the copy-an-installed-scenario card must show the derived content summary before creating."

Assert-Contains "home top-issue ranking" $scenarioTopIssueResolver "Severity == ScenarioIssueSeverity\.Error.*return issues\[i\].*Severity == ScenarioIssueSeverity\.Warning.*return issues\[i\]" "top-issue ranking must return blocking errors before advisory warnings."
Assert-Contains "home top-issue reuse" $scenarioTopIssueResolver "ScenarioPlaytestFixActionResolver\.BuildFixAction\(issue\.Message\).*ScenarioPublishAuthoringContentBuilder\.BuildIssueNavigationAction\(issue\)" "top-issue fix action must reuse the playtest fix resolver and the publish issue-row navigation."
Assert-Contains "home top-issue reuse" $scenarioPublishAuthoringContentBuilder "internal static ScenarioAuthoringInspectorAction BuildIssueNavigationAction" "publish issue-row navigation must be shareable as one source of truth."
Assert-Contains "home top-issue surfacing" $scenarioOverviewAuthoringContentBuilder "ScenarioTopIssueResolver\.ResolveTopIssue\(validation\).*Next: " "Home must surface the single highest-priority issue with a Next line."

Assert-Contains "scenario goal field" $scenarioDefinitionModel "public string Goal \{ get; set; \}" "ScenarioDefinition must expose the optional Goal field."
Assert-Contains "scenario goal XML round-trip" $scenarioSerializer "definition\.Goal = ReadText\(meta, ""Goal""\)" "serializer must read the optional <Goal> element."
Assert-Contains "scenario goal XML round-trip" $scenarioSerializer "WriteElement\(writer, ""Goal"", definition\.Goal\)" "serializer must write the optional <Goal> element."
Assert-Contains "scenario goal edit command" $scenarioMetadataActions "ActionDraftGoalPrefix.*definition\.Goal = value\.Trim\(\)" "metadata command handler must commit goal edits."
Assert-Contains "scenario goal home card" $scenarioOverviewAuthoringContentBuilder "ActionDraftGoalPrefix.*FormatVictorySummary" "Home identity card must show the editable goal beside its victory condition."
Assert-Contains "scenario goal readme" $scenarioPackagePlan "AppendLine\(""GOAL""\).*definition\.Goal.*Victory condition: " "export README must include the goal and, when present, the victory condition."
Assert-Contains "scenario goal verification" $scenarioVerification "VerifyWizInfoContent.*goalRoundTrip\.Goal == withGoal\.Goal.*ScenarioContentSummary\.Build\(fixture\).*ScenarioTopIssueResolver\.ResolveTopIssue" "framework verification must cover goal round-trip, installed-copy summary, and top-issue ranking."

# TESTCHECKLIST: per-draft author testing evidence, editor-known verification seams,
# non-blocking Test/Export UI, and conditional README honesty output.
Assert-Contains "test checklist fixed model" $scenarioAuthorTestChecklistService 'Started a playtest.*Saved and reloaded during play.*Reached each ending/outcome.*Verified required mods list.*Installed the exported package and played it' "the author checklist must retain all five fixed product-review steps."
Assert-Contains "test checklist internal model" $scenarioAuthorTestChecklistModel 'internal sealed class ScenarioAuthorTestChecklistItem.*bool Checked.*string Note.*DateTime\? CheckedUtc.*ScenarioAuthorTestVerificationSource Source' "checklist entries must be internal and retain checked state, note, timestamp, and verification source."
Assert-Contains "test checklist XML backward compatibility" $scenarioSerializer 'checklistSerializer\.Read\(Child\(root, "AuthorTestChecklist"\)\).*checklistSerializer\.Write\(writer, definition\.AuthorTestChecklist\)' "scenario XML must read an absent checklist as empty and write authored checklist content."
Assert-Contains "test checklist XML item round-trip" $scenarioAuthorTestChecklistSerializer 'checkedUtc.*source.*Note.*DateTime\.TryParse' "checklist XML must round-trip source, UTC check date, and optional note."
Assert-Contains "test checklist playtest auto-check" $scenarioPlaytestOrchestrator 'PlaytestState = ScenarioPlaytestState\.Playtesting;.*MarkPlaytestStarted\(session\)' "a successfully started playtest must mark the editor-verified checklist seam."
Assert-Contains "editable field transport" $scenarioStoryAuthoringActions 'EncodeToken\(string value\).*ScenarioAuthoringActionCodec\.EncodeToken\(value\).*DecodeToken\(string token\).*ScenarioAuthoringActionCodec\.DecodeToken\(token\)' "story editable actions must share the renderer base64 codec so decoded plain text reaches the draft."
Assert-NotContains "editable field transport" $scenarioStoryAuthoringActions 'Uri\.EscapeDataString|Uri\.UnescapeDataString' "story editable actions must not use a second URI token transport."
Assert-Contains "playtest stop reset" $scenarioEditorSession 'ResetStoppedPlaytestWorld\(\).*HasAppliedToCurrentWorld = false;.*AppliedDraftRevision = DraftRevision' "stopping playtest must clear the live-world latch before a restart."
Assert-Contains "playtest stop reset" $scenarioPlaytestOrchestrator 'EndPlaytest\(ScenarioEditorSession session\).*session\.ResetStoppedPlaytestWorld\(\).*InvalidateCompletionCarrier\(\).*InvalidateCompletionCarrier\(\).*binding\.ScenarioQuestInstanceId = null' "playtest stop must reset both the session latch and its stale quest carrier."
Assert-NotContains "playtest stop preserves authoring binding" $scenarioPlaytestOrchestrator 'binding\.IsActive = false' "stopping a playtest must not deactivate the authoring binding and accidentally enter normal-save play."
Assert-Contains "playtest fresh start applies draft" $scenarioPlaytestOrchestrator 'PlaytestState == ScenarioPlaytestState\.Playtesting.*return alreadyRunning;.*session\.ResetStoppedPlaytestWorld\(\).*InvalidateCompletionCarrier\(\).*_applier\.ApplyAll\(session\.WorkingDefinition, scenarioFilePath\).*session\.MarkAppliedToCurrentWorld\(\)' "every non-active start, including a fresh or stopped draft with a newer revision, must apply the current draft."
Assert-Contains "playtest carrier validates persisted ids" $scenarioPlaytestOrchestrator 'TryGetQuestInstance\(existingBinding\.ScenarioQuestInstanceId\.Value.*existingBinding\.ScenarioQuestInstanceId = null.*TrySpawnScenario' "fresh, reopened, and restarted playtests must validate a carrier id and respawn when it is absent from this world."
Assert-Contains "loaded carrier invalidation" $scenarioRuntimeBindingManager 'HandleAfterLoad.*InvalidateUnavailableCompletionCarrier\(loaded\).*TryGetQuestInstance\(binding\.ScenarioQuestInstanceId\.Value.*binding\.ScenarioQuestInstanceId = null' "a reopened save must invalidate a persisted carrier id that cannot be resolved in the loaded world."
Assert-Contains "failed outcome returns to paused authoring" $scenarioWinLossOutcomeService 'ResolveSatisfiedOutcome.*!_questInstanceResolver\.TryResolve.*ReturnAuthoringPlaytestToEditor\(\)' "failed resolution must return an authoring playtest to its paused editor state."
Assert-Contains "scenario end-game presenter injection" $scenarioWinLossOutcomeService 'IScenarioEndGamePresenter.*_endGamePresenter\.TryPresent\(presentation' "resolved outcomes must use the injected presentation seam."
Assert-Contains "scenario end-game presentation retry" $scenarioWinLossOutcomeService '_presentationPending.*if \(_presentationPending\).*PresentOutcome\(_pendingPresentation\).*_presentationPending = false' "a resolved outcome must retry presentation until the selected end-game target is ready."
Assert-Contains "scenario end-game context selection" $scenarioEndGamePresenter 'HasActiveSession.*_playtestPresenter\.TryPresent.*_installedPresenter\.TryPresent' "end-game presentation must select authoring return or installed vanilla ending from the active-session context."
Assert-Contains "survival scenario victory branch" $scenarioEndGamePresenter 'UsesScenarioVictoryPanel.*ScenarioVictoryPanel\.TryShow.*baseGameMode != ScenarioBaseGameMode\.Surrounded.*baseGameMode != ScenarioBaseGameMode\.Stasis' "Survival authored wins must use the ShelteredAPI victory panel while native success modes retain vanilla flow."
Assert-Contains "scenario victory panel chrome" $scenarioVictoryPanel 'BasePanel.*ScenarioDisplayName.*FieldManualWindowChrome\.BuildBook.*"Scenario Complete".*FulfilledConditionText.*ScenarioVictoryContinue' "the authored victory panel must use Field Manual chrome and show scenario, day, condition, and Continue content."
Assert-Contains "scenario victory panel continue" $scenarioVictoryPanel 'TooltipperObj\.ShowTooltip\(null\).*DeleteCurrentSlot\(\).*ShowLoadingScreen\("MenuScene"\)' "scenario victory Continue must mirror the vanilla game-over menu transition."
Assert-Contains "installed vanilla game-over latch" $scenarioEndGamePresenter 'UpdateModeResult\(result\).*Field\("game_over"\).*Method\("OnGameOver"\).*gameOverField\.SetValue\(true\).*onGameOver\.GetValue\(\).*UpdateModeResult\(result\)' "installed outcomes must run the complete vanilla game-over latch flow while preserving authored WIN/LOSS."
Assert-Contains "scenario outcome executable contracts" $scenarioRuntimeOutcomeVerification 'VerifyPresenterSelection.*VerifyRetryableEffectContract.*ShouldJournalEffectFailure' "framework verification must cover context presenter selection and retryable-not-consumed semantics."
Assert-NotContains "playtest fresh start stale refusal" $scenarioPlaytestOrchestrator 'Playtest not restarted: the running world predates recent draft edits|staleLiveWorld|reusedLiveWorld' "a paused/fresh playtest start must not be refused for a stale-world latch."
Assert-Contains "test checklist reinstall auto-check" $scenarioPublishExport 'result != null && result\.Success.*MarkExportReinstalled' "a successful publish.export.install action must mark the editor-verified reinstall seam."
Assert-Contains "test checklist Test UI" $scenarioAuthorTestChecklistSection 'VERIFIED BY THE EDITOR.*SELF-ATTESTED.*Editable = true.*Title = "Did you test it\?"' "the Test-stage card must show editable notes and visually distinguish editor verification from self-attestation."
Assert-Contains "test checklist Export UI" $scenarioAuthorTestChecklistSection 'of 5 test steps done.*Export is still allowed; completing the author test checklist is encouraged' "the export summary must report progress and encourage completion without blocking."
Assert-Contains "test checklist README honesty" $scenarioPackagePlan 'BuildReadmeHonestyLine\(definition\).*if \(!string\.IsNullOrEmpty\(honestyLine\)\).*AppendLine\(honestyLine\)' "PACKAGEUX README output must add the honesty line only when the checklist supplies one."
Assert-Contains "test checklist executable contracts" $scenarioAuthorTestChecklistVerification 'XML without AuthorTestChecklist.*MarkPlaytestStarted.*MarkExportReinstalled.*README omitted the conditional author-verification line.*README included an honesty line for an empty checklist' "framework verification must cover backward compatibility, round-trip/auto-check behavior, and conditional README output."

# ACTIONCOVER: every direct IMGUI event surface is either mapped to a registered
# semantic action family or carries a documented OS-input exemption.
$coveragePath = Join-Path $RepoRoot "tools\ScenarioAuthoringRendererActionCoverage.json"
$coverage = Get-Content -LiteralPath $coveragePath -Raw | ConvertFrom-Json
$coverageByKey = @{}
foreach ($entry in $coverage.interactiveMethods) {
    if ([string]::IsNullOrWhiteSpace([string]$entry.key) -or [string]::IsNullOrWhiteSpace([string]$entry.actionFamily)) {
        $failures.Add("renderer action coverage manifest: interactive entries require key and actionFamily")
        continue
    }
    if ($coverageByKey.ContainsKey([string]$entry.key)) {
        $failures.Add("renderer action coverage manifest: duplicate key $($entry.key)")
    }
    $coverageByKey[[string]$entry.key] = $entry
}
$exemptionsByKey = @{}
foreach ($entry in $coverage.exemptions) {
    if ([string]::IsNullOrWhiteSpace([string]$entry.key) -or [string]::IsNullOrWhiteSpace([string]$entry.reason) -or [string]::IsNullOrWhiteSpace([string]$entry.osClickFallback)) {
        $failures.Add("renderer action coverage manifest: exemptions require file/method key, reason, and OS-click fallback")
        continue
    }
    if ($coverageByKey.ContainsKey([string]$entry.key) -or $exemptionsByKey.ContainsKey([string]$entry.key)) {
        $failures.Add("renderer action coverage manifest: duplicate action/exemption key $($entry.key)")
    }
    $exemptionsByKey[[string]$entry.key] = $entry
}

$rendererRoot = Join-Path $RepoRoot "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\Rendering"
$interactionPattern = '(?:GUI|GUILayout)\.(?:Button|Toggle|HorizontalSlider|VerticalSlider|SelectionGrid|TextField|TextArea)|DrawPlainButton\(|EventType\.MouseDown'
$methodPattern = '^\s*private\s+(?:static\s+)?[\w<>\[\],]+\s+(\w+)\s*\('
$discovered = @{}
foreach ($file in Get-ChildItem -LiteralPath $rendererRoot -Filter 'ScenarioAuthoringShell*ImguiRenderer.cs') {
    $method = $null
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        if ($line -match $methodPattern) { $method = $matches[1] }
        if ($line -match $interactionPattern) {
            if ([string]::IsNullOrEmpty($method)) {
                $failures.Add("renderer action coverage scan: could not resolve method for $($file.Name)")
                continue
            }
            $key = "$($file.Name)::$method"
            $discovered[$key] = $true
            if (-not $coverageByKey.ContainsKey($key) -and -not $exemptionsByKey.ContainsKey($key)) {
                $failures.Add("renderer action coverage scan: uncovered interactive method $key")
            }
        }
    }
}
foreach ($key in $coverageByKey.Keys) {
    if (-not $discovered.ContainsKey($key)) {
        $failures.Add("renderer action coverage manifest: stale interactive method $key")
    }
}
foreach ($key in $exemptionsByKey.Keys) {
    if ($key -like 'ScenarioAuthoringShell*ImguiRenderer.cs::*' -and -not $discovered.ContainsKey($key)) {
        $failures.Add("renderer action coverage manifest: stale renderer exemption $key")
    }
}

$rendererManifestSource = Read-RepoFile "ShelteredAPI\Scenarios\Presentation\Authoring\Shell\ScenarioAuthoringRendererActionManifest.cs"
$rendererContractsSource = Read-RepoFile "ShelteredAPI\Scenarios\Application\Authoring\ScenarioAuthoringContracts.cs"
$rendererCoverageSource = $rendererManifestSource + $rendererContractsSource
foreach ($entry in $coverage.interactiveMethods) {
    $family = [string]$entry.actionFamily
    if (($family.StartsWith('shell.') -or $family.StartsWith('sprite_')) -and $rendererCoverageSource.IndexOf($family, [StringComparison]::Ordinal) -lt 0) {
        $failures.Add("renderer action coverage manifest: action family '$family' is not registered by the product manifest/contracts")
    }
}

$actionCoverageVerification = Read-RepoFile "ShelteredAPI\Scenarios\Diagnostics\ScenarioAuthoringActionCoverageVerification.cs"
Assert-Contains "renderer action runtime fixture" $actionCoverageVerification 'ScenarioAuthoringRendererActionManifest\.Build\(.*BuildContractWindow\(shell\).*RequireFamily\(ids, ScenarioAuthoringActionIds\.ActionRendererMapFilterTogglePrefix.*Require\(ids, ScenarioAuthoringActionIds\.ActionRendererPlacementDone.*visuals\.snap_to_grid.*visuals\.show_grid' "runtime verification must build the serialized semantic-action window and assert the known action families."
Assert-Contains "recursive shell action projection" $rendererManifestSource 'CollectWindows\(actions.*CollectDocument\(actions.*CollectHelp\(actions.*CollectTutorial\(actions.*CollectTour\(actions.*CollectSettings\(actions' "the shell contract projection must recursively include windows, focused documents, popups/help/tutorial/tour, and settings."
Assert-Contains "semantic contract serializer window" $rendererManifestSource 'Id = "contract\.semantic_actions".*Visible = false.*Items = items' "the existing shell serializer must receive a non-rendered window containing the exhaustive action/field projection."

if ($failures.Count -gt 0) {
    Write-Host ("ShelteredAPI contract tests failed: " + $failures.Count)
    foreach ($failure in $failures) {
        Write-Host ("FAIL`t" + $failure)
    }
    exit 1
}

Write-Host "ShelteredAPI contract tests passed."
