using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class AssetApplyService
    {
        private readonly IScenarioSpriteSwapEngine _spriteSwapEngine;
        private readonly IScenarioSceneSpritePlacementEngine _sceneSpritePlacementEngine;

        public AssetApplyService(
            IScenarioSpriteSwapEngine spriteSwapEngine,
            IScenarioSceneSpritePlacementEngine sceneSpritePlacementEngine)
        {
            _spriteSwapEngine = spriteSwapEngine;
            _sceneSpritePlacementEngine = sceneSpritePlacementEngine;
        }

        public void Apply(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result)
        {
            if (_spriteSwapEngine != null)
                _spriteSwapEngine.Activate(definition, scenarioFilePath, result);
            if (_sceneSpritePlacementEngine != null)
                _sceneSpritePlacementEngine.Activate(definition, scenarioFilePath, result);
        }
    }
}
