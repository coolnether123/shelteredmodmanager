using System;
using System.Collections.Generic;
using UnityEngine;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ScenarioObjectPlacementRuntimeBinding : MonoBehaviour
    {
        private const float PlacementMatchTolerance = 0.15f;
        private static readonly List<ScenarioObjectPlacementRuntimeBinding> Bindings = new List<ScenarioObjectPlacementRuntimeBinding>();

        public string ScenarioObjectId;
        public string RuntimeBindingKey;
        public Obj_Base Object;
        public int PlacementIndex = -1;
        public int GridX = -1;
        public int GridY = -1;

        public static ScenarioObjectPlacementRuntimeBinding Attach(GameObject target, ObjectPlacement placement, Obj_Base obj, int placementIndex)
        {
            if (target == null || placement == null)
                return null;

            ScenarioObjectPlacementRuntimeBinding binding = target.GetComponent<ScenarioObjectPlacementRuntimeBinding>();
            if (binding == null)
                binding = target.AddComponent<ScenarioObjectPlacementRuntimeBinding>();

            binding.ScenarioObjectId = placement.ScenarioObjectId;
            binding.RuntimeBindingKey = placement.RuntimeBindingKey;
            binding.Object = obj;
            binding.PlacementIndex = placementIndex;
            binding.GridX = ScenarioPropertyBag.GetInt(
                placement.CustomProperties,
                ScenarioPlacementDefinitions.PropertyGridX,
                -1);
            binding.GridY = ScenarioPropertyBag.GetInt(
                placement.CustomProperties,
                ScenarioPlacementDefinitions.PropertyGridY,
                -1);
            Register(binding);
            return binding;
        }

        public static ScenarioObjectPlacementRuntimeBinding Find(string idOrBindingKey)
        {
            if (string.IsNullOrEmpty(idOrBindingKey))
                return null;

            for (int i = Bindings.Count - 1; i >= 0; i--)
            {
                ScenarioObjectPlacementRuntimeBinding binding = Bindings[i];
                if (binding == null)
                {
                    Bindings.RemoveAt(i);
                    continue;
                }

                if (string.Equals(binding.ScenarioObjectId, idOrBindingKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(binding.RuntimeBindingKey, idOrBindingKey, StringComparison.OrdinalIgnoreCase))
                    return binding;
            }

            return null;
        }

        /// <summary>
        /// Finds the vanilla object restored with an authoring backing save for a
        /// placement record.  Runtime bindings are not serialized by the game, so
        /// the draft's stable source-object id and type/position fallback are both
        /// needed after a scene reload.
        /// </summary>
        public static Obj_Base FindExistingWorldObject(ObjectManager manager, ObjectPlacement placement)
        {
            if (manager == null || placement == null)
                return null;

            List<Obj_Base> worldObjects;
            try
            {
                worldObjects = manager.GetAllObjects();
            }
            catch
            {
                return null;
            }

            for (int i = 0; worldObjects != null && i < worldObjects.Count; i++)
            {
                Obj_Base candidate = worldObjects[i];
                if (MatchesPlacement(placement, candidate))
                    return candidate;
            }

            return null;
        }

        private static bool MatchesPlacement(ObjectPlacement placement, Obj_Base candidate)
        {
            ScenarioObjectPlacementRuntimeBinding binding = candidate.GetComponent<ScenarioObjectPlacementRuntimeBinding>();
            if (binding != null)
            {
                if (!string.IsNullOrEmpty(binding.ScenarioObjectId)
                    && string.Equals(placement.ScenarioObjectId, binding.ScenarioObjectId, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (!string.IsNullOrEmpty(binding.RuntimeBindingKey)
                    && string.Equals(placement.RuntimeBindingKey, binding.RuntimeBindingKey, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            string sourceObjectId = ScenarioPropertyBag.GetString(
                placement.CustomProperties,
                ScenarioPlacementDefinitions.PropertySourceObjectId);
            if (candidate.objectId > 0
                && !string.IsNullOrEmpty(sourceObjectId)
                && string.Equals(sourceObjectId, candidate.objectId.ToString(), StringComparison.OrdinalIgnoreCase))
                return true;

            if (placement.Position == null
                || !string.Equals(placement.DefinitionReference, candidate.GetObjectType().ToString(), StringComparison.OrdinalIgnoreCase))
                return false;

            Vector3 position = new Vector3(placement.Position.X, placement.Position.Y, placement.Position.Z);
            return Vector3.Distance(candidate.transform.position, position) <= PlacementMatchTolerance;
        }

        public static void ApplyActiveState(string idOrBindingKey, bool active)
        {
            ScenarioObjectPlacementRuntimeBinding binding = Find(idOrBindingKey);
            if (binding == null || binding.gameObject == null)
                return;

            if (binding.Object != null)
            {
                if (active)
                    binding.Object.EnableObject();
                else
                    binding.Object.DisableObject();
            }

            binding.gameObject.SetActive(active);
        }

        private void Awake()
        {
            Register(this);
        }

        private void OnDestroy()
        {
            Bindings.Remove(this);
        }

        private static void Register(ScenarioObjectPlacementRuntimeBinding binding)
        {
            if (binding != null && !Bindings.Contains(binding))
                Bindings.Add(binding);
        }
    }
}
