using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Shared;
using ShelteredScenarioEditor.Application.Selection;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredAPI.Scenarios.Public;
namespace ShelteredScenarioEditor.Application.Authoring{
    internal static class ScenarioBunkerDraftService
    {
        private const float PlacementMatchTolerance = 0.15f;

        public static BunkerEditsDefinition EnsureBunkerEdits(ScenarioEditorSession session)
        {
            if (session == null || session.WorkingDefinition == null)
                throw new InvalidOperationException("No authoring session is active.");

            if (session.WorkingDefinition.BunkerEdits == null)
                session.WorkingDefinition.BunkerEdits = new BunkerEditsDefinition();

            return session.WorkingDefinition.BunkerEdits;
        }

        public static void MarkBunkerDirty(ScenarioEditorSession session)
        {
            if (session == null)
                return;

            session.MarkDraftChanged(ScenarioDirtySection.Bunker, ScenarioEditCategory.Bunker);
        }

        public static void UpsertRoomEdit(ScenarioEditorSession session, int gridX, int gridY, Action<RoomEdit> applyUpdate)
        {
            if (applyUpdate == null)
                return;

            BunkerEditsDefinition bunkerEdits = EnsureBunkerEdits(session);
            RoomEdit room = FindRoomEdit(bunkerEdits.RoomChanges, gridX, gridY);
            if (room == null)
            {
                room = new RoomEdit
                {
                    GridX = gridX,
                    GridY = gridY
                };
                bunkerEdits.RoomChanges.Add(room);
            }

            applyUpdate(room);
            bunkerEdits.RoomChanges.Sort(CompareRoomEdits);
            MarkBunkerDirty(session);
        }

        public static ObjectPlacement CreatePlacement(Obj_Base obj, ScenarioPreviewSessionHost previewHost)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");

            Transform transform = obj.transform;
            ObjectPlacement placement = new ObjectPlacement();
            placement.DefinitionReference = obj.GetObjectType().ToString();
            placement.Position = new ScenarioVector3
            {
                X = transform.position.x,
                Y = transform.position.y,
                Z = transform.position.z
            };
            placement.Rotation = new ScenarioVector3
            {
                X = transform.eulerAngles.x,
                Y = transform.eulerAngles.y,
                Z = transform.eulerAngles.z
            };
            placement.CustomProperties.Add(new ScenarioProperty { Key = ScenarioPlacementDefinitions.PropertyLevel, Value = obj.objectLevel.ToString() });
            placement.CustomProperties.Add(new ScenarioProperty { Key = ScenarioPlacementDefinitions.PropertyLockDeconstruct, Value = obj.lockDeconstructOption.ToString().ToLowerInvariant() });
            placement.CustomProperties.Add(new ScenarioProperty { Key = ScenarioPlacementDefinitions.PropertyMovable, Value = obj.movable.ToString().ToLowerInvariant() });
            if (obj.objectId > 0)
            {
                placement.CustomProperties.Add(new ScenarioProperty
                {
                    Key = ScenarioPlacementDefinitions.PropertySourceObjectId,
                    Value = obj.objectId.ToString()
                });
            }

            placement.CustomProperties.Add(new ScenarioProperty
            {
                Key = ScenarioPlacementDefinitions.PropertyCapturedName,
                Value = SafeObjectName(obj)
            });
            if (previewHost != null)
            {
                previewHost.CaptureRuntimeObjectState(obj, placement);
                previewHost.CaptureStationUpgradeState(obj, placement);
            }
            return placement;
        }

        public static ObjectPlacement CreatePlacement(string definitionReference, Vector3 position, Vector3 rotation, params ScenarioProperty[] properties)
        {
            ObjectPlacement placement = new ObjectPlacement();
            placement.DefinitionReference = definitionReference;
            placement.Position = new ScenarioVector3
            {
                X = position.x,
                Y = position.y,
                Z = position.z
            };
            placement.Rotation = new ScenarioVector3
            {
                X = rotation.x,
                Y = rotation.y,
                Z = rotation.z
            };

            for (int i = 0; properties != null && i < properties.Length; i++)
            {
                ScenarioProperty property = properties[i];
                if (property != null)
                    placement.CustomProperties.Add(property);
            }

            return placement;
        }

        public static void UpsertPlacement(ScenarioEditorSession session, ObjectPlacement placement)
        {
            if (placement == null)
                return;

            BunkerEditsDefinition bunkerEdits = EnsureBunkerEdits(session);
            int index = FindPlacementIndex(bunkerEdits.ObjectPlacements, placement);
            if (index >= 0)
                bunkerEdits.ObjectPlacements[index] = placement;
            else
                bunkerEdits.ObjectPlacements.Add(placement);

            bunkerEdits.ObjectPlacements.Sort(ComparePlacements);
            MarkBunkerDirty(session);
        }

