using Cortex.Core.Models;
using Cortex.Presentation.Models;

namespace Cortex.Plugin.Harmony.Services.Editor
{
    internal interface IHarmonyMethodInspectorNavigationActionFactory
    {
        MethodInspectorActionViewModel[] CreatePatchNavigationActions(HarmonyPatchNavigationTarget target, string label, string hint);
    }

    internal sealed class HarmonyMethodInspectorNavigationActionFactory : IHarmonyMethodInspectorNavigationActionFactory
    {
        public MethodInspectorActionViewModel[] CreatePatchNavigationActions(HarmonyPatchNavigationTarget target, string label, string hint)
        {
            var actionId = HarmonyMethodInspectorNavigationActionCodec.Create(target);
            if (string.IsNullOrEmpty(actionId))
            {
                return new MethodInspectorActionViewModel[0];
            }

            return new[]
            {
                new MethodInspectorActionViewModel
                {
                    Id = actionId,
                    Label = string.IsNullOrEmpty(label) ? "Open" : label,
                    Hint = string.IsNullOrEmpty(hint) ? "Open the matching patch method." : hint,
                    Enabled = true
                }
            };
        }
    }
}
