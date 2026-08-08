using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Application.Runtime;

namespace ShelteredScenarioEditor.Application.Assets
{
    /// <summary>
    /// Editor-owned preview port for scene assets. Runtime application remains in
    /// ShelteredAPI and is reached only through its public facade.
    /// </summary>
    internal sealed class ScenarioEditorSceneAssetPreviewService
    {
        private readonly ScenarioPreviewSessionHost _previewSession;

        public ScenarioEditorSceneAssetPreviewService(ScenarioPreviewSessionHost previewSession)
        {
            _previewSession = previewSession;
        }

        public int RefreshSpriteSwaps(ScenarioDefinition definition, string scenarioFilePath)
        {
            ScenarioPreviewResult result = _previewSession.Refresh(
                definition,
                ScenarioPreviewRefreshScope.SpriteSwaps);
            return result != null && result.Started ? result.SpriteSwapChanges : 0;
        }

        public int RefreshScenePlacements(ScenarioDefinition definition, string scenarioFilePath)
        {
            ScenarioPreviewResult result = _previewSession.Refresh(
                definition,
                ScenarioPreviewRefreshScope.ScenePlacements);
            return result != null && result.Started ? result.ScenePlacementChanges : 0;
        }

        public int RefreshAll(ScenarioDefinition definition, string scenarioFilePath)
        {
            ScenarioPreviewResult result = _previewSession.Refresh(
                definition,
                ScenarioPreviewRefreshScope.SceneAssets);
            return result != null && result.Started
                ? result.SpriteSwapChanges + result.ScenePlacementChanges
                : 0;
        }

    }
}
