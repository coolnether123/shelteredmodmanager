using System.Collections.Generic;
using System.Text;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Diagnostics
{
    internal static class FieldManualLayoutDiagnostics
    {
        private const float OverlapEpsilon = 0.5f;

        public static void Log(GameObject root, string phase)
        {
            if (root == null)
            {
                MMLog.WriteInfo("[FieldManualLayout] root=null phase=" + Safe(phase));
                return;
            }

            Transform rootTransform = root.transform;
            var entries = new List<Entry>();
            CollectWidgets(rootTransform, entries);
            CollectColliders(rootTransform, entries);

            var sb = new StringBuilder(4096);
            sb.AppendLine("[FieldManualLayout] BEGIN phase=" + Safe(phase) + " root=" + Path(rootTransform) + " entries=" + entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                sb.AppendLine(string.Format(
                    "[FieldManualLayout] ITEM {0:000} type={1} path={2} localPos={3} rootRect={4} size={5} pivot={6} depth={7} active={8}",
                    i,
                    e.Type,
                    e.Path,
                    Vec(e.LocalPosition),
                    RectText(e.RootRect),
                    Vec(e.Size),
                    e.Pivot,
                    e.Depth,
                    e.Active));
            }

            int overlapCount = 0;
            for (int a = 0; a < entries.Count; a++)
            {
                for (int b = a + 1; b < entries.Count; b++)
                {
                    Rect overlap;
                    if (!TryOverlap(entries[a].RootRect, entries[b].RootRect, out overlap)) continue;
                    overlapCount++;
                    sb.AppendLine(string.Format(
                        "[FieldManualLayout] OVERLAP {0:000}x{1:000} area={2:0.##} rect={3} a={4} b={5}",
                        a,
                        b,
                        overlap.width * overlap.height,
                        RectText(overlap),
                        entries[a].Path,
                        entries[b].Path));
                }
            }

            sb.AppendLine("[FieldManualLayout] END phase=" + Safe(phase) + " overlaps=" + overlapCount);
            MMLog.WriteInfo(sb.ToString());
        }

        private static void CollectWidgets(Transform root, List<Entry> entries)
        {
            UIWidget[] widgets = root.GetComponentsInChildren<UIWidget>(true);
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null) continue;

                Rect rect = RectFromLocalBox(root, widget.transform, widget.width, widget.height, PivotOffset(widget.pivot, widget.width, widget.height));
                entries.Add(new Entry(
                    "UIWidget:" + widget.GetType().Name,
                    Path(widget.transform),
                    widget.transform.localPosition,
                    rect,
                    new Vector2(widget.width, widget.height),
                    widget.pivot.ToString(),
                    widget.depth,
                    widget.gameObject.activeInHierarchy));
            }
        }

        private static void CollectColliders(Transform root, List<Entry> entries)
        {
            BoxCollider[] colliders = root.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider collider = colliders[i];
                if (collider == null) continue;

                Rect rect = RectFromLocalBox(root, collider.transform, collider.size.x, collider.size.y, collider.center);
                entries.Add(new Entry(
                    "BoxCollider",
                    Path(collider.transform),
                    collider.transform.localPosition,
                    rect,
                    new Vector2(collider.size.x, collider.size.y),
                    "Center",
                    0,
                    collider.gameObject.activeInHierarchy));
            }
        }

        private static Rect RectFromLocalBox(Transform root, Transform transform, float width, float height, Vector3 center)
        {
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            Vector3[] corners = new Vector3[4];
            corners[0] = root.InverseTransformPoint(transform.TransformPoint(new Vector3(center.x - halfW, center.y - halfH, center.z)));
            corners[1] = root.InverseTransformPoint(transform.TransformPoint(new Vector3(center.x - halfW, center.y + halfH, center.z)));
            corners[2] = root.InverseTransformPoint(transform.TransformPoint(new Vector3(center.x + halfW, center.y + halfH, center.z)));
            corners[3] = root.InverseTransformPoint(transform.TransformPoint(new Vector3(center.x + halfW, center.y - halfH, center.z)));

            float minX = corners[0].x;
            float maxX = corners[0].x;
            float minY = corners[0].y;
            float maxY = corners[0].y;

            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 c = corners[i];
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.y > maxY) maxY = c.y;
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Vector3 PivotOffset(UIWidget.Pivot pivot, float width, float height)
        {
            switch (pivot)
            {
                case UIWidget.Pivot.TopLeft: return new Vector3(width * 0.5f, -height * 0.5f, 0f);
                case UIWidget.Pivot.Top: return new Vector3(0f, -height * 0.5f, 0f);
                case UIWidget.Pivot.TopRight: return new Vector3(-width * 0.5f, -height * 0.5f, 0f);
                case UIWidget.Pivot.Left: return new Vector3(width * 0.5f, 0f, 0f);
                case UIWidget.Pivot.Right: return new Vector3(-width * 0.5f, 0f, 0f);
                case UIWidget.Pivot.BottomLeft: return new Vector3(width * 0.5f, height * 0.5f, 0f);
                case UIWidget.Pivot.Bottom: return new Vector3(0f, height * 0.5f, 0f);
                case UIWidget.Pivot.BottomRight: return new Vector3(-width * 0.5f, height * 0.5f, 0f);
                case UIWidget.Pivot.Center:
                default:
                    return Vector3.zero;
            }
        }

        private static bool TryOverlap(Rect a, Rect b, out Rect overlap)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float yMax = Mathf.Min(a.yMax, b.yMax);

            if (xMax - xMin <= OverlapEpsilon || yMax - yMin <= OverlapEpsilon)
            {
                overlap = new Rect();
                return false;
            }

            overlap = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        private static string Path(Transform transform)
        {
            if (transform == null) return "<null>";
            var parts = new List<string>();
            Transform t = transform;
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static string RectText(Rect rect)
        {
            return string.Format("x={0:0.##},y={1:0.##},w={2:0.##},h={3:0.##}", rect.x, rect.y, rect.width, rect.height);
        }

        private static string Vec(Vector2 value)
        {
            return string.Format("{0:0.##},{1:0.##}", value.x, value.y);
        }

        private static string Vec(Vector3 value)
        {
            return string.Format("{0:0.##},{1:0.##},{2:0.##}", value.x, value.y, value.z);
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value;
        }

        private struct Entry
        {
            public readonly string Type;
            public readonly string Path;
            public readonly Vector3 LocalPosition;
            public readonly Rect RootRect;
            public readonly Vector2 Size;
            public readonly string Pivot;
            public readonly int Depth;
            public readonly bool Active;

            public Entry(string type, string path, Vector3 localPosition, Rect rootRect, Vector2 size, string pivot, int depth, bool active)
            {
                Type = type;
                Path = path;
                LocalPosition = localPosition;
                RootRect = rootRect;
                Size = size;
                Pivot = pivot;
                Depth = depth;
                Active = active;
            }
        }
    }
}
