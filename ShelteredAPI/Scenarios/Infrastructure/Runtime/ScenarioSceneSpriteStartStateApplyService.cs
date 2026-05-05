using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;

namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
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
                string id = ScenarioSceneSpritePlacementIdentity.ResolveScenarioObjectId(placement, i);
                string bindingKey = ScenarioSceneSpritePlacementIdentity.ResolveRuntimeBindingKey(placement, id);
                ScenarioObjectStartStateApplyService.Record(state, id, bindingKey, placement.StartState);
            }
        }
    }
}
