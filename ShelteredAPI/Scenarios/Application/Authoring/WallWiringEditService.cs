using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class WallWiringEditService
    {
        private readonly IScenarioDraftMutationService _draftMutationService;

        public WallWiringEditService(IScenarioDraftMutationService draftMutationService)
        {
            _draftMutationService = draftMutationService;
        }

        public bool CanRecordEdit(out string message)
        {
            return _draftMutationService.CanMutateActiveDraft(out message);
        }

        public bool ApplyWall(int gridX, int gridY, int wallSpriteIndex)
        {
            return _draftMutationService.TryUpsertRoomEdit(
                gridX,
                gridY,
                delegate(RoomEdit room) { room.WallSpriteIndex = wallSpriteIndex; });
        }

        public bool ApplyWire(int gridX, int gridY, int wireSpriteIndex)
        {
            return _draftMutationService.TryUpsertRoomEdit(
                gridX,
                gridY,
                delegate(RoomEdit room) { room.WireSpriteIndex = wireSpriteIndex; });
        }
    }
}
