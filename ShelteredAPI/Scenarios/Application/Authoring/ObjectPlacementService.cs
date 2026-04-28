using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ObjectPlacementService
    {
        private readonly IScenarioDraftMutationService _draftMutationService;

        public ObjectPlacementService(IScenarioDraftMutationService draftMutationService)
        {
            _draftMutationService = draftMutationService;
        }

        public ObjectPlacement CapturePlacement(Obj_Base obj)
        {
            return ScenarioBunkerDraftService.CreatePlacement(obj);
        }

        public bool CanRecordPlacement(out string message)
        {
            return _draftMutationService.CanMutateActiveDraft(out message);
        }

        public bool UpsertPlacement(ObjectPlacement placement)
        {
            return _draftMutationService.TryUpsertPlacement(placement);
        }
    }
}
