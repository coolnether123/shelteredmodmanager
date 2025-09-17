using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModAPI
{
    public static class SceneUtil
    {
        public static GameObject FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var root in GetActiveSceneRoots())
            {
                var found = BfsFind(root, (t) => string.Equals(t.name, name, StringComparison.Ordinal));
                if (found != null) return found.gameObject;
            }
            return null;
        }

        public static GameObject FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var parts = path.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            foreach (var root in GetActiveSceneRoots())
            {
                Transform cursor = root;
                int index = 0;
                if (string.Equals(root.name, parts[0], StringComparison.Ordinal))
                {
                    index = 1;
                }
                else if (root.name.StartsWith(parts[0], StringComparison.Ordinal) && parts.Length == 1)
                {
                    return root.gameObject;
                }
                else
                {
                    cursor = BfsFind(root, (t) => string.Equals(t.name, parts[0], StringComparison.Ordinal));
                    if (cursor == null) continue;
                    index = 1;
                }

                while (cursor != null && index < parts.Length)
                {
                    cursor = FindDirectChild(cursor, parts[index]);
                    index++;
                }
                if (cursor != null && index == parts.Length) return cursor.gameObject;
            }
            return null;
        }

        public static GameObject Find(string nameOrPath)
        {
            if (string.IsNullOrEmpty(nameOrPath)) return null;
            if (nameOrPath.IndexOf('/') >= 0) return FindByPath(nameOrPath);
            return FindByName(nameOrPath);
        }

        public static GameObject FindPanelOrWarn(IPluginContext ctx, string nameOrPath)
        {
            var go = Find(nameOrPath);
            if (go == null)
            {
                var key = "SceneUtil:FindPanel:" + (nameOrPath ?? "");
                MMLog.WarnOnce(key, "Panel not found: '" + nameOrPath + "'");
                try { if (ctx != null) ctx.Log.Warn("Panel not found: '" + nameOrPath + "'"); }
                catch (Exception ex) { MMLog.WarnOnce("SceneUtil.FindPanelOrWarn.Log", "Error logging warning: " + ex.Message); }
            }
            return go;
        }

        public static string GetCurrentSceneName()
        {
            try
            {
                var sceneManagerType = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine");
                if (sceneManagerType != null)
                {
                    var getActiveSceneMethod = sceneManagerType.GetMethod("GetActiveScene", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (getActiveSceneMethod != null)
                    {
                        var activeScene = getActiveSceneMethod.Invoke(null, null);
                        var nameProperty = activeScene.GetType().GetProperty("name");
                        return (string)nameProperty.GetValue(activeScene, null);
                    }
                }
            }
            catch (Exception ex) { MMLog.WarnOnce("SceneUtil.GetCurrentSceneName.Modern", "Error getting modern scene name: " + ex.Message); }

            try
            {
                return Application.loadedLevelName;
            }
            catch (Exception ex) { MMLog.WarnOnce("SceneUtil.GetCurrentSceneName.Legacy", "Error getting legacy scene name: " + ex.Message); }

            return null;
        }

        public static bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return string.Equals(GetCurrentSceneName(), sceneName, StringComparison.Ordinal);
        }

        private static IEnumerable<Transform> GetActiveSceneRoots()
        {
            var results = new List<Transform>();
            try
            {
                var scene = SceneManager.GetActiveScene();
                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    var go = roots[i];
                    if (go != null && go.activeInHierarchy) results.Add(go.transform);
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("SceneUtil.GetActiveSceneRoots.Modern", "Error getting modern scene roots: " + ex.Message);
                // Fallback for older Unity versions or specific scene loading scenarios
                // where SceneManager might not be fully initialized or accessible.
                var all = UnityEngine.Object.FindObjectsOfType<Transform>();
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t != null && t.parent == null && t.gameObject.activeInHierarchy)
                        results.Add(t);
                }
            }

            for (int i = 0; i < results.Count; i++)
                yield return results[i];
        }

        private static Transform BfsFind(Transform root, Predicate<Transform> match)
        {
            if (root == null || match == null) return null;
            var q = new Queue<Transform>();
            q.Enqueue(root);
            while (q.Count > 0)
            {
                var t = q.Dequeue();
                if (t == null) continue;
                if (!t.gameObject.activeInHierarchy) continue;
                if (match(t)) return t;
                for (int i = 0; i < t.childCount; i++)
                    q.Enqueue(t.GetChild(i));
            }
            return null;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName)) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var ch = parent.GetChild(i);
                if (ch != null && ch.gameObject.activeInHierarchy && string.Equals(ch.name, childName, StringComparison.Ordinal))
                    return ch;
            }
            return null;
        }
    }
}
