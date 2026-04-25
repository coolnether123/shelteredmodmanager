using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class StructurePlacementService
    {
        public ObjectPlacement CreateRoomPlacement(int gridX, int gridY, Vector3 position, string identity)
        {
            return CreatePlacement(ScenarioPlacementDefinitions.Room, gridX, gridY, position, identity, null);
        }

        public ObjectPlacement CreateLadderPlacement(int gridX, int gridY, Vector3 position, string identity, float horizontalPos)
        {
            return CreatePlacement(
                ScenarioPlacementDefinitions.Ladder,
                gridX,
                gridY,
                position,
                identity,
                new ScenarioProperty
                {
                    Key = ScenarioPlacementDefinitions.PropertyHorizontalPos,
                    Value = horizontalPos.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
        }

        public ObjectPlacement CreateRoomLightPlacement(int gridX, int gridY, Vector3 position, string identity)
        {
            return CreatePlacement(ScenarioPlacementDefinitions.RoomLight, gridX, gridY, position, identity, null);
        }

        private static ObjectPlacement CreatePlacement(
            string definitionReference,
            int gridX,
            int gridY,
            Vector3 position,
            string identity,
            ScenarioProperty extraProperty)
        {
            ObjectPlacement placement = ScenarioBunkerDraftService.CreatePlacement(definitionReference, position, Vector3.zero);
            ScenarioBunkerDraftService.SetProperty(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyGridX, gridX.ToString());
            ScenarioBunkerDraftService.SetProperty(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyGridY, gridY.ToString());
            ScenarioBunkerDraftService.SetProperty(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyAuthoringIdentity, identity);
            if (extraProperty != null)
                placement.CustomProperties.Add(extraProperty);
            return placement;
        }
    }
}
