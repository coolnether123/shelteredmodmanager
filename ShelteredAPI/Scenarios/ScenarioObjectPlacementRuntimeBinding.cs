using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioObjectPlacementRuntimeBinding : MonoBehaviour
    {
        private static readonly List<ScenarioObjectPlacementRuntimeBinding> Bindings = new List<ScenarioObjectPlacementRuntimeBinding>();

        public string ScenarioObjectId;
        public string RuntimeBindingKey;
        public Obj_Base Object;
        public int PlacementIndex = -1;

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
