using System;
using System.Collections.Generic;
using System.Text;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Application.Selection;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredScenarioEditor.Infrastructure.Assets;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;
using ShelteredAPI.Scenarios.Shared;
using ShelteredAPI.Scenarios.Public;
namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioAuthoringSelectionService
    {
        private readonly ScenarioEditorCharacterAppearanceService _characterAppearanceService;
        private readonly ScenarioSelectionScopeService _scopeService;
        private readonly ScenarioAuthoringInputCaptureService _inputCapture;
        private readonly ScenarioAuthoringEditorCameraService _editorCamera;
        private readonly ScenarioVanillaInteractionRuntimeService _vanillaInteraction;
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacement;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringSelectionMenuService _selectionMenu;
        private readonly ScenarioHoverVisualService _hoverVisuals;
        private readonly ScenarioAuthoringTargetAdapterRegistry _adapterRegistry = new ScenarioAuthoringTargetAdapterRegistry();
        private const float MinHitTestTolerance = 0.04f;
        private const int PrimaryDomainScore = 420;
        private const int SecondaryDomainScore = 220;
        private const int TertiaryDomainScore = 120;
        private const int SuppressedDomainScore = 80;

        public ScenarioAuthoringSelectionService(
            ScenarioEditorCharacterAppearanceService characterAppearanceService,
            ScenarioSelectionScopeService scopeService,
            ScenarioAuthoringInputCaptureService inputCapture,
            ScenarioAuthoringEditorCameraService editorCamera,
            ScenarioVanillaInteractionRuntimeService vanillaInteraction,
            ScenarioBuildPlacementAuthoringService buildPlacement,
            IScenarioEditorService editorService,
            ScenarioAuthoringSelectionMenuService selectionMenu,
            ScenarioHoverVisualService hoverVisuals)
        {
            _characterAppearanceService = characterAppearanceService;
            _scopeService = scopeService;
            _inputCapture = inputCapture;
            _editorCamera = editorCamera;
            _vanillaInteraction = vanillaInteraction;
            _buildPlacement = buildPlacement;
            _editorService = editorService;
            _selectionMenu = selectionMenu;
            _hoverVisuals = hoverVisuals;
            _adapterRegistry.Register(new DefaultScenarioAuthoringTargetAdapter(_characterAppearanceService));
            _adapterRegistry.Register(new GridCellScenarioAuthoringTargetAdapter());
        }

        public bool Update(ScenarioAuthoringState state)
        {
            if (state == null)
            {
                _hoverVisuals.Clear();
                _selectionMenu.Reset();
                return false;
            }

            ScenarioAuthoringTarget hovered = null;
            List<ScenarioAuthoringTarget> stack = null;
            bool selectionMode = ScenarioAuthoringRuntimeGuards.ShouldResolveSelection();
            bool changed = state.SelectionModeActive != selectionMode;
            state.SelectionModeActive = selectionMode;
            changed |= _scopeService.ClearSelectionIfOutOfScope(state);

            if (!selectionMode)
            {
                if (state.HoveredTarget != null)
                {
                    state.HoveredTarget = null;
                    changed = true;
                }
            }
            else
            {
                bool worldSelectionSuppressedByUi = UICamera.hoveredObject != null
                    || (_inputCapture != null && _inputCapture.ShouldSuppressWorldInputNow());
                if (worldSelectionSuppressedByUi)
                {
                    hovered = null;
                }
                else if (TryResolveCandidateStack(state, out stack))
                {
                    hovered = stack.Count > 0 ? stack[0] : null;

                    if (!AreSameTarget(state.HoveredTarget, hovered))
                    {
                        state.HoveredTarget = hovered != null ? hovered.Copy() : null;
                        changed = true;
                    }
                }
                else
                {
                    if (state.HoveredTarget != null)
                    {
                        state.HoveredTarget = null;
                        changed = true;
                    }
                }

                bool placementActive = _buildPlacement != null && _buildPlacement.HasActivePlacement;
                bool dragPanConsumedClick = _editorCamera != null && _editorCamera.ShouldSuppressSelectionClickThisFrame();
                bool vanillaInteractionClick = IsVanillaInteractionRightClickCandidate();
                if (!placementActive
                    && !dragPanConsumedClick
                    && !vanillaInteractionClick
                    && !worldSelectionSuppressedByUi
                    && ScenarioAuthoringInputActions.IsConfirmSelectionDown()
                    && hovered != null
                    && _scopeService.CanSelectTargetForCurrentStage(state, hovered))
                {
                    changed |= SynchronizeSelectionStack(state, stack);
                    changed |= ApplySelection(state, hovered, IsAddSelectionHeld());
                }
            }

            if (IsVanillaInteractionRightClickCandidate())
            {
                if (TryOpenVanillaInteractionFromRightClick())
                {
                    _hoverVisuals.UpdateFromState(state);
                    _selectionMenu.Sync(state);
                    return true;
                }

                _hoverVisuals.UpdateFromState(state);
                _selectionMenu.Sync(state);
                return changed;
            }

            if (ScenarioAuthoringInputActions.IsClearSelectionDown())
            {
                ScenarioAuthoringTarget menuTarget = state.SelectedTarget ?? hovered;
                if (selectionMode && menuTarget != null)
                {
                    _selectionMenu.OpenMenu(state, menuTarget);
                    if (state.SelectionStack != null && state.SelectionStack.Count > 1)
                        state.StatusMessage = "Selection stack opened with " + state.SelectionStack.Count + " candidates.";
                    changed = true;
                }
                else if (state.SelectedTarget != null)
                {
                    state.SelectedTarget = null;
                    state.MultiSelection.Clear();
                    ClearSelectionStack(state);
                    state.StatusMessage = "Selection cleared.";
                    changed = true;
                }
            }

            _hoverVisuals.UpdateFromState(state);
            _selectionMenu.Sync(state);
            return changed;
        }

        // Explicit selection seam for trusted integration tooling. It deliberately
        // reuses the same target adapters and ApplySelection path as world clicks.
        public bool TrySelectRuntimeObject(ScenarioAuthoringState state, GameObject gameObject, out ScenarioAuthoringTarget target, out string message)
        {
            target = null;
            message = null;
            if (state == null)
            {
                message = "Scenario authoring is not active.";
                return false;
            }
            if (gameObject == null)
            {
                message = "The requested world object was not found.";
                return false;
            }

            ScenarioAuthoringTargetContext context = new ScenarioAuthoringTargetContext
            {
                GameObject = gameObject,
                WorldPoint = gameObject.transform.position
            };
            if (!_adapterRegistry.TryCreateTarget(context, out target) || target == null)
            {
                message = "The requested world object is not editable.";
                return false;
            }
            if (IsGlobalBackdropTarget(state, target) || !_scopeService.CanSelectTargetForCurrentStage(state, target))
            {
                message = "The requested world object is outside the current authoring scope.";
                target = null;
                return false;
            }

            state.HoveredTarget = target.Copy();
            ApplySelection(state, target, false);
            _hoverVisuals.UpdateFromState(state);
            _selectionMenu.Sync(state);
            message = state.StatusMessage;
            return true;
        }

        /// <summary>
        /// Creates the canonical authoring target for a live object without changing selection state.
        /// Catalogs and hierarchy projections use this seam so target identity and classification do
        /// not drift from pointer selection.
        /// </summary>
        public bool TryCreateTarget(GameObject gameObject, out ScenarioAuthoringTarget target)
        {
            target = null;
            if (gameObject == null)
                return false;

            return _adapterRegistry.TryCreateTarget(new ScenarioAuthoringTargetContext
            {
                GameObject = gameObject,
                WorldPoint = gameObject.transform.position
            }, out target) && target != null;
        }

        public bool TryResolveTarget(ScenarioAuthoringState state, string targetId, out ScenarioAuthoringTarget target)
        {
            target = null;
            for (int i = 0; state != null && state.SelectionStack != null && i < state.SelectionStack.Count; i++)
            {
                ScenarioAuthoringTarget stackTarget = state.SelectionStack[i];
                if (stackTarget != null && string.Equals(stackTarget.Id, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    target = stackTarget.Copy();
                    return true;
                }
            }

            int separator = targetId != null ? targetId.LastIndexOf(':') : -1;
            int instanceId;
            if (separator <= 0
                || separator >= targetId.Length - 1
                || !int.TryParse(targetId.Substring(separator + 1), out instanceId))
            {
                return false;
            }

            GameObject[] objects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            for (int i = 0; objects != null && i < objects.Length; i++)
            {
                GameObject gameObject = objects[i];
                ScenarioAuthoringTarget candidate;
                if (gameObject == null
                    || gameObject.transform == null
                    || gameObject.transform.GetInstanceID() != instanceId
                    || !TryCreateTarget(gameObject, out candidate))
                {
                    continue;
                }

                if (!string.Equals(candidate.Id, targetId, StringComparison.OrdinalIgnoreCase))
                    continue;

                target = candidate;
                return true;
            }

            return false;
        }

        public bool TryApplyDirectSelection(
            ScenarioAuthoringState state,
            ScenarioAuthoringTarget target,
            out string message)
        {
            message = null;
            if (state == null || target == null)
            {
                message = "The requested authoring target is unavailable.";
                return false;
            }

            if (_scopeService != null && !_scopeService.CanSelectTargetForCurrentStage(state, target, out message))
                return false;

            ApplySelection(state, target, false);
            state.HoveredTarget = target.Copy();
            ClearSelectionStack(state);
            message = state.StatusMessage;
            return true;
        }

        private bool IsVanillaInteractionRightClickCandidate()
        {
            if (!UnityEngine.Input.GetMouseButtonDown(1)
                && !UnityEngine.Input.GetMouseButtonUp(1)
                && !UnityEngine.Input.GetMouseButton(1))
                return false;

            try
            {
                return _vanillaInteraction != null && _vanillaInteraction.CanStartWorldInteraction();
            }
            catch
            {
                return false;
            }
        }

        private bool TryOpenVanillaInteractionFromRightClick()
        {
            if (!UnityEngine.Input.GetMouseButtonUp(1))
                return false;

            try
            {
                return _vanillaInteraction != null && _vanillaInteraction.TryOpenWorldInteractionUnderPointer();
            }
            catch
            {
                return false;
            }
        }

        private bool TryResolveCandidateStack(ScenarioAuthoringState state, out List<ScenarioAuthoringTarget> targets)
        {
            targets = null;
            if (UICamera.hoveredObject != null)
                return false;
            if (_inputCapture != null && _inputCapture.ShouldSuppressWorldInputNow())
                return false;

            Camera camera = Camera.main;
            if (camera == null)
            {
                Camera[] cameras = Camera.allCameras;
                if (cameras == null || cameras.Length == 0)
                    return false;

                camera = cameras[0];
            }

            Ray ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            Vector3 worldPoint = ResolveMouseWorldPoint(camera);
            float hitTolerance = ResolveHitTestTolerance(camera);
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
            List<SelectionCandidate> candidates = new List<SelectionCandidate>();
            if (hits != null && hits.Length > 0)
            {
                Array.Sort(hits, CompareRaycastHit);
                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];
                    Collider collider = hit.collider;
                    GameObject gameObject = collider != null ? collider.gameObject : null;
                    if (gameObject == null)
                        continue;

                    ScenarioAuthoringTargetContext context = new ScenarioAuthoringTargetContext
                    {
                        Camera = camera,
                        Ray = ray,
                        Hit = hit,
                        Collider = collider,
                        GameObject = gameObject,
                        WorldPoint = hit.point
                    };

                    AddCandidate(state, candidates, context, 300, hit.distance, null);
                }
            }

            try
            {
                Collider2D[] hits2D = Physics2D.OverlapCircleAll(new Vector2(worldPoint.x, worldPoint.y), hitTolerance);
                for (int i = 0; hits2D != null && i < hits2D.Length; i++)
                {
                    Collider2D collider = hits2D[i];
                    GameObject gameObject = collider != null ? collider.gameObject : null;
                    if (gameObject == null)
                        continue;

                    ScenarioAuthoringTargetContext context = new ScenarioAuthoringTargetContext
                    {
                        Camera = camera,
                        Ray = ray,
                        Collider = null,
                        GameObject = gameObject,
                        WorldPoint = worldPoint
                    };

                    AddCandidate(state, candidates, context, 260, 0f, null);
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug("[ScenarioAuthoringSelection] 2D overlap check failed: " + ex.Message);
            }

            AddSpriteRendererCandidates(state, candidates, camera, ray, worldPoint, hitTolerance);
            AddAuthoredStructuralCandidates(state, candidates, worldPoint);

            ScenarioAuthoringTargetContext gridContext = new ScenarioAuthoringTargetContext
            {
                Camera = camera,
                Ray = ray,
                WorldPoint = worldPoint
            };
            AddCandidate(state, candidates, gridContext, 0, 0f, null);

            targets = BuildSortedTargets(state, candidates);
            return targets != null && targets.Count > 0;
        }

        private void AddCandidate(
            ScenarioAuthoringState state,
            List<SelectionCandidate> candidates,
            ScenarioAuthoringTargetContext context,
            int sourceRank,
            float distance,
            SpriteRenderer spriteRenderer)
        {
            ScenarioAuthoringTarget target;
            if (!_adapterRegistry.TryCreateTarget(context, out target) || target == null)
                return;
            if (IsGlobalBackdropTarget(state, target))
                return;
            if (!_scopeService.CanSelectTargetForCurrentStage(state, target))
                return;

            candidates.Add(new SelectionCandidate
            {
                Target = target,
                SourceRank = sourceRank,
                Distance = distance,
                ToolScore = ScoreDomainRelevance(state, target),
                StageScore = ScoreStageRelevance(state, target),
                KindScore = ScoreKind(target.Kind),
                SortingLayer = spriteRenderer != null ? SortingLayer.GetLayerValueFromID(spriteRenderer.sortingLayerID) : ResolveSortingLayer(target),
                SortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder : ResolveSortingOrder(target),
                Z = target.WorldPosition.z,
                Area = spriteRenderer != null ? ResolveArea(spriteRenderer.bounds) : ResolveArea(target)
            });
        }

        private void AddSpriteRendererCandidates(
            ScenarioAuthoringState state,
            List<SelectionCandidate> candidates,
            Camera camera,
            Ray ray,
            Vector3 worldPoint,
            float hitTolerance)
        {
            SpriteRenderer[] spriteRenderers = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
            for (int i = 0; spriteRenderers != null && i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null
                    || spriteRenderer.sprite == null
                    || !spriteRenderer.enabled
                    || spriteRenderer.gameObject == null
                    || !spriteRenderer.gameObject.activeInHierarchy
                    || !ContainsPoint2D(spriteRenderer.bounds, worldPoint, hitTolerance))
                {
                    continue;
                }

                ScenarioAuthoringTargetContext context = new ScenarioAuthoringTargetContext
                {
                    Camera = camera,
                    Ray = ray,
                    GameObject = spriteRenderer.gameObject,
                    WorldPoint = worldPoint
                };
                AddCandidate(state, candidates, context, 220, 0f, spriteRenderer);
            }
        }

        private void AddAuthoredStructuralCandidates(
            ScenarioAuthoringState state,
            List<SelectionCandidate> candidates,
            Vector3 worldPoint)
        {
            int pointerGridX;
            int pointerGridY;
            if (!ShelteredScenarioRuntime.TryGetShelterGridCell(worldPoint, out pointerGridX, out pointerGridY))
                return;

            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            BunkerEditsDefinition bunkerEdits = session != null && session.WorkingDefinition != null
                ? session.WorkingDefinition.BunkerEdits
                : null;
            List<ObjectPlacement> placements = bunkerEdits != null ? bunkerEdits.ObjectPlacements : null;
            for (int i = 0; placements != null && i < placements.Count; i++)
            {
                ObjectPlacement placement = placements[i];
                int gridX;
                int gridY;
                if (!TryGetStructuralPlacementGrid(placement, out gridX, out gridY)
                    || gridX != pointerGridX
                    || gridY != pointerGridY)
                {
                    continue;
                }

                ScenarioAuthoringTarget target = BuildAuthoredStructuralTarget(placement, gridX, gridY);
                if (target == null || !_scopeService.CanSelectTargetForCurrentStage(state, target))
                    continue;

                candidates.Add(new SelectionCandidate
                {
                    Target = target,
                    SourceRank = 240,
                    Distance = 0f,
                    ToolScore = ScoreDomainRelevance(state, target),
                    StageScore = ScoreStageRelevance(state, target),
                    KindScore = ScoreKind(target.Kind),
                    SortingLayer = ResolveSortingLayer(target),
                    SortingOrder = ResolveSortingOrder(target),
                    Z = target.WorldPosition.z,
                    Area = ResolveArea(target)
                });
            }
        }

        private static bool TryGetStructuralPlacementGrid(ObjectPlacement placement, out int gridX, out int gridY)
        {
            gridX = -1;
            gridY = -1;
            ScenarioPlacementDefinitionKind kind;
            return placement != null
                && ScenarioPlacementDefinitions.TryParseSpecialKind(placement.DefinitionReference, out kind)
                && ScenarioPropertyBag.TryGetInt(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyGridX, out gridX)
                && ScenarioPropertyBag.TryGetInt(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyGridY, out gridY);
        }

        private static ScenarioAuthoringTarget BuildAuthoredStructuralTarget(ObjectPlacement placement, int gridX, int gridY)
        {
            ScenarioPlacementDefinitionKind placementKind;
            if (placement == null || !ScenarioPlacementDefinitions.TryParseSpecialKind(placement.DefinitionReference, out placementKind))
                return null;

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            ShelterRoomGrid.GridCell cell = grid != null ? grid.GetCell(gridX, gridY) : null;
            GameObject cellObject = cell != null ? cell.prefab : null;
            Vector3 cellCenter = ShelteredScenarioRuntime.GetShelterGridCellCenter(gridX, gridY);
            ScenarioAuthoringTargetKind targetKind = ResolveStructuralTargetKind(placementKind);
            string label = FormatStructuralTargetLabel(placementKind, gridX, gridY);
            string reference = placement.DefinitionReference ?? placementKind.ToString();

            return new ScenarioAuthoringTarget
            {
                Id = "structure:" + reference + ":" + gridX + ":" + gridY,
                Kind = targetKind,
                DisplayName = label,
                Description = "Authored " + label.ToLowerInvariant() + ".",
                AdapterId = "ShelteredAPI.AuthoredStructure",
                GameObjectName = cellObject != null ? cellObject.name : label,
                TransformPath = cellObject != null ? ShelteredScenarioRuntime.GetTransformPath(cellObject.transform) : ("ShelterGrid/" + gridX + "/" + gridY),
                RuntimeObject = cellObject,
                HighlightObject = cellObject,
                WorldPosition = cellCenter,
                GridX = gridX,
                GridY = gridY,
                SupportsInspect = true,
                SupportsReplace = true
            };
        }

        private static ScenarioAuthoringTargetKind ResolveStructuralTargetKind(ScenarioPlacementDefinitionKind kind)
        {
            switch (kind)
            {
                case ScenarioPlacementDefinitionKind.Room:
                    return ScenarioAuthoringTargetKind.Room;
                case ScenarioPlacementDefinitionKind.RoomLight:
                    return ScenarioAuthoringTargetKind.Light;
                case ScenarioPlacementDefinitionKind.Ladder:
                    return ScenarioAuthoringTargetKind.Tile;
                default:
                    return ScenarioAuthoringTargetKind.Tile;
            }
        }

        private static string FormatStructuralTargetLabel(ScenarioPlacementDefinitionKind kind, int gridX, int gridY)
        {
            string name;
            switch (kind)
            {
                case ScenarioPlacementDefinitionKind.Room:
                    name = "Room";
                    break;
                case ScenarioPlacementDefinitionKind.Ladder:
                    name = "Ladder";
                    break;
                case ScenarioPlacementDefinitionKind.RoomLight:
                    name = "Room Light";
                    break;
                default:
                    name = "Structure";
                    break;
            }

            return name + " " + gridX + "," + gridY;
        }

        private static List<ScenarioAuthoringTarget> BuildSortedTargets(ScenarioAuthoringState state, List<SelectionCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return new List<ScenarioAuthoringTarget>();

            Dictionary<string, SelectionCandidate> byId = new Dictionary<string, SelectionCandidate>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < candidates.Count; i++)
            {
                SelectionCandidate candidate = candidates[i];
                if (candidate == null || candidate.Target == null || string.IsNullOrEmpty(candidate.Target.Id))
                    continue;

                SelectionCandidate existing;
                if (byId.TryGetValue(candidate.Target.Id, out existing) && CompareCandidates(candidate, existing) >= 0)
                    continue;

                byId[candidate.Target.Id] = candidate;
            }

            List<SelectionCandidate> sorted = new List<SelectionCandidate>(byId.Values);
            sorted.Sort(CompareCandidates);
            List<ScenarioAuthoringTarget> result = new List<ScenarioAuthoringTarget>();
            for (int i = 0; i < sorted.Count; i++)
                result.Add(sorted[i].Target);
            return result;
        }

        private static int CompareCandidates(SelectionCandidate left, SelectionCandidate right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int leftScore = left.TotalScore;
            int rightScore = right.TotalScore;
            if (leftScore != rightScore)
                return rightScore.CompareTo(leftScore);
            if (left.SourceRank != right.SourceRank)
                return right.SourceRank.CompareTo(left.SourceRank);
            int distance = left.Distance.CompareTo(right.Distance);
            if (distance != 0)
                return distance;
            if (left.SortingLayer != right.SortingLayer)
                return right.SortingLayer.CompareTo(left.SortingLayer);
            if (left.SortingOrder != right.SortingOrder)
                return right.SortingOrder.CompareTo(left.SortingOrder);
            int z = right.Z.CompareTo(left.Z);
            if (z != 0)
                return z;
            return left.Area.CompareTo(right.Area);
        }

        private static int ScoreDomainRelevance(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            if (state == null || target == null)
                return 0;

            switch (state.ActiveTool)
            {
                case ScenarioAuthoringTool.Objects:
                    if (target.Kind == ScenarioAuthoringTargetKind.Character)
                        return PrimaryDomainScore;
                    if (target.Kind == ScenarioAuthoringTargetKind.PlaceableObject || target.Kind == ScenarioAuthoringTargetKind.Vehicle)
                        return PrimaryDomainScore;
                    if (target.Kind == ScenarioAuthoringTargetKind.Light || IsNamedLike(target, "ladder"))
                        return TertiaryDomainScore;
                    if (target.Kind == ScenarioAuthoringTargetKind.Room || target.Kind == ScenarioAuthoringTargetKind.Tile)
                        return SuppressedDomainScore;
                    if (target.Kind == ScenarioAuthoringTargetKind.Wire)
                        return 70;
                    if (target.Kind == ScenarioAuthoringTargetKind.Wall)
                        return 60;
                    return 0;
                case ScenarioAuthoringTool.Shelter:
                    if (target.Kind == ScenarioAuthoringTargetKind.Room
                        || target.Kind == ScenarioAuthoringTargetKind.Tile
                        || target.Kind == ScenarioAuthoringTargetKind.Light
                        || IsNamedLike(target, "ladder"))
                    {
                        return PrimaryDomainScore;
                    }
                    if (target.Kind == ScenarioAuthoringTargetKind.Wall || target.Kind == ScenarioAuthoringTargetKind.Wire)
                        return SecondaryDomainScore;
                    return 0;
                case ScenarioAuthoringTool.Wiring:
                    if (target.Kind == ScenarioAuthoringTargetKind.Wall || target.Kind == ScenarioAuthoringTargetKind.Wire)
                        return PrimaryDomainScore;
                    if (target.Kind == ScenarioAuthoringTargetKind.Light)
                        return SecondaryDomainScore;
                    if (target.Kind == ScenarioAuthoringTargetKind.Room || target.Kind == ScenarioAuthoringTargetKind.Tile)
                        return TertiaryDomainScore;
                    return 0;
                case ScenarioAuthoringTool.Assets:
                    if (target.Kind == ScenarioAuthoringTargetKind.SceneSprite || target.Kind == ScenarioAuthoringTargetKind.Background)
                        return PrimaryDomainScore;
                    return target.SupportsReplace ? 80 : 0;
                case ScenarioAuthoringTool.Family:
                case ScenarioAuthoringTool.People:
                    return target.Kind == ScenarioAuthoringTargetKind.Character ? 80 : 0;
                default:
                    return 20;
            }
        }

        private static bool IsGlobalBackdropTarget(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            if (state == null || target == null || state.ActiveStage == ScenarioStageKind.BunkerBackground)
                return false;

            if (target.Kind == ScenarioAuthoringTargetKind.Background)
                return true;

            return target.Kind == ScenarioAuthoringTargetKind.SceneSprite
                && IsNamedLike(target, "sun", "sky", "backdrop", "farback", "far back");
        }

        private static bool IsNamedLike(ScenarioAuthoringTarget target, params string[] parts)
        {
            if (target == null || parts == null)
                return false;

            return ContainsSelectionText(target.DisplayName, parts)
                || ContainsSelectionText(target.GameObjectName, parts)
                || ContainsSelectionText(target.TransformPath, parts)
                || ContainsSelectionText(target.Description, parts);
        }

        private static int ScoreStageRelevance(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            if (state == null || target == null)
                return 0;

            if (state.ActiveStage == ScenarioStageKind.BunkerInside
                && (target.Kind == ScenarioAuthoringTargetKind.PlaceableObject || target.Kind == ScenarioAuthoringTargetKind.Character || target.Kind == ScenarioAuthoringTargetKind.SceneSprite))
                return 40;
            if (state.ActiveStage == ScenarioStageKind.BunkerSurface
                && (target.Kind == ScenarioAuthoringTargetKind.Room || target.Kind == ScenarioAuthoringTargetKind.Tile || target.Kind == ScenarioAuthoringTargetKind.Light))
                return 40;
            if (state.ActiveStage == ScenarioStageKind.BunkerBackground
                && (target.Kind == ScenarioAuthoringTargetKind.Background || target.Kind == ScenarioAuthoringTargetKind.Wall || target.Kind == ScenarioAuthoringTargetKind.Wire))
                return 40;
            return 0;
        }

        private static int ScoreKind(ScenarioAuthoringTargetKind kind)
        {
            switch (kind)
            {
                case ScenarioAuthoringTargetKind.Character:
                    return 70;
                case ScenarioAuthoringTargetKind.PlaceableObject:
                    return 65;
                case ScenarioAuthoringTargetKind.SceneSprite:
                    return 60;
                case ScenarioAuthoringTargetKind.Wire:
                case ScenarioAuthoringTargetKind.Light:
                case ScenarioAuthoringTargetKind.Wall:
                    return 55;
                case ScenarioAuthoringTargetKind.Room:
                    return 40;
                case ScenarioAuthoringTargetKind.Tile:
                    return 20;
                case ScenarioAuthoringTargetKind.Background:
                    return 10;
                default:
                    return 30;
            }
        }

        private static int ResolveSortingLayer(ScenarioAuthoringTarget target)
        {
            SpriteRenderer spriteRenderer = ResolveSpriteRenderer(target);
            return spriteRenderer != null ? SortingLayer.GetLayerValueFromID(spriteRenderer.sortingLayerID) : 0;
        }

        private static int ResolveSortingOrder(ScenarioAuthoringTarget target)
        {
            SpriteRenderer spriteRenderer = ResolveSpriteRenderer(target);
            return spriteRenderer != null ? spriteRenderer.sortingOrder : 0;
        }

        private static SpriteRenderer ResolveSpriteRenderer(ScenarioAuthoringTarget target)
        {
            GameObject gameObject = target != null ? target.RuntimeObject as GameObject : null;
            return gameObject != null ? (gameObject.GetComponent<SpriteRenderer>() ?? gameObject.GetComponentInChildren<SpriteRenderer>(true)) : null;
        }

        private static float ResolveArea(ScenarioAuthoringTarget target)
        {
            SpriteRenderer spriteRenderer = ResolveSpriteRenderer(target);
            return spriteRenderer != null ? ResolveArea(spriteRenderer.bounds) : 999999f;
        }

        private static float ResolveArea(Bounds bounds)
        {
            return Mathf.Abs(bounds.size.x * bounds.size.y);
        }

        private static bool ContainsPoint2D(Bounds bounds, Vector3 point, float tolerance)
        {
            float padding = Mathf.Max(0f, tolerance);
            return point.x >= bounds.min.x - padding
                && point.x <= bounds.max.x + padding
                && point.y >= bounds.min.y - padding
                && point.y <= bounds.max.y + padding;
        }

        private static bool SynchronizeSelectionStack(ScenarioAuthoringState state, List<ScenarioAuthoringTarget> targets)
        {
            string signature = BuildStackSignature(targets);
            bool changed = !string.Equals(state.SelectionStackSignature, signature, StringComparison.Ordinal);
            if (changed)
            {
                state.SelectionStack.Clear();
                for (int i = 0; targets != null && i < targets.Count; i++)
                {
                    if (targets[i] != null)
                        state.SelectionStack.Add(targets[i].Copy());
                }

                state.SelectionStackSignature = signature;
                state.ActiveSelectionStackIndex = 0;
                return true;
            }

            if (state.SelectionStack.Count != (targets != null ? targets.Count : 0))
            {
                state.SelectionStack.Clear();
                for (int i = 0; targets != null && i < targets.Count; i++)
                {
                    if (targets[i] != null)
                        state.SelectionStack.Add(targets[i].Copy());
                }
                return true;
            }

            state.ActiveSelectionStackIndex = Mathf.Clamp(state.ActiveSelectionStackIndex, 0, Math.Max(0, state.SelectionStack.Count - 1));
            return false;
        }

        private static void ClearSelectionStack(ScenarioAuthoringState state)
        {
            if (state == null)
                return;
            state.SelectionStack.Clear();
            state.ActiveSelectionStackIndex = 0;
            state.SelectionStackSignature = null;
            state.SelectionStackExpanded = false;
        }

        private static bool CycleSelectionStack(ScenarioAuthoringState state, int delta)
        {
            if (state == null || state.SelectionStack == null || state.SelectionStack.Count <= 1)
                return false;

            int count = state.SelectionStack.Count;
            int next = state.ActiveSelectionStackIndex + delta;
            while (next < 0)
                next += count;
            next = next % count;
            if (next == state.ActiveSelectionStackIndex)
                return false;

            state.ActiveSelectionStackIndex = next;
            ScenarioAuthoringTarget active = state.SelectionStack[next];
            if (state.SelectedTarget != null && active != null)
            {
                state.SelectedTarget = active.Copy();
                state.MultiSelection.Clear();
                state.MultiSelection.Add(active.Copy());
            }

            state.StatusMessage = "Selection stack " + (next + 1) + "/" + count + ": " + (active != null ? active.DisplayName : "Unknown") + ".";
            return true;
        }

        private static string BuildStackSignature(List<ScenarioAuthoringTarget> targets)
        {
            if (targets == null || targets.Count == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < targets.Count; i++)
            {
                ScenarioAuthoringTarget target = targets[i];
                if (target == null)
                    continue;

                if (builder.Length > 0)
                    builder.Append("|");
                builder.Append(target.Id);
            }

            return builder.ToString();
        }

        private static bool ApplySelection(ScenarioAuthoringState state, ScenarioAuthoringTarget hovered, bool additive)
        {
            if (state == null || hovered == null)
                return false;

            if (additive)
            {
                for (int i = 0; i < state.MultiSelection.Count; i++)
                {
                    if (AreSameTarget(state.MultiSelection[i], hovered))
                    {
                        state.StatusMessage = hovered.DisplayName + " is already in the selection.";
                        return true;
                    }
                }

                if (state.SelectedTarget != null && state.MultiSelection.Count == 0)
                    state.MultiSelection.Add(state.SelectedTarget.Copy());

                state.MultiSelection.Add(hovered.Copy());
                state.SelectedTarget = hovered.Copy();
                state.StatusMessage = "Added " + hovered.DisplayName + " to the selection.";
                return true;
            }

            state.SelectedTarget = hovered.Copy();
            state.MultiSelection.Clear();
            state.MultiSelection.Add(hovered.Copy());
            state.StatusMessage = "Selected " + hovered.DisplayName + ".";
            return true;
        }

        private static bool IsAddSelectionHeld()
        {
            return UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
        }

        private static Vector3 ResolveMouseWorldPoint(Camera camera)
        {
            Vector3 mouse = UnityEngine.Input.mousePosition;
            mouse.z = camera.orthographic ? Mathf.Abs(camera.transform.position.z) : camera.nearClipPlane;
            return camera.ScreenToWorldPoint(mouse);
        }

        private static float ResolveHitTestTolerance(Camera camera)
        {
            if (camera == null || !camera.orthographic || camera.pixelHeight <= 0)
                return MinHitTestTolerance;

            float worldUnitsPerPixel = (camera.orthographicSize * 2f) / camera.pixelHeight;
            return Mathf.Max(MinHitTestTolerance, worldUnitsPerPixel * 3f);
        }

        private static bool TryResolveSpriteContext(Camera camera, Ray ray, Vector3 worldPoint, out ScenarioAuthoringTargetContext context)
        {
            context = null;
            GameObject bestObject = null;
            int bestCategory = int.MinValue;
            int bestLayerValue = int.MinValue;
            int bestOrder = int.MinValue;
            float bestZ = float.MinValue;
            float bestArea = float.MaxValue;

            SpriteRenderer[] spriteRenderers = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
            for (int i = 0; spriteRenderers != null && i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null
                    || spriteRenderer.sprite == null
                    || !spriteRenderer.enabled
                    || spriteRenderer.gameObject == null
                    || !spriteRenderer.gameObject.activeInHierarchy
                    || !ContainsPoint2D(spriteRenderer.bounds, worldPoint, ResolveHitTestTolerance(camera)))
                {
                    continue;
                }

                int category = EstimateSelectionCategory(spriteRenderer.gameObject);
                int layerValue = SortingLayer.GetLayerValueFromID(spriteRenderer.sortingLayerID);
                int order = spriteRenderer.sortingOrder;
                float z = -spriteRenderer.transform.position.z;
                Bounds bounds = spriteRenderer.bounds;
                float area = Mathf.Abs(bounds.size.x * bounds.size.y);
                if (bestObject != null
                    && (category < bestCategory
                        || (category == bestCategory && layerValue < bestLayerValue)
                        || (category == bestCategory && layerValue == bestLayerValue && order < bestOrder)
                        || (category == bestCategory && layerValue == bestLayerValue && order == bestOrder && z < bestZ)
                        || (category == bestCategory && layerValue == bestLayerValue && order == bestOrder && Mathf.Approximately(z, bestZ) && area >= bestArea)))
                    continue;

                bestObject = spriteRenderer.gameObject;
                bestCategory = category;
                bestLayerValue = layerValue;
                bestOrder = order;
                bestZ = z;
                bestArea = area;
            }

            if (bestObject == null)
                return false;

            context = new ScenarioAuthoringTargetContext
            {
                Camera = camera,
                Ray = ray,
                GameObject = bestObject,
                WorldPoint = worldPoint
            };
            return true;
        }

        private static int EstimateSelectionCategory(GameObject gameObject)
        {
            if (gameObject == null)
                return 0;

            if (gameObject.GetComponentInParent<FamilyMember>() != null
                || gameObject.GetComponentInParent<NpcVisitor>() != null
                || gameObject.GetComponentInParent<BaseCharacter>() != null)
            {
                return 100;
            }

            if (gameObject.GetComponentInParent<Obj_Base>() != null)
                return 90;

            string path = BuildSelectionPath(gameObject.transform).ToLowerInvariant();
            if (ContainsSelectionText(path, "background", "scenery", "sky", "terrain", "backdrop"))
                return 10;
            if (ContainsSelectionText(path, "tile", "grid"))
                return 5;

            return 50;
        }

        private static string BuildSelectionPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static bool ContainsSelectionText(string value, params string[] parts)
        {
            if (string.IsNullOrEmpty(value) || parts == null)
                return false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]) && value.IndexOf(parts[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static int CompareRaycastHit(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }

        private static bool AreSameTarget(ScenarioAuthoringTarget left, ScenarioAuthoringTarget right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;
            return string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class SelectionCandidate
        {
            public ScenarioAuthoringTarget Target;
            public int SourceRank;
            public int ToolScore;
            public int StageScore;
            public int KindScore;
            public int SortingLayer;
            public int SortingOrder;
            public float Distance;
            public float Z;
            public float Area;

            public int TotalScore
            {
                get { return ToolScore + StageScore + KindScore; }
            }
        }

        private sealed class ScenarioAuthoringTargetAdapterRegistry
        {
            private readonly List<IScenarioAuthoringTargetAdapter> _adapters = new List<IScenarioAuthoringTargetAdapter>();

            public void Register(IScenarioAuthoringTargetAdapter adapter)
            {
                if (adapter == null)
                    return;

                _adapters.Add(adapter);
                _adapters.Sort(CompareAdapters);
            }

            public bool TryCreateTarget(ScenarioAuthoringTargetContext context, out ScenarioAuthoringTarget target)
            {
                target = null;
                for (int i = 0; i < _adapters.Count; i++)
                {
                    if (_adapters[i] != null && _adapters[i].TryCreateTarget(context, out target) && target != null)
                        return true;
                }

                return false;
            }

            private static int CompareAdapters(IScenarioAuthoringTargetAdapter left, IScenarioAuthoringTargetAdapter right)
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return 1;
                if (right == null) return -1;
                return right.Priority.CompareTo(left.Priority);
            }
        }

        private sealed class DefaultScenarioAuthoringTargetAdapter : IScenarioAuthoringTargetAdapter
        {
            private readonly ScenarioEditorCharacterAppearanceService _characterAppearanceService;

            public DefaultScenarioAuthoringTargetAdapter(ScenarioEditorCharacterAppearanceService characterAppearanceService)
            {
                _characterAppearanceService = characterAppearanceService;
            }

            public string AdapterId
            {
                get { return "ShelteredAPI.DefaultWorldObject"; }
            }

            public int Priority
            {
                get { return 0; }
            }

            public bool TryCreateTarget(ScenarioAuthoringTargetContext context, out ScenarioAuthoringTarget target)
            {
                target = null;
                GameObject sourceObject = context != null ? context.GameObject : null;
                if (IsIgnoredSelectionObject(sourceObject))
                    return false;

                GameObject gameObject = ResolveTargetRoot(sourceObject);
                if (gameObject == null)
                    return false;
                if (IsIgnoredSelectionObject(gameObject))
                    return false;

                Transform transform = gameObject.transform;
                ScenarioAuthoringTargetKind kind = Classify(gameObject);
                string transformPath = ShelteredScenarioRuntime.GetTransformPath(transform);
                string displayName = ScenarioWorldObjectDisplayNameResolver.Resolve(gameObject, kind);
                string description = kind + " at " + transformPath;

                target = new ScenarioAuthoringTarget
                {
                    Id = kind + ":" + transform.GetInstanceID(),
                    Kind = kind,
                    DisplayName = displayName,
                    Description = description,
                    AdapterId = AdapterId,
                    GameObjectName = gameObject.name,
                    TransformPath = transformPath,
                    ScenarioReferenceId = ResolveScenarioReferenceId(gameObject),
                    RuntimeObject = gameObject,
                    HighlightObject = ResolveHighlightObject(sourceObject, gameObject),
                    WorldPosition = ResolveWorldPosition(context, transform),
                    GridX = ResolveGridX(context, transform),
                    GridY = ResolveGridY(context, transform),
                    SupportsInspect = true,
                    SupportsReplace = SupportsReplace(gameObject, kind)
                };

                return true;
            }

            private static GameObject ResolveTargetRoot(GameObject gameObject)
            {
                if (gameObject == null)
                    return null;

                ScenarioRuntimeIdentity sceneSprite;
                if (ScenarioRuntimeIdentityCatalog.TryGet(gameObject, out sceneSprite)
                    && sceneSprite.Kind == ScenarioRuntimeIdentityKind.SceneSpritePlacement)
                {
                    return ScenarioRuntimeIdentityCatalog.FindSceneSpritePlacement(sceneSprite.PlacementId) ?? gameObject;
                }

                Obj_Base objBase = gameObject.GetComponentInParent<Obj_Base>();
                if (objBase != null)
                    return objBase.gameObject;

                GameObject logicalRoot = ScenarioWorldObjectDisplayNameResolver.ResolveLogicalRoot(gameObject);
                if (logicalRoot != null)
                    return logicalRoot;

                FamilyMember familyMember = gameObject.GetComponentInParent<FamilyMember>();
                if (familyMember != null)
                    return familyMember.gameObject;

                NpcVisitor visitor = gameObject.GetComponentInParent<NpcVisitor>();
                if (visitor != null)
                    return visitor.gameObject;

                BaseCharacter baseCharacter = gameObject.GetComponentInParent<BaseCharacter>();
                if (baseCharacter != null)
                    return baseCharacter.gameObject;

                Rigidbody body3D = gameObject.GetComponentInParent<Rigidbody>();
                if (body3D != null)
                    return body3D.gameObject;

                Rigidbody2D body2D = gameObject.GetComponentInParent<Rigidbody2D>();
                if (body2D != null)
                    return body2D.gameObject;

                return gameObject;
            }

            private static UnityEngine.Object ResolveHighlightObject(GameObject sourceObject, GameObject targetRoot)
            {
                if (HasSpriteHierarchy(targetRoot))
                    return targetRoot;

                GameObject highlightObject = sourceObject != null ? sourceObject : targetRoot;
                if (highlightObject == null)
                    return targetRoot;

                if (targetRoot != null && !highlightObject.transform.IsChildOf(targetRoot.transform) && highlightObject != targetRoot)
                    highlightObject = targetRoot;

                if (HasSpriteComponent(highlightObject))
                    return highlightObject;

                return targetRoot != null ? (UnityEngine.Object)targetRoot : highlightObject;
            }

            private static bool HasSpriteComponent(GameObject gameObject)
            {
                return gameObject != null
                    && (gameObject.GetComponent<SpriteRenderer>() != null || gameObject.GetComponent<UI2DSprite>() != null);
            }

            private static bool HasSpriteHierarchy(GameObject gameObject)
            {
                if (gameObject == null)
                    return false;

                if (HasSpriteComponent(gameObject))
                    return true;

                return gameObject.GetComponentInChildren<SpriteRenderer>(true) != null
                    || gameObject.GetComponentInChildren<UI2DSprite>(true) != null;
            }

            private static bool IsIgnoredSelectionObject(GameObject gameObject)
            {
                if (gameObject == null)
                    return false;

                if (gameObject.GetComponentInParent<CursorBase>() != null)
                    return true;

                string name = gameObject.name;
                if (ContainsAny(name, "cursor", "cogsprite"))
                    return true;

                string path = ShelteredScenarioRuntime.GetTransformPath(gameObject.transform);
                return ContainsAny(path, "cursor", "cogsprite");
            }

            private static ScenarioAuthoringTargetKind Classify(GameObject gameObject)
            {
                if (gameObject == null)
                    return ScenarioAuthoringTargetKind.Unknown;

                ScenarioRuntimeIdentity runtimeIdentity;
                if (ScenarioRuntimeIdentityCatalog.TryGet(gameObject, out runtimeIdentity)
                    && runtimeIdentity.Kind == ScenarioRuntimeIdentityKind.SceneSpritePlacement)
                    return ScenarioAuthoringTargetKind.SceneSprite;

                if (gameObject.GetComponentInParent<FamilyMember>() != null
                    || gameObject.GetComponentInParent<NpcVisitor>() != null
                    || gameObject.GetComponentInParent<BaseCharacter>() != null)
                    return ScenarioAuthoringTargetKind.Character;

                string path = ShelteredScenarioRuntime.GetTransformPath(gameObject.transform).ToLowerInvariant();
                SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>() ?? gameObject.GetComponentInChildren<SpriteRenderer>(true);
                if (ContainsAny(path, "wire", "cable", "power"))
                    return ScenarioAuthoringTargetKind.Wire;
                if (ContainsAny(path, "wall", "barricade"))
                    return ScenarioAuthoringTargetKind.Wall;
                if (ContainsAny(path, "light", "lamp"))
                    return ScenarioAuthoringTargetKind.Light;
                if (spriteRenderer != null && (spriteRenderer.sortingOrder < 0 || ContainsAny(path, "background", "scenery", "sky", "terrain", "backdrop")))
                    return ScenarioAuthoringTargetKind.Background;
                if (ContainsAny(path, "van", "vehicle", "rv"))
                    return ScenarioAuthoringTargetKind.Vehicle;
                if (ContainsAny(path, "room"))
                    return ScenarioAuthoringTargetKind.Room;
                if (ContainsAny(path, "tile", "grid"))
                    return ScenarioAuthoringTargetKind.Tile;

                Component[] components = gameObject.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component == null)
                        continue;

                    string typeName = component.GetType().Name.ToLowerInvariant();
                    if (ContainsAny(typeName, "familymember", "character", "survivor", "visitor", "npc"))
                        return ScenarioAuthoringTargetKind.Character;
                    if (ContainsAny(typeName, "wire", "cable"))
                        return ScenarioAuthoringTargetKind.Wire;
                    if (ContainsAny(typeName, "wall"))
                        return ScenarioAuthoringTargetKind.Wall;
                    if (ContainsAny(typeName, "light", "lamp"))
                        return ScenarioAuthoringTargetKind.Light;
                    if (ContainsAny(typeName, "van", "vehicle"))
                        return ScenarioAuthoringTargetKind.Vehicle;
                    if (ContainsAny(typeName, "room"))
                        return ScenarioAuthoringTargetKind.Room;
                    if (ContainsAny(typeName, "tile"))
                        return ScenarioAuthoringTargetKind.Tile;
                }

                return ScenarioAuthoringTargetKind.PlaceableObject;
            }

            private static Vector3 ResolveWorldPosition(ScenarioAuthoringTargetContext context, Transform transform)
            {
                if (context != null)
                {
                    if (context.Hit.point != Vector3.zero)
                        return context.Hit.point;
                    if (context.WorldPoint != Vector3.zero)
                        return context.WorldPoint;
                }

                return transform != null ? transform.position : Vector3.zero;
            }

            private static string ResolveScenarioReferenceId(GameObject gameObject)
            {
                ScenarioRuntimeIdentity marker;
                return gameObject != null
                    && ScenarioRuntimeIdentityCatalog.TryGet(gameObject, out marker)
                    && marker.Kind == ScenarioRuntimeIdentityKind.SceneSpritePlacement
                    ? marker.PlacementId : null;
            }

            private static int? ResolveGridX(ScenarioAuthoringTargetContext context, Transform transform)
            {
                ScenarioRuntimeIdentity marker;
                if (context != null && context.GameObject != null
                    && ScenarioRuntimeIdentityCatalog.TryGet(context.GameObject, out marker)
                    && marker.Kind == ScenarioRuntimeIdentityKind.SceneSpritePlacement
                    && marker.GridX >= 0)
                    return marker.GridX;

                int gridX;
                int gridY;
                if (ShelteredScenarioRuntime.TryGetShelterGridCell(ResolveWorldPosition(context, transform), out gridX, out gridY))
                    return gridX;

                return null;
            }

            private static int? ResolveGridY(ScenarioAuthoringTargetContext context, Transform transform)
            {
                ScenarioRuntimeIdentity marker;
                if (context != null && context.GameObject != null
                    && ScenarioRuntimeIdentityCatalog.TryGet(context.GameObject, out marker)
                    && marker.Kind == ScenarioRuntimeIdentityKind.SceneSpritePlacement
                    && marker.GridY >= 0)
                    return marker.GridY;

                int gridX;
                int gridY;
                if (ShelteredScenarioRuntime.TryGetShelterGridCell(ResolveWorldPosition(context, transform), out gridX, out gridY))
                    return gridY;

                return null;
            }

            private bool SupportsReplace(GameObject gameObject, ScenarioAuthoringTargetKind kind)
            {
                switch (kind)
                {
                    case ScenarioAuthoringTargetKind.Character:
                        ScenarioEditorCharacterAppearanceService.ResolvedCharacterTarget characterTarget;
                        string characterMessage;
                        return ScenarioEditorCharacterTargetAdapter.TryResolve(
                            _characterAppearanceService,
                            new ScenarioAuthoringTarget { RuntimeObject = gameObject },
                            out characterTarget,
                            out characterMessage);
                    case ScenarioAuthoringTargetKind.PlaceableObject:
                    case ScenarioAuthoringTargetKind.Wall:
                    case ScenarioAuthoringTargetKind.Wire:
                    case ScenarioAuthoringTargetKind.Light:
                    case ScenarioAuthoringTargetKind.Vehicle:
                    case ScenarioAuthoringTargetKind.Room:
                    case ScenarioAuthoringTargetKind.Tile:
                    case ScenarioAuthoringTargetKind.Background:
                    case ScenarioAuthoringTargetKind.SceneSprite:
                        return true;
                    default:
                        return false;
                }
            }

            private static bool ContainsAny(string value, params string[] parts)
            {
                if (string.IsNullOrEmpty(value) || parts == null)
                    return false;

                for (int i = 0; i < parts.Length; i++)
                {
                    if (!string.IsNullOrEmpty(parts[i]) && value.IndexOf(parts[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }

                return false;
            }

        }

        private sealed class GridCellScenarioAuthoringTargetAdapter : IScenarioAuthoringTargetAdapter
        {
            public string AdapterId
            {
                get { return "ShelteredAPI.GridCell"; }
            }

            public int Priority
            {
                get { return -100; }
            }

            public bool TryCreateTarget(ScenarioAuthoringTargetContext context, out ScenarioAuthoringTarget target)
            {
                target = null;
                int gridX;
                int gridY;
                if (context == null || !ShelteredScenarioRuntime.TryGetShelterGridCell(context.WorldPoint, out gridX, out gridY))
                    return false;

                ShelterRoomGrid grid = ShelterRoomGrid.Instance;
                ShelterRoomGrid.GridCell cell = grid != null ? grid.GetCell(gridX, gridY) : null;
                GameObject cellObject = cell != null ? cell.prefab : null;
                Vector3 cellCenter = ShelteredScenarioRuntime.GetShelterGridCellCenter(gridX, gridY);
                string transformPath = cellObject != null ? ShelteredScenarioRuntime.GetTransformPath(cellObject.transform) : ("ShelterGrid/" + gridX + "/" + gridY);
                string displayName = "Grid " + gridX + "," + gridY;

                target = new ScenarioAuthoringTarget
                {
                    Id = "grid:" + gridX + ":" + gridY,
                    Kind = ScenarioAuthoringTargetKind.Tile,
                    DisplayName = displayName,
                    Description = "Selectable shelter grid cell at " + gridX + "," + gridY + ".",
                    AdapterId = AdapterId,
                    GameObjectName = cellObject != null ? cellObject.name : displayName,
                    TransformPath = transformPath,
                    RuntimeObject = cellObject,
                    HighlightObject = cellObject,
                    WorldPosition = cellCenter,
                    GridX = gridX,
                    GridY = gridY,
                    SupportsInspect = true,
                    SupportsReplace = true
                };
                return true;
            }

        }
    }
}
