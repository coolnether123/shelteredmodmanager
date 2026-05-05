namespace ShelteredAPI.Scenarios.Domain.Assets{
    internal enum ScenarioPlaceableAssetKind
    {
        VisualOnly = 0,
        Person = 1,
        InteractiveObject = 2,
        PathfindingActor = 3,
        GameplayAsset = 4
    }

    internal sealed class ScenarioPlaceableAssetClassification
    {
        public ScenarioPlaceableAssetKind Kind;
        public bool CanPlaceAsSceneSprite;
        public string Label;
        public string Guidance;
    }
}
