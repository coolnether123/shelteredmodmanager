using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch(typeof(global::OccupationsManager), "ShouldBeWorkingNow")]
    [PatchPolicy(
        PatchDomain.Characters,
        "Paralives Attendance Policies",
        TargetBehavior = "Lets registered runtime policies override whether a character should physically attend a scheduled occupation.",
        FailureMode = "Mods can still edit occupation data, but cannot centrally suppress physical school or work attendance.",
        RollbackStrategy = "Unregister attendance policies or disable this optional policy patch.",
        IsOptional = true)]
    internal static class OccupationsManagerShouldBeWorkingNowPatch
    {
        private static void Postfix(global::AssetCharacter character, int occupationIndex, ref bool __result)
        {
            bool shouldAttend;
            if (ParalivesRuntimeInfo.Current.AttendancePolicies.TryResolve(
                character,
                occupationIndex,
                __result,
                out shouldAttend))
            {
                __result = shouldAttend;
            }
        }
    }
}
