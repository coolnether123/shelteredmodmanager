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
                    room.WallCleared = false;
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
                    room.WireCleared = false;
                });
        }

        public bool ResetWall(int gridX, int gridY)
        {
            return _draftMutationService.TryUpsertRoomEdit(
                gridX,
                gridY,
                delegate(RoomEdit room)
                {
                    room.WallSpriteIndex = null;
                    room.WallRuntimeSpriteKey = null;
                    room.WallCleared = true;
                });
        }

        public bool ResetWire(int gridX, int gridY)
        {
            return _draftMutationService.TryUpsertRoomEdit(
                gridX,
                gridY,
                delegate(RoomEdit room)
                {
                    room.WireSpriteIndex = null;
                    room.WireRuntimeSpriteKey = null;
                    room.WireCleared = true;
                });
        }

        public bool RemoveRoomEdit(int gridX, int gridY)
        {
            return _draftMutationService.TryRemoveRoomEdit(
                gridX,
                gridY,
                null);
        }
    }
}
