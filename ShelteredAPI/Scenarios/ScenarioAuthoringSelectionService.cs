using System;
using System.Collections.Generic;
using System.Text;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringSelectionService
    {
        private readonly ScenarioCharacterAppearanceService _characterAppearanceService;
        private readonly ScenarioSelectionScopeService _scopeService;
        private readonly ScenarioAuthoringTargetAdapterRegistry _adapterRegistry = new ScenarioAuthoringTargetAdapterRegistry();

        public ScenarioAuthoringSelectionService(
            ScenarioCharacterAppearanceService characterAppearanceService,
            ScenarioSelectionScopeService scopeService)
        {
            _characterAppearanceService = characterAppearanceService;
            _scopeService = scopeService;
            _adapterRegistry.Register(new DefaultScenarioAuthoringTargetAdapter(_characterAppearanceService));
            _adapterRegistry.Register(new GridCellScenarioAuthoringTargetAdapter());
        }

        public bool Update(ScenarioAuthoringState state)
        {
            if (state == null)
            {
                ScenarioHoverVisualService.Instance.Clear();
                ScenarioAuthoringSelectionMenuService.Instance.Reset();
                return false;
            }

            ScenarioAuthoringTarget hovered = null;
            List<ScenarioAuthoringTarget> stack = null;
            bool selectionMode = ScenarioAuthoringRuntimeGuards.ShouldResolveSelection()
                && (ScenarioAuthoringInputActions.IsSelectionModifierHeld() || IsAddSelectionHeld());
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
                if (TryResolveCandidateStack(state, out stack))
                {
                    changed |= SynchronizeSelectionStack(state, stack);
                    if (state.SelectionStack != null && state.SelectionStack.Count > 0)
                        hovered = state.SelectionStack[Mathf.Clamp(state.ActiveSelectionStackIndex, 0, state.SelectionStack.Count - 1)];

                    if (ScenarioAuthoringInputActions.IsStackCycleDown())
                    {
                        changed |= CycleSelectionStack(state, 1);
                        if (state.SelectionStack != null && state.SelectionStack.Count > 0)
                            hovered = state.SelectionStack[Mathf.Clamp(state.ActiveSelectionStackIndex, 0, state.SelectionStack.Count - 1)];
                    }

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

                    if (state.SelectionStack != null && state.SelectionStack.Count > 0)
                    {
                        ClearSelectionStack(state);
                        changed = true;
                    }
                }

                if (ScenarioAuthoringInputActions.IsConfirmSelectionDown()
                    && hovered != null
                    && _scopeService.CanSelectTargetForCurrentStage(state, hovered))
                {
                    if (state.SelectionStack != null
                        && state.SelectionStack.Count > 1
                        && AreSameTarget(state.SelectedTarget, hovered))
                    {
                        changed |= CycleSelectionStack(state, 1);
                        hovered = state.SelectionStack[Mathf.Clamp(state.ActiveSelectionStackIndex, 0, state.SelectionStack.Count - 1)];
                    }

                    changed |= ApplySelection(state, hovered);
                }
            }

            if (ScenarioAuthoringInputActions.IsClearSelectionDown())
            {
                ScenarioAuthoringTarget menuTarget = state.SelectedTarget ?? hovered;
                if (selectionMode && menuTarget != null)
                {
                    ScenarioAuthoringSelectionMenuService.Instance.OpenMenu(state, menuTarget);
                    if (state.SelectionStack != null && state.SelectionStack.Count > 1)
                        state.StatusMessage = "Selection stack opened with " + state.SelectionStack.Count + " candidates.";
                    changed = true;
                }
                else if (state.SelectedTarget != null)
                {
                    state.SelectedTarget = null;
                    state.MultiSelection.Clear();
                    state.StatusMessage = "Selection cleared.";
                    changed = true;
                }
            }

            ScenarioHoverVisualService.Instance.UpdateFromState(state);
            ScenarioAuthoringSelectionMenuService.Instance.Sync(state);
            return changed;
        }

        private bool TryResolveCandidateStack(ScenarioAuthoringState state, out List<ScenarioAuthoringTarget> targets)
        {
            targets = null;
            if (UICamera.hoveredObject != null)
                return false;
            if (ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>().PointerOverAuthoringUi)
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
                Collider2D[] hits2D = Physics2D.OverlapPointAll(new Vector2(worldPoint.x, worldPoint.y));
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

            AddSpriteRendererCandidates(state, candidates, camera, ray, worldPoint);

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
            if (!_scopeService.CanSelectTargetForCurrentStage(state, target))
                return;

            candidates.Add(new SelectionCandidate
            {
                Target = target,
                SourceRank = sourceRank,
                Distance = distance,
                ToolScore = ScoreToolRelevance(state, target),
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
            Vector3 worldPoint)
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
                    || !spriteRenderer.bounds.Contains(worldPoint))
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
            if (left.SortingLayer != right.SortingLayer)
                return right.SortingLayer.CompareTo(left.SortingLayer);
            if (left.SortingOrder != right.SortingOrder)
                return right.SortingOrder.CompareTo(left.SortingOrder);
            int distance = left.Distance.CompareTo(right.Distance);
            if (distance != 0)
                return distance;
            int z = right.Z.CompareTo(left.Z);
            if (z != 0)
                return z;
            return left.Area.CompareTo(right.Area);
        }

        private static int ScoreToolRelevance(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            if (state == null || target == null)
                return 0;

            switch (state.ActiveTool)
            {
                case ScenarioAuthoringTool.Objects:
                    return target.Kind == ScenarioAuthoringTargetKind.PlaceableObject ? 80 : target.Kind == ScenarioAuthoringTargetKind.SceneSprite ? 30 : 0;
                case ScenarioAuthoringTool.Shelter:
                    return target.Kind == ScenarioAuthoringTargetKind.Room || target.Kind == ScenarioAuthoringTargetKind.Tile || target.Kind == ScenarioAuthoringTargetKind.Light ? 80 : 0;
                case ScenarioAuthoringTool.Wiring:
                    return target.Kind == ScenarioAuthoringTargetKind.Wall || target.Kind == ScenarioAuthoringTargetKind.Wire || target.Kind == ScenarioAuthoringTargetKind.Light ? 80 : 0;
                case ScenarioAuthoringTool.Assets:
                    return target.SupportsReplace ? 80 : 0;
                case ScenarioAuthoringTool.Family:
                case ScenarioAuthoringTool.People:
                    return target.Kind == ScenarioAuthoringTargetKind.Character ? 80 : 0;
                default:
                    return 20;
            }
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

        private static bool ApplySelection(ScenarioAuthoringState state, ScenarioAuthoringTarget hovered)
        {
            if (state == null || hovered == null)
                return false;

            if (IsAddSelectionHeld())
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
                    || !spriteRenderer.bounds.Contains(worldPoint))
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
                get { return SourceRank + ToolScore + StageScore + KindScore; }
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
            private readonly ScenarioCharacterAppearanceService _characterAppearanceService;

            public DefaultScenarioAuthoringTargetAdapter(ScenarioCharacterAppearanceService characterAppearanceService)
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
                string transformPath = BuildTransformPath(transform);
                string displayName = !string.IsNullOrEmpty(gameObject.name) ? gameObject.name : kind.ToString();
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

                ScenarioSceneSpritePlacementMarker sceneSprite = gameObject.GetComponentInParent<ScenarioSceneSpritePlacementMarker>();
                if (sceneSprite != null)
                    return sceneSprite.gameObject;

                Obj_Base objBase = gameObject.GetComponentInParent<Obj_Base>();
                if (objBase != null)
                    return objBase.gameObject;

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

                string path = BuildTransformPath(gameObject.transform);
                return ContainsAny(path, "cursor", "cogsprite");
            }

            private static ScenarioAuthoringTargetKind Classify(GameObject gameObject)
            {
                if (gameObject == null)
                    return ScenarioAuthoringTargetKind.Unknown;

                if (gameObject.GetComponentInParent<ScenarioSceneSpritePlacementMarker>() != null)
                    return ScenarioAuthoringTargetKind.SceneSprite;

                if (gameObject.GetComponentInParent<FamilyMember>() != null
                    || gameObject.GetComponentInParent<NpcVisitor>() != null
                    || gameObject.GetComponentInParent<BaseCharacter>() != null)
                    return ScenarioAuthoringTargetKind.Character;

                string path = BuildTransformPath(gameObject.transform).ToLowerInvariant();
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
                ScenarioSceneSpritePlacementMarker marker = gameObject != null ? gameObject.GetComponentInParent<ScenarioSceneSpritePlacementMarker>() : null;
                return marker != null ? marker.PlacementId : null;
            }

            private static int? ResolveGridX(ScenarioAuthoringTargetContext context, Transform transform)
            {
                ScenarioSceneSpritePlacementMarker marker = context != null && context.GameObject != null
                    ? context.GameObject.GetComponentInParent<ScenarioSceneSpritePlacementMarker>()
                    : null;
                if (marker != null && marker.GridX >= 0)
                    return marker.GridX;

                int gridX;
                int gridY;
                if (ScenarioGridSnapService.TryGetCell(ResolveWorldPosition(context, transform), out gridX, out gridY))
                    return gridX;

                return null;
            }

            private static int? ResolveGridY(ScenarioAuthoringTargetContext context, Transform transform)
            {
                ScenarioSceneSpritePlacementMarker marker = context != null && context.GameObject != null
                    ? context.GameObject.GetComponentInParent<ScenarioSceneSpritePlacementMarker>()
                    : null;
                if (marker != null && marker.GridY >= 0)
                    return marker.GridY;

                int gridX;
                int gridY;
                if (ScenarioGridSnapService.TryGetCell(ResolveWorldPosition(context, transform), out gridX, out gridY))
                    return gridY;

                return null;
            }

            private bool SupportsReplace(GameObject gameObject, ScenarioAuthoringTargetKind kind)
            {
                switch (kind)
                {
                    case ScenarioAuthoringTargetKind.Character:
                        return _characterAppearanceService.CanEdit(new ScenarioAuthoringTarget
                        {
                            RuntimeObject = gameObject
                        });
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

            private static string BuildTransformPath(Transform transform)
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
                if (context == null || !ScenarioGridSnapService.TryGetCell(context.WorldPoint, out gridX, out gridY))
                    return false;

                ShelterRoomGrid grid = ShelterRoomGrid.Instance;
                ShelterRoomGrid.GridCell cell = grid != null ? grid.GetCell(gridX, gridY) : null;
                GameObject cellObject = cell != null ? cell.prefab : null;
                Vector3 cellCenter = ScenarioGridSnapService.GetCellCenterWorldPosition(gridX, gridY);
                string transformPath = cellObject != null ? BuildTransformPath(cellObject.transform) : ("ShelterGrid/" + gridX + "/" + gridY);
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

            private static string BuildTransformPath(Transform transform)
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
        }
    }
}
