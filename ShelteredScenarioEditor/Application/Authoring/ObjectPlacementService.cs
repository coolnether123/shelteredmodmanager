using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Bunker;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ObjectPlacementService
    {
        private readonly IScenarioDraftMutationService _draftMutationService;
        private readonly ScenarioPreviewSessionHost _previewHost;

        public ObjectPlacementService(
            IScenarioDraftMutationService draftMutationService,
            ScenarioPreviewSessionHost previewHost)
        {
            _draftMutationService = draftMutationService;
            _previewHost = previewHost;
        }

        public ObjectPlacement CapturePlacement(Obj_Base obj)
        {
            return ScenarioBunkerDraftService.CreatePlacement(obj, _previewHost);
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
