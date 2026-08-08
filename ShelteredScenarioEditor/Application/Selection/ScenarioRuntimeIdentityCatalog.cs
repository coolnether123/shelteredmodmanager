using System;
using System.Collections.Generic;
using UnityEngine;
using ShelteredAPI.Scenarios.Public;

namespace ShelteredScenarioEditor.Application.Selection
{
    internal sealed class ScenarioRuntimeIdentityEntry
    {
        public GameObject GameObject { get; set; }
        public ScenarioRuntimeIdentity Identity { get; set; }
    }

    /// <summary>Editor-side enumeration over the API's encapsulated runtime identity query.</summary>
    internal static class ScenarioRuntimeIdentityCatalog
    {
        public static bool TryGet(GameObject gameObject, out ScenarioRuntimeIdentity identity)
        {
            return ShelteredScenarioRuntime.TryGetRuntimeIdentity(gameObject, out identity);
        }

        public static GameObject FindObjectPlacement(string identity)
        {
            if (string.IsNullOrEmpty(identity)) return null;
            GameObject[] objects = UnityEngine.Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
            for (int i = 0; objects != null && i < objects.Length; i++)
            {
                ScenarioRuntimeIdentity runtimeIdentity;
                if (!TryGet(objects[i], out runtimeIdentity)
                    || runtimeIdentity.Kind != ScenarioRuntimeIdentityKind.ObjectPlacement) continue;
                if (string.Equals(runtimeIdentity.ScenarioObjectId, identity, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(runtimeIdentity.RuntimeBindingKey, identity, StringComparison.OrdinalIgnoreCase)) return objects[i];
            }
            return null;
        }

        public static ScenarioRuntimeIdentityEntry[] ListSceneSpritePlacements()
        {
            List<ScenarioRuntimeIdentityEntry> entries = new List<ScenarioRuntimeIdentityEntry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            GameObject[] objects = UnityEngine.Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
            for (int i = 0; objects != null && i < objects.Length; i++)
            {
                ScenarioRuntimeIdentity identity;
                if (!TryGet(objects[i], out identity) || identity.Kind != ScenarioRuntimeIdentityKind.SceneSpritePlacement) continue;
                if (!string.IsNullOrEmpty(identity.PlacementId) && !seen.Add(identity.PlacementId)) continue;
                entries.Add(new ScenarioRuntimeIdentityEntry { GameObject = objects[i], Identity = identity });
            }
            return entries.ToArray();
        }

        public static GameObject FindSceneSpritePlacement(string placementId)
        {
            ScenarioRuntimeIdentityEntry[] entries = ListSceneSpritePlacements();
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].Identity != null
                    && string.Equals(entries[i].Identity.PlacementId, placementId, StringComparison.OrdinalIgnoreCase)) return entries[i].GameObject;
            return null;
        }
    }
}
