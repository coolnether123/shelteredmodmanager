using ModAPI.Inspector;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    // Single owner of BoundsHighlighter's hover/selection/secondary transforms. Keeps
    // the selection service free of rendering concerns and gives copy/paste an obvious
    // place to pin its visual source marker.
    internal sealed class ScenarioHoverVisualService
    {
        private static readonly ScenarioHoverVisualService _instance = new ScenarioHoverVisualService();

        public static ScenarioHoverVisualService Instance
        {
            get { return _instance; }
        }

        private ScenarioHoverVisualService()
        {
        }

        public void UpdateFromState(ScenarioAuthoringState state)
        {
            if (state == null)
            {
                BoundsHighlighter.Target = null;
                BoundsHighlighter.HoverTarget = null;
                return;
            }

            BoundsHighlighter.Target = ResolveTransform(state.SelectedTarget);
            BoundsHighlighter.HoverTarget = state.SelectionModeActive
                ? ResolveTransform(state.HoveredTarget)
                : null;
        }

        public void SetSecondary(ScenarioAuthoringTarget target)
        {
            BoundsHighlighter.SecondaryTarget = ResolveTransform(target);
        }

        public void ClearSecondary()
        {
            BoundsHighlighter.SecondaryTarget = null;
        }

        public void Clear()
        {
            BoundsHighlighter.Target = null;
            BoundsHighlighter.HoverTarget = null;
            BoundsHighlighter.SecondaryTarget = null;
        }

        private static Transform ResolveTransform(ScenarioAuthoringTarget target)
        {
            if (target == null)
                return null;

            if (target.HighlightObject != null)
            {
                GameObject highlightGameObject = target.HighlightObject as GameObject;
                if (highlightGameObject != null)
                    return highlightGameObject.transform;

                Component highlightComponent = target.HighlightObject as Component;
                if (highlightComponent != null)
                    return highlightComponent.transform;
            }

            if (target.RuntimeObject == null)
                return null;

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject != null)
                return gameObject.transform;

            Component component = target.RuntimeObject as Component;
            return component != null ? component.transform : null;
        }
    }
}
