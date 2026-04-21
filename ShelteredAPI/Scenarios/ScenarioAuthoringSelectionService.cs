using System;
using System.Collections.Generic;
using ModAPI.Inspector;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringSelectionService
    {
        private readonly ScenarioAuthoringTargetAdapterRegistry _adapterRegistry = new ScenarioAuthoringTargetAdapterRegistry();

        public ScenarioAuthoringSelectionService()
        {
            _adapterRegistry.Register(new DefaultScenarioAuthoringTargetAdapter());
        }

        public bool Update(ScenarioAuthoringState state)
        {
            if (state == null)
            {
                BoundsHighlighter.Target = null;
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
            }

            if (ScenarioAuthoringInputActions.IsClearSelectionDown() && state.SelectedTarget != null)
            {
                state.SelectedTarget = null;
                state.StatusMessage = "Selection cleared.";
                changed = true;
            }

            UpdateHighlightTarget(state);
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
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
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
                        GameObject = gameObject
                    };

                    if (_adapterRegistry.TryCreateTarget(context, out target) && target != null)
                        return true;
                }
            }

            try
            {
                Vector3 mouseWorld = camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
                Collider2D[] hits2D = Physics2D.OverlapPointAll(new Vector2(mouseWorld.x, mouseWorld.y));
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
                        GameObject = gameObject
                    };

                    if (_adapterRegistry.TryCreateTarget(context, out target) && target != null)
                        return true;
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug("[ScenarioAuthoringSelection] 2D overlap check failed: " + ex.Message);
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

        private static void UpdateHighlightTarget(ScenarioAuthoringState state)
        {
            if (state == null)
            {
                BoundsHighlighter.Target = null;
                return;
            }

            ScenarioAuthoringTarget target = state.SelectionModeActive && state.HoveredTarget != null
                ? state.HoveredTarget
                : state.SelectedTarget;

            BoundsHighlighter.Target = ResolveHighlightTransform(target);
        }

        private static Transform ResolveHighlightTransform(ScenarioAuthoringTarget target)
        {
            if (target == null || target.RuntimeObject == null)
                return null;

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject != null)
                return gameObject.transform;

            Component component = target.RuntimeObject as Component;
            return component != null ? component.transform : null;
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
                GameObject gameObject = context != null ? ResolveTargetRoot(context.GameObject) : null;
                if (gameObject == null)
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
                    RuntimeObject = gameObject,
                    WorldPosition = ResolveWorldPosition(context, transform),
                    SupportsInspect = true,
                    SupportsReplace = SupportsReplace(kind)
                };

                return true;
            }

            private static GameObject ResolveTargetRoot(GameObject gameObject)
            {
                if (gameObject == null)
                    return null;

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

            private static ScenarioAuthoringTargetKind Classify(GameObject gameObject)
            {
                if (gameObject == null)
                    return ScenarioAuthoringTargetKind.Unknown;

                if (gameObject.GetComponentInParent<FamilyMember>() != null
                    || gameObject.GetComponentInParent<NpcVisitor>() != null
                    || gameObject.GetComponentInParent<BaseCharacter>() != null)
                    return ScenarioAuthoringTargetKind.Character;

                string path = BuildTransformPath(gameObject.transform).ToLowerInvariant();
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
                if (context != null && context.Hit.point != Vector3.zero)
                    return context.Hit.point;

                return transform != null ? transform.position : Vector3.zero;
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
    }
}
