using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShelteredAPI.Scenarios.Application.Authoring
{
    /// <summary>
    /// Canonical scene-object discovery shared by authoring capture and hierarchy UI.
    /// ObjectManager supplies registered objects; the scene scan adds inactive base
    /// layout objects that Unity's normal FindObjectsOfType path omits.
    /// </summary>
    internal static class ScenarioLiveShelterObjectCatalog
    {
        public static List<Obj_Base> Discover()
        {
            List<Obj_Base> result = new List<Obj_Base>();
            HashSet<int> seen = new HashSet<int>();

            ObjectManager manager = ObjectManager.Instance;
            List<Obj_Base> registered = manager != null ? manager.GetAllObjects() : null;
            for (int i = 0; registered != null && i < registered.Count; i++)
                AddIfSceneObject(result, seen, registered[i], false, null);

            Scene activeScene = SceneManager.GetActiveScene();
            Obj_Base[] loaded = Resources.FindObjectsOfTypeAll<Obj_Base>();
            for (int i = 0; loaded != null && i < loaded.Length; i++)
                AddIfSceneObject(result, seen, loaded[i], true, activeScene.name);

            result.Sort(Compare);
            return result;
        }

        private static void AddIfSceneObject(
            List<Obj_Base> result,
            HashSet<int> seen,
            Obj_Base obj,
            bool requireActiveScene,
            string activeSceneName)
        {
            if (obj == null || obj.gameObject == null)
                return;

            if (requireActiveScene)
            {
                Scene scene = obj.gameObject.scene;
                if (!scene.IsValid()
                    || string.IsNullOrEmpty(scene.name)
                    || !string.Equals(scene.name, activeSceneName, StringComparison.Ordinal))
                {
                    return;
                }
            }

            int instanceId = obj.GetInstanceID();
            if (!seen.Add(instanceId))
                return;

            result.Add(obj);
        }

        private static int Compare(Obj_Base left, Obj_Base right)
        {
            string leftName = left != null && left.gameObject != null
                ? ScenarioWorldObjectDisplayNameResolver.Resolve(left.gameObject, ScenarioAuthoringTargetKind.PlaceableObject)
                : string.Empty;
            string rightName = right != null && right.gameObject != null
                ? ScenarioWorldObjectDisplayNameResolver.Resolve(right.gameObject, ScenarioAuthoringTargetKind.PlaceableObject)
                : string.Empty;
            int byName = string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
            if (byName != 0)
                return byName;
            int leftId = left != null ? left.GetInstanceID() : 0;
            int rightId = right != null ? right.GetInstanceID() : 0;
            return leftId.CompareTo(rightId);
        }
    }
}
