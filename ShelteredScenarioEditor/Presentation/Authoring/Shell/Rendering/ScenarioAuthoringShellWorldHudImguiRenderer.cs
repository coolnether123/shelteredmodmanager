using System;
using UnityEngine;

using ShelteredScenarioEditor.Application.Assets;
using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private void DrawPlacementGridOverlayCore(Rect worldRect)
        {
            bool gridOn = _snapshot == null
                || _snapshot.State == null
                || _snapshot.State.Settings == null
                || _snapshot.State.Settings.GetBool("visuals.show_grid", true);
            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            Camera camera = Camera.main;
            if (!gridOn || grid == null || !grid.isInitialized || camera == null)
                return;

            Color previous = GUI.color;
            GUI.color = _uiContext.Styles.Theme.Palette.WorkspaceMap;
            Vector3 origin = grid.transform.position;
            for (int x = 0; x <= grid.grid_width; x++)
            {
                float worldX = origin.x + (x * grid.grid_cell_width);
                DrawGridLine(
                    camera,
                    new Vector3(worldX, origin.y, 0f),
                    new Vector3(worldX, origin.y - (grid.grid_height * grid.grid_cell_height), 0f),
                    worldRect);
            }
            for (int y = 0; y <= grid.grid_height; y++)
            {
                float worldY = origin.y - (y * grid.grid_cell_height);
                DrawGridLine(
                    camera,
                    new Vector3(origin.x, worldY, 0f),
                    new Vector3(origin.x + (grid.grid_width * grid.grid_cell_width), worldY, 0f),
                    worldRect);
            }
            GUI.color = previous;
        }

        private void DrawGridLine(Camera camera, Vector3 worldStart, Vector3 worldEnd, Rect clipRect)
        {
            Vector3 startScreen = camera.WorldToScreenPoint(worldStart);
            Vector3 endScreen = camera.WorldToScreenPoint(worldEnd);
            if (startScreen.z < 0f || endScreen.z < 0f)
                return;

            Vector2 start = new Vector2(startScreen.x / _activeUiScale, (Screen.height - startScreen.y) / _activeUiScale);
            Vector2 end = new Vector2(endScreen.x / _activeUiScale, (Screen.height - endScreen.y) / _activeUiScale);
            float left = Mathf.Max(clipRect.xMin, Mathf.Min(start.x, end.x));
            float right = Mathf.Min(clipRect.xMax, Mathf.Max(start.x, end.x));
            float top = Mathf.Max(clipRect.yMin, Mathf.Min(start.y, end.y));
            float bottom = Mathf.Min(clipRect.yMax, Mathf.Max(start.y, end.y));
            if (right < left || bottom < top)
                return;

            if (Mathf.Abs(start.x - end.x) < Mathf.Abs(start.y - end.y))
                GUI.DrawTexture(new Rect(left, top, 1f, Math.Max(1f, bottom - top)), Texture2D.whiteTexture);
            else
                GUI.DrawTexture(new Rect(left, top, Math.Max(1f, right - left), 1f), Texture2D.whiteTexture);
        }

        private void DrawPlacementPointerAidCore(float scaledWidth, float scaledHeight)
        {
            string cell;
            string footprint;
            string reason;
            bool canPlace;
            if (!TryResolvePlacementPointerAid(out cell, out footprint, out canPlace, out reason))
                return;

            Vector2 pointer = Event.current != null
                ? Event.current.mousePosition
                : new Vector2(UnityEngine.Input.mousePosition.x / _activeUiScale, (Screen.height - UnityEngine.Input.mousePosition.y) / _activeUiScale);
            const float width = 292f;
            const float height = 52f;
            float x = Mathf.Clamp(pointer.x + 18f, 8f, Math.Max(8f, scaledWidth - width - 8f));
            float y = Mathf.Clamp(pointer.y + 18f, 8f, Math.Max(8f, scaledHeight - height - 8f));
            Rect rect = new Rect(x, y, width, height);
            DrawChromePanel(rect, _uiContext.Styles.Chrome);
            GUI.Label(new Rect(x + 9f, y + 5f, width - 18f, 20f), "Cell " + cell + "  |  Footprint " + footprint, _smallTitleStyle);

            Color previous = GUI.color;
            GUI.color = canPlace ? _uiContext.Styles.Theme.Palette.SemanticReadyStrong : _uiContext.Styles.Theme.Palette.SemanticErrorStrong;
            GUI.Label(new Rect(x + 9f, y + 27f, width - 18f, 19f), ShortenToFit(reason ?? (canPlace ? "Valid target." : "Invalid target."), width - 18f, _mutedTextStyle), _mutedTextStyle);
            GUI.color = previous;
        }

        private bool TryResolvePlacementPointerAid(out string cell, out string footprint, out bool canPlace, out string reason)
        {
            cell = null;
            footprint = null;
            reason = null;
            canPlace = false;
            if (IsBuildPlacementActive())
            {
                ScenarioBuildPlacementAuthoringService.StatusModel model = _buildPlacement.GetStatusModel(
                    _snapshot != null ? _snapshot.State : null,
                    null);
                if (model == null || !model.PlacementActive)
                    return false;
                cell = model.TargetCell ?? "<none>";
                footprint = model.Footprint ?? "1 x 1 (1 cell)";
                canPlace = model.CanPlace.HasValue && model.CanPlace.Value;
                reason = model.ValidationReason;
                return true;
            }

            if (IsSceneSpritePlacementActive())
            {
                ScenarioSceneSpritePlacementAuthoringService.PointerAidModel model = _sceneSpritePlacement.GetPointerAidModel();
                if (model == null)
                    return false;
                cell = model.TargetCell ?? "free";
                footprint = model.Footprint ?? "1 x 1 (1 cell)";
                canPlace = model.CanPlace;
                reason = model.Reason;
                return true;
            }
            return false;
        }

        private void DrawWorldInteractionLegendCore(float scaledWidth, float scaledHeight, Rect placementHudRect)
        {
            const float width = 310f;
            const float height = 24f;
            float y = scaledHeight - StatusHeight - height - 4f;
            Rect rect = new Rect((scaledWidth - width) * 0.5f, y, width, height);
            DrawChromePanel(rect, _uiContext.Styles.Chrome);
            GUI.Label(rect, "Left: select  |  Right: interact  |  Esc: cancel", _uiContext.Styles.HeaderSubtitleText);
        }
    }
}
