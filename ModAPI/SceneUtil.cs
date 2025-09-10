using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModAPI
{
    /// <summary>
    /// Lightweight helpers for finding scene GameObjects by name or path.
    /// Designed for modders to locate existing UI panels reliably without
    /// keeping static state. Searches the active scene only and only active
    /// objects to avoid pulling in prefabs.
    /// </summary>
    public static class SceneUtil
    {
        /// <summary>
        /// Finds the first active GameObject matching the name in the active scene.
        /// Performs a breadth-first traversal from scene roots.
        /// </summary>
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

        /// <summary>
        /// Finds by slash-separated path from any scene root, e.g., "UIRoot/Panel/Child".
        /// Only navigates active transforms.
        /// </summary>
        public static GameObject FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var parts = path.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            foreach (var root in GetActiveSceneRoots())
            {
                // Optional first element may be a root name; if so, enforce match
                Transform cursor = root;
                int index = 0;
                if (string.Equals(root.name, parts[0], StringComparison.Ordinal))
                {
                    index = 1; // consume the root name
                }
                else if (root.name.StartsWith(parts[0], StringComparison.Ordinal) && parts.Length == 1)
                {
                    // allow single-element name matching a root directly
                    return root.gameObject;
                }
                else
                {
                    // Try to locate a child whose name matches the first element
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

        /// <summary>
        /// Unified entry: if the string contains '/', treats it as a path; otherwise by name.
        /// </summary>
        public static GameObject Find(string nameOrPath)
        {
            if (string.IsNullOrEmpty(nameOrPath)) return null;
            if (nameOrPath.IndexOf('/') >= 0) return FindByPath(nameOrPath);
            return FindByName(nameOrPath);
        }

        // --- Internals -----------------------------------------------------

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
            catch
            {
                // Fallback: legacy approach—enumerate all transforms and pick those with null parent
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
