using HarmonyLib;
using ModAPI.Harmony;

namespace ShelteredAPI.Characters.Internal
{
    [PatchPolicy(PatchDomain.Characters, "CharacterAttributes",
        TargetBehavior = "Apply registered WalkSpeed attributes after vanilla speed calculation.",
        FailureMode = "WalkSpeed contributions are ignored.",
        RollbackStrategy = "Disable the Characters patch domain.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(BaseCharacter), "GetWalkSpeed")]
    internal static class CharacterAttributePatches
    {
        private static void Postfix(BaseCharacter __instance, ref float __result)
        {
            BasicCharacterAttributes attributes = CharacterEffectSystem.InternalInstance.FindBoundAttributes(__instance);
            if (attributes != null) __result = attributes.ApplyTo("WalkSpeed", __result);
        }
    }
}
