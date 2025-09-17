using System;
using UnityEngine;

namespace ModAPI.Inspector
{
    // Lightweight GL-based bounds highlighter for the currently selected object
    public class BoundsHighlighter : MonoBehaviour
    {
        public static Transform Target;
        public static bool HighlightEnabled = true;
        public static Color LineColor = new Color(1f, 0.85f, 0.1f, 1f); // warm yellow

        private static Material _lineMat;

        private void OnRenderObject()
        {
            if (!HighlightEnabled || Target == null) return;

            bool has;
            var b = HierarchyUtil.ComputeHierarchyRendererBounds(Target, out has);
            if (!has) return;

            EnsureMaterial();
            if (_lineMat == null) return;

            _lineMat.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);
            GL.Color(LineColor);

            DrawBounds(b);

            GL.End();
            GL.PopMatrix();
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
    }
}

