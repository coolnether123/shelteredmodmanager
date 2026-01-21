using UnityEngine;
using System.Text;
using ModAPI.Inspector;
using ModAPI.Core;

public class HierarchyPrinter : MonoBehaviour
{
    void Awake()
    {
        MMLog.Write("--- Printing Scene Hierarchy ---");
        StringBuilder sb = new StringBuilder();
        
        var roots = HierarchyUtil.GetRootTransforms();
        foreach (var root in roots)
        {
            PrintTransform(root, sb, "");
        }

        MMLog.Write(sb.ToString());
        MMLog.Write("--- End of Scene Hierarchy ---");

        // Destroy this component so it doesn't run again
        Destroy(this);
    }

    private void PrintTransform(Transform t, StringBuilder sb, string indent)
    {
        if (t == null) return;
        sb.AppendLine(indent + t.name + (t.gameObject.activeInHierarchy ? "" : " (inactive)"));

        for (int i = 0; i < t.childCount; i++)
        {
            PrintTransform(t.GetChild(i), sb, indent + "  ");
        }
    }
}
