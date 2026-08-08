using UnityEngine;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredScenarioEditor.Presentation.UiKit.Frame{
    /// <summary>
    /// Builds the visual chrome for a scenario authoring window: background
    /// surface, header strip, optional footer strip. Implementations are
    /// responsible only for drawing chrome; they must not contain interaction
    /// or business logic. A caller passes in the outer rect plus titles and
    /// receives <see cref="ScenarioUiWindowRegions"/> describing where to paint
    /// the body content.
    /// </summary>
    internal interface IScenarioUiWindowFrame
    {
        ScenarioUiWindowRegions Build(Rect outer, string title, string subtitle, bool reserveFooter);
        ScenarioUiWindowRegions Build(
            Rect outer,
            string title,
            string subtitle,
            bool reserveFooter,
            float headerHeight,
            float titleRightInset);
    }
}
