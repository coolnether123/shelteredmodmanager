using System;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Inspector
{
    public static class HierarchyUtil
    {
        public static List<Transform> GetRootTransforms()
        {
            var roots = new List<Transform>();
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<Transform>();
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t != null && t.parent == null && t.gameObject.activeInHierarchy)
                        roots.Add(t);
                }
                roots.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex) { MMLog.WarnOnce("HierarchyUtil.GetRootTransforms", "Error getting root transforms: " + ex.Message); }
            return roots;
        }

        public static IEnumerable<Transform> EnumerateChildren(Transform t)
        {
            if (t == null) yield break;
            for (int i = 0; i < t.childCount; i++)
                yield return t.GetChild(i);
        }

        public static Bounds ComputeHierarchyRendererBounds(Transform root, out bool hasBounds)
        {
            hasBounds = false;
            var combined = new Bounds();
            if (root == null) return combined;

            try
            {
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    if (!hasBounds)
                    {
                        combined = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combined.Encapsulate(r.bounds);
                    }
                }
            }
            catch (Exception ex) { MMLog.WarnOnce("HierarchyUtil.ComputeHierarchyRendererBounds", "Error computing bounds: " + ex.Message); }
            return combined;
        }

        public static string GetTransformPath(Transform t)
        {
            if (t == null) return string.Empty;
            var stack = new Stack<string>();
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", stack.ToArray());
        }
    }
}
