using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Map
{
    /// <summary>
    /// Application boundary for projecting semantic map commands onto the live
    /// expedition map. Draft mutation remains owned by <see cref="ScenarioMapDraftService"/>.
    /// </summary>
    internal interface IScenarioMapAuthoringRuntime
    {
        bool OpenVanillaMap();
        bool CloseVanillaMap();
        void CleanupMarkers();
        bool TryCreateSelectionFromWorldPosition(
            float worldX,
            float worldY,
            ScenarioEditorSession session,
            string source,
            out ScenarioMapRegionSelection selection);
        bool TryResolveGrid(
            float worldX,
            float worldY,
            out int gridX,
            out int gridY,
            out float centreWorldX,
            out float centreWorldY);
        bool CanAuthorLocationAtGrid(int gridX, int gridY, out string reason);
        bool CanPaintTerrainAtGrid(int gridX, int gridY, string terrainId, out string reason);
        bool PreviewTerrainDraft(ScenarioEditorSession session, out string reason);
        void RefreshMarkers(ScenarioAuthoringState state, ScenarioEditorSession session);
    }
}
