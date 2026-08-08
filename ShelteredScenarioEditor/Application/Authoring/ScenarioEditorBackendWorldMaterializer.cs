using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Bunker;

namespace ShelteredScenarioEditor.Application.Authoring
{
    /// <summary>Editor-owned switching policy for the neutral per-base-mode world documents.</summary>
    internal static class ScenarioEditorBackendWorldMaterializer
    {
        public static void StoreCurrentWorld(ScenarioDefinition definition)
        {
            if (definition == null) return;
            if (definition.BackendWorlds == null) definition.BackendWorlds = new ScenarioBackendWorldsDefinition();
            ScenarioDefinition copy = Application.Runtime.ScenarioEditorDefinitionCloner.Clone(definition);
            ScenarioBackendWorldDefinition world = definition.BackendWorlds.GetOrCreate(definition.BaseGameMode);
            world.BunkerEdits = copy.BunkerEdits;
            world.BunkerGrid = copy.BunkerGrid;
            world.SceneSpritePlacements.Clear();
            if (copy.AssetReferences == null) return;
            for (int i = 0; i < copy.AssetReferences.SceneSpritePlacements.Count; i++)
                world.SceneSpritePlacements.Add(copy.AssetReferences.SceneSpritePlacements[i]);
        }

        public static void MaterializeCurrentWorld(ScenarioDefinition definition, ScenarioBaseGameMode baseMode)
        {
            if (definition == null) return;
            if (definition.BackendWorlds == null) definition.BackendWorlds = new ScenarioBackendWorldsDefinition();
            ScenarioDefinition copy = Application.Runtime.ScenarioEditorDefinitionCloner.Clone(definition);
            ScenarioBackendWorldDefinition world = copy.BackendWorlds.Find(baseMode);
            definition.BunkerEdits = world != null ? world.BunkerEdits : new BunkerEditsDefinition();
            definition.BunkerGrid = world != null ? world.BunkerGrid : new ScenarioBunkerGridDefinition();
            if (definition.AssetReferences == null) definition.AssetReferences = new AssetReferencesDefinition();
            definition.AssetReferences.SceneSpritePlacements.Clear();
            for (int i = 0; world != null && i < world.SceneSpritePlacements.Count; i++)
                definition.AssetReferences.SceneSpritePlacements.Add(world.SceneSpritePlacements[i]);
        }
    }
}
