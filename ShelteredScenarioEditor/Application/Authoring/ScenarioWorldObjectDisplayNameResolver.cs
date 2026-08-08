using System;
using UnityEngine;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal static class ScenarioWorldObjectDisplayNameResolver
    {
        public static GameObject ResolveLogicalRoot(GameObject source)
        {
            if (source == null)
                return null;

            for (Transform current = source.transform; current != null; current = current.parent)
            {
                if (IsShelterEntrance(current))
                    return current.gameObject;
            }

            return null;
        }

        public static string Resolve(GameObject gameObject, ScenarioAuthoringTargetKind fallbackKind)
        {
            if (gameObject == null)
                return fallbackKind.ToString();

            Obj_Base shelterObject = gameObject.GetComponent<Obj_Base>();
            if (shelterObject != null)
            {
                string objectName = shelterObject.GetName();
                if (!string.IsNullOrEmpty(objectName))
                    return objectName;
            }

            GameObject entrance = ResolveLogicalRoot(gameObject);
            if (entrance != null || IsShelterEntrance(gameObject.transform))
                return "Bunker Entrance";

            return !string.IsNullOrEmpty(gameObject.name) ? gameObject.name : fallbackKind.ToString();
        }

        private static bool IsShelterEntrance(Transform transform)
        {
            if (transform == null
                || !string.Equals(transform.name, "entrance", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (Transform current = transform.parent; current != null; current = current.parent)
            {
                if (string.Equals(current.name, "shelter_grid", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
