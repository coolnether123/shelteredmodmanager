using UnityEngine;

namespace ShelteredAPI.Scenarios.Presentation.Selection
{
    /// <summary>
    /// Additive reflection seam for the Agent Interface. The visible UI remains the
    /// source of row identity while these fields expose organization state without
    /// requiring text parsing.
    /// </summary>
    internal sealed class ScenarioBookLibraryHarnessPayload : MonoBehaviour
    {
        public string ScenarioId;
        public string SortMode;
        public bool Pinned;
    }
}
