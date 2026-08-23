using UnityEngine;

namespace ShelteredAPI.Scenarios.Presentation.Selection
{
    /// <summary>
    /// Exposes row organization state to the UI test harness without parsing visible text.
    /// </summary>
    internal sealed class ScenarioBookLibraryHarnessPayload : MonoBehaviour
    {
        public string ScenarioId;
        public string SortMode;
        public bool Pinned;
    }
}
