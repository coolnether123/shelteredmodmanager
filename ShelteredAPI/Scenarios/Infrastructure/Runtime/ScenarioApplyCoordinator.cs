using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioApplyCoordinator : IScenarioApplier
    {
        private readonly FamilyApplyService _familyApplyService;
        private readonly InventoryApplyService _inventoryApplyService;
        private readonly BunkerApplyService _bunkerApplyService;
        private readonly AssetApplyService _assetApplyService;
        private readonly TriggerRuntimeAdapter _triggerRuntimeAdapter;

        public ScenarioApplyCoordinator(
            FamilyApplyService familyApplyService,
            InventoryApplyService inventoryApplyService,
            BunkerApplyService bunkerApplyService,
            AssetApplyService assetApplyService,
            TriggerRuntimeAdapter triggerRuntimeAdapter)
        {
            _familyApplyService = familyApplyService;
            _inventoryApplyService = inventoryApplyService;
            _bunkerApplyService = bunkerApplyService;
            _assetApplyService = assetApplyService;
            _triggerRuntimeAdapter = triggerRuntimeAdapter;
        }

        public ScenarioApplyResult ApplyAll(ScenarioDefinition definition, string scenarioFilePath)
        {
            ScenarioApplyResult result = new ScenarioApplyResult();
            if (definition == null)
            {
                result.AddMessage("Scenario definition is null; nothing applied.");
                return result;
            }

            if (_familyApplyService != null)
                _familyApplyService.Apply(definition, scenarioFilePath, result);
            if (_inventoryApplyService != null)
                _inventoryApplyService.Apply(definition, result);
            if (_bunkerApplyService != null)
                _bunkerApplyService.Apply(definition, result);
            if (_triggerRuntimeAdapter != null)
                _triggerRuntimeAdapter.Apply(definition, result);
            if (_assetApplyService != null)
                _assetApplyService.Apply(definition, scenarioFilePath, result);
            return result;
        }

        public ScenarioApplyResult ApplyAll(ScenarioDefinition definition)
        {
            return ApplyAll(definition, null);
        }
    }
}
