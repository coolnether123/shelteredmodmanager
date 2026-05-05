using UnityEngine;


using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.UI.FieldManual.Frame
{
    /// <summary>
    /// Builds the visual chrome for a panel (background, borders, title strip).
    /// Implementations are responsible for purely visual concerns; they must not
    /// contain interaction or business logic. Returns named regions the orchestrator
    /// will populate with content.
    /// </summary>
    internal interface IPanelFrame
    {
        PanelFrameRegions Build(GameObject parent, string title, string subtitle);
    }
}
