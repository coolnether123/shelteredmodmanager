using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioSpriteRuntimeMutationService
    {
        public static bool TryApply(
            ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget,
            Sprite sprite)
        {
            if (runtimeTarget == null || sprite == null)
                return false;

            if (runtimeTarget.Kind == ScenarioSpriteTargetComponentKind.SpriteRenderer
                && runtimeTarget.SpriteRenderer != null)
            {
                runtimeTarget.SpriteRenderer.sprite = sprite;
                return true;
            }

            if (runtimeTarget.Kind == ScenarioSpriteTargetComponentKind.UI2DSprite
                && runtimeTarget.Ui2DSprite != null)
            {
                runtimeTarget.Ui2DSprite.sprite2D = sprite;
                return true;
            }

            return false;
        }
    }
}
