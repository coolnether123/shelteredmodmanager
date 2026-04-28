using System;
using ModAPI.Core;
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
        private readonly ScenarioObjectStartStateApplyService _objectStartStateApplyService;
        private readonly ScenarioSceneSpriteStartStateApplyService _sceneSpriteStartStateApplyService;

        public ScenarioApplyCoordinator(
            FamilyApplyService familyApplyService,
            InventoryApplyService inventoryApplyService,
            BunkerApplyService bunkerApplyService,
            AssetApplyService assetApplyService,
            TriggerRuntimeAdapter triggerRuntimeAdapter,
            ScenarioObjectStartStateApplyService objectStartStateApplyService,
            ScenarioSceneSpriteStartStateApplyService sceneSpriteStartStateApplyService)
        {
            _familyApplyService = familyApplyService;
            _inventoryApplyService = inventoryApplyService;
            _bunkerApplyService = bunkerApplyService;
            _assetApplyService = assetApplyService;
            _triggerRuntimeAdapter = triggerRuntimeAdapter;
            _objectStartStateApplyService = objectStartStateApplyService;
            _sceneSpriteStartStateApplyService = sceneSpriteStartStateApplyService;
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
                ApplyStep("family", result, delegate { _familyApplyService.Apply(definition, scenarioFilePath, result); });
            if (_inventoryApplyService != null)
                ApplyStep("inventory", result, delegate { _inventoryApplyService.Apply(definition, result); });
            if (_bunkerApplyService != null)
                ApplyStep("bunker", result, delegate { _bunkerApplyService.Apply(definition, result); });
            if (_triggerRuntimeAdapter != null)
                ApplyStep("scheduled runtime", result, delegate { _triggerRuntimeAdapter.Apply(definition, result); });
            if (_objectStartStateApplyService != null)
                ApplyStep("object start state", result, delegate { _objectStartStateApplyService.Apply(definition, result); });
            if (_assetApplyService != null)
                ApplyStep("assets", result, delegate { _assetApplyService.Apply(definition, scenarioFilePath, result); });
            if (_sceneSpriteStartStateApplyService != null)
                ApplyStep("scene sprite start state", result, delegate { _sceneSpriteStartStateApplyService.Apply(definition, result); });
            return result;
        }

        public ScenarioApplyResult ApplyAll(ScenarioDefinition definition)
        {
            return ApplyAll(definition, null);
        }

        private static void ApplyStep(string label, ScenarioApplyResult result, Action apply)
        {
            if (apply == null)
                return;

            try
            {
                apply();
            }
            catch (Exception ex)
            {
                string message = "Scenario " + (label ?? "runtime") + " apply failed: " + ex.Message;
                if (result != null)
                    result.AddMessage(message);
                MMLog.WriteWarning("[ScenarioApplyCoordinator] " + message);
            }
        }
    }
}