        public static bool RemovePlacement(ScenarioEditorSession session, Predicate<ObjectPlacement> predicate)
        {
            if (predicate == null)
                return false;

            BunkerEditsDefinition bunkerEdits = EnsureBunkerEdits(session);
            for (int i = bunkerEdits.ObjectPlacements.Count - 1; i >= 0; i--)
            {
                ObjectPlacement placement = bunkerEdits.ObjectPlacements[i];
                if (placement != null && predicate(placement))
                {
                    bunkerEdits.ObjectPlacements.RemoveAt(i);
                    MarkBunkerDirty(session);
                    return true;
                }
            }

            return false;
        }

        public static bool RemoveRoomEdit(ScenarioEditorSession session, int gridX, int gridY, Func<RoomEdit, bool> shouldRemove)
        {
            BunkerEditsDefinition bunkerEdits = EnsureBunkerEdits(session);
            RoomEdit room = FindRoomEdit(bunkerEdits.RoomChanges, gridX, gridY);
            if (room == null)
                return false;

            if (shouldRemove != null && !shouldRemove(room))
                return false;

            bunkerEdits.RoomChanges.Remove(room);
            MarkBunkerDirty(session);
            return true;
        }

        public static int FindPlacementIndex(List<ObjectPlacement> placements, Obj_Base obj)
        {
            if (placements == null || obj == null)
                return -1;

            for (int i = 0; i < placements.Count; i++)
            {
                ObjectPlacement placement = placements[i];
                if (MatchesPlacement(placement, obj))
                    return i;
            }

            return -1;
        }

