using System;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Bunker;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Application.Authoring{
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

        public bool ApplyWall(int gridX, int gridY, int wallSpriteIndex, string runtimeSpriteKey)
        {
            return _draftMutationService.TryUpsertRoomEdit(
                gridX,
                gridY,
                delegate(RoomEdit room)
                {
                    room.WallSpriteIndex = wallSpriteIndex >= 0 ? (int?)wallSpriteIndex : null;
                    room.WallRuntimeSpriteKey = runtimeSpriteKey;
                });
        }

        public bool ApplyWire(int gridX, int gridY, int wireSpriteIndex, string runtimeSpriteKey)
        {
            return _draftMutationService.TryUpsertRoomEdit(
                gridX,
                gridY,
                delegate(RoomEdit room)
                {
                    room.WireSpriteIndex = wireSpriteIndex >= 0 ? (int?)wireSpriteIndex : null;
                    room.WireRuntimeSpriteKey = runtimeSpriteKey;
                });
        }
    }
}
