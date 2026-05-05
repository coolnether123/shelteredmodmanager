using UnityEngine;

using ShelteredAPI.Scenarios.Domain.Objects;
namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ScenarioSceneSpritePlacementMarker : MonoBehaviour
    {
        public string PlacementId;
        public string ScenarioObjectId;
        public string RuntimeBindingKey;
        public ScenarioObjectStartState StartState;
        public int GridX = -1;
        public int GridY = -1;
    }
}