        public static bool MatchesPlacement(ObjectPlacement placement, Obj_Base obj)
        {
            if (placement == null || obj == null)
                return false;

            ScenarioRuntimeIdentity binding;
            if (ScenarioRuntimeIdentityCatalog.TryGet(obj.gameObject, out binding)
                && binding.Kind == ScenarioRuntimeIdentityKind.ObjectPlacement)
            {
                if (!string.IsNullOrEmpty(binding.ScenarioObjectId)
                    && string.Equals(placement.ScenarioObjectId, binding.ScenarioObjectId, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (!string.IsNullOrEmpty(binding.RuntimeBindingKey)
                    && string.Equals(placement.RuntimeBindingKey, binding.RuntimeBindingKey, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            string objectId = obj.objectId > 0 ? obj.objectId.ToString() : null;
            string sourceObjectId = ScenarioPropertyBag.GetString(placement.CustomProperties, ScenarioPlacementDefinitions.PropertySourceObjectId);
            if (!string.IsNullOrEmpty(objectId)
                && !string.IsNullOrEmpty(sourceObjectId)
                && string.Equals(sourceObjectId, objectId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.Equals(placement.DefinitionReference, obj.GetObjectType().ToString(), StringComparison.OrdinalIgnoreCase)
                || placement.Position == null)
            {
                return false;
            }

            Vector3 placementPosition = new Vector3(placement.Position.X, placement.Position.Y, placement.Position.Z);
            return Vector3.Distance(obj.transform.position, placementPosition) <= PlacementMatchTolerance;
        }

        public static int FindPlacementIndex(List<ObjectPlacement> placements, ObjectPlacement placement)
        {
            if (placements == null || placement == null)
                return -1;

            string identity = ScenarioPropertyBag.GetString(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyAuthoringIdentity);
            string sourceObjectId = ScenarioPropertyBag.GetString(placement.CustomProperties, ScenarioPlacementDefinitions.PropertySourceObjectId);
            Vector3 position = new Vector3(
                placement.Position != null ? placement.Position.X : 0f,
                placement.Position != null ? placement.Position.Y : 0f,
                placement.Position != null ? placement.Position.Z : 0f);

            for (int i = 0; i < placements.Count; i++)
            {
                ObjectPlacement existing = placements[i];
                if (existing == null)
                    continue;

                string existingIdentity = ScenarioPropertyBag.GetString(existing.CustomProperties, ScenarioPlacementDefinitions.PropertyAuthoringIdentity);
                if (!string.IsNullOrEmpty(identity)
                    && !string.IsNullOrEmpty(existingIdentity)
                    && string.Equals(existingIdentity, identity, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }

                string existingSourceObjectId = ScenarioPropertyBag.GetString(existing.CustomProperties, ScenarioPlacementDefinitions.PropertySourceObjectId);
                if (!string.IsNullOrEmpty(sourceObjectId)
                    && !string.IsNullOrEmpty(existingSourceObjectId)
                    && string.Equals(existingSourceObjectId, sourceObjectId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }

                if (!string.Equals(existing.DefinitionReference, placement.DefinitionReference, StringComparison.OrdinalIgnoreCase)
                    || existing.Position == null)
                {
                    continue;
                }

                Vector3 existingPosition = new Vector3(existing.Position.X, existing.Position.Y, existing.Position.Z);
                if (Vector3.Distance(existingPosition, position) <= PlacementMatchTolerance)
                    return i;
            }

            return -1;
        }

        public static bool IsPlacementAtGrid(ObjectPlacement placement, string definitionReference, int gridX, int gridY)
        {
            if (placement == null || !string.Equals(placement.DefinitionReference, definitionReference, StringComparison.OrdinalIgnoreCase))
                return false;

            int placementGridX;
            int placementGridY;
            if (ScenarioPropertyBag.TryGetInt(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyGridX, out placementGridX)
                && ScenarioPropertyBag.TryGetInt(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyGridY, out placementGridY))
            {
                return placementGridX == gridX && placementGridY == gridY;
            }

            return false;
        }

        public static bool ShouldPreserveDuringLiveCapture(ObjectPlacement placement)
        {
            if (placement == null)
                return false;

            ScenarioPlacementDefinitionKind kind;
            if (!ScenarioPlacementDefinitions.TryParseSpecialKind(placement.DefinitionReference, out kind))
                return false;

            return kind == ScenarioPlacementDefinitionKind.Room
                || kind == ScenarioPlacementDefinitionKind.Ladder
                || kind == ScenarioPlacementDefinitionKind.RoomLight;
        }

        public static string GetProperty(List<ScenarioProperty> properties, string key)
        {
            return ScenarioPropertyBag.GetString(properties, key);
        }

        public static void SetProperty(List<ScenarioProperty> properties, string key, string value)
        {
            ScenarioPropertyBag.Set(properties, key, value);
        }

        public static string SafeObjectName(Obj_Base obj)
        {
            if (obj == null)
                return "Unknown Object";

            string name = obj.GetName();
            if (!string.IsNullOrEmpty(name))
                return name;

            if (!string.IsNullOrEmpty(obj.name))
                return obj.name;

            return obj.GetObjectType().ToString();
        }

        private static RoomEdit FindRoomEdit(List<RoomEdit> edits, int gridX, int gridY)
        {
            for (int i = 0; edits != null && i < edits.Count; i++)
            {
                RoomEdit edit = edits[i];
                if (edit != null && edit.GridX == gridX && edit.GridY == gridY)
                    return edit;
            }

            return null;
        }

        private static int CompareRoomEdits(RoomEdit left, RoomEdit right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            int yCompare = left.GridY.CompareTo(right.GridY);
            if (yCompare != 0)
                return yCompare;

            return left.GridX.CompareTo(right.GridX);
        }

        private static int ComparePlacements(ObjectPlacement left, ObjectPlacement right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            int priorityCompare = GetPlacementPriority(left).CompareTo(GetPlacementPriority(right));
            if (priorityCompare != 0)
                return priorityCompare;

            int typeCompare = string.Compare(left.DefinitionReference ?? string.Empty, right.DefinitionReference ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (typeCompare != 0)
                return typeCompare;

            float leftY = left.Position != null ? left.Position.Y : 0f;
            float rightY = right.Position != null ? right.Position.Y : 0f;
            int yCompare = leftY.CompareTo(rightY);
            if (yCompare != 0)
                return yCompare;

            float leftX = left.Position != null ? left.Position.X : 0f;
            float rightX = right.Position != null ? right.Position.X : 0f;
            return leftX.CompareTo(rightX);
        }

        private static int GetPlacementPriority(ObjectPlacement placement)
        {
            ScenarioPlacementDefinitionKind kind;
            if (!ScenarioPlacementDefinitions.TryParseSpecialKind(placement != null ? placement.DefinitionReference : null, out kind))
                return 2;

            switch (kind)
            {
                case ScenarioPlacementDefinitionKind.Room:
                    return 0;
                case ScenarioPlacementDefinitionKind.Ladder:
                case ScenarioPlacementDefinitionKind.RoomLight:
                    return 1;
                default:
                    return 2;
            }
        }
    }
}
