using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
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

            if (!session.DirtyFlags.Contains(ScenarioDirtySection.Bunker))
                session.DirtyFlags.Add(ScenarioDirtySection.Bunker);

            session.CurrentEditCategory = ScenarioEditCategory.Bunker;
            session.HasAppliedToCurrentWorld = true;
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

        public static ObjectPlacement CreatePlacement(Obj_Base obj)
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

        public static int FindPlacementIndex(List<ObjectPlacement> placements, Obj_Base obj)
        {
            if (placements == null || obj == null)
                return -1;

            string objectId = obj.objectId > 0 ? obj.objectId.ToString() : null;
            string definitionReference = obj.GetObjectType().ToString();
            Vector3 position = obj.transform.position;

            for (int i = 0; i < placements.Count; i++)
            {
                ObjectPlacement placement = placements[i];
                if (placement == null)
                    continue;

                string sourceObjectId = GetProperty(placement.CustomProperties, ScenarioPlacementDefinitions.PropertySourceObjectId);
                if (!string.IsNullOrEmpty(objectId)
                    && !string.IsNullOrEmpty(sourceObjectId)
                    && string.Equals(sourceObjectId, objectId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }

                if (!string.Equals(placement.DefinitionReference, definitionReference, StringComparison.OrdinalIgnoreCase)
                    || placement.Position == null)
                {
                    continue;
                }

                Vector3 placementPosition = new Vector3(placement.Position.X, placement.Position.Y, placement.Position.Z);
                if (Vector3.Distance(position, placementPosition) <= PlacementMatchTolerance)
                    return i;
            }

            return -1;
        }

        public static int FindPlacementIndex(List<ObjectPlacement> placements, ObjectPlacement placement)
        {
            if (placements == null || placement == null)
                return -1;

            string identity = GetProperty(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyAuthoringIdentity);
            string sourceObjectId = GetProperty(placement.CustomProperties, ScenarioPlacementDefinitions.PropertySourceObjectId);
            Vector3 position = new Vector3(
                placement.Position != null ? placement.Position.X : 0f,
                placement.Position != null ? placement.Position.Y : 0f,
                placement.Position != null ? placement.Position.Z : 0f);

            for (int i = 0; i < placements.Count; i++)
            {
                ObjectPlacement existing = placements[i];
                if (existing == null)
                    continue;

                string existingIdentity = GetProperty(existing.CustomProperties, ScenarioPlacementDefinitions.PropertyAuthoringIdentity);
                if (!string.IsNullOrEmpty(identity)
                    && !string.IsNullOrEmpty(existingIdentity)
                    && string.Equals(existingIdentity, identity, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }

                string existingSourceObjectId = GetProperty(existing.CustomProperties, ScenarioPlacementDefinitions.PropertySourceObjectId);
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

        public static bool ShouldPreserveDuringLiveCapture(ObjectPlacement placement)
        {
            if (placement == null)
                return false;

            ScenarioPlacementDefinitionKind kind;
            if (!ScenarioPlacementDefinitions.TryParseSpecialKind(placement.DefinitionReference, out kind))
                return false;

            return kind == ScenarioPlacementDefinitionKind.Room
                || kind == ScenarioPlacementDefinitionKind.Ladder;
        }

        public static string GetProperty(List<ScenarioProperty> properties, string key)
        {
            if (properties == null || string.IsNullOrEmpty(key))
                return null;

            for (int i = 0; i < properties.Count; i++)
            {
                ScenarioProperty property = properties[i];
                if (property != null && string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }

            return null;
        }

        public static void SetProperty(List<ScenarioProperty> properties, string key, string value)
        {
            if (properties == null || string.IsNullOrEmpty(key))
                return;

            for (int i = 0; i < properties.Count; i++)
            {
                ScenarioProperty property = properties[i];
                if (property == null || !string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                property.Value = value;
                return;
            }

            properties.Add(new ScenarioProperty
            {
                Key = key,
                Value = value
            });
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
