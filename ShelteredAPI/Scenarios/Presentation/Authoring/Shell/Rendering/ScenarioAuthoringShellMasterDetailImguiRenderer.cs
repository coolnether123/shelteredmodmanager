using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        // Slice 1 intentionally establishes only the compiling renderer seam. Slice 2
        // owns visual layout and all interactive controls.
        private Rect DrawWorkspaceBody(
            Rect bodyRect,
            ScenarioAuthoringShellWindowViewModel window)
        {
            if (window == null || window.WorkspaceBody == null)
                return bodyRect;

            return bodyRect;
        }
    }
}
