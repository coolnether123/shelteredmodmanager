using System;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Inspector
{
    // Lightweight GL-based bounds highlighter for the currently selected object
    public class BoundsHighlighter : MonoBehaviour
    {
        public static Transform Target;
        public static Transform HoverTarget;
        public static Transform SecondaryTarget; // e.g. copy source pinned while user chooses a paste target
        public static bool HighlightEnabled = true;
        public static Color LineColor = new Color(1f, 0.85f, 0.1f, 1f); // selection: warm yellow
        public static Color HoverColor = new Color(0.2f, 0.85f, 1f, 1f); // hover: cyan
        public static Color SecondaryColor = new Color(0.3f, 1f, 0.4f, 1f); // copy source: green

        private static Material _lineMat;
        private static readonly Dictionary<int, Dictionary<ulong, int>> _boundaryEdgeCache = new Dictionary<int, Dictionary<ulong, int>>();

        private void OnRenderObject()
        {
            if (!HighlightEnabled) return;
            if (Target == null && HoverTarget == null && SecondaryTarget == null) return;

            EnsureMaterial();
            if (_lineMat == null) return;

            _lineMat.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);

            // Draw hover first so selection (drawn on top) wins if both point at the
            // same transform.
            if (HoverTarget != null && HoverTarget != Target)
                DrawOutline(HoverTarget, HoverColor);

            if (SecondaryTarget != null && SecondaryTarget != Target && SecondaryTarget != HoverTarget)
                DrawOutline(SecondaryTarget, SecondaryColor);

            if (Target != null)
                DrawOutline(Target, LineColor);

            GL.End();
            GL.PopMatrix();
        }

        private static void DrawOutline(Transform target, Color color)
        {
            GL.Color(color);
            if (TryDrawSpriteOutline(target))
                return;

            bool has;
            Bounds b = HierarchyUtil.ComputeHierarchyRendererBounds(target, out has);
            if (has)
                DrawBounds(b);
        }

        private static void EnsureMaterial()
        {
            if (_lineMat != null) return;
            try
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader != null)
                {
                    _lineMat = new Material(shader);
                    _lineMat.hideFlags = HideFlags.HideAndDontSave;
                    _lineMat.SetInt("_ZWrite", 0);
                    _lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    _lineMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                }
            }
            catch (Exception ex) { MMLog.WarnOnce("BoundsHighlighter.EnsureMaterial", "Error creating line material: " + ex.Message); }
        }

        private static void DrawBounds(Bounds b)
        {
            var c = b.center;
            var e = b.extents;

            // 8 corners
            Vector3 p000 = c + new Vector3(-e.x, -e.y, -e.z);
            Vector3 p001 = c + new Vector3(-e.x, -e.y,  e.z);
            Vector3 p010 = c + new Vector3(-e.x,  e.y, -e.z);
            Vector3 p011 = c + new Vector3(-e.x,  e.y,  e.z);
            Vector3 p100 = c + new Vector3( e.x, -e.y, -e.z);
            Vector3 p101 = c + new Vector3( e.x, -e.y,  e.z);
            Vector3 p110 = c + new Vector3( e.x,  e.y, -e.z);
            Vector3 p111 = c + new Vector3( e.x,  e.y,  e.z);

            // 12 edges
            Line(p000, p001); Line(p001, p101); Line(p101, p100); Line(p100, p000); // bottom rectangle
            Line(p010, p011); Line(p011, p111); Line(p111, p110); Line(p110, p010); // top rectangle
            Line(p000, p010); Line(p001, p011); Line(p101, p111); Line(p100, p110); // verticals
        }

        private static void Line(Vector3 a, Vector3 b)
        {
            GL.Vertex(a);
            GL.Vertex(b);
        }

        private static bool TryDrawSpriteOutline(Transform target)
        {
            if (target == null)
                return false;

            bool drew = false;

            SpriteRenderer[] spriteRenderers = target.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; spriteRenderers != null && i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null || spriteRenderer.sprite == null || !spriteRenderer.enabled)
                    continue;

                drew |= DrawSpriteOutline(spriteRenderer.transform, spriteRenderer.sprite, spriteRenderer.flipX, spriteRenderer.flipY);
            }

            UI2DSprite[] ui2DSprites = target.GetComponentsInChildren<UI2DSprite>(true);
            for (int i = 0; ui2DSprites != null && i < ui2DSprites.Length; i++)
            {
                UI2DSprite ui2DSprite = ui2DSprites[i];
                if (ui2DSprite == null || ui2DSprite.sprite2D == null || !ui2DSprite.enabled)
                    continue;

                drew |= DrawSpriteOutline(ui2DSprite.transform, ui2DSprite.sprite2D, false, false);
            }

            return drew;
        }

        private static bool DrawSpriteOutline(Transform transform, Sprite sprite, bool flipX, bool flipY)
        {
            if (transform == null || sprite == null)
                return false;

            Vector2[] vertices = sprite.vertices;
            ushort[] triangles = sprite.triangles;
            if (vertices == null || triangles == null || vertices.Length == 0 || triangles.Length < 3)
                return false;

            Dictionary<ulong, int> edgeCounts = GetBoundaryEdges(sprite);
            bool drew = false;
            foreach (KeyValuePair<ulong, int> pair in edgeCounts)
            {
                if (pair.Value != 1)
                    continue;

                int startIndex = (int)(pair.Key >> 32);
                int endIndex = (int)(pair.Key & 0xffffffff);
                if (startIndex < 0 || endIndex < 0 || startIndex >= vertices.Length || endIndex >= vertices.Length)
                    continue;

                Vector3 start = transform.TransformPoint(ApplyFlip(vertices[startIndex], flipX, flipY));
                Vector3 end = transform.TransformPoint(ApplyFlip(vertices[endIndex], flipX, flipY));
                Line(start, end);
                drew = true;
            }

            return drew;
        }

        private static Vector3 ApplyFlip(Vector2 vertex, bool flipX, bool flipY)
        {
            return new Vector3(flipX ? -vertex.x : vertex.x, flipY ? -vertex.y : vertex.y, 0f);
        }

        private static Dictionary<ulong, int> GetBoundaryEdges(Sprite sprite)
        {
            int spriteId = sprite.GetInstanceID();
            Dictionary<ulong, int> edgeCounts;
            if (_boundaryEdgeCache.TryGetValue(spriteId, out edgeCounts) && edgeCounts != null)
                return edgeCounts;

            edgeCounts = new Dictionary<ulong, int>();
            ushort[] triangles = sprite.triangles;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                AddEdge(edgeCounts, triangles[i], triangles[i + 1]);
                AddEdge(edgeCounts, triangles[i + 1], triangles[i + 2]);
                AddEdge(edgeCounts, triangles[i + 2], triangles[i]);
            }

            _boundaryEdgeCache[spriteId] = edgeCounts;
            return edgeCounts;
        }

        private static void AddEdge(Dictionary<ulong, int> edgeCounts, int a, int b)
        {
            int min = a < b ? a : b;
            int max = a < b ? b : a;
            ulong key = ((ulong)(uint)min << 32) | (uint)max;

            int count;
            edgeCounts.TryGetValue(key, out count);
            edgeCounts[key] = count + 1;
        }
    }
}

