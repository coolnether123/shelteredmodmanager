using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class WallWiringEditService
    {
        public void ApplyWall(ScenarioEditorSession session, int gridX, int gridY, int wallSpriteIndex)
        {
            ScenarioBunkerDraftService.UpsertRoomEdit(
                session,
                gridX,
                gridY,
                delegate(RoomEdit room) { room.WallSpriteIndex = wallSpriteIndex; });
        }

        public void ApplyWire(ScenarioEditorSession session, int gridX, int gridY, int wireSpriteIndex)
        {
            ScenarioBunkerDraftService.UpsertRoomEdit(
                session,
                gridX,
                gridY,
                delegate(RoomEdit room) { room.WireSpriteIndex = wireSpriteIndex; });
        }
    }
}
