using System;
using System.Collections.Generic;
using System.Text;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSceneSpritePlacementAuthoringService
    {
        internal sealed class PlacementPickerModel
        {
            public List<ScenarioSpriteCatalogService.SpriteCandidate> VanillaCandidates;
            public List<ScenarioSpriteCatalogService.SpriteCandidate> ModdedCandidates;
            public SceneSpritePlacement ActivePlacement;
            public bool PlacementActive;
            public bool CanPlace;
            public string PlacementSummary;
            public string CompatibilitySummary;
            public string GuidanceMessage;
            public string XmlPathHint;
            public int BlockedPeople;
            public int BlockedInteractiveObjects;
            public int BlockedPathfindingActors;
            public int BlockedGameplayAssets;
        }

        private sealed class ActivePlacementSession
        {
            public ScenarioSpriteCatalogService.SpriteCandidate Candidate;
            public GameObject PreviewObject;
            public SpriteRenderer PreviewRenderer;
            public string ExistingPlacementId;
            public string SortingLayerName;
            public int SortingOrder;
            public PlacementResolution LastResolution;
        }

        private sealed class PlacementResolution
        {
            public Vector3 Position;
            public bool SnapToGrid;
            public int? GridX;
            public int? GridY;
            public bool CanPlace;
            public bool OverrideFit;
        }

        private const string DefaultPlacementSortingLayerName = "Objects";
        private const int DefaultPlacementSortingOrder = 20;

        private readonly ScenarioSceneSpritePlacementCatalogService _catalogService;
        private readonly ScenarioAuthoringHistoryService _historyService;
        private readonly IScenarioSceneSpritePlacementEngine _sceneSpritePlacementEngine;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioObjectIdentityAssignmentService _identityAssignmentService;
        private ActivePlacementSession _activePlacement;

        public static ScenarioSceneSpritePlacementAuthoringService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioSceneSpritePlacementAuthoringService>(); }
        }

        internal ScenarioSceneSpritePlacementAuthoringService(
            ScenarioSceneSpritePlacementCatalogService catalogService,
            ScenarioAuthoringHistoryService historyService,
            IScenarioSceneSpritePlacementEngine sceneSpritePlacementEngine,
            IScenarioEditorService editorService,
            ScenarioObjectIdentityAssignmentService identityAssignmentService)
        {
            _catalogService = catalogService;
            _historyService = historyService;
            _sceneSpritePlacementEngine = sceneSpritePlacementEngine;
            _editorService = editorService;
            _identityAssignmentService = identityAssignmentService;
        }

        public PlacementPickerModel GetPickerModel(ScenarioEditorSession session, ScenarioAuthoringTarget target, string scenarioFilePath)
        {
            PlacementPickerModel model = new PlacementPickerModel
            {
                VanillaCandidates = new List<ScenarioSpriteCatalogService.SpriteCandidate>(),
                ModdedCandidates = new List<ScenarioSpriteCatalogService.SpriteCandidate>(),
                PlacementActive = HasActivePlacement,
                CanPlace = true
            };

            ScenarioSceneSpritePlacementCatalogService.PlacementCatalog catalog = _catalogService.GetCatalog(session, scenarioFilePath);
            if (catalog != null)
            {
                model.VanillaCandidates.AddRange(catalog.VanillaCandidates);
                model.ModdedCandidates.AddRange(catalog.ModdedCandidates);
                model.CompatibilitySummary = catalog.FilterSummary;
                model.GuidanceMessage = catalog.GuidanceMessage;
                model.XmlPathHint = catalog.XmlPathHint;
                model.BlockedPeople = catalog.BlockedPeople;
                model.BlockedInteractiveObjects = catalog.BlockedInteractiveObjects;
                model.BlockedPathfindingActors = catalog.BlockedPathfindingActors;
                model.BlockedGameplayAssets = catalog.BlockedGameplayAssets;
            }

            SceneSpritePlacement activePlacement = FindPlacement(session != null ? session.WorkingDefinition : null, target);
            model.ActivePlacement = activePlacement;
            if (HasActivePlacement)
                model.PlacementSummary = "Placing '" + SafeLabel(_activePlacement.Candidate != null ? _activePlacement.Candidate.Label : null) + "'. Move over the bunker to snap; hold Shift to place freely.";
            else
                model.PlacementSummary = activePlacement != null
                    ? "Placement '" + (activePlacement.Id ?? "<placement>") + "' is selected."
                    : "Selecting a sprite starts a snapped scene sprite placement preview.";
            return model;
        }

        public bool HasActivePlacement
        {
            get { return _activePlacement != null && _activePlacement.PreviewObject != null; }
        }

        public void Invalidate()
        {
            _catalogService.Invalidate();
        }

        public void Reset()
        {
            CancelActivePlacement(null);
        }

        public bool Update(ScenarioAuthoringState state, ScenarioEditorSession session, out string message)
        {
            message = null;
            if (!HasActivePlacement)
                return false;

            if (state == null || session == null || session.WorkingDefinition == null || ScenarioAuthoringRuntimeGuards.IsPlaytesting())
                return CancelActivePlacement("Scene sprite placement cancelled because authoring is no longer in live-edit mode.", out message);

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                return CancelActivePlacement("Scene sprite placement cancelled.", out message);

            ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
            if (inputCapture != null && inputCapture.PointerOverAuthoringUi)
                return false;

            Vector3 worldPoint;
            if (TryGetMouseWorldPoint(out worldPoint))
                UpdatePreview(session.WorkingDefinition, worldPoint, IsPlacementOverrideHeld());

            if (UnityEngine.Input.GetMouseButtonUp(1))
                return CancelActivePlacement("Scene sprite placement cancelled.", out message);

            if (UnityEngine.Input.GetMouseButtonUp(0))
                return CompleteActivePlacement(state, session, out message);

            return false;
        }

        public bool TryHandleAction(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove, StringComparison.Ordinal))
            {
                handled = true;
                return RemovePlacement(state, out message);
            }

            if (!actionId.StartsWith(ScenarioAuthoringActionIds.ActionSceneSpritePlacementApplyPrefix, StringComparison.Ordinal))
                return false;

            handled = true;
            string token = DecodeActionToken(actionId.Substring(ScenarioAuthoringActionIds.ActionSceneSpritePlacementApplyPrefix.Length));
            if (string.IsNullOrEmpty(token))
            {
                message = "Scene sprite selection could not be decoded.";
                return false;
            }

            return StartPlacement(state, token, out message);
        }

        public static string BuildApplyActionId(string token)
        {
            if (string.IsNullOrEmpty(token))
                return ScenarioAuthoringActionIds.ActionSceneSpritePlacementApplyPrefix;

            byte[] bytes = Encoding.UTF8.GetBytes(token);
            return ScenarioAuthoringActionIds.ActionSceneSpritePlacementApplyPrefix + Convert.ToBase64String(bytes);
        }

        private bool StartPlacement(ScenarioAuthoringState state, string token, out string message)
        {
            message = null;
            ScenarioEditorSession session = _editorService.CurrentSession;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "Open a scenario draft before placing a sprite.";
                return false;
            }

            ScenarioAuthoringTarget anchor = state != null ? state.SelectedTarget : null;
            PlacementPickerModel model = GetPickerModel(session, anchor, state != null ? state.ActiveScenarioFilePath : null);
            ScenarioSpriteCatalogService.SpriteCandidate candidate = FindCandidate(model, token);
            if (candidate == null)
            {
                message = model != null && !string.IsNullOrEmpty(model.GuidanceMessage)
                    ? model.GuidanceMessage
                    : "The requested sprite placement candidate was not available.";
                return false;
            }

            if (!candidate.CanPlaceAsSceneSprite)
            {
                message = !string.IsNullOrEmpty(candidate.PlacementGuidance)
                    ? candidate.PlacementGuidance
                    : "That asset cannot be placed as a visual-only scene sprite.";
                return false;
            }

            CancelActivePlacement(null);
            _activePlacement = CreatePlacementSession(candidate, model != null ? model.ActivePlacement : null, anchor);
            if (_activePlacement == null)
            {
                message = "The selected sprite could not be prepared for placement.";
                return false;
            }

            Vector3 worldPoint;
            if (TryGetMouseWorldPoint(out worldPoint))
                UpdatePreview(session.WorkingDefinition, worldPoint, IsPlacementOverrideHeld());

            message = "Placing '" + SafeLabel(candidate.Label) + "'. Move over the bunker to snap; hold Shift to place freely.";
            MMLog.WriteInfo("[ScenarioSceneSpritePlacementAuthoring] " + message);
            return true;
        }

        private ActivePlacementSession CreatePlacementSession(
            ScenarioSpriteCatalogService.SpriteCandidate candidate,
            SceneSpritePlacement existingPlacement,
            ScenarioAuthoringTarget anchor)
        {
            if (candidate == null || candidate.Sprite == null)
                return null;

            GameObject preview = new GameObject("ShelteredAPI.SceneSpritePlacementPreview");
            SpriteRenderer renderer = preview.AddComponent<SpriteRenderer>();
            renderer.sprite = candidate.Sprite;

            ActivePlacementSession session = new ActivePlacementSession
            {
                Candidate = candidate,
                PreviewObject = preview,
                PreviewRenderer = renderer,
                ExistingPlacementId = existingPlacement != null ? existingPlacement.Id : null,
                SortingLayerName = existingPlacement != null ? existingPlacement.SortingLayerName : null,
                SortingOrder = existingPlacement != null ? existingPlacement.SortingOrder : DefaultPlacementSortingOrder
            };

            if (existingPlacement == null || !IsPlacementTarget(anchor, existingPlacement.Id))
                ApplyTargetSorting(session, anchor);
            renderer.sortingLayerName = ResolveSortingLayerName(session.SortingLayerName);
            renderer.sortingOrder = session.SortingOrder;
            SetPreviewColor(session, false, false);
            return session;
        }

        private bool CompleteActivePlacement(ScenarioAuthoringState state, ScenarioEditorSession session, out string message)
        {
            message = null;
            if (!HasActivePlacement || _activePlacement.Candidate == null)
            {
                message = "No scene sprite placement preview is active.";
                return false;
            }

            PlacementResolution resolution = _activePlacement.LastResolution;
            if (resolution == null || !resolution.CanPlace)
            {
                message = "That snapped spot is unavailable for this sprite. Hold Shift to place freely.";
                return true;
            }

            EnsureAssetReferences(session.WorkingDefinition);
            _historyService.RecordVisualChange(session.WorkingDefinition, "Apply scene sprite placement");
            SceneSpritePlacement placement = FindPlacement(session.WorkingDefinition, _activePlacement.ExistingPlacementId);
            bool isNew = placement == null;
            if (isNew)
            {
                placement = CreatePlacement();
                session.WorkingDefinition.AssetReferences.SceneSpritePlacements.Add(placement);
            }

            ApplyCandidateReference(placement, _activePlacement.Candidate);
            ApplyPlacementTransform(placement, resolution);
            placement.SortingLayerName = ResolveSortingLayerName(_activePlacement.SortingLayerName);
            placement.SortingOrder = _activePlacement.SortingOrder;
            AssignMissingIdentity(session);

            string label = SafeLabel(_activePlacement.Candidate.Label);
            string placementId = SafeLabel(placement.Id);
            CancelActivePlacement(null);
            MarkAssetsDirty(session);
            _sceneSpritePlacementEngine.Activate(session.WorkingDefinition, state != null ? state.ActiveScenarioFilePath : null, null);
            Invalidate();

            message = isNew
                ? "Placed scene sprite '" + label + "' as '" + placementId + "'."
                : "Updated placed scene sprite '" + placementId + "'.";
            MMLog.WriteInfo("[ScenarioSceneSpritePlacementAuthoring] " + message);
            return true;
        }

        private void AssignMissingIdentity(ScenarioEditorSession session)
        {
            if (_identityAssignmentService != null)
                _identityAssignmentService.AssignMissingIds(session);
        }

        private bool RemovePlacement(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioEditorSession session = _editorService.CurrentSession;
            if (session == null || session.WorkingDefinition == null || state.SelectedTarget == null)
            {
                message = "No scene sprite placement is selected.";
                return false;
            }

            SceneSpritePlacement placement = FindPlacement(session.WorkingDefinition, state.SelectedTarget);
            if (placement == null)
            {
                message = "The selected target is not an authored scene sprite placement.";
                return false;
            }

            _historyService.RecordVisualChange(session.WorkingDefinition, "Remove scene sprite placement");
            session.WorkingDefinition.AssetReferences.SceneSpritePlacements.Remove(placement);
            MarkAssetsDirty(session);
            _sceneSpritePlacementEngine.Activate(session.WorkingDefinition, state.ActiveScenarioFilePath, null);
            Invalidate();
            message = "Removed scene sprite placement '" + SafeLabel(placement.Id) + "'.";
            MMLog.WriteInfo("[ScenarioSceneSpritePlacementAuthoring] " + message);
            return true;
        }

        private static ScenarioSpriteCatalogService.SpriteCandidate FindCandidate(PlacementPickerModel model, string token)
        {
            ScenarioSpriteCatalogService.SpriteCandidate candidate = FindCandidate(model != null ? model.VanillaCandidates : null, token);
            if (candidate != null)
                return candidate;

            return FindCandidate(model != null ? model.ModdedCandidates : null, token);
        }

        private static ScenarioSpriteCatalogService.SpriteCandidate FindCandidate(List<ScenarioSpriteCatalogService.SpriteCandidate> candidates, string token)
        {
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                if (candidate != null && string.Equals(candidate.Token, token, StringComparison.Ordinal))
                    return candidate;
            }

            return null;
        }

        private static SceneSpritePlacement FindPlacement(ScenarioDefinition definition, ScenarioAuthoringTarget target)
        {
            if (definition == null
                || definition.AssetReferences == null
                || definition.AssetReferences.SceneSpritePlacements == null
                || target == null
                || string.IsNullOrEmpty(target.ScenarioReferenceId))
            {
                return null;
            }

            for (int i = 0; i < definition.AssetReferences.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = definition.AssetReferences.SceneSpritePlacements[i];
                if (placement != null && string.Equals(placement.Id, target.ScenarioReferenceId, StringComparison.OrdinalIgnoreCase))
                    return placement;
            }

            return null;
        }

        private static SceneSpritePlacement FindPlacement(ScenarioDefinition definition, string placementId)
        {
            if (definition == null
                || definition.AssetReferences == null
                || definition.AssetReferences.SceneSpritePlacements == null
                || string.IsNullOrEmpty(placementId))
            {
                return null;
            }

            for (int i = 0; i < definition.AssetReferences.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = definition.AssetReferences.SceneSpritePlacements[i];
                if (placement != null && string.Equals(placement.Id, placementId, StringComparison.OrdinalIgnoreCase))
                    return placement;
            }

            return null;
        }

        private static SceneSpritePlacement CreatePlacement()
        {
            SceneSpritePlacement placement = new SceneSpritePlacement();
            placement.Id = "scene_sprite_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            return placement;
        }

        private static bool IsPlacementTarget(ScenarioAuthoringTarget target, string placementId)
        {
            return target != null
                && !string.IsNullOrEmpty(placementId)
                && string.Equals(target.ScenarioReferenceId, placementId, StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyCandidateReference(SceneSpritePlacement placement, ScenarioSpriteCatalogService.SpriteCandidate candidate)
        {
            placement.SpriteId = null;
            placement.RelativePath = null;
            placement.RuntimeSpriteKey = null;

            if (candidate.SourceKind == ScenarioSpriteCatalogService.SpriteCandidateSourceKind.VanillaRuntime)
                placement.RuntimeSpriteKey = candidate.RuntimeSpriteKey;
            else
            {
                placement.SpriteId = candidate.SpriteId;
                placement.RelativePath = candidate.RelativePath;
            }
        }

        private static void ApplyPlacementTransform(SceneSpritePlacement placement, PlacementResolution resolution)
        {
            if (placement == null || resolution == null)
                return;

            placement.SnapToGrid = resolution.SnapToGrid;
            placement.GridX = resolution.GridX;
            placement.GridY = resolution.GridY;

            placement.Position = new ScenarioVector3
            {
                X = resolution.Position.x,
                Y = resolution.Position.y,
                Z = resolution.Position.z
            };
        }

        private static void ApplyTargetSorting(ActivePlacementSession session, ScenarioAuthoringTarget target)
        {
            if (session == null)
                return;

            SpriteRenderer spriteRenderer = ResolveSpriteRenderer(target);
            if (spriteRenderer != null)
            {
                session.SortingLayerName = spriteRenderer.sortingLayerName;
                session.SortingOrder = spriteRenderer.sortingOrder + 1;
                return;
            }

            session.SortingLayerName = ResolveSortingLayerName(session.SortingLayerName);
            if (session.SortingOrder == 0)
                session.SortingOrder = DefaultPlacementSortingOrder;
        }

        private static string ResolveSortingLayerName(string sortingLayerName)
        {
            return string.IsNullOrEmpty(sortingLayerName) ? DefaultPlacementSortingLayerName : sortingLayerName;
        }

        private static SpriteRenderer ResolveSpriteRenderer(ScenarioAuthoringTarget target)
        {
            if (target == null || target.RuntimeObject == null)
                return null;

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject != null)
                return gameObject.GetComponent<SpriteRenderer>() ?? gameObject.GetComponentInChildren<SpriteRenderer>(true);

            Component component = target.RuntimeObject as Component;
            return component != null ? (component.GetComponent<SpriteRenderer>() ?? component.GetComponentInChildren<SpriteRenderer>(true)) : null;
        }

        private static void EnsureAssetReferences(ScenarioDefinition definition)
        {
            if (definition == null)
                return;

            if (definition.AssetReferences == null)
                definition.AssetReferences = new AssetReferencesDefinition();
        }

        private static void MarkAssetsDirty(ScenarioEditorSession session)
        {
            if (session == null)
                return;

            if (!session.DirtyFlags.Contains(ScenarioDirtySection.Assets))
                session.DirtyFlags.Add(ScenarioDirtySection.Assets);

            session.CurrentEditCategory = ScenarioEditCategory.Assets;
            session.HasAppliedToCurrentWorld = true;
        }

        private void UpdatePreview(ScenarioDefinition definition, Vector3 worldPoint, bool overrideFit)
        {
            if (!HasActivePlacement)
                return;

            PlacementResolution resolution = ResolvePlacement(definition, _activePlacement, worldPoint, overrideFit);
            _activePlacement.LastResolution = resolution;
            _activePlacement.PreviewObject.transform.position = resolution.Position;
            SetPreviewColor(_activePlacement, resolution.CanPlace, resolution.OverrideFit);
        }

        private static PlacementResolution ResolvePlacement(
            ScenarioDefinition definition,
            ActivePlacementSession session,
            Vector3 worldPoint,
            bool overrideFit)
        {
            if (overrideFit)
            {
                worldPoint.z = 0f;
                return new PlacementResolution
                {
                    Position = worldPoint,
                    SnapToGrid = false,
                    GridX = null,
                    GridY = null,
                    CanPlace = true,
                    OverrideFit = true
                };
            }

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            int preferredX;
            int preferredY;
            if (grid == null
                || !grid.isInitialized
                || !ScenarioGridSnapService.TryGetCell(worldPoint, out preferredX, out preferredY))
            {
                worldPoint.z = 0f;
                return new PlacementResolution
                {
                    Position = worldPoint,
                    SnapToGrid = false,
                    GridX = null,
                    GridY = null,
                    CanPlace = false,
                    OverrideFit = false
                };
            }

            if (CanFitAt(definition, session, grid, preferredX, preferredY))
            {
                Vector3 position = ScenarioGridSnapService.GetCellCenterWorldPosition(preferredX, preferredY);
                position.z = 0f;
                return new PlacementResolution
                {
                    Position = position,
                    SnapToGrid = true,
                    GridX = preferredX,
                    GridY = preferredY,
                    CanPlace = true,
                    OverrideFit = false
                };
            }

            Vector3 fallback = ScenarioGridSnapService.GetCellCenterWorldPosition(preferredX, preferredY);
            fallback.z = 0f;
            return new PlacementResolution
            {
                Position = fallback,
                SnapToGrid = true,
                GridX = preferredX,
                GridY = preferredY,
                CanPlace = false,
                OverrideFit = false
            };
        }

        private static bool CanFitAt(
            ScenarioDefinition definition,
            ActivePlacementSession session,
            ShelterRoomGrid grid,
            int gridX,
            int gridY)
        {
            if (session == null || session.Candidate == null || session.Candidate.Sprite == null || grid == null)
                return false;

            ShelterRoomGrid.GridCell cell = grid.GetCell(gridX, gridY);
            if (!IsUsableBunkerCell(cell))
                return false;

            Vector3 position = ScenarioGridSnapService.GetCellCenterWorldPosition(gridX, gridY);
            Bounds bounds = CreateSpriteBounds(session.Candidate.Sprite, position);
            if (!IsWithinGridBounds(grid, bounds))
                return false;

            return !OverlapsExistingPlacement(definition, session.ExistingPlacementId, bounds, gridX, gridY);
        }

        private static bool IsUsableBunkerCell(ShelterRoomGrid.GridCell cell)
        {
            if (cell == null)
                return false;

            return cell.type == ShelterRoomGrid.CellType.Room
                || cell.type == ShelterRoomGrid.CellType.RoomTop
                || cell.type == ShelterRoomGrid.CellType.Surface;
        }

        private static bool IsWithinGridBounds(ShelterRoomGrid grid, Bounds bounds)
        {
            Vector3 origin = grid.transform.position;
            float left = origin.x;
            float right = origin.x + (grid.grid_width * grid.grid_cell_width);
            float top = origin.y;
            float bottom = origin.y - (grid.grid_height * grid.grid_cell_height);
            const float tolerance = 0.001f;
            return bounds.min.x >= left - tolerance
                && bounds.max.x <= right + tolerance
                && bounds.max.y <= top + tolerance
                && bounds.min.y >= bottom - tolerance;
        }

        private static bool OverlapsExistingPlacement(
            ScenarioDefinition definition,
            string existingPlacementId,
            Bounds candidateBounds,
            int gridX,
            int gridY)
        {
            ScenarioSceneSpritePlacementMarker[] markers = UnityEngine.Object.FindObjectsOfType<ScenarioSceneSpritePlacementMarker>();
            for (int i = 0; markers != null && i < markers.Length; i++)
            {
                ScenarioSceneSpritePlacementMarker marker = markers[i];
                if (marker == null || string.Equals(marker.PlacementId, existingPlacementId, StringComparison.OrdinalIgnoreCase))
                    continue;

                SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>() ?? marker.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null && renderer.sprite != null && renderer.bounds.Intersects(candidateBounds))
                    return true;
            }

            if (definition == null || definition.AssetReferences == null || definition.AssetReferences.SceneSpritePlacements == null)
                return false;

            for (int i = 0; i < definition.AssetReferences.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = definition.AssetReferences.SceneSpritePlacements[i];
                if (placement == null || string.Equals(placement.Id, existingPlacementId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (placement.SnapToGrid
                    && placement.GridX.HasValue
                    && placement.GridY.HasValue
                    && placement.GridX.Value == gridX
                    && placement.GridY.Value == gridY)
                {
                    return true;
                }
            }

            return false;
        }

        private static Bounds CreateSpriteBounds(Sprite sprite, Vector3 position)
        {
            Bounds spriteBounds = sprite != null ? sprite.bounds : new Bounds(Vector3.zero, Vector3.zero);
            return new Bounds(position + spriteBounds.center, spriteBounds.size);
        }

        private static void SetPreviewColor(ActivePlacementSession session, bool canPlace, bool overrideFit)
        {
            if (session == null || session.PreviewRenderer == null)
                return;

            if (overrideFit)
                session.PreviewRenderer.color = new Color(1f, 1f, 1f, 0.7f);
            else if (canPlace)
                session.PreviewRenderer.color = new Color(0.35f, 1f, 0.45f, 0.65f);
            else
                session.PreviewRenderer.color = new Color(1f, 0.25f, 0.25f, 0.65f);
        }

        private static bool TryGetMouseWorldPoint(out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            Camera camera = Camera.main;
            if (camera == null)
            {
                Camera[] cameras = Camera.allCameras;
                if (cameras == null || cameras.Length == 0)
                    return false;
                camera = cameras[0];
            }

            Vector3 mouse = UnityEngine.Input.mousePosition;
            mouse.z = camera.orthographic ? Mathf.Abs(camera.transform.position.z) : camera.nearClipPlane;
            worldPoint = camera.ScreenToWorldPoint(mouse);
            return true;
        }

        private static bool IsPlacementOverrideHeld()
        {
            return UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
        }

        private bool CancelActivePlacement(string fallbackMessage)
        {
            string ignored;
            return CancelActivePlacement(fallbackMessage, out ignored);
        }

        private bool CancelActivePlacement(string fallbackMessage, out string message)
        {
            message = fallbackMessage;
            if (_activePlacement == null)
                return !string.IsNullOrEmpty(message);

            GameObject preview = _activePlacement.PreviewObject;
            _activePlacement = null;
            if (preview != null)
                UnityEngine.Object.Destroy(preview);
            return true;
        }

        private static string DecodeActionToken(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        private static string SafeLabel(string value)
        {
            return string.IsNullOrEmpty(value) ? "<scene sprite>" : value;
        }
    }
}
