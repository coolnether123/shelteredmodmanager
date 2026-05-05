using System.Globalization;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal static class ScenarioSceneSpritePlacementIdentity
    {
        public static string ResolveScenarioObjectId(SceneSpritePlacement placement, int index)
        {
            if (placement == null)
                return "scene_sprite_" + index.ToString(CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(placement.ScenarioObjectId))
                return placement.ScenarioObjectId;

            if (!string.IsNullOrEmpty(placement.Id))
                return placement.Id;

            return "scene_sprite_" + index.ToString(CultureInfo.InvariantCulture);
        }

        public static string ResolveRuntimeBindingKey(SceneSpritePlacement placement, string scenarioObjectId)
        {
            if (placement == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(placement.RuntimeBindingKey))
                return placement.RuntimeBindingKey;

            return !string.IsNullOrEmpty(scenarioObjectId) ? "binding:" + scenarioObjectId : string.Empty;
        }
    }
}
