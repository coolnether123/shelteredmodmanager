using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ObjectPlacementService
    {
        public ObjectPlacement CapturePlacement(Obj_Base obj)
        {
            return ScenarioBunkerDraftService.CreatePlacement(obj);
        }

        public void UpsertPlacement(ScenarioEditorSession session, ObjectPlacement placement)
        {
            ScenarioBunkerDraftService.UpsertPlacement(session, placement);
        }
    }
}
