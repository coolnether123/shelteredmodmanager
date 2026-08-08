using System;
using UnityEngine;

namespace ShelteredScenarioEditor.Presentation.UiKit.Animation
{
    internal struct ScenarioUiGuiScope : IDisposable
    {
        private readonly Color _color;
        private readonly Matrix4x4 _matrix;

        private ScenarioUiGuiScope(Color color, Matrix4x4 matrix)
        {
            _color = color;
            _matrix = matrix;
        }

        public static ScenarioUiGuiScope Apply(float alpha, Rect rect, float scale)
        {
            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color nextColor = previousColor;
            nextColor.a *= Mathf.Clamp01(alpha);
            GUI.color = nextColor;

            if (Mathf.Abs(scale - 1f) > 0.0001f)
            {
                Vector3 pivot = new Vector3(rect.x + (rect.width * 0.5f), rect.y + (rect.height * 0.5f), 0f);
                GUI.matrix = previousMatrix
                    * Matrix4x4.TRS(pivot, Quaternion.identity, Vector3.one)
                    * Matrix4x4.Scale(new Vector3(scale, scale, 1f))
                    * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
            }

            return new ScenarioUiGuiScope(previousColor, previousMatrix);
        }

        public void Dispose()
        {
            GUI.color = _color;
            GUI.matrix = _matrix;
        }
    }
}
