using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringSelectionService
    {
        private readonly ScenarioAuthoringTargetAdapterRegistry _adapterRegistry = new ScenarioAuthoringTargetAdapterRegistry();

        public ScenarioAuthoringSelectionService()
        {
            _adapterRegistry.Register(new DefaultScenarioAuthoringTargetAdapter());
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

            bool selectionMode = ScenarioAuthoringRuntimeGuards.ShouldResolveSelection()
                && ScenarioAuthoringInputActions.IsSelectionModifierHeld();
            bool changed = state.SelectionModeActive != selectionMode;
            state.SelectionModeActive = selectionMode;

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
                ScenarioAuthoringTarget hovered;
                if (TryResolveHoveredTarget(out hovered))
                {
                    if (!AreSameTarget(state.HoveredTarget, hovered))
                    {
                        state.HoveredTarget = hovered;
                        changed = true;
                    }
                }
                else if (state.HoveredTarget != null)
                {
                    state.HoveredTarget = null;
                    changed = true;
                }

                if (ScenarioAuthoringInputActions.IsConfirmSelectionDown() && hovered != null && !AreSameTarget(state.SelectedTarget, hovered))
                {
                    state.SelectedTarget = hovered.Copy();
                    state.StatusMessage = "Selected " + hovered.DisplayName + ".";
                    changed = true;
                }

                if (ScenarioAuthoringInputActions.IsConfirmSelectionDown() && hovered != null)
                    ScenarioAuthoringSelectionMenuService.Instance.OpenMenu(state, hovered);
            }

            if (ScenarioAuthoringInputActions.IsClearSelectionDown() && state.SelectedTarget != null)
            {
                state.SelectedTarget = null;
                state.StatusMessage = "Selection cleared.";
                changed = true;
            }

            ScenarioHoverVisualService.Instance.UpdateFromState(state);
            ScenarioAuthoringSelectionMenuService.Instance.Sync(state);
            return changed;
        }

        private bool TryResolveHoveredTarget(out ScenarioAuthoringTarget target)
        {
            target = null;
            if (UICamera.hoveredObject != null)
                return false;
            if (ScenarioAuthoringImguiRenderModule.IsPointerOverInteractiveUi())
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
            ScenarioAuthoringTarget fallbackTarget = null;
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

                    ScenarioAuthoringTarget candidate;
                    if (!_adapterRegistry.TryCreateTarget(context, out candidate) || candidate == null)
                        continue;

                    if (!IsBackgroundLike(candidate))
                    {
                        target = candidate;
                        return true;
                    }

                    if (fallbackTarget == null)
                        fallbackTarget = candidate;
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

                    ScenarioAuthoringTarget candidate;
                    if (!_adapterRegistry.TryCreateTarget(context, out candidate) || candidate == null)
                        continue;

                    if (!IsBackgroundLike(candidate))
                    {
                        target = candidate;
                        return true;
                    }

                    if (fallbackTarget == null)
                        fallbackTarget = candidate;
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug("[ScenarioAuthoringSelection] 2D overlap check failed: " + ex.Message);
            }

            ScenarioAuthoringTargetContext spriteContext;
            if (TryResolveSpriteContext(camera, ray, worldPoint, out spriteContext)
                && _adapterRegistry.TryCreateTarget(spriteContext, out target)
                && target != null)
            {
                return true;
            }

            if (fallbackTarget != null)
            {
                target = fallbackTarget;
                return true;
            }

            ScenarioAuthoringTargetContext gridContext = new ScenarioAuthoringTargetContext
            {
                Camera = camera,
                Ray = ray,
                WorldPoint = worldPoint
            };
            return _adapterRegistry.TryCreateTarget(gridContext, out target) && target != null;
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

        private static bool IsBackgroundLike(ScenarioAuthoringTarget target)
        {
            if (target == null)
                return false;

            return target.Kind == ScenarioAuthoringTargetKind.Background
                || target.Kind == ScenarioAuthoringTargetKind.Tile;
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
                    SupportsReplace = SupportsReplace(kind)
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
                if (spriteRenderer != null && (spriteRenderer.sortingOrder < 0 || ContainsAny(path, "background", "scenery", "sky", "terrain", "backdrop")))
                    return ScenarioAuthoringTargetKind.Background;
                if (ContainsAny(path, "wire", "cable", "power"))
                    return ScenarioAuthoringTargetKind.Wire;
                if (ContainsAny(path, "wall", "barricade"))
                    return ScenarioAuthoringTargetKind.Wall;
                if (ContainsAny(path, "light", "lamp"))
                    return ScenarioAuthoringTargetKind.Light;
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

            private static bool SupportsReplace(ScenarioAuthoringTargetKind kind)
            {
                switch (kind)
                {
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
