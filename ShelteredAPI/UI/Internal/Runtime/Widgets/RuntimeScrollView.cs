using System.Collections.Generic;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;
using ShelteredAPI.Content;
namespace ShelteredAPI.UI.Internal.Runtime.Widgets{
    internal static class RuntimeScrollView
    {
        public static NGUIScrollHelper Attach(GameObject owner, List<GameObject> rows, float startY, float itemSpacing, float minY, float maxY, float minX, float maxX)
        {
            if (owner == null)
                return null;

            NGUIScrollHelper helper = owner.GetComponent<NGUIScrollHelper>();
            if (helper == null)
                helper = owner.AddComponent<NGUIScrollHelper>();

            helper.Initialize(rows ?? new List<GameObject>(), startY, itemSpacing, minY, maxY, minX, maxX);
            return helper;
        }
    }
}
