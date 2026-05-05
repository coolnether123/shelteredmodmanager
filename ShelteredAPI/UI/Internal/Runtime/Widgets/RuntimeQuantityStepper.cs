using System;
using UnityEngine;
using ShelteredAPI.Content;
namespace ShelteredAPI.UI.Internal.Runtime.Widgets{
    internal static class RuntimeQuantityStepper
    {
        public static GameObject Create(GameObject parent, int value, int min, int max, Vector3 localPosition, int depth, Action<int> onChanged)
        {
            GameObject root = RuntimeWidgetUtil.CreateChild(parent, "QuantityStepper", localPosition);
            if (root == null)
                return null;

            int current = Math.Max(min, Math.Min(max, value));
            RuntimeButton.Create(root, "Minus", "-", 28, 26, new Vector3(-50f, 0f, 0f), depth, delegate
            {
                current = Math.Max(min, current - 1);
                if (onChanged != null) onChanged(current);
            });
            RuntimeWidgetUtil.CreateLabel(root, current.ToString(), 54, 26, 18, Vector3.zero, NGUIText.Alignment.Center, depth + 1);
            RuntimeButton.Create(root, "Plus", "+", 28, 26, new Vector3(50f, 0f, 0f), depth, delegate
            {
                current = Math.Min(max, current + 1);
                if (onChanged != null) onChanged(current);
            });

            return root;
        }
    }
}
