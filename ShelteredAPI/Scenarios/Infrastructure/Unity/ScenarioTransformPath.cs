using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity
{
    /// <summary>
    /// Builds the canonical root-to-leaf identity path for a live Unity transform.
    /// Missing transforms have no path and are represented by an empty string.
    /// </summary>
    internal static class ScenarioTransformPath
    {
        /// <summary>Returns the canonical root-to-leaf path for a live transform.</summary>
        internal static string Build(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }
    }
}
