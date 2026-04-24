using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSceneSpriteStartStateApplyService
    {
        private readonly ScenarioRuntimeStateService _stateService;

        public ScenarioSceneSpriteStartStateApplyService(ScenarioRuntimeStateService stateService)
        {
            _stateService = stateService;
        }

        public void Apply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            ScenarioRuntimeState state = _stateService != null ? _stateService.State : null;
            for (int i = 0; definition != null && definition.AssetReferences != null && definition.AssetReferences.SceneSpritePlacements != null && i < definition.AssetReferences.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = definition.AssetReferences.SceneSpritePlacements[i];
                if (placement == null)
                    continue;
                string id = !string.IsNullOrEmpty(placement.ScenarioObjectId) ? placement.ScenarioObjectId : placement.Id;
                ScenarioObjectStartStateApplyService.Record(state, id, placement.RuntimeBindingKey, placement.StartState);
            }
        }
    }
}
