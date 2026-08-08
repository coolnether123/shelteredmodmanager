using ModAPI.Scenarios;
using ShelteredScenarioEditor.Infrastructure.Assets;
using UnityEngine;

namespace ShelteredScenarioEditor.Application.Authoring
{
    /// <summary>
    /// Application-layer adapter from editor selection state to the infrastructure
    /// appearance service's live family-member contract.
    /// </summary>
    internal static class ScenarioEditorCharacterTargetAdapter
    {
        public static bool TryResolve(
            this ScenarioEditorCharacterAppearanceService appearance,
            ScenarioAuthoringTarget target,
            out ScenarioEditorCharacterAppearanceService.ResolvedCharacterTarget resolved,
            out string message)
        {
            resolved = null;
            message = null;
            if (appearance == null || target == null || target.RuntimeObject == null)
            {
                message = "Character editor requires a live character target.";
                return false;
            }

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject == null)
            {
                Component component = target.RuntimeObject as Component;
                gameObject = component != null ? component.gameObject : null;
            }
            FamilyMember familyMember = gameObject != null
                ? gameObject.GetComponentInParent<FamilyMember>()
                : null;
            if (familyMember == null)
            {
                message = "Character editing currently supports family members only.";
                return false;
            }

            return appearance.TryResolve(
                familyMember,
                target.TransformPath,
                target.DisplayName,
                out resolved,
                out message);
        }
    }
}
