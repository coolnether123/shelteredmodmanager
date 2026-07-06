using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Bunker;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Application.Authoring{
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

        public bool TryFindSinglePlacement(System.Predicate<ObjectPlacement> predicate, out ObjectPlacement placement)
        {
            placement = null;
            return _draftMutationService.TryFindSinglePlacement(predicate, out placement);
        }

        public bool RemovePlacement(System.Predicate<ObjectPlacement> predicate)
        {
            return _draftMutationService.TryRemovePlacement(predicate);
        }
    }
}
